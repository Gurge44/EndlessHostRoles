namespace EHR.Roles.AddOns.Common
{
    internal class Magnet : IAddon
    {
        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(14697, CustomRoles.Magnet, canSetNum: true);
        }
    }
}
