namespace EHR.Roles;

public class Bluetooth : IAddon
{
    public AddonTypes Type => AddonTypes.ImpOnly;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(659500, CustomRoles.Bluetooth, canSetNum: true);
    }
}