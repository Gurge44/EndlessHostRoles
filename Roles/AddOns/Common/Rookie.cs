namespace EHR.AddOns.Common
{
    public class Rookie : IAddon
    {
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption() => Options.SetupAdtRoleOptions(649594, CustomRoles.Rookie, canSetNum: true, teamSpawnOptions: true);
    }
}