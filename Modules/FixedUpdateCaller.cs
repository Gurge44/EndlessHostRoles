using System;
using System.Linq;
using EHR.Gamemodes;
using EHR.Patches;
using HarmonyLib;
using InnerNet;

namespace EHR.Modules;

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.FixedUpdate))]
public static class FixedUpdateCaller
{
    private static int NonLowLoadPlayerId;

    private static long Now;
    private static long LastFileLoadTS;
    private static long LastAutoMessageSendTS;

    private static AmongUsClient AmongUsClient;
    private static LobbyBehaviour LobbyBehaviour;
    private static HudManager HudManager;

    private static Predicate<PlayerControl> Predicate;

    // ReSharper disable once UnusedMember.Global
    public static void Postfix()
    {
        try
        {
            AmongUsClient = AmongUsClient.Instance;
            LobbyBehaviour = LobbyBehaviour.Instance;

            if (LobbyBehaviour)
            {
                LobbyFixedUpdatePatch.Postfix();
                LobbyBehaviourUpdatePatch.Postfix(LobbyBehaviour);

                Now = Utils.TimeStamp;

                if (Now - LastFileLoadTS > 10)
                {
                    LastFileLoadTS = Now;
                    Options.LoadUserData();
                }

                if (Options.EnableAutoMessage.GetBool() && Now - LastAutoMessageSendTS > Options.AutoMessageSendInterval.GetInt())
                {
                    LastAutoMessageSendTS = Now;
                    TemplateManager.SendTemplate("Notification", importance: MessageImportance.Low);
                }
            }

            if (HudManager.InstanceExists)
            {
                HudManager = HudManager.Instance;

                HudManagerPatch.Postfix(HudManager);
                Zoom.Postfix();
                HudSpritePatch.Postfix(HudManager);
            }

            if (!PlayerControl.LocalPlayer) return;

            if (AmongUsClient.IsGameStarted)
                Utils.CountAlivePlayers();

            try
            {
                if (HudManager.InstanceExists && GameStates.IsInTask && !ExileController.Instance && !AntiBlackout.SkipTasks && PlayerControl.LocalPlayer.CanUseKillButton())
                {

                    Predicate = AmongUsClient.AmHost
                        ? Options.CurrentGameMode switch
                        {
                            CustomGameMode.BedWars => BedWars.IsNotInLocalPlayersTeam,
                            CustomGameMode.CaptureTheFlag => CaptureTheFlag.IsNotInLocalPlayersTeam,
                            CustomGameMode.KingOfTheZones => KingOfTheZones.IsNotInLocalPlayersTeam,
                            _ => _ => true
                        }
                        : _ => true;

                    PlayerControl closest = FastVector2.TryGetClosestPlayerInRangeTo(PlayerControl.LocalPlayer, GameManager.Instance.LogicOptions.GetKillDistance(), out PlayerControl closestPlayer, Predicate) ? closestPlayer : null;

                    KillButton killButton = HudManager.KillButton;

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
                if (AmongUsClient.AmHost && GameStates.InGame && !GameStates.IsEnded)
                    FixedUpdatePatch.LoversSuicide();
            }
            catch (Exception e) { Utils.ThrowException(e); }

            bool lobby = GameStates.IsLobby;

            if (lobby || (Main.IntroDestroyed && GameStates.InGame && !GameStates.IsMeeting && !ExileController.Instance && !AntiBlackout.SkipTasks))
            {
                NonLowLoadPlayerId++;

                var players = Main.CachedAllPlayerControls();
                int playerCount = players.Count;

                if (NonLowLoadPlayerId >= playerCount)
                    NonLowLoadPlayerId = Math.Min(0, -(30 - playerCount));

                CustomGameMode currentGameMode = Options.CurrentGameMode;
                //bool vanilla = GameStates.CurrentServerType == GameStates.ServerType.Vanilla;

                for (byte playerId = 0; playerId < playerCount; playerId++)
                {
                    try
                    {
                        PlayerControl pc = players[playerId];
                        if (!pc || pc.PlayerId >= 254) continue;

                        FixedUpdatePatch.Postfix(pc, NonLowLoadPlayerId != playerId);

                        if (lobby) continue;

                        //if (vanilla && NonLowLoadPlayerId == playerId)
                        //    Utils.NotifyRoles(SpecifySeer: pc, ForceLoop: true, SendOption: SendOption.None);

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
                    if (AmongUsClient.AmHost && Main.GameTimer.IsRunning && Options.EnableGameTimeLimit.GetBool() && Main.GameTimer.Elapsed.TotalSeconds > Options.GameTimeLimit.GetInt() && Options.CurrentGameMode is CustomGameMode.Standard or CustomGameMode.NaturalDisasters)
                    {
                        Main.GameTimer.Reset();
                        Main.GameEndDueToTimer = true;
                        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.None);

                        if (Options.CurrentGameMode == CustomGameMode.NaturalDisasters)
                        {
                            var alivePlayers = Main.CachedAlivePlayerControls();
                            for (int i = 0; i < alivePlayers.Count; i++)
                                CustomWinnerHolder.WinnerIds.Add(alivePlayers[i].PlayerId);
                        }
                    }
                }
                catch (Exception e) { Utils.ThrowException(e); }
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }
}