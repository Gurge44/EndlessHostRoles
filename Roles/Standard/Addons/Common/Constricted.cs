namespace EHR.Roles;

internal class Constricted : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(656360, CustomRoles.Constricted, canSetNum: true, teamSpawnOptions: true);
    }
}
