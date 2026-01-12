namespace EHR.Roles;

public class Concealer : IAddon
{
    public AddonTypes Type => AddonTypes.ImpOnly;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(656200, CustomRoles.Concealer, canSetNum: true);
    }
}