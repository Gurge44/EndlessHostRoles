namespace EHR.AddOns.Impostor;

public class Venom : IAddon
{
    public AddonTypes Type => AddonTypes.Helpful;

    public static OptionItem VenomDissolveTime;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(654500, CustomRoles.Venom, canSetNum: true);
        
        VenomDissolveTime = new FloatOptionItem(654510, "ViperDissolveTime", new(0f, 60f, 0.5f), 10f, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Venom])
            .SetValueFormat(OptionFormat.Seconds);
    }
}