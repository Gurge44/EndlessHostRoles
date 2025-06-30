using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR;

internal static class HotPotato
{
    private static OptionItem Time;
    private static OptionItem HolderSpeed;
    private static OptionItem Range;
    private static OptionItem HolderCanPassViaKillButton;
    private static OptionItem ExtraTimeOnAirship;
    private static OptionItem ExtraTimeOnFungle;
    private static OptionItem HolderHasArrowToNearestPlayerIfPlayersLessThan;

    private static (byte HolderID, byte LastHolderID, int TimeLeft, int RoundNum) HotPotatoState;
    private static Dictionary<byte, int> SurvivalTimes;
    private static float DefaultSpeed;
    private static long LastPassTS;

    public static bool CanPassViaKillButton => HolderCanPassViaKillButton.GetBool();

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

        HolderCanPassViaKillButton = new BooleanOptionItem(69_213_003, "HotPotato_HolderCanPassViaKillButton", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.HotPotato)
            .SetColor(new Color32(232, 205, 70, byte.MaxValue))
            .SetHeader(true);

        ExtraTimeOnAirship = new IntegerOptionItem(69_213_005, "HotPotato_ExtraTimeOnAirship", new(0, 60, 1), 15, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.HotPotato)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new Color32(232, 205, 70, byte.MaxValue));

        ExtraTimeOnFungle = new IntegerOptionItem(69_213_006, "HotPotato_ExtraTimeOnFungle", new(0, 60, 1), 10, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.HotPotato)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new Color32(232, 205, 70, byte.MaxValue));

        HolderHasArrowToNearestPlayerIfPlayersLessThan = new IntegerOptionItem(69_213_007, "HotPotato_HolderHasArrowToNearestPlayerIfPlayersLessThan", new(2, 15, 1), 5, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.HotPotato)
            .SetValueFormat(OptionFormat.Players)
            .SetColor(new Color32(232, 205, 70, byte.MaxValue));
    }

    public static void Init()
    {
        HotPotatoState = (byte.MaxValue, byte.MaxValue, Time.GetInt() + 8, 1);
        SurvivalTimes = [];
        foreach (PlayerControl pc in Main.AllPlayerControls) SurvivalTimes[pc.PlayerId] = 0;

        DefaultSpeed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
    }

    public static void OnGameStart()
    {
        int time = Time.GetInt();
        time += Main.CurrentMap switch
        {
            MapNames.Airship => ExtraTimeOnAirship.GetInt(),
            MapNames.Fungle => ExtraTimeOnFungle.GetInt(),
            _ => 0
        };
        HotPotatoState = (byte.MaxValue, byte.MaxValue, time, 1);
        LastPassTS = Utils.TimeStamp;
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
        if (!Main.PlayerStates.TryGetValue(id, out PlayerState state) || state.IsDead) return string.Empty;
        string holding = HotPotatoState.HolderID == id ? $"{Translator.GetString("HotPotato_HoldingNotify")}\n" : string.Empty;
        string arrows = TargetArrow.GetAllArrows(id);
        arrows = arrows.Length > 0 ? $"\n{arrows}" : string.Empty;
        return $"{holding}{Translator.GetString("HotPotato_TimeLeftSuffix")}{HotPotatoState.TimeLeft}s{arrows}";
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        HotPotatoState.HolderID = reader.ReadByte();
        HotPotatoState.LastHolderID = reader.ReadByte();
    }

    //[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class FixedUpdatePatch
    {
        private static float UpdateDelay;
        private static long LastFixedUpdate;

        public static void Postfix(PlayerControl __instance)
        {
            if (Options.CurrentGameMode != CustomGameMode.HotPotato || !Main.IntroDestroyed || !AmongUsClient.Instance.AmHost || !GameStates.IsInTask || ExileController.Instance || __instance.PlayerId >= 254 || Utils.GameStartTimeStamp + 15 > Utils.TimeStamp) return;

            PlayerControl holder = Utils.GetPlayerById(HotPotatoState.HolderID);

            if (holder == null || holder.Data.Disconnected || !holder.IsAlive())
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
                holder.Suicide();
                SurvivalTimes[HotPotatoState.HolderID] = Time.GetInt() * (HotPotatoState.RoundNum - 1);
                PassHotPotato();

                if (holder.IsLocalPlayer())
                    Achievements.Type.OutOfTime.Complete();

                return;
            }

            if (Utils.GameStartTimeStamp + 30 > now && LastPassTS == now) return;

            PlayerControl[] aapc = Main.AllAlivePlayerControls;
            Vector2 pos = holder.Pos();
            if (HotPotatoState.HolderID != __instance.PlayerId || !aapc.Any(x => x.PlayerId != HotPotatoState.HolderID && (x.PlayerId != HotPotatoState.LastHolderID || aapc.Length == 2) && Vector2.Distance(x.Pos(), pos) <= Range.GetFloat())) return;

            float wait = aapc.Length <= 2 ? 0.4f : 0f;
            UpdateDelay += UnityEngine.Time.fixedDeltaTime;
            if (UpdateDelay < wait) return;

            UpdateDelay = 0;

            PlayerControl target = aapc.OrderBy(x => Vector2.Distance(x.Pos(), pos)).FirstOrDefault(x => x.PlayerId != HotPotatoState.HolderID && (x.PlayerId != HotPotatoState.LastHolderID || aapc.Length == 2));
            if (target == null) return;

            PassHotPotato(target, false);
        }

        public static void PassHotPotato(PlayerControl target = null, bool resetTime = true)
        {
            PlayerControl[] aapc = Main.AllAlivePlayerControls;

            if (!Main.IntroDestroyed || aapc.Length < 2) return;

            if (resetTime)
            {
                int time = Time.GetInt();
                time += Main.CurrentMap switch
                {
                    MapNames.Airship => ExtraTimeOnAirship.GetInt(),
                    MapNames.Fungle => ExtraTimeOnFungle.GetInt(),
                    _ => 0
                };
                HotPotatoState.TimeLeft = time;
                HotPotatoState.RoundNum++;
            }

            try
            {
                target ??= aapc.RandomElement();

                HotPotatoState.LastHolderID = HotPotatoState.HolderID;
                HotPotatoState.HolderID = target.PlayerId;

                Utils.SendRPC(CustomRPC.HotPotatoSync, HotPotatoState.HolderID, HotPotatoState.LastHolderID);

                if (CanPassViaKillButton)
                {
                    target.RpcChangeRoleBasis(CustomRoles.NSerialKiller);
                    LateTask.New(() => target.SetKillCooldown(1f), 0.2f, log: false);
                }

                if (aapc.Length < HolderHasArrowToNearestPlayerIfPlayersLessThan.GetInt() && aapc.Length > 1)
                {
                    Vector2 pos = target.Pos();
                    TargetArrow.Add(HotPotatoState.HolderID, aapc.Without(target).Where(x => x.PlayerId != HotPotatoState.LastHolderID).MinBy(x => Vector2.Distance(x.Pos(), pos)).PlayerId);
                }

                Main.AllPlayerSpeed[target.PlayerId] = HolderSpeed.GetFloat();
                target.MarkDirtySettings();

                PlayerControl lastHolder = Utils.GetPlayerById(HotPotatoState.LastHolderID);

                if (lastHolder != null)
                {
                    if (CanPassViaKillButton) lastHolder.RpcChangeRoleBasis(CustomRoles.Potato);

                    TargetArrow.RemoveAllTarget(HotPotatoState.LastHolderID);

                    Main.AllPlayerSpeed[HotPotatoState.LastHolderID] = DefaultSpeed;
                    lastHolder.MarkDirtySettings();
                    Utils.NotifyRoles(SpecifyTarget: lastHolder);

                    Logger.Info($"Hot Potato Passed: {lastHolder.GetRealName()} => {target.GetRealName()}", "HotPotato");
                }
            }
            catch (Exception ex) { Logger.Exception(ex, "HotPotatoManager.FixedUpdatePatch.PassHotPotato"); }
            finally { Utils.NotifyRoles(SpecifyTarget: target); }

            LastPassTS = Utils.TimeStamp;
        }
    }
}