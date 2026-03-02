namespace EHR.Roles;

public class Fragile : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(645332, CustomRoles.Fragile, canSetNum: true, teamSpawnOptions: true);
    }
}