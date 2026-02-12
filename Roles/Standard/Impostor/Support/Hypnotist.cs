using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Modules.Extensions;
using Hazel;

namespace EHR.Roles;

public class Hypnotist : RoleBase
{
    public static bool On;
    private static List<Hypnotist> Instances = [];

    public static OptionItem AbilityCooldown;
    public static OptionItem AbilityDuration;
    public static OptionItem AbilityUseLimit;
    public static OptionItem AbilityUseGainWithEachKill;
    public static OptionItem DoReportAfterHypnosisEnds;

    private CountdownTimer Timer;
    private byte HypnotistId;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(647550)
            .AutoSetupOption(ref AbilityCooldown, 30, new IntegerValueRule(0, 60, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityDuration, 15, new IntegerValueRule(1, 30, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachKill, 0.8f, new FloatValueRule(0f, 5f, 0.1f), OptionFormat.Times)
            .AutoSetupOption(ref DoReportAfterHypnosisEnds, true);
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
        Timer = null;
        HypnotistId = playerId;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.ShapeshifterCooldown = AbilityCooldown.GetInt();
        AURoleOptions.ShapeshifterDuration = 1f;
    }

    public static bool OnAnyoneReport()
    {
        return Instances.All(x => x.Timer == null);
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        OnPet(shapeshifter);
        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        OnPet(pc);
        return false;
    }

    public override void OnPet(PlayerControl pc)
    {
        if (pc.GetAbilityUseLimit() < 1) return;
        pc.RpcRemoveAbilityUse();

        Timer = new CountdownTimer(AbilityDuration.GetInt(), () =>
        {
            Timer = null;
            if (DoReportAfterHypnosisEnds.GetBool()) ReportDeadBodyPatch.CanReport.SetAllValues(true);
            pc.RpcResetAbilityCooldown();
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }, onTick: () =>
        {
            if (Timer.Remaining.TotalSeconds >= 6) return;
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }, onCanceled: () =>
        {
            Timer = null;
            if (DoReportAfterHypnosisEnds.GetBool()) ReportDeadBodyPatch.CanReport.SetAllValues(true);
        });
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        Utils.SendRPC(CustomRPC.SyncRoleData, HypnotistId);

        if (DoReportAfterHypnosisEnds.GetBool()) ReportDeadBodyPatch.CanReport.SetAllValues(false);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        Timer = new CountdownTimer(AbilityDuration.GetInt(), () => Timer = null, onCanceled: () => Timer = null);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || seer.PlayerId != HypnotistId || meeting || (seer.IsModdedClient() && !hud) || Timer == null) return string.Empty;

        var timeLeft = (int)Math.Ceiling(Timer.Remaining.TotalSeconds);
        return timeLeft <= 5 || hud ? $"\u25a9 ({timeLeft})" : "\u25a9";
    }
}
