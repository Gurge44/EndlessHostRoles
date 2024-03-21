namespace EHR.Roles.AddOns.Impostor
{
    internal class Mimic : IAddon
    {
        public AddonTypes Type => AddonTypes.ImpOnly;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(16000, CustomRoles.Mimic, canSetNum: true, tab: TabGroup.Addons);
            Options.MimicCanSeeDeadRoles = BooleanOptionItem.Create(16010, "MimicCanSeeDeadRoles", true, TabGroup.Addons, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Mimic]);
        }
    }
}
