using System.Linq;
using static TOHE.Options;

namespace TOHE.Roles.Crewmate
{
    internal class Electric
    {
        private static int Id => 64410;
        private static OptionItem FreezeDuration;
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
            var Random = IRandom.Instance;

            var targetList = Main.AllAlivePlayerControls.Where(x => !x.Is(Team.Crewmate)).ToList();
            if (targetList.Count == 0) return;
            var target = targetList[Random.Next(0, targetList.Count)];

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
