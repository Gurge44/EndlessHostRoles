namespace TOHE.Roles.AddOns.Impostor
{
    internal class Egoist : IAddon
    {
        public AddonTypes Type => AddonTypes.ImpOnly;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(18900, CustomRoles.Egoist, canSetNum: true, tab: TabGroup.Addons);
            Options.ImpEgoistVisibalToAllies = BooleanOptionItem.Create(18912, "ImpEgoistVisibalToAllies", true, TabGroup.Addons, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Egoist]);
        }
    }
}
