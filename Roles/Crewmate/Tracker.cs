using Hazel;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate
{
    public static class Tracker
    {
        private static readonly int Id = 8300;

        private static List<byte> playerIdList = new();

        private static OptionItem TrackLimitOpt;
        private static OptionItem OptionCanSeeLastRoomInMeeting;
        public static OptionItem HideVote;
        public static OptionItem TrackerAbilityUseGainWithEachTaskCompleted;

        public static bool CanSeeLastRoomInMeeting;

        public static Dictionary<byte, float> TrackLimit = new();
        public static Dictionary<byte, byte> TrackerTarget = new();

        public static Dictionary<byte, string> msgToSend = new();

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Tracker);
            TrackLimitOpt = IntegerOptionItem.Create(Id + 10, "DivinatorSkillLimit", new(1, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Tracker])
                .SetValueFormat(OptionFormat.Times);
            OptionCanSeeLastRoomInMeeting = BooleanOptionItem.Create(Id + 11, "EvilTrackerCanSeeLastRoomInMeeting", false, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tracker]);
            HideVote = BooleanOptionItem.Create(Id + 12, "TrackerHideVote", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Tracker]);
            TrackerAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 13, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.4f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Tracker])
            .SetValueFormat(OptionFormat.Times);
        }
        public static void Init()
        {
            playerIdList = new();
            TrackLimit = new();
            TrackerTarget = new();
            msgToSend = new();
            CanSeeLastRoomInMeeting = OptionCanSeeLastRoomInMeeting.GetBool();
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            TrackLimit.TryAdd(playerId, TrackLimitOpt.GetInt());
            TrackerTarget.Add(playerId, byte.MaxValue);
        }
        public static void SendRPC(byte trackerId = byte.MaxValue, byte targetId = byte.MaxValue)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetTrackerTarget, SendOption.Reliable, -1);
            writer.Write(trackerId);
            writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            byte trackerId = reader.ReadByte();
            byte targetId = reader.ReadByte();

            if (TrackerTarget[trackerId] != byte.MaxValue)
            {
                TargetArrow.Remove(trackerId, TrackerTarget[trackerId]);
            }

            TrackerTarget[trackerId] = targetId;
            TargetArrow.Add(trackerId, targetId);

        }
        public static bool IsEnable => playerIdList.Any();

        public static string GetTargetMark(PlayerControl seer, PlayerControl target) => TrackerTarget.ContainsKey(seer.PlayerId) && TrackerTarget[seer.PlayerId] == target.PlayerId ? Utils.ColorString(seer.GetRoleColor(), "◀") : string.Empty;

        public static void OnReportDeadBody()
        {
            if (!OptionCanSeeLastRoomInMeeting.GetBool()) return;

            for (int i = 0; i < playerIdList.Count; i++)
            {
                byte pc = playerIdList[i];
                if (TrackerTarget[pc] == byte.MaxValue)
                {
                    continue;
                }

                string room = string.Empty;
                var targetRoom = Main.PlayerStates[TrackerTarget[pc]].LastRoom;
                if (targetRoom == null) room += GetString("FailToTrack");
                else room += GetString(targetRoom.RoomId.ToString());

                if (msgToSend.ContainsKey(pc))
                {
                    msgToSend[pc] = string.Format(GetString("TrackerLastRoomMessage"), room);
                }
                else
                {
                    msgToSend.Add(pc, string.Format(GetString("TrackerLastRoomMessage"), room));
                }


            }
        }

        public static void OnVote(PlayerControl player, PlayerControl target)
        {
            if (player == null || target == null) return;
            if (TrackLimit[player.PlayerId] < 1) return;
            if (player.PlayerId == target.PlayerId) return;
            if (target.PlayerId == TrackerTarget[player.PlayerId]) return;

            TrackLimit[player.PlayerId] -= 1;

            if (TrackerTarget[player.PlayerId] != byte.MaxValue)
            {
                TargetArrow.Remove(player.PlayerId, TrackerTarget[player.PlayerId]);
            }

            TrackerTarget[player.PlayerId] = target.PlayerId;
            TargetArrow.Add(player.PlayerId, target.PlayerId);

            SendRPC(player.PlayerId, target.PlayerId);
        }

        public static string GetTrackerArrow(PlayerControl seer, PlayerControl target = null)
        {
            if (!seer.Is(CustomRoles.Tracker)) return string.Empty;
            if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;
            if (GameStates.IsMeeting) return string.Empty;
            if (!TrackerTarget.ContainsKey(seer.PlayerId)) return string.Empty;
            return Utils.ColorString(Color.white, TargetArrow.GetArrows(seer, TrackerTarget[seer.PlayerId]));
        }

        public static bool IsTrackTarget(PlayerControl seer, PlayerControl target)
            => seer.IsAlive() && playerIdList.Contains(seer.PlayerId)
                && TrackerTarget[seer.PlayerId] == target.PlayerId
                && target.IsAlive();

        public static string GetArrowAndLastRoom(PlayerControl seer, PlayerControl target)
        {
            string text = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Tracker), TargetArrow.GetArrows(seer, target.PlayerId));
            var room = Main.PlayerStates[target.PlayerId].LastRoom;
            if (room == null) text += Utils.ColorString(Color.gray, "@" + GetString("FailToTrack"));
            else text += Utils.ColorString(Utils.GetRoleColor(CustomRoles.Tracker), "@" + GetString(room.RoomId.ToString()));
            return text;
        }
    }
}
