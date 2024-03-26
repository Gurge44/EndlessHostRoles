using HarmonyLib;
using System.Linq;

namespace EHR.Roles.Impostor
{
    internal class Blackmailer : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public byte BlackmailedPlayerId;

        public static void SetupCustomOption() => Options.SetupSingleRoleOptions(12190, TabGroup.ImpostorRoles, CustomRoles.Blackmailer);

        public override void Add(byte playerId)
        {
            On = true;
            BlackmailedPlayerId = byte.MaxValue;
        }

        public override void Init()
        {
            On = false;
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            return killer.CheckDoubleTrigger(target, () =>
            {
                BlackmailedPlayerId = target.PlayerId;
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                killer.SetKillCooldown(3f);
            });
        }

        public override void AfterMeetingTasks()
        {
            BlackmailedPlayerId = byte.MaxValue;
        }

        public static void OnCheckForEndVoting()
        {
            if (!On) return;
            var bmState = Main.PlayerStates.FirstOrDefault(x => x.Value.MainRole == CustomRoles.Blackmailer);
            if (bmState.Value.Role is not Blackmailer { IsEnable: true } bm || bm.BlackmailedPlayerId == byte.MaxValue) return;

            var bmVotedForTemp = MeetingHud.Instance.playerStates.FirstOrDefault(x => x.TargetPlayerId == bmState.Key)?.VotedFor;
            if (bmVotedForTemp == null) return;
            var bmVotedFor = (byte)bmVotedForTemp;

            MeetingHud.Instance.playerStates.DoIf(x => x.TargetPlayerId == bm.BlackmailedPlayerId, x =>
            {
                x.UnsetVote();
                if (x.TargetPlayerId == 0) MeetingHud.Instance.CmdCastVote(x.TargetPlayerId, bmVotedFor);
                else MeetingHud.Instance.CastVote(x.TargetPlayerId, bmVotedFor);
                x.VotedFor = bmVotedFor;
            });
        }
    }
}
