﻿using static EHR.Options;

namespace EHR.AddOns.Crewmate;

internal class Physicist : IAddon
{
    public static OptionItem PhysicistCD;
    public static OptionItem PhysicistViewDuration;
    public AddonTypes Type => AddonTypes.Helpful;

    public void SetupCustomOption()
    {
        SetupAdtRoleOptions(15650, CustomRoles.Physicist, canSetNum: true);

        PhysicistCD = new FloatOptionItem(15657, "VitalsCooldown", new(0, 180, 1), 20, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Physicist])
            .SetValueFormat(OptionFormat.Seconds);

        PhysicistViewDuration = new FloatOptionItem(15656, "VitalsDuration", new(0, 180, 1), 5, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Physicist])
            .SetValueFormat(OptionFormat.Seconds);
    }
}