using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using HarmonyLib;

namespace EHR
{
    public static class AllInOneGameMode
    {
        public static HashSet<byte> Taskers = [];

        public static void Init()
        {
            Taskers = [];
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        private static class FixedUpdatePatch
        {
            [SuppressMessage("ReSharper", "UnusedMember.Local")]
            public static void Postfix(PlayerControl __instance)
            {
                if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || GameStates.IsEnded || !CustomGameMode.AllInOne.IsActiveOrIntegrated() || Main.HasJustStarted || __instance.Is(CustomRoles.Killer)) return;

                bool doneWithTasks = Taskers.Contains(__instance.PlayerId) && __instance.GetTaskState().IsTaskFinished;

                if (doneWithTasks)
                {
                    Taskers.Remove(__instance.PlayerId);
                    __instance.RpcChangeRoleBasis(CustomRoles.Killer);
                    __instance.RpcSetCustomRole(CustomRoles.Killer);
                    Logger.Info($"{__instance.GetRealName()} has completed all tasks, changing role to Killer", "AllInOneGameMode");
                    return;
                }

                Vector2 pos = __instance.Pos();

                bool nearTask = __instance.myTasks.ToArray().Where(x => !x.IsComplete).SelectMany(x => x.FindValidConsolesPositions().ToArray()).Any(x => Vector2.Distance(x, pos) <= DisableDevice.UsableDistance);

                switch (nearTask)
                {
                    case true when Taskers.Add(__instance.PlayerId):
                        __instance.RpcChangeRoleBasis(CustomRoles.Tasker);
                        __instance.RpcSetCustomRole(CustomRoles.Tasker);
                        Logger.Info($"{__instance.GetRealName()} is near an incomplete task, changing role to Tasker", "AllInOneGameMode");
                        break;
                    case false when Taskers.Remove(__instance.PlayerId):
                        __instance.RpcChangeRoleBasis(CustomRoles.KB_Normal);
                        __instance.RpcSetCustomRole(CustomRoles.KB_Normal);
                        Logger.Info($"{__instance.GetRealName()} is no longer near an incomplete task, changing role to KB_Normal", "AllInOneGameMode");
                        break;
                }
            }
        }
    }
}