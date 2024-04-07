using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Gravestone : IAddon
    {
        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(14000, CustomRoles.Gravestone, canSetNum: true);
            ImpCanBeGravestone = BooleanOptionItem.Create(14010, "ImpCanBeGravestone", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Gravestone]);
            CrewCanBeGravestone = BooleanOptionItem.Create(14011, "CrewCanBeGravestone", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Gravestone]);
            NeutralCanBeGravestone = BooleanOptionItem.Create(14012, "NeutralCanBeGravestone", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Gravestone]);
        }
    }
}
