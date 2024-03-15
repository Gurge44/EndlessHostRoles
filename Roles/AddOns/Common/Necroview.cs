using static TOHE.Options;

namespace TOHE.Roles.AddOns.Common
{
    internal class Necroview : IAddon
    {
        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(14400, CustomRoles.Necroview, canSetNum: true, tab: TabGroup.Addons);
            ImpCanBeNecroview = BooleanOptionItem.Create(14410, "ImpCanBeNecroview", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Necroview]);
            CrewCanBeNecroview = BooleanOptionItem.Create(14411, "CrewCanBeNecroview", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Necroview]);
            NeutralCanBeNecroview = BooleanOptionItem.Create(14412, "NeutralCanBeNecroview", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Necroview]);
        }
    }
}
