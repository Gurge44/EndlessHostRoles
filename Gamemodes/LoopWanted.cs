using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Translator;

namespace EHR.Gamemodes;

internal static class LoopWanted
{
    private static Dictionary<byte, byte> TargetMap = [];
    private static Dictionary<byte, long> LowerVisionList = [];
    private static bool CarnivalMode;
    private static float DefaultSpeed;

    private static OptionItem BaseKillCooldown;
    private static OptionItem PunishmentMode;
    private static OptionItem LowerVisionMultiplier;
    private static OptionItem CarnivalPlayerCount;
    private static OptionItem CarnivalKillCooldown;
    private static OptionItem CarnivalSpeedMultiplier;
    private static OptionItem ShowTargetArrow;

    private static readonly string[] PunishmentModeOptions = ["LoopWanted.PunishSuicide", "LoopWanted.PunishLowerVision"];

    public static void SetupCustomOption()
    {
        var id = 69_226_001;
        Color color = new Color32(255, 180, 50, byte.MaxValue);
        const CustomGameMode gameMode = CustomGameMode.LoopWanted;
        const TabGroup tab = TabGroup.GameSettings;

        BaseKillCooldown = new FloatOptionItem(id++, "LoopWanted.BaseKillCooldown", new(2.5f, 60f, 2.5f), 15f, tab)
            .SetHeader(true)
            .SetGameMode(gameMode)
            .SetColor(color)
            .SetValueFormat(OptionFormat.Seconds);

        PunishmentMode = new StringOptionItem(id++, "LoopWanted.PunishmentMode", PunishmentModeOptions, 0, tab)
            .SetGameMode(gameMode)
            .SetColor(color)
            .RegisterUpdateValueEvent((_, _, currentValue) =>
            {
                LowerVisionMultiplier.SetHidden(currentValue != 1);
            })
            .SetRunEventOnLoad(true);

        LowerVisionMultiplier = new FloatOptionItem(id++, "LoopWanted.LowerVisionMultiplier", new(0f, 10f, 0.5f), 0.5f, tab)
            .SetParent(PunishmentMode)
            .SetGameMode(gameMode)
            .SetColor(color)
            .SetValueFormat(OptionFormat.Multiplier);

        CarnivalPlayerCount = new IntegerOptionItem(id++, "LoopWanted.CarnivalPlayerCount", new(1, 127, 1), 3, tab)
            .SetGameMode(gameMode)
            .SetColor(color)
            .SetValueFormat(OptionFormat.Players);

        CarnivalKillCooldown = new FloatOptionItem(id++, "LoopWanted.CarnivalKillCooldown", new(2.5f, 60f, 2.5f), 5f, tab)
            .SetGameMode(gameMode)
            .SetColor(color)
            .SetValueFormat(OptionFormat.Seconds);

        CarnivalSpeedMultiplier = new FloatOptionItem(id++, "LoopWanted.CarnivalSpeedMultiplier", new(0.25f, 10f, 0.25f), 2.0f, tab)
            .SetGameMode(gameMode)
            .SetColor(color)
            .SetValueFormat(OptionFormat.Multiplier);

        ShowTargetArrow = new BooleanOptionItem(id++, "LoopWanted.ShowTargetArrow", false, tab)
            .SetGameMode(gameMode)
            .SetColor(color);
    }

    public static void Init()
    {
        if (Options.CurrentGameMode != CustomGameMode.LoopWanted) return;

        TargetMap = [];
        LowerVisionList = [];
        CarnivalMode = false;

        DefaultSpeed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);

        Utils.SendRPC(CustomRPC.LoopWantedSync, 1, false);
    }

    public static void OnGameStart()
    {
        if (Options.CurrentGameMode != CustomGameMode.LoopWanted) return;
        AssignTargets();
    }

    private static void AssignTargets()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        TargetMap = [];
        TargetArrow.Init();

        var allPlayers = Main.EnumerateAlivePlayerControls();
        if (Main.GM.Value)
            allPlayers = allPlayers.Without(PlayerControl.LocalPlayer);
        allPlayers = allPlayers.ExceptBy(ChatCommands.Spectators, x => x.PlayerId).ToArray();

        List<byte> playerIds = allPlayers.Select(x => x.PlayerId).Shuffle().ToList();

        if (playerIds.Count < 2)
        {
            Logger.Warn("Not enough players for LoopWanted", "LoopWanted");
            return;
        }

        for (int i = 0; i < playerIds.Count; i++)
        {
            byte targetId = playerIds[(i + 1) % playerIds.Count];
            byte hunterId = playerIds[i];
            if (hunterId == targetId) continue; // should never happen, but guard
            if (TargetMap.ContainsValue(targetId))
                TargetMap.Remove(TargetMap.First(kv => kv.Value == targetId).Key);
            TargetMap[hunterId] = targetId;
            if (ShowTargetArrow.GetBool()) TargetArrow.Add(hunterId, targetId);
            Logger.Info($"{hunterId.ColoredPlayerName()} 2 targets 2 {targetId.ColoredPlayerName()}", "LoopWanted");
        }

        SyncTargetMapRPC();
        CarnivalMode = false;
    }

    public static byte GetTarget(byte playerId)
    {
        return TargetMap.TryGetValue(playerId, out byte target) ? target : byte.MaxValue;
    }

    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost) return false;

        byte killerId = killer.PlayerId;
        byte targetId = target.PlayerId;

        byte expectedTarget = GetTarget(killerId);

        if (expectedTarget == targetId)
        {
            Logger.Info($"{killer.GetRealName()} correctly killed their target {target.GetRealName()}", "LoopWanted");

            byte nextTarget = GetTarget(targetId);

            TargetArrow.RemoveAllTarget(targetId);
            RemoveTargetArrowsTo(targetId);

            if (nextTarget != byte.MaxValue && nextTarget != killerId)
            {
                if (TargetMap.ContainsValue(nextTarget))
                    TargetMap.Remove(TargetMap.First(kv => kv.Value == nextTarget).Key);
                TargetMap[killerId] = nextTarget;
                if (ShowTargetArrow.GetBool()) TargetArrow.Add(killerId, nextTarget);
                Logger.Info($"{killer.GetRealName()}'s new target is {nextTarget.ColoredPlayerName()}", "LoopWanted");
            }
            else
            {
                TargetMap.Remove(killerId);
                TargetArrow.RemoveAllTarget(killerId);
            }

            TargetMap.Remove(targetId);

            killer.SyncSettings();

            CheckCarnivalMode();
            Utils.NotifyRoles(SpecifySeer: killer);

            SyncTargetMapRPC();
            Utils.SendRPC(CustomRPC.LoopWantedSync, 1, CarnivalMode);

            killer.Kill(target);
            return false;
        }
        else
        {
            Logger.Info($"{killer.GetRealName()} killed wrong target {target.GetRealName()} (expected: {expectedTarget.ColoredPlayerName()})", "LoopWanted");
            int punishment = PunishmentMode.GetInt();
            if (punishment == 0)
            {
                killer.Kill(target);
                ApplyPunishment(killer);
            }
            else
            {
                ApplyPunishment(killer);
            }
            return false;
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
        int punishment = PunishmentMode.GetInt();

        switch (punishment)
        {
            case 0:
                Logger.Info($"{killer.GetRealName()} is committing suicide for wrong kill", "LoopWanted");
                killer.Notify(GetString("LoopWanted.WrongKillSuicide"), 5f);
                LateTask.New(() =>
                {
                    if (!killer || !killer.IsAlive() || GameStates.IsEnded) return;
                    killer.Suicide(PlayerState.DeathReason.Kill);
                    HandleDeadPlayer(killer.PlayerId);
                }, 0.5f, "LoopWanted Suicide Punishment");
                break;

            case 1:
                float visionMultiplier = LowerVisionMultiplier.GetFloat();
                LowerVisionList[killer.PlayerId] = Utils.TimeStamp;
                killer.MarkDirtySettings();
                killer.Notify(string.Format(GetString("LoopWanted.WrongKillLowerVision"), visionMultiplier), 5f);
                break;
        }
    }

    public static void HandleDeadPlayer(byte deadPlayerId)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!TargetMap.ContainsKey(deadPlayerId)) return;

        byte hunterId = byte.MaxValue;
        foreach (var kvp in TargetMap)
        {
            if (kvp.Value == deadPlayerId && kvp.Key != deadPlayerId)
            {
                hunterId = kvp.Key;
                break;
            }
        }

        byte nextTarget = GetTarget(deadPlayerId);

        TargetArrow.RemoveAllTarget(deadPlayerId);
        TargetMap.Remove(deadPlayerId);
        LowerVisionList.Remove(deadPlayerId);

        if (hunterId != byte.MaxValue && nextTarget != byte.MaxValue && nextTarget != hunterId)
        {
            TargetArrow.RemoveAllTarget(hunterId);
            if (TargetMap.ContainsValue(nextTarget))
                TargetMap.Remove(TargetMap.First(kv => kv.Value == nextTarget).Key);
            TargetMap[hunterId] = nextTarget;
            if (ShowTargetArrow.GetBool()) TargetArrow.Add(hunterId, nextTarget);

            PlayerControl hunter = Utils.GetPlayerById(hunterId);
            if (hunter) Utils.NotifyRoles(SpecifySeer: hunter);
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
        int aliveCount = Main.EnumerateAlivePlayerControls().Count(x => !ChatCommands.Spectators.Contains(x.PlayerId));
        int threshold = CarnivalPlayerCount.GetInt();

        if (!CarnivalMode && aliveCount <= threshold && aliveCount > 1)
        {
            CarnivalMode = true;
            Utils.SendRPC(CustomRPC.LoopWantedSync, 1, true);

            foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
            {
                if (ChatCommands.Spectators.Contains(pc.PlayerId)) continue;

                Main.AllPlayerSpeed[pc.PlayerId] = DefaultSpeed * CarnivalSpeedMultiplier.GetFloat();
                Main.AllPlayerKillCooldown[pc.PlayerId] = CarnivalKillCooldown.GetFloat();
                pc.SyncSettings();
                pc.MarkDirtySettings();
                pc.Notify(GetString("LoopWanted.CarnivalModeActivated"), 5f);
            }

            SoundManager.Instance.PlaySound(HudManager.Instance.LobbyTimerExtensionUI.lobbyTimerPopUpSound, false);
            Utils.FlashColor(new(1f, 0.5f, 0f, 0.4f), 1.4f);
            Logger.Info($"Carnival Mode activated! {aliveCount} players remaining.", "LoopWanted");
        }
    }

    public static bool CheckForGameEnd(out GameOverReason reason)
    {
        reason = GameOverReason.ImpostorsByKill;

        if (!Main.IntroDestroyed || GameStates.IsEnded) return false;

        var alivePlayers = Main.EnumerateAlivePlayerControls()
            .Where(x => !ChatCommands.Spectators.Contains(x.PlayerId)).ToArray();

        if (alivePlayers.Length <= 1)
        {
            if (alivePlayers.Length == 1)
            {
                PlayerControl winner = alivePlayers[0];
                CustomWinnerHolder.WinnerIds = [winner.PlayerId];
                Logger.Info($"LoopWanted Winner: {winner.GetRealName()}", "LoopWanted");
            }
            else
            {
                CustomWinnerHolder.WinnerIds = [];
            }

            return true;
        }

        return false;
    }

    public static string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false)
    {
        if (seer.PlayerId != target.PlayerId || GameStates.IsMeeting) return string.Empty;

        byte targetId = GetTarget(seer.PlayerId);
        if (targetId == byte.MaxValue) return string.Empty;

        string targetName = Main.AllPlayerNames.GetValueOrDefault(targetId, GetString("Unknown"));
        string arrow = ShowTargetArrow.GetBool() ? TargetArrow.GetArrows(seer, targetId) : string.Empty;

        var sb = new StringBuilder();
        sb.Append($"<color=#ffb432>{(hud ? GetString("LoopWanted.CurrentTarget") : GetString("Target"))}:</color> <b>{targetName.RemoveHtmlTags().Replace("\r\n", string.Empty)}</b> {arrow}");

        if (CarnivalMode)
            sb.Append($"\n<color=#ff6600>{GetString("LoopWanted.CarnivalModeHUD")}</color>");

        return sb.ToString();
    }

    private static void SyncTargetMapRPC()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        var writer = Utils.CreateRPC(CustomRPC.LoopWantedSync);
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
            opt.SetVision(false);
            opt.SetFloat(FloatOptionNames.CrewLightMod, LowerVisionMultiplier.GetFloat());
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, LowerVisionMultiplier.GetFloat());
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
            if (!Main.IntroDestroyed || !GameStates.IsInTask || ExileController.Instance ||
                Options.CurrentGameMode != CustomGameMode.LoopWanted || !AmongUsClient.Instance.AmHost) return;

            long now = Utils.TimeStamp;
            if (IntroCutsceneDestroyPatch.IntroDestroyTS + 10 > now || LastFixedUpdate == now) return;
            LastFixedUpdate = now;

            var deadPlayersInMap = new List<byte>();
            foreach (var kvp in TargetMap)
            {
                byte id = kvp.Key;
                if (id.GetPlayer()?.IsAlive() == true) continue;
                if (Main.PlayerStates.TryGetValue(id, out var state) && state.IsDead)
                    deadPlayersInMap.Add(id);
            }
            foreach (byte deadId in deadPlayersInMap)
                HandleDeadPlayer(deadId);

            var orphaned = new List<byte>();
            foreach (var kvp in TargetMap)
            {
                byte targetId = kvp.Value;
                var targetPlayer = targetId.GetPlayer();
                var state = Main.PlayerStates.GetValueOrDefault(targetId);
                if (targetPlayer && !targetPlayer.IsAlive() || (state != null && state.IsDead))
                    orphaned.Add(kvp.Key);
            }
            foreach (byte hunterId in orphaned)
                HandleDeadPlayer(TargetMap[hunterId]);

            CheckCarnivalMode();
        }
    }
}
