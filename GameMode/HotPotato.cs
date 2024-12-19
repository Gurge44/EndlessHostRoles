using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using EHR.Modules;
using HarmonyLib;
using UnityEngine;

namespace EHR;

internal static class HotPotato
{
    private static OptionItem Time;
    private static OptionItem HolderSpeed;
    private static OptionItem Range;

    private static (byte HolderID, byte LastHolderID, int TimeLeft, int RoundNum) HotPotatoState;
    private static Dictionary<byte, int> SurvivalTimes;
    private static float DefaultSpeed;

    public static (byte HolderID, byte LastHolderID) GetState()
    {
        return (HotPotatoState.HolderID, HotPotatoState.LastHolderID);
    }

    public static void SetupCustomOption()
    {
        Time = new IntegerOptionItem(69_213_001, "HotPotato_Time", new(1, 90, 1), 20, TabGroup.GameSettings)
            .SetHeader(true)
            .SetGameMode(CustomGameMode.HotPotato)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new Color32(232, 205, 70, byte.MaxValue));

        HolderSpeed = new FloatOptionItem(69_213_002, "HotPotato_HolderSpeed", new(0.1f, 5f, 0.1f), 2f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.HotPotato)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(new Color32(232, 205, 70, byte.MaxValue));

        Range = new FloatOptionItem(69_213_004, "HotPotato_Range", new(0.25f, 5f, 0.25f), 1f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.HotPotato)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(new Color32(232, 205, 70, byte.MaxValue));
    }

    public static void Init()
    {
        FixedUpdatePatch.Return = true;

        HotPotatoState = (byte.MaxValue, byte.MaxValue, Time.GetInt() + 60, 1);
        SurvivalTimes = [];
        foreach (PlayerControl pc in Main.AllPlayerControls) SurvivalTimes[pc.PlayerId] = 0;

        DefaultSpeed = Main.AllPlayerSpeed[0];
    }

    public static void OnGameStart()
    {
        LateTask.New(() => { FixedUpdatePatch.Return = false; }, 7f, log: false);
        HotPotatoState = (byte.MaxValue, byte.MaxValue, Time.GetInt() + 40, 1);
    }

    public static int GetSurvivalTime(byte id)
    {
        return SurvivalTimes.GetValueOrDefault(id, 1);
    }

    public static string GetIndicator(byte id)
    {
        return HotPotatoState.HolderID == id ? "  \u2668  " : string.Empty;
    }

    public static string GetSuffixText(byte id)
    {
        if (!Main.PlayerStates.TryGetValue(id, out var state) || state.IsDead) return string.Empty;
        return $"{(HotPotatoState.HolderID == id ? $"{Translator.GetString("HotPotato_HoldingNotify")}\n" : string.Empty)}{Translator.GetString("HotPotato_TimeLeftSuffix")}{HotPotatoState.TimeLeft}s";
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    private static class FixedUpdatePatch
    {
        private static float UpdateDelay;
        private static long LastFixedUpdate;
        public static bool Return;

        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        public static void Postfix(PlayerControl __instance)
        {
            if (!CustomGameMode.HotPotato.IsActiveOrIntegrated() || Return || !AmongUsClient.Instance.AmHost || !GameStates.IsInTask || __instance.PlayerId == 255) return;

            PlayerControl Holder = Utils.GetPlayerById(HotPotatoState.HolderID);

            if (Holder == null || Holder.Data.Disconnected || !Holder.IsAlive())
            {
                PassHotPotato();
                return;
            }

            long now = Utils.TimeStamp;

            if (now > LastFixedUpdate)
            {
                HotPotatoState.TimeLeft--;
                LastFixedUpdate = now;
                Utils.NotifyRoles();
            }

            if (HotPotatoState.TimeLeft <= 0)
            {
                Holder.Suicide();
                SurvivalTimes[HotPotatoState.HolderID] = Time.GetInt() * (HotPotatoState.RoundNum - 1);
                PassHotPotato();

                if (Holder.IsLocalPlayer())
                    Achievements.Type.OutOfTime.Complete();

                return;
            }

            PlayerControl[] aapc = Main.AllAlivePlayerControls;
            if (HotPotatoState.HolderID != __instance.PlayerId || !aapc.Any(x => x.PlayerId != HotPotatoState.HolderID && (x.PlayerId != HotPotatoState.LastHolderID || aapc.Length == 2) && Vector2.Distance(x.Pos(), Holder.Pos()) <= Range.GetFloat())) return;

            float wait = aapc.Length <= 2 ? 0.4f : 0.05f;
            UpdateDelay += UnityEngine.Time.fixedDeltaTime;
            if (UpdateDelay < wait) return;

            UpdateDelay = 0;

            PlayerControl Target = aapc.OrderBy(x => Vector2.Distance(x.Pos(), Holder.Pos())).FirstOrDefault(x => x.PlayerId != HotPotatoState.HolderID && (x.PlayerId != HotPotatoState.LastHolderID || aapc.Length == 2));
            if (Target == null) return;

            PassHotPotato(Target, false);
        }

        private static void PassHotPotato(PlayerControl target = null, bool resetTime = true)
        {
            if (Return || Main.AllAlivePlayerControls.Length < 2) return;

            if (resetTime)
            {
                int time = Time.GetInt();
                if (Options.CurrentGameMode == CustomGameMode.AllInOne) time *= AllInOneGameMode.HotPotatoTimerMultiplier.GetInt();

                HotPotatoState.TimeLeft = time;
                HotPotatoState.RoundNum++;
            }

            try
            {
                target ??= Main.AllAlivePlayerControls.RandomElement();

                HotPotatoState.LastHolderID = HotPotatoState.HolderID;
                HotPotatoState.HolderID = target.PlayerId;

                Main.AllPlayerSpeed[target.PlayerId] = HolderSpeed.GetFloat();
                target.MarkDirtySettings();

                PlayerControl LastHolder = Utils.GetPlayerById(HotPotatoState.LastHolderID);

                if (LastHolder != null)
                {
                    Main.AllPlayerSpeed[HotPotatoState.LastHolderID] = DefaultSpeed;
                    LastHolder.MarkDirtySettings();
                    Utils.NotifyRoles(SpecifyTarget: LastHolder);

                    Logger.Info($"Hot Potato Passed: {LastHolder.GetRealName()} => {target.GetRealName()}", "HotPotato");
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "HotPotatoManager.FixedUpdatePatch.PassHotPotato");
            }
            finally
            {
                Utils.NotifyRoles(SpecifyTarget: target);
            }
        }
    }
}