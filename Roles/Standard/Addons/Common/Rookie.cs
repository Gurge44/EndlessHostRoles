namespace EHR.Roles;

public class Rookie : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(649592, CustomRoles.Rookie, canSetNum: true, teamSpawnOptions: true);
    }
}