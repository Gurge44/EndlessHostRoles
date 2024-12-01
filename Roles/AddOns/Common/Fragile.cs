namespace EHR.AddOns.Common;

public class Fragile : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(645333, CustomRoles.Fragile, canSetNum: true, teamSpawnOptions: true);
    }
}