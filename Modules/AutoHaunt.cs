using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.GameMode.HideAndSeekRoles;
using UnityEngine;

namespace EHR.Modules;

public static class AutoHaunt
{
    private static PlayerControl GetPreferredHauntTarget()
    {
        IEnumerable<PlayerControl> validPCs = Main.AllAlivePlayerControls.Where(x => !AFKDetector.PlayerData.ContainsKey(x.PlayerId));

        return Options.CurrentGameMode switch
        {
            CustomGameMode.Standard => validPCs.OrderByDescending(x => x.GetCustomRole() is CustomRoles.Workaholic or CustomRoles.Snitch).ThenByDescending(x => x.Is(CustomRoleTypes.Coven)).ThenByDescending(x => x.IsNeutralKiller()).ThenByDescending(x => x.IsImpostor()).ThenByDescending(x => x.GetCustomRole().IsNeutral()).FirstOrDefault(),
            CustomGameMode.SoloKombat => validPCs.Where(x => x.SoloAlive()).MinBy(x => SoloPVP.GetRankFromScore(x.PlayerId)),
            CustomGameMode.FFA => validPCs.MaxBy(x => FreeForAll.KillCount.GetValueOrDefault(x.PlayerId, 0)),
            CustomGameMode.MoveAndStop => validPCs.MaxBy(x => x.GetTaskState().CompletedTasksCount),
            CustomGameMode.HotPotato => HotPotato.GetState().HolderID.GetPlayer(),
            CustomGameMode.HideAndSeek => validPCs.OrderByDescending(x => x.GetCustomRole() is CustomRoles.Venter or CustomRoles.Dasher).ThenByDescending(x => CustomHnS.PlayerRoles.TryGetValue(x.PlayerId, out (IHideAndSeekRole Interface, CustomRoles Role) info) && info.Interface.Team == Team.Impostor).ThenByDescending(x => CustomHnS.PlayerRoles.TryGetValue(x.PlayerId, out (IHideAndSeekRole Interface, CustomRoles Role) info) && info.Interface.Team == Team.Neutral).FirstOrDefault(),
            CustomGameMode.Speedrun => Speedrun.CanKill.Count > 0 ? Speedrun.CanKill.ToValidPlayers().RandomElement() : validPCs.MaxBy(x => x.GetTaskState().CompletedTasksCount),
            CustomGameMode.CaptureTheFlag => validPCs.OrderByDescending(x => CaptureTheFlag.IsCarrier(x.PlayerId)).ThenByDescending(x => CaptureTheFlag.GetFlagTime(x.PlayerId)).ThenByDescending(x => CaptureTheFlag.GetTagCount(x.PlayerId)).FirstOrDefault(),
            CustomGameMode.RoomRush => RoomRush.PointsSystem ? validPCs.MaxBy(x => RoomRush.GetPoints(x.PlayerId)) : validPCs.RandomElement(),
            CustomGameMode.KingOfTheZones => validPCs.MaxBy(x => KingOfTheZones.GetZoneTime(x.PlayerId)),
            CustomGameMode.Deathrace => validPCs.MaxBy(x => Deathrace.Data.TryGetValue(x.PlayerId, out var drData) ? drData.Lap : 0),
            _ => validPCs.RandomElement()
        };
    }

    public static void Start()
    {
        Main.Instance.StartCoroutine(AutoHauntCoroutine());
        return;

        IEnumerator AutoHauntCoroutine()
        {
            while (Main.AutoHaunt.Value)
            {
                if (GameStates.IsInTask && !ExileController.Instance && !AntiBlackout.SkipTasks && !PlayerControl.LocalPlayer.IsAlive() && PlayerControl.LocalPlayer.Data.RoleType is RoleTypes.CrewmateGhost or RoleTypes.ImpostorGhost && !ExtendedPlayerControl.TempExiled.Contains(PlayerControl.LocalPlayer.PlayerId))
                {
                    if (HauntMenuMinigameStartPatch.Instance != null)
                    {
                        PlayerControl currentTarget = HauntMenuMinigameStartPatch.Instance.HauntTarget;
                        PlayerControl preferredTarget = GetPreferredHauntTarget();

                        if (preferredTarget != null && currentTarget != preferredTarget)
                            HauntMenuMinigameSetHauntTargetPatch.Prefix(HauntMenuMinigameStartPatch.Instance, preferredTarget);
                    }
                    else if (HudManager.InstanceExists)
                    {
                        HudManager.Instance.AbilityButton.DoClick();
                    }
                }

                yield return new WaitForSeconds(5f);
            }

            if (GameStates.IsInTask && !ExileController.Instance && !AntiBlackout.SkipTasks && !PlayerControl.LocalPlayer.IsAlive() && HauntMenuMinigameStartPatch.Instance != null)
                HauntMenuMinigameSetHauntTargetPatch.Prefix(HauntMenuMinigameStartPatch.Instance, null);
        }
    }
}