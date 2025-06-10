using System;
using HarmonyLib;
using InnerNet;

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
            InnerNetClientFixedUpdatePatch.Postfix();

            if (ShipStatus.Instance)
            {
                ShipFixedUpdatePatch.Postfix();
                ShipStatusFixedUpdatePatch.Postfix(ShipStatus.Instance);
            }

            if (LobbyBehaviour.Instance)
                LobbyFixedUpdatePatch.Postfix();

            bool lobby = GameStates.IsLobby;

            if (lobby || (Main.IntroDestroyed && !GameStates.IsMeeting && !ExileController.Instance && !AntiBlackout.SkipTasks))
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
                            break;
                        case CustomGameMode.FFA:
                            FreeForAll.FixedUpdatePatch.Postfix();
                            break;
                        case CustomGameMode.KingOfTheZones:
                            KingOfTheZones.FixedUpdatePatch.Postfix();
                            break;
                        case CustomGameMode.NaturalDisasters:
                            NaturalDisasters.FixedUpdatePatch.Postfix();
                            break;
                        case CustomGameMode.Quiz:
                            Quiz.FixedUpdatePatch.Postfix();
                            break;
                        case CustomGameMode.RoomRush:
                            RoomRush.FixedUpdatePatch.Postfix();
                            break;
                    }
                }
                catch (Exception e) { Utils.ThrowException(e); }
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }
}