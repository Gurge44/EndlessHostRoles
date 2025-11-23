using static EHR.Options;

namespace EHR.AddOns.Common;

internal class Tiebreaker : IAddon
{
    public AddonTypes Type => AddonTypes.Mixed;

    public void SetupCustomOption()
    {
        SetupAdtRoleOptions(14900, CustomRoles.Tiebreaker, canSetNum: true, teamSpawnOptions: true);
    }
}