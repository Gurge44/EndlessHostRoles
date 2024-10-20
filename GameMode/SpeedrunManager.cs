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
        private static OptionItem KillCooldown;
        private static OptionItem KillersCanKillTaskingPlayers;

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

            TimeLimit = new IntegerOptionItem(id + 2, "Speedrun_TimeLimit", new(1, 90, 1), 25, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.Speedrun)
                .SetValueFormat(OptionFormat.Seconds)
                .SetColor(color);

            KillCooldown = new IntegerOptionItem(id + 3, "KillCooldown", new(0, 60, 1), 10, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.Speedrun)
                .SetValueFormat(OptionFormat.Seconds)
                .SetColor(color);

            KillersCanKillTaskingPlayers = new BooleanOptionItem(id + 4, "Speedrun_KillersCanKillTaskingPlayers", true, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.Speedrun)
                .SetColor(color);
        }

        public static void Init()
        {
            CanKill = [];
            Timers = Main.AllAlivePlayerControls.ToDictionary(x => x.PlayerId, _ => TimeLimit.GetInt() + 5);
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
            Main.AllPlayerKillCooldown[pc.PlayerId] = KillCooldown.GetInt();
            pc.RpcChangeRoleBasis(CustomRoles.Runner);
            pc.Notify(Translator.GetString("Speedrun_CompletedTasks"));
            pc.SyncSettings();
            pc.SetKillCooldown(KillCooldown.GetInt());
        }

        public static string GetTaskBarText()
        {
            return string.Join('\n', Main.PlayerStates
                .Join(Main.AllAlivePlayerControls, x => x.Key, x => x.PlayerId, (kvp, pc) => (
                    Name: Utils.ColorString(Main.PlayerColors.GetValueOrDefault(kvp.Key, Color.white), pc.GetRealName()),
                    CompletedTasks: kvp.Value.TaskState.CompletedTasksCount,
                    AllTasks: kvp.Value.TaskState.AllTasksCount,
                    Time: Timers.GetValueOrDefault(pc.PlayerId)))
                .OrderByDescending(x => x.CompletedTasks)
                .Select(x => x.CompletedTasks < x.AllTasks ? $"{x.Name}: {x.CompletedTasks}/{x.AllTasks} ({x.Time}s)" : $"{x.Name}: {Translator.GetString("Speedrun_KillingPlayer")} ({x.Time}s)"));
        }

        public static string GetSuffixText(PlayerControl pc)
        {
            if (!pc.IsAlive()) return string.Empty;

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

        public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!CanKill.Contains(killer.PlayerId)) return false;
            return CanKill.Contains(target.PlayerId) || KillersCanKillTaskingPlayers.GetBool();
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        static class FixedUpdatePatch
        {
            private static long LastUpdate;

            public static void Postfix(PlayerControl __instance)
            {
                if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || Options.CurrentGameMode != CustomGameMode.Speedrun || Main.HasJustStarted) return;

                if (__instance.IsAlive() && Timers[__instance.PlayerId] <= 0) __instance.Suicide();

                long now = Utils.TimeStamp;
                if (LastUpdate == now) return;
                LastUpdate = now;

                Timers.AdjustAllValues(x => x - 1);
                Utils.NotifyRoles();

                CanKill.RemoveWhere(x => x.GetPlayer() == null || !x.GetPlayer().IsAlive());
            }
        }
    }

    public class Runner : RoleBase
    {
        public override bool IsEnable => false;

        public override void Init()
        {
        }

        public override void Add(byte playerId)
        {
        }

        public override void SetupCustomOption()
        {
        }

        public override bool CanUseVent(PlayerControl pc, int ventId)
        {
            return false;
        }
    }
}