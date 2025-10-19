using System.Collections.Generic;

namespace EHR.AddOns.GhostRoles;

public class Shade : IGhostRole
{
    private static OptionItem CD;
    
    public HashSet<byte> Protected = [];
    
    public Team Team => Team.Neutral;
    public int Cooldown => CD.GetInt();
    
    public void OnProtect(PlayerControl pc, PlayerControl target)
    {
        Protected.Add(target.PlayerId);
    }
    
    public void OnAssign(PlayerControl pc)
    {
        Protected = [];
    }
    
    public void SetupCustomOption()
    {
        Options.SetupRoleOptions(649900, TabGroup.OtherRoles, CustomRoles.Bloodmoon);

        CD = new IntegerOptionItem(649902, "AbilityCooldown", new(0, 120, 1), 10, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Bloodmoon])
            .SetValueFormat(OptionFormat.Seconds);
    }
}