namespace EHR.AddOns.Common
{
    public class AntiTP : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(16893, CustomRoles.AntiTP, canSetNum: true, teamSpawnOptions: true);
        }
    }
}