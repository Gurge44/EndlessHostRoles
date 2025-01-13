using static EHR.Options;

namespace EHR.AddOns.Common;

internal class Bait : IAddon
{
    public AddonTypes Type => AddonTypes.Mixed;

    public void SetupCustomOption()
    {
        SetupAdtRoleOptions(13700, CustomRoles.Bait, canSetNum: true, teamSpawnOptions: true);

        BaitDelayMin = new FloatOptionItem(13713, "BaitDelayMin", new(0f, 90f, 1f), 0f, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bait])
            .SetValueFormat(OptionFormat.Seconds);

        BaitDelayMax = new FloatOptionItem(13714, "BaitDelayMax", new(0f, 90f, 1f), 0f, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bait])
            .SetValueFormat(OptionFormat.Seconds);

        BaitDelayNotify = new BooleanOptionItem(13715, "BaitDelayNotify", true, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bait]);

        BaitNotification = new BooleanOptionItem(13716, "BaitNotification", false, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bait]);

        ReportBaitAtAllCost = new BooleanOptionItem(13717, "ReportBaitAtAllCost", false, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bait]);
    }
}