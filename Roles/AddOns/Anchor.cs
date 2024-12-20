namespace EHR.AddOns;

public class Anchor : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(649093, CustomRoles.Anchor, canSetNum: true, teamSpawnOptions: true);
    }
}