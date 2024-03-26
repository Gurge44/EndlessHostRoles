using static EHR.Options;

namespace EHR.Roles.AddOns.Impostor
{
    internal class Mare : IAddon
    {
        public AddonTypes Type => AddonTypes.ImpOnly;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(1600, CustomRoles.Mare, canSetNum: true, tab: TabGroup.Addons);
            MareKillCD = FloatOptionItem.Create(1605, "KillCooldown", new(0f, 60f, 1f), 15f, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mare])
                .SetValueFormat(OptionFormat.Seconds);
            MareKillCDNormally = FloatOptionItem.Create(1606, "KillCooldownNormally", new(0f, 90f, 1f), 40f, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mare])
                .SetValueFormat(OptionFormat.Seconds);
            MareHasIncreasedSpeed = BooleanOptionItem.Create(1607, "MareHasIncreasedSpeed", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mare]);
            MareSpeedDuringLightsOut = FloatOptionItem.Create(1608, "MareSpeedDuringLightsOut", new(0.5f, 3f, 0.05f), 1.75f, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mare])
                .SetValueFormat(OptionFormat.Multiplier);
        }
    }
}
