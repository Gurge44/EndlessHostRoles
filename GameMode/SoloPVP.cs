using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.RandomSpawn;

namespace EHR;

internal static class SoloPVP
{
    private static Dictionary<byte, float> PlayerHPMax = [];
    private static Dictionary<byte, float> PlayerHP = [];
    public static Dictionary<byte, float> PlayerHPReco = [];
    public static Dictionary<byte, float> PlayerATK = [];
    public static Dictionary<byte, float> PlayerDF = [];

    public static Dictionary<byte, int> PlayerScore = [];
    public static int RoundTime;

    private static readonly Dictionary<byte, (string Text, long RemoveTimeStamp)> NameNotify = [];

    private static Dictionary<byte, int> BackCountdown = [];
    private static Dictionary<byte, long> LastHurt = [];
    private static Dictionary<byte, long> LastCountdownTime = [];

    public static bool CanVent => SoloPVP_CanVent.GetBool();

    public static bool SoloAlive(this PlayerControl pc)
    {
        return PlayerHP.TryGetValue(pc.PlayerId, out float hp) && hp > 0f;
    }

    public static void SetupCustomOption()
    {
        SoloPVP_GameTime = new IntegerOptionItem(66_233_001, "SoloPVP_GameTime", new(30, 300, 5), 180, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloPVP)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds)
            .SetHeader(true);

        SoloPVP_ATKCooldown = new FloatOptionItem(66_223_008, "SoloPVP_ATKCooldown", new(1f, 10f, 0.1f), 1f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloPVP)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);

        SoloPVP_HPMax = new FloatOptionItem(66_233_002, "SoloPVP_HPMax", new(10f, 990f, 5f), 100f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloPVP)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Health);

        SoloPVP_ATK = new FloatOptionItem(66_233_003, "SoloPVP_ATK", new(1f, 100f, 1f), 8f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloPVP)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Health);

        SoloPVP_RecoverPerSecond = new FloatOptionItem(66_233_005, "SoloPVP_RecoverPerSecond", new(1f, 180f, 1f), 2f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloPVP)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Health);

        SoloPVP_RecoverAfterSecond = new IntegerOptionItem(66_233_004, "SoloPVP_RecoverAfterSecond", new(0, 60, 1), 8, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloPVP)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);

        SoloPVP_ResurrectionWaitingTime = new IntegerOptionItem(66_233_006, "SoloPVP_ResurrectionWaitingTime", new(3, 990, 1), 15, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloPVP)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);

        SoloPVP_KillBonusMultiplier = new FloatOptionItem(66_233_007, "SoloPVP_KillBonusMultiplier", new(0.25f, 5f, 0.25f), 1.25f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloPVP)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Multiplier);

        SoloPVP_CanVent = new BooleanOptionItem(66_233_009, "SoloPVP_CanVent", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloPVP)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue));
    }

    public static void Init()
    {
        if (Options.CurrentGameMode != CustomGameMode.SoloPVP) return;

        PlayerHPMax = [];
        PlayerHP = [];
        PlayerHPReco = [];
        PlayerATK = [];
        PlayerDF = [];

        LastHurt = [];
        LastCountdownTime = [];
        BackCountdown = [];
        PlayerScore = [];
        RoundTime = SoloPVP_GameTime.GetInt() + 8;
        Utils.SendRPC(CustomRPC.SoloPVPSync, 1, RoundTime);

        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            PlayerHPMax.TryAdd(pc.PlayerId, SoloPVP_HPMax.GetFloat());
            PlayerHP.TryAdd(pc.PlayerId, SoloPVP_HPMax.GetFloat());
            PlayerHPReco.TryAdd(pc.PlayerId, SoloPVP_RecoverPerSecond.GetFloat());
            PlayerATK.TryAdd(pc.PlayerId, SoloPVP_ATK.GetFloat());
            PlayerDF.TryAdd(pc.PlayerId, 0f);

            PlayerScore.TryAdd(pc.PlayerId, 0);
            Utils.SendRPC(CustomRPC.SoloPVPSync, 2, pc.PlayerId, 0);

            LastHurt.TryAdd(pc.PlayerId, Utils.TimeStamp);
            LastCountdownTime.TryAdd(pc.PlayerId, Utils.TimeStamp);
        }
    }

    public static string GetDisplayHealth(PlayerControl pc, bool self)
    {
        return (pc.SoloAlive() ? Utils.ColorString(GetHealthColor(pc), $"{(int)PlayerHP[pc.PlayerId]}/{(int)PlayerHPMax[pc.PlayerId]}") : string.Empty) + (self ? GetStatsForVanilla(pc) : string.Empty);
    }

    private static Color32 GetHealthColor(PlayerControl pc)
    {
        var x = (int)(PlayerHP[pc.PlayerId] / PlayerHPMax[pc.PlayerId] * 10 * 50);
        var r = 255;
        var g = 255;
        var b = 0;

        if (x > 255)
            r -= x - 255;
        else
            g = x;

        return new((byte)r, (byte)g, (byte)b, byte.MaxValue);
    }

    private static string GetStatsForVanilla(PlayerControl pc)
    {
        var finalText = string.Empty;
        if (pc.IsModdedClient()) return finalText;

        finalText += "\n<size=90%>";
        finalText += GetHudText();
        finalText += "</size>\n";

        finalText += "<size=70%>";
        finalText += $"\n{Translator.GetString("PVP.ATK")}: {PlayerATK[pc.PlayerId]:N1}";
        finalText += $" | {Translator.GetString("PVP.DF")}: {PlayerDF[pc.PlayerId]:N1}";
        finalText += $" | {Translator.GetString("PVP.RCO")}: {PlayerHPReco[pc.PlayerId]:N1}";
        finalText += "</size>";

        int rank = GetRankFromScore(pc.PlayerId);
        finalText += "<size=80%>";

        if (rank != 1)
        {
            byte first = Main.PlayerStates.Keys.MinBy(GetRankFromScore);
            finalText += $"\n1. {first.ColoredPlayerName()} - {string.Format(Translator.GetString("KillCount").TrimStart(' '), PlayerScore.GetValueOrDefault(first, 0))}";
        }

        finalText += $"\n{rank}. {pc.PlayerId.ColoredPlayerName()} - {string.Format(Translator.GetString("KillCount").TrimStart(' '), PlayerScore.GetValueOrDefault(pc.PlayerId, 0))}";
        return $"<#ffffff>{finalText}</color>";
    }

    public static string GetSummaryStatistics(byte id)
    {
        int rank = GetRankFromScore(id);
        int score = PlayerScore.GetValueOrDefault(id, 0);
        return string.Format(Translator.GetString("SoloPVP.Summary"), rank, score);
    }

    public static void GetNameNotify(PlayerControl player, ref string name)
    {
        if (Options.CurrentGameMode != CustomGameMode.SoloPVP || player == null) return;

        if (BackCountdown.TryGetValue(player.PlayerId, out int value))
        {
            name = string.Format(Translator.GetString("SoloPVP_BackCountDown"), value);
            NameNotify.Remove(player.PlayerId);
            return;
        }

        if (NameNotify.TryGetValue(player.PlayerId, out (string Text, long RemoveTimeStamp) value1))
            name = value1.Text;
    }

    public static int GetRankFromScore(byte playerId)
    {
        try
        {
            int ms = PlayerScore[playerId];
            int rank = 1 + PlayerScore.Values.Count(x => x > ms);
            rank += PlayerScore.Where(x => x.Value == ms).Select(x => x.Key).ToList().IndexOf(playerId);
            return rank;
        }
        catch { return Main.AllPlayerControls.Length; }
    }

    public static string GetHudText()
    {
        if (RoundTime == 60)
        {
            SoundManager.Instance.PlaySound(HudManager.Instance.LobbyTimerExtensionUI.lobbyTimerPopUpSound, false);
            Utils.FlashColor(new(1f, 1f, 0f, 0.4f), 1.4f);
        }
        return $"{RoundTime / 60:00}:{RoundTime % 60:00}";
    }

    public static void OnPlayerAttack(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null || Options.CurrentGameMode != CustomGameMode.SoloPVP || !Main.IntroDestroyed) return;

        if (!killer.SoloAlive() || !target.SoloAlive() || target.inVent || target.MyPhysics.Animations.IsPlayingEnterVentAnimation()) return;

        float dmg = PlayerATK[killer.PlayerId] - PlayerDF[target.PlayerId];
        PlayerHP[target.PlayerId] = Math.Max(0f, PlayerHP[target.PlayerId] - dmg);

        if (!target.SoloAlive())
        {
            Main.Instance.StartCoroutine(OnPlayerDead(target));
            OnPlayerKill(killer);
        }

        LastHurt[target.PlayerId] = Utils.TimeStamp;

        float kcd = SoloPVP_ATKCooldown.GetFloat();
        if (killer.IsHost()) kcd += Math.Max(0.5f, Utils.CalculatePingDelay());
        killer.SetKillCooldown(kcd, target);

        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
        RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);

        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);
    }

    private static void OnPlayerBack(PlayerControl pc)
    {
        BackCountdown.Remove(pc.PlayerId);
        PlayerHP[pc.PlayerId] = PlayerHPMax[pc.PlayerId];
        LastHurt[pc.PlayerId] = Utils.TimeStamp;
        pc.ReviveFromTemporaryExile();
        RPC.PlaySoundRPC(pc.PlayerId, Sounds.SpawnSound);
        SpawnMap.GetSpawnMap().RandomTeleport(pc);
        Utils.NotifyRoles(SpecifyTarget: pc, SendOption: SendOption.None);
    }

    private static System.Collections.IEnumerator OnPlayerDead(PlayerControl target)
    {
        BackCountdown.TryAdd(target.PlayerId, SoloPVP_ResurrectionWaitingTime.GetInt());
        if (target.inVent || target.MyPhysics.Animations.IsPlayingEnterVentAnimation()) LateTask.New(() => target.MyPhysics.RpcExitVent(target.GetClosestVent().Id), 0.6f, log: false);
        while (target.inVent || target.inMovingPlat || target.onLadder || target.MyPhysics.Animations.IsPlayingAnyLadderAnimation() || target.MyPhysics.Animations.IsPlayingEnterVentAnimation()) yield return null;
        if (BackCountdown.ContainsKey(target.PlayerId)) target.ExileTemporarily();
    }

    private static void OnPlayerKill(PlayerControl killer)
    {
        killer.KillFlash();
        if (Main.GM.Value && AmongUsClient.Instance.AmHost) PlayerControl.LocalPlayer.KillFlash();
        ChatCommands.Spectators.ToValidPlayers().Do(x => x.KillFlash());

        PlayerScore[killer.PlayerId]++;
        Utils.SendRPC(CustomRPC.SoloPVPSync, 2, killer.PlayerId, PlayerScore[killer.PlayerId]);

        float addRate = IRandom.Instance.Next(3, 5 + GetRankFromScore(killer.PlayerId)) / 100f;
        addRate *= SoloPVP_KillBonusMultiplier.GetFloat();
        if (killer.IsHost()) addRate /= 2f;

        var text = string.Empty;
        float addin;

        switch (IRandom.Instance.Next(0, 4))
        {
            case 0:
                addin = PlayerHPMax[killer.PlayerId] * addRate;
                PlayerHPMax[killer.PlayerId] += addin;
                text = string.Format(Translator.GetString("SoloPVP_Buff_HPMax"), addin.ToString("0.0#####"));
                break;
            case 1:
                addin = PlayerHPReco[killer.PlayerId] * addRate * 2;
                PlayerHPReco[killer.PlayerId] += addin;
                text = string.Format(Translator.GetString("SoloPVP_Buff_HPReco"), addin.ToString("0.0#####"));
                break;
            case 2:
                addin = PlayerATK[killer.PlayerId] * addRate;
                PlayerATK[killer.PlayerId] += addin;
                text = string.Format(Translator.GetString("SoloPVP_Buff_ATK"), addin.ToString("0.0#####"));
                break;
            case 3:
                addin = Math.Max(PlayerDF[killer.PlayerId], 1f) * addRate * 8;
                PlayerDF[killer.PlayerId] += addin;
                text = string.Format(Translator.GetString("SoloPVP_Buff_DF"), addin.ToString("0.0#####"));
                break;
        }

        NameNotify[killer.PlayerId] = (text, Utils.TimeStamp + 5);
    }

    //[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class FixedUpdatePatch
    {
        private static long LastFixedUpdate;

        public static void Postfix(PlayerControl __instance)
        {
            byte id = __instance.PlayerId;
            if (!GameStates.IsInTask || ExileController.Instance || !Main.IntroDestroyed || Options.CurrentGameMode != CustomGameMode.SoloPVP || !AmongUsClient.Instance.AmHost || id >= 254) return;

            long now = Utils.TimeStamp;
            if (LastCountdownTime[id] == now) return;
            LastCountdownTime[id] = now;

            if (LastHurt[id] + SoloPVP_RecoverAfterSecond.GetInt() < now && PlayerHP[id] < PlayerHPMax[id] && __instance.SoloAlive() && !__instance.inVent)
            {
                PlayerHP[id] += PlayerHPReco[id];
                PlayerHP[id] = Math.Min(PlayerHPMax[id], PlayerHP[id]);
            }

            if (BackCountdown.ContainsKey(id))
            {
                BackCountdown[id]--;
                if (BackCountdown[id] <= 0) OnPlayerBack(__instance);
            }

            if (NameNotify.TryGetValue(id, out (string Text, long RemoveTimeStamp) nameNotify) && nameNotify.RemoveTimeStamp < now)
                NameNotify.Remove(id);


            if (LastFixedUpdate == now) return;
            LastFixedUpdate = now;

            RoundTime--;
            Utils.SendRPC(CustomRPC.SoloPVPSync, 1, RoundTime);

            Utils.NotifyRoles(SendOption: SendOption.None);
        }
    }

    // Options
    // ReSharper disable InconsistentNaming
    public static OptionItem SoloPVP_GameTime;
    public static OptionItem SoloPVP_ATKCooldown;
    private static OptionItem SoloPVP_HPMax;
    private static OptionItem SoloPVP_ATK;
    private static OptionItem SoloPVP_RecoverAfterSecond;
    private static OptionItem SoloPVP_RecoverPerSecond;
    private static OptionItem SoloPVP_ResurrectionWaitingTime;
    private static OptionItem SoloPVP_KillBonusMultiplier;
    private static OptionItem SoloPVP_CanVent;

    // ReSharper restore InconsistentNaming
}
