using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Crewmate;
using EHR.Patches;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace EHR.Modules;

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.FixedUpdate))]
public static class FixedUpdateCaller
{
    private static int NonLowLoadPlayerIndex;

    private static long LastFileLoadTS;
    private static long LastAutoMessageSendTS;

    // ReSharper disable once UnusedMember.Global
    public static void Postfix()
    {
        try
        {
            InnerNetClientFixedUpdatePatch.Postfix();

            var amongUsClient = AmongUsClient.Instance;

            var shipStatus = ShipStatus.Instance;

            if (shipStatus)
            {
                ShipFixedUpdatePatch.Postfix();
                ShipStatusFixedUpdatePatch.Postfix(shipStatus);
            }

            var lobbyBehaviour = LobbyBehaviour.Instance;

            if (lobbyBehaviour)
            {
                LobbyFixedUpdatePatch.Postfix();
                LobbyBehaviourUpdatePatch.Postfix(lobbyBehaviour);

                long now = Utils.TimeStamp;

                if (now - LastFileLoadTS > 10)
                {
                    LastFileLoadTS = now;
                    Options.LoadUserData();
                }

                if (Options.EnableAutoMessage.GetBool() && now - LastAutoMessageSendTS > Options.AutoMessageSendInterval.GetInt())
                {
                    LastAutoMessageSendTS = now;
                    TemplateManager.SendTemplate("Notification", sendOption: Hazel.SendOption.None);
                }
            }

#if ANDROID

            if (GameStartManager.InstanceExists)
                GameStartManagerPatch.GameStartManagerUpdatePatch.Postfix_ManualCall(GameStartManager.Instance);
#endif

            if (HudManager.InstanceExists)
            {
                HudManager hudManager = HudManager.Instance;

                if (hudManager)
                {
                    HudManagerPatch.Postfix(hudManager);
                    Zoom.Postfix();
                    HudSpritePatch.Postfix(hudManager);
                }
            }

            try
            {
                foreach (byte key in EAC.TimeSinceLastTaskCompletion.Keys.ToArray())
                    EAC.TimeSinceLastTaskCompletion[key] += Time.fixedDeltaTime;
            }
            catch (Exception e) { Utils.ThrowException(e); }

            if (!PlayerControl.LocalPlayer) return;

            if (amongUsClient.IsGameStarted)
                Utils.CountAlivePlayers();

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

            try
            {
                if (amongUsClient.AmHost && GameStates.InGame && !GameStates.IsEnded)
                    FixedUpdatePatch.LoversSuicide();
            }
            catch (Exception e) { Utils.ThrowException(e); }

            bool lobby = GameStates.IsLobby;

            if (lobby || (Main.IntroDestroyed && GameStates.InGame && !GameStates.IsMeeting && !ExileController.Instance && !AntiBlackout.SkipTasks))
            {
                NonLowLoadPlayerIndex++;

                int count = PlayerControl.AllPlayerControls.Count;

                if (NonLowLoadPlayerIndex >= count)
                    NonLowLoadPlayerIndex = Math.Min(0, -(30 - count));

                CustomGameMode currentGameMode = Options.CurrentGameMode;

                for (var index = 0; index < count; index++)
                {
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
                                CustomWinnerHolder.WinnerIds.UnionWith(Main.AllAlivePlayerControls.Select(x => x.PlayerId));
                        }
                    }
                }
                catch (Exception e) { Utils.ThrowException(e); }
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }
}