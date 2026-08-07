namespace EHR.Roles;

public class Priority : IAddon
{
    public AddonTypes Type => AddonTypes.Helpful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(659550, CustomRoles.Priority, canSetNum: true);
    }
}
