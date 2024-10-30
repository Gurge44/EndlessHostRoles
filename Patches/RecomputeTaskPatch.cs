using HarmonyLib;

namespace EHR
{
    [HarmonyPatch(typeof(GameData), nameof(GameData.RecomputeTaskCounts))]
    internal class CustomTaskCountsPatch
    {
        public static bool Prefix(GameData __instance)
        {
            __instance.TotalTasks = 0;
            __instance.CompletedTasks = 0;

            foreach (NetworkedPlayerInfo p in __instance.AllPlayers)
            {
                if (p == null)
                {
                    continue;
                }

                bool hasTasks = Utils.HasTasks(p) && Main.PlayerStates[p.PlayerId].TaskState.AllTasksCount > 0;
                if (hasTasks)
                {
                    foreach (NetworkedPlayerInfo.TaskInfo task in p.Tasks)
                    {
                        __instance.TotalTasks++;
                        if (task.Complete)
                        {
                            __instance.CompletedTasks++;
                        }
                    }
                }
            }

            return false;
        }
    }
}