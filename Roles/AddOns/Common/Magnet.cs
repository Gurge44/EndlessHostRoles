namespace EHR.AddOns.Common;

internal class Magnet : IAddon
{
    public AddonTypes Type => AddonTypes.Helpful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(19692, CustomRoles.Magnet, canSetNum: true, teamSpawnOptions: true);
    }
}