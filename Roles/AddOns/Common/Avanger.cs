namespace EHR.Roles.AddOns.Common
{
    internal class Avanger : IAddon
    {
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(15100, CustomRoles.Avanger, canSetNum: true, teamSpawnOptions: true);
        }
    }
}