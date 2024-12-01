using static EHR.Options;

namespace EHR.AddOns.Common;

internal class Diseased : IAddon
{
    public AddonTypes Type => AddonTypes.Mixed;

    public void SetupCustomOption()
    {
        const int id = 648600;
        SetupAdtRoleOptions(id, CustomRoles.Diseased, canSetNum: true, teamSpawnOptions: true);

        DiseasedCDOpt = new FloatOptionItem(id + 8, "DiseasedCDOpt", new(0f, 180f, 1f), 25f, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Diseased])
            .SetValueFormat(OptionFormat.Seconds);

        DiseasedCDReset = new BooleanOptionItem(id + 7, "DiseasedCDReset", true, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Diseased]);
    }
}