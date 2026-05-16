namespace EHR.Roles;

public class Venom : IAddon
{
    public AddonTypes Type => AddonTypes.ImpOnly;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(654500, CustomRoles.Venom, canSetNum: true);
    }
}