using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Crewmate;

public class Vacuum : RoleBase
{
    public static bool On;
    private static List<Vacuum> Instances = [];

    public override bool IsEnable => On;
    
    public static OptionItem AbilityCooldown;
    public static OptionItem AbilityDuration;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachTaskCompleted;
    private static OptionItem AbilityChargesWhenFinishedTasks;

    private byte VacuumId;
    private long AbilityEndTS;

    public override void SetupCustomOption()
    {
        StartSetup(655600)
            .AutoSetupOption(ref AbilityCooldown, 30, new IntegerValueRule(0, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityDuration, 15, new IntegerValueRule(0, 60, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachTaskCompleted, 1f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityChargesWhenFinishedTasks, 0.2f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        AbilityEndTS = 0;
        VacuumId = playerId;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
        Instances.Add(this);
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        StringBuilder sb = new();
        sb.Append(Utils.GetAbilityUseLimitDisplay(playerId, AbilityEndTS != 0));
        sb.Append(Utils.GetTaskCount(playerId, comms));
        return sb.ToString();
    }
    
    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (Options.UsePets.GetBool()) return;

        AURoleOptions.EngineerCooldown = AbilityCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 1f;

        try { AURoleOptions.GuardianAngelCooldown = 900f; }
        catch { }
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (Options.UsePets.GetBool()) return;
        OnPet(pc);
    }

    public override void OnPet(PlayerControl pc)
    {
        if (pc.GetAbilityUseLimit() < 1) return;
        pc.RpcRemoveAbilityUse();
        AbilityEndTS = Utils.TimeStamp + AbilityDuration.GetInt();
        Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, AbilityEndTS);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }
    
    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (AbilityEndTS == 0) return;
        if (AbilityEndTS > Utils.TimeStamp) return;
        AbilityEndTS = 0;
        Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, AbilityEndTS);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        AbilityEndTS = long.Parse(reader.ReadString());
    }

    public static bool BeforeMurderCheck(PlayerControl target)
    {
        try
        {
            foreach (Vacuum instance in Instances)
            {
                try
                {
                    if (instance.AbilityEndTS == 0) continue;
                    PlayerControl vacuum = instance.VacuumId.GetPlayer();
                    if (vacuum == null || !vacuum.IsAlive()) continue;
                    target.TP(vacuum);
                    return false;
                }
                catch (Exception e) { Utils.ThrowException(e); }
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }

        return true;
    }
}