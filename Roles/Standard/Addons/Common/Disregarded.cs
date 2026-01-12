using static EHR.Options;

namespace EHR.Roles;

internal class Disregarded : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        SetupAdtRoleOptions(15300, CustomRoles.Disregarded, canSetNum: true, teamSpawnOptions: true);
    }
}