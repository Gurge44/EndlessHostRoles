namespace EHR.Roles.AddOns.Common
{
    internal class Magnet : IAddon
    {
        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(19694, CustomRoles.Magnet, canSetNum: true, teamSpawnOptions: true);
        }
    }
}