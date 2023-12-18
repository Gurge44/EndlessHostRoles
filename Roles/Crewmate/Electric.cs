using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Crewmate
{
    internal class Electric
    {
        private static int Id => 64410;
        private static OptionItem FreezeDuration;
        private static readonly IRandom Random = IRandom.Instance;
        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Electric);
            FreezeDuration = FloatOptionItem.Create(Id + 2, "GamblerFreezeDur", new(0.5f, 90f, 0.5f), 3f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Electric])
                .SetValueFormat(OptionFormat.Seconds);
        }
        public static void OnTaskComplete(PlayerControl pc)
        {
            if (pc == null) return;
            var target = Main.AllAlivePlayerControls.Where(x => !x.Is(Team.Crewmate)).ToList()[Random.Next(0, Main.AllAlivePlayerControls.Length)];
            var beforeSpeed = Main.AllPlayerSpeed[target.PlayerId];
            Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
            target.MarkDirtySettings();
            _ = new LateTask(() =>
            {
                Main.AllPlayerSpeed[target.PlayerId] = beforeSpeed;
                target.MarkDirtySettings();
            }, FreezeDuration.GetFloat(), "Electric Freeze Reset");
        }
    }
}
