﻿using EHR.Modules;
using System.Linq;
using static EHR.Options;

namespace EHR.Roles.Crewmate
{
    internal class Speedrunner : RoleBase
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

        public static void SetupCustomOption()
        {
            SetupRoleOptions(9170, TabGroup.CrewmateRoles, CustomRoles.Speedrunner);
            SpeedrunnerNotifyKillers = BooleanOptionItem.Create(9178, "SpeedrunnerNotifyKillers", true, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Speedrunner]);
            SpeedrunnerNotifyAtXTasksLeft = IntegerOptionItem.Create(9179, "SpeedrunnerNotifyAtXTasksLeft", new(0, 90, 1), 3, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Speedrunner]);
            SpeedrunnerTasks = OverrideTasksData.Create(9180, TabGroup.CrewmateRoles, CustomRoles.Speedrunner);
        }

        public override void OnTaskComplete(PlayerControl player, int CompletedTasksCount, int AllTasksCount)
        {
            var completedTasks = CompletedTasksCount + 1;
            int remainingTasks = AllTasksCount - completedTasks;
            if (completedTasks >= AllTasksCount)
            {
                Logger.Info("Speedrunner finished tasks", "Speedrunner");
                player.RPCPlayCustomSound("Congrats");
                GameData.Instance.CompletedTasks = GameData.Instance.TotalTasks;
            }
            else if (completedTasks >= SpeedrunnerNotifyAtXTasksLeft.GetInt() && SpeedrunnerNotifyKillers.GetBool())
            {
                string speedrunnerName = player.GetRealName().RemoveHtmlTags();
                string notifyString = Translator.GetString("SpeedrunnerHasXTasksLeft");
                foreach (var pc in Main.AllAlivePlayerControls.Where(pc => !pc.Is(Team.Crewmate)).ToArray())
                {
                    pc.Notify(string.Format(notifyString, speedrunnerName, remainingTasks));
                }
            }
        }
    }
}