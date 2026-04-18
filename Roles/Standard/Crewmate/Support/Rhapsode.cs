using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Modules.Extensions;
using Hazel;

namespace EHR.Roles;

public class Rhapsode : RoleBase
{
    public static bool On;
    private static List<Rhapsode> Instances = [];

    public static OptionItem AbilityCooldown;
    public static OptionItem AbilityDuration;
    private static OptionItem ExcludeCrewmates;
    private static OptionItem AbilityUseLimit;
    public static OptionItem RhapsodeAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;
    
    private CountdownTimer Timer;
    private byte RhapsodeId;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        var id = 647650;
        Options.SetupRoleOptions(id++, TabGroup.CrewmateRoles, CustomRoles.Rhapsode);

        AbilityCooldown = new IntegerOptionItem(++id, "AbilityCooldown", new(0, 60, 1), 30, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Rhapsode])
            .SetValueFormat(OptionFormat.Seconds);

        AbilityDuration = new IntegerOptionItem(++id, "AbilityDuration", new(0, 60, 1), 10, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Rhapsode])
            .SetValueFormat(OptionFormat.Seconds);

        ExcludeCrewmates = new BooleanOptionItem(++id, "Rhapsode.ExcludeCrewmates", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Rhapsode]);

        AbilityUseLimit = new FloatOptionItem(++id, "AbilityUseLimit", new(0, 20, 0.05f), 1, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Rhapsode])
            .SetValueFormat(OptionFormat.Times);

        RhapsodeAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(++id, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.3f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Rhapsode])
            .SetValueFormat(OptionFormat.Times);

        AbilityChargesWhenFinishedTasks = new FloatOptionItem(++id, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Rhapsode])
            .SetValueFormat(OptionFormat.Times);
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
        RhapsodeId = playerId;
        Timer = null;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (Options.UsePets.GetBool()) return;

        AURoleOptions.EngineerCooldown = AbilityCooldown.GetInt();
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (Options.UsePets.GetBool()) return;
        ActivateAbility(pc);
    }

    public override void OnPet(PlayerControl pc)
    {
        ActivateAbility(pc);
    }

    private void ActivateAbility(PlayerControl pc)
    {
        Timer = new CountdownTimer(AbilityDuration.GetInt(), () => Timer = null, onTick: () => Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc), onCanceled: () => Timer = null);
        Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public static bool CheckAbilityUse(PlayerControl pc)
    {
        if (pc.IsCrewmate() && ExcludeCrewmates.GetBool()) return true;
        return Instances.All(x => x.Timer == null);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        Timer = new CountdownTimer(AbilityDuration.GetInt(), () => Timer = null, onCanceled: () => Timer = null);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || seer.PlayerId != RhapsodeId || hud || meeting || Timer == null) return string.Empty;
        return $"\u25b6 ({(int)Math.Ceiling(Timer.Remaining.TotalSeconds)}s)";
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.Is(CustomRoles.Nimble) || pc.GetClosestVent()?.Id == ventId;
    }
}