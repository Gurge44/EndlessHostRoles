﻿namespace EHR.Coven;

public class VoodooMaster : Coven
{
    public static bool On;

    private static OptionItem AbilityCooldown;

    public override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650050)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = AbilityCooldown.GetFloat();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        RoleBase roleBase = Main.PlayerStates[target.PlayerId].Role;
        var type = roleBase.GetType();

        if (type.GetMethod("OnCheckMurder")?.DeclaringType == type) roleBase.OnCheckMurder(target, target);
        else if (type.GetMethod("OnPet")?.DeclaringType == type) roleBase.OnPet(target);

        if (!HasNecronomicon) killer.SetKillCooldown(AbilityCooldown.GetFloat());
        return HasNecronomicon;
    }
}