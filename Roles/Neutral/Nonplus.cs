﻿namespace EHR.Neutral;

public class Nonplus : RoleBase
{
    public static bool On;

    public static OptionItem BlindCooldown;
    public static OptionItem BlindDuration;
    public static OptionItem UseLimit;
    public static OptionItem NonplusAbilityUseGainWithEachKill;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        const int id = 649500;
        const TabGroup tab = TabGroup.NeutralRoles;
        const CustomRoles role = CustomRoles.Nonplus;

        Options.SetupRoleOptions(id, tab, role);

        BlindCooldown = new IntegerOptionItem(id + 2, "Nonplus.BlindCooldown", new(0, 60, 1), 30, tab)
            .SetParent(Options.CustomRoleSpawnChances[role])
            .SetValueFormat(OptionFormat.Seconds);

        BlindDuration = new IntegerOptionItem(id + 3, "Nonplus.BlindDuration", new(0, 60, 1), 10, tab)
            .SetParent(Options.CustomRoleSpawnChances[role])
            .SetValueFormat(OptionFormat.Seconds);

        UseLimit = new FloatOptionItem(id + 4, "AbilityUseLimit", new(0, 20, 0.05f), 0, tab)
            .SetParent(Options.CustomRoleSpawnChances[role])
            .SetValueFormat(OptionFormat.Times);

        NonplusAbilityUseGainWithEachKill = new FloatOptionItem(id + 5, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 1.5f, tab)
            .SetParent(Options.CustomRoleSpawnChances[role])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(UseLimit.GetFloat());
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return true;
    }

    public override void OnPet(PlayerControl pc)
    {
        Blind(pc);
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (Options.UsePets.GetBool()) return;

        Blind(pc);
    }

    private static void Blind(PlayerControl pc)
    {
        if (pc.GetAbilityUseLimit() < 1) return;

        pc.RpcRemoveAbilityUse();

        Main.AllAlivePlayerControls.Without(pc).Do(x =>
        {
            Main.PlayerStates[x.PlayerId].IsBlackOut = true;
            x.MarkDirtySettings();
        });

        LateTask.New(() =>
        {
            Main.AllAlivePlayerControls.Without(pc).Do(x =>
            {
                Main.PlayerStates[x.PlayerId].IsBlackOut = false;
                x.MarkDirtySettings();
            });
        }, BlindDuration.GetInt(), "Nonplus.Blind");
    }
}