using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Loyal : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(15500, CustomRoles.Loyal, canSetNum: true);
            ImpCanBeLoyal = BooleanOptionItem.Create(15510, "ImpCanBeLoyal", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Loyal]);
            CrewCanBeLoyal = BooleanOptionItem.Create(15511, "CrewCanBeLoyal", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Loyal]);
        }
    }
}
