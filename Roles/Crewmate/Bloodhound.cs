namespace TOHE.Roles.Crewmate
{
    using Hazel;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using static TOHE.Options;
    using static TOHE.Translator;

    public static class Bloodhound
    {
        private static readonly int Id = 6400;
        private static List<byte> playerIdList = new();

        public static List<byte> UnreportablePlayers = new();
        public static Dictionary<byte, List<byte>> BloodhoundTargets = new();
        public static Dictionary<byte, float> UseLimit = new();

        public static OptionItem ArrowsPointingToDeadBody;
        public static OptionItem UseLimitOpt;
        public static OptionItem LeaveDeadBodyUnreportable;
        public static OptionItem NotifyKiller;
        public static OptionItem BloodhoundAbilityUseGainWithEachTaskCompleted;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Bloodhound);
            ArrowsPointingToDeadBody = BooleanOptionItem.Create(Id + 10, "BloodhoundArrowsPointingToDeadBody", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound]);
            LeaveDeadBodyUnreportable = BooleanOptionItem.Create(Id + 11, "BloodhoundLeaveDeadBodyUnreportable", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound]);
            NotifyKiller = BooleanOptionItem.Create(Id + 14, "BloodhoundNotifyKiller", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound]);
            UseLimitOpt = IntegerOptionItem.Create(Id + 12, "AbilityUseLimit", new(1, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound])
            .SetValueFormat(OptionFormat.Times);
            BloodhoundAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 13, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound])
            .SetValueFormat(OptionFormat.Times);
        }
        public static void Init()
        {
            playerIdList = new();
            UseLimit = new();
            UnreportablePlayers = new List<byte>();
            BloodhoundTargets = new Dictionary<byte, List<byte>>();
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            UseLimit.Add(playerId, UseLimitOpt.GetInt());
            BloodhoundTargets.Add(playerId, new List<byte>());

        }
        public static bool IsEnable => playerIdList.Any();

        private static void SendRPC(byte playerId, bool add, Vector3 loc = new())
        {
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

        public static void Clear()
        {
            for (int i = 0; i < playerIdList.Count; i++)
            {
                byte apc = playerIdList[i];
                LocateArrow.RemoveAllTarget(apc);
                SendRPC(apc, false);
            }

            foreach (var bloodhound in BloodhoundTargets)
            {
                for (int i = 0; i < bloodhound.Value.Count; i++)
                {
                    byte target = bloodhound.Value[i];
                    TargetArrow.Remove(bloodhound.Key, target);
                }

                BloodhoundTargets[bloodhound.Key].Clear();
            }
        }

        public static void OnPlayerDead(PlayerControl target)
        {
            if (!ArrowsPointingToDeadBody.GetBool()) return;

            for (int i = 0; i < playerIdList.Count; i++)
            {
                byte pc = playerIdList[i];
                var player = Utils.GetPlayerById(pc);
                if (player == null || !player.IsAlive()) continue;
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
            if (BloodhoundTargets.ContainsKey(seer.PlayerId) && BloodhoundTargets[seer.PlayerId].Any())
            {
                var arrows = string.Empty;
                for (int i = 0; i < BloodhoundTargets[seer.PlayerId].Count; i++)
                {
                    byte targetId = BloodhoundTargets[seer.PlayerId][i];
                    var arrow = TargetArrow.GetArrows(seer, targetId);
                    arrows += Utils.ColorString(seer.GetRoleColor(), arrow);
                }
                return arrows;
            }
            return Utils.ColorString(Color.white, LocateArrow.GetArrows(seer));
        }
    }
}
