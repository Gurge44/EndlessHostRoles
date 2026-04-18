using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;

namespace EHR.Roles;

public class Clerk : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    private static OptionItem AbilityUseLimit;
    private static OptionItem CanUseAbilityOnFirstMeeting;
    private static OptionItem IncreasedSpeed;
    private static OptionItem IncreasedVision;
    public static OptionItem CancelVote;

    public static HashSet<byte> DidTaskThisRound = [];
    public static HashSet<byte> HasBoost = [];

    public override void SetupCustomOption()
    {
        StartSetup(654800)
            .AutoSetupOption(ref AbilityUseLimit, 5, new IntegerValueRule(1, 30, 1), OptionFormat.Times)
            .AutoSetupOption(ref CanUseAbilityOnFirstMeeting, false)
            .AutoSetupOption(ref IncreasedSpeed, 1.8f, new FloatValueRule(0.1f, 3f, 0.1f), OptionFormat.Multiplier)
            .AutoSetupOption(ref IncreasedVision, 1f, new FloatValueRule(0.1f, 1.5f, 0.1f), OptionFormat.Multiplier)
            .CreateVoteCancellingUseSetting(ref CancelVote);
    }

    public override void Init()
    {
        On = false;
        DidTaskThisRound = [];
        HasBoost = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetInt());
    }

    public override bool OnVote(PlayerControl voter, PlayerControl target)
    {
        if (Starspawn.IsDayBreak) return false;
        if (Main.DontCancelVoteList.Contains(voter.PlayerId)) return false;
        if (MeetingStates.FirstMeeting && !CanUseAbilityOnFirstMeeting.GetBool()) return false;
        if (voter.GetAbilityUseLimit() < 1) return false;
        if (target.PlayerId == voter.PlayerId) return false;

        voter.RpcRemoveAbilityUse();

        TaskState ts = target.GetTaskState();

        if (DidTaskThisRound.Contains(target.PlayerId) || !ts.HasTasks || ts.IsTaskFinished)
        {
            HasBoost.Add(target.PlayerId);
            Utils.SendMessage(string.Format(Translator.GetString("Clerk.DidTax"), target.PlayerId.ColoredPlayerName()), voter.PlayerId, CustomRoles.Clerk.ToColoredString(), importance: MessageImportance.High);
        }
        else if (!target.Is(CustomRoles.Pestilence))
        {
            target.SetRealKiller(voter);
            PlayerState state = Main.PlayerStates[target.PlayerId];
            state.deathReason = PlayerState.DeathReason.Taxes;
            state.SetDead();
            Medic.IsDead(target);
            target.RpcExileV2();
            Utils.AfterPlayerDeathTasks(target, true);
            Utils.SendMessage(string.Format(Translator.GetString("Clerk.Killed"), target.PlayerId.ColoredPlayerName()), title: CustomRoles.Clerk.ToColoredString(), importance: MessageImportance.High);
            
            if (voter.AmOwner) Achievements.Type.PayUp.Complete();
        }

        Main.DontCancelVoteList.Add(voter.PlayerId);
        return true;
    }

    public override void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        OnVote(shapeshifter, target);
    }

    public override void AfterMeetingTasks()
    {
        DidTaskThisRound = [];
    }

    public override void OnReportDeadBody()
    {
        HasBoost.Do(x => Main.AllPlayerSpeed[x] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod));
        HasBoost = [];
    }

    public static void OnAnyoneApplyGameOptions(IGameOptions opt, byte id)
    {
        if (HasBoost.Contains(id))
        {
            Main.AllPlayerSpeed[id] = IncreasedSpeed.GetFloat();
            float increasedVision = IncreasedVision.GetFloat();
            opt.SetFloat(FloatOptionNames.CrewLightMod, increasedVision);
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, increasedVision);
        }
    }
}