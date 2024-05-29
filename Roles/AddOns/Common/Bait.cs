using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Bait : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(13700, CustomRoles.Bait, canSetNum: true, teamSpawnOptions: true);
            BaitDelayMin = FloatOptionItem.Create(13713, "BaitDelayMin", new(0f, 90f, 1f), 0f, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bait])
                .SetValueFormat(OptionFormat.Seconds);
            BaitDelayMax = FloatOptionItem.Create(13714, "BaitDelayMax", new(0f, 90f, 1f), 0f, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bait])
                .SetValueFormat(OptionFormat.Seconds);
            BaitDelayNotify = BooleanOptionItem.Create(13715, "BaitDelayNotify", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bait]);
            BaitNotification = BooleanOptionItem.Create(13716, "BaitNotification", false, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bait]);
            ReportBaitAtAllCost = BooleanOptionItem.Create(13717, "ReportBaitAtAllCost", false, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bait]);
        }
    }
}