namespace TOHE.Roles.Crewmate
{
    using System.Collections.Generic;
    using Hazel;
    using Sentry.Protocol;
    using UnityEngine;
    using static TOHE.Options;
    using static UnityEngine.GraphicsBuffer;
    using static TOHE.Translator;

    public static class Bloodhound
    {
        private static readonly int Id = 6400;
        private static List<byte> playerIdList = new();

        public static List<byte> UnreportablePlayers = new();
        public static Dictionary<byte, List<byte>> BloodhoundTargets = new();

        public static OptionItem ArrowsPointingToDeadBody;
        public static OptionItem LeaveDeadBodyUnreportable;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Bloodhound);
            ArrowsPointingToDeadBody = BooleanOptionItem.Create(Id + 10, "BloodhoundArrowsPointingToDeadBody", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Bloodhound]);
            LeaveDeadBodyUnreportable = BooleanOptionItem.Create(Id + 11, "BloodhoundLeaveDeadBodyUnreportable", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Bloodhound]);
        }
        public static void Init()
        {
            playerIdList = new();
            UnreportablePlayers = new List<byte>();
            BloodhoundTargets = new Dictionary<byte, List<byte>>();
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            BloodhoundTargets.Add(playerId, new List<byte>());

        }
        public static bool IsEnable => playerIdList.Count > 0;

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
            foreach (var apc in playerIdList)
            {
                LocateArrow.RemoveAllTarget(apc);
                SendRPC(apc, false);
            }

            foreach (var bloodhound in BloodhoundTargets)
            {
                foreach (var target in bloodhound.Value)
                {
                    TargetArrow.Remove(bloodhound.Key, target);
                }

                BloodhoundTargets[bloodhound.Key].Clear();
            }
        }

        public static void OnPlayerDead(PlayerControl target)
        {
            if (!ArrowsPointingToDeadBody.GetBool()) return; 

            foreach (var pc in playerIdList)
            {
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

            BloodhoundTargets[pc.PlayerId].Add(killer.PlayerId);
            TargetArrow.Add(pc.PlayerId, killer.PlayerId);

            pc.Notify(GetString("BloodhoundTrackRecorded"));

            if (LeaveDeadBodyUnreportable.GetBool())
            {
                UnreportablePlayers.Add(target.PlayerId);
            }
        }

        public static string GetTargetArrow(PlayerControl seer, PlayerControl target = null)
        {
            if (!seer.Is(CustomRoles.Bloodhound)) return "";
            if (target != null && seer.PlayerId != target.PlayerId) return "";
            if (GameStates.IsMeeting) return "";
            if (BloodhoundTargets.ContainsKey(seer.PlayerId) && BloodhoundTargets[seer.PlayerId].Count > 0)
            {
                var arrows = "";
                foreach (var targetId in BloodhoundTargets[seer.PlayerId])
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
