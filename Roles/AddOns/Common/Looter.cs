namespace EHR.AddOns.Common;

public class Looter : IAddon
{
    public AddonTypes Type => AddonTypes.Mixed;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(656500, CustomRoles.Looter, canSetNum: true, teamSpawnOptions: true);
    }
}