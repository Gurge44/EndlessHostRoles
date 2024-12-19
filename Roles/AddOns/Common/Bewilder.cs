using static EHR.Options;

namespace EHR.AddOns.Common;

internal class Bewilder : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        SetupAdtRoleOptions(15200, CustomRoles.Bewilder, canSetNum: true, teamSpawnOptions: true);

        BewilderVision = new FloatOptionItem(15210, "BewilderVision", new(0f, 5f, 0.05f), 0.6f, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bewilder])
            .SetValueFormat(OptionFormat.Multiplier);
    }
}