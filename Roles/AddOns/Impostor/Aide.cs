namespace EHR.AddOns.Impostor;

public class Aide : IAddon
{
    public AddonTypes Type => AddonTypes.ImpOnly;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(646000, CustomRoles.Aide, canSetNum: true);
    }
}