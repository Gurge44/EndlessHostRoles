namespace EHR.Roles.AddOns.Common
{
    internal class Flash : IAddon
    {
        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(18700, CustomRoles.Flashman, canSetNum: true, tab: TabGroup.Addons);
            Options.FlashmanSpeed = FloatOptionItem.Create(18703, "FlashmanSpeed", new(0.25f, 3f, 0.25f), 2.5f, TabGroup.Addons, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Flashman])
                .SetValueFormat(OptionFormat.Multiplier);
        }
    }
}
