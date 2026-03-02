using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Gamemodes;

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

        HolderCanPassViaKillButton = new BooleanOptionItem(69_213_003, "HotPotato_HolderCanPassViaKillButton", true, TabGroup.GameSettings)
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
        foreach (PlayerControl pc in Main.EnumeratePlayerControls()) SurvivalTimes[pc.PlayerId] = 0;

        DefaultSpeed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
    }

    public static void OnGameStart()
    {
        HotPotatoState = (byte.MaxValue, byte.MaxValue, GetKillInterval(), 1);
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

    public static string GetSuffixText(byte id, bool hud)
    {
        if (!Main.PlayerStates.TryGetValue(id, out PlayerState state) || (id.IsPlayerModdedClient() && !hud) || state.IsDead) return string.Empty;
        string holding = HotPotatoState.HolderID == id ? $"{Translator.GetString("HotPotato_HoldingNotify")}\n" : string.Empty;
        string arrows = TargetArrow.GetAllArrows(id);
        arrows = arrows.Length > 0 ? $"\n{arrows}" : string.Empty;
        return $"{holding}{Translator.GetString("HotPotato_TimeLeftSuffix")}{HotPotatoState.TimeLeft}s{arrows}";
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                int timeLeft = reader.ReadPackedInt32();
                HotPotatoState.TimeLeft = timeLeft;
                break;
            case 2:
                HotPotatoState.HolderID = reader.ReadByte();
                HotPotatoState.LastHolderID = reader.ReadByte();
                break;
        }
    }

    public static int GetKillInterval()
    {
        return Time.GetInt() + Main.CurrentMap switch
        {
            MapNames.Airship => ExtraTimeOnAirship.GetInt(),
            MapNames.Fungle => ExtraTimeOnFungle.GetInt(),
            _ => 0
        };
    }

    //[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class FixedUpdatePatch
    {
        private static long LastFixedUpdate;

        public static void Postfix(PlayerControl __instance)
        {
            if (Options.CurrentGameMode != CustomGameMode.HotPotato || !Main.IntroDestroyed || !AmongUsClient.Instance.AmHost || !GameStates.IsInTask || ExileController.Instance || __instance.PlayerId >= 254 || IntroCutsceneDestroyPatch.IntroDestroyTS + 5 > Utils.TimeStamp) return;

            PlayerControl holder = Utils.GetPlayerById(HotPotatoState.HolderID);

            if (!holder || holder.Data.Disconnected || !holder.IsAlive())
            {
                PassHotPotato();
                return;
            }

            long now = Utils.TimeStamp;

            if (now > LastFixedUpdate)
            {
                HotPotatoState.TimeLeft--;
                Utils.SendRPC(CustomRPC.HotPotatoSync, 1, HotPotatoState.TimeLeft);
                LastFixedUpdate = now;
                Utils.NotifyRoles(SendOption: SendOption.None);
            }

            if (HotPotatoState.TimeLeft <= 0)
            {
                holder.Suicide();
                SurvivalTimes[HotPotatoState.HolderID] = (HotPotatoState.RoundNum - 1) * GetKillInterval();
                PassHotPotato();

                if (holder.AmOwner)
                    Achievements.Type.OutOfTime.Complete();

                return;
            }

            if (CanPassViaKillButton || LastPassTS == now) return;
            if (HotPotatoState.HolderID != __instance.PlayerId) return;

            Dictionary<byte, Vector2> alivePlayerPositions = Main.EnumerateAlivePlayerControls().ToDictionary(x => x.PlayerId, x => x.Pos());
            bool allowLastHolder = alivePlayerPositions.Count == 2;
            Vector2 holderPos = alivePlayerPositions[HotPotatoState.HolderID];
            float range = Range.GetFloat();

            if (!FastVector2.TryGetClosestInRange(holderPos, alivePlayerPositions, range, out byte passToId)) return;
            if (!allowLastHolder && passToId == HotPotatoState.LastHolderID) return;
            PassHotPotato(passToId.GetPlayer(), false);
        }

        public static void PassHotPotato(PlayerControl target = null, bool resetTime = true)
        {
            var aapc = Main.AllAlivePlayerControls;

            if (!Main.IntroDestroyed || aapc.Count < 2) return;

            if (resetTime)
            {
                HotPotatoState.TimeLeft = GetKillInterval();
                HotPotatoState.RoundNum++;
            }

            try
            {
                target ??= aapc.RandomElement();

                HotPotatoState.LastHolderID = HotPotatoState.HolderID;
                HotPotatoState.HolderID = target.PlayerId;

                Utils.SendRPC(CustomRPC.HotPotatoSync, 2, HotPotatoState.HolderID, HotPotatoState.LastHolderID);

                if (CanPassViaKillButton)
                {
                    target.RpcSetRoleDesync(RoleTypes.Impostor, target.OwnerId);
                    LateTask.New(() => target.SetKillCooldown(1f), 0.2f, log: false);
                }

                if (aapc.Count < HolderHasArrowToNearestPlayerIfPlayersLessThan.GetInt() && aapc.Count > 1)
                {
                    Vector2 pos = target.Pos();
                    TargetArrow.Add(HotPotatoState.HolderID, aapc.Without(target).Where(x => x.PlayerId != HotPotatoState.LastHolderID).MinBy(x => Vector2.Distance(x.Pos(), pos)).PlayerId);
                }

                Main.AllPlayerSpeed[target.PlayerId] = HolderSpeed.GetFloat();
                target.MarkDirtySettings();

                PlayerControl lastHolder = Utils.GetPlayerById(HotPotatoState.LastHolderID);

                if (lastHolder)
                {
                    if (CanPassViaKillButton && lastHolder.IsAlive()) lastHolder.RpcSetRoleGlobal(RoleTypes.Crewmate);

                    TargetArrow.RemoveAllTarget(HotPotatoState.LastHolderID);

                    Main.AllPlayerSpeed[HotPotatoState.LastHolderID] = DefaultSpeed;
                    PlayerGameOptionsSender.SetDirty(HotPotatoState.LastHolderID);
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
