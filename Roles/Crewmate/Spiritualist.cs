using System.Collections.Generic;
using System.Linq;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Roles.Crewmate
{
    internal class Spiritualist : RoleBase
    {
        private const int Id = 8100;

        private static List<byte> playerIdList = [];

        public static OptionItem ShowGhostArrowEverySeconds;
        public static OptionItem ShowGhostArrowForSeconds;

        private long ShowGhostArrowUntil;
        private long LastGhostArrowShowTime;

        public static byte SpiritualistTarget;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Spiritualist);
            ShowGhostArrowEverySeconds = FloatOptionItem.Create(Id + 10, "SpiritualistShowGhostArrowEverySeconds", new(1f, 60f, 1f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Spiritualist])
                .SetValueFormat(OptionFormat.Seconds);
            ShowGhostArrowForSeconds = FloatOptionItem.Create(Id + 11, "SpiritualistShowGhostArrowForSeconds", new(1f, 60f, 1f), 2f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Spiritualist])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Init()
        {
            playerIdList = [];
            SpiritualistTarget = new();
            LastGhostArrowShowTime = 0;
            ShowGhostArrowUntil = 0;
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            SpiritualistTarget = byte.MaxValue;
            LastGhostArrowShowTime = 0;
            ShowGhostArrowUntil = 0;
        }

        public override bool IsEnable => playerIdList.Count > 0;

        bool ShowArrow
        {
            get
            {
                long timestamp = Utils.TimeStamp;

                if (LastGhostArrowShowTime == 0 || LastGhostArrowShowTime + (long)ShowGhostArrowEverySeconds.GetFloat() <= timestamp)
                {
                    LastGhostArrowShowTime = timestamp;
                    ShowGhostArrowUntil = timestamp + (long)ShowGhostArrowForSeconds.GetFloat();
                    return true;
                }

                return ShowGhostArrowUntil >= timestamp;
            }
        }

        public static void OnReportDeadBody(GameData.PlayerInfo target)
        {
            if (target == null)
            {
                return;
            }

            if (SpiritualistTarget != byte.MaxValue)
                RemoveTarget();

            SpiritualistTarget = target.PlayerId;
        }

        public override void AfterMeetingTasks()
        {
            foreach (byte spiritualist in playerIdList)
            {
                PlayerControl player = Main.AllPlayerControls.FirstOrDefault(a => a.PlayerId == spiritualist);
                if (!player.IsAlive())
                {
                    continue;
                }

                LastGhostArrowShowTime = 0;
                ShowGhostArrowUntil = 0;

                PlayerControl target = Main.AllPlayerControls.FirstOrDefault(a => a.PlayerId == SpiritualistTarget);
                if (target == null)
                {
                    continue;
                }

                target.Notify("<color=#ffff00>The Spiritualist has an arrow pointing toward you</color>");

                TargetArrow.Add(spiritualist, target.PlayerId);

                var writer = CustomRpcSender.Create("SpiritualistSendMessage");
                writer.StartMessage(target.GetClientId());
                writer.StartRpc(target.NetId, (byte)RpcCalls.SetName)
                    .Write(GetString("SpiritualistNoticeTitle"))
                    .EndRpc();
                writer.StartRpc(target.NetId, (byte)RpcCalls.SendChat)
                    .Write(GetString("SpiritualistNoticeMessage"))
                    .EndRpc();
                writer.StartRpc(target.NetId, (byte)RpcCalls.SetName)
                    .Write(target.Data.PlayerName)
                    .EndRpc();
                writer.EndMessage();
                writer.SendMessage();
            }
        }

        public static string GetSpiritualistArrow(PlayerControl seer, PlayerControl target = null)
        {
            if (Main.PlayerStates[seer.PlayerId].Role is not Spiritualist { IsEnable: true } st || !seer.IsAlive()) return string.Empty;
            if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;
            if (GameStates.IsMeeting) return string.Empty;
            return SpiritualistTarget != byte.MaxValue && st.ShowArrow ? Utils.ColorString(seer.GetRoleColor(), TargetArrow.GetArrows(seer, SpiritualistTarget)) : string.Empty;
        }

        public static void RemoveTarget()
        {
            foreach (byte spiritualist in playerIdList)
            {
                TargetArrow.Remove(spiritualist, SpiritualistTarget);
            }

            SpiritualistTarget = byte.MaxValue;
        }
    }
}