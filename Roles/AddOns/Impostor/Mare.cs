using static EHR.Options;

namespace EHR.AddOns.Impostor
{
    internal class Mare : IAddon
    {
        public AddonTypes Type => AddonTypes.ImpOnly;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(1600, CustomRoles.Mare, canSetNum: true, tab: TabGroup.Addons);
            MareKillCD = new FloatOptionItem(1609, "KillCooldown", new(0f, 60f, 1f), 15f, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mare])
                .SetValueFormat(OptionFormat.Seconds);
            MareKillCDNormally = new FloatOptionItem(1606, "KillCooldownNormally", new(0f, 90f, 1f), 40f, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mare])
                .SetValueFormat(OptionFormat.Seconds);
            MareHasIncreasedSpeed = new BooleanOptionItem(1607, "MareHasIncreasedSpeed", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mare]);
            MareSpeedDuringLightsOut = new FloatOptionItem(1608, "MareSpeedDuringLightsOut", new(0.5f, 3f, 0.05f), 1.75f, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mare])
                .SetValueFormat(OptionFormat.Multiplier);
        }
    }
}