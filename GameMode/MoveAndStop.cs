using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

public class Counter(int totalGreenTime, int totalRedTime, long startTimeStamp, char symbol, bool isRed, Func<char, int> randomRedTimeFunc, Func<char, int> randomGreenTimeFunc, bool isYellow = false)
{
    private int TotalGreenTime { get; set; } = totalGreenTime;
    public int TotalRedTime { get; private set; } = totalRedTime;
    private long StartTimeStamp { get; set; } = startTimeStamp;
    private char Symbol { get; } = symbol;
    public bool IsRed { get; private set; } = isRed;
    private Func<char, int> RandomRedTime { get; } = randomRedTimeFunc;
    private Func<char, int> RandomGreenTime { get; } = randomGreenTimeFunc;
    private bool IsYellow { get; set; } = isYellow;
    private static int TotalYellowTime => 3;

    public int Timer => (IsRed ? TotalRedTime : IsYellow ? TotalYellowTime : TotalGreenTime) - (int)Math.Round((double)(Utils.TimeStamp - StartTimeStamp));

    public string ColoredTimerString
    {
        get
        {
            string result = IsYellow || (Timer == TotalGreenTime && !IsRed && !IsYellow) || (Timer == TotalRedTime && IsRed) ? Utils.ColorString(Color.clear, "0") : Utils.ColorString(IsRed ? Color.red : Color.green, Timer < 10 ? $" {Timer}" : Timer.ToString());

            if (Timer is <= 19 and >= 10 && !IsYellow) result = $" {result}";
            if (Timer % 10 == 1 && !IsYellow) result = result.Insert(result.Length - 9, " ");

            return $"<font=\"DIGITAL-7 SDF\" material=\"DIGITAL-7 Black Outline\"><size=130%>{result}</size></font>";
        }
    }

    public string ColoredArrow => Utils.ColorString(IsRed ? Timer <= 2 ? Palette.Orange : Color.red : IsYellow ? Color.yellow : Color.green, Symbol.ToString());

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
                    TotalGreenTime = RandomGreenTime(Symbol);
                    IsRed = false;
                    break;
                // Change from yellow to red
                case false when IsYellow:
                    TotalRedTime = RandomRedTime(Symbol);
                    IsYellow = false;
                    IsRed = true;
                    break;
            }

            StartTimeStamp = Utils.TimeStamp;
        }
    }
}

internal class MoveAndStopPlayerData(Counter leftCounter, Counter middleCounter, Counter rightCounter, float positionX, float positionY, int lives)
{
    public Counter LeftCounter { get; } = leftCounter;

    public Counter MiddleCounter { get; } = middleCounter;

    public Counter RightCounter { get; } = rightCounter;

    public float PositionX { get; set; } = positionX;

    public float PositionY { get; set; } = positionY;

    public int Lives { get; private set; } = lives;

    private float LostLifeCooldownTimer { get; set; }

    public override string ToString()
    {
        string leftTimer = LeftCounter.ColoredTimerString;
        string middleTimer = MiddleCounter.ColoredTimerString;
        string rightTimer = RightCounter.ColoredTimerString;

        var arrowRow = $"{LeftCounter.ColoredArrow}   {MiddleCounter.ColoredArrow}   {RightCounter.ColoredArrow}";
        var counterRow = $"{leftTimer}  {middleTimer}  {rightTimer}";

        return $"{counterRow}\n{arrowRow}";
    }

    public void UpdateCounters()
    {
        LeftCounter.Update();
        MiddleCounter.Update();
        RightCounter.Update();

        if (LostLifeCooldownTimer > 0f) LostLifeCooldownTimer -= Time.deltaTime;
    }

    public bool RemoveLife(PlayerControl pc)
    {
        if (LostLifeCooldownTimer > 0f) return false;

        Lives--;
        LostLifeCooldownTimer = 1f;

        if (Lives <= 0)
        {
            pc.Suicide();
            return true;
        }

        pc.KillFlash();
        pc.Notify(string.Format(GetString("MoveAndStop_LivesRemainingNotify"), Lives));

        return false;
    }
}

internal static class MoveAndStop
{
    private static Dictionary<byte, MoveAndStopPlayerData> AllPlayerTimers = [];

    private static OptionItem GameTime;
    private static OptionItem PlayerLives;
    private static OptionItem RightCounterGreenMin;
    private static OptionItem RightCounterRedMin;
    private static OptionItem RightCounterGreenMax;
    private static OptionItem RightCounterRedMax;
    private static OptionItem LeftCounterGreenMin;
    private static OptionItem LeftCounterRedMin;
    private static OptionItem LeftCounterGreenMax;
    private static OptionItem LeftCounterRedMax;
    private static OptionItem MiddleCounterGreenMin;
    private static OptionItem MiddleCounterRedMin;
    private static OptionItem MiddleCounterGreenMax;
    private static OptionItem MiddleCounterRedMax;
    private static OptionItem ExtraGreenTimeOnAirhip;
    private static OptionItem ExtraGreenTimeOnFungle;
    private static OptionItem EnableTutorial;

    public static readonly HashSet<string> HasPlayed = [];

    private static IRandom Random => IRandom.Instance;

    public static int RoundTime { get; set; }

    private static bool HasJustStarted => GameTime.GetInt() - RoundTime < 20;

    private static int ExtraGreenTime => Main.CurrentMap switch
    {
        MapNames.Airship => ExtraGreenTimeOnAirhip.GetInt(),
        MapNames.Fungle => ExtraGreenTimeOnFungle.GetInt(),
        _ => 0
    } + (Options.CurrentGameMode == CustomGameMode.AllInOne ? IRandom.Instance.Next(AllInOneGameMode.MoveAndStopMinGreenTimeBonus.GetInt(), AllInOneGameMode.MoveAndStopMaxGreenTimeBonus.GetInt() + 1) : 0);

    private static IntegerValueRule CounterValueRule => new(1, 100, 1);
    private static IntegerValueRule ExtraTimeValue => new(0, 50, 1);
    private static int DefaultMinValue => 5;
    private static int DefaultMaxValue => 30;

    public static string HUDText => string.Format(GetString("KBTimeRemain"), RoundTime.ToString());

    private static int StartingGreenTime(PlayerControl pc)
    {
        if (Options.CurrentGameMode == CustomGameMode.AllInOne) return 60;
        bool tutorial = EnableTutorial.GetBool() && !HasPlayed.Contains(pc.FriendCode);
        return Main.CurrentMap == MapNames.Airship ? tutorial ? 57 : 47 : tutorial ? 47 : 37;
    }

    private static int RandomRedTime(char direction)
    {
        return direction switch
        {
            '➡' => Random.Next(RightCounterRedMin.GetInt(), RightCounterRedMax.GetInt()),
            '⇅' => Random.Next(MiddleCounterRedMin.GetInt(), MiddleCounterRedMax.GetInt()),
            '⬅' => Random.Next(LeftCounterRedMin.GetInt(), LeftCounterRedMax.GetInt()),
            _ => throw new ArgumentException("Invalid symbol representing the direction (RandomRedTime method in MoveAndStop.cs)", nameof(direction))
        };
    }

    private static int RandomGreenTime(char direction)
    {
        return ExtraGreenTime + direction switch
        {
            '➡' => Random.Next(RightCounterGreenMin.GetInt(), RightCounterGreenMax.GetInt()),
            '⇅' => Random.Next(MiddleCounterGreenMin.GetInt(), MiddleCounterGreenMax.GetInt()),
            '⬅' => Random.Next(LeftCounterGreenMin.GetInt(), LeftCounterGreenMax.GetInt()),
            _ => throw new ArgumentException("Invalid symbol representing the direction (RandomGreenTime method in MoveAndStop.cs)", nameof(direction))
        };
    }

    private static string CounterSettingString(string direction, bool red, bool min)
    {
        return $"MoveAndStop_{direction}Counter{(red ? "Red" : "Green")}{(min ? "Min" : "Max")}";
    }

    private static OptionItem CreateSetting(int Id, string direction, bool red, bool min)
    {
        return new IntegerOptionItem(Id, CounterSettingString(direction, red, min), CounterValueRule, min ? DefaultMinValue : DefaultMaxValue, TabGroup.GameSettings)
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
    }

    private static OptionItem CreateExtraTimeSetting(int Id, string mapName, int defaultValue)
    {
        return new IntegerOptionItem(Id, $"MoveAndStop_ExtraGreenTimeOn{mapName}", ExtraTimeValue, defaultValue, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.MoveAndStop)
            .SetColor(new Color32(0, 255, 255, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds)
            .AddReplacement(new("Green", Utils.ColorString(Color.green, "Green")));
    }

    public static void SetupCustomOption()
    {
        GameTime = new IntegerOptionItem(68_213_001, "FFA_GameTime", new(30, 1200, 10), 900, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.MoveAndStop)
            .SetColor(new Color32(0, 255, 255, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds)
            .SetHeader(true);

        PlayerLives = new IntegerOptionItem(68_213_017, "MoveAndStop_PlayerLives", new(1, 10, 1), 3, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.MoveAndStop)
            .SetColor(new Color32(0, 255, 255, byte.MaxValue))
            .SetValueFormat(OptionFormat.Health)
            .SetHeader(true);

        RightCounterGreenMin = CreateSetting(68_213_002, "Right", false, true).SetHeader(true);
        RightCounterRedMin = CreateSetting(68_213_003, "Right", true, true);
        RightCounterGreenMax = CreateSetting(68_213_004, "Right", false, false);
        RightCounterRedMax = CreateSetting(68_213_005, "Right", true, false);
        LeftCounterGreenMin = CreateSetting(68_213_006, "Left", false, true);
        LeftCounterRedMin = CreateSetting(68_213_007, "Left", true, true);
        LeftCounterGreenMax = CreateSetting(68_213_008, "Left", false, false);
        LeftCounterRedMax = CreateSetting(68_213_009, "Left", true, false);
        MiddleCounterGreenMin = CreateSetting(68_213_010, "Middle", false, true);
        MiddleCounterRedMin = CreateSetting(68_213_011, "Middle", true, true);
        MiddleCounterGreenMax = CreateSetting(68_213_012, "Middle", false, false);
        MiddleCounterRedMax = CreateSetting(68_213_013, "Middle", true, false);
        ExtraGreenTimeOnAirhip = CreateExtraTimeSetting(68_213_014, "Airship", 20);
        ExtraGreenTimeOnFungle = CreateExtraTimeSetting(68_213_015, "Fungle", 10);

        EnableTutorial = new BooleanOptionItem(68_213_016, "MoveAndStop_EnableTutorial", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.MoveAndStop)
            .SetHeader(true)
            .SetColor(new Color32(0, 255, 255, byte.MaxValue));
    }

    public static void Init()
    {
        if (!CustomGameMode.MoveAndStop.IsActiveOrIntegrated()) return;

        FixedUpdatePatch.LastSuffix = [];
        FixedUpdatePatch.Limit = [];
        AllPlayerTimers = [];
        RoundTime = GameTime.GetInt() + 8;

        FixedUpdatePatch.DoChecks = false;
        LateTask.New(() => FixedUpdatePatch.DoChecks = true, 10f, log: false);

        long now = Utils.TimeStamp;

        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            int startingGreenTime = StartingGreenTime(pc);
            Vector2 pos = pc.Pos();

            AllPlayerTimers.TryAdd(pc.PlayerId, new(
                new(startingGreenTime, RandomRedTime('⬅'), now, '⬅', false, RandomRedTime, RandomGreenTime),
                new(startingGreenTime, RandomRedTime('⇅'), now, '⇅', false, RandomRedTime, RandomGreenTime),
                new(startingGreenTime, RandomRedTime('➡'), now, '➡', false, RandomRedTime, RandomGreenTime),
                pos.x, pos.y, PlayerLives.GetInt()));

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

    public static int GetRankFromScore(byte playerId)
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
        if (!pc.IsAlive() || !AllPlayerTimers.TryGetValue(pc.PlayerId, out MoveAndStopPlayerData timers)) return string.Empty;

        var text = timers.ToString();

        if (HasJustStarted && EnableTutorial.GetBool() && !HasPlayed.Contains(pc.FriendCode) && Options.CurrentGameMode != CustomGameMode.AllInOne)
            text += $"\n\n{GetString("MoveAndStop_Tutorial")}";

        return text;
    }

    public static int GetLivesRemaining(byte id) => AllPlayerTimers.TryGetValue(id, out MoveAndStopPlayerData data) ? data.Lives : 0;

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    private static class FixedUpdatePatch
    {
        public static bool DoChecks;
        private static long LastFixedUpdate;
        public static Dictionary<byte, string> LastSuffix = [];
        public static Dictionary<byte, float> Limit = [];

        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        public static void Postfix(PlayerControl __instance)
        {
            if (!GameStates.IsInTask || !CustomGameMode.MoveAndStop.IsActiveOrIntegrated() || !__instance.IsAlive() || !AmongUsClient.Instance.AmHost || !DoChecks || __instance.PlayerId == 255) return;

            PlayerControl pc = __instance;

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

                // Now we can check the components of the direction vector to determine the movement direction
                switch (direction.x)
                {
                    // Player is moving right
                    case > 0:
                    {
                        switch (IsCounterRed(data.RightCounter))
                        {
                            // If: Right counter is red && player moved more than their limit
                            case true when distanceX > limit:
                                if (data.RemoveLife(pc)) // Remove a life from the player
                                    goto End; // If no lives left, kill them and skip the rest of the code

                                // If the player has lives left, update their position
                                goto case false;
                            // Else If: the Right counter is yellow or green
                            case false:
                                data.PositionX = currentPosition.x; // Update the player's last position regardless of the distance
                                break;
                        }

                        break;
                    }
                    // Player is moving left
                    case < 0:
                    {
                        switch (IsCounterRed(data.LeftCounter))
                        {
                            // The distance is negative here because it's the opposite direction as right
                            case true when distanceX < -limit:
                                if (data.RemoveLife(pc)) goto End;
                                goto case false;
                            case false:
                                data.PositionX = currentPosition.x;
                                break;
                        }

                        break;
                    }
                }

                if (direction.y is > 0 or < 0) // y > 0 means the player is moving up, y < 0 means the player is moving down
                {
                    switch (IsCounterRed(data.MiddleCounter))
                    {
                        // The player dies if either they moved up OR down too far
                        case true when distanceY > limit || distanceY < -limit:
                            if (data.RemoveLife(pc)) goto End;
                            goto case false;
                        case false:
                            data.PositionY = currentPosition.y;
                            break;
                    }
                }

                End:

                data.UpdateCounters();

                if (!pc.IsAlive()) goto NoSuffix; // If the player is dead, there's no need to get them a suffix and notify them

                string suffix = GetSuffixText(pc);

                if (!LastSuffix.TryGetValue(pc.PlayerId, out string beforeSuffix) || beforeSuffix != suffix)
                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);

                LastSuffix[pc.PlayerId] = suffix;

                bool IsCounterRed(Counter counter) => counter.IsRed && (pc.IsHost() || counter.Timer != counter.TotalRedTime);
            }

            NoSuffix:

            long now = Utils.TimeStamp;
            if (LastFixedUpdate == now) return;
            LastFixedUpdate = now;

            RoundTime--;
        }
    }
}