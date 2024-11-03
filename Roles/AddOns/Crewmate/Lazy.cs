using static EHR.Options;

namespace EHR.AddOns.Crewmate
{
    internal class Lazy : IAddon
    {
        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(14100, CustomRoles.Lazy, canSetNum: true);

            TasklessCrewCanBeLazy = new BooleanOptionItem(14110, "TasklessCrewCanBeLazy", false, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Lazy]);

            TaskBasedCrewCanBeLazy = new BooleanOptionItem(14120, "TaskBasedCrewCanBeLazy", false, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Lazy]);
        }
    }
}