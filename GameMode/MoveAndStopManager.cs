using System;
using System.Collections.Generic;
using System.Linq;
using EHR.AddOns.Common;
using HarmonyLib;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

public class Counter(int totalGreenTime, int totalRedTime, long startTimeStamp, char symbol, bool isRed, bool isYellow = false, bool moveAndStop = true)
{
    public int TotalGreenTime
    {
        get => totalGreenTime;
        set => totalGreenTime = value;
    }

    public int TotalRedTime
    {
        get => totalRedTime;
        set => totalRedTime = value;
    }

    public long StartTimeStamp
    {
        get => startTimeStamp;
        set => startTimeStamp = value;
    }

    public char Symbol
    {
        get => symbol;
        set => symbol = value;
    }

    public bool IsRed
    {
        get => isRed;
        set => isRed = value;
    }

    public bool IsYellow
    {
        get => isYellow;
        set => isYellow = value;
    }

    public bool MoveAndStop
    {
        get => moveAndStop;
        set => moveAndStop = value;
    }

    private static int TotalYellowTime => 3;

    public int Timer
    {
        get => (IsRed ? TotalRedTime : IsYellow ? TotalYellowTime : TotalGreenTime) - (int)Math.Round((double)(Utils.TimeStamp - StartTimeStamp));
    }

    public string ColoredTimerString
    {
        get
        {
            string result = IsYellow || (Timer == TotalGreenTime && !IsRed && !IsYellow) || (Timer == TotalRedTime && IsRed) ? Utils.ColorString(Color.clear, "00") : Utils.ColorString(IsRed ? Color.red : Color.green, Timer < 10 ? $" {Timer}" : Timer.ToString());

            if (Timer is <= 19 and >= 10 && !IsYellow) result = $" {result}";
            if (Timer % 10 == 1 && !IsYellow) result = result.Insert(result.Length - 9, " ");

            return $"<font=\"DIGITAL-7 SDF\"><size=130%>{result}</size></font>";
        }
    }

    public string ColoredArrow
    {
        get => Utils.ColorString(IsRed ? Timer <= 2 ? Palette.Orange : Color.red : IsYellow ? Color.yellow : Color.green, Symbol.ToString());
    }

    public void Update()
    {
        if (Timer <= 0)
        {
            switch (IsRed)
            {
                // Change from green to yellow
                case false when !IsYellow:
                    IsYellow = true;
                    break;
                // Change from red to green
                case true when !IsYellow:
                    TotalGreenTime = MoveAndStop ? MoveAndStopManager.RandomGreenTime(Symbol) : Asthmatic.RandomGreenTime();
                    IsRed = false;
                    break;
                default:
                    if (IsYellow && !IsRed) // Change from yellow to red
                    {
                        TotalRedTime = MoveAndStop ? MoveAndStopManager.RandomRedTime(Symbol) : Asthmatic.RandomRedTime();
                        IsYellow = false;
                        IsRed = true;
                    }

                    break;
            }

            StartTimeStamp = Utils.TimeStamp;
        }
    }
}

class MoveAndStopPlayerData(Counter leftCounter, Counter middleCounter, Counter rightCounter, float positionX, float positionY)
{
    public Counter LeftCounter
    {
        get => leftCounter;
        set => leftCounter = value;
    }

    public Counter MiddleCounter
    {
        get => middleCounter;
        set => middleCounter = value;
    }

    public Counter RightCounter
    {
        get => rightCounter;
        set => rightCounter = value;
    }

    public float PositionX
    {
        get => positionX;
        set => positionX = value;
    }

    public float PositionY
    {
        get => positionY;
        set => positionY = value;
    }

    public override string ToString()
    {
        var leftTimer = LeftCounter.ColoredTimerString;
        var middleTimer = MiddleCounter.ColoredTimerString;
        var rightTimer = RightCounter.ColoredTimerString;

        var arrowRow = $"{LeftCounter.ColoredArrow}   {MiddleCounter.ColoredArrow}   {RightCounter.ColoredArrow}";
        var counterRow = $"{leftTimer}  {middleTimer}  {rightTimer}";

        return $"{counterRow}\n{arrowRow}";
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
    private static OptionItem MoveAndStop_ExtraGreenTimeOnAirhip;
    private static OptionItem MoveAndStop_ExtraGreenTimeOnFungle;
    private static OptionItem MoveAndStop_EnableTutorial;

    public static readonly HashSet<string> HasPlayed = [];

    private static IRandom Random => IRandom.Instance;

    public static int RoundTime { get; set; }

    public static bool HasJustStarted => MoveAndStop_GameTime.GetInt() - RoundTime < 30;

    public static bool Tutorial => MoveAndStop_EnableTutorial.GetBool();

    private static int ExtraGreenTime => (MapNames)Main.NormalOptions.MapId switch
    {
        MapNames.Airship => MoveAndStop_ExtraGreenTimeOnAirhip.GetInt(),
        MapNames.Fungle => MoveAndStop_ExtraGreenTimeOnFungle.GetInt(),
        _ => 0
    };

    private static IntegerValueRule CounterValueRule => new(1, 100, 1);
    private static IntegerValueRule ExtraTimeValue => new(0, 50, 1);
    private static int DefaultMinValue => 5;
    private static int DefaultMaxValue => 30;

    public static string HUDText => string.Format(GetString("KBTimeRemain"), RoundTime.ToString());
    private static int StartingGreenTime(PlayerControl pc) => (MapNames)Main.NormalOptions.MapId == MapNames.Airship ? Tutorial && !HasPlayed.Contains(pc.FriendCode) ? 60 : 40 : Tutorial && !HasPlayed.Contains(pc.FriendCode) ? 50 : 30;

    public static int RandomRedTime(char direction) => direction switch
    {
        '→' => Random.Next(MoveAndStop_RightCounterRedMin.GetInt(), MoveAndStop_RightCounterRedMax.GetInt()),
        '●' => Random.Next(MoveAndStop_MiddleCounterRedMin.GetInt(), MoveAndStop_MiddleCounterRedMax.GetInt()),
        '←' => Random.Next(MoveAndStop_LeftCounterRedMin.GetInt(), MoveAndStop_LeftCounterRedMax.GetInt()),
        _ => throw new NotImplementedException()
    };

    public static int RandomGreenTime(char direction) => ExtraGreenTime + direction switch
    {
        '→' => Random.Next(MoveAndStop_RightCounterGreenMin.GetInt(), MoveAndStop_RightCounterGreenMax.GetInt()),
        '●' => Random.Next(MoveAndStop_MiddleCounterGreenMin.GetInt(), MoveAndStop_MiddleCounterGreenMax.GetInt()),
        '←' => Random.Next(MoveAndStop_LeftCounterGreenMin.GetInt(), MoveAndStop_LeftCounterGreenMax.GetInt()),
        _ => throw new NotImplementedException()
    };

    private static string CounterSettingString(string direction, bool red, bool min) => $"MoveAndStop_{direction}Counter{(red ? "Red" : "Green")}{(min ? "Min" : "Max")}";

    private static OptionItem CreateSetting(int Id, string direction, bool red, bool min) =>
        new IntegerOptionItem(Id, CounterSettingString(direction, red, min), CounterValueRule, min ? DefaultMinValue : DefaultMaxValue, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.MoveAndStop)
            .SetColor(new Color32(0, 255, 255, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds)
            .AddReplacement(new("Red", Utils.ColorString(Color.red, "Red")))
            .AddReplacement(new("Green", Utils.ColorString(Color.green, "Green")))
            .AddReplacement(new("Minimum", Utils.ColorString(Color.blue, "Minimum")))
            .AddReplacement(new("Maximum", Utils.ColorString(Color.gray, "Maximum")))
            .AddReplacement(new("Right", Utils.ColorString(Color.magenta, "Right")))
            .AddReplacement(new("Left", Utils.ColorString(Color.yellow, "Left")))
            .AddReplacement(new("Middle", Utils.ColorString(Color.white, "Middle")));

    private static OptionItem CreateExtraTimeSetting(int Id, string mapName, int defaultValue) =>
        new IntegerOptionItem(Id, $"MoveAndStop_ExtraGreenTimeOn{mapName}", ExtraTimeValue, defaultValue, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.MoveAndStop)
            .SetColor(new Color32(0, 255, 255, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds)
            .AddReplacement(new("Green", Utils.ColorString(Color.green, "Green")));

    public static void SetupCustomOption()
    {
        MoveAndStop_GameTime = new IntegerOptionItem(68_213_001, "FFA_GameTime", new(30, 1200, 10), 900, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.MoveAndStop)
            .SetColor(new Color32(0, 255, 255, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds)
            .SetHeader(true);
        MoveAndStop_RightCounterGreenMin = CreateSetting(68_213_002, "Right", false, true);
        MoveAndStop_RightCounterRedMin = CreateSetting(68_213_003, "Right", true, true);
        MoveAndStop_RightCounterGreenMax = CreateSetting(68_213_004, "Right", false, false);
        MoveAndStop_RightCounterRedMax = CreateSetting(68_213_005, "Right", true, false);
        MoveAndStop_LeftCounterGreenMin = CreateSetting(68_213_006, "Left", false, true);
        MoveAndStop_LeftCounterRedMin = CreateSetting(68_213_007, "Left", true, true);
        MoveAndStop_LeftCounterGreenMax = CreateSetting(68_213_008, "Left", false, false);
        MoveAndStop_LeftCounterRedMax = CreateSetting(68_213_009, "Left", true, false);
        MoveAndStop_MiddleCounterGreenMin = CreateSetting(68_213_010, "Middle", false, true);
        MoveAndStop_MiddleCounterRedMin = CreateSetting(68_213_011, "Middle", true, true);
        MoveAndStop_MiddleCounterGreenMax = CreateSetting(68_213_012, "Middle", false, false);
        MoveAndStop_MiddleCounterRedMax = CreateSetting(68_213_013, "Middle", true, false);
        MoveAndStop_ExtraGreenTimeOnAirhip = CreateExtraTimeSetting(68_213_014, "Airship", 20);
        MoveAndStop_ExtraGreenTimeOnFungle = CreateExtraTimeSetting(68_213_015, "Fungle", 10);
        MoveAndStop_EnableTutorial = new BooleanOptionItem(68_213_016, "MoveAndStop_EnableTutorial", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.MoveAndStop)
            .SetHeader(true)
            .SetColor(new Color32(0, 255, 255, byte.MaxValue));
    }

    public static void Init()
    {
        if (Options.CurrentGameMode != CustomGameMode.MoveAndStop) return;

        FixedUpdatePatch.LastSuffix = [];
        FixedUpdatePatch.Limit = [];
        AllPlayerTimers = [];
        RoundTime = MoveAndStop_GameTime.GetInt() + 8;

        FixedUpdatePatch.DoChecks = false;
        LateTask.New(() => { FixedUpdatePatch.DoChecks = true; }, 10f, log: false);

        long now = Utils.TimeStamp;

        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            AllPlayerTimers.TryAdd(pc.PlayerId, new(
                new(StartingGreenTime(pc), RandomRedTime('←'), now, '←', false),
                new(StartingGreenTime(pc), RandomRedTime('●'), now, '●', false),
                new(StartingGreenTime(pc), RandomRedTime('→'), now, '→', false),
                pc.Pos().x, pc.Pos().y));

            float limit;
            try
            {
                limit = pc.GetClient().PlatformData.Platform is Platforms.Unknown or Platforms.IPhone or Platforms.Android or Platforms.Switch or Platforms.Xbox or Platforms.Playstation
                    ? 2f // If the player has a joystick, the game is a lot harder
                    : 0.5f; // On PC, you have WASD, you can't mess up
            }
            catch
            {
                limit = 2f;
            }

            FixedUpdatePatch.Limit[pc.PlayerId] = limit;
        }
    }

    public static int GetRankOfScore(byte playerId)
    {
        try
        {
            int ms = Main.PlayerStates[playerId].TaskState.CompletedTasksCount;
            int rank = 1 + Main.PlayerStates.Values.Count(x => x.TaskState.CompletedTasksCount > ms);
            rank += Main.PlayerStates.Values.Where(x => x.TaskState.CompletedTasksCount == ms).ToList().IndexOf(Main.PlayerStates[playerId]);
            return rank;
        }
        catch
        {
            return Main.AllPlayerControls.Length;
        }
    }

    public static string GetSuffixText(PlayerControl pc)
    {
        if (!pc.IsAlive() || !AllPlayerTimers.TryGetValue(pc.PlayerId, out var timers)) return string.Empty;

        var text = timers.ToString();

        if (HasJustStarted && Tutorial && !HasPlayed.Contains(pc.FriendCode))
        {
            text += $"\n\n{GetString("MoveAndStop_Tutorial")}";
        }

        return text;
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    class FixedUpdatePatch
    {
        public static bool DoChecks;
        private static long LastFixedUpdate;
        public static Dictionary<byte, string> LastSuffix = [];
        public static Dictionary<byte, float> Limit = [];

        public static void Postfix(PlayerControl __instance)
        {
            if (!GameStates.IsInTask || Options.CurrentGameMode != CustomGameMode.MoveAndStop || !__instance.IsAlive() || !AmongUsClient.Instance.AmHost || !DoChecks) return;

            var pc = __instance;

            if (AllPlayerTimers.TryGetValue(pc.PlayerId, out MoveAndStopPlayerData data))
            {
                Vector2 previousPosition = new(data.PositionX, data.PositionY);

                // Update the player's position
                Vector2 currentPosition = pc.transform.position;
                currentPosition.x += data.PositionX * Time.deltaTime;
                currentPosition.y += data.PositionY * Time.deltaTime;

                // Calculate the direction of movement
                Vector2 direction = currentPosition - previousPosition;

                // Normalize the direction vector to get a unit vector
                direction.Normalize();

                // Calculate the distance moved along each axis
                float distanceX = currentPosition.x - previousPosition.x;
                float distanceY = currentPosition.y - previousPosition.y;

                float limit = Limit.GetValueOrDefault(pc.PlayerId, 2f);

                switch (direction.x)
                {
                    // Now we can check the components of the direction vector to determine the movement direction
                    // Player is moving right
                    case > 0:
                    {
                        switch (data.RightCounter.IsRed)
                        {
                            // If: Right counter is red && player moved more than their limit
                            case true when distanceX > limit:
                                pc.Suicide(); // Player suicides
                                goto End; // Skip the upcoming checks, the player is already dead
                            // Else If: Right counter is yellow or green
                            case false:
                                data.PositionX = currentPosition.x; // Update the player's last position regardless of the distance
                                break;
                        }

                        break;
                    }
                    // Player is moving left
                    case < 0:
                    {
                        switch (data.LeftCounter.IsRed)
                        {
                            // The distance is negative here because it's the opposite direction as right
                            case true when distanceX < -limit:
                                pc.Suicide();
                                goto End;
                            case false:
                                data.PositionX = currentPosition.x;
                                break;
                        }

                        break;
                    }
                }

                if (direction.y is > 0 or < 0) // y > 0 means the player is moving up, y < 0 means the player is moving down
                {
                    switch (data.MiddleCounter.IsRed)
                    {
                        // The player dies if either they moved up OR down too far
                        case true when (distanceY > limit || distanceY < -limit):
                            pc.Suicide();
                            goto End;
                        case false:
                            data.PositionY = currentPosition.y;
                            break;
                    }
                }

                End:

                data.UpdateCounters();

                if (!pc.IsAlive()) goto NoSuffix; // If the player is dead, there's no need to get them a suffix and notify them

                string suffix = GetSuffixText(pc);

                if (!LastSuffix.TryGetValue(pc.PlayerId, out var beforeSuffix) || beforeSuffix != suffix)
                {
                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                }

                LastSuffix[pc.PlayerId] = suffix;
            }

            NoSuffix:

            long now = Utils.TimeStamp;
            if (LastFixedUpdate == now) return;
            LastFixedUpdate = now;

            RoundTime--;
        }
    }
}