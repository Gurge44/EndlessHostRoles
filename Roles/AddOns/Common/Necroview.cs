using static EHR.Options;

namespace EHR.AddOns.Common;

internal class Necroview : IAddon
{
    public AddonTypes Type => AddonTypes.Helpful;

    public void SetupCustomOption()
    {
        SetupAdtRoleOptions(14400, CustomRoles.Necroview, canSetNum: true, teamSpawnOptions: true);
    }
}