using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Busy : IAddon
    {
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(15290, CustomRoles.Busy, canSetNum: true, teamSpawnOptions: true);
            BusyLongTasks = new IntegerOptionItem(15296, "BusyLongTasks", new(0, 90, 1), 1, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Busy]);
            BusyShortTasks = new IntegerOptionItem(15297, "BusyShortTasks", new(0, 90, 1), 1, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Busy]);
        }
    }
}