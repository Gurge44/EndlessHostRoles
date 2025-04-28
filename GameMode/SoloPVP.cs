﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Neutral;
using HarmonyLib;
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

    public static Dictionary<byte, int> KBScore = [];
    public static int RoundTime;

    private static readonly Dictionary<byte, (string Text, long TimeStamp)> NameNotify = [];

    private static Dictionary<byte, int> BackCountdown = [];
    private static Dictionary<byte, long> LastHurt = [];
    private static Dictionary<byte, long> LastCountdownTime = [];

    public static bool SoloAlive(this PlayerControl pc)
    {
        return PlayerHP.TryGetValue(pc.PlayerId, out float hp) && hp > 0f;
    }

    public static void SetupCustomOption()
    {
        KB_GameTime = new IntegerOptionItem(66_233_001, "KB_GameTime", new(30, 300, 5), 180, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds)
            .SetHeader(true);

        KB_ATKCooldown = new FloatOptionItem(66_223_008, "KB_ATKCooldown", new(1f, 10f, 0.1f), 1f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);

        KB_HPMax = new FloatOptionItem(66_233_002, "KB_HPMax", new(10f, 990f, 5f), 100f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Health);

        KB_ATK = new FloatOptionItem(66_233_003, "KB_ATK", new(1f, 100f, 1f), 8f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Health);

        KB_RecoverPerSecond = new FloatOptionItem(66_233_005, "KB_RecoverPerSecond", new(1f, 180f, 1f), 2f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Health);

        KB_RecoverAfterSecond = new IntegerOptionItem(66_233_004, "KB_RecoverAfterSecond", new(0, 60, 1), 8, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);

        KB_ResurrectionWaitingTime = new IntegerOptionItem(66_233_006, "KB_ResurrectionWaitingTime", new(3, 990, 1), 15, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);

        KB_KillBonusMultiplier = new FloatOptionItem(66_233_007, "KB_KillBonusMultiplier", new(0.25f, 5f, 0.25f), 1.25f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Multiplier);
    }

    public static void Init()
    {
        if (!CustomGameMode.SoloKombat.IsActiveOrIntegrated()) return;

        PlayerHPMax = [];
        PlayerHP = [];
        PlayerHPReco = [];
        PlayerATK = [];
        PlayerDF = [];

        LastHurt = [];
        LastCountdownTime = [];
        BackCountdown = [];
        KBScore = [];
        RoundTime = KB_GameTime.GetInt() + 8;

        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            PlayerHPMax.TryAdd(pc.PlayerId, KB_HPMax.GetFloat());
            PlayerHP.TryAdd(pc.PlayerId, KB_HPMax.GetFloat());
            PlayerHPReco.TryAdd(pc.PlayerId, KB_RecoverPerSecond.GetFloat());
            PlayerATK.TryAdd(pc.PlayerId, KB_ATK.GetFloat());
            PlayerDF.TryAdd(pc.PlayerId, 0f);

            KBScore.TryAdd(pc.PlayerId, 0);

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
        if (pc.IsHost()) return finalText;

        finalText += "\n<size=90%>";
        finalText += GetHudText();
        finalText += "</size>\n";

        finalText += "<size=70%>";
        finalText += $"\n{Translator.GetString("PVP.ATK")}: {PlayerATK[pc.PlayerId]:N1}";
        finalText += $"\n{Translator.GetString("PVP.DF")}: {PlayerDF[pc.PlayerId]:N1}";
        finalText += $"\n{Translator.GetString("PVP.RCO")}: {PlayerHPReco[pc.PlayerId]:N1}";
        finalText += "</size>";

        int rank = GetRankFromScore(pc.PlayerId);
        finalText += "<size=80%>";

        if (rank != 1)
        {
            byte first = Main.PlayerStates.Keys.MinBy(GetRankFromScore);
            finalText += $"\n1. {first.ColoredPlayerName()} - {string.Format(Translator.GetString("KillCount").TrimStart(' '), KBScore.GetValueOrDefault(first, 0))}";
        }

        finalText += $"\n{rank}. {pc.PlayerId.ColoredPlayerName()} - {string.Format(Translator.GetString("KillCount").TrimStart(' '), KBScore.GetValueOrDefault(pc.PlayerId, 0))}";
        return $"<#ffffff>{finalText}</color>";
    }

    public static string GetSummaryStatistics(byte id)
    {
        int rank = GetRankFromScore(id);
        int score = KBScore.GetValueOrDefault(id, 0);
        return string.Format(Translator.GetString("SoloPVP.Summary"), rank, score);
    }

    public static void GetNameNotify(PlayerControl player, ref string name)
    {
        if (!CustomGameMode.SoloKombat.IsActiveOrIntegrated() || player == null) return;

        if (BackCountdown.TryGetValue(player.PlayerId, out int value))
        {
            name = string.Format(Translator.GetString("KBBackCountDown"), value);
            NameNotify.Remove(player.PlayerId);
            return;
        }

        if (NameNotify.TryGetValue(player.PlayerId, out (string Text, long TimeStamp) value1)) name = value1.Text;
    }

    public static int GetRankFromScore(byte playerId)
    {
        try
        {
            int ms = KBScore[playerId];
            int rank = 1 + KBScore.Values.Count(x => x > ms);
            rank += KBScore.Where(x => x.Value == ms).Select(x => x.Key).ToList().IndexOf(playerId);
            return rank;
        }
        catch { return Main.AllPlayerControls.Length; }
    }

    public static string GetHudText()
    {
        return $"{RoundTime / 60:00}:{RoundTime % 60:00}";
    }

    public static void OnPlayerAttack(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null || !CustomGameMode.SoloKombat.IsActiveOrIntegrated() || !Main.IntroDestroyed) return;

        if (!killer.SoloAlive() || !target.SoloAlive() || target.inVent || target.MyPhysics.Animations.IsPlayingEnterVentAnimation()) return;

        float dmg = PlayerATK[killer.PlayerId] - PlayerDF[target.PlayerId];
        PlayerHP[target.PlayerId] = Math.Max(0f, PlayerHP[target.PlayerId] - dmg);

        if (!target.SoloAlive())
        {
            OnPlayerDead(target);
            OnPlayerKill(killer);
        }

        LastHurt[target.PlayerId] = Utils.TimeStamp;

        float kcd = KB_ATKCooldown.GetFloat();
        if (killer.IsHost()) kcd += Math.Max(0.5f, Utils.CalculatePingDelay());
        killer.SetKillCooldown(kcd, target);

        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
        RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
        if (!target.IsModdedClient() && !target.AmOwner) target.SetKillCooldown(0.01f);

        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);
    }

    private static void OnPlayerBack(PlayerControl pc)
    {
        BackCountdown.Remove(pc.PlayerId);
        PlayerHP[pc.PlayerId] = PlayerHPMax[pc.PlayerId];

        LastHurt[pc.PlayerId] = Utils.TimeStamp;
        Main.AllPlayerSpeed[pc.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
        pc.MarkDirtySettings();

        RPC.PlaySoundRPC(pc.PlayerId, Sounds.TaskComplete);
        pc.RpcGuardAndKill();

        if (pc.inVent) pc.MyPhysics.RpcBootFromVent(ShipStatus.Instance.AllVents.RandomElement().Id);
        else SpawnMap.GetSpawnMap().RandomTeleport(pc);
    }

    private static void OnPlayerDead(PlayerControl target)
    {
        target.MyPhysics.RpcCancelPet();
        PetsHelper.RpcRemovePet(target);

        Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
        target.MarkDirtySettings();

        if (target.walkingToVent || target.inVent || target.inMovingPlat || target.onLadder || target.MyPhysics.Animations.IsPlayingEnterVentAnimation())
        {
            LateTask.New(() =>
            {
                target.MyPhysics.RpcExitVent(Main.LastEnteredVent.TryGetValue(target.PlayerId, out var vent) ? vent.Id : 2);
                LateTask.New(() => target.TP(Pelican.GetBlackRoomPS()), 0.6f, log: false);
            }, 1f, log: false);
        }
        else
            target.TP(Pelican.GetBlackRoomPS());

        BackCountdown.TryAdd(target.PlayerId, KB_ResurrectionWaitingTime.GetInt());
    }

    private static void OnPlayerKill(PlayerControl killer)
    {
        killer.KillFlash();
        if (Main.GM.Value && AmongUsClient.Instance.AmHost) PlayerControl.LocalPlayer.KillFlash();
        ChatCommands.Spectators.ToValidPlayers().Do(x => x.KillFlash());

        KBScore[killer.PlayerId]++;

        if (Options.CurrentGameMode == CustomGameMode.AllInOne)
            Speedrun.ResetTimer(killer);

        float addRate = IRandom.Instance.Next(3, 5 + GetRankFromScore(killer.PlayerId)) / 100f;
        addRate *= KB_KillBonusMultiplier.GetFloat();
        if (killer.IsHost()) addRate /= 2f;

        float addin;

        switch (IRandom.Instance.Next(0, 4))
        {
            case 0:
                addin = PlayerHPMax[killer.PlayerId] * addRate;
                PlayerHPMax[killer.PlayerId] += addin;
                AddNameNotify(killer, string.Format(Translator.GetString("KB_Buff_HPMax"), addin.ToString("0.0#####")));
                break;
            case 1:
                addin = PlayerHPReco[killer.PlayerId] * addRate * 2;
                PlayerHPReco[killer.PlayerId] += addin;
                AddNameNotify(killer, string.Format(Translator.GetString("KB_Buff_HPReco"), addin.ToString("0.0#####")));
                break;
            case 2:
                addin = PlayerATK[killer.PlayerId] * addRate;
                PlayerATK[killer.PlayerId] += addin;
                AddNameNotify(killer, string.Format(Translator.GetString("KB_Buff_ATK"), addin.ToString("0.0#####")));
                break;
            case 3:
                addin = Math.Max(PlayerDF[killer.PlayerId], 1f) * addRate * 8;
                PlayerDF[killer.PlayerId] += addin;
                AddNameNotify(killer, string.Format(Translator.GetString("KB_Buff_DF"), addin.ToString("0.0#####")));
                break;
        }
    }

    private static void AddNameNotify(PlayerControl pc, string text, int time = 5)
    {
        NameNotify.Remove(pc.PlayerId);
        NameNotify.Add(pc.PlayerId, (text, Utils.TimeStamp + time));
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    private static class FixedUpdatePatch
    {
        private static long LastFixedUpdate;

        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        public static void Postfix(PlayerControl __instance)
        {
            byte id = __instance.PlayerId;
            if (!GameStates.IsInTask || !Main.IntroDestroyed || !CustomGameMode.SoloKombat.IsActiveOrIntegrated() || !AmongUsClient.Instance.AmHost || id == 255) return;

            bool soloAlive = __instance.SoloAlive();
            bool inVent = __instance.inVent;

            try
            {
                Vector2 pos = Pelican.GetBlackRoomPS();
                float dis = Vector2.Distance(pos, __instance.Pos());

                switch (soloAlive)
                {
                    case false when !inVent && !__instance.walkingToVent && !__instance.MyPhysics.Animations.IsPlayingEnterVentAnimation() && dis > 1f:
                    {
                        __instance.TP(pos);
                        break;
                    }
                    case true when !inVent && dis < 1.1f:
                    {
                        SpawnMap.GetSpawnMap().RandomTeleport(__instance);
                        break;
                    }
                }
            }
            catch (Exception e) { Utils.ThrowException(e); }

            long now = Utils.TimeStamp;
            if (LastCountdownTime[id] == now) return;
            LastCountdownTime[id] = now;

            if (LastHurt[id] + KB_RecoverAfterSecond.GetInt() < now && PlayerHP[id] < PlayerHPMax[id] && soloAlive && !inVent)
            {
                PlayerHP[id] += PlayerHPReco[id];
                PlayerHP[id] = Math.Min(PlayerHPMax[id], PlayerHP[id]);
            }

            if (BackCountdown.ContainsKey(id))
            {
                BackCountdown[id]--;
                if (BackCountdown[id] <= 0) OnPlayerBack(__instance);
            }

            if (NameNotify.TryGetValue(id, out (string Text, long TimeStamp) nameNotify) && nameNotify.TimeStamp < now)
                NameNotify.Remove(id);


            if (LastFixedUpdate == now) return;
            LastFixedUpdate = now;

            RoundTime--;

            Utils.NotifyRoles();
        }
    }

    // Options
    // ReSharper disable InconsistentNaming
    private static OptionItem KB_GameTime;
    public static OptionItem KB_ATKCooldown;
    private static OptionItem KB_HPMax;
    private static OptionItem KB_ATK;
    private static OptionItem KB_RecoverAfterSecond;
    private static OptionItem KB_RecoverPerSecond;
    private static OptionItem KB_ResurrectionWaitingTime;
    private static OptionItem KB_KillBonusMultiplier;

    // ReSharper restore InconsistentNaming
}