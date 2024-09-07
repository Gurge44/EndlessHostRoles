namespace EHR.AddOns.Common
{
    public class Spotter : IAddon
    {
        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(648294, CustomRoles.Spotter, canSetNum: true, teamSpawnOptions: true);
        }
    }
}