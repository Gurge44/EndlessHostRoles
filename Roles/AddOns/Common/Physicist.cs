﻿using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Physicist : IAddon
    {
        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(15650, CustomRoles.Physicist);
            PhysicistCD = FloatOptionItem.Create(15655, "VitalsCooldown", new(0, 180, 1), 20, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Physicist])
                .SetValueFormat(OptionFormat.Seconds);
            PhysicistViewDuration = FloatOptionItem.Create(15656, "VitalsDuration", new(0, 180, 1), 5, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Physicist])
                .SetValueFormat(OptionFormat.Seconds);
        }
    }
}
