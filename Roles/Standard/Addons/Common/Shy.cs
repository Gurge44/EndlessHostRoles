namespace EHR.Roles;

public class Shy : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(645890, CustomRoles.Shy, canSetNum: true, teamSpawnOptions: true);
    }
}