namespace TOHE.Roles.Crewmate
{
    using Hazel;
    using System.Collections.Generic;
    using UnityEngine;
    using static TOHE.Options;
    using static TOHE.Translator;

    public static class Bloodhound
    {
        private static readonly int Id = 6400;
        private static List<byte> playerIdList = [];

        public static List<byte> UnreportablePlayers = [];
        public static Dictionary<byte, List<byte>> BloodhoundTargets = [];

        public static OptionItem ArrowsPointingToDeadBody;
        public static OptionItem UseLimitOpt;
        public static OptionItem LeaveDeadBodyUnreportable;
        public static OptionItem NotifyKiller;
        public static OptionItem BloodhoundAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Bloodhound);
            ArrowsPointingToDeadBody = BooleanOptionItem.Create(Id + 10, "BloodhoundArrowsPointingToDeadBody", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound]);
            LeaveDeadBodyUnreportable = BooleanOptionItem.Create(Id + 11, "BloodhoundLeaveDeadBodyUnreportable", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound]);
            NotifyKiller = BooleanOptionItem.Create(Id + 14, "BloodhoundNotifyKiller", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound]);
            UseLimitOpt = IntegerOptionItem.Create(Id + 12, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound])
            .SetValueFormat(OptionFormat.Times);
            BloodhoundAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 13, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound])
            .SetValueFormat(OptionFormat.Times);
            AbilityChargesWhenFinishedTasks = FloatOptionItem.Create(Id + 15, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound])
                .SetValueFormat(OptionFormat.Times);
        }
        public static void Init()
        {
            playerIdList = [];
            UseLimit = [];
            UnreportablePlayers = [];
            BloodhoundTargets = [];
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            UseLimit.Add(playerId, UseLimitOpt.GetInt());
            BloodhoundTargets.Add(playerId, []);

        }
        public static bool IsEnable => playerIdList.Count > 0;

        private static void SendRPC(byte playerId, bool add, Vector3 loc = new())
        {
            if (!IsEnable || !Utils.DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetBloodhoundArrow, SendOption.Reliable, -1);
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
            bool add = reader.ReadBoolean();
            if (add)
                LocateArrow.Add(playerId, new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
            else
                LocateArrow.RemoveAllTarget(playerId);
        }

        public static void SendRPCPlus(byte playerId)
        {
            if (!IsEnable || !Utils.DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.BloodhoundIncreaseAbilityUseByOne, SendOption.Reliable, -1);
            writer.Write(playerId);
            writer.Write(UseLimit[playerId]);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPCPlus(MessageReader reader)
        {
            if (AmongUsClient.Instance.AmHost) return;

            byte playerId = reader.ReadByte();
            float uses = reader.ReadSingle();
            UseLimit[playerId] = uses;
        }

        public static void Clear()
        {
            foreach (byte apc in playerIdList.ToArray())
            {
                LocateArrow.RemoveAllTarget(apc);
                SendRPC(apc, false);
            }

            foreach (var bloodhound in BloodhoundTargets)
            {
                foreach (byte target in bloodhound.Value.ToArray())
                {
                    TargetArrow.Remove(bloodhound.Key, target);
                }

                BloodhoundTargets[bloodhound.Key].Clear();
            }
        }

        public static void OnPlayerDead(PlayerControl target)
        {
            if (!ArrowsPointingToDeadBody.GetBool()) return;

            foreach (byte pc in playerIdList.ToArray())
            {
                var player = Utils.GetPlayerById(pc);
                if (player == null || !player.IsAlive())
                    continue;
                LocateArrow.Add(pc, target.transform.position);
                SendRPC(pc, true, target.transform.position);
            }
        }

        public static void OnReportDeadBody(PlayerControl pc, GameData.PlayerInfo target, PlayerControl killer)
        {
            if (BloodhoundTargets[pc.PlayerId].Contains(killer.PlayerId))
            {
                return;
            }

            LocateArrow.Remove(pc.PlayerId, target.Object.transform.position);
            SendRPC(pc.PlayerId, false);

            if (UseLimit[pc.PlayerId] >= 1)
            {
                BloodhoundTargets[pc.PlayerId].Add(killer.PlayerId);
                TargetArrow.Add(pc.PlayerId, killer.PlayerId);

                pc.Notify(GetString("BloodhoundTrackRecorded"));
                UseLimit[pc.PlayerId] -= 1;
                SendRPCPlus(pc.PlayerId);

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

        public static string GetTargetArrow(PlayerControl seer, PlayerControl target = null)
        {
            if (!seer.Is(CustomRoles.Bloodhound)) return string.Empty;
            if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;
            if (GameStates.IsMeeting) return string.Empty;
            if (BloodhoundTargets.ContainsKey(seer.PlayerId) && BloodhoundTargets[seer.PlayerId].Count > 0)
            {
                var arrows = string.Empty;
                foreach (byte targetId in BloodhoundTargets[seer.PlayerId].ToArray())
                {
                    var arrow = TargetArrow.GetArrows(seer, targetId);
                    arrows += Utils.ColorString(seer.GetRoleColor(), arrow);
                }
                return arrows;
            }
            return Utils.ColorString(Color.white, LocateArrow.GetArrows(seer));
        }
    }
}
