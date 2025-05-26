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

    // Optimally, there's 1 alive impostor and at least 2 alive crewmates in everyone's POV.
    // We force this to prevent black screens after meetings.
    // If there are only 2 alive players, we revive 1 additional crewmate.
    public static void SetOptimalRoleTypesToPreventBlackScreen()
    {
        if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default || PlayerControl.AllPlayerControls.Count <= 2) return;

        SkipTasks = true;
        CachedRoleMap = StartGameHostPatch.RpcSetRoleReplacer.RoleMap.ToDictionary(x => (x.Key.SeerID, x.Key.TargetID), x => (x.Value.RoleType, x.Value.CustomRole));

        PlayerControl[] players = Main.AllAlivePlayerControls;
        if (CheckForEndVotingPatch.TempExiledPlayer != null) players = players.Where(x => x.PlayerId != CheckForEndVotingPatch.TempExiledPlayer.PlayerId).ToArray();
        PlayerControl dummyImp = players.MinBy(x => x.PlayerId);

        if (players.Length == 2)
        {
            PlayerControl revived = Main.AllPlayerControls.Where(x => !x.IsAlive() && !x.Data.Disconnected && x.PlayerId != CheckForEndVotingPatch.TempExiledPlayer?.PlayerId).MinBy(x => x.PlayerId);
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
            if (!pc.Data.IsDead && !pc.Data.Disconnected && !pc.IsAlive())
            {
                pc.Data.IsDead = true;
                pc.Data.MarkDirty();
                AmongUsClient.Instance.SendAllStreamedObjects();
            }
        }

        // Reset the role types for all players.
        foreach (((byte seerId, byte targetId), (RoleTypes roleType, CustomRoles _)) in CachedRoleMap)
        {
            PlayerControl seer = seerId.GetPlayer();
            PlayerControl target = targetId.GetPlayer();
            if (seer == null || target == null) continue;

            if (target.IsAlive()) target.RpcSetRoleDesync(roleType, seer.OwnerId);
            else target.RpcSetRoleDesync(target.HasGhostRole() ? RoleTypes.GuardianAngel : roleType is RoleTypes.Impostor or RoleTypes.Shapeshifter or RoleTypes.Phantom ? RoleTypes.ImpostorGhost : RoleTypes.CrewmateGhost, seer.OwnerId);
        }

        // Reset the role map to the original values.
        StartGameHostPatch.RpcSetRoleReplacer.RoleMap = CachedRoleMap.ToDictionary(x => (x.Key.SeerID, x.Key.TargetID), x => (x.Value.RoleType, x.Value.CustomRole));
        CachedRoleMap = [];

        LateTask.New(() =>
        {
            foreach (PlayerControl pc in Main.AllPlayerControls)
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

            // Only execute AfterMeetingTasks after everything is reset.
            LateTask.New(() =>
            {
                SkipTasks = false;
                ExileControllerWrapUpPatch.AfterMeetingTasks();
            }, Utils.CalculatePingDelay() * 2f, "Reset SkipTasks after SetRealPlayerRoles");
        }, 0.2f, "SetRealPlayerRoles - Reset Cooldowns");
    }

    public static void Reset()
    {
        Logger.Info("==Reset==", "AntiBlackout");
        CachedRoleMap = [];
        SkipTasks = false;
    }
}