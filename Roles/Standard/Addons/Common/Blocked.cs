namespace EHR.Roles;

public class Blocked : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(646100, CustomRoles.Blocked, canSetNum: true, teamSpawnOptions: true);
    }
}