using System.Collections.Generic;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate;

internal static class NiceEraser
{
    private static readonly int Id = 5580;
    public static List<byte> playerIdList = [];

    private static OptionItem EraseLimitOpt;
    public static OptionItem HideVote;
    public static OptionItem CancelVote;

    private static List<byte> didVote = [];
    private static List<byte> PlayerToErase = [];

    public static void SetupCustomOption()
    {
        Options.SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.NiceEraser, 1);
        EraseLimitOpt = IntegerOptionItem.Create(Id + 2, "EraseLimit", new(1, 15, 1), 1, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceEraser])
            .SetValueFormat(OptionFormat.Times);
        HideVote = BooleanOptionItem.Create(Id + 3, "NiceEraserHideVote", false, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceEraser]);
        CancelVote = Options.CreateVoteCancellingUseSetting(Id + 4, CustomRoles.NiceEraser, TabGroup.CrewmateRoles);
    }

    public static void Init()
    {
        playerIdList = [];
    }

    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(EraseLimitOpt.GetInt());
    }

    public static bool IsEnable => playerIdList.Count > 0;
    public static string GetProgressText(byte playerId) => Utils.GetAbilityUseLimitDisplay(playerId);

    public static bool OnVote(PlayerControl player, PlayerControl target)
    {
        if (player == null || target == null) return false;
        if (didVote.Contains(player.PlayerId) || Main.DontCancelVoteList.Contains(player.PlayerId)) return false;
        didVote.Add(player.PlayerId);

        if (player.GetAbilityUseLimit() < 1) return false;

        if (target.PlayerId == player.PlayerId)
        {
            Utils.SendMessage(GetString("EraserEraseSelf"), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.NiceEraser), GetString("EraserEraseMsgTitle")));
            return false;
        }

        if (target.GetCustomRole().IsNeutral())
        {
            Utils.SendMessage(string.Format(GetString("EraserEraseNeutralNotice"), target.GetRealName()), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.NiceEraser), GetString("EraserEraseMsgTitle")));
            return false;
        }

        player.RpcRemoveAbilityUse();

        if (!PlayerToErase.Contains(target.PlayerId))
            PlayerToErase.Add(target.PlayerId);

        Utils.SendMessage(string.Format(GetString("EraserEraseNotice"), target.GetRealName()), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.NiceEraser), GetString("EraserEraseMsgTitle")));

        if (GameStates.IsInTask) Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: target);

        Main.DontCancelVoteList.Add(player.PlayerId);
        return true;
    }

    public static void OnReportDeadBody()
    {
        PlayerToErase = [];
        didVote = [];
    }

    public static void AfterMeetingTasks()
    {
        foreach (byte pc in PlayerToErase.ToArray())
        {
            var player = Utils.GetPlayerById(pc);
            if (player == null) continue;
            player.RpcSetCustomRole(player.GetCustomRole().GetErasedRole());
            player.Notify(GetString("LostRoleByNiceEraser"));
            Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()} 被擦除了", "NiceEraser");
            player.MarkDirtySettings();
        }
    }
}