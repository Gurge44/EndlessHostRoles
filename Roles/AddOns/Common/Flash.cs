namespace EHR.AddOns.Common;

internal class Flashman : IAddon
{
    public AddonTypes Type => AddonTypes.Helpful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(18700, CustomRoles.Flashman, canSetNum: true, tab: TabGroup.Addons, teamSpawnOptions: true);

        Options.FlashmanSpeed = new FloatOptionItem(18707, "FlashmanSpeed", new(0.25f, 3f, 0.05f), 2.5f, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Flashman])
            .SetValueFormat(OptionFormat.Multiplier);
    }
}