using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace EHR;

public static class AllInOneGameMode
{
    public static HashSet<byte> Taskers = [];

    public static readonly Dictionary<CustomGameMode, OptionItem> GameModeIntegrationSettings = [];

    public static OptionItem HotPotatoTimerMultiplier;
    public static OptionItem MoveAndStopMinGreenTimeBonus;
    public static OptionItem MoveAndStopMaxGreenTimeBonus;
    public static OptionItem NaturalDisastersDisasterSpawnCooldownMultiplier;
    public static OptionItem NaturalDisastersWarningDurationMultiplier;
    public static OptionItem RoomRushTimeLimitMultiplier;
    public static OptionItem SpeedrunTimeLimitMultiplier;

    public static void SetupCustomOption()
    {
        var id = 69_218_001;
        var color = ColorUtility.TryParseHtmlString("#f542ad", out Color c) ? c : Color.magenta;
        const CustomGameMode gameMode = CustomGameMode.AllInOne;

        foreach (CustomGameMode mode in Enum.GetValues<CustomGameMode>())
        {
            if (mode is CustomGameMode.Standard or gameMode or CustomGameMode.All) continue;

            bool defaultValue = mode is CustomGameMode.HotPotato or CustomGameMode.MoveAndStop or CustomGameMode.NaturalDisasters or CustomGameMode.RoomRush or CustomGameMode.SoloKombat or CustomGameMode.Speedrun;

            GameModeIntegrationSettings[mode] = new BooleanOptionItem(id++, $"AllInOne.{mode}.Integration", defaultValue, TabGroup.GameSettings)
                .SetHeader((int)mode == 2)
                .SetGameMode(gameMode)
                .SetColor(color);
        }

        HotPotatoTimerMultiplier = new IntegerOptionItem(id++, "AllInOne.HotPotato.TimerMultiplier", new(1, 10, 1), 3, TabGroup.GameSettings)
            .SetHeader(true)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetGameMode(gameMode)
            .SetColor(color);

        MoveAndStopMinGreenTimeBonus = new IntegerOptionItem(id++, "AllInOne.MoveAndStop.MinGreenTimeBonus", new(0, 60, 1), 5, TabGroup.GameSettings)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(gameMode)
            .SetColor(color);

        MoveAndStopMaxGreenTimeBonus = new IntegerOptionItem(id++, "AllInOne.MoveAndStop.MaxGreenTimeBonus", new(0, 60, 1), 20, TabGroup.GameSettings)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(gameMode)
            .SetColor(color);

        NaturalDisastersDisasterSpawnCooldownMultiplier = new IntegerOptionItem(id++, "AllInOne.NaturalDisasters.DisasterSpawnCooldownMultiplier", new(1, 10, 1), 4, TabGroup.GameSettings)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetGameMode(gameMode)
            .SetColor(color);

        NaturalDisastersWarningDurationMultiplier = new IntegerOptionItem(id++, "AllInOne.NaturalDisasters.WarningDurationMultiplier", new(1, 10, 1), 6, TabGroup.GameSettings)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetGameMode(gameMode)
            .SetColor(color);

        RoomRushTimeLimitMultiplier = new IntegerOptionItem(id++, "AllInOne.RoomRush.TimeLimitMultiplier", new(1, 10, 1), 3, TabGroup.GameSettings)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetGameMode(gameMode)
            .SetColor(color);

        SpeedrunTimeLimitMultiplier = new IntegerOptionItem(id, "AllInOne.Speedrun.TimeLimitMultiplier", new(1, 10, 1), 3, TabGroup.GameSettings)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetGameMode(gameMode)
            .SetColor(color);
    }

    public static void Init()
    {
        Taskers = [];
    }

    public static CustomGameMode GetPrioritizedGameModeForRoles()
    {
        return new[]
        {
            CustomGameMode.HideAndSeek,
            CustomGameMode.CaptureTheFlag,
            CustomGameMode.FFA,
            CustomGameMode.SoloKombat,
            CustomGameMode.RoomRush,
            CustomGameMode.Speedrun,
            CustomGameMode.NaturalDisasters,
            CustomGameMode.MoveAndStop,
            CustomGameMode.HotPotato
        }.First(x => x.IsActiveOrIntegrated());
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    private static class FixedUpdatePatch
    {
        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        public static void Postfix(PlayerControl __instance)
        {
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || GameStates.IsEnded || Options.CurrentGameMode != CustomGameMode.AllInOne || Main.HasJustStarted || __instance.PlayerId == 255 || __instance.Is(CustomRoles.Killer) || CustomGameMode.HideAndSeek.IsActiveOrIntegrated()) return;

            var gameModes = Enum.GetValues<CustomGameMode>().Where(x => x.IsActiveOrIntegrated()).Split(x => x is CustomGameMode.CaptureTheFlag or CustomGameMode.FFA or CustomGameMode.SoloKombat);
            if (gameModes.TrueList.Count == 0 || gameModes.FalseList.Count == 0) return;

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