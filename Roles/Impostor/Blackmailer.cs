using System.Collections.Generic;
using System.Linq;
using EHR.Patches;

namespace EHR.Impostor
{
    internal class Blackmailer : RoleBase
    {
        public static bool On;

        private static OptionItem AbilityExpires;
        private static OptionItem AbilityUseLimit;
        private static OptionItem AbilityUseGainWithEachKill;
        private static OptionItem MaxBlackmailedPlayersPerMeeting;
        private static OptionItem MaxBlackmailedPlayersAtOnce;
        private static OptionItem WhoSeesBlackmailedPlayers;

        public List<byte> BlackmailedPlayerIds;
        private int NumBlackmailedThisRound;
        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            StartSetup(12190)
                .AutoSetupOption(ref AbilityExpires, 0, new[] { "BMAE.AfterMeeting", "BMAE.Never" })
                .AutoSetupOption(ref AbilityUseLimit, 1, new IntegerValueRule(0, 20, 1), OptionFormat.Times)
                .AutoSetupOption(ref AbilityUseGainWithEachKill, 0.4f, new FloatValueRule(0f, 5f, 0.1f), OptionFormat.Times)
                .AutoSetupOption(ref MaxBlackmailedPlayersPerMeeting, 1, new IntegerValueRule(1, 14, 1), OptionFormat.Players)
                .AutoSetupOption(ref MaxBlackmailedPlayersAtOnce, 1, new IntegerValueRule(1, 14, 1), OptionFormat.Players)
                .AutoSetupOption(ref WhoSeesBlackmailedPlayers, 1, new[] { "BMWSBP.Blackmailer", "BMWSBP.BlackmailerAndBlackmailed", "BMWSBP.Impostors", "BMWSBP.Everyone" });
        }

        public override void Add(byte playerId)
        {
            On = true;
            BlackmailedPlayerIds = [];
            NumBlackmailedThisRound = 0;
            playerId.SetAbilityUseLimit(AbilityUseLimit.GetInt());
        }

        public override void Init()
        {
            On = false;
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer.GetAbilityUseLimit() < 1) return true;
            if (NumBlackmailedThisRound >= MaxBlackmailedPlayersPerMeeting.GetInt()) return true;
            if (BlackmailedPlayerIds.Count >= MaxBlackmailedPlayersAtOnce.GetInt()) return true;
            if (BlackmailedPlayerIds.Contains(target.PlayerId)) return true;

            return killer.CheckDoubleTrigger(target, () =>
            {
                BlackmailedPlayerIds.Add(target.PlayerId);
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                killer.SetKillCooldown(3f);
                NumBlackmailedThisRound++;
            });
        }

        public override void AfterMeetingTasks()
        {
            if (AbilityExpires.GetValue() == 0)
            {
                BlackmailedPlayerIds.Clear();
            }
        }

        public static void OnCheckForEndVoting()
        {
            if (!On) return;

            CheckForEndVotingPatch.RunRoleCode = false;

            try
            {
                var bmState = Main.PlayerStates.FirstOrDefault(x => x.Value.MainRole == CustomRoles.Blackmailer);
                if (bmState.Value.Role is not Blackmailer { IsEnable: true } bm || bm.BlackmailedPlayerIds.Count == 0) return;

                var bmVotedForTemp = MeetingHud.Instance.playerStates.FirstOrDefault(x => x.TargetPlayerId == bmState.Key)?.VotedFor;
                if (bmVotedForTemp == null) return;
                var bmVotedFor = (byte)bmVotedForTemp;

                MeetingHud.Instance.playerStates.DoIf(x => bm.BlackmailedPlayerIds.Contains(x.TargetPlayerId), x =>
                {
                    if (x.DidVote)
                    {
                        x.UnsetVote();
                        MeetingHud.Instance.RpcClearVote(x.TargetPlayerId.GetPlayer().GetClientId());
                    }

                    if (x.TargetPlayerId.IsHost()) MeetingHud.Instance.CmdCastVote(x.TargetPlayerId, bmVotedFor);
                    else MeetingHud.Instance.CastVote(x.TargetPlayerId, bmVotedFor);
                    x.VotedFor = bmVotedFor;
                });
            }
            finally
            {
                CheckForEndVotingPatch.RunRoleCode = true;
            }
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
        {
            if (!On || !meeting || !BlackmailedPlayerIds.Contains(target.PlayerId)) return string.Empty;

            switch (WhoSeesBlackmailedPlayers.GetValue())
            {
                case 0 when seer.Is(CustomRoles.Blackmailer):
                case 1 when seer.Is(CustomRoles.Blackmailer) || (BlackmailedPlayerIds.Contains(seer.PlayerId) && seer.PlayerId == target.PlayerId):
                case 2 when seer.IsImpostor():
                case 3:
                    return Translator.GetString("BlackmailedSuffix");

                default:
                    return string.Empty;
            }
        }
    }
}