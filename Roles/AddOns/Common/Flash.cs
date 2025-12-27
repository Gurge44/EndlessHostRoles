namespace EHR.AddOns.Common;

internal class Flash : IAddon
{
    public AddonTypes Type => AddonTypes.Helpful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(18700, CustomRoles.Flash, canSetNum: true, tab: TabGroup.Addons, teamSpawnOptions: true);

        Options.FlashSpeed = new FloatOptionItem(18708, "FlashSpeed", new(0.25f, 3f, 0.05f), 2.5f, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Flash])
            .SetValueFormat(OptionFormat.Multiplier);
    }
}
