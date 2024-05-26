using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EHR.Roles.Crewmate
{
    using static Options;
    using static Translator;

    public class Bloodhound : RoleBase
    {
        private const int Id = 6400;
        private static List<byte> PlayerIdList = [];

        public static List<byte> UnreportablePlayers = [];

        public static OptionItem ArrowsPointingToDeadBody;
        public static OptionItem UseLimitOpt;
        public static OptionItem LeaveDeadBodyUnreportable;
        public static OptionItem NotifyKiller;
        public static OptionItem BloodhoundAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;

        private List<byte> BloodhoundTargets = [];

        public override bool IsEnable => PlayerIdList.Count > 0;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Bloodhound);
            ArrowsPointingToDeadBody = BooleanOptionItem.Create(Id + 10, "BloodhoundArrowsPointingToDeadBody", false, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound]);
            LeaveDeadBodyUnreportable = BooleanOptionItem.Create(Id + 11, "BloodhoundLeaveDeadBodyUnreportable", false, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound]);
            NotifyKiller = BooleanOptionItem.Create(Id + 14, "BloodhoundNotifyKiller", false, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound]);
            UseLimitOpt = IntegerOptionItem.Create(Id + 12, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound])
                .SetValueFormat(OptionFormat.Times);
            BloodhoundAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 13, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound])
                .SetValueFormat(OptionFormat.Times);
            AbilityChargesWhenFinishedTasks = FloatOptionItem.Create(Id + 15, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Init()
        {
            PlayerIdList = [];
            UnreportablePlayers = [];
            BloodhoundTargets = [];
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(UseLimitOpt.GetInt());
            BloodhoundTargets = [];
        }

        public override void OnReportDeadBody()
        {
            foreach (byte id in PlayerIdList)
            {
                TargetArrow.RemoveAllTarget(id);
                LocateArrow.RemoveAllTarget(id);
            }

            BloodhoundTargets.Clear();
        }

        public static void OnPlayerDead(PlayerControl target)
        {
            if (!ArrowsPointingToDeadBody.GetBool()) return;

            foreach (byte id in PlayerIdList)
            {
                var player = Utils.GetPlayerById(id);
                if (player == null || !player.IsAlive()) continue;

                var pos = target.Pos();
                LocateArrow.Add(id, pos);
            }
        }

        public override bool CheckReportDeadBody(PlayerControl pc, GameData.PlayerInfo target, PlayerControl killer)
        {
            if (killer != null)
            {
                if (BloodhoundTargets.Contains(killer.PlayerId))
                {
                    return false;
                }

                var pos = target.Object.Pos();
                LocateArrow.Remove(pc.PlayerId, pos);

                if (pc.GetAbilityUseLimit() >= 1)
                {
                    BloodhoundTargets.Add(killer.PlayerId);
                    TargetArrow.Add(pc.PlayerId, killer.PlayerId);

                    pc.Notify(GetString("BloodhoundTrackRecorded"));
                    pc.RpcRemoveAbilityUse();

                    if (LeaveDeadBodyUnreportable.GetBool())
                    {
                        UnreportablePlayers.Add(target.PlayerId);
                    }

                    if (NotifyKiller.GetBool()) killer.Notify(GetString("BloodhoundKillerNotify"));
                }
                else
                {
                    pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
                }
            }
            else
            {
                pc.Notify(GetString("BloodhoundNoTrack"));
            }

            return false;
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool m = false)
        {
            if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;
            if (GameStates.IsMeeting) return string.Empty;
            if (Main.PlayerStates[seer.PlayerId].Role is not Bloodhound bh) return string.Empty;

            return bh.BloodhoundTargets.Count > 0 ? bh.BloodhoundTargets.Select(targetId => TargetArrow.GetArrows(seer, targetId)).Aggregate(string.Empty, (current, arrow) => current + Utils.ColorString(seer.GetRoleColor(), arrow)) : Utils.ColorString(Color.white, LocateArrow.GetArrows(seer));
        }
    }
}