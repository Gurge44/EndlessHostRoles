using System.Collections.Generic;
using System.Linq;

namespace EHR.Roles.Crewmate
{
    internal class Insight : RoleBase
    {
        public List<byte> KnownRolesOfPlayerIds = [];

        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(5650, TabGroup.CrewmateRoles, CustomRoles.Insight);
            Options.InsightTasks = Options.OverrideTasksData.Create(5653, TabGroup.CrewmateRoles, CustomRoles.Insight);
        }

        public override void Add(byte playerId)
        {
            On = true;
            KnownRolesOfPlayerIds = [];
        }

        public override void Init()
        {
            On = false;
        }

        public override void OnTaskComplete(PlayerControl player, int completedTaskCount, int totalTaskCount)
        {
            var list = Main.AllPlayerControls.Where(x => !KnownRolesOfPlayerIds.Contains(x.PlayerId) && !x.Is(CountTypes.OutOfGame) && !x.Is(CustomRoles.Insight) && !x.Is(CustomRoles.GM) && !x.Is(CustomRoles.NotAssigned))?.ToList();
            if (list.Count != 0)
            {
                var target = list[IRandom.Instance.Next(0, list.Count)];
                KnownRolesOfPlayerIds.Add(target.PlayerId);
                player.Notify(string.Format(Utils.ColorString(target.GetRoleColor(), Translator.GetString("InsightNotify")), target.GetDisplayRoleName(pure: true)));
            }
        }
    }
}