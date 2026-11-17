using AmongUs.GameOptions;

namespace EHR.Roles;

public class Influencer : IGhostRole
{
    private static OptionItem CD;
    
    public Team Team => Team.Crewmate;
    public RoleTypes RoleTypes => RoleTypes.SpiritGuide;
    public int Cooldown => CD.GetInt();

    public void SetupCustomOption()
    {
        Options.SetupRoleOptions(647000, TabGroup.OtherRoles, CustomRoles.Influencer);

        CD = new IntegerOptionItem(647002, "SpiritGuideCooldown", new(0, 120, 1), 30, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Influencer])
            .SetValueFormat(OptionFormat.Seconds);
    }
    
    public void OnProtect(PlayerControl pc, PlayerControl target)
    {
        
    }
    
    public void OnAssign(PlayerControl pc)
    {
        
    }
}