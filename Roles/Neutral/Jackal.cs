using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Neutral;

public class Jackal : RoleBase
{
    private const int Id = 12100;
    public static List<Jackal> Instances = [];

    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    public static OptionItem CanSabotage;
    public static OptionItem HasImpostorVision;
    public static OptionItem ResetKillCooldownWhenSbGetKilled;
    private static OptionItem ResetKillCooldownOn;
    private static OptionItem CanRecruitImpostors;
    private static OptionItem CanRecruitMadmates;
    public static OptionItem SidekickCountMode;
    public static OptionItem SKCanKill;
    public static OptionItem KillCooldownSK;
    public static OptionItem CanVentSK;
    public static OptionItem CanSabotageSK;
    private static OptionItem SKPromotesToJackal;
    private static OptionItem PromotedSKCanRecruit;

    public static bool On;

    public byte SidekickId;

    public override bool IsEnable => Instances.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Jackal);
        KillCooldown = new FloatOptionItem(Id + 2, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jackal])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = new BooleanOptionItem(Id + 3, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        CanSabotage = new BooleanOptionItem(Id + 4, "CanSabotage", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        HasImpostorVision = new BooleanOptionItem(Id + 6, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        ResetKillCooldownWhenSbGetKilled = new BooleanOptionItem(Id + 7, "ResetKillCooldownWhenPlayerGetKilled", false, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        ResetKillCooldownOn = new FloatOptionItem(Id + 8, "ResetKillCooldownOn", new(0f, 180f, 2.5f), 15f, TabGroup.NeutralRoles)
            .SetParent(ResetKillCooldownWhenSbGetKilled)
            .SetValueFormat(OptionFormat.Seconds);
        var SKOpts = new BooleanOptionItem(Id + 9, "SidekickSettings", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        CanRecruitImpostors = new BooleanOptionItem(Id + 10, "JackalCanRecruitImpostors", true, TabGroup.NeutralRoles)
            .SetParent(SKOpts);
        CanRecruitMadmates = new BooleanOptionItem(Id + 11, "JackalCanRecruitMadmates", true, TabGroup.NeutralRoles)
            .SetParent(CanRecruitImpostors);
        JackalCanKillSidekick = new BooleanOptionItem(Id + 12, "JackalCanKillSidekick", false, TabGroup.NeutralRoles)
            .SetParent(SKOpts);
        SKCanKill = new BooleanOptionItem(Id + 13, "SKCanKill", true, TabGroup.NeutralRoles)
            .SetParent(SKOpts);
        KillCooldownSK = new FloatOptionItem(Id + 14, "KillCooldown", new(0f, 180f, 2.5f), 20f, TabGroup.NeutralRoles)
            .SetParent(SKCanKill)
            .SetValueFormat(OptionFormat.Seconds);
        SidekickCanKillJackal = new BooleanOptionItem(Id + 15, "SidekickCanKillJackal", false, TabGroup.NeutralRoles)
            .SetParent(SKCanKill);
        SidekickCanKillSidekick = new BooleanOptionItem(Id + 16, "SidekickCanKillSidekick", false, TabGroup.NeutralRoles)
            .SetParent(SKCanKill);
        CanVentSK = new BooleanOptionItem(Id + 17, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(SKOpts);
        CanSabotageSK = new BooleanOptionItem(Id + 18, "CanSabotage", true, TabGroup.NeutralRoles)
            .SetParent(SKOpts);
        SKPromotesToJackal = new BooleanOptionItem(Id + 19, "SKPromotesToJackal", true, TabGroup.NeutralRoles)
            .SetParent(SKOpts);
        PromotedSKCanRecruit = new BooleanOptionItem(Id + 20, "PromotedSKCanRecruit", true, TabGroup.NeutralRoles)
            .SetParent(SKPromotesToJackal);
        SidekickCountMode = new StringOptionItem(Id + 21, "SidekickCountMode", Options.SidekickCountMode, 0, TabGroup.NeutralRoles)
            .SetParent(SKOpts);
    }

    public override void Init()
    {
        Instances = [];
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        Instances.Add(this);
        playerId.SetAbilityUseLimit(1);
        SidekickId = byte.MaxValue;
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();
    public override bool CanUseSabotage(PlayerControl pc) => base.CanUseSabotage(pc) || (CanSabotage.GetBool() && pc.IsAlive());

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton.OverrideText(id.GetAbilityUseLimit() > 0 ? $"{GetString("GangsterButtonText")}" : $"{GetString("KillButtonText")}");
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
        if (killer.GetAbilityUseLimit() < 1 || !CanBeSidekick(target)) return true;

        killer.RpcRemoveAbilityUse();
        target.RpcSetCustomRole(CustomRoles.Sidekick);
        target.RpcChangeRoleBasis(CustomRoles.Sidekick);
        SidekickId = target.PlayerId;

        Main.ResetCamPlayerList.Add(target.PlayerId);

        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), GetString("GangsterSuccessfullyRecruited")));
        target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), GetString("BeRecruitedByJackal")));

        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer, ForceLoop: true);

        killer.SetKillCooldown(3f);
        target.RpcGuardAndKill(killer);
        target.RpcGuardAndKill(target);

        Logger.Info($" {target.Data?.PlayerName} = {target.GetCustomRole()} + {CustomRoles.Sidekick}", $"Assign {CustomRoles.Sidekick}");
        return false;
    }

    private static bool CanBeSidekick(PlayerControl pc)
    {
        if (!CanRecruitImpostors.GetBool() && pc.Is(CustomRoleTypes.Impostor)) return false;
        if (!CanRecruitMadmates.GetBool() && pc.IsMadmate()) return false;
        return pc != null && !pc.Is(CustomRoles.Sidekick) && !pc.IsConverted() && pc.GetCustomRole().IsAbleToBeSidekicked();
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (pc.IsAlive()) return;
        PromoteSidekick();
    }

    public void PromoteSidekick()
    {
        try
        {
            if (!SKPromotesToJackal.GetBool()) return;
            var sk = SidekickId.GetPlayer();
            if (sk == null || !sk.Is(CustomRoles.Sidekick)) return;
            sk.RpcSetCustomRole(CustomRoles.Jackal);
            if (!PromotedSKCanRecruit.GetBool()) sk.SetAbilityUseLimit(0);
        }
        catch (Exception e)
        {
            Utils.ThrowException(e);
        }
    }
}

public class Sidekick : RoleBase
{
    private static List<byte> PlayerIdList = [];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
    }

    public override void Init()
    {
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        new[] { CustomRoles.Damocles, CustomRoles.Stressed }.Do(x => Main.PlayerStates[playerId].RemoveSubRole(x));
    }

    public override bool CanUseKillButton(PlayerControl pc) => Jackal.SKCanKill.GetBool();
    public override bool CanUseImpostorVentButton(PlayerControl pc) => Jackal.CanVentSK.GetBool();
    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = Jackal.KillCooldownSK.GetFloat();
    public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(Jackal.HasImpostorVision.GetBool());
    public override bool CanUseSabotage(PlayerControl pc) => base.CanUseSabotage(pc) || (Jackal.CanSabotageSK.GetBool() && pc.IsAlive());

    public override void SetButtonTexts(HudManager __instance, byte id)
    {
        __instance.SabotageButton.ToggleVisible(Jackal.CanSabotageSK.GetBool());
        __instance.KillButton?.OverrideText(GetString("KillButtonText"));
        __instance.ImpostorVentButton?.OverrideText(GetString("ReportButtonText"));
        __instance.SabotageButton?.OverrideText(GetString("SabotageButtonText"));
    }
}