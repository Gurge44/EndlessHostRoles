using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Busy : IAddon
    {
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(15290, CustomRoles.Busy, canSetNum: true);
            BusyLongTasks = IntegerOptionItem.Create(15293, "BusyLongTasks", new(0, 90, 1), 1, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Busy]);
            BusyShortTasks = IntegerOptionItem.Create(15294, "BusyShortTasks", new(0, 90, 1), 1, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Busy]);
        }
    }
}
