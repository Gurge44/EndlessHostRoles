namespace EHR.AddOns.Common
{
    public class Trainee : IAddon
    {
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(647843, CustomRoles.Trainee, canSetNum: true, teamSpawnOptions: true);
        }
    }
}
