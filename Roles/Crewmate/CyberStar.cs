using static EHR.Options;

namespace EHR.Crewmate;

internal class CyberStar : RoleBase
{
    public override bool IsEnable => false;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(5300, TabGroup.CrewmateRoles, CustomRoles.CyberStar);

        ImpKnowCyberStarDead = new BooleanOptionItem(5303, "ImpKnowCyberStarDead", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.CyberStar]);

        NeutralKnowCyberStarDead = new BooleanOptionItem(5304, "NeutralKnowCyberStarDead", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.CyberStar]);
            
        CovenKnowCyberStarDead = new BooleanOptionItem(5305, "CovenKnowCyberStarDead", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.CyberStar]);
    }

    public override void Init() { }

    public override void Add(byte playerId) { }
}
