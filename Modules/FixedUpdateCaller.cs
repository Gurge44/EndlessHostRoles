using System;
using System.Collections.Generic;
using EHR.Patches;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace EHR.Modules;

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.FixedUpdate))]
public static class FixedUpdateCaller
{
    private static int NonLowLoadPlayerIndex;

    // ReSharper disable once UnusedMember.Global
    public static void Postfix()
    {
        try
        {
            PingTrackerUpdatePatch.LastFPS.Add(1.0f / Time.deltaTime);
            if (PingTrackerUpdatePatch.LastFPS.Count > 10) PingTrackerUpdatePatch.LastFPS.RemoveAt(0);
            
            InnerNetClientFixedUpdatePatch.Postfix();

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
            }

            HudManager hudManager = FastDestroyableSingleton<HudManager>.Instance;

            if (hudManager)
            {
                HudManagerPatch.Postfix(hudManager);
                Zoom.Postfix();
                HudSpritePatch.Postfix(hudManager);
            }

            if (!PlayerControl.LocalPlayer) return;

            try
            {
                if (GameStates.IsInTask && !ExileController.Instance && !AntiBlackout.SkipTasks && PlayerControl.LocalPlayer.CanUseKillButton())
                {
                    List<PlayerControl> players = PlayerControl.LocalPlayer.GetPlayersInAbilityRangeSorted();
                    PlayerControl closest = players.Count == 0 ? null : players[0];

                    KillButton killButton = hudManager.KillButton;

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

            bool lobby = GameStates.IsLobby;

            if (lobby || (Main.IntroDestroyed && GameStates.InGame && !GameStates.IsMeeting && !ExileController.Instance && !AntiBlackout.SkipTasks))
            {
                NonLowLoadPlayerIndex++;

                if (NonLowLoadPlayerIndex >= PlayerControl.AllPlayerControls.Count)
                    NonLowLoadPlayerIndex = 0;

                CustomGameMode currentGameMode = Options.CurrentGameMode;

                for (var index = 0; index < PlayerControl.AllPlayerControls.Count; index++)
                {
                    try
                    {
                        PlayerControl pc = PlayerControl.AllPlayerControls[index];

                        if (pc.PlayerId >= 254) continue;

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
                            case CustomGameMode.MoveAndStop:
                                MoveAndStop.FixedUpdatePatch.Postfix(pc);
                                break;
                            case CustomGameMode.SoloKombat:
                                SoloPVP.FixedUpdatePatch.Postfix(pc);
                                break;
                            case CustomGameMode.Speedrun:
                                Speedrun.FixedUpdatePatch.Postfix(pc);
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
                        default:
                            if (Options.IntegrateNaturalDisasters.GetBool()) goto case CustomGameMode.NaturalDisasters;
                            break;
                    }
                }
                catch (Exception e) { Utils.ThrowException(e); }
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }
}