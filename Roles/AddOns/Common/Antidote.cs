using static EHR.Options;

namespace EHR.AddOns.Common;

internal class Antidote : IAddon
{
    public AddonTypes Type => AddonTypes.Mixed;

    public void SetupCustomOption()
    {
        const int id = 648500;
        SetupAdtRoleOptions(id, CustomRoles.Antidote, canSetNum: true, teamSpawnOptions: true);

        AntidoteCDOpt = new FloatOptionItem(id + 8, "AntidoteCDOpt", new(0f, 180f, 1f), 5f, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Antidote])
            .SetValueFormat(OptionFormat.Seconds);

        AntidoteCDReset = new BooleanOptionItem(id + 9, "AntidoteCDReset", true, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Antidote]);
    }
}