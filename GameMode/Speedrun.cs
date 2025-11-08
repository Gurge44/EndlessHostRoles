using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR;

public static class Speedrun
{
    private static OptionItem TaskFinishWins;
    private static OptionItem TimeStacksUp;
    private static OptionItem TimeLimit;
    private static OptionItem KillCooldown;
    private static OptionItem KillersCanKillTaskingPlayers;

    public static HashSet<byte> CanKill = [];
    public static Dictionary<byte, int> Timers = [];

    public static int KCD => KillCooldown.GetInt();
    public static int TimeLimitValue => TimeLimit.GetInt();
    public static bool RestrictedKilling => !KillersCanKillTaskingPlayers.GetBool();

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

        TimeLimit = new IntegerOptionItem(id + 2, "Speedrun_TimeLimit", new(1, 90, 1), 20, TabGroup.GameSettings)
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
        Timers = Main.AllAlivePlayerControls.ToDictionary(x => x.PlayerId, _ => TimeLimit.GetInt() + 10);
        Utils.SendRPC(CustomRPC.SpeedrunSync, 1);
    }

    public static void ResetTimer(PlayerControl pc)
    {
        int timer = TimeLimit.GetInt();
        if (Main.CurrentMap is MapNames.Airship or MapNames.Fungle) timer += 5;
        if (SubmergedCompatibility.IsSubmerged()) timer *= 2;

        if (TimeStacksUp.GetBool())
            Timers[pc.PlayerId] += timer;
        else
            Timers[pc.PlayerId] = timer;

        Logger.Info($" Timer for {pc.GetRealName()} set to {Timers[pc.PlayerId]}", "Speedrun");
    }

    public static void OnTaskFinish(PlayerControl pc)
    {
        if (TaskFinishWins.GetBool()) return;

        CanKill.Add(pc.PlayerId);
        Utils.SendRPC(CustomRPC.SpeedrunSync, 2, pc.PlayerId);
        int kcd = KillCooldown.GetInt();
        Main.AllPlayerKillCooldown[pc.PlayerId] = kcd;
        pc.RpcChangeRoleBasis(CustomRoles.SerialKiller);
        pc.Notify(Translator.GetString("Speedrun_CompletedTasks"), sendOption: SendOption.None);
        pc.SyncSettings();
        LateTask.New(() => pc.SetKillCooldown(kcd), 0.2f, log: false);
    }

    public static string GetTaskBarText()
    {
        return string.Join('\n', Main.PlayerStates
            .Join(Main.AllAlivePlayerControls, x => x.Key, x => x.PlayerId, (kvp, _) => (
                Name: kvp.Key.ColoredPlayerName(),
                CompletedTasks: kvp.Value.TaskState.CompletedTasksCount,
                AllTasks: kvp.Value.TaskState.AllTasksCount,
                Time: AmongUsClient.Instance.AmHost ? $" ({Timers.GetValueOrDefault(kvp.Key)}s)" : string.Empty))
            .OrderByDescending(x => x.CompletedTasks)
            .Select(x => x.CompletedTasks < x.AllTasks ? $"{x.Name}: {x.CompletedTasks}/{x.AllTasks}{x.Time}" : $"{x.Name}: {Translator.GetString("Speedrun_KillingPlayer")}{x.Time}"));
    }

    public static string GetSuffixText(PlayerControl pc)
    {
        if (!pc.IsAlive()) return string.Empty;

        int time = Timers[pc.PlayerId];
        int alive = Main.AllAlivePlayerControls.Length;
        int apc = Main.AllPlayerControls.Length;
        int killers = CanKill.Count;

        string arrows = TargetArrow.GetAllArrows(pc.PlayerId);
        arrows = arrows.Length > 0 ? $"\n{arrows}" : string.Empty;

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (CanKill.Contains(pc.PlayerId)) return string.Format(Translator.GetString("Speedrun_CanKillSuffixInfo"), alive, apc, killers - 1, time) + arrows;
        return string.Format(Translator.GetString("Speedrun_DoTasksSuffixInfo"), pc.GetTaskState().RemainingTasksCount, alive, apc, killers, time);
    }

    public static bool CheckForGameEnd(out GameOverReason reason)
    {
        PlayerControl[] aapc = Main.AllAlivePlayerControls;

        if (TaskFinishWins.GetBool())
        {
            PlayerControl player = aapc.FirstOrDefault(x => x.GetTaskState().IsTaskFinished);

            if (player != null)
            {
                CustomWinnerHolder.WinnerIds = [player.PlayerId];
                reason = GameOverReason.CrewmatesByTask;
                return true;
            }
        }

        switch (aapc.Length)
        {
            case 1:
                CustomWinnerHolder.WinnerIds = [aapc[0].PlayerId];
                reason = GameOverReason.ImpostorsByKill;
                return true;
            case 0:
                CustomWinnerHolder.WinnerIds = [];
                reason = GameOverReason.CrewmateDisconnect;
                return true;
        }

        reason = GameOverReason.ImpostorsByKill;
        KeyCode[] keys = [KeyCode.LeftShift, KeyCode.L, KeyCode.Return];
        return keys.Any(Input.GetKeyDown) && keys.All(Input.GetKey);
    }

    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!CanKill.Contains(killer.PlayerId)) return false;

        bool allow = CanKill.Contains(target.PlayerId) || KillersCanKillTaskingPlayers.GetBool();

        if (allow)
        {
            if (Main.GM.Value && AmongUsClient.Instance.AmHost) PlayerControl.LocalPlayer.KillFlash();
            ChatCommands.Spectators.ToValidPlayers().Do(x => x.KillFlash());
        }

        return allow;
    }

    //[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class FixedUpdatePatch
    {
        private static long LastUpdate;
        private static bool Arrow;

        public static void Postfix(PlayerControl __instance)
        {
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || ExileController.Instance || Options.CurrentGameMode != CustomGameMode.Speedrun || Main.HasJustStarted || __instance.Is(CustomRoles.Killer) || __instance.PlayerId >= 254) return;

            if (__instance.IsAlive() && Timers[__instance.PlayerId] <= 0)
            {
                __instance.Suicide();

                if (__instance.AmOwner)
                    Achievements.Type.OutOfTime.Complete();
            }

            long now = Utils.TimeStamp;
            if (LastUpdate == now) return;
            LastUpdate = now;

            Timers.AdjustAllValues(x => x - 1);

            CanKill.RemoveWhere(x => x.GetPlayer() == null || !x.GetPlayer().IsAlive());

            PlayerControl[] aapc = Main.AllAlivePlayerControls;

            switch (Arrow, aapc.Length == 2)
            {
                case (false, true):
                {
                    PlayerControl pc1 = aapc[0];
                    PlayerControl pc2 = aapc[1];

                    if (CanKill.Contains(pc1.PlayerId) && CanKill.Contains(pc2.PlayerId) && Timers[pc1.PlayerId] == Timers[pc2.PlayerId])
                    {
                        TargetArrow.Add(pc1.PlayerId, pc2.PlayerId);
                        TargetArrow.Add(pc2.PlayerId, pc1.PlayerId);
                        Arrow = true;
                    }

                    break;
                }
                case (true, false):
                {
                    Main.PlayerStates.Keys.Do(TargetArrow.RemoveAllTarget);
                    Arrow = false;
                    break;
                }
            }

            Utils.NotifyRoles();
        }
    }
}

public class Runner : RoleBase
{
    public override bool IsEnable => false;

    public override void Init() { }

    public override void Add(byte playerId) { }

    public override void SetupCustomOption() { }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc);
    }
}