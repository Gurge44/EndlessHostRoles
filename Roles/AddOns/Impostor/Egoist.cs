namespace EHR.AddOns.Impostor
{
    internal class Egoist : IAddon
    {
        public AddonTypes Type => AddonTypes.ImpOnly;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(18900, CustomRoles.Egoist, canSetNum: true, tab: TabGroup.Addons);

            Options.ImpEgoistVisibalToAllies = new BooleanOptionItem(18912, "ImpEgoistVisibalToAllies", true, TabGroup.Addons)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Egoist]);
        }
    }
}