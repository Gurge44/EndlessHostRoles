using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TOHE.Roles.Neutral
{
    internal class Opportunist : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

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
            return !Options.OppoImmuneToAttacksWhenTasksDone.GetBool() || !target.Is(CustomRoles.Opportunist) || !target.AllTasksCompleted();
        }
    }
}
