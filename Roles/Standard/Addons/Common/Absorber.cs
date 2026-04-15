namespace EHR.Roles;

public class Absorber : IAddon
{
    public AddonTypes Type => AddonTypes.Helpful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(645670, CustomRoles.Absorber, canSetNum: true, teamSpawnOptions: true);
    }
}
