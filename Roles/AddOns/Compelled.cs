namespace EHR.AddOns;

public class Compelled : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(654980, CustomRoles.Compelled, canSetNum: true, teamSpawnOptions: true);
    }
}