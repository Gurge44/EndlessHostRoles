namespace EHR.AddOns.Common;

public class Anchor : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(649092, CustomRoles.Anchor, canSetNum: true, teamSpawnOptions: true);
    }
}