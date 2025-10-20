namespace EHR.AddOns.Common;

public class Composter : IAddon
{
    public AddonTypes Type => AddonTypes.Helpful;

    public static OptionItem AbilityUseGainMultiplier;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(656000, CustomRoles.Composter, canSetNum: true, teamSpawnOptions: true);

        AbilityUseGainMultiplier = new FloatOptionItem(656010, "Composter.AbilityUseGainMultiplier", new(1.05f, 10f, 0.05f), 1.5f, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Composter])
            .SetValueFormat(OptionFormat.Multiplier);
    }
}