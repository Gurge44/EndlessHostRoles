namespace TOHE.Roles.AddOns.Common
{
    internal class Giant : IAddon
    {
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(18750, CustomRoles.Giant, canSetNum: true, tab: TabGroup.Addons);
            Options.GiantSpeed = FloatOptionItem.Create(18753, "GiantSpeed", new(0.25f, 3f, 0.25f), 0.75f, TabGroup.Addons, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Giant])
                .SetValueFormat(OptionFormat.Multiplier);
        }
    }
}
