namespace EHR.Roles.AddOns.Common
{
    internal class Haste : IAddon
    {
        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(14550, CustomRoles.Haste, canSetNum: true, teamSpawnOptions: true);
        }
    }
}