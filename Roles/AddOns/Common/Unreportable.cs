using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Unreportable : IAddon
    {
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(15300, CustomRoles.Unreportable, canSetNum: true);
            ImpCanBeUnreportable = BooleanOptionItem.Create(15310, "ImpCanBeUnreportable", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Unreportable]);
            CrewCanBeUnreportable = BooleanOptionItem.Create(15311, "CrewCanBeUnreportable", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Unreportable]);
            NeutralCanBeUnreportable = BooleanOptionItem.Create(15312, "NeutralCanBeUnreportable", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Unreportable]);
        }
    }
}
