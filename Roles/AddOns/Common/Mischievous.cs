namespace EHR.Roles.AddOns.Common
{
    internal class Mischievous : IAddon
    {
        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(15160, CustomRoles.Mischievous, canSetNum: true);
        }
    }
}
