using System.Collections.Generic;
using EHR.Patches;

namespace EHR.Roles;

public class Assumer : RoleBase
{
    public static bool On;
    private static List<Assumer> Instances = [];

    private static OptionItem VoteReceiverDies;
    private static OptionItem MinPlayersToAssume;
    private static OptionItem CanKill;
    private static OptionItem KillCooldown;
    private static OptionItem CanVent;

    private byte AssumerId;
    private (byte Id, int VoteNum) Assumption;
    private bool HasAssumed => Assumption.Id != byte.MaxValue;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(649250)
            .AutoSetupOption(ref VoteReceiverDies, false)
            .AutoSetupOption(ref MinPlayersToAssume, 6, new IntegerValueRule(1, 15, 1), OptionFormat.Players)
            .AutoSetupOption(ref CanKill, true)
            .AutoSetupOption(ref KillCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds, overrideParent: CanKill)
            .AutoSetupOption(ref CanVent, false);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        Instances.Add(this);
        AssumerId = playerId;
        Assumption = (byte.MaxValue, 0);
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return CanKill.GetBool();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public static void OnVotingEnd(Dictionary<byte, int> voteNum)
    {
        if (!On) return;

        foreach (Assumer instance in Instances)
        {
            if (instance.HasAssumed && voteNum.TryGetValue(instance.Assumption.Id, out int num) && num == instance.Assumption.VoteNum)
            {
                foreach (PlayerVoteArea pva in MeetingHud.Instance.playerStates)
                {
                    if (pva.VotedFor == instance.Assumption.Id || (VoteReceiverDies.GetBool() && pva.TargetPlayerId == instance.Assumption.Id))
                        CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Assumed, pva.TargetPlayerId);
                }
            }

            instance.Assumption = (byte.MaxValue, 0);
        }
    }

    public static void Assume(byte assumerId, byte id, int num)
    {
        if (Main.AllAlivePlayerControls.Count < MinPlayersToAssume.GetInt()) return;

        Assumer assumer = Instances.Find(x => x.AssumerId == assumerId);
        if (assumer == null || assumer.HasAssumed) return;

        assumer.Assumption = (id, num);
        Utils.SendMessage("\n", assumerId, string.Format(Translator.GetString("Assumer.AssumedMessage"), id.ColoredPlayerName(), num), importance: MessageImportance.High);
    }
}