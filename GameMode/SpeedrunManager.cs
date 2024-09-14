using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace EHR
{
    public class SpeedrunManager
    {
        private static OptionItem TaskFinishWins;
        private static OptionItem TimeStacksUp;
        private static OptionItem TimeLimit;

        public static HashSet<byte> CanKill = [];

        private static Dictionary<byte, int> Timers = [];

        public static void SetupCustomOption()
        {
            const int id = 69_214_001;
            Color color = Utils.GetRoleColor(CustomRoles.Speedrunner);

            TaskFinishWins = new BooleanOptionItem(id, "Speedrun_TaskFinishWins", false, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.Speedrun)
                .SetColor(color);

            TimeStacksUp = new BooleanOptionItem(id + 1, "Speedrun_TimeStacksUp", true, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.Speedrun)
                .SetColor(color);

            TimeLimit = new IntegerOptionItem(id + 2, "Speedrun_TimeLimit", new(1, 90, 1), 30, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.Speedrun)
                .SetValueFormat(OptionFormat.Seconds)
                .SetColor(color);
        }

        public static void Init()
        {
            CanKill = [];
            Timers = Main.AllAlivePlayerControls.ToDictionary(x => x.PlayerId, _ => TimeLimit.GetInt());
        }

        public static void ResetTimer(PlayerControl pc)
        {
            if (TimeStacksUp.GetBool()) Timers[pc.PlayerId] += TimeLimit.GetInt();
            else Timers[pc.PlayerId] = TimeLimit.GetInt();
            Logger.Info($" Timer for {pc.GetRealName()} set to {Timers[pc.PlayerId]}", "Speedrun");
        }

        public static void OnTaskFinish(PlayerControl pc)
        {
            if (TaskFinishWins.GetBool()) return;

            CanKill.Add(pc.PlayerId);
            pc.RpcChangeRoleBasis(CustomRoles.Runner);
            pc.Notify(Translator.GetString("Speedrun_CompletedTasks"));
        }

        public static string GetTaskBarText()
        {
            return string.Join('\n', Main.PlayerStates
                .Join(Main.AllAlivePlayerControls, x => x.Key, x => x.PlayerId, (kvp, pc) => (
                    Name: Utils.ColorString(Main.PlayerColors.GetValueOrDefault(kvp.Key, Color.white), pc.GetRealName()),
                    CompletedTasks: kvp.Value.TaskState.CompletedTasksCount,
                    AllTasks: kvp.Value.TaskState.AllTasksCount))
                .Select(x => $"{x.Name}: {x.CompletedTasks}/{x.AllTasks}"));
        }

        public static string GetSuffixText(PlayerControl pc)
        {
            int time = Timers[pc.PlayerId];
            int alive = Main.AllAlivePlayerControls.Length;
            int apc = Main.AllPlayerControls.Length;
            int killers = CanKill.Count;

            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (CanKill.Contains(pc.PlayerId)) return string.Format(Translator.GetString("Speedrun_CanKillSuffixInfo"), alive, apc, killers - 1, time);
            return string.Format(Translator.GetString("Speedrun_DoTasksSuffixInfo"), pc.GetTaskState().RemainingTasksCount, alive, apc, killers, time);
        }

        public static bool CheckForGameEnd(out GameOverReason reason)
        {
            PlayerControl[] aapc = Main.AllAlivePlayerControls;
            
            if (TaskFinishWins.GetBool())
            {
                var player = aapc.FirstOrDefault(x => x.GetTaskState().IsTaskFinished);
                if (player != null)
                {
                    CustomWinnerHolder.WinnerIds = [player.PlayerId];
                    reason = GameOverReason.HumansByTask;
                    return true;
                }
            }
            
            switch (aapc.Length)
            {
                case 1:
                    CustomWinnerHolder.WinnerIds = [aapc[0].PlayerId];
                    reason = GameOverReason.ImpostorByKill;
                    return true;
                case 0:
                    CustomWinnerHolder.WinnerIds = [];
                    reason = GameOverReason.HumansDisconnect;
                    return true;
            }

            reason = GameOverReason.ImpostorByKill;
            var keys = new[] { KeyCode.LeftShift, KeyCode.L, KeyCode.Return };
            return keys.Any(Input.GetKeyDown) && keys.All(Input.GetKey);
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        class FixedUpdatePatch
        {
            private static long LastUpdate;

            public static void Postfix(PlayerControl __instance)
            {
                if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || Options.CurrentGameMode != CustomGameMode.Speedrun || Main.HasJustStarted) return;

                if (__instance.IsAlive() && Timers[__instance.PlayerId] <= 0) __instance.Suicide();

                long now = Utils.TimeStamp;
                if (LastUpdate == now) return;
                LastUpdate = now;

                //Timers.Keys.ToArray().Do(x => Timers[x]--);
                Timers.AdjustAllValues(x => x - 1);
                Utils.NotifyRoles();
            }
        }
    }
}