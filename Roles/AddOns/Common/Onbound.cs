namespace EHR.Roles.AddOns.Common
{
    public class Onbound : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(14500, CustomRoles.Onbound, canSetNum: true, teamSpawnOptions: true);
        }
    }
}