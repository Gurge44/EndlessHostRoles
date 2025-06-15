using System.Linq;
using static EHR.Options;

namespace EHR.Crewmate;

internal class SuperStar : RoleBase
{
    public static bool On;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(6000, TabGroup.CrewmateRoles, CustomRoles.SuperStar);

        EveryOneKnowSuperStar = new BooleanOptionItem(6010, "EveryOneKnowSuperStar", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.SuperStar]);

        ImpKnowSuperStarDead = new BooleanOptionItem(5303, "ImpKnowSuperStarDead", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.SuperStar]);

        NeutralKnowSuperStarDead = new BooleanOptionItem(5304, "NeutralKnowSuperStarDead", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.SuperStar]);

        CovenKnowSuperStarDead = new BooleanOptionItem(5305, "CovenKnowSuperStarDead", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.SuperStar]);
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void Init()
    {
        On = false;
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        return !Main.AllAlivePlayerControls.Any(x => x.PlayerId != killer.PlayerId && x.PlayerId != target.PlayerId && Vector2.Distance(x.Pos(), target.Pos()) < 2f);
    }

    public override bool KnowRole(PlayerControl seer, PlayerControl target)
    {
        return base.KnowRole(seer, target) || (target.Is(CustomRoles.SuperStar) && EveryOneKnowSuperStar.GetBool());
    }
}