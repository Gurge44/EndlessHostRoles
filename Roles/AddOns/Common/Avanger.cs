namespace TOHE.Roles.AddOns.Common
{
    internal class Avanger : IAddon
    {
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(15100, CustomRoles.Avanger, canSetNum: true);
            Options.ImpCanBeAvanger = BooleanOptionItem.Create(15110, "ImpCanBeAvanger", false, TabGroup.Addons, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Avanger]);
        }
    }
}
