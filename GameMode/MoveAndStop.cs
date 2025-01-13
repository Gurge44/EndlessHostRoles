﻿using System;
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
    public long StartTimeStamp { get; set; } = startTimeStamp;
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
            if (MoveAndStop.IsEventActive && MoveAndStop.Event.Type == MoveAndStop.Events.HiddenTimers)
                return "<font=\"DIGITAL-7 SDF\" material=\"DIGITAL-7 Black Outline\"><size=130%><#ffffff>--</color></size></font>";

            bool hidden = IsYellow || (Timer == TotalGreenTime && !IsRed && !IsYellow) || (Timer == TotalRedTime && IsRed);
            string result = hidden ? Utils.ColorString(Color.clear, "--") : Utils.ColorString(IsRed ? Color.red : Color.green, Timer < 10 ? $" {Timer}" : Timer.ToString());

            if (Timer is <= 19 and >= 10 && !hidden) result = $" {result}";
            if (Timer % 10 == 1 && !hidden) result = result.Insert(result.Length - 9, " ");

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

internal class MoveAndStopPlayerData(Counter[] counters, float positionX, float positionY, int lives)
{
    public Counter LeftCounter { get; } = counters[0];

    public Counter MiddleCounter { get; } = counters[1];

    public Counter RightCounter { get; } = counters[2];

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
        if (LostLifeCooldownTimer > 0f) LostLifeCooldownTimer -= Time.deltaTime;

        LeftCounter.Update();
        MiddleCounter.Update();
        RightCounter.Update();
    }

    public bool RemoveLife(PlayerControl pc)
    {
        if (pc.inVent || LostLifeCooldownTimer > 0f) return false;

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
    public enum Events
    {
        HiddenTimers,
        DoubledTimers,
        HalvedTimers,
        FrozenTimers,
        VentAccess,
        CommsSabotage
    }

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
    private static OptionItem EventFrequency;

    private static Dictionary<Events, OptionItem> EventChances = [];
    private static Dictionary<Events, OptionItem> EventDurations = [];

    public static readonly HashSet<string> HasPlayed = [];

    public static (Events Type, int Duration, long StartTimeStamp) Event = (default(Events), 0, Utils.TimeStamp);

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

    public static string HUDText => $"{(RoundTime / 60):00}:{(RoundTime % 60):00}";
    public static bool IsEventActive => Event.Duration + Event.StartTimeStamp > Utils.TimeStamp;

    private static int StartingGreenTime(PlayerControl pc)
    {
        if (Options.CurrentGameMode == CustomGameMode.AllInOne) return 60;
        bool tutorial = EnableTutorial.GetBool() && !HasPlayed.Contains(pc.FriendCode);

        int time = 37;
        if (tutorial) time += 10;
        if (Main.CurrentMap is MapNames.Airship or MapNames.Fungle) time += 5;
        if (Main.CurrentMap == MapNames.Airship) time += 7;
        return time;
    }

    private static int RandomRedTime(char direction)
    {
        int time = direction switch
        {
            '➡' => Random.Next(RightCounterRedMin.GetInt(), RightCounterRedMax.GetInt()),
            '⇅' => Random.Next(MiddleCounterRedMin.GetInt(), MiddleCounterRedMax.GetInt()),
            '⬅' => Random.Next(LeftCounterRedMin.GetInt(), LeftCounterRedMax.GetInt()),
            _ => throw new ArgumentException("Invalid symbol representing the direction (RandomRedTime method in MoveAndStop.cs)", nameof(direction))
        };

        ApplyEventToTimer(ref time);
        return time;
    }

    private static int RandomGreenTime(char direction)
    {
        int time = ExtraGreenTime + direction switch
        {
            '➡' => Random.Next(RightCounterGreenMin.GetInt(), RightCounterGreenMax.GetInt()),
            '⇅' => Random.Next(MiddleCounterGreenMin.GetInt(), MiddleCounterGreenMax.GetInt()),
            '⬅' => Random.Next(LeftCounterGreenMin.GetInt(), LeftCounterGreenMax.GetInt()),
            _ => throw new ArgumentException("Invalid symbol representing the direction (RandomGreenTime method in MoveAndStop.cs)", nameof(direction))
        };

        ApplyEventToTimer(ref time);
        return time;
    }

    private static void ApplyEventToTimer(ref int time)
    {
        if (!IsEventActive) return;

        time = Event.Type switch
        {
            Events.HalvedTimers => time / 2,
            Events.DoubledTimers => time * 2,
            _ => time
        };

        if (time < 3) time = 3;
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

        PlayerLives = new IntegerOptionItem(68_213_017, "MoveAndStop_PlayerLives", new(1, 10, 1), 2, TabGroup.GameSettings)
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

        EventFrequency = new IntegerOptionItem(68_213_018, "MoveAndStop_EventFrequency", new(2, 120, 1), 30, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.MoveAndStop)
            .SetColor(new Color32(0, 255, 255, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds)
            .SetHeader(true);

        Events[] events = Enum.GetValues<Events>();

        EventChances = events.ToDictionary(x => x, x => new IntegerOptionItem(68_213_019 + (int)x, $"MoveAndStop_EventChance_{x}", new(0, 100, 5), EventDefaults(x).Chance, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.MoveAndStop)
            .SetColor(new Color32(0, 255, 255, byte.MaxValue))
            .SetValueFormat(OptionFormat.Percent));

        EventDurations = events.ToDictionary(x => x, x => new IntegerOptionItem(68_213_019 + events.Length + (int)x, $"MoveAndStop_EventDuration_{x}", new(5, 120, 5), EventDefaults(x).Duration, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.MoveAndStop)
            .SetColor(new Color32(0, 255, 255, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds));

        return;

        (int Chance, int Duration) EventDefaults(Events e) => e switch
        {
            Events.HiddenTimers => (30, 20),
            Events.DoubledTimers => (20, 25),
            Events.HalvedTimers => (50, 25),
            Events.FrozenTimers => (40, 10),
            Events.VentAccess => (35, 10),
            Events.CommsSabotage => (60, 30),
            _ => (50, 25)
        };
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

            Counter[] counters = new[] { '⬅', '⇅', '➡' }.Select(x => new Counter(startingGreenTime, RandomRedTime(x), now, x, false, RandomRedTime, RandomGreenTime)).ToArray();
            AllPlayerTimers[pc.PlayerId] = new(counters, pos.x, pos.y, PlayerLives.GetInt());

            float limit;

            try
            {
                limit = pc.GetClient().PlatformData.Platform is Platforms.Unknown or Platforms.IPhone or Platforms.Android or Platforms.Switch or Platforms.Xbox or Platforms.Playstation
                    ? 2f // If the player has a joystick, the game is a lot harder
                    : 0.5f; // On PC, you have WASD, you can't mess up
            }
            catch { limit = 2f; }

            FixedUpdatePatch.Limit[pc.PlayerId] = limit;
        }
    }

    public static int GetRankFromScore(byte playerId)
    {
        try
        {
            PlayerState state = Main.PlayerStates[playerId];
            int ms = state.TaskState.CompletedTasksCount;
            int rank = 1 + Main.PlayerStates.Values.Count(x => x.TaskState.CompletedTasksCount > ms);
            rank += Main.PlayerStates.Values.Where(x => x.TaskState.CompletedTasksCount == ms).ToList().IndexOf(state);
            return rank;
        }
        catch { return Main.AllPlayerControls.Length; }
    }

    public static string GetSuffixText(PlayerControl pc)
    {
        if (!pc.IsAlive() || !AllPlayerTimers.TryGetValue(pc.PlayerId, out MoveAndStopPlayerData timers)) return string.Empty;

        var text = IsEventActive ? $"{string.Format(GetString("MoveAndStop_EventActive"), GetString($"MoveAndStop_Event_{Event.Type}"), Event.Duration + Event.StartTimeStamp - Utils.TimeStamp)}\n" : "\n";
        text += IsEventActive && Event.Type == Events.FrozenTimers ? FixedUpdatePatch.LastSuffix[pc.PlayerId].Trim() : timers.ToString();

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

            long now = Utils.TimeStamp;

            if (!HasJustStarted && pc.IsLocalPlayer() && !IsEventActive && (now - Event.StartTimeStamp - Event.Duration) >= EventFrequency.GetInt())
            {
                var pool = EventChances.SelectMany(x => Enumerable.Repeat(x.Key, x.Value.GetInt() / 5)).ToList();
                if (Event.Duration == 0) pool.RemoveAll(x => x == Events.VentAccess);

                if (pool.Count > 0)
                {
                    Events newEvent = pool.RandomElement();
                    int duration = EventDurations[newEvent].GetInt();

                    Event = (newEvent, duration, now);

                    switch (newEvent)
                    {
                        case Events.VentAccess:
                        {
                            Main.AllAlivePlayerControls.Do(x => x.RpcChangeRoleBasis(CustomRoles.EngineerEHR));

                            LateTask.New(() =>
                            {
                                Main.AllAlivePlayerControls.Do(x =>
                                {
                                    x.RpcChangeRoleBasis(CustomRoles.Tasker);

                                    if (x.inVent || x.MyPhysics.Animations.IsPlayingEnterVentAnimation())
                                        LateTask.New(() => x.MyPhysics.RpcExitVent(x.GetClosestVent().Id), 1f, log: false);
                                });
                            }, duration, log: false);

                            break;
                        }
                        case Events.CommsSabotage:
                        {
                            Main.AllAlivePlayerControls.Do(x => x.RpcDesyncRepairSystem(SystemTypes.Comms, 128));

                            LateTask.New(() =>
                            {
                                Main.AllAlivePlayerControls.Do(x =>
                                {
                                    x.RpcDesyncRepairSystem(SystemTypes.Comms, 16);
                                    if (Main.NormalOptions.MapId is 1 or 5) x.RpcDesyncRepairSystem(SystemTypes.Comms, 17);
                                });
                            }, duration, log: false);

                            break;
                        }
                        case Events.FrozenTimers:
                        {
                            AllPlayerTimers.Values.SelectMany(x => new[] { x.LeftCounter, x.MiddleCounter, x.RightCounter }).Do(x => x.StartTimeStamp += duration);
                            break;
                        }
                    }
                }
            }

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

                if (IsEventActive && Event.Type == Events.FrozenTimers)
                {
                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                    goto NoSuffix;
                }

                string suffix = GetSuffixText(pc);

                if (!LastSuffix.TryGetValue(pc.PlayerId, out string beforeSuffix) || beforeSuffix != suffix)
                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);

                LastSuffix[pc.PlayerId] = suffix;

                bool IsCounterRed(Counter counter) => counter.IsRed && (pc.IsHost() || counter.Timer != counter.TotalRedTime);
            }

            NoSuffix:

            if (LastFixedUpdate == now) return;
            LastFixedUpdate = now;

            RoundTime--;
        }
    }
}