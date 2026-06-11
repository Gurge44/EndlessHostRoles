using System.Collections.Generic;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Roles;

public class Oracle : RoleBase
{
    private const int Id = 7600;
    private static bool On;

    public static OptionItem CheckLimitOpt;
    public static OptionItem HideVote;
    public static OptionItem FailChance;
    public static OptionItem OracleAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;
    public static OptionItem CancelVote;

    public static readonly List<byte> DidVote;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Oracle);

        CheckLimitOpt = new IntegerOptionItem(Id + 10, "OracleSkillLimit", new(0, 10, 1), 0, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Oracle])
            .SetValueFormat(OptionFormat.Times);

        HideVote = new BooleanOptionItem(Id + 12, "OracleHideVote", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Oracle]);

        FailChance = new IntegerOptionItem(Id + 13, "FailChance", new(0, 100, 5), 20, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Oracle])
            .SetValueFormat(OptionFormat.Percent);

        OracleAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 14, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Oracle])
            .SetValueFormat(OptionFormat.Times);

        AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 15, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Oracle])
            .SetValueFormat(OptionFormat.Times);

        CancelVote = CreateVoteCancellingUseSetting(Id + 11, CustomRoles.Oracle, TabGroup.CrewmateRoles);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(CheckLimitOpt.GetFloat());
    }

    public override bool OnVote(PlayerControl player, PlayerControl target)
    {
        if (Starspawn.IsDayBreak) return false;
        if (player == null || target == null) return false;

        DidVote ??= [];
        if (DidVote.Contains(player.PlayerId) || Main.DontCancelVoteList.Contains(player.PlayerId)) return false;
        DidVote.Add(player.PlayerId);

        if (player.GetAbilityUseLimit() < 1)
        {
            Utils.SendMessage(GetString("OracleCheckReachLimit"), player.PlayerId, CustomRoles.Oracle.ColoredTextByRole(GetString("OracleCheckMsgTitle")));
            return false;
        }

        player.RpcRemoveAbilityUse();

        if (player.PlayerId == target.PlayerId)
        {
            Utils.SendMessage(GetString("OracleCheckSelfMsg") + "\n\n" + string.Format(GetString("OracleCheckLimit"), player.GetAbilityUseLimit()), player.PlayerId, CustomRoles.Oracle.ColoredTextByRole(GetString("OracleCheckMsgTitle")), importance: MessageImportance.Low);
            return false;
        }

        Team team = target.GetTeam();

        if (IRandom.Instance.Next(100) < FailChance.GetInt())
            team = Main.TeamValues[1..].Without(team).RandomElement();

        string msg = string.Format(GetString($"OracleCheck.{GetString($"ShortTeamName.{team}", SupportedLangs.English)}"), target.GetRealName());

        Utils.SendMessage($"{GetString("OracleCheck")}\n{msg}\n\n{string.Format(GetString("OracleCheckLimit"), player.GetAbilityUseLimit())}", player.PlayerId, CustomRoles.Oracle.ColoredTextByRole(GetString("OracleCheckMsgTitle")), importance: MessageImportance.High);

        Main.DontCancelVoteList.Add(player.PlayerId);
        return true;
    }

    public override void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        OnVote(shapeshifter, target);
    }
}