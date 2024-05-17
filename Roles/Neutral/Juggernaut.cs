﻿using static EHR.Options;

namespace EHR.Roles.Neutral;

public class Juggernaut : ISettingHolder
{
    private const int Id = 12300;

    public static OptionItem DefaultKillCooldown;
    public static OptionItem ReduceKillCooldown;
    public static OptionItem MinKillCooldown;
    public static OptionItem HasImpostorVision;
    public static OptionItem CanVent;

    public void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Juggernaut);
        DefaultKillCooldown = FloatOptionItem.Create(Id + 10, "SansDefaultKillCooldown", new(0f, 180f, 0.5f), 30f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Juggernaut])
            .SetValueFormat(OptionFormat.Seconds);
        ReduceKillCooldown = FloatOptionItem.Create(Id + 11, "SansReduceKillCooldown", new(0f, 30f, 0.5f), 4f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Juggernaut])
            .SetValueFormat(OptionFormat.Seconds);
        MinKillCooldown = FloatOptionItem.Create(Id + 12, "SansMinKillCooldown", new(0f, 30f, 0.5f), 10f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Juggernaut])
            .SetValueFormat(OptionFormat.Seconds);
        HasImpostorVision = BooleanOptionItem.Create(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Juggernaut]);
        CanVent = BooleanOptionItem.Create(Id + 14, "CanVent", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Juggernaut]);
    }
}