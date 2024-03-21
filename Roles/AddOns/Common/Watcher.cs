using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Watcher : IAddon
    {
        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(15000, CustomRoles.Watcher, canSetNum: true);
            ImpCanBeWatcher = BooleanOptionItem.Create(15010, "ImpCanBeWatcher", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Watcher]);
            CrewCanBeWatcher = BooleanOptionItem.Create(15011, "CrewCanBeWatcher", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Watcher]);
            NeutralCanBeWatcher = BooleanOptionItem.Create(15012, "NeutralCanBeWatcher", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Watcher]);
        }
    }
}
