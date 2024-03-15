using static TOHE.Options;

namespace TOHE.Roles.AddOns.Common
{
    internal class Rascal : IAddon
    {
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(15600, CustomRoles.Rascal, canSetNum: true, tab: TabGroup.Addons);
            RascalAppearAsMadmate = BooleanOptionItem.Create(15610, "RascalAppearAsMadmate", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Rascal]);
        }
    }
}
