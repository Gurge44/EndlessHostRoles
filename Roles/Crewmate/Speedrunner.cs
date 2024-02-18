using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TOHE.Modules;

namespace TOHE.Roles.Crewmate
{
    internal static class Speedrunner
    {
        public static void OnTaskComplete(PlayerControl player, int CompletedTasksCount, int AllTasksCount)
        {
            var completedTasks = CompletedTasksCount + 1;
            int remainingTasks = AllTasksCount - completedTasks;
            if (completedTasks >= AllTasksCount)
            {
                Logger.Info("Speedrunner finished tasks", "Speedrunner");
                player.RPCPlayCustomSound("Congrats");
                GameData.Instance.CompletedTasks = GameData.Instance.TotalTasks;
            }
            else if (completedTasks >= Options.SpeedrunnerNotifyAtXTasksLeft.GetInt() && Options.SpeedrunnerNotifyKillers.GetBool())
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
