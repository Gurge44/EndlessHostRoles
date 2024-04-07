using static EHR.Options;

namespace EHR.Roles.AddOns.Crewmate
{
    internal class Lazy : IAddon
    {
        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(14100, CustomRoles.Lazy, canSetNum: true);
            TasklessCrewCanBeLazy = BooleanOptionItem.Create(14110, "TasklessCrewCanBeLazy", false, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Lazy]);
            TaskBasedCrewCanBeLazy = BooleanOptionItem.Create(14120, "TaskBasedCrewCanBeLazy", false, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Lazy]);
        }
    }
}
