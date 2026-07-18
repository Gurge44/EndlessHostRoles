namespace EHR.Roles;

public class Focused : IAddon
{
    public AddonTypes Type => AddonTypes.Mixed;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(659400, CustomRoles.Focused, canSetNum: true, teamSpawnOptions: true);
    }
}