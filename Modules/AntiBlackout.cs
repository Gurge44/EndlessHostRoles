using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Patches;
using Hazel;

namespace EHR;

public static class AntiBlackout
{
    public static bool SkipTasks;
    private static Dictionary<(byte SeerID, byte TargetID), (RoleTypes RoleType, CustomRoles CustomRole)> CachedRoleMap = [];

    // Optimally, there's 1 living impostor and at least 2 living crewmates in everyone's POV.
    // We force this to prevent black screens after meetings.
    public static void SetOptimalRoleTypesToPreventBlackScreen()
    {
        // If there are only 2 or fewer players in the game in total, there's nothing we can do.
        if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default || PlayerControl.AllPlayerControls.Count <= 2) return;

        SkipTasks = true;
        CachedRoleMap = StartGameHostPatch.RpcSetRoleReplacer.RoleMap.ToDictionary(x => (x.Key.SeerID, x.Key.TargetID), x => (x.Value.RoleType, x.Value.CustomRole));

        PlayerControl[] players = Main.AllAlivePlayerControls;
        if (CheckForEndVotingPatch.TempExiledPlayer != null) players = players.Where(x => x.PlayerId != CheckForEndVotingPatch.TempExiledPlayer.PlayerId).ToArray();
        PlayerControl dummyImp = players.MinBy(x => x.PlayerId);

        // There are only 2 players alive. We need to revive 1 dead player to have 2 living crewmates.
        if (players.Length == 2)
        {
            PlayerControl revived = Main.AllPlayerControls.Where(x => !x.IsAlive() && !x.Data.Disconnected && x.PlayerId != CheckForEndVotingPatch.TempExiledPlayer?.PlayerId).MinBy(x => x.PlayerId);

            // The black screen cannot be prevented if there are no players to revive in this case.
            if (revived == null)
            {
                // Fix the black screen manually for each player after the ejection screen.
                if (CheckForEndVotingPatch.TempExiledPlayer != null) CheckForEndVotingPatch.TempExiledPlayer.Object.FixBlackScreen();
                players.Do(x => x.FixBlackScreen());
                return;
            }

            revived.Data.IsDead = false;
            revived.Data.MarkDirty();
            AmongUsClient.Instance.SendAllStreamedObjects();
            revived.RpcChangeRoleBasis(CustomRoles.CrewmateEHR, forced: true);
        }

        dummyImp.RpcChangeRoleBasis(CustomRoles.ImpostorEHR, forced: true);
        players.Without(dummyImp).Do(x => x.RpcChangeRoleBasis(CustomRoles.CrewmateEHR, forced: true));
    }

    // After the ejection screen, we revert the role types to their actual values.
    public static void RevertToActualRoleTypes()
    {
        if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default || CachedRoleMap.Count == 0) return;

        // Set the temporarily revived crewmate back to dead.
        foreach (PlayerControl pc in Main.AllPlayerControls)
        {
            try
            {
                if (pc.Data != null && !pc.Data.IsDead && !pc.Data.Disconnected && !pc.IsAlive())
                {
                    pc.Data.IsDead = true;
                    pc.Data.MarkDirty();
                    AmongUsClient.Instance.SendAllStreamedObjects();
                }
            }
            catch (Exception e) { Utils.ThrowException(e); }
        }

        // Reset the role types for all players.
        foreach (((byte seerId, byte targetId), (RoleTypes roleType, CustomRoles _)) in CachedRoleMap)
        {
            try
            {
                PlayerControl seer = seerId.GetPlayer();
                PlayerControl target = targetId.GetPlayer();
                if (seer == null || target == null) continue;

                if (target.IsAlive()) target.RpcSetRoleDesync(roleType, seer.OwnerId);
                else target.RpcSetRoleDesync(target.HasGhostRole() ? RoleTypes.GuardianAngel : roleType is RoleTypes.Impostor or RoleTypes.Shapeshifter or RoleTypes.Phantom ? RoleTypes.ImpostorGhost : RoleTypes.CrewmateGhost, seer.OwnerId);
            }
            catch (Exception e) { Utils.ThrowException(e); }
        }

        // Reset the role map to the original values.
        StartGameHostPatch.RpcSetRoleReplacer.RoleMap = CachedRoleMap.ToDictionary(x => (x.Key.SeerID, x.Key.TargetID), x => (x.Value.RoleType, x.Value.CustomRole));
        CachedRoleMap = [];

        LateTask.New(() =>
        {
            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                try
                {
                    if (pc.IsAlive())
                    {
                        // Due to the role base change, we need to reset the cooldowns for abilities.
                        pc.RpcResetAbilityCooldown();
                        pc.SetKillCooldown();
                    }
                    else
                    {
                        // Ensure that the players who are considered dead by the mod are actually dead in the game.
                        pc.Exiled();
                        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(pc.NetId, (byte)RpcCalls.Exiled, SendOption.Reliable);
                        AmongUsClient.Instance.FinishRpcImmediately(writer);
                    }
                }
                catch (Exception e) { Utils.ThrowException(e); }
            }

            // Only execute AfterMeetingTasks after everything is reset.
            LateTask.New(() =>
            {
                SkipTasks = false;
                ExileControllerWrapUpPatch.AfterMeetingTasks();
            }, Math.Max(1f, Utils.CalculatePingDelay() * 2f), "Reset SkipTasks after SetRealPlayerRoles");
        }, 0.2f, "SetRealPlayerRoles - Reset Cooldowns");
    }

    public static void Reset()
    {
        Logger.Info("==Reset==", "AntiBlackout");
        CachedRoleMap = [];
        SkipTasks = false;
    }
}