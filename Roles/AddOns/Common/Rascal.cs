using static EHR.Options;

namespace EHR.AddOns.Common;

internal class Rascal : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        SetupAdtRoleOptions(15600, CustomRoles.Rascal, canSetNum: true, tab: TabGroup.Addons, teamSpawnOptions: true);

        RascalAppearAsMadmate = new BooleanOptionItem(15610, "RascalAppearAsMadmate", true, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Rascal]);
    }
}