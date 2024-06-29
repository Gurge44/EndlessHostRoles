using System.Linq;
using static EHR.Options;

namespace EHR.Crewmate
{
    internal class Electric : RoleBase
    {
        public static bool On;
        private static OptionItem FreezeDuration;
        public override bool IsEnable => On;

        private static int Id => 64410;

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Electric);
            FreezeDuration = new FloatOptionItem(Id + 2, "GamblerFreezeDur", new(0.5f, 90f, 0.5f), 3f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Electric])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
        {
            if (pc == null) return;

            var targetList = Main.AllAlivePlayerControls.Where(x => !x.Is(Team.Crewmate)).ToList();
            if (targetList.Count == 0) return;
            var target = targetList.RandomElement();

            var beforeSpeed = Main.AllPlayerSpeed[target.PlayerId];
            Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
            target.MarkDirtySettings();
            LateTask.New(() =>
            {
                Main.AllPlayerSpeed[target.PlayerId] = beforeSpeed;
                target.MarkDirtySettings();
            }, FreezeDuration.GetFloat(), "Electric Freeze Reset");
        }
    }
}