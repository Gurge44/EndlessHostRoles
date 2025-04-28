namespace EHR.AddOns.Common;

public class Blind : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(651600, CustomRoles.Blind, canSetNum: true, teamSpawnOptions: true);
    }
}