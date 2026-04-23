namespace EHR.Roles;

public class Dizzy : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(658180, CustomRoles.Dizzy, canSetNum: true, teamSpawnOptions: true);
    }
}