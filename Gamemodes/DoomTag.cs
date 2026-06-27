using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Translator;

namespace EHR.Gamemodes;

internal static class DoomTag
{
    private static Dictionary<byte, byte> TargetMap = [];
    private static Dictionary<byte, long> LowerVisionList = [];
    private static bool CarnivalMode;
    private static float DefaultSpeed;

    public static OptionItem BaseKillCooldown;
    private static OptionItem PunishmentMode;
    private static OptionItem LowerVisionMultiplier;
    private static OptionItem CarnivalPlayerCount;
    private static OptionItem CarnivalKillCooldown;
    private static OptionItem CarnivalSpeedMultiplier;
    private static OptionItem ShowTargetArrow;

    private static readonly string[] PunishmentModeOptions = ["DoomTag.PunishSuicide", "DoomTag.PunishLowerVision"];

    public static void SetupCustomOption()
    {
        var id = 69_226_001;
        Color color = Utils.GetRoleColor(CustomRoles.Tagger);
        const CustomGameMode gameMode = CustomGameMode.DoomTag;
        const TabGroup tab = TabGroup.GameSettings;

        BaseKillCooldown = new FloatOptionItem(id++, "DoomTag.BaseKillCooldown", new(2.5f, 60f, 2.5f), 15f, tab)
            .SetHeader(true)
            .SetGameMode(gameMode)
            .SetColor(color)
            .SetValueFormat(OptionFormat.Seconds);

        PunishmentMode = new StringOptionItem(id++, "DoomTag.PunishmentMode", PunishmentModeOptions, 0, tab)
            .SetGameMode(gameMode)
            .SetColor(color);

        LowerVisionMultiplier = new FloatOptionItem(id++, "DoomTag.LowerVisionMultiplier", new(0f, 10f, 0.5f), 0.5f, tab)
            .SetParent(PunishmentMode)
            .SetGameMode(gameMode)
            .SetColor(color)
            .SetValueFormat(OptionFormat.Multiplier);

        CarnivalPlayerCount = new IntegerOptionItem(id++, "DoomTag.CarnivalPlayerCount", new(1, 127, 1), 3, tab)
            .SetGameMode(gameMode)
            .SetColor(color)
            .SetValueFormat(OptionFormat.Players);

        CarnivalKillCooldown = new FloatOptionItem(id++, "DoomTag.CarnivalKillCooldown", new(2.5f, 60f, 2.5f), 5f, tab)
            .SetGameMode(gameMode)
            .SetColor(color)
            .SetValueFormat(OptionFormat.Seconds);

        CarnivalSpeedMultiplier = new FloatOptionItem(id++, "DoomTag.CarnivalSpeedMultiplier", new(0.25f, 10f, 0.25f), 2.0f, tab)
            .SetGameMode(gameMode)
            .SetColor(color)
            .SetValueFormat(OptionFormat.Multiplier);

        ShowTargetArrow = new BooleanOptionItem(id, "DoomTag.ShowTargetArrow", false, tab)
            .SetGameMode(gameMode)
            .SetColor(color);
    }

    public static void Init()
    {
        Suffix.Clear();
        TargetMap = [];
        LowerVisionList = [];
        CarnivalMode = false;

        if (Options.CurrentGameMode != CustomGameMode.DoomTag) return;

        DefaultSpeed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);

        Utils.SendRPC(CustomRPC.DoomTagSync, 1, false);
    }

    public static void OnGameStart()
    {
        if (Options.CurrentGameMode != CustomGameMode.DoomTag) return;
        AssignTargets();
    }

    private static void AssignTargets()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        TargetMap = [];

        var allPlayers = Main.CachedAlivePlayerControls();
        if (Main.GM.Value) allPlayers.Remove(PlayerControl.LocalPlayer);
        allPlayers.RemoveAll(x => ChatCommands.Spectators.Contains(x.PlayerId));

        List<byte> playerIds = allPlayers.ConvertAll(x => x.PlayerId).Shuffle();

        if (playerIds.Count < 2)
        {
            Logger.Warn("Not enough players for DoomTag", "DoomTag");
            return;
        }

        for (int i = 0; i < playerIds.Count; i++)
        {
            byte targetId = playerIds[(i + 1) % playerIds.Count];
            byte hunterId = playerIds[i];
            if (hunterId == targetId) continue; // should never happen, but guard
            if (TargetMap.ContainsValue(targetId))
                TargetMap.Remove(TargetMap.GetKeyByValue(targetId));
            TargetMap[hunterId] = targetId;
            if (ShowTargetArrow.GetBool()) TargetArrow.Add(hunterId, targetId);
            Logger.Info($"{hunterId.ColoredPlayerName()} 2 targets 2 {targetId.ColoredPlayerName()}", "DoomTag");
        }

        SyncTargetMapRPC();
        CarnivalMode = false;
    }

    private static byte GetTarget(byte playerId)
    {
        return TargetMap.GetValueOrDefault(playerId, byte.MaxValue);
    }

    public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        byte killerId = killer.PlayerId;
        byte targetId = target.PlayerId;

        byte expectedTarget = GetTarget(killerId);

        if (expectedTarget == targetId)
        {
            Logger.Info($"{killer.GetRealName()} correctly killed their target {target.GetRealName()}", "DoomTag");

            byte nextTarget = GetTarget(targetId);

            TargetArrow.RemoveAllTarget(targetId);
            RemoveTargetArrowsTo(targetId);

            if (nextTarget != byte.MaxValue && nextTarget != killerId)
            {
                if (TargetMap.ContainsValue(nextTarget))
                    TargetMap.Remove(TargetMap.GetKeyByValue(nextTarget));
                TargetMap[killerId] = nextTarget;
                if (ShowTargetArrow.GetBool()) TargetArrow.Add(killerId, nextTarget);
                Logger.Info($"{killer.GetRealName()}'s new target is {nextTarget.ColoredPlayerName()}", "DoomTag");
            }
            else
            {
                TargetMap.Remove(killerId);
                TargetArrow.RemoveAllTarget(killerId);
            }

            TargetMap.Remove(targetId);

            CheckCarnivalMode();
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: killer);

            SyncTargetMapRPC();
            Utils.SendRPC(CustomRPC.DoomTagSync, 1, CarnivalMode);

            killer.Kill(target);
        }
        else
        {
            Logger.Info($"{killer.GetRealName()} killed wrong target {target.GetRealName()} (expected: {expectedTarget.ColoredPlayerName()})", "DoomTag");

            if (PunishmentMode.GetInt() == 0)
                killer.Kill(target);

            ApplyPunishment(killer);
        }
    }

    private static void RemoveTargetArrowsTo(byte targetId)
    {
        foreach (var kvp in TargetMap)
        {
            if (kvp.Value == targetId)
                TargetArrow.RemoveAllTarget(kvp.Key);
        }
    }

    private static void ApplyPunishment(PlayerControl killer)
    {
        switch (PunishmentMode.GetInt())
        {
            case 0:
                Logger.Info($"{killer.GetRealName()} is committing suicide for wrong kill", "DoomTag");
                killer.Notify(GetString("DoomTag.WrongKillSuicide"));
                LateTask.New(() =>
                {
                    if (!killer || !killer.IsAlive() || GameStates.IsEnded) return;
                    killer.Suicide();
                    HandleDeadPlayer(killer.PlayerId);
                }, 0.5f, "DoomTag Suicide Punishment");
                break;

            case 1:
                float visionMultiplier = LowerVisionMultiplier.GetFloat();
                LowerVisionList[killer.PlayerId] = Utils.TimeStamp;
                killer.MarkDirtySettings();
                killer.Notify(string.Format(GetString("DoomTag.WrongKillLowerVision"), visionMultiplier));
                break;
        }
    }

    private static void HandleDeadPlayer(byte deadPlayerId)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!TargetMap.Remove(deadPlayerId)) return;

        byte hunterId = TargetMap.GetKeyByValue(deadPlayerId, byte.MaxValue);
        byte nextTarget = GetTarget(deadPlayerId);

        TargetArrow.RemoveAllTarget(deadPlayerId);
        LowerVisionList.Remove(deadPlayerId);

        if (hunterId != byte.MaxValue && nextTarget != byte.MaxValue && nextTarget != hunterId)
        {
            TargetArrow.RemoveAllTarget(hunterId);
            if (TargetMap.ContainsValue(nextTarget))
                TargetMap.Remove(TargetMap.GetKeyByValue(nextTarget));
            TargetMap[hunterId] = nextTarget;
            if (ShowTargetArrow.GetBool()) TargetArrow.Add(hunterId, nextTarget);

            PlayerControl hunter = Utils.GetPlayerById(hunterId);
            if (hunter) Utils.NotifyRoles(SpecifySeer: hunter, SpecifyTarget: hunter);
        }
        else if (hunterId != byte.MaxValue)
        {
            TargetMap.Remove(hunterId);
            TargetArrow.RemoveAllTarget(hunterId);
        }

        CheckCarnivalMode();
        SyncTargetMapRPC();
    }

    private static void CheckCarnivalMode()
    {
        int aliveCount = Main.AllAlivePlayerControlsCount;
        int threshold = CarnivalPlayerCount.GetInt();

        if (!CarnivalMode && aliveCount <= threshold && aliveCount > 1)
        {
            CarnivalMode = true;
            Utils.SendRPC(CustomRPC.DoomTagSync, 1, true);

            foreach (PlayerControl pc in Main.CachedAlivePlayerControls())
            {
                Main.AllPlayerSpeed[pc.PlayerId] = DefaultSpeed * CarnivalSpeedMultiplier.GetFloat();
                Main.AllPlayerKillCooldown[pc.PlayerId] = CarnivalKillCooldown.GetFloat();
                if (!pc.AmOwner) pc.ReactorFlash();
                pc.MarkDirtySettings();
                pc.Notify(GetString("DoomTag.CarnivalModeActivated"));
            }

            SoundManager.Instance.PlaySound(HudManager.Instance.LobbyTimerExtensionUI.lobbyTimerPopUpSound, false);
            Utils.FlashColor(new(1f, 0.5f, 0f, 0.4f), 1.4f);
            Logger.Info($"Carnival Mode activated! {aliveCount} players remaining.", "DoomTag");
        }
    }

    public static bool CheckForGameEnd(out GameOverReason reason)
    {
        reason = GameOverReason.ImpostorsByKill;

        if (!Main.IntroDestroyed || GameStates.IsEnded) return false;

        var alivePlayers = Main.CachedAlivePlayerControls();

        switch (alivePlayers.Count)
        {
            case 1:
                PlayerControl winner = alivePlayers[0];
                CustomWinnerHolder.WinnerIds = [winner.PlayerId];
                Logger.Info($"DoomTag Winner: {winner.GetRealName()}", "DoomTag");
                return true;
            case 0:
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.None);
                Logger.Info("No players alive, force ending the game", "DoomTag");
                return true;
            default:
                return false;
        }
    }

    private static readonly StringBuilder Suffix = new();

    public static string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false)
    {
        if (seer.PlayerId != target.PlayerId || GameStates.IsMeeting) return string.Empty;

        byte targetId = GetTarget(seer.PlayerId);
        if (targetId == byte.MaxValue) return string.Empty;

        Suffix.Clear();

        Suffix.Append("<#D9BAA5>");
        Suffix.Append(hud ? GetString("DoomTag.CurrentTarget") : GetString("Target"));
        Suffix.Append(": <b>");
        Suffix.Append(targetId.ColoredPlayerName());
        Suffix.Append("</b>");

        if (ShowTargetArrow.GetBool())
        {
            Suffix.Append(' ');
            Suffix.Append(TargetArrow.GetArrows(seer, targetId));
        }

        Suffix.Append("</color>");

        if (CarnivalMode)
        {
            Suffix.Append("\n<#ff6600>");
            Suffix.Append(GetString("DoomTag.CarnivalModeHUD"));
            Suffix.Append("</color>");
        }

        return Suffix.ToString();
    }

    private static void SyncTargetMapRPC()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        var writer = Utils.CreateRPC(CustomRPC.DoomTagSync);
        writer.Write(2);
        writer.Write(TargetMap.Count);
        foreach (var kvp in TargetMap)
        {
            writer.Write(kvp.Key);
            writer.Write(kvp.Value);
        }
        Utils.EndRPC(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                CarnivalMode = reader.ReadBoolean();
                break;
            case 2:
                TargetMap = [];
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                    TargetMap[reader.ReadByte()] = reader.ReadByte();
                break;
        }
    }

    public static void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.PhantomCooldown = 0.1f;

        try { AURoleOptions.GuardianAngelCooldown = 900f; }
        catch (Exception e) { Utils.ThrowException(e); }

        if (LowerVisionList.ContainsKey(playerId))
        {
            float vision = LowerVisionMultiplier.GetFloat();
            opt.SetVision(false);
            opt.SetFloat(FloatOptionNames.CrewLightMod, vision);
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, vision);
        }
        else
        {
            opt.SetVision(true);
            opt.SetFloat(FloatOptionNames.CrewLightMod, 1.3f);
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, 1.3f);
        }
    }

    public static class FixedUpdatePatch
    {
        private static long LastFixedUpdate;

        public static void Postfix()
        {
            if (!Main.IntroDestroyed || !GameStates.IsInTask || ExileController.Instance || Options.CurrentGameMode != CustomGameMode.DoomTag || !AmongUsClient.Instance.AmHost) return;

            long now = Utils.TimeStamp;
            if (IntroCutsceneDestroyPatch.IntroDestroyTS + 10 > now || LastFixedUpdate == now) return;
            LastFixedUpdate = now;

            List<byte> deadPlayersInMap = null;
            foreach (var kvp in TargetMap)
            {
                CheckDead(kvp.Key);
                CheckDead(kvp.Value);
                continue;

                void CheckDead(byte id)
                {
                    if (!Main.PlayerStates.TryGetValue(id, out var state) || state.IsDead)
                    {
                        deadPlayersInMap ??= [];
                        deadPlayersInMap.Add(id);
                    }
                }
            }
            deadPlayersInMap?.ForEach(HandleDeadPlayer);

            CheckCarnivalMode();
        }
    }
}
