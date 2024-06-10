using System.Linq;
using UnityEngine;

namespace EHR.Roles.Crewmate
{
    internal class SuperStar : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(6000, TabGroup.CrewmateRoles, CustomRoles.SuperStar);
            Options.EveryOneKnowSuperStar = new BooleanOptionItem(6010, "EveryOneKnowSuperStar", true, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SuperStar]);
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
    }
}