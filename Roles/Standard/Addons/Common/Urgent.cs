namespace EHR.Roles;

public class Urgent : IAddon
{
    public AddonTypes Type => AddonTypes.Mixed;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(658090, CustomRoles.Urgent, canSetNum: true, teamSpawnOptions: true);
    }
}