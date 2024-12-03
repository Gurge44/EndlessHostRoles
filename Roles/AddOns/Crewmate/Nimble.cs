using static EHR.Options;

namespace EHR.AddOns.Crewmate;

internal class Nimble : IAddon
{
    public static OptionItem NimbleCD;
    public static OptionItem NimbleInVentTime;
    public AddonTypes Type => AddonTypes.Helpful;

    public void SetupCustomOption()
    {
        SetupAdtRoleOptions(15640, CustomRoles.Nimble, canSetNum: true);

        NimbleCD = new FloatOptionItem(15647, "VentCooldown", new(0, 180, 1), 30, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Nimble])
            .SetValueFormat(OptionFormat.Seconds);

        NimbleInVentTime = new FloatOptionItem(15646, "MaxInVentTime", new(0, 180, 1), 15, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Nimble])
            .SetValueFormat(OptionFormat.Seconds);
    }
}