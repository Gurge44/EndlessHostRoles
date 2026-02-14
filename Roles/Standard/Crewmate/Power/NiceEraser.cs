using System.Collections.Generic;
using Hazel;
using static EHR.Translator;

namespace EHR.Roles;

internal class NiceEraser : RoleBase
{
    private const int Id = 5580;
    public static List<byte> PlayerIdList = [];

    private static OptionItem EraseLimitOpt;
    public static OptionItem HideVote;
    public static OptionItem CancelVote;
    public static OptionItem NiceEraserAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;

    private static List<byte> DidVote = [];
    private static List<byte> PlayerToErase = [];
    public static List<byte> ErasedPlayers = [];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.NiceEraser);

        EraseLimitOpt = new IntegerOptionItem(Id + 2, "EraseLimit", new(1, 15, 1), 1, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceEraser])
            .SetValueFormat(OptionFormat.Times);

        HideVote = new BooleanOptionItem(Id + 3, "NiceEraserHideVote", false, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceEraser]);
        CancelVote = Options.CreateVoteCancellingUseSetting(Id + 4, CustomRoles.NiceEraser, TabGroup.CrewmateRoles);
        
        NiceEraserAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 5, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.4f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceEraser])
            .SetValueFormat(OptionFormat.Times);
        
        AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 6, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceEraser])
            .SetValueFormat(OptionFormat.Times);
        
    }

    public override void Init()
    {
        PlayerIdList = [];
        ErasedPlayers = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(EraseLimitOpt.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override bool OnVote(PlayerControl player, PlayerControl target)
    {
        if (Starspawn.IsDayBreak) return false;
        if (player == null || target == null) return false;

        if (DidVote.Contains(player.PlayerId) || Main.DontCancelVoteList.Contains(player.PlayerId)) return false;

        DidVote.Add(player.PlayerId);

        if (player.GetAbilityUseLimit() < 1) return false;

        if (target.PlayerId == player.PlayerId)
        {
            Utils.SendMessage(GetString("EraserEraseSelf"), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.NiceEraser), GetString("EraserEraseMsgTitle")), importance: MessageImportance.Low);
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

        if (GameStates.IsInTask)
            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: target);

        Main.DontCancelVoteList.Add(player.PlayerId);
        return true;
    }

    public override void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        OnVote(shapeshifter, target);
    }

    public override void OnReportDeadBody()
    {
        PlayerToErase = [];
        DidVote = [];
    }

    public override void AfterMeetingTasks()
    {
        PlayerToErase.ForEach(pc =>
        {
            PlayerControl player = Utils.GetPlayerById(pc);
            if (player == null) return;

            CustomRoles erasedRole = player.IsImpostor() ? CustomRoles.ImpostorEHR : player.IsCrewmate() ? CustomRoles.CrewmateEHR : player.Is(Team.Coven) ? CustomRoles.CovenMember : CustomRoles.Amnesiac;
            player.RpcSetCustomRole(erasedRole);
            player.RpcChangeRoleBasis(erasedRole);
            player.Notify(GetString("LostRoleByNiceEraser"));
            Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()} lost their role", "NiceEraser");
            ErasedPlayers.Add(pc);
        });
    }
}