using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

internal static class FreeForAll
{
    private static Dictionary<byte, long> FFAShieldedList = [];
    private static Dictionary<byte, long> FFAIncreasedSpeedList = [];
    private static Dictionary<byte, long> FFADecreasedSpeedList = [];
    public static Dictionary<byte, long> FFALowerVisionList = [];

    public static Dictionary<byte, int> KillCount = [];
    public static int RoundTime;

    private static string LatestChatMessage = string.Empty;

    public static Dictionary<byte, int> PlayerTeams = [];

    public static readonly Dictionary<int, string> TeamColors = new()
    {
        { 0, "#00ffff" },
        { 1, "#ffff00" },
        { 2, "#ff00ff" },
        { 3, "#ff0000" },
        { 4, "#00ff00" },
        { 5, "#0000ff" },
        { 6, "#ffffff" },
        { 7, "#000000" }
    };

    public static OptionItem FFAGameTime;
    public static OptionItem FFAKcd;
    public static OptionItem FFALowerVision;
    public static OptionItem FFAIncreasedSpeed;
    public static OptionItem FFADecreasedSpeed;
    public static OptionItem FFAShieldDuration;
    public static OptionItem FFAModifiedVisionDuration;
    public static OptionItem FFAModifiedSpeedDuration;
    public static OptionItem FFADisableVentingWhenTwoPlayersAlive;
    public static OptionItem FFADisableVentingWhenKcdIsUp;
    public static OptionItem FFAEnableRandomAbilities;
    public static OptionItem FFAEnableRandomTwists;
    public static OptionItem FFAShieldIsOneTimeUse;
    public static OptionItem FFAChatDuringGame;
    public static OptionItem FFATeamMode;
    public static OptionItem FFATeamNumber;

    public static void SetupCustomOption()
    {
        FFAGameTime = new IntegerOptionItem(67_223_001, "FFA_GameTime", new(30, 600, 10), 300, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.FFA)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds)
            .SetHeader(true);

        FFAKcd = new FloatOptionItem(67_223_002, "FFA_KCD", new(1f, 60f, 1f), 10f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.FFA)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);

        FFADisableVentingWhenTwoPlayersAlive = new BooleanOptionItem(67_223_003, "FFA_DisableVentingWhenTwoPlayersAlive", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.FFA)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue));

        FFADisableVentingWhenKcdIsUp = new BooleanOptionItem(67_223_004, "FFA_DisableVentingWhenKCDIsUp", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.FFA)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue));

        FFAEnableRandomAbilities = new BooleanOptionItem(67_223_005, "FFA_EnableRandomAbilities", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.FFA)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue));

        FFAShieldDuration = new FloatOptionItem(67_223_006, "FFA_ShieldDuration", new(1f, 70f, 1f), 7f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.FFA)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);

        FFAIncreasedSpeed = new FloatOptionItem(67_223_007, "FFA_IncreasedSpeed", new(0.1f, 5f, 0.1f), 1.5f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.FFA)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue))
            .SetValueFormat(OptionFormat.Multiplier);

        FFADecreasedSpeed = new FloatOptionItem(67_223_008, "FFA_DecreasedSpeed", new(0.1f, 5f, 0.1f), 1f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.FFA)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue))
            .SetValueFormat(OptionFormat.Multiplier);

        FFAModifiedSpeedDuration = new FloatOptionItem(67_223_009, "FFA_ModifiedSpeedDuration", new(1f, 60f, 1f), 10f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.FFA).SetColor(new Color32(0, 255, 165, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);

        FFALowerVision = new FloatOptionItem(67_223_010, "FFA_LowerVision", new(0f, 1f, 0.05f), 0.5f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.FFA).SetColor(new Color32(0, 255, 165, byte.MaxValue))
            .SetValueFormat(OptionFormat.Multiplier);

        FFAModifiedVisionDuration = new FloatOptionItem(67_223_011, "FFA_ModifiedVisionDuration", new(1f, 70f, 1f), 5f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.FFA).SetColor(new Color32(0, 255, 165, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);

        FFAEnableRandomTwists = new BooleanOptionItem(67_223_012, "FFA_EnableRandomTwists", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.FFA)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue));

        FFAShieldIsOneTimeUse = new BooleanOptionItem(67_223_013, "FFA_ShieldIsOneTimeUse", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.FFA)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue));

        FFAChatDuringGame = new BooleanOptionItem(67_223_014, "FFA_ChatDuringGame", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.FFA)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue));

        FFATeamMode = new BooleanOptionItem(67_223_015, "FFA_TeamMode", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.FFA)
            .SetHeader(true)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue));

        FFATeamNumber = new IntegerOptionItem(67_223_016, "FFA_TeamNumber", new(2, 8, 1), 2, TabGroup.GameSettings)
            .SetParent(FFATeamMode)
            .SetGameMode(CustomGameMode.FFA)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue));
    }

    public static void Init()
    {
        if (Options.CurrentGameMode != CustomGameMode.FFA) return;

        FFADecreasedSpeedList = [];
        FFAIncreasedSpeedList = [];
        FFALowerVisionList = [];
        FFAShieldedList = [];

        KillCount = [];
        RoundTime = FFAGameTime.GetInt() + 8;
        Utils.SendRPC(CustomRPC.FFASync, 1, RoundTime);

        PlayerTeams = [];

        PlayerControl[] allPlayers = Main.AllAlivePlayerControls;
        if (Main.GM.Value && AmongUsClient.Instance.AmHost) allPlayers = allPlayers.Without(PlayerControl.LocalPlayer).ToArray();
        allPlayers = allPlayers.ExceptBy(ChatCommands.Spectators, x => x.PlayerId).ToArray();

        foreach (PlayerControl pc in allPlayers)
        {
            KillCount[pc.PlayerId] = 0;
            Utils.SendRPC(CustomRPC.FFASync, 2, pc.PlayerId, 0);
        }

        if (FFATeamMode.GetBool())
        {
            int i = 0;

            foreach (IEnumerable<byte> ids in allPlayers.Select(x => x.PlayerId).Shuffle().Partition(FFATeamNumber.GetInt()))
                foreach (byte id in ids)
                    PlayerTeams[id] = i++;
        }
    }

    public static int GetRankFromScore(byte playerId)
    {
        try
        {
            int ms = KillCount[playerId];
            int rank = 1 + KillCount.Values.Count(x => x > ms);
            rank += KillCount.Where(x => x.Value == ms).Select(x => x.Key).ToList().IndexOf(playerId); // In the old version, the struct 'KeyValuePair' was checked for equality using the inefficient runtime-provided implementation
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
        try
        {
            if (killer == null || target == null || Options.CurrentGameMode != CustomGameMode.FFA) return;

            if (target.inVent)
            {
                Logger.Info("Target is in a vent, kill blocked", "FFA");
                return;
            }

            if (FFATeamMode.GetBool() && PlayerTeams[killer.PlayerId] == PlayerTeams[target.PlayerId])
            {
                Logger.Info("Killer and Target are in the same team, attack blocked", "FFA");
                return;
            }

            int totalalive = Main.AllAlivePlayerControls.Length;

            if (FFAShieldedList.TryGetValue(target.PlayerId, out long dur))
            {
                killer.Notify(GetString("FFA_TargetIsShielded"));
                Logger.Info($"{killer.GetRealName().RemoveHtmlTags()} attacked shielded player {target.GetRealName().RemoveHtmlTags()}, their shield expires in {FFAShieldDuration.GetInt() - (Utils.TimeStamp - dur)}s", "FFA");

                if (FFAShieldIsOneTimeUse.GetBool())
                {
                    FFAShieldedList.Remove(target.PlayerId);
                    target.Notify(GetString("FFA_ShieldBroken"));
                    Logger.Info($"{target.GetRealName().RemoveHtmlTags()}'s shield was removed because {killer.GetRealName().RemoveHtmlTags()} tried to kill them and the shield is one-time-use according to settings", "FFA");
                }

                return;
            }

            OnPlayerKill(killer);

            if (totalalive == 3)
            {
                PlayerControl otherPC = null;

                foreach (PlayerControl pc in Main.AllAlivePlayerControls.Where(a => a.PlayerId != killer.PlayerId && a.PlayerId != target.PlayerId && a.IsAlive()))
                {
                    TargetArrow.Add(killer.PlayerId, pc.PlayerId);
                    TargetArrow.Add(pc.PlayerId, killer.PlayerId);
                    otherPC = pc;
                }

                Logger.Info($"The last 2 players ({killer.GetRealName().RemoveHtmlTags()} & {otherPC?.GetRealName().RemoveHtmlTags()}) now have an arrow toward each other", "FFA");

                if (FFADisableVentingWhenTwoPlayersAlive.GetBool())
                {
                    if (killer.inVent) killer.MyPhysics?.RpcExitVent(killer.GetClosestVent().Id);
                    if (otherPC != null && otherPC.inVent) otherPC.MyPhysics?.RpcExitVent(otherPC.GetClosestVent().Id);
                }
            }

            if (FFAEnableRandomAbilities.GetBool())
            {
                var sync = false;
                var mark = false;
                float nowKCD = Main.AllPlayerKillCooldown[killer.PlayerId];
                byte EffectType;

                if (Main.NormalOptions.MapId != 4)
                    EffectType = (byte)IRandom.Instance.Next(0, 10);
                else
                    EffectType = (byte)IRandom.Instance.Next(4, 10);

                switch (EffectType)
                {
                    // Buff
                    case <= 7:
                    {
                        var EffectID = (byte)IRandom.Instance.Next(0, 3);
                        if (Main.NormalOptions.MapId == 4) EffectID = 2;

                        switch (EffectID)
                        {
                            case 0:
                                FFAShieldedList.TryAdd(killer.PlayerId, Utils.TimeStamp);
                                killer.Notify(GetString("FFA-Event-GetShield"), FFAShieldDuration.GetFloat());
                                Main.AllPlayerKillCooldown[killer.PlayerId] = FFAKcd.GetFloat();
                                break;
                            case 1:
                                FFAIncreasedSpeedList[killer.PlayerId] = Utils.TimeStamp;
                                Main.AllPlayerSpeed[killer.PlayerId] = FFAIncreasedSpeed.GetFloat();
                                Main.AllPlayerKillCooldown[killer.PlayerId] = FFAKcd.GetFloat();
                                killer.Notify(GetString("FFA-Event-GetIncreasedSpeed"), FFAModifiedSpeedDuration.GetFloat());
                                mark = true;
                                break;
                            case 2:
                                Main.AllPlayerKillCooldown[killer.PlayerId] = Math.Clamp(FFAKcd.GetFloat() - 3f, 1f, 60f);
                                killer.Notify(GetString("FFA-Event-GetLowKCD"));
                                sync = true;
                                break;
                            default:
                                Main.AllPlayerKillCooldown[killer.PlayerId] = FFAKcd.GetFloat();
                                break;
                        }

                        break;
                    }
                    // De-Buff
                    case 8:
                    {
                        var effectID = (byte)IRandom.Instance.Next(0, 3);
                        if (Main.NormalOptions.MapId == 4) effectID = 1;

                        switch (effectID)
                        {
                            case 0:
                                FFADecreasedSpeedList[killer.PlayerId] = Utils.TimeStamp;
                                Main.AllPlayerSpeed[killer.PlayerId] = FFADecreasedSpeed.GetFloat();
                                killer.Notify(GetString("FFA-Event-GetDecreasedSpeed"), FFAModifiedSpeedDuration.GetFloat());
                                Main.AllPlayerKillCooldown[killer.PlayerId] = FFAKcd.GetFloat();
                                mark = true;
                                break;
                            case 1:
                                Main.AllPlayerKillCooldown[killer.PlayerId] = Math.Clamp(FFAKcd.GetFloat() + 3f, 1f, 60f);
                                killer.Notify(GetString("FFA-Event-GetHighKCD"));
                                sync = true;
                                break;
                            case 2:
                                FFALowerVisionList.TryAdd(killer.PlayerId, Utils.TimeStamp);
                                Main.AllPlayerKillCooldown[killer.PlayerId] = FFAKcd.GetFloat();
                                killer.Notify(GetString("FFA-Event-GetLowVision"));
                                mark = true;
                                break;
                            default:
                                Main.AllPlayerKillCooldown[killer.PlayerId] = FFAKcd.GetFloat();
                                break;
                        }

                        break;
                    }
                    // Mixed
                    default:
                        LateTask.New(() => killer.TPToRandomVent(), 0.5f, "FFA-Event-TP");
                        killer.Notify(GetString("FFA-Event-GetTP"));
                        Main.AllPlayerKillCooldown[killer.PlayerId] = FFAKcd.GetFloat();
                        break;
                }

                if (killer.IsHost()) Main.AllPlayerKillCooldown[killer.PlayerId] += Utils.CalculatePingDelay();

                if (sync || Math.Abs(nowKCD - Main.AllPlayerKillCooldown[killer.PlayerId]) > 0.1f)
                {
                    mark = false;
                    killer.SyncSettings();
                }

                if (mark) killer.MarkDirtySettings();
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }

        killer.Kill(target);
    }

    private static void OnPlayerKill(PlayerControl killer)
    {
        if (Main.GM.Value && AmongUsClient.Instance.AmHost) PlayerControl.LocalPlayer.KillFlash();
        ChatCommands.Spectators.ToValidPlayers().Do(x => x.KillFlash());

        KillCount[killer.PlayerId]++;
        Utils.SendRPC(CustomRPC.FFASync, 2, killer.PlayerId, KillCount[killer.PlayerId]);
    }

    public static string GetPlayerArrow(PlayerControl seer, PlayerControl target = null)
    {
        if (GameStates.IsMeeting || target != null && seer.PlayerId != target.PlayerId || Main.AllAlivePlayerControls.Length != 2) return string.Empty;

        PlayerControl otherPlayer = Main.AllAlivePlayerControls.FirstOrDefault(pc => pc.IsAlive() && pc.PlayerId != seer.PlayerId);
        if (otherPlayer == null) return string.Empty;

        string arrow = TargetArrow.GetArrows(seer, otherPlayer.PlayerId);
        return Utils.ColorString(Utils.GetRoleColor(CustomRoles.Killer), arrow);
    }

    public static void UpdateLastChatMessage(string playerName, string message)
    {
        LatestChatMessage = string.Format(GetString("FFAChatMessageNotify"), playerName, message);
        Main.AllAlivePlayerControls.NotifyPlayers(LatestChatMessage);
    }

    //[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class FixedUpdatePatch
    {
        private static long LastFixedUpdate;

        public static void Postfix()
        {
            if (!Main.IntroDestroyed || !GameStates.IsInTask || ExileController.Instance || Options.CurrentGameMode != CustomGameMode.FFA || !AmongUsClient.Instance.AmHost) return;

            long now = Utils.TimeStamp;
            if (LastFixedUpdate == now) return;
            LastFixedUpdate = now;

            RoundTime--;
            Utils.SendRPC(CustomRPC.FFASync, 1, RoundTime);

            var rd = IRandom.Instance;
            var ffaDoTPdecider = (byte)rd.Next(0, 100);
            bool ffaDoTP = ffaDoTPdecider == 0;

            if (FFAEnableRandomTwists.GetBool() && ffaDoTP)
            {
                Logger.Info("Swap everyone with someone", "FFA");

                List<byte> changePositionPlayers = [];

                foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                {
                    if (changePositionPlayers.Contains(pc.PlayerId) || !pc.IsAlive() || pc.onLadder || pc.inVent || pc.inMovingPlat) continue;

                    PlayerControl[] filtered = Main.AllAlivePlayerControls.Where(a =>
                        pc.IsAlive() && !pc.inVent && a.PlayerId != pc.PlayerId && !changePositionPlayers.Contains(a.PlayerId)).ToArray();

                    if (filtered.Length == 0) break;

                    PlayerControl target = filtered.RandomElement();

                    if (pc.inVent || target.inVent) continue;

                    changePositionPlayers.Add(target.PlayerId);
                    changePositionPlayers.Add(pc.PlayerId);

                    pc.RPCPlayCustomSound("Teleport");

                    Vector2 originPs = target.Pos();
                    target.TP(pc.Pos());
                    pc.TP(originPs);

                    target.Notify(Utils.ColorString(new(0, 255, 165, byte.MaxValue), string.Format(GetString("FFA-Event-RandomTP"), pc.GetRealName())));
                    pc.Notify(Utils.ColorString(new(0, 255, 165, byte.MaxValue), string.Format(GetString("FFA-Event-RandomTP"), target.GetRealName())));
                }

                changePositionPlayers.Clear();
            }

            if (Main.NormalOptions.MapId == 4) return;

            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                if (pc == null) return;

                var sync = false;

                if (FFADecreasedSpeedList.TryGetValue(pc.PlayerId, out long dstime) && dstime + FFAModifiedSpeedDuration.GetInt() < now)
                {
                    Logger.Info(pc.GetRealName() + "'s decreased speed expired", "FFA");
                    FFADecreasedSpeedList.Remove(pc.PlayerId);
                    Main.AllPlayerSpeed[pc.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                    sync = true;
                }

                if (FFAIncreasedSpeedList.TryGetValue(pc.PlayerId, out long istime) && istime + FFAModifiedSpeedDuration.GetInt() < now)
                {
                    Logger.Info(pc.GetRealName() + "'s increased speed expired", "FFA");
                    FFAIncreasedSpeedList.Remove(pc.PlayerId);
                    Main.AllPlayerSpeed[pc.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                    sync = true;
                }

                if (FFALowerVisionList.TryGetValue(pc.PlayerId, out long lvtime) && lvtime + FFAModifiedVisionDuration.GetInt() < now)
                {
                    Logger.Info(pc.GetRealName() + "'s lower vision effect expired", "FFA");
                    FFALowerVisionList.Remove(pc.PlayerId);
                    sync = true;
                }

                if (FFAShieldedList.TryGetValue(pc.PlayerId, out long stime) && stime + FFAShieldDuration.GetInt() < now)
                {
                    Logger.Info(pc.GetRealName() + "'s shield expired", "FFA");
                    FFAShieldedList.Remove(pc.PlayerId);
                }

                if (sync) pc.MarkDirtySettings();
            }
        }
    }
}
