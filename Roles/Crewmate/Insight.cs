using System.Collections.Generic;
using System.Linq;

namespace EHR.Crewmate
{
    internal class Insight : RoleBase
    {
        public static bool On;
        private byte InsightId;
        private List<byte> KnownRolesOfPlayerIds = [];
        private List<CustomRoles> RolesKnownThisRound = [];
        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(5650, TabGroup.CrewmateRoles, CustomRoles.Insight);
            Options.OverrideTasksData.Create(5653, TabGroup.CrewmateRoles, CustomRoles.Insight);
        }

        public override void Add(byte playerId)
        {
            On = true;
            InsightId = playerId;
            KnownRolesOfPlayerIds = [];
            RolesKnownThisRound = [];
        }

        public override void Init()
        {
            On = false;
        }

        public override void OnTaskComplete(PlayerControl player, int completedTaskCount, int totalTaskCount)
        {
            var list = Main.AllPlayerControls.Where(x => !KnownRolesOfPlayerIds.Contains(x.PlayerId) && !x.Is(CustomRoles.Insight) && !x.Is(CustomRoles.GM) && !x.Is(CustomRoles.NotAssigned)).ToList();
            if (list.Count != 0)
            {
                var target = list.RandomElement();
                KnownRolesOfPlayerIds.Add(target.PlayerId);
                var role = target.GetCustomRole();
                player.Notify(string.Format(Translator.GetString("InsightNotify"), role.ToColoredString()));
                RolesKnownThisRound.Add(role);
            }
        }

        public override void OnReportDeadBody()
        {
            LateTask.New(() =>
            {
                RolesKnownThisRound.Do(x => Utils.SendMessage("\n", InsightId, string.Format(Translator.GetString("InsightNotify"), x.ToColoredString())));
                RolesKnownThisRound = [];
            }, 10f, log: false);
        }
    }
}