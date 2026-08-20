using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Patches;
using EHR.Roles;
using Hazel;
using UnityEngine;

namespace EHR;

public static class AntiBlackout
{
    public static bool SkipTasks;
    public static bool AllowSyncSettings;
    private static Dictionary<(byte SeerID, byte TargetID), (RoleTypes RoleType, CustomRoles CustomRole)> CachedRoleMap = [];

    // Optimally, there's 1 living impostor and at least 2 living crewmates in everyone's POV.
    // We force this to prevent black screens after meetings.
    public static void SetOptimalRoleTypes()
    {
        // If there are only 2 or fewer players in the game in total, there's nothing we can do.
        if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default || PlayerControl.AllPlayerControls.Count <= 2) return;

        SkipTasks = true;
        CachedRoleMap = StartGameHostPatch.RpcSetRoleReplacer.RoleMap.ToDictionary(x => (x.Key.SeerID, x.Key.TargetID), x => (x.Value.RoleType, x.Value.CustomRole));

        var players = Main.AllAlivePlayerControlsToArray;
        if (CheckForEndVotingPatch.TempExiledPlayer) players = players.Where(x => x.PlayerId != CheckForEndVotingPatch.TempExiledPlayer.PlayerId).ToArray();
        PlayerControl dummyImp = players.OrderByDescending(x => x.GetCustomRole() is not (CustomRoles.DetectiveEHR or CustomRoles.Detective) && !x.Is(CustomRoles.Examiner)).ThenByDescending(x => x.IsModdedClient()).MinBy(x => x.PlayerId);

        if (players.Length == 2)
        {
            // There are only 2 players alive. We need to revive 1 dead player to have 2 living crewmates.
            PlayerControl revived = Main.EnumeratePlayerControls().Where(x => !x.IsAlive() && !x.Data.Disconnected && x != CheckForEndVotingPatch.TempExiledPlayer?.Object).MaxBy(x => x.PlayerId);

            // The black screen cannot be prevented if there are no players to revive in this case.
            if (!revived)
            {
                // Fix the black screen manually for each player after the ejection screen.
                if (CheckForEndVotingPatch.TempExiledPlayer) CheckForEndVotingPatch.TempExiledPlayer.Object.FixBlackScreen();
                players.Do(x => x.FixBlackScreen());

                // Don't skip tasks since we couldn't set the optimal roles.
                SkipTasks = false;
                CachedRoleMap = [];
                return;
            }

            revived.RpcSetRoleGlobal(RoleTypes.Crewmate);
        }

        dummyImp.RpcSetRoleGlobal(RoleTypes.Impostor);
        players.Without(dummyImp).Where(x => x.GetCustomRole() is not (CustomRoles.DetectiveEHR or CustomRoles.Detective) && !x.Is(CustomRoles.Examiner)).Do(x => x.RpcSetRoleGlobal(RoleTypes.Crewmate));
        
        Main.EnumeratePlayerControls().DoIf(x => !x.IsAlive() && x.Data && x.Data.IsDead && (!x.AmOwner || !Utils.TempReviveHostRunning), x => x.RpcSetRoleGlobal(GhostRolesManager.AssignedGhostRoles.TryGetValue(x.PlayerId, out var ghostRole) ? ghostRole.Instance.RoleTypes : RoleTypes.CrewmateGhost));
    }

    // After the ejection screen, we revert the role types to their actual values.
    public static void RevertToActualRoleTypes()
    {
        if (CachedRoleMap.Count == 0 || GameStates.IsEnded)
        {
            SkipTasks = false;
            ExileControllerWrapUpPatch.AfterMeetingTasks();
            return;
        }

        // Reset the role types for all players.
        // First group all entries by target.
        foreach (var targetGroup in CachedRoleMap.GroupBy(x => x.Key.TargetID))
        {
            try
            {
                byte targetId = targetGroup.Key;
                PlayerControl target = targetId.GetPlayer();
                if (!target) continue;

                // Compute the role every seer should see.
                Dictionary<byte, RoleTypes> rolesForSeers = [];

                foreach (var entry in targetGroup)
                {
                    byte seerId = entry.Key.SeerID;

                    RoleTypes role = target.IsAlive() && !Main.AfterMeetingDeathPlayers.ContainsKey(targetId) && Main.LastVotedPlayerInfo != target.Data
                        ? entry.Value.RoleType
                        : GhostRolesManager.AssignedGhostRoles.TryGetValue(targetId, out var ghostRole)
                            ? ghostRole.Instance.RoleTypes
                            : seerId == targetId &&
                              !(target.Is(CustomRoleTypes.Impostor) && Options.DeadImpCantSabotage.GetBool()) &&
                              Main.PlayerStates.TryGetValue(targetId, out var state) &&
                              state.Role.CanUseSabotage(target)
                                ? RoleTypes.ImpostorGhost
                                : RoleTypes.CrewmateGhost;

                    rolesForSeers[seerId] = role;
                }

                // First set them to the role they're most commonly seen as.
                RoleTypes globalRole = rolesForSeers.GroupBy(x => x.Value).MaxBy(g => g.Count()).Key;
                target.RpcSetRoleGlobal(globalRole);

                LateTask.New(() =>
                {
                    // Only send desync RPCs where needed. Often this will just be 1 additional RPC or none.
                    foreach ((byte seerId, RoleTypes roleTypes) in rolesForSeers)
                    {
                        try
                        {
                            if (roleTypes == globalRole) continue;

                            PlayerControl seer = seerId.GetPlayer();

                            if (!seer || (seerId == targetId && seer.AmOwner && Utils.TempReviveHostRunning))
                                continue;

                            target.RpcSetRoleDesync(roleTypes, seer.OwnerId);
                        }
                        catch (Exception e) { Utils.ThrowException(e); }
                    }
                }, 0.2f, "Set Desync Roles", log: false);
            }
            catch (Exception e) { Utils.ThrowException(e); }
        }

        // Reset the role map to the original values.
        StartGameHostPatch.RpcSetRoleReplacer.RoleMap = CachedRoleMap.ToDictionary(x => (x.Key.SeerID, x.Key.TargetID), x => (x.Value.RoleType, x.Value.CustomRole));
        CachedRoleMap = [];

        LateTask.New(() =>
        {
            List<PlayerControl> rpcGuardAndKill = [];
            var elapsedSeconds = (int)ExileControllerWrapUpPatch.Stopwatch.Elapsed.TotalSeconds;
            var senderPacked = CustomRpcSender.Create("AntiBlackout Packed Sender", SendOption.Reliable).StartPackedMessage();
            var senderToAll = CustomRpcSender.Create("Exile Dead Players After Meeting", SendOption.Reliable);
            var hasValuePacked = false;
            var hasValueToAll = false;
            
            foreach (PlayerControl pc in Main.EnumeratePlayerControls())
            {
                try
                {
                    if (pc.OwnerId < 0) continue;
                    
                    if (pc.IsAlive())
                    {
                        // Due to the role base change, we need to reset the cooldowns for abilities.
                        if (!Utils.ShouldNotApplyAbilityCooldownAfterMeeting(pc))
                        {
                            if (senderPacked.RpcResetAbilityCooldown(pc))
                                hasValuePacked = true;
                        }

                        float time = -1f;

                        if (Main.AllPlayerKillCooldown.TryGetValue(pc.PlayerId, out float kcd))
                        {
                            time = kcd - elapsedSeconds;
                            if (time <= 0) continue;
                        }

                        if (!Mathf.Approximately(time, -1f) && Committed.ReduceKCD != null && Committed.ReduceKCD.TryGetValue(pc.PlayerId, out float reduction))
                        {
                            time -= reduction;
                            if (time <= 0) continue;
                        }

                        Logger.Info($"{pc.GetNameWithRole()}'s KCD set to {(time < 0f ? Main.AllPlayerKillCooldown[pc.PlayerId] : time)}s", "SetKCD");

                        if (pc.GetCustomRole().UsesPetInsteadOfKill())
                        {
                            if (time < 0f)
                                pc.AddKCDAsAbilityCD();
                            else
                                pc.AddAbilityCD((int)Math.Round(time));

                            if (pc.GetCustomRole() is not CustomRoles.Necromancer and not CustomRoles.Deathknight and not CustomRoles.Renegade and not CustomRoles.Sidekick) continue;
                        }

                        pc.AddKillTimerToDict(cd: time);

                        if (time >= 0f)
                            Main.AllPlayerKillCooldown[pc.PlayerId] = time * 2;
                        else
                            Main.AllPlayerKillCooldown[pc.PlayerId] *= 2;

                        if (pc.Is(CustomRoles.Glitch) && Main.PlayerStates[pc.PlayerId].Role is Glitch gc)
                        {
                            gc.LastKill = Utils.TimeStamp + ((int)(time / 2) - Glitch.KillCooldown.GetInt());
                            gc.KCDTimer = (int)(time / 2);
                        }
                        else if (!pc.IsModdedClient() || !Options.DisableShieldAnimations.GetBool())
                        {
                            pc.MarkDirtySettings();
                            rpcGuardAndKill.Add(pc);
                        }
                        else
                        {
                            time = Main.AllPlayerKillCooldown[pc.PlayerId] / 2;

                            if (pc.AmOwner)
                                PlayerControl.LocalPlayer.SetKillTimer(time);
                            else
                            {
                                senderPacked.AutoStartRpc(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetKillTimer, pc.OwnerId);
                                senderPacked.Write(time);
                                senderPacked.EndRpc();
                                hasValuePacked = true;
                            }
                        }

                        if (pc.GetCustomRole() is not CustomRoles.Inhibitor and not CustomRoles.Saboteur)
                        {
                            LateTask.New(() =>
                            {
                                pc.ResetKillCooldown(sync: false);
                                pc.MarkDirtySettings();
                            }, 0.3f, log: false);
                        }
                    }
                    else
                    {
                        if (pc.AmOwner && Utils.TempReviveHostRunning) continue;

                        // Ensure that the players who are considered dead by the mod are actually dead in the game.
                        senderToAll.RpcExiled(pc);
                        hasValueToAll = true;

                        if (GhostRolesManager.AssignedGhostRoles.TryGetValue(pc.PlayerId, out var ghostRole) && ghostRole.Instance.RoleTypes == RoleTypes.GuardianAngel)
                            pc.AddAbilityCD(ghostRole.Instance.Cooldown);
                    }
                }
                catch (Exception e) { Utils.ThrowException(e); }
            }

            AllowSyncSettings = true;
            PlayerGameOptionsSender.SendAllImmediately();
            AllowSyncSettings = false;
            
            rpcGuardAndKill.ForEach(pc => hasValuePacked |= senderPacked.RpcGuardAndKill(pc, fromSetKCD: true));

            LateTask.New(() => senderPacked.SendMessage(dispose: !hasValuePacked), 0.1f);
            
            senderToAll.SendMessage(dispose: !hasValueToAll);

            // Only execute AfterMeetingTasks after everything is reset.
            LateTask.New(() =>
            {
                SkipTasks = false;
                ExileControllerWrapUpPatch.AfterMeetingTasks();
            }, 1f, "Reset SkipTasks after SetRealPlayerRoles");
        }, 0.4f, "SetRealPlayerRoles - Reset Cooldowns");
    }

    public static void Reset()
    {
        Logger.Info("==Reset==", "AntiBlackout");
        CachedRoleMap = [];
        SkipTasks = false;
    }
}