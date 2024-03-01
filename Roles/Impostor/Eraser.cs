using System.Collections.Generic;
using TOHE.Roles.Crewmate;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor;

internal class Eraser : RoleBase
{
    private const int Id = 16800;
    public static List<byte> playerIdList = [];

    public static readonly string[] EraseMode =
    [
        "EKill",
        "EVote"
    ];

    public static readonly string[] WhenTargetIsNeutralAction =
    [
        "E2Block",
        "E2Kill"
    ];

    private static OptionItem EraseLimitOpt;
    private static OptionItem EraseMethod;
    private static OptionItem WhenTargetIsNeutral;
    public static OptionItem HideVote;
    public static OptionItem CancelVote;

    private static List<byte> didVote = [];
    private static List<byte> PlayerToErase = [];

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Eraser);
        EraseMethod = StringOptionItem.Create(Id + 10, "EraseMethod", EraseMode, 0, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Eraser]);
        WhenTargetIsNeutral = StringOptionItem.Create(Id + 11, "WhenTargetIsNeutral", WhenTargetIsNeutralAction, 0, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Eraser]);
        EraseLimitOpt = IntegerOptionItem.Create(Id + 12, "EraseLimit", new(1, 15, 1), 1, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Eraser])
            .SetValueFormat(OptionFormat.Times);
        HideVote = BooleanOptionItem.Create(Id + 13, "EraserHideVote", false, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Eraser]);
        CancelVote = Options.CreateVoteCancellingUseSetting(Id + 14, CustomRoles.Eraser, TabGroup.ImpostorRoles);
    }

    public override void Init()
    {
        playerIdList = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(EraseLimitOpt.GetInt());
    }

    public override bool IsEnable => playerIdList.Count > 0;

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (Medic.ProtectList.Contains(target.PlayerId)) return false;
        if (killer.GetAbilityUseLimit() < 1) return true;
        if (target.PlayerId == killer.PlayerId) return true;
        if (target.GetCustomRole().IsNeutral() && EraseMethod.GetInt() == 0)
        {
            killer.Notify(GetString("EraserEraseNeutralNotice"));
            if (WhenTargetIsNeutral.GetInt() == 1) return true;
            killer.SetKillCooldown(5f);
            return false;
        }

        if (EraseMethod.GetInt() == 0)
        {
            if (killer.GetAbilityUseLimit() < 1) return true;
            return killer.CheckDoubleTrigger(target, () =>
            {
                killer.RpcRemoveAbilityUse();
                killer.SetKillCooldown();
                killer.Notify(GetString("TargetErasedInRound"));
                if (!PlayerToErase.Contains(target.PlayerId))
                    PlayerToErase.Add(target.PlayerId);
            });
        }

        return false;
    }

    public static bool OnVote(PlayerControl player, PlayerControl target)
    {
        if (player == null || target == null || EraseMethod.GetInt() == 0) return false;
        if (didVote.Contains(player.PlayerId) || Main.DontCancelVoteList.Contains(player.PlayerId)) return false;
        didVote.Add(player.PlayerId);

        if (player.GetAbilityUseLimit() < 1) return false;

        if (target.PlayerId == player.PlayerId)
        {
            Utils.SendMessage(GetString("EraserEraseSelf"), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Eraser), GetString("EraserEraseMsgTitle")));
            return false;
        }

        if (target.GetCustomRole().IsNeutral())
        {
            Utils.SendMessage(string.Format(GetString("EraserEraseNeutralNotice"), target.GetRealName()), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Eraser), GetString("EraserEraseMsgTitle")));
            return false;
        }

        player.RpcRemoveAbilityUse();

        if (!PlayerToErase.Contains(target.PlayerId))
            PlayerToErase.Add(target.PlayerId);

        Utils.SendMessage(string.Format(GetString("EraserEraseNotice"), target.GetRealName()), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Eraser), GetString("EraserEraseMsgTitle")));

        if (GameStates.IsInTask) Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: target);

        Main.DontCancelVoteList.Add(player.PlayerId);
        return true;
    }

    public override void OnReportDeadBody()
    {
        didVote = [];
    }

    public override void AfterMeetingTasks()
    {
        foreach (byte pc in PlayerToErase.ToArray())
        {
            var player = Utils.GetPlayerById(pc);
            if (player == null) continue;
            player.RpcSetCustomRole(player.GetCustomRole().GetErasedRole());
            player.Notify(GetString("LostRoleByEraser"));
            Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()} 被擦除了", "Eraser");
            player.MarkDirtySettings();
        }

        PlayerToErase = [];
    }
}