namespace EHR.Roles.AddOns.Common
{
    public class Energetic : IAddon
    {
        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(644594, CustomRoles.Energetic, canSetNum: true, teamSpawnOptions: true);
        }
    }
}