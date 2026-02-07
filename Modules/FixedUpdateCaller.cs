using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EHR.Gamemodes;
using EHR.Patches;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace EHR.Modules;

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.FixedUpdate))]
public static class FixedUpdateCaller
{
    private static int NonLowLoadPlayerIndex;

    private static long LastFileLoadTS;
    private static long LastAutoMessageSendTS;

    private static long LastMeasureTS;

    // ReSharper disable once UnusedMember.Global
    public static void Postfix()
    {
        try
        {
            long now = Utils.TimeStamp;
            Stopwatch stopwatch = Stopwatch.StartNew();
            bool measure = false;

            if (LastMeasureTS != now)
            {
                LastMeasureTS = now;
                measure = true;
            }
            
            InnerNetClientFixedUpdatePatch.Postfix();
            if (measure) Logger.Warn($"Elapsed: {stopwatch.ElapsedMilliseconds} ms (1)", "debug");

            var amongUsClient = AmongUsClient.Instance;

            var shipStatus = ShipStatus.Instance;

            if (shipStatus)
            {
                ShipFixedUpdatePatch.Postfix();
                if (measure) Logger.Warn($"Elapsed: {stopwatch.ElapsedMilliseconds} ms (2)", "debug");
                ShipStatusFixedUpdatePatch.Postfix(shipStatus);
                if (measure) Logger.Warn($"Elapsed: {stopwatch.ElapsedMilliseconds} ms (3)", "debug");
            }

            var lobbyBehaviour = LobbyBehaviour.Instance;

            if (lobbyBehaviour)
            {
                LobbyFixedUpdatePatch.Postfix();
                if (measure) Logger.Warn($"Elapsed: {stopwatch.ElapsedMilliseconds} ms (4)", "debug");
                LobbyBehaviourUpdatePatch.Postfix(lobbyBehaviour);
                if (measure) Logger.Warn($"Elapsed: {stopwatch.ElapsedMilliseconds} ms (5)", "debug");

                //long now = Utils.TimeStamp;

                if (now - LastFileLoadTS > 10)
                {
                    LastFileLoadTS = now;
                    Options.LoadUserData();
                    if (measure) Logger.Warn($"Elapsed: {stopwatch.ElapsedMilliseconds} ms (6)", "debug");
                }

                if (Options.EnableAutoMessage.GetBool() && now - LastAutoMessageSendTS > Options.AutoMessageSendInterval.GetInt())
                {
                    LastAutoMessageSendTS = now;
                    TemplateManager.SendTemplate("Notification", sendOption: SendOption.None);
                    if (measure) Logger.Warn($"Elapsed: {stopwatch.ElapsedMilliseconds} ms (7)", "debug");
                }
            }

            if (measure) Logger.Warn($"Elapsed: {stopwatch.ElapsedMilliseconds} ms (8)", "debug");
            if (HudManager.InstanceExists)
            {
                HudManager hudManager = HudManager.Instance;

                if (hudManager)
                {
                    HudManagerPatch.Postfix(hudManager);
                    if (measure) Logger.Warn($"Elapsed: {stopwatch.ElapsedMilliseconds} ms (9)", "debug");
                    Zoom.Postfix();
                    if (measure) Logger.Warn($"Elapsed: {stopwatch.ElapsedMilliseconds} ms (10)", "debug");
                    HudSpritePatch.Postfix(hudManager);
                    if (measure) Logger.Warn($"Elapsed: {stopwatch.ElapsedMilliseconds} ms (11)", "debug");
                }
            }

            if (measure) Logger.Warn($"Elapsed: {stopwatch.ElapsedMilliseconds} ms (12)", "debug");
            try
            {
                foreach (byte key in EAC.TimeSinceLastTaskCompletion.Keys.ToArray())
                    EAC.TimeSinceLastTaskCompletion[key] += Time.fixedDeltaTime;
            }
            catch (Exception e) { Utils.ThrowException(e); }

            if (measure) Logger.Warn($"Elapsed: {stopwatch.ElapsedMilliseconds} ms (13)", "debug");
            if (!PlayerControl.LocalPlayer) return;

            if (amongUsClient.IsGameStarted)
                Utils.CountAlivePlayers();
            if (measure) Logger.Warn($"Elapsed: {stopwatch.ElapsedMilliseconds} ms (14)", "debug");

            try
            {
                if (HudManager.InstanceExists && GameStates.IsInTask && !ExileController.Instance && !AntiBlackout.SkipTasks && PlayerControl.LocalPlayer.CanUseKillButton())
                {
                    Predicate<PlayerControl> predicate = amongUsClient.AmHost
                        ? Options.CurrentGameMode switch
                        {
                            CustomGameMode.BedWars => BedWars.IsNotInLocalPlayersTeam,
                            CustomGameMode.CaptureTheFlag => CaptureTheFlag.IsNotInLocalPlayersTeam,
                            CustomGameMode.KingOfTheZones => KingOfTheZones.IsNotInLocalPlayersTeam,
                            _ => _ => true
                        }
                        : _ => true;

                    List<PlayerControl> players = PlayerControl.LocalPlayer.GetPlayersInAbilityRangeSorted(predicate);
                    PlayerControl closest = players.Count == 0 ? null : players[0];

                    KillButton killButton = HudManager.Instance.KillButton;

                    if (killButton.currentTarget && killButton.currentTarget != closest)
                        killButton.currentTarget.ToggleHighlight(false, RoleTeamTypes.Impostor);

                    killButton.currentTarget = closest;

                    if (killButton.currentTarget)
                    {
                        killButton.currentTarget.ToggleHighlight(true, RoleTeamTypes.Impostor);
                        killButton.SetEnabled();
                    }
                    else
                        killButton.SetDisabled();
                }
            }
            catch { }
            if (measure) Logger.Warn($"Elapsed: {stopwatch.ElapsedMilliseconds} ms (15)", "debug");

            try
            {
                if (amongUsClient.AmHost && GameStates.InGame && !GameStates.IsEnded)
                    FixedUpdatePatch.LoversSuicide();
            }
            catch (Exception e) { Utils.ThrowException(e); }
            if (measure) Logger.Warn($"Elapsed: {stopwatch.ElapsedMilliseconds} ms (16)", "debug");

            bool lobby = GameStates.IsLobby;

            if (lobby || (Main.IntroDestroyed && GameStates.InGame && !GameStates.IsMeeting && !ExileController.Instance && !AntiBlackout.SkipTasks))
            {
                if (measure) Logger.Warn($"Elapsed: {stopwatch.ElapsedMilliseconds} ms (17)", "debug");
                NonLowLoadPlayerIndex++;

                int count = PlayerControl.AllPlayerControls.Count;

                if (NonLowLoadPlayerIndex >= count)
                    NonLowLoadPlayerIndex = Math.Min(0, -(30 - count));

                CustomGameMode currentGameMode = Options.CurrentGameMode;
                if (measure) Logger.Warn($"Elapsed: {stopwatch.ElapsedMilliseconds} ms (18)", "debug");

                for (var index = 0; index < count; index++)
                {
                    if (measure) Logger.Warn($"Elapsed: {stopwatch.ElapsedMilliseconds} ms (19)", "debug");
                    try
                    {
                        PlayerControl pc = PlayerControl.AllPlayerControls[index];

                        if (!pc || pc.PlayerId >= 254) continue;

                        FixedUpdatePatch.Postfix(pc, NonLowLoadPlayerIndex != index);

                        if (lobby) continue;

                        switch (currentGameMode)
                        {
                            case CustomGameMode.CaptureTheFlag:
                                CaptureTheFlag.FixedUpdatePatch.Postfix(pc);
                                break;
                            case CustomGameMode.HotPotato:
                                HotPotato.FixedUpdatePatch.Postfix(pc);
                                break;
                            case CustomGameMode.StopAndGo:
                                StopAndGo.FixedUpdatePatch.Postfix(pc);
                                break;
                            case CustomGameMode.SoloPVP:
                                SoloPVP.FixedUpdatePatch.Postfix(pc);
                                break;
                            case CustomGameMode.Speedrun:
                                Speedrun.FixedUpdatePatch.Postfix(pc);
                                break;
                            case CustomGameMode.BedWars:
                                BedWars.FixedUpdatePatch.Postfix(pc);
                                break;
                            case CustomGameMode.Snowdown:
                                Snowdown.FixedUpdatePatch.Postfix(pc);
                                break;
                        }

                        CheckInvalidMovementPatch.Postfix(pc);
                    }
                    catch (Exception e) { Utils.ThrowException(e); }
                }
                if (measure) Logger.Warn($"Elapsed: {stopwatch.ElapsedMilliseconds} ms (20)", "debug");

                if (lobby) return;

                try
                {
                    switch (currentGameMode)
                    {
                        case CustomGameMode.HideAndSeek:
                            CustomHnS.FixedUpdatePatch.Postfix();
                            goto default;
                        case CustomGameMode.FFA:
                            FreeForAll.FixedUpdatePatch.Postfix();
                            goto default;
                        case CustomGameMode.KingOfTheZones:
                            KingOfTheZones.FixedUpdatePatch.Postfix();
                            goto default;
                        case CustomGameMode.NaturalDisasters:
                            NaturalDisasters.FixedUpdatePatch.Postfix();
                            break;
                        case CustomGameMode.Quiz:
                            Quiz.FixedUpdatePatch.Postfix();
                            goto default;
                        case CustomGameMode.RoomRush:
                            RoomRush.FixedUpdatePatch.Postfix();
                            goto default;
                        case CustomGameMode.Deathrace:
                            Deathrace.FixedUpdatePatch.Postfix();
                            goto default;
                        case CustomGameMode.Mingle:
                            Mingle.FixedUpdatePatch.Postfix();
                            goto default;
                        default:
                            if (Options.IntegrateNaturalDisasters.GetBool()) goto case CustomGameMode.NaturalDisasters;
                            break;
                    }
                }
                catch (Exception e) { Utils.ThrowException(e); }
                if (measure) Logger.Warn($"Elapsed: {stopwatch.ElapsedMilliseconds} ms (21)", "debug");

                try
                {
                    if (amongUsClient.AmHost && Options.EnableGameTimeLimit.GetBool())
                    {
                        Main.GameTimer += Time.fixedDeltaTime;
                        
                        if (Main.GameTimer > Options.GameTimeLimit.GetInt() && Options.CurrentGameMode is CustomGameMode.Standard or CustomGameMode.NaturalDisasters)
                        {
                            Main.GameTimer = 0f;
                            Main.GameEndDueToTimer = true;
                            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.None);
                        
                            if (Options.CurrentGameMode == CustomGameMode.NaturalDisasters)
                                CustomWinnerHolder.WinnerIds.UnionWith(Main.EnumerateAlivePlayerControls().Select(x => x.PlayerId));
                        }
                    }
                }
                catch (Exception e) { Utils.ThrowException(e); }
                if (measure) Logger.Warn($"Elapsed: {stopwatch.ElapsedMilliseconds} ms (22)", "debug");
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }
}