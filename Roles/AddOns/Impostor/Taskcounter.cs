namespace EHR.Roles.AddOns.Impostor
{
    internal class Taskcounter : IAddon
    {
        public AddonTypes Type => AddonTypes.ImpOnly;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(14370, CustomRoles.Taskcounter, canSetNum: true);
        }
    }
}