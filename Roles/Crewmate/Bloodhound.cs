using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Roles.Crewmate
{
    using static Options;
    using static Translator;

    public class Bloodhound : RoleBase
    {
        private const int Id = 6400;
        private static List<byte> playerIdList = [];

        public static List<byte> UnreportablePlayers = [];
        public List<byte> BloodhoundTargets = [];

        public static OptionItem ArrowsPointingToDeadBody;
        public static OptionItem UseLimitOpt;
        public static OptionItem LeaveDeadBodyUnreportable;
        public static OptionItem NotifyKiller;
        public static OptionItem BloodhoundAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Bloodhound);
            ArrowsPointingToDeadBody = BooleanOptionItem.Create(Id + 10, "BloodhoundArrowsPointingToDeadBody", false, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound]);
            LeaveDeadBodyUnreportable = BooleanOptionItem.Create(Id + 11, "BloodhoundLeaveDeadBodyUnreportable", false, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound]);
            NotifyKiller = BooleanOptionItem.Create(Id + 14, "BloodhoundNotifyKiller", false, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound]);
            UseLimitOpt = IntegerOptionItem.Create(Id + 12, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound])
                .SetValueFormat(OptionFormat.Times);
            BloodhoundAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 13, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound])
                .SetValueFormat(OptionFormat.Times);
            AbilityChargesWhenFinishedTasks = FloatOptionItem.Create(Id + 15, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Init()
        {
            playerIdList = [];
            UnreportablePlayers = [];
            BloodhoundTargets = [];
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(UseLimitOpt.GetInt());
            BloodhoundTargets = [];
        }

        public override bool IsEnable => playerIdList.Count > 0;

        static void SendRPC(byte playerId, bool add, Vector3 loc = new())
        {
            if (!Utils.DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetBloodhoundArrow, SendOption.Reliable);
            writer.Write(playerId);
            writer.Write(add);
            if (add)
            {
                writer.Write(loc.x);
                writer.Write(loc.y);
                writer.Write(loc.z);
            }

            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            byte playerId = reader.ReadByte();
            if (Main.PlayerStates[playerId].Role is not Bloodhound) return;

            bool add = reader.ReadBoolean();
            if (add) LocateArrow.Add(playerId, new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
            else LocateArrow.RemoveAllTarget(playerId);
        }

        public override void OnReportDeadBody()
        {
            foreach (byte apc in playerIdList)
            {
                LocateArrow.RemoveAllTarget(apc);
                SendRPC(apc, false);
            }

            BloodhoundTargets.Clear();
        }

        public static void OnPlayerDead(PlayerControl target)
        {
            if (!ArrowsPointingToDeadBody.GetBool()) return;

            foreach (byte pc in playerIdList)
            {
                var player = Utils.GetPlayerById(pc);
                if (player == null || !player.IsAlive())
                    continue;
                LocateArrow.Add(pc, target.transform.position);
                SendRPC(pc, true, target.transform.position);
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

                LocateArrow.Remove(pc.PlayerId, target.Object.transform.position);
                SendRPC(pc.PlayerId, false);

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

        public static string GetTargetArrow(PlayerControl seer, PlayerControl target = null)
        {
            if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;
            if (GameStates.IsMeeting) return string.Empty;
            if (Main.PlayerStates[seer.PlayerId].Role is not Bloodhound bh) return string.Empty;

            return bh.BloodhoundTargets.Count > 0 ? bh.BloodhoundTargets.Select(targetId => TargetArrow.GetArrows(seer, targetId)).Aggregate(string.Empty, (current, arrow) => current + Utils.ColorString(seer.GetRoleColor(), arrow)) : Utils.ColorString(Color.white, LocateArrow.GetArrows(seer));
        }
    }
}