using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE;

class Counter(int totalGreenTime, int totalRedTime, long startTimeStamp, char symbol, bool isRed, bool isYellow = false)
{
    public int TotalGreenTime { get => totalGreenTime; set => totalGreenTime = value; }
    public int TotalRedTime { get => totalRedTime; set => totalRedTime = value; }
    public long StartTimeStamp { get => startTimeStamp; set => startTimeStamp = value; }
    public char Symbol { get => symbol; set => symbol = value; }
    public bool IsRed { get => isRed; set => isRed = value; }
    public bool IsYellow { get => isYellow; set => isYellow = value; }

    private static int TotalYellowTime => 3;
    private static Color Orange => new(255, 165, 0, 255);

    public int Timer { get => (IsRed ? TotalRedTime : IsYellow ? TotalYellowTime : TotalGreenTime) - (int)Math.Round((double)(Utils.GetTimeStamp() - StartTimeStamp)); }
    public string ColoredTimerString { get => IsYellow ? string.Empty : Utils.ColorString(IsRed ? Color.red : Color.green, Timer.ToString()); }
    public string ColoredArrow { get => Utils.ColorString(IsRed ? Timer <= 2 ? Orange : Color.red : IsYellow ? Color.yellow : Color.green, Symbol.ToString()); }

    public void Update()
    {
        if (Timer <= 0)
        {
            if (!IsRed && !IsYellow) // Change from green to yellow
            {
                IsYellow = true;
            }
            else if (IsRed && !IsYellow) // Change from red to green
            {
                TotalGreenTime = MoveAndStopManager.RandomGreenTime(Symbol);
                IsRed = false;
            }
            else if (IsYellow && !IsRed) // Change from yellow to red
            {
                TotalRedTime = MoveAndStopManager.RandomRedTime(Symbol);
                IsYellow = false;
                IsRed = true;
            }
            StartTimeStamp = Utils.GetTimeStamp();
        }
    }
}
class MoveAndStopPlayerData(Counter leftCounter, Counter middleCounter, Counter rightCounter, float position_x, float position_y)
{
    public Counter LeftCounter { get => leftCounter; set => leftCounter = value; }
    public Counter MiddleCounter { get => middleCounter; set => middleCounter = value; }
    public Counter RightCounter { get => rightCounter; set => rightCounter = value; }
    public float Position_X { get => position_x; set => position_x = value; }
    public float Position_Y { get => position_y; set => position_y = value; }

    private static string Space(int length) => length is 1 or 0 ? length is 1 ? " " /*1*/ : string.Empty /*2*/ : "  " /*0*/;

    public string SuffixText
    {
        get
        {
            var leftTimer = LeftCounter.ColoredTimerString;
            var middleTimer = MiddleCounter.ColoredTimerString;
            var rightTimer = RightCounter.ColoredTimerString;

            var arrowRow = $"{LeftCounter.ColoredArrow}  {MiddleCounter.ColoredArrow}  {RightCounter.ColoredArrow}";
            var counterRow = $"{leftTimer}{Space(leftTimer.Length)} {middleCounter}{Space(middleTimer.Length)} {rightCounter}{Space(rightTimer.Length)}";

            return $"{arrowRow}\n{counterRow}";
        }
    }

    public void UpdateCounters()
    {
        LeftCounter.Update();
        MiddleCounter.Update();
        RightCounter.Update();
    }
}
internal class MoveAndStopManager
{
    private static Dictionary<byte, MoveAndStopPlayerData> AllPlayerTimers = [];

    private static IRandom Random => IRandom.Instance;
    public static int RoundTime;

    private static int StartingGreenTime => 10;
    public static int RandomRedTime(char direction) => direction switch
    {
        '→' => Random.Next(MoveAndStop_RightCounterRedMin.GetInt(), MoveAndStop_RightCounterRedMax.GetInt()),
        '●' => Random.Next(MoveAndStop_MiddleCounterRedMin.GetInt(), MoveAndStop_MiddleCounterRedMax.GetInt()),
        '←' => Random.Next(MoveAndStop_LeftCounterRedMin.GetInt(), MoveAndStop_LeftCounterRedMax.GetInt()),
        _ => throw new NotImplementedException(),
    };
    public static int RandomGreenTime(char direction) => direction switch
    {
        '→' => Random.Next(MoveAndStop_RightCounterGreenMin.GetInt(), MoveAndStop_RightCounterGreenMax.GetInt()),
        '●' => Random.Next(MoveAndStop_MiddleCounterGreenMin.GetInt(), MoveAndStop_MiddleCounterGreenMax.GetInt()),
        '←' => Random.Next(MoveAndStop_LeftCounterGreenMin.GetInt(), MoveAndStop_LeftCounterGreenMax.GetInt()),
        _ => throw new NotImplementedException(),
    };

    private static OptionItem MoveAndStop_GameTime;
    private static OptionItem MoveAndStop_RightCounterGreenMin;
    private static OptionItem MoveAndStop_RightCounterRedMin;
    private static OptionItem MoveAndStop_RightCounterGreenMax;
    private static OptionItem MoveAndStop_RightCounterRedMax;
    private static OptionItem MoveAndStop_LeftCounterGreenMin;
    private static OptionItem MoveAndStop_LeftCounterRedMin;
    private static OptionItem MoveAndStop_LeftCounterGreenMax;
    private static OptionItem MoveAndStop_LeftCounterRedMax;
    private static OptionItem MoveAndStop_MiddleCounterGreenMin;
    private static OptionItem MoveAndStop_MiddleCounterRedMin;
    private static OptionItem MoveAndStop_MiddleCounterGreenMax;
    private static OptionItem MoveAndStop_MiddleCounterRedMax;

    private static string CounterSettingString(string direction, bool red, bool min) => GetString($"MoveAndStop_{direction}Counter{(red ? "Red" : "Green")}{(min ? "Min" : "Max")}");
    private static IntegerValueRule CounterValueRule => new(1, 100, 1);
    private static int DefaultMinValue => 5;
    private static int DefaultMaxValue => 30;
    private static OptionItem CreateSetting(int Id, string direction, bool red, bool min) =>
    IntegerOptionItem.Create(Id, CounterSettingString(direction, red, min), CounterValueRule, min ? DefaultMinValue : DefaultMaxValue, TabGroup.GameSettings, false)
                     .SetGameMode(CustomGameMode.MoveAndStop)
                     .SetColor(new Color32(0, 255, 255, byte.MaxValue))
                     .SetValueFormat(OptionFormat.Seconds);

    public static void SetupCustomOption()
    {
        MoveAndStop_GameTime = IntegerOptionItem.Create(68_213_001, "FFA_GameTime", new(30, 1200, 10), 600, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.MoveAndStop)
            .SetColor(new Color32(0, 255, 255, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds)
            .SetHeader(true);
        MoveAndStop_RightCounterGreenMin = CreateSetting(68_213_002, "Right", false, true);
        MoveAndStop_RightCounterRedMin = CreateSetting(68_213_002, "Right", true, true);
        MoveAndStop_RightCounterGreenMax = CreateSetting(68_213_003, "Right", false, false);
        MoveAndStop_RightCounterRedMax = CreateSetting(68_213_003, "Right", true, false);
        MoveAndStop_LeftCounterGreenMin = CreateSetting(68_213_004, "Left", false, true);
        MoveAndStop_LeftCounterRedMin = CreateSetting(68_213_004, "Left", true, true);
        MoveAndStop_LeftCounterGreenMax = CreateSetting(68_213_005, "Left", false, false);
        MoveAndStop_LeftCounterRedMax = CreateSetting(68_213_005, "Left", true, false);
        MoveAndStop_MiddleCounterGreenMin = CreateSetting(68_213_006, "Middle", false, true);
        MoveAndStop_MiddleCounterRedMin = CreateSetting(68_213_006, "Middle", true, true);
        MoveAndStop_MiddleCounterGreenMax = CreateSetting(68_213_007, "Middle", false, false);
        MoveAndStop_MiddleCounterRedMax = CreateSetting(68_213_007, "Middle", true, false);
    }

    public static void Init()
    {
        if (Options.CurrentGameMode != CustomGameMode.MoveAndStop) return;

        AllPlayerTimers = [];
        RoundTime = MoveAndStop_GameTime.GetInt() + 8;

        long now = Utils.GetTimeStamp();

        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            AllPlayerTimers.TryAdd(pc.PlayerId, new(new(StartingGreenTime, RandomRedTime('←'), now, '←', false), new(StartingGreenTime, RandomRedTime('●'), now, '●', false), new(StartingGreenTime, RandomRedTime('→'), now, '→', false), pc.Pos().x, pc.Pos().y));
        }

        _ = new LateTask(Utils.SetChatVisible, 7f, "Set Chat Visible for Everyone");
    }
    public static string GetDisplayScore(byte playerId)
    {
        int rank = GetRankOfScore(playerId);
        string score = Main.PlayerStates.TryGetValue(playerId, out var s) ? $"{s.GetTaskState().CompletedTasksCount}" : "Invalid";
        string text = string.Format(GetString("MoveAndStopScoreDisplay"), rank.ToString(), score);
        Color color = Utils.GetRoleColor(CustomRoles.Tasker);
        return Utils.ColorString(color, text);
    }
    public static int GetRankOfScore(byte playerId)
    {
        try
        {
            int ms = Main.PlayerStates[playerId].GetTaskState().CompletedTasksCount;
            int rank = 1 + Main.PlayerStates.Values.Where(x => x.GetTaskState().CompletedTasksCount > ms).Count();
            rank += Main.PlayerStates.Values.Where(x => x.GetTaskState().CompletedTasksCount == ms).ToList().IndexOf(Main.PlayerStates[playerId]);
            return rank;
        }
        catch
        {
            return Main.AllPlayerControls.Length;
        }
    }
    public static string GetHudText() => string.Format(GetString("KBTimeRemain"), RoundTime.ToString());
    public static string GetSuffixText(PlayerControl pc) => AllPlayerTimers.TryGetValue(pc.PlayerId, out var timers) ? timers.SuffixText : string.Empty;

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    class FixedUpdatePatch
    {
        private static long LastFixedUpdate;
        public static void Postfix(PlayerControl __instance)
        {
            if (!GameStates.IsInTask || Options.CurrentGameMode != CustomGameMode.MoveAndStop || !__instance.IsAlive() || !AmongUsClient.Instance.AmHost) return;

            var pc = __instance;

            if (AllPlayerTimers.TryGetValue(pc.PlayerId, out var data))
            {
                Vector2 pos = pc.Pos();
                if (pos.x - data.Position_X > 0.5f)
                {
                    if (data.LeftCounter.IsRed) pc.Suicide();
                    data.Position_X = pos.x;
                }
                if (pos.x - data.Position_X < 0.5f)
                {
                    if (data.RightCounter.IsRed) pc.Suicide();
                    data.Position_X = pos.x;
                }
                if (pos.y - data.Position_Y is > 0.5f or < 0.5f)
                {
                    if (data.MiddleCounter.IsRed) pc.Suicide();
                    data.Position_Y = pos.y;
                }

                data.UpdateCounters();
            }

            long now = Utils.GetTimeStamp();
            if (LastFixedUpdate == now) return;
            LastFixedUpdate = now;

            RoundTime--;
        }
    }
}