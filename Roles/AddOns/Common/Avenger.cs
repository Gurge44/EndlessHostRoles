namespace EHR.AddOns.Common;

internal class Avenger : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(15100, CustomRoles.Avenger, canSetNum: true, teamSpawnOptions: true);
    }
}