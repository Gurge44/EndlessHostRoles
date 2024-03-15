using static TOHE.Options;

namespace TOHE.Roles.AddOns.Common
{
    internal class Autopsy : IAddon
    {
        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(13600, CustomRoles.Autopsy, canSetNum: true);
            ImpCanBeAutopsy = BooleanOptionItem.Create(13610, "ImpCanBeAutopsy", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Autopsy]);
            CrewCanBeAutopsy = BooleanOptionItem.Create(13611, "CrewCanBeAutopsy", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Autopsy]);
            NeutralCanBeAutopsy = BooleanOptionItem.Create(13612, "NeutralCanBeAutopsy", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Autopsy]);
        }
    }
}