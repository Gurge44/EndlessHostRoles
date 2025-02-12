namespace EHR.AddOns.Common;

public class Blocked : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(646100, CustomRoles.Blocked, canSetNum: true, teamSpawnOptions: true);
    }
}