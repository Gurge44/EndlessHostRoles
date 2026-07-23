namespace EHR.Roles;

public class Priority : IAddon
{
    public AddonTypes Type => AddonTypes.Helpful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(659500, CustomRoles.Priority, canSetNum: true);
    }
}
