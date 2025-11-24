using static EHR.Options;

namespace EHR.AddOns.Common;

internal class Disregarded : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        SetupAdtRoleOptions(15300, CustomRoles.Disregarded, canSetNum: true, teamSpawnOptions: true);
    }
}