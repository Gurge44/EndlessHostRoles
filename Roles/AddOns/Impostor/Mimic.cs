namespace EHR.AddOns.Impostor
{
    internal class Mimic : IAddon
    {
        public AddonTypes Type => AddonTypes.ImpOnly;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(16000, CustomRoles.Mimic, canSetNum: true, tab: TabGroup.Addons);

            Options.MimicCanSeeDeadRoles = new BooleanOptionItem(16010, "MimicCanSeeDeadRoles", true, TabGroup.Addons)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Mimic]);
        }
    }
}