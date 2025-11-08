using System;
using System.Collections.Generic;
using EHR.Neutral;

namespace EHR.Impostor;

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
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachKill, 0.8f, new FloatValueRule(0f, 5f, 0.1f), OptionFormat.Times)
            .AutoSetupOption(ref MaxBlackmailedPlayersPerMeeting, 1, new IntegerValueRule(1, 14, 1), OptionFormat.Players)
            .AutoSetupOption(ref MaxBlackmailedPlayersAtOnce, 1, new IntegerValueRule(1, 14, 1), OptionFormat.Players)
            .AutoSetupOption(ref WhoSeesBlackmailedPlayers, 1, new[] { "BMWSBP.Blackmailer", "BMWSBP.BlackmailerAndBlackmailed", "BMWSBP.Impostors", "BMWSBP.Everyone" });
    }

    public override void Add(byte playerId)
    {
        On = true;
        BlackmailedPlayerIds = [];
        NumBlackmailedThisRound = 0;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
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
        
        if (Thanos.IsImmune(target)) return false;

        return killer.CheckDoubleTrigger(target, () =>
        {
            BlackmailedPlayerIds.Add(target.PlayerId);
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            killer.SetKillCooldown(3f);
            killer.RpcRemoveAbilityUse();
            NumBlackmailedThisRound++;
        });
    }

    public override void AfterMeetingTasks()
    {
        if (AbilityExpires.GetValue() == 0) BlackmailedPlayerIds.Clear();
    }

    public static void ManipulateVotingResult(Dictionary<byte, int> votingData, MeetingHud.VoterState[] states)
    {
        try
        {
            foreach ((byte id, PlayerState state) in Main.PlayerStates)
            {
                try
                {
                    if (state.MainRole != CustomRoles.Blackmailer || state.Role is not Blackmailer { IsEnable: true } bm || bm.BlackmailedPlayerIds.Count == 0) continue;

                    int idx = Array.FindIndex(states, s => s.VoterId == id);
                    if (idx == -1) continue;
                    ref var vs = ref states[idx];

                    foreach (byte targetId in bm.BlackmailedPlayerIds)
                    {
                        try
                        {
                            if (!Main.PlayerStates.TryGetValue(targetId, out PlayerState ps) || ps.IsDead) continue;

                            int targetIdx = Array.FindIndex(states, s => s.VoterId == targetId);
                            if (targetIdx == -1) continue;
                            ref var targetVs = ref states[targetIdx];

                            if (vs.VotedForId == targetVs.VotedForId) continue;

                            byte vote;

                            if (vs.SkippedVote)
                                vote = 253;
                            else if (vs.AmDead || vs.VotedForId.GetPlayer() == null)
                                vote = 254;
                            else
                                vote = vs.VotedForId;

                            if (votingData.TryGetValue(targetVs.VotedForId, out int oldCount))
                            {
                                if (oldCount <= 1) votingData.Remove(targetVs.VotedForId);
                                else votingData[targetVs.VotedForId] = oldCount - 1;
                            }

                            targetVs.VotedForId = vote;

                            if (vote <= 253 && !votingData.TryAdd(vote, 1))
                                votingData[vote]++;
                        }
                        catch (Exception e) { Utils.ThrowException(e); }
                    }
                }
                catch (Exception e) { Utils.ThrowException(e); }
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
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