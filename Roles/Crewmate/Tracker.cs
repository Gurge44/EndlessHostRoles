using Hazel;
using System.Collections.Generic;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate
{
    public static class Tracker
    {
        private static readonly int Id = 8300;
        private static List<byte> playerIdList = [];
        public static bool IsEnable;

        private static OptionItem TrackLimitOpt;
        private static OptionItem OptionCanSeeLastRoomInMeeting;
        private static OptionItem CanGetColoredArrow;
        public static OptionItem HideVote;
        public static OptionItem TrackerAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;
        public static OptionItem CancelVote;

        public static bool CanSeeLastRoomInMeeting;

        public static Dictionary<byte, List<byte>> TrackerTarget = [];
        public static Dictionary<byte, float> TempTrackLimit = [];

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Tracker);
            TrackLimitOpt = IntegerOptionItem.Create(Id + 5, "DivinatorSkillLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Tracker])
                .SetValueFormat(OptionFormat.Times);
            CanGetColoredArrow = BooleanOptionItem.Create(Id + 6, "TrackerCanGetArrowColor", true, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tracker]);
            OptionCanSeeLastRoomInMeeting = BooleanOptionItem.Create(Id + 7, "EvilTrackerCanSeeLastRoomInMeeting", true, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tracker]);
            HideVote = BooleanOptionItem.Create(Id + 8, "TrackerHideVote", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Tracker]);
            TrackerAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 9, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 1f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tracker])
                .SetValueFormat(OptionFormat.Times);
            AbilityChargesWhenFinishedTasks = FloatOptionItem.Create(Id + 3, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tracker])
                .SetValueFormat(OptionFormat.Times);
            CancelVote = CreateVoteCancellingUseSetting(Id + 4, CustomRoles.Tracker, TabGroup.CrewmateRoles);
        }
        public static void Init()
        {
            playerIdList = [];
            TrackLimit = [];
            TrackerTarget = [];
            CanSeeLastRoomInMeeting = OptionCanSeeLastRoomInMeeting.GetBool();
            TempTrackLimit = [];
            IsEnable = false;
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            TrackLimit.Add(playerId, TrackLimitOpt.GetInt());
            TrackerTarget.Add(playerId, []);
            IsEnable = true;
        }
        public static void SendRPC(byte trackerId = byte.MaxValue, byte targetId = byte.MaxValue)
        {
            if (!IsEnable || !Utils.DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetTrackerTarget, SendOption.Reliable, -1);
            writer.Write(trackerId);
            writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            byte trackerId = reader.ReadByte();
            byte targetId = reader.ReadByte();

            TrackLimit[trackerId]--;

            TrackerTarget[trackerId].Add(targetId);
            TargetArrow.Add(trackerId, targetId);
        }
        public static string GetTargetMark(PlayerControl seer, PlayerControl target) => !(seer == null || target == null) && TrackerTarget.ContainsKey(seer.PlayerId) && TrackerTarget[seer.PlayerId].Contains(target.PlayerId) ? Utils.ColorString(seer.GetRoleColor(), "◀") : string.Empty;

        public static bool OnVote(PlayerControl player, PlayerControl target)
        {
            if (player == null || target == null || TrackLimit[player.PlayerId] < 1 || player.PlayerId == target.PlayerId || TrackerTarget[player.PlayerId].Contains(target.PlayerId) || Main.DontCancelVoteList.Contains(player.PlayerId)) return false;

            TrackLimit[player.PlayerId]--;

            TrackerTarget[player.PlayerId].Add(target.PlayerId);
            TargetArrow.Add(player.PlayerId, target.PlayerId);

            SendRPC(player.PlayerId, target.PlayerId);

            Main.DontCancelVoteList.Add(player.PlayerId);
            return true;
        }

        public static void OnReportDeadBody()
        {
            if (!IsEnable) return;

            foreach (var trackerId in playerIdList)
            {
                TempTrackLimit[trackerId] = TrackLimit[trackerId];
            }
        }

        public static string GetTrackerArrow(PlayerControl seer, PlayerControl target = null)
        {
            if (seer == null) return string.Empty;
            if (!seer.Is(CustomRoles.Tracker)) return string.Empty;
            if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;
            if (!TrackerTarget.ContainsKey(seer.PlayerId)) return string.Empty;
            if (GameStates.IsMeeting) return string.Empty;

            var arrows = string.Empty;
            var targetList = TrackerTarget[seer.PlayerId];
            foreach (var trackTarget in targetList)
            {
                if (!TrackerTarget[seer.PlayerId].Contains(trackTarget)) continue;

                var targetData = Utils.GetPlayerById(trackTarget);
                if (targetData == null) continue;

                var arrow = TargetArrow.GetArrows(seer, trackTarget);
                arrows += Utils.ColorString(CanGetColoredArrow.GetBool() ? Palette.PlayerColors[targetData.Data.DefaultOutfit.ColorId] : Color.white, arrow);
            }
            return arrows;
        }

        public static bool IsTrackTarget(PlayerControl seer, PlayerControl target)
            => seer.IsAlive() && playerIdList.Contains(seer.PlayerId)
                && TrackerTarget[seer.PlayerId].Contains(target.PlayerId)
                && target.IsAlive();

        public static string GetArrowAndLastRoom(PlayerControl seer, PlayerControl target)
        {
            if (seer == null || target == null) return string.Empty;
            string text = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Tracker), TargetArrow.GetArrows(seer, target.PlayerId));
            var room = Main.PlayerStates[target.PlayerId].LastRoom;
            if (room == null) text += Utils.ColorString(Color.gray, "@" + GetString("FailToTrack"));
            else text += Utils.ColorString(Utils.GetRoleColor(CustomRoles.Tracker), "@" + GetString(room.RoomId.ToString()));
            return text;
        }
    }
}