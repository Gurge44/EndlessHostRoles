namespace EHR.AddOns.Common;

public class Hidden : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(656600, CustomRoles.Hidden, canSetNum: true, teamSpawnOptions: true);
    }
}