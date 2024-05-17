using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Roles.Neutral;

public class Jackal : RoleBase
{
    private const int Id = 12100;
    public static List<byte> PlayerIdList = [];

    public static OptionItem KillCooldown;
    public static OptionItem CanVent;
    public static OptionItem CanSabotage;
    public static OptionItem CanWinBySabotageWhenNoImpAlive;
    public static OptionItem HasImpostorVision;
    public static OptionItem ResetKillCooldownWhenSbGetKilled;
    private static OptionItem ResetKillCooldownOn;
    public static OptionItem CanRecruitSidekick;
    public static OptionItem SidekickRecruitLimitOpt;
    public static OptionItem SidekickCountMode;
    public static OptionItem SidekickAssignMode;
    public static OptionItem KillCooldownSK;
    public static OptionItem CanVentSK;
    public static OptionItem CanSabotageSK;

    public static readonly string[] SidekickAssignModeStrings =
    [
        "SidekickAssignMode.SidekickAndRecruit",
        "SidekickAssignMode.Sidekick",
        "SidekickAssignMode.Recruit",
    ];

    public static bool On;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Jackal);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Jackal])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 11, "CanVent", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        CanSabotage = BooleanOptionItem.Create(Id + 12, "CanSabotage", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        CanWinBySabotageWhenNoImpAlive = BooleanOptionItem.Create(Id + 14, "JackalCanWinBySabotageWhenNoImpAlive", true, TabGroup.NeutralRoles).SetParent(CanSabotage);
        HasImpostorVision = BooleanOptionItem.Create(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        ResetKillCooldownWhenSbGetKilled = BooleanOptionItem.Create(Id + 16, "ResetKillCooldownWhenPlayerGetKilled", false, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        ResetKillCooldownOn = FloatOptionItem.Create(Id + 28, "ResetKillCooldownOn", new(0f, 180f, 2.5f), 15f, TabGroup.NeutralRoles)
            .SetParent(ResetKillCooldownWhenSbGetKilled)
            .SetValueFormat(OptionFormat.Seconds);
        JackalCanKillSidekick = BooleanOptionItem.Create(Id + 15, "JackalCanKillSidekick", false, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        CanRecruitSidekick = BooleanOptionItem.Create(Id + 17, "JackalCanRecruitSidekick", false, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        SidekickAssignMode = StringOptionItem.Create(Id + 29, "SidekickAssignMode", SidekickAssignModeStrings, 0, TabGroup.NeutralRoles).SetParent(CanRecruitSidekick);
        SidekickRecruitLimitOpt = IntegerOptionItem.Create(Id + 18, "JackalSidekickRecruitLimit", new(0, 15, 1), 0, TabGroup.NeutralRoles).SetParent(CanRecruitSidekick)
            .SetValueFormat(OptionFormat.Times);
        KillCooldownSK = FloatOptionItem.Create(Id + 20, "KillCooldown", new(0f, 180f, 2.5f), 20f, TabGroup.NeutralRoles).SetParent(CanRecruitSidekick)
            .SetValueFormat(OptionFormat.Seconds);
        CanVentSK = BooleanOptionItem.Create(Id + 21, "CanVent", true, TabGroup.NeutralRoles).SetParent(CanRecruitSidekick);
        CanSabotageSK = BooleanOptionItem.Create(Id + 22, "CanSabotage", true, TabGroup.NeutralRoles).SetParent(CanRecruitSidekick);
        SidekickCanKillJackal = BooleanOptionItem.Create(Id + 23, "SidekickCanKillJackal", false, TabGroup.NeutralRoles).SetParent(CanRecruitSidekick);
        //  SidekickKnowOtherSidekick = BooleanOptionItem.Create(6050585, "SidekickKnowOtherSidekick", false, TabGroup.NeutralRoles, false).SetParent(CanRecruitSidekick);
        //  SidekickKnowOtherSidekickRole = BooleanOptionItem.Create(6050590, "SidekickKnowOtherSidekickRole", false, TabGroup.NeutralRoles, false).SetParent(SidekickKnowOtherSidekick);
        SidekickCanKillSidekick = BooleanOptionItem.Create(Id + 24, "SidekickCanKillSidekick", false, TabGroup.NeutralRoles).SetParent(CanRecruitSidekick);
        SidekickCountMode = StringOptionItem.Create(Id + 25, "SidekickCountMode", Options.SidekickCountMode, 0, TabGroup.NeutralRoles).SetParent(CanRecruitSidekick);
        //   SidekickCanWinWithOriginalTeam = BooleanOptionItem.Create(6050795, "SidekickCanWinWithOriginalTeam", false, TabGroup.NeutralRoles, false).SetParent(CanRecruitSidekick);
    }

    public override void Init()
    {
        PlayerIdList = [];
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(SidekickRecruitLimitOpt.GetInt());

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public static bool CanRecruit(byte id) => id.GetAbilityUseLimit() > 0;
    public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();
    public override bool CanUseSabotage(PlayerControl pc) => CanSabotage.GetBool() && pc.IsAlive();

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton.OverrideText(CanRecruit(id) ? $"{GetString("GangsterButtonText")}" : $"{GetString("KillButtonText")}");
        hud.SabotageButton.ToggleVisible(CanSabotage.GetBool());
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());

    public static void AfterPlayerDiedTask(PlayerControl target)
    {
        if (target.Is(CustomRoles.Jackal)) return;
        Main.AllAlivePlayerControls
            .Where(x => x.Is(CustomRoles.Jackal))
            .Do(x => x.SetKillCooldown(ResetKillCooldownOn.GetFloat()));
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!CanRecruitSidekick.GetBool() || killer.GetAbilityUseLimit() < 1) return true;
        if (SidekickAssignMode.GetValue() != 2)
        {
            if (CanBeSidekick(target))
            {
                killer.RpcRemoveAbilityUse();
                target.RpcSetCustomRole(CustomRoles.Sidekick);

                if (!Main.ResetCamPlayerList.Contains(target.PlayerId))
                    Main.ResetCamPlayerList.Add(target.PlayerId);

                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), GetString("GangsterSuccessfullyRecruited")));
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), GetString("BeRecruitedByJackal")));

                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer, ForceLoop: true);

                killer.ResetKillCooldown();
                killer.SetKillCooldown();
                //killer.RpcGuardAndKill(target);
                target.RpcGuardAndKill(killer);
                target.RpcGuardAndKill(target);

                Logger.Info("SetRole:" + target.Data?.PlayerName + " = " + target.GetCustomRole() + " + " + CustomRoles.Sidekick, "Assign " + CustomRoles.Sidekick);
                return false;
            }
        }

        if (SidekickAssignMode.GetValue() != 1)
        {
            if (!CanBeSidekick(target) && !target.Is(CustomRoles.Sidekick) && !target.Is(CustomRoles.Recruit) && !target.Is(CustomRoles.Loyal))
            {
                killer.RpcRemoveAbilityUse();
                target.RpcSetCustomRole(CustomRoles.Recruit);

                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), GetString("GangsterSuccessfullyRecruited")));
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), GetString("BeRecruitedByJackal")));

                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer, ForceLoop: true);

                killer.ResetKillCooldown();
                killer.SetKillCooldown();
                //killer.RpcGuardAndKill(target);
                target.RpcGuardAndKill(killer);
                target.RpcGuardAndKill(target);

                Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole() + " + " + CustomRoles.Sidekick, "Assign " + CustomRoles.Sidekick);
                return false;
            }
        }

        return false;
    }

    public static bool CanBeSidekick(PlayerControl pc)
    {
        return pc != null && !pc.Is(CustomRoles.Sidekick) && !pc.Is(CustomRoles.Recruit) && !pc.Is(CustomRoles.Loyal) && !pc.Is(CustomRoles.Rascal) && !pc.Is(CustomRoles.Madmate) && !pc.Is(CustomRoles.Charmed) && !pc.Is(CustomRoles.DualPersonality) && !pc.Is(CustomRoles.Contagious) && pc.GetCustomRole().IsAbleToBeSidekicked();
    }
}