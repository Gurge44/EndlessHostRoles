namespace EHR.AddOns.Common;

internal class Giant : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(18750, CustomRoles.Giant, canSetNum: true, tab: TabGroup.Addons, teamSpawnOptions: true);

        Options.GiantSpeed = new FloatOptionItem(18757, "GiantSpeed", new(0.25f, 3f, 0.05f), 0.75f, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Giant])
            .SetValueFormat(OptionFormat.Multiplier);
    }
}