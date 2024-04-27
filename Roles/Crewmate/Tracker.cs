using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Roles.Crewmate
{
    public class Tracker : RoleBase
    {
        private const int Id = 8300;
        private static List<byte> playerIdList = [];

        private static OptionItem TrackLimitOpt;
        private static OptionItem OptionCanSeeLastRoomInMeeting;
        private static OptionItem CanGetColoredArrow;
        public static OptionItem HideVote;
        public static OptionItem TrackerAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;
        public static OptionItem CancelVote;

        public static bool CanSeeLastRoomInMeeting;

        public static Dictionary<byte, List<byte>> TrackerTarget = [];

        public override bool IsEnable => playerIdList.Count > 0;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Tracker);
            TrackLimitOpt = IntegerOptionItem.Create(Id + 5, "DivinatorSkillLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Tracker])
                .SetValueFormat(OptionFormat.Times);
            CanGetColoredArrow = BooleanOptionItem.Create(Id + 6, "TrackerCanGetArrowColor", true, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tracker]);
            OptionCanSeeLastRoomInMeeting = BooleanOptionItem.Create(Id + 7, "EvilTrackerCanSeeLastRoomInMeeting", true, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tracker]);
            HideVote = BooleanOptionItem.Create(Id + 8, "TrackerHideVote", false, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Tracker]);
            TrackerAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 9, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 1f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tracker])
                .SetValueFormat(OptionFormat.Times);
            AbilityChargesWhenFinishedTasks = FloatOptionItem.Create(Id + 3, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tracker])
                .SetValueFormat(OptionFormat.Times);
            CancelVote = CreateVoteCancellingUseSetting(Id + 4, CustomRoles.Tracker, TabGroup.CrewmateRoles);
        }

        public override void Init()
        {
            playerIdList = [];
            TrackerTarget = [];
            CanSeeLastRoomInMeeting = OptionCanSeeLastRoomInMeeting.GetBool();
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(TrackLimitOpt.GetInt());
            TrackerTarget.Add(playerId, []);
        }

        public static void SendRPC(byte trackerId = byte.MaxValue, byte targetId = byte.MaxValue)
        {
            if (!Utils.DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetTrackerTarget, SendOption.Reliable);
            writer.Write(trackerId);
            writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            byte trackerId = reader.ReadByte();
            byte targetId = reader.ReadByte();

            Utils.GetPlayerById(trackerId).RpcRemoveAbilityUse();

            TrackerTarget[trackerId].Add(targetId);
            TargetArrow.Add(trackerId, targetId);
        }

        public static string GetTargetMark(PlayerControl seer, PlayerControl target) => !(seer == null || target == null) && TrackerTarget.ContainsKey(seer.PlayerId) && TrackerTarget[seer.PlayerId].Contains(target.PlayerId) ? Utils.ColorString(seer.GetRoleColor(), "◀") : string.Empty;

        public static bool OnVote(PlayerControl player, PlayerControl target)
        {
            if (player == null || target == null || player.GetAbilityUseLimit() < 1f || player.PlayerId == target.PlayerId || TrackerTarget[player.PlayerId].Contains(target.PlayerId) || Main.DontCancelVoteList.Contains(player.PlayerId)) return false;

            player.RpcRemoveAbilityUse();

            TrackerTarget[player.PlayerId].Add(target.PlayerId);
            TargetArrow.Add(player.PlayerId, target.PlayerId);

            SendRPC(player.PlayerId, target.PlayerId);

            Main.DontCancelVoteList.Add(player.PlayerId);
            return true;
        }

        public static string GetTrackerArrow(PlayerControl seer, PlayerControl target = null)
        {
            if (seer == null) return string.Empty;
            if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;
            if (!TrackerTarget.ContainsKey(seer.PlayerId)) return string.Empty;
            if (GameStates.IsMeeting) return string.Empty;

            return TrackerTarget[seer.PlayerId].Aggregate(string.Empty, (current, trackTarget) => current + Utils.ColorString(CanGetColoredArrow.GetBool() ? Main.PlayerColors[trackTarget] : Color.white, TargetArrow.GetArrows(seer, trackTarget) + LocateArrow.GetArrows(seer)));
        }

        public static bool IsTrackTarget(PlayerControl seer, PlayerControl target)
            => seer.IsAlive() && playerIdList.Contains(seer.PlayerId)
                              && TrackerTarget[seer.PlayerId].Contains(target.PlayerId)
                              && target.IsAlive();

        public static string GetArrowAndLastRoom(PlayerControl seer, PlayerControl target)
        {
            if (seer == null || target == null) return string.Empty;

            var roleColor = Utils.GetRoleColor(CustomRoles.Tracker);
            string text = Utils.ColorString(roleColor, TargetArrow.GetArrows(seer, target.PlayerId));
            text += Utils.ColorString(roleColor, LocateArrow.GetArrows(seer));

            var room = Main.PlayerStates[target.PlayerId].LastRoom;
            if (room == null) text += Utils.ColorString(Color.gray, "@" + GetString("FailToTrack"));
            else text += Utils.ColorString(roleColor, "@" + GetString(room.RoomId.ToString()));

            return text;
        }

        public static void OnPlayerDeath(PlayerControl player)
        {
            if (player == null) return;
            foreach (var kvp in TrackerTarget)
            {
                if (kvp.Value.Contains(player.PlayerId))
                {
                    kvp.Value.Remove(player.PlayerId);
                    TargetArrow.Remove(kvp.Key, player.PlayerId);
                    LocateArrow.Add(kvp.Key, player.Pos());
                }
            }
        }
    }
}