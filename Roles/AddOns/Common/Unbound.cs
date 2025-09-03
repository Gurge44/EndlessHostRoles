namespace EHR.AddOns.Common;

public class Unbound : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(653900, CustomRoles.Unbound, canSetNum: true, teamSpawnOptions: true);
    }
}