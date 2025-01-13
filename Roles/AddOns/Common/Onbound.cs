namespace EHR.AddOns.Common;

public class Onbound : IAddon
{
    public static OptionItem GuesserSuicides;
    public AddonTypes Type => AddonTypes.Mixed;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(14500, CustomRoles.Onbound, canSetNum: true, teamSpawnOptions: true);

        GuesserSuicides = new BooleanOptionItem(14508, "GuesserSuicides", true, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Onbound])
            .SetGameMode(CustomGameMode.Standard);
    }
}