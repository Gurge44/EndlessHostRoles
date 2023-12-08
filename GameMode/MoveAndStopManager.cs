using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TOHE.Modules;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE;

class Counter(int totalGreenTime, int totalRedTime, long startTimeStamp, char symbol, bool isRed)
{
    public int TotalGreenTime { get => totalGreenTime; set => totalGreenTime = value; }
    public int TotalRedTime { get => totalRedTime; set => totalRedTime = value; }
    public long StartTimeStamp { get => startTimeStamp; set => startTimeStamp = value; }
    public char Symbol { get => symbol; set => symbol = value; }
    public bool IsRed { get => isRed; set => isRed = value; }

    public int Timer { get => (IsRed ? TotalRedTime : TotalGreenTime) - (int)Math.Round((double)(Utils.GetTimeStamp() - StartTimeStamp)); }
    public string ColoredTimerString { get => Utils.ColorString(IsRed ? Color.red : Color.green, Timer.ToString()); }
    public string ColoredArrow { get => Utils.ColorString(IsRed ? Color.red : Color.green, Symbol.ToString()); }

    public void Update()
    {
        if (Timer <= 0)
        {
            IsRed = !IsRed;
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
    private static int RandomRedTime(string direction) => direction switch
    {
        "Right" => Random.Next(MoveAndStop_RightCounterRedMin.GetInt(), MoveAndStop_RightCounterRedMax.GetInt()),
        "Middle" => Random.Next(MoveAndStop_MiddleCounterRedMin.GetInt(), MoveAndStop_MiddleCounterRedMax.GetInt()),
        "Left" => Random.Next(MoveAndStop_LeftCounterRedMin.GetInt(), MoveAndStop_LeftCounterRedMax.GetInt()),
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
        MoveAndStop_RightCounterGreenMin  = CreateSetting(68_213_002, "Right",  false, true);
        MoveAndStop_RightCounterRedMin    = CreateSetting(68_213_002, "Right",  true,  true);
        MoveAndStop_RightCounterGreenMax  = CreateSetting(68_213_003, "Right",  false, false);
        MoveAndStop_RightCounterRedMax    = CreateSetting(68_213_003, "Right",  true,  false);
        MoveAndStop_LeftCounterGreenMin   = CreateSetting(68_213_004, "Left",   false, true);
        MoveAndStop_LeftCounterRedMin     = CreateSetting(68_213_004, "Left",   true,  true);
        MoveAndStop_LeftCounterGreenMax   = CreateSetting(68_213_005, "Left",   false, false);
        MoveAndStop_LeftCounterRedMax     = CreateSetting(68_213_005, "Left",   true,  false);
        MoveAndStop_MiddleCounterGreenMin = CreateSetting(68_213_006, "Middle", false, true);
        MoveAndStop_MiddleCounterRedMin   = CreateSetting(68_213_006, "Middle", true,  true);
        MoveAndStop_MiddleCounterGreenMax = CreateSetting(68_213_007, "Middle", false, false);
        MoveAndStop_MiddleCounterRedMax   = CreateSetting(68_213_007, "Middle", true,  false);
    }

    public static void Init()
    {
        if (Options.CurrentGameMode != CustomGameMode.MoveAndStop) return;

        AllPlayerTimers = [];
        RoundTime = MoveAndStop_GameTime.GetInt() + 8;

        long now = Utils.GetTimeStamp();

        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            AllPlayerTimers.TryAdd(pc.PlayerId, new(new(StartingGreenTime, RandomRedTime("Left"), now, '←', false), new(StartingGreenTime, RandomRedTime("Middle"), now, '●', false), new(StartingGreenTime, RandomRedTime("Right"), now, '→', false), pc.Pos().x, pc.Pos().y));
        }

        _ = new LateTask(Utils.SetChatVisible, 7f, "Set Chat Visible for Everyone");
    }
    public static string GetDisplayScore(byte playerId)
    {
        int rank = GetRankOfScore(playerId);
        string score = KillCount.TryGetValue(playerId, out var s) ? $"{s}" : "Invalid";
        string text = string.Format(GetString("KBDisplayScore"), rank.ToString(), score);
        Color color = Utils.GetRoleColor(CustomRoles.Killer);
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
    public static string GetHudText()
    {
        return string.Format(GetString("KBTimeRemain"), RoundTime.ToString());
    }
    public static void OnPlayerAttack(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null || Options.CurrentGameMode != CustomGameMode.MoveAndStop) return;
        if (target.inVent)
        {
            Logger.Info("Target is in a vent, kill blocked", "MoveAndStop");
            return;
        }
        var totalalive = Main.AllAlivePlayerControls.Length;
        if (MoveAndStopShieldedList.TryGetValue(target.PlayerId, out var dur))
        {
            killer.Notify(GetString("MoveAndStop_TargetIsShielded"));
            Logger.Info($"{killer.GetRealName().RemoveHtmlTags()} attacked shielded player {target.GetRealName().RemoveHtmlTags()}, their shield expires in {MoveAndStop_ShieldDuration.GetInt() - (Utils.GetTimeStamp() - dur)}s", "MoveAndStop");
            if (MoveAndStop_ShieldIsOneTimeUse.GetBool())
            {
                MoveAndStopShieldedList.Remove(target.PlayerId);
                target.Notify(GetString("MoveAndStop_ShieldBroken"));
                Logger.Info($"{target.GetRealName().RemoveHtmlTags()}'s shield was removed because {killer.GetRealName().RemoveHtmlTags()} tried to kill them and the shield is one-time-use according to settings", "MoveAndStop");
            }
            return;
        }

        OnPlayerKill(killer);

        SendRPCSyncMoveAndStopPlayer(target.PlayerId);

        if (totalalive == 3)
        {
            PlayerControl otherPC = null;
            foreach (var pc in Main.AllAlivePlayerControls.Where(a => a.PlayerId != killer.PlayerId && a.PlayerId != target.PlayerId && a.IsAlive()).ToArray())
            {
                TargetArrow.Add(killer.PlayerId, pc.PlayerId);
                TargetArrow.Add(pc.PlayerId, killer.PlayerId);
                otherPC = pc;
            }
            Logger.Info($"The last 2 players ({killer.GetRealName().RemoveHtmlTags()} & {otherPC?.GetRealName().RemoveHtmlTags()}) now have an arrow toward each other", "MoveAndStop");
        }

        if (MoveAndStop_EnableRandomAbilities.GetBool())
        {
            bool sync = false;
            bool mark = false;
            var nowKCD = Main.AllPlayerKillCooldown[killer.PlayerId];
            byte EffectType;
            if (Main.NormalOptions.MapId != 4) EffectType = (byte)HashRandom.Next(0, 10);
            else EffectType = (byte)HashRandom.Next(4, 10);
            if (EffectType <= 7) // Buff
            {
                byte EffectID = (byte)HashRandom.Next(0, 3);
                if (Main.NormalOptions.MapId == 4) EffectID = 2;
                switch (EffectID)
                {
                    case 0:
                        MoveAndStopShieldedList.TryAdd(killer.PlayerId, Utils.GetTimeStamp());
                        killer.Notify(GetString("MoveAndStop-Event-GetShield"), MoveAndStop_ShieldDuration.GetFloat());
                        Main.AllPlayerKillCooldown[killer.PlayerId] = MoveAndStop_KCD.GetFloat();
                        break;
                    case 1:
                        if (MoveAndStopIncreasedSpeedList.ContainsKey(killer.PlayerId))
                        {
                            MoveAndStopIncreasedSpeedList.Remove(killer.PlayerId);
                            MoveAndStopIncreasedSpeedList.Add(killer.PlayerId, Utils.GetTimeStamp());
                        }
                        else
                        {
                            MoveAndStopIncreasedSpeedList.TryAdd(killer.PlayerId, Utils.GetTimeStamp());
                            originalSpeed.TryAdd(killer.PlayerId, Main.AllPlayerSpeed[killer.PlayerId]);
                            Main.AllPlayerSpeed[killer.PlayerId] = MoveAndStop_IncreasedSpeed.GetFloat();
                        }
                        killer.Notify(GetString("MoveAndStop-Event-GetIncreasedSpeed"), MoveAndStop_ModifiedSpeedDuration.GetFloat());
                        Main.AllPlayerKillCooldown[killer.PlayerId] = MoveAndStop_KCD.GetFloat();
                        mark = true;
                        break;
                    case 2:
                        Main.AllPlayerKillCooldown[killer.PlayerId] = System.Math.Clamp(MoveAndStop_KCD.GetFloat() - 3f, 1f, 60f);
                        killer.Notify(GetString("MoveAndStop-Event-GetLowKCD"));
                        sync = true;
                        break;
                    default:
                        Main.AllPlayerKillCooldown[killer.PlayerId] = MoveAndStop_KCD.GetFloat();
                        break;
                }
            }
            else if (EffectType == 8) // De-Buff
            {
                byte EffectID = (byte)HashRandom.Next(0, 3);
                if (Main.NormalOptions.MapId == 4) EffectID = 1;
                switch (EffectID)
                {
                    case 0:
                        if (MoveAndStopDecreasedSpeedList.ContainsKey(killer.PlayerId))
                        {
                            MoveAndStopDecreasedSpeedList.Remove(killer.PlayerId);
                            MoveAndStopDecreasedSpeedList.Add(killer.PlayerId, Utils.GetTimeStamp());
                        }
                        else
                        {
                            MoveAndStopDecreasedSpeedList.TryAdd(killer.PlayerId, Utils.GetTimeStamp());
                            originalSpeed.TryAdd(killer.PlayerId, Main.AllPlayerSpeed[killer.PlayerId]);
                            Main.AllPlayerSpeed[killer.PlayerId] = MoveAndStop_DecreasedSpeed.GetFloat();
                        }
                        killer.Notify(GetString("MoveAndStop-Event-GetDecreasedSpeed"), MoveAndStop_ModifiedSpeedDuration.GetFloat());
                        Main.AllPlayerKillCooldown[killer.PlayerId] = MoveAndStop_KCD.GetFloat();
                        mark = true;
                        break;
                    case 1:
                        Main.AllPlayerKillCooldown[killer.PlayerId] = System.Math.Clamp(MoveAndStop_KCD.GetFloat() + 3f, 1f, 60f);
                        killer.Notify(GetString("MoveAndStop-Event-GetHighKCD"));
                        sync = true;
                        break;
                    case 2:
                        MoveAndStopLowerVisionList.TryAdd(killer.PlayerId, Utils.GetTimeStamp());
                        Main.AllPlayerKillCooldown[killer.PlayerId] = MoveAndStop_KCD.GetFloat();
                        killer.Notify(GetString("MoveAndStop-Event-GetLowVision"));
                        mark = true;
                        break;
                    default:
                        Main.AllPlayerKillCooldown[killer.PlayerId] = MoveAndStop_KCD.GetFloat();
                        break;
                }
            }
            else // Mixed
            {
                _ = new LateTask(killer.TPtoRndVent, 0.5f, "MoveAndStop-Event-TP");
                killer.Notify(GetString("MoveAndStop-Event-GetTP"));
                Main.AllPlayerKillCooldown[killer.PlayerId] = MoveAndStop_KCD.GetFloat();
            }

            if (sync || nowKCD != Main.AllPlayerKillCooldown[killer.PlayerId])
            {
                mark = false;
                killer.SyncSettings();
            }
            if (mark)
            {
                killer.MarkDirtySettings();
            }
        }

        killer.Kill(target);
    }

    public static void OnPlayerKill(PlayerControl killer)
    {
        if (PlayerControl.LocalPlayer.Is(CustomRoles.GM))
            PlayerControl.LocalPlayer.KillFlash();

        KillCount[killer.PlayerId]++;
    }

    public static string GetPlayerArrow(PlayerControl seer, PlayerControl target = null)
    {
        if (GameStates.IsMeeting) return string.Empty;
        if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;
        if (Main.AllAlivePlayerControls.Length != 2) return string.Empty;

        string arrows = string.Empty;
        PlayerControl otherPlayer = null;
        foreach (var pc in Main.AllAlivePlayerControls.Where(pc => pc.IsAlive() && pc.PlayerId != seer.PlayerId).ToArray())
        {
            otherPlayer = pc;
            break;
        }
        if (otherPlayer == null) return string.Empty;

        var arrow = TargetArrow.GetArrows(seer, otherPlayer.PlayerId);
        arrows += Utils.ColorString(Utils.GetRoleColor(CustomRoles.Killer), arrow);

        return arrows;
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    class FixedUpdatePatch
    {
        private static long LastFixedUpdate;
        public static void Postfix(PlayerControl __instance)
        {
            if (!GameStates.IsInTask || Options.CurrentGameMode != CustomGameMode.MoveAndStop) return;

            if (AmongUsClient.Instance.AmHost)
            {
                var now = Utils.GetTimeStamp();

                if (LastFixedUpdate == now) return;
                LastFixedUpdate = now;

                RoundTime--;

                foreach (var pc in Main.AllPlayerControls.Where(pc => NameNotify.TryGetValue(pc.PlayerId, out var nn) && nn.TIMESTAMP < now).ToArray())
                {
                    NameNotify.Remove(pc.PlayerId);
                    SendRPCSyncNameNotify(pc);
                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                }

                var rd = IRandom.Instance;
                byte MoveAndStopdoTPdecider = (byte)rd.Next(0, 100);
                bool MoveAndStopdoTP = false;
                if (MoveAndStopdoTPdecider == 0) MoveAndStopdoTP = true;

                if (MoveAndStop_EnableRandomTwists.GetBool() && MoveAndStopdoTP)
                {
                    Logger.Info("Swap everyone with someone", "MoveAndStop");

                    List<byte> changePositionPlayers = [];

                    foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                    {
                        if (changePositionPlayers.Contains(pc.PlayerId) || !pc.IsAlive() || pc.onLadder || pc.inVent) continue;

                        var filtered = Main.AllAlivePlayerControls.Where(a =>
                            pc.IsAlive() && !pc.inVent && a.PlayerId != pc.PlayerId && !changePositionPlayers.Contains(a.PlayerId)).ToArray();
                        if (filtered.Length == 0) break;

                        PlayerControl target = filtered[rd.Next(0, filtered.Length)];

                        if (pc.inVent || target.inVent) continue;

                        changePositionPlayers.Add(target.PlayerId);
                        changePositionPlayers.Add(pc.PlayerId);

                        pc.RPCPlayCustomSound("Teleport");

                        var originPs = target.Pos();
                        target.TP(pc.Pos());
                        pc.TP(originPs);

                        target.Notify(Utils.ColorString(new Color32(0, 255, 165, byte.MaxValue), string.Format(GetString("MoveAndStop-Event-RandomTP"), pc.GetRealName())));
                        pc.Notify(Utils.ColorString(new Color32(0, 255, 165, byte.MaxValue), string.Format(GetString("MoveAndStop-Event-RandomTP"), target.GetRealName())));
                    }

                    changePositionPlayers.Clear();
                }

                if (Main.NormalOptions.MapId == 4) return;

                foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                {
                    if (pc == null) return;

                    bool sync = false;

                    if (MoveAndStopDecreasedSpeedList.TryGetValue(pc.PlayerId, out var dstime) && dstime + MoveAndStop_ModifiedSpeedDuration.GetInt() < now)
                    {
                        Logger.Info(pc.GetRealName() + "'s decreased speed expired", "MoveAndStop");
                        MoveAndStopDecreasedSpeedList.Remove(pc.PlayerId);
                        Main.AllPlayerSpeed[pc.PlayerId] = originalSpeed[pc.PlayerId];
                        originalSpeed.Remove(pc.PlayerId);
                        sync = true;
                    }
                    if (MoveAndStopIncreasedSpeedList.TryGetValue(pc.PlayerId, out var istime) && istime + MoveAndStop_ModifiedSpeedDuration.GetInt() < now)
                    {
                        Logger.Info(pc.GetRealName() + "'s increased speed expired", "MoveAndStop");
                        MoveAndStopIncreasedSpeedList.Remove(pc.PlayerId);
                        Main.AllPlayerSpeed[pc.PlayerId] = originalSpeed[pc.PlayerId];
                        originalSpeed.Remove(pc.PlayerId);
                        sync = true;
                    }
                    if (MoveAndStopLowerVisionList.TryGetValue(pc.PlayerId, out var lvtime) && lvtime + MoveAndStop_ModifiedSpeedDuration.GetInt() < now)
                    {
                        Logger.Info(pc.GetRealName() + "'s lower vision effect expired", "MoveAndStop");
                        MoveAndStopLowerVisionList.Remove(pc.PlayerId);
                        sync = true;
                    }
                    if (MoveAndStopShieldedList.TryGetValue(pc.PlayerId, out var stime) && stime + MoveAndStop_ShieldDuration.GetInt() < now)
                    {
                        Logger.Info(pc.GetRealName() + "'s shield expired", "MoveAndStop");
                        MoveAndStopShieldedList.Remove(pc.PlayerId);
                    }

                    if (sync) pc.MarkDirtySettings();
                }
            }
        }
    }
}