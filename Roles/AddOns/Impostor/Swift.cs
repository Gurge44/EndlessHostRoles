namespace EHR.Roles.AddOns.Impostor
{
    internal class Swift : IAddon
    {
        public AddonTypes Type => AddonTypes.ImpOnly;

        public void SetupCustomOption() => Options.SetupAdtRoleOptions(16050, CustomRoles.Swift, canSetNum: true, tab: TabGroup.Addons);
    }
}
