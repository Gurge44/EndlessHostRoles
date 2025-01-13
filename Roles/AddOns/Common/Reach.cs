namespace EHR.AddOns.Common;

internal class Reach : IAddon
{
    public AddonTypes Type => AddonTypes.Helpful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(14600, CustomRoles.Reach, canSetNum: true, teamSpawnOptions: true);
    }
}