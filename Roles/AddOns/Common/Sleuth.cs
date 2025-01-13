namespace EHR.AddOns.Common;

internal class Sleuth : IAddon
{
    public AddonTypes Type => AddonTypes.Helpful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(15150, CustomRoles.Sleuth, canSetNum: true, teamSpawnOptions: true);
    }
}