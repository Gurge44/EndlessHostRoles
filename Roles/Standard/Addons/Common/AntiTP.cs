namespace EHR.Roles;

public class AntiTP : IAddon
{
    public AddonTypes Type => AddonTypes.Mixed;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(16892, CustomRoles.AntiTP, canSetNum: true, teamSpawnOptions: true);
    }
}