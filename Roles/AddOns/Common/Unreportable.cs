using static EHR.Options;

namespace EHR.AddOns.Common;

internal class Unreportable : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        SetupAdtRoleOptions(15300, CustomRoles.Unreportable, canSetNum: true, teamSpawnOptions: true);
    }
}