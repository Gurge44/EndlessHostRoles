using System.Collections.Generic;

namespace EHR.Roles;

public class Onbound : IAddon
{
    public static OptionItem GuesserSuicides;
    public static OptionItem MaxAttemptsBlocked;
    
    public AddonTypes Type => AddonTypes.Mixed;

    public static Dictionary<byte, HashSet<byte>> NumBlocked = [];

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(14500, CustomRoles.Onbound, canSetNum: true, teamSpawnOptions: true);

        GuesserSuicides = new BooleanOptionItem(14508, "GuesserSuicides", false, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Onbound])
            .SetGameMode(CustomGameMode.Standard);
        
        MaxAttemptsBlocked = new IntegerOptionItem(14509, "MaxAttemptsBlocked", new(1, 100, 1), 1, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Onbound])
            .SetGameMode(CustomGameMode.Standard);
    }
}