using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Neutral;

public class Jackal : RoleBase
{
    private const int Id = 12100;
    private static List<byte> PlayerIdList = [];

    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    public static OptionItem CanSabotage;
    public static OptionItem CanWinBySabotageWhenNoImpAlive;
    public static OptionItem HasImpostorVision;
    public static OptionItem ResetKillCooldownWhenSbGetKilled;
    private static OptionItem ResetKillCooldownOn;
    private static OptionItem CanRecruitImpostors;
    public static OptionItem SidekickCountMode;
    public static OptionItem SKCanKill;
    public static OptionItem KillCooldownSK;
    public static OptionItem CanVentSK;
    public static OptionItem CanSabotageSK;
    private static OptionItem SKPromotesToJackal;
    private static OptionItem PromotedSKCanRecruit;

    public static bool On;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Jackal);
        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jackal])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = new BooleanOptionItem(Id + 11, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        CanSabotage = new BooleanOptionItem(Id + 12, "CanSabotage", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        CanWinBySabotageWhenNoImpAlive = new BooleanOptionItem(Id + 14, "JackalCanWinBySabotageWhenNoImpAlive", true, TabGroup.NeutralRoles)
            .SetParent(CanSabotage);
        HasImpostorVision = new BooleanOptionItem(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        ResetKillCooldownWhenSbGetKilled = new BooleanOptionItem(Id + 16, "ResetKillCooldownWhenPlayerGetKilled", false, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        ResetKillCooldownOn = new FloatOptionItem(Id + 28, "ResetKillCooldownOn", new(0f, 180f, 2.5f), 15f, TabGroup.NeutralRoles)
            .SetParent(ResetKillCooldownWhenSbGetKilled)
            .SetValueFormat(OptionFormat.Seconds);
        var SKOpts = new BooleanOptionItem(Id + 17, "SidekickSettings", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        CanRecruitImpostors = new BooleanOptionItem(Id + 18, "JackalCanRecruitImpostors", true, TabGroup.NeutralRoles)
            .SetParent(SKOpts);
        JackalCanKillSidekick = new BooleanOptionItem(Id + 15, "JackalCanKillSidekick", false, TabGroup.NeutralRoles)
            .SetParent(SKOpts);
        SKCanKill = new BooleanOptionItem(Id + 19, "SKCanKill", true, TabGroup.NeutralRoles)
            .SetParent(SKOpts);
        KillCooldownSK = new FloatOptionItem(Id + 20, "KillCooldown", new(0f, 180f, 2.5f), 20f, TabGroup.NeutralRoles)
            .SetParent(SKCanKill)
            .SetValueFormat(OptionFormat.Seconds);
        SidekickCanKillJackal = new BooleanOptionItem(Id + 23, "SidekickCanKillJackal", false, TabGroup.NeutralRoles)
            .SetParent(SKCanKill);
        SidekickCanKillSidekick = new BooleanOptionItem(Id + 24, "SidekickCanKillSidekick", false, TabGroup.NeutralRoles)
            .SetParent(SKCanKill);
        CanVentSK = new BooleanOptionItem(Id + 21, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(SKOpts);
        CanSabotageSK = new BooleanOptionItem(Id + 22, "CanSabotage", true, TabGroup.NeutralRoles)
            .SetParent(SKOpts);
        SKPromotesToJackal = new BooleanOptionItem(Id + 26, "SKPromotesToJackal", true, TabGroup.NeutralRoles)
            .SetParent(SKOpts);
        PromotedSKCanRecruit = new BooleanOptionItem(Id + 27, "PromotedSKCanRecruit", true, TabGroup.NeutralRoles)
            .SetParent(SKPromotesToJackal);
        SidekickCountMode = new StringOptionItem(Id + 25, "SidekickCountMode", Options.SidekickCountMode, 0, TabGroup.NeutralRoles)
            .SetParent(SKOpts);
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
        playerId.SetAbilityUseLimit(1);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    private static bool CanRecruit(byte id) => id.GetAbilityUseLimit() > 0;
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
        if (killer.GetAbilityUseLimit() < 1 || !CanBeSidekick(target, out var needBasisChange, out var targetRoleType)) return true;

        killer.RpcRemoveAbilityUse();
        target.RpcSetCustomRole(targetRoleType == RoleTypes.Shapeshifter ? CustomRoles.Recruit : CustomRoles.Sidekick);
        if (needBasisChange) target.ChangeRoleBasis(RoleTypes.Impostor);

        if (!Main.ResetCamPlayerList.Contains(target.PlayerId))
            Main.ResetCamPlayerList.Add(target.PlayerId);

        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), GetString("GangsterSuccessfullyRecruited")));
        target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), GetString("BeRecruitedByJackal")));

        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer, ForceLoop: true);

        killer.ResetKillCooldown();
        target.RpcGuardAndKill(killer);
        target.RpcGuardAndKill(target);

        Logger.Info($" {target.Data?.PlayerName} = {target.GetCustomRole()} + {CustomRoles.Sidekick}", "Assign " + CustomRoles.Sidekick);
        return false;
    }

    private static bool CanBeSidekick(PlayerControl pc, out bool needBasisChange, out RoleTypes targetRoleType)
    {
        targetRoleType = pc.GetRoleTypes();
        needBasisChange = targetRoleType is not RoleTypes.Impostor and not RoleTypes.Shapeshifter;
        if (!CanRecruitImpostors.GetBool() && pc.Is(Team.Impostor)) return false;
        return pc != null && !pc.Is(CustomRoles.Sidekick) && !pc.Is(CustomRoles.Recruit) && !pc.Is(CustomRoles.Loyal) && !pc.Is(CustomRoles.Rascal) && !pc.Is(CustomRoles.Madmate) && !pc.Is(CustomRoles.Charmed) && !pc.Is(CustomRoles.Contagious) && pc.GetCustomRole().IsAbleToBeSidekicked();
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!SKPromotesToJackal.GetBool() || pc.IsAlive()) return;
        var sk = Main.AllPlayerControls.FirstOrDefault(x => x.Is(CustomRoles.Sidekick));
        if (sk == null) return;
        sk.RpcSetCustomRole(CustomRoles.Jackal);
        if (!PromotedSKCanRecruit.GetBool()) sk.SetAbilityUseLimit(0);
    }
}

public class Sidekick : RoleBase
{
    private static List<byte> PlayerIdList = [];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void Init()
    {
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        new[] { CustomRoles.Damocles, CustomRoles.Stressed }.Do(x => Main.PlayerStates[playerId].RemoveSubRole(x));

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool CanUseKillButton(PlayerControl pc) => Jackal.SKCanKill.GetBool();
    public override bool CanUseImpostorVentButton(PlayerControl pc) => Jackal.CanVentSK.GetBool();
    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = Jackal.KillCooldownSK.GetFloat();
    public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(Jackal.HasImpostorVision.GetBool());
    public override bool CanUseSabotage(PlayerControl pc) => Jackal.CanSabotageSK.GetBool() && pc.IsAlive();

    public override void SetButtonTexts(HudManager __instance, byte id)
    {
        __instance.SabotageButton.ToggleVisible(Jackal.CanSabotageSK.GetBool());
        __instance.KillButton?.OverrideText(GetString("KillButtonText"));
        __instance.ImpostorVentButton?.OverrideText(GetString("ReportButtonText"));
        __instance.SabotageButton?.OverrideText(GetString("SabotageButtonText"));
    }
}