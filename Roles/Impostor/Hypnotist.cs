using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Impostor;

public class Hypnotist : RoleBase
{
    public static bool On;
    private static List<Hypnotist> Instances = [];

    public static OptionItem AbilityCooldown;
    public static OptionItem AbilityDuration;
    public static OptionItem AbilityUseLimit;
    public static OptionItem AbilityUseGainWithEachKill;
    public static OptionItem DoReportAfterHypnosisEnds;

    private long ActivateTS;
    private int Count;
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
        Count = 0;
        ActivateTS = 0;
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
        return Instances.All(x => x.ActivateTS == 0);
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

        Count = 0;
        ActivateTS = Utils.TimeStamp;
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        Utils.SendRPC(CustomRPC.SyncRoleData, HypnotistId, ActivateTS);

        if (DoReportAfterHypnosisEnds.GetBool()) ReportDeadBodyPatch.CanReport.SetAllValues(false);
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (ActivateTS == 0) return;

        var notify = false;
        var timeLeft = (int)(ActivateTS + AbilityDuration.GetInt() - Utils.TimeStamp);

        switch (timeLeft)
        {
            case <= 0:
                ActivateTS = 0;
                notify = true;
                pc.RpcResetAbilityCooldown();
                Utils.SendRPC(CustomRPC.SyncRoleData, HypnotistId, ActivateTS);
                if (DoReportAfterHypnosisEnds.GetBool()) ReportDeadBodyPatch.CanReport.SetAllValues(true);
                break;
            case <= 6 when Count++ >= 30:
                Count = 0;
                notify = true;
                break;
        }

        if (notify) Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        ActivateTS = long.Parse(reader.ReadString());
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || seer.PlayerId != HypnotistId || meeting || (seer.IsModdedClient() && !hud) || ActivateTS == 0) return string.Empty;

        var timeLeft = (int)(ActivateTS + AbilityDuration.GetInt() - Utils.TimeStamp);
        return timeLeft <= 5 ? $"\u25a9 ({timeLeft})" : "\u25a9";
    }
}
