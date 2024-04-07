using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Seer : IAddon
    {
        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(14800, CustomRoles.Seer, canSetNum: true);
            ImpCanBeSeer = BooleanOptionItem.Create(14810, "ImpCanBeSeer", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Seer]);
            CrewCanBeSeer = BooleanOptionItem.Create(14811, "CrewCanBeSeer", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Seer]);
            NeutralCanBeSeer = BooleanOptionItem.Create(14812, "NeutralCanBeSeer", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Seer]);
        }
    }
}
