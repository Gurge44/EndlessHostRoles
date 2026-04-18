using System.Collections.Generic;
using System.Linq;

namespace EHR.Roles;

public class Inquisitor : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    private static OptionItem KnowExactRolesAfterTasksFinished;
    private static OptionItem ExcludeDeadPlayers;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachTaskCompleted;
    private static OptionItem AbilityChargesWhenFinishedTasks;

    public override void SetupCustomOption()
    {
        StartSetup(654900)
            .AutoSetupOption(ref KnowExactRolesAfterTasksFinished, true)
            .AutoSetupOption(ref ExcludeDeadPlayers, false)
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachTaskCompleted, 0.5f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityChargesWhenFinishedTasks, 0.2f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override bool OnVote(PlayerControl voter, PlayerControl target)
    {
        if (Starspawn.IsDayBreak) return false;
        if (voter == null || target == null || voter.PlayerId == target.PlayerId || Main.DontCancelVoteList.Contains(voter.PlayerId)) return false;

        var players = ExcludeDeadPlayers.GetBool() ? Main.EnumerateAlivePlayerControls() : Main.EnumeratePlayerControls();
        List<(byte Id, CustomRoles Role)> knownRoles = [.. from pc in players where Utils.KnowsTargetRole(target, pc) select (pc.PlayerId, pc.GetCustomRole())];
        
        string result;
        
        if (knownRoles.Count == 0)
            result = Translator.GetString("InquisitorNoInfo");
        else if (KnowExactRolesAfterTasksFinished.GetBool() && voter.GetTaskState().IsTaskFinished)
            result = string.Join('\n', knownRoles.Select(x => $"{x.Id.ColoredPlayerName()}: {x.Role.ToColoredString()}"));
        else
            result = string.Join(", ", knownRoles.Select(x => x.Id.ColoredPlayerName()));
        
        Utils.SendMessage("\n", voter.PlayerId, string.Format(Translator.GetString("InquisitorVoteResult"), target.PlayerId.ColoredPlayerName(), result), importance: MessageImportance.High);
        
        voter.RpcRemoveAbilityUse();
        Main.DontCancelVoteList.Add(voter.PlayerId);
        return true;
    }

    public override void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        OnVote(shapeshifter, target);
    }
}