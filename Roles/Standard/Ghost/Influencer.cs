using AmongUs.GameOptions;

namespace EHR.Roles;

public class Influencer : IGhostRole
{
    public Team Team => Team.Crewmate;
    public RoleTypes RoleTypes => RoleTypes.SpiritGuide;
    public int Cooldown => CrewmateVanillaRoles.SpiritGuideCooldown.GetInt();

    public void SetupCustomOption()
    {
        Options.SetupRoleOptions(647000, TabGroup.OtherRoles, CustomRoles.Influencer);
    }
    
    public void OnProtect(PlayerControl pc, PlayerControl target)
    {
        
    }
    
    public void OnAssign(PlayerControl pc)
    {
        
    }
}