using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AmongUs.Data;
using AmongUs.GameOptions;
using EHR.AddOns.Common;
using EHR.AddOns.Crewmate;
using EHR.AddOns.GhostRoles;
using EHR.AddOns.Impostor;
using EHR.Crewmate;
using EHR.Impostor;
using EHR.Modules;
using EHR.Neutral;
using HarmonyLib;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes;
using InnerNet;
using Newtonsoft.Json;
using UnityEngine;
using static EHR.Translator;


namespace EHR;

/*
List of symbols that work in game

1-stars: ★ ☆ ⁂ ⁑ ✽
2-arrows: ☝ ☞ ☟ ☜ ↑ ↓ → ← ↔ ↕  ⬆ ↗ ➡ ↘ ⬇ ↙ ⬅ ↖ ↕ ↔  ⤴ ⤵ ￩ ￪ ￫ ￬ ⇦ ⇧ ⇨ ⇩ ⇵ ⇄ ⇅ ⇆  ↹
3- shapes • ○ ◦ ⦿ ▲ ▼ ♠ ♥ ♣ ♦ ♤ ♡ ♧ ♢ ■ □ ▢ ▣ ▤ ▥ ▦ ▧ ▨ ▩ ▪ ▫  ◌ ● ◐ ◑ ◒ ◓ ◯ ⦿ ◆ ◇ ◈ ❖  ▩  ▱ ▶ ◀
4- symbols: ✓ ∞ † ✚ ♫ ♪ ♲ ♳ ♴ ♵ ♶ ♷ ♸ ♹ ♺ ♻ ♼ ♽☎ ☏ ✂ ♀♂ ⚠
5- emojis:  😂☹️😆☺️😎😉😅😊😋😀😁😂😃😄😅😍😎😋😊😉😆☺️☁️☂️☀️
6- random: ‰ § ¶ © ™ ¥ $ ¢ € ƒ  £ Æ

other:  ∟ ⌠ ⌡ ╬ ╨ ▓ ▒ ░ « » █ ▄ ▌▀▐│ ┤ ╡ ╢ ╖ ╕ ╣ ║ ╗ ╝ ╜ ╛ ┐ └ ┴ ┬ ─ ┼ ╞ ╟ ╚ ╔ ╩ ╦ ╠ ═ ╬ ╧ ╨ ╤ ╥ ╙ ╘ ╒ ╓ ╫ ╪ ┘ ┌ Θ ∩ ¿
*/

public static class Utils
{
    private static readonly DateTime TimeStampStartTime = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly StringBuilder SelfSuffix = new();
    private static readonly StringBuilder SelfMark = new(20);
    private static readonly StringBuilder TargetSuffix = new();
    private static readonly StringBuilder TargetMark = new(20);

    private static readonly Dictionary<string, Sprite> CachedSprites = [];

    public static long TimeStamp => (long)(DateTime.Now.ToUniversalTime() - TimeStampStartTime).TotalSeconds;
    public static bool DoRPC => AmongUsClient.Instance.AmHost && Main.AllPlayerControls.Any(x => x.IsModClient() && !x.IsHost());
    public static int TotalTaskCount => Main.RealOptionsData.GetInt(Int32OptionNames.NumCommonTasks) + Main.RealOptionsData.GetInt(Int32OptionNames.NumLongTasks) + Main.RealOptionsData.GetInt(Int32OptionNames.NumShortTasks);
    public static string EmptyMessage => "<size=0>.</size>";
    public static int AllPlayersCount => Main.PlayerStates.Values.Count(state => state.countTypes != CountTypes.OutOfGame);
    public static int AllAlivePlayersCount => Main.AllAlivePlayerControls.Count(pc => !pc.Is(CountTypes.OutOfGame));
    public static bool IsAllAlive => Main.PlayerStates.Values.All(state => state.countTypes == CountTypes.OutOfGame || !state.IsDead);
    public static long GetTimeStamp(DateTime? dateTime = null) => (long)((dateTime ?? DateTime.Now).ToUniversalTime() - TimeStampStartTime).TotalSeconds;

    public static void ErrorEnd(string text)
    {
        if (AmongUsClient.Instance.AmHost)
        {
            Logger.Fatal($"{text} error, triggering anti-black screen measures", "Anti-Blackout");
            ChatUpdatePatch.DoBlockChat = true;
            Main.OverrideWelcomeMsg = GetString("AntiBlackOutNotifyInLobby");
            LateTask.New(() => { Logger.SendInGame(GetString("AntiBlackOutLoggerSendInGame") /*, true*/); }, 3f, "Anti-Black Msg SendInGame");
            LateTask.New(() =>
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Error);
                GameManager.Instance.LogicFlow.CheckEndCriteria();
                RPC.ForceEndGame(CustomWinner.Error);
            }, 5.5f, "Anti-Black End Game");
        }
        else
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpc(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.AntiBlackout);
            writer.Write(text);
            writer.EndMessage();
            if (Options.EndWhenPlayerBug.GetBool())
            {
                LateTask.New(() => { Logger.SendInGame(GetString("AntiBlackOutRequestHostToForceEnd") /*, true*/); }, 3f, "Anti-Black Msg SendInGame");
            }
            else
            {
                LateTask.New(() => { Logger.SendInGame(GetString("AntiBlackOutHostRejectForceEnd") /*, true*/); }, 3f, "Anti-Black Msg SendInGame");
                LateTask.New(() =>
                {
                    AmongUsClient.Instance.ExitGame(DisconnectReasons.Custom);
                    Logger.Fatal($"{text} error, disconnected from game", "Anti-black");
                }, 8f, "Anti-Black Exit Game");
            }
        }
    }

    public static void TPAll(Vector2 location, bool log = true)
    {
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            TP(pc.NetTransform, location, log);
        }
    }

    public static bool TP(CustomNetworkTransform nt, Vector2 location, bool log = true)
    {
        var pc = nt.myPlayer;
        if (pc.Is(CustomRoles.AntiTP)) return false;
        if (pc.inVent || pc.inMovingPlat || pc.onLadder || !pc.IsAlive() || pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation() || pc.MyPhysics.Animations.IsPlayingEnterVentAnimation())
        {
            if (log) Logger.Warn($"Target ({pc.GetNameWithRole().RemoveHtmlTags()}) is in an un-teleportable state - Teleporting canceled", "TP");
            return false;
        }

        if (AmongUsClient.Instance.AmClient) nt.SnapTo(location, (ushort)(nt.lastSequenceId + 328));

        ushort newSid = (ushort)(nt.lastSequenceId + 8);
        MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(nt.NetId, (byte)RpcCalls.SnapTo, SendOption.Reliable);
        NetHelpers.WriteVector2(location, messageWriter);
        messageWriter.Write(newSid);
        AmongUsClient.Instance.FinishRpcImmediately(messageWriter);

        if (log) Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} => {location}", "TP");
        return true;
    }

    // ReSharper disable once InconsistentNaming
    public static bool TPtoRndVent(CustomNetworkTransform nt, bool log = true)
    {
        var vents = ShipStatus.Instance.AllVents;
        var vent = vents.RandomElement();

        Logger.Info($"{nt.myPlayer.GetNameWithRole().RemoveHtmlTags()} => {vent.transform.position} (vent)", "TP");

        return TP(nt, new(vent.transform.position.x, vent.transform.position.y + 0.3636f), log);
    }

    public static ClientData GetClientById(int id)
    {
        try
        {
            var client = AmongUsClient.Instance.allClients.ToArray().FirstOrDefault(cd => cd.Id == id);
            return client;
        }
        catch
        {
            return null;
        }
    }

    public static bool IsActive(SystemTypes type)
    {
        int mapId = Main.NormalOptions.MapId;
        switch (type)
        {
            case SystemTypes.Electrical:
            {
                if (mapId == 5) return false; // if The Fungle return false
                var SwitchSystem = ShipStatus.Instance.Systems[type].Cast<SwitchSystem>();
                return SwitchSystem is { IsActive: true };
            }
            case SystemTypes.Reactor:
            {
                switch (mapId)
                {
                    case 2:
                        return false; // if Polus return false
                    // Only Airhip
                    case 4:
                    {
                        var HeliSabotageSystem = ShipStatus.Instance.Systems[type].Cast<HeliSabotageSystem>();
                        return HeliSabotageSystem != null && HeliSabotageSystem.IsActive;
                    }
                    default:
                    {
                        var ReactorSystemType = ShipStatus.Instance.Systems[type].Cast<ReactorSystemType>();
                        return ReactorSystemType is { IsActive: true };
                    }
                }
            }
            case SystemTypes.Laboratory:
            {
                if (mapId != 2) return false; // Only Polus
                var ReactorSystemType = ShipStatus.Instance.Systems[type].Cast<ReactorSystemType>();
                return ReactorSystemType is { IsActive: true };
            }
            case SystemTypes.LifeSupp:
            {
                if (mapId is 2 or 4 or 5) return false; // Only Skeld & Mira HQ
                var LifeSuppSystemType = ShipStatus.Instance.Systems[type].Cast<LifeSuppSystemType>();
                return LifeSuppSystemType is { IsActive: true };
            }
            case SystemTypes.Comms:
            {
                if (mapId is 1 or 5) // Only Mira HQ & The Fungle
                {
                    var HqHudSystemType = ShipStatus.Instance.Systems[type].Cast<HqHudSystemType>();
                    return HqHudSystemType is { IsActive: true };
                }

                var HudOverrideSystemType = ShipStatus.Instance.Systems[type].Cast<HudOverrideSystemType>();
                return HudOverrideSystemType is { IsActive: true };
            }
            case SystemTypes.HeliSabotage:
            {
                if (mapId != 4) return false; // Only Airhip
                var HeliSabotageSystem = ShipStatus.Instance.Systems[type].Cast<HeliSabotageSystem>();
                return HeliSabotageSystem != null && HeliSabotageSystem.IsActive;
            }
            case SystemTypes.MushroomMixupSabotage:
            {
                if (mapId != 5) return false; // Only The Fungle
                var MushroomMixupSabotageSystem = ShipStatus.Instance.Systems[type].Cast<MushroomMixupSabotageSystem>();
                return MushroomMixupSabotageSystem != null && MushroomMixupSabotageSystem.IsActive;
            }
            default:
                return false;
        }
    }

    public static void SetVision(this IGameOptions opt, bool HasImpVision)
    {
        if (HasImpVision)
        {
            opt.SetFloat(FloatOptionNames.CrewLightMod, opt.GetFloat(FloatOptionNames.ImpostorLightMod));

            if (IsActive(SystemTypes.Electrical))
            {
                opt.SetFloat(FloatOptionNames.CrewLightMod, opt.GetFloat(FloatOptionNames.CrewLightMod) * 5);
            }

            return;
        }

        opt.SetFloat(FloatOptionNames.ImpostorLightMod, opt.GetFloat(FloatOptionNames.CrewLightMod));

        if (IsActive(SystemTypes.Electrical))
        {
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, opt.GetFloat(FloatOptionNames.ImpostorLightMod) / 5);
        }
    }

    public static void SetVisionV2(this IGameOptions opt)
    {
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, opt.GetFloat(FloatOptionNames.CrewLightMod));
        if (IsActive(SystemTypes.Electrical))
        {
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, opt.GetFloat(FloatOptionNames.ImpostorLightMod) / 5);
        }
    }

    //誰かが死亡したときのメソッド
    public static void TargetDies(PlayerControl killer, PlayerControl target)
    {
        if (!target.Data.IsDead || GameStates.IsMeeting) return;

        var targetRole = target.GetCustomRole();

        foreach (PlayerControl seer in Main.AllPlayerControls)
        {
            if (KillFlashCheck(killer, target, seer))
            {
                seer.KillFlash();
                continue;
            }

            if (targetRole == CustomRoles.CyberStar)
            {
                if (!Options.ImpKnowCyberStarDead.GetBool() && seer.GetCustomRole().IsImpostor()) continue;
                if (!Options.NeutralKnowCyberStarDead.GetBool() && seer.GetCustomRole().IsNeutral()) continue;
                seer.KillFlash();
                seer.Notify(ColorString(GetRoleColor(CustomRoles.CyberStar), GetString("OnCyberStarDead")));
            }
        }

        switch (targetRole)
        {
            case CustomRoles.CyberStar when !Main.CyberStarDead.Contains(target.PlayerId):
                Main.CyberStarDead.Add(target.PlayerId);
                break;
            case CustomRoles.Demolitionist:
                Demolitionist.OnDeath(killer, target);
                break;
        }
    }

    public static bool KillFlashCheck(PlayerControl killer, PlayerControl target, PlayerControl seer)
    {
        if (seer.Is(CustomRoles.GM) || seer.Is(CustomRoles.Seer)) return true;
        if (seer.Data.IsDead || killer == seer || target == seer) return false;
        return seer.Is(CustomRoles.EvilTracker) && EvilTracker.KillFlashCheck(killer, target);
    }

    public static void BlackOut(this IGameOptions opt, bool IsBlackOut)
    {
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, Main.DefaultImpostorVision);
        opt.SetFloat(FloatOptionNames.CrewLightMod, Main.DefaultCrewmateVision);
        if (IsBlackOut)
        {
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, 0);
            opt.SetFloat(FloatOptionNames.CrewLightMod, 0);
        }
    }

    public static void SaveComboInfo()
    {
        SaveFile("./EHR_DATA/AlwaysCombos.json");
        SaveFile("./EHR_DATA/NeverCombos.json");
        return;

        void SaveFile(string path)
        {
            try
            {
                var data = new Il2CppSystem.Collections.Generic.Dictionary<int, Il2CppSystem.Collections.Generic.Dictionary<string, Il2CppSystem.Collections.Generic.List<string>>>();
                var dict = path.Contains("Always") ? Main.AlwaysSpawnTogetherCombos : Main.NeverSpawnTogetherCombos;
                dict.Do(kvp =>
                {
                    data[kvp.Key] = new();
                    kvp.Value.Do(pair =>
                    {
                        var key = pair.Key.ToString();
                        data[kvp.Key][key] = new();
                        pair.Value.Do(x => data[kvp.Key][key].Add(x.ToString()));
                    });
                });
                File.WriteAllText(path, JsonConvert.SerializeObject(data, Formatting.Indented));
            }
            catch (Exception e)
            {
                Logger.Error("Failed to save combo info", "SaveComboInfo");
                ThrowException(e);
            }
        }
    }

    public static void LoadComboInfo()
    {
        LoadFile("./EHR_DATA/AlwaysCombos.json");
        LoadFile("./EHR_DATA/NeverCombos.json");
        return;

        void LoadFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                var data = JsonConvert.DeserializeObject<Il2CppSystem.Collections.Generic.Dictionary<int, Il2CppSystem.Collections.Generic.Dictionary<string, Il2CppSystem.Collections.Generic.List<string>>>>(File.ReadAllText(path));
                var dict = path.Contains("Always") ? Main.AlwaysSpawnTogetherCombos : Main.NeverSpawnTogetherCombos;
                dict.Clear();
                foreach (var kvp in data)
                {
                    dict[kvp.Key] = [];
                    foreach (var pair in kvp.Value)
                    {
                        var key = Enum.Parse<CustomRoles>(pair.Key);
                        dict[kvp.Key][key] = [];
                        foreach (var n in pair.Value)
                        {
                            dict[kvp.Key][key].Add(Enum.Parse<CustomRoles>(n));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("Failed to load combo info", "LoadComboInfo");
                ThrowException(e);
            }
        }
    }

    public static string GetDisplayRoleName(byte playerId, bool pure = false)
    {
        var TextData = GetRoleText(playerId, playerId, pure);
        return ColorString(TextData.Item2, TextData.Item1);
    }

    public static string GetRoleName(CustomRoles role, bool forUser = true)
    {
        return GetRoleString(role.ToString(), forUser);
    }

    public static string GetRoleMode(CustomRoles role, bool parentheses = true)
    {
        if (Options.HideGameSettings.GetBool() && Main.AllPlayerControls.Length > 1)
            return string.Empty;

        string mode;
        try
        {
            mode = !role.IsAdditionRole()
                ? GetString($"Rate{role.GetMode()}")
                : GetString($"Rate{Options.CustomAdtRoleSpawnRate[role].GetInt()}");
        }
        catch (KeyNotFoundException)
        {
            mode = GetString("Rate0");
        }

        mode = mode.Replace("color=", string.Empty);
        return parentheses ? $"({mode})" : mode;
    }

    public static string GetDeathReason(PlayerState.DeathReason status)
    {
        return GetString("DeathReason." + Enum.GetName(typeof(PlayerState.DeathReason), status));
    }

    public static Color GetRoleColor(CustomRoles role)
    {
        var hexColor = Main.RoleColors.GetValueOrDefault(role, "#ffffff");
        _ = ColorUtility.TryParseHtmlString(hexColor, out Color c);
        return c;
    }

    public static string GetRoleColorCode(CustomRoles role)
    {
        var hexColor = Main.RoleColors.GetValueOrDefault(role, "#ffffff");
        return hexColor;
    }

    public static (string, Color) GetRoleText(byte seerId, byte targetId, bool pure = false)
    {
        var seerMainRole = Main.PlayerStates[seerId].MainRole;
        var seerSubRoles = Main.PlayerStates[seerId].SubRoles;

        var targetMainRole = Main.PlayerStates[targetId].MainRole;
        var targetSubRoles = Main.PlayerStates[targetId].SubRoles;

        var self = seerId == targetId || Main.PlayerStates[seerId].IsDead;

        bool isHnsAgentOverride = Options.CurrentGameMode == CustomGameMode.HideAndSeek && targetMainRole == CustomRoles.Agent && HnSManager.PlayerRoles[seerId].Interface.Team != Team.Impostor;
        bool loversShowDifferentRole = false;
        if (!GameStates.IsEnded && targetMainRole == CustomRoles.LovingImpostor && !self && seerMainRole != CustomRoles.LovingCrewmate && !seerSubRoles.Contains(CustomRoles.Lovers))
        {
            targetMainRole = Lovers.LovingImpostorRoleForOtherImps.GetValue() switch
            {
                0 => CustomRoles.ImpostorEHR,
                1 => Lovers.LovingImpostorRole,
                _ => CustomRoles.LovingImpostor
            };
            loversShowDifferentRole = true;
        }

        string RoleText = GetRoleName(isHnsAgentOverride ? CustomRoles.Hider : targetMainRole);
        Color RoleColor = GetRoleColor(loversShowDifferentRole ? CustomRoles.Impostor : targetMainRole);

        if (LastImpostor.currentId == targetId)
            RoleText = GetRoleString("Last-") + RoleText;

        if (Options.NameDisplayAddons.GetBool() && !pure && self)
        {
            foreach (var subRole in targetSubRoles.Where(x => x is not CustomRoles.LastImpostor and not CustomRoles.Madmate and not CustomRoles.Charmed and not CustomRoles.Recruit and not CustomRoles.Lovers and not CustomRoles.Contagious))
            {
                var str = GetString("Prefix." + subRole);
                if (!subRole.IsAdditionRole())
                {
                    str = GetString(subRole.ToString());
                }

                RoleText = ColorString(GetRoleColor(subRole), (Options.AddBracketsToAddons.GetBool() ? "<#ffffff>(</color>" : string.Empty) + str + (Options.AddBracketsToAddons.GetBool() ? "<#ffffff>)</color>" : string.Empty) + " ") + RoleText;
            }
        }

        if (targetSubRoles.Contains(CustomRoles.Madmate))
        {
            RoleColor = GetRoleColor(CustomRoles.Madmate);
            RoleText = GetRoleString("Mad-") + RoleText;
        }

        if (targetSubRoles.Contains(CustomRoles.Recruit))
        {
            RoleColor = GetRoleColor(CustomRoles.Recruit);
            RoleText = GetRoleString("Recruit-") + RoleText;
        }

        if (targetSubRoles.Contains(CustomRoles.Charmed) && (self || pure || seerMainRole == CustomRoles.Succubus || (Succubus.TargetKnowOtherTarget.GetBool() && seerSubRoles.Contains(CustomRoles.Charmed))))
        {
            RoleColor = GetRoleColor(CustomRoles.Charmed);
            RoleText = GetRoleString("Charmed-") + RoleText;
        }

        if (targetSubRoles.Contains(CustomRoles.Contagious) && (self || pure || seerMainRole == CustomRoles.Virus || (Virus.TargetKnowOtherTarget.GetBool() && seerSubRoles.Contains(CustomRoles.Contagious))))
        {
            RoleColor = GetRoleColor(CustomRoles.Contagious);
            RoleText = GetRoleString("Contagious-") + RoleText;
        }

        return (RoleText, RoleColor);
    }

    public static string GetKillCountText(byte playerId, bool ffa = false)
    {
        if (Main.PlayerStates.All(x => x.Value.GetRealKiller() != playerId) && !ffa) return string.Empty;
        return ' ' + ColorString(new(255, 69, 0, byte.MaxValue), string.Format(GetString("KillCount"), Main.PlayerStates.Count(x => x.Value.GetRealKiller() == playerId)));
    }

    public static string GetVitalText(byte playerId, bool realKillerColor = false)
    {
        var state = Main.PlayerStates[playerId];
        string deathReason = state.IsDead ? GetString("DeathReason." + state.deathReason) : GetString("Alive");
        if (realKillerColor)
        {
            var KillerId = state.GetRealKiller();
            Color color = KillerId != byte.MaxValue ? Main.PlayerColors[KillerId] : GetRoleColor(CustomRoles.Doctor);
            if (state.deathReason == PlayerState.DeathReason.Disconnected) color = new(255, 255, 255, 50);
            deathReason = ColorString(color, deathReason);
        }

        return deathReason;
    }

    public static MessageWriter CreateRPC(CustomRPC rpc) => AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)rpc, SendOption.Reliable);
    public static void EndRPC(MessageWriter writer) => AmongUsClient.Instance.FinishRpcImmediately(writer);

    public static void SendRPC(CustomRPC rpc, params object[] data)
    {
        if (!DoRPC) return;

        var w = CreateRPC(rpc);
        foreach (var o in data)
        {
            switch (o)
            {
                case byte b:
                    w.Write(b);
                    break;
                case int i:
                    w.WritePacked(i);
                    break;
                case float f:
                    w.Write(f);
                    break;
                case string s:
                    w.Write(s);
                    break;
                case bool b:
                    w.Write(b);
                    break;
                case long l:
                    w.Write(l.ToString());
                    break;
                case char c:
                    w.Write(c.ToString());
                    break;
                case Vector2 v:
                    w.Write(v);
                    break;
                case Vector3 v:
                    w.Write(v);
                    break;
                default:
                    try
                    {
                        if (o != null && Enum.TryParse(o.GetType(), o.ToString(), out var e) && e != null)
                            w.WritePacked((int)e);
                    }
                    catch (InvalidCastException e)
                    {
                        ThrowException(e);
                    }

                    break;
            }
        }

        EndRPC(w);
    }

    public static void IncreaseAbilityUseLimitOnKill(PlayerControl killer)
    {
        if (Main.PlayerStates[killer.PlayerId].Role is Mafioso { IsEnable: true } mo) mo.OnMurder(killer, null);
        var add = GetSettingNameAndValueForRole(killer.GetCustomRole(), "AbilityUseGainWithEachKill");
        killer.RpcIncreaseAbilityUseLimitBy(add);
    }

    public static void ThrowException(Exception ex, [CallerFilePath] string fileName = "", [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string callerMemberName = "")
    {
        try
        {
            StackTrace st = new(1, true);
            StackFrame[] stFrames = st.GetFrames();

            StackFrame firstFrame = stFrames.FirstOrDefault();

            var sb = new StringBuilder();
            sb.Append($" Exception: {ex.Message}\n      thrown by {ex.Source}\n      at {ex.TargetSite}\n      in {fileName} at line {lineNumber} in {callerMemberName}\n------ Stack Trace ------");

            bool skip = true;
            foreach (StackFrame sf in stFrames)
            {
                if (skip)
                {
                    skip = false;
                    continue;
                }

                var callerMethod = sf.GetMethod();

                string callerMethodName = callerMethod?.Name;
                string callerClassName = callerMethod?.DeclaringType?.FullName;

                sb.Append($"\n      at {callerClassName}.{callerMethodName}");
            }

            sb.Append("\n------ End of Stack Trace ------");

            Logger.Error(sb.ToString(), firstFrame?.GetMethod()?.ToString(), multiLine: true);
        }
        catch
        {
        }
    }

    public static bool HasTasks(NetworkedPlayerInfo p, bool ForRecompute = true)
    {
        if (GameStates.IsLobby) return false;
        if (p.Tasks == null) return false;
        if (p.Role == null) return false;

        var hasTasks = true;
        var States = Main.PlayerStates[p.PlayerId];
        if (p.Disconnected) return false;
        if (p.Role.IsImpostor)
            hasTasks = false;
        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.SoloKombat: return false;
            case CustomGameMode.FFA: return false;
            case CustomGameMode.MoveAndStop: return true;
            case CustomGameMode.HotPotato: return false;
            case CustomGameMode.Speedrun: return true;
            case CustomGameMode.HideAndSeek: return HnSManager.HasTasks(p);
        }

        // if (Shifter.ForceDisableTasks(p.PlayerId)) return false;

        var role = States.MainRole;
        switch (role)
        {
            case CustomRoles.GM:
            case CustomRoles.Sheriff when !Options.UsePets.GetBool() || !Sheriff.UsePet.GetBool():
            case CustomRoles.Arsonist:
            case CustomRoles.Jackal:
            case CustomRoles.Sidekick:
            case CustomRoles.Poisoner:
            case CustomRoles.Eclipse:
            case CustomRoles.Pyromaniac:
            case CustomRoles.NSerialKiller:
            case CustomRoles.Beehive:
            case CustomRoles.RouleteGrandeur:
            case CustomRoles.Nonplus:
            case CustomRoles.Tremor:
            case CustomRoles.Evolver:
            case CustomRoles.Rogue:
            case CustomRoles.Patroller:
            case CustomRoles.Simon:
            case CustomRoles.Chemist:
            case CustomRoles.Samurai:
            case CustomRoles.QuizMaster:
            case CustomRoles.Bargainer:
            case CustomRoles.Tiger:
            case CustomRoles.SoulHunter:
            case CustomRoles.Enderman:
            case CustomRoles.Mycologist:
            case CustomRoles.Bubble:
            case CustomRoles.Hookshot:
            case CustomRoles.Sprayer:
            case CustomRoles.Doppelganger:
            case CustomRoles.PlagueDoctor:
            case CustomRoles.Postman:
            case CustomRoles.SchrodingersCat:
            case CustomRoles.Shifter:
            case CustomRoles.Impartial:
            case CustomRoles.Predator:
            case CustomRoles.Reckless:
            case CustomRoles.WeaponMaster:
            case CustomRoles.Magician:
            case CustomRoles.Vengeance:
            case CustomRoles.HeadHunter:
            case CustomRoles.Imitator:
            case CustomRoles.Werewolf:
            case CustomRoles.Bandit:
            case CustomRoles.Jailor when !Options.UsePets.GetBool() || !Jailor.UsePet.GetBool():
            case CustomRoles.Traitor:
            case CustomRoles.Glitch:
            case CustomRoles.Pickpocket:
            case CustomRoles.Maverick:
            case CustomRoles.Jinx:
            case CustomRoles.Parasite:
            case CustomRoles.Agitater:
            case CustomRoles.Crusader when !Options.UsePets.GetBool() || !Crusader.UsePet.GetBool():
            case CustomRoles.Refugee:
            case CustomRoles.Jester:
            case CustomRoles.Mario:
            case CustomRoles.Vulture:
            case CustomRoles.God:
            case CustomRoles.SwordsMan when !Options.UsePets.GetBool() || !SwordsMan.UsePet.GetBool():
            case CustomRoles.Innocent:
            case CustomRoles.Pelican:
            case CustomRoles.Medusa:
            case CustomRoles.Revolutionist:
            case CustomRoles.FFF:
            case CustomRoles.Gamer:
            case CustomRoles.HexMaster:
            case CustomRoles.Wraith:
            case CustomRoles.Juggernaut:
            case CustomRoles.Ritualist:
            case CustomRoles.DarkHide:
            case CustomRoles.Collector:
            case CustomRoles.ImperiusCurse:
            case CustomRoles.Provocateur:
            case CustomRoles.Medic when !Options.UsePets.GetBool() || !Medic.UsePet.GetBool():
            case CustomRoles.BloodKnight:
            case CustomRoles.Camouflager:
            case CustomRoles.Totocalcio:
            case CustomRoles.Romantic:
            case CustomRoles.VengefulRomantic:
            case CustomRoles.RuthlessRomantic:
            case CustomRoles.Succubus:
            case CustomRoles.Necromancer:
            case CustomRoles.Deathknight:
            case CustomRoles.Amnesiac:
            case CustomRoles.Monarch when !Options.UsePets.GetBool() || !Monarch.UsePet.GetBool():
            case CustomRoles.Deputy when !Options.UsePets.GetBool() || !Deputy.UsePet.GetBool():
            case CustomRoles.Virus:
            case CustomRoles.Farseer when !Options.UsePets.GetBool() || !Farseer.UsePet.GetBool():
            case CustomRoles.Aid when !Options.UsePets.GetBool() || !Aid.UsePet.GetBool():
            case CustomRoles.Socialite when !Options.UsePets.GetBool() || !Socialite.UsePet.GetBool():
            case CustomRoles.Escort when !Options.UsePets.GetBool() || !Escort.UsePet.GetBool():
            case CustomRoles.DonutDelivery when !Options.UsePets.GetBool() || !DonutDelivery.UsePet.GetBool():
            case CustomRoles.Gaulois when !Options.UsePets.GetBool() || !Gaulois.UsePet.GetBool():
            case CustomRoles.Analyst when !Options.UsePets.GetBool() || !Analyst.UsePet.GetBool():
            case CustomRoles.Witness when !Options.UsePets.GetBool() || !Options.WitnessUsePet.GetBool():
            case CustomRoles.Goose:
            case CustomRoles.Pursuer:
            case CustomRoles.Spiritcaller:
            case CustomRoles.PlagueBearer:
            case CustomRoles.Pestilence:
            case CustomRoles.Doomsayer:
                hasTasks = false;
                break;
            case CustomRoles.Workaholic:
            case CustomRoles.Terrorist:
            case CustomRoles.Sunnyboy:
            case CustomRoles.Convict:
            case CustomRoles.Opportunist:
            case CustomRoles.Executioner:
            case CustomRoles.Lawyer:
            case CustomRoles.Phantasm:
                if (ForRecompute)
                    hasTasks = false;
                break;
            case CustomRoles.Cherokious:
            case CustomRoles.Crewpostor:
                if (ForRecompute && !p.IsDead)
                    hasTasks = false;
                if (p.IsDead)
                    hasTasks = false;
                break;
            default:
                if (role.IsImpostor()) hasTasks = false;
                break;
        }

        foreach (CustomRoles subRole in States.SubRoles)
        {
            switch (subRole)
            {
                case CustomRoles.Madmate:
                case CustomRoles.Charmed:
                case CustomRoles.Recruit:
                case CustomRoles.Egoist:
                case CustomRoles.Contagious:
                case CustomRoles.Rascal:
                case CustomRoles.EvilSpirit:
                    hasTasks &= !ForRecompute;
                    break;
                case CustomRoles.Bloodlust:
                    hasTasks = false;
                    break;
                default:
                    if (subRole.IsGhostRole()) hasTasks = true;
                    break;
            }
        }

        if (CopyCat.Instances.Any(x => x.CopyCatPC.PlayerId == p.PlayerId) && ForRecompute && (!Options.UsePets.GetBool() || CopyCat.UsePet.GetBool())) hasTasks = false;

        hasTasks |= role.UsesPetInsteadOfKill();

        return hasTasks;
    }

    public static bool CanBeMadmate(this PlayerControl pc)
    {
        return pc != null && pc.IsCrewmate() && !pc.Is(CustomRoles.Madmate)
               && !(
                   (pc.Is(CustomRoles.Sheriff) && !Options.SheriffCanBeMadmate.GetBool()) ||
                   (pc.Is(CustomRoles.Mayor) && !Options.MayorCanBeMadmate.GetBool()) ||
                   (pc.Is(CustomRoles.NiceGuesser) && !Options.NGuesserCanBeMadmate.GetBool()) ||
                   (pc.Is(CustomRoles.Snitch) && !Options.SnitchCanBeMadmate.GetBool()) ||
                   (pc.Is(CustomRoles.Judge) && !Options.JudgeCanBeMadmate.GetBool()) ||
                   (pc.Is(CustomRoles.Marshall) && !Options.MarshallCanBeMadmate.GetBool()) ||
                   (pc.Is(CustomRoles.Farseer) && !Options.FarseerCanBeMadmate.GetBool()) ||
                   pc.Is(CustomRoles.NiceSwapper) ||
                   pc.Is(CustomRoles.Needy) ||
                   pc.Is(CustomRoles.Lazy) ||
                   pc.Is(CustomRoles.Loyal) ||
                   pc.Is(CustomRoles.SuperStar) ||
                   pc.Is(CustomRoles.CyberStar) ||
                   pc.Is(CustomRoles.Demolitionist) ||
                   pc.Is(CustomRoles.NiceEraser) ||
                   pc.Is(CustomRoles.Egoist) ||
                   pc.Is(CustomRoles.DualPersonality)
               );
    }

    public static bool IsRoleTextEnabled(PlayerControl __instance)
    {
        if (__instance.PlayerId == PlayerControl.LocalPlayer.PlayerId || Options.CurrentGameMode is CustomGameMode.FFA or CustomGameMode.SoloKombat or CustomGameMode.MoveAndStop or CustomGameMode.HotPotato or CustomGameMode.Speedrun || (Options.CurrentGameMode == CustomGameMode.HideAndSeek && HnSManager.IsRoleTextEnabled(PlayerControl.LocalPlayer, __instance)) || Main.VisibleTasksCount && PlayerControl.LocalPlayer.Data.IsDead && Options.GhostCanSeeOtherRoles.GetBool() || PlayerControl.LocalPlayer.Is(CustomRoles.Mimic) && Main.VisibleTasksCount && __instance.Data.IsDead && Options.MimicCanSeeDeadRoles.GetBool()) return true;

        switch (__instance.GetCustomRole())
        {
            case CustomRoles.Crewpostor when PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && Options.CrewpostorKnowsAllies.GetBool():
            case CustomRoles.Jackal when PlayerControl.LocalPlayer.Is(CustomRoles.Jackal):
            case CustomRoles.Jackal when PlayerControl.LocalPlayer.Is(CustomRoles.Sidekick):
            case CustomRoles.Jackal when PlayerControl.LocalPlayer.Is(CustomRoles.Recruit):
            case CustomRoles.Recruit when PlayerControl.LocalPlayer.Is(CustomRoles.Jackal):
            case CustomRoles.Recruit when PlayerControl.LocalPlayer.Is(CustomRoles.Sidekick):
            case CustomRoles.Recruit when PlayerControl.LocalPlayer.Is(CustomRoles.Recruit):
            case CustomRoles.Sidekick when PlayerControl.LocalPlayer.Is(CustomRoles.Jackal):
            case CustomRoles.Sidekick when PlayerControl.LocalPlayer.Is(CustomRoles.Sidekick):
            case CustomRoles.Sidekick when PlayerControl.LocalPlayer.Is(CustomRoles.Recruit):
            case CustomRoles.Workaholic when Workaholic.WorkaholicVisibleToEveryone.GetBool():
            case CustomRoles.Doctor when !__instance.HasEvilAddon() && Options.DoctorVisibleToEveryone.GetBool():
            case CustomRoles.Mayor when Mayor.MayorRevealWhenDoneTasks.GetBool() && __instance.GetTaskState().IsTaskFinished:
            case CustomRoles.Marshall when PlayerControl.LocalPlayer.Is(CustomRoleTypes.Crewmate) && __instance.GetTaskState().IsTaskFinished:
                return true;
        }

        return __instance.Is(CustomRoles.Madmate) && PlayerControl.LocalPlayer.Is(CustomRoles.Madmate) && Options.MadmateKnowWhosMadmate.GetBool() ||
               __instance.Is(CustomRoles.Mimic) && Main.VisibleTasksCount && __instance.Data.IsDead ||
               __instance.Is(CustomRoles.Madmate) && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && Options.ImpKnowWhosMadmate.GetBool() ||
               __instance.Is(CustomRoleTypes.Impostor) && PlayerControl.LocalPlayer.Is(CustomRoles.Crewpostor) && Options.AlliesKnowCrewpostor.GetBool() ||
               __instance.Is(CustomRoleTypes.Impostor) && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && Options.ImpKnowAlliesRole.GetBool() ||
               __instance.Is(CustomRoleTypes.Impostor) && PlayerControl.LocalPlayer.Is(CustomRoles.Madmate) && Options.MadmateKnowWhosImp.GetBool() ||
               Main.LoversPlayers.TrueForAll(x => x.PlayerId == __instance.PlayerId || x.PlayerId == PlayerControl.LocalPlayer.PlayerId) && Main.LoversPlayers.Count == 2 && Lovers.LoverKnowRoles.GetBool() ||
               CustomTeamManager.AreInSameCustomTeam(__instance.PlayerId, PlayerControl.LocalPlayer.PlayerId) && CustomTeamManager.IsSettingEnabledForPlayerTeam(__instance.PlayerId, CTAOption.KnowRoles) ||
               Main.PlayerStates.Values.Any(x => x.Role.KnowRole(PlayerControl.LocalPlayer, __instance)) ||
               PlayerControl.LocalPlayer.IsRevealedPlayer(__instance) ||
               PlayerControl.LocalPlayer.Is(CustomRoles.God) ||
               PlayerControl.LocalPlayer.Is(CustomRoles.GM) ||
               Markseeker.PlayerIdList.Any(x => Main.PlayerStates[x].Role is Markseeker { IsEnable: true, TargetRevealed: true } ms && ms.MarkedId == __instance.PlayerId) ||
               Main.GodMode.Value;
    }

    public static string GetFormattedRoomName(string roomName) => roomName == "Outside" ? "<#00ffa5>Outside</color>" : $"<#ffffff>In</color> <#00ffa5>{roomName}</color>";
    public static string GetFormattedVectorText(Vector2 pos) => $"<#777777>(at {pos.ToString().Replace("(", string.Empty).Replace(")", string.Empty)})</color>";

    public static string GetProgressText(PlayerControl pc)
    {
        var taskState = pc.GetTaskState();
        var Comms = false;
        if (taskState.hasTasks)
        {
            if (IsActive(SystemTypes.Comms)) Comms = true;
            if (Camouflager.IsActive) Comms = true;
            //if (PlayerControl.LocalPlayer.myTasks.ToArray().Any(x => x.TaskType == TaskTypes.FixComms)) Comms = true;
        }

        return GetProgressText(pc.PlayerId, Comms);
    }

    public static string GetProgressText(byte playerId, bool comms = false)
    {
        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.MoveAndStop: return GetTaskCount(playerId, comms, moveAndStop: true);
            case CustomGameMode.Speedrun: return string.Empty;
        }

        var ProgressText = new StringBuilder();
        PlayerControl pc = GetPlayerById(playerId);
        try
        {
            ProgressText.Append(Main.PlayerStates[playerId].Role.GetProgressText(playerId, comms));
        }
        catch (Exception ex)
        {
            Logger.Error($"For {pc.GetNameWithRole().RemoveHtmlTags()}, failed to get progress text:  " + ex, "Utils.GetProgressText");
        }

        if (pc.Is(CustomRoles.Damocles)) ProgressText.Append($" {Damocles.GetProgressText(playerId)}");
        if (pc.Is(CustomRoles.Stressed)) ProgressText.Append($" {Stressed.GetProgressText(playerId)}");
        if (pc.Is(CustomRoles.Circumvent)) ProgressText.Append($" {Circumvent.GetProgressText(playerId)}");
        if (pc.Is(CustomRoles.Taskcounter))
        {
            string totalCompleted = comms ? "?" : $"{GameData.Instance.CompletedTasks}";
            ProgressText.Append($" <#00ffa5>{totalCompleted}</color><#ffffff>/{GameData.Instance.TotalTasks}</color>");
        }

        if (ProgressText.Length != 0 && !ProgressText.ToString().RemoveHtmlTags().StartsWith(' '))
            ProgressText.Insert(0, ' ');

        return ProgressText.ToString();
    }

    public static string GetAbilityUseLimitDisplay(byte playerId, bool usingAbility = false)
    {
        try
        {
            float limit = playerId.GetAbilityUseLimit();
            if (float.IsNaN(limit) /* || limit is > 100 or < 0*/) return string.Empty;
            Color TextColor;
            if (limit < 1) TextColor = Color.red;
            else if (usingAbility) TextColor = Color.green;
            else TextColor = GetRoleColor(Main.PlayerStates[playerId].MainRole).ShadeColor(0.25f);
            return ColorString(TextColor, $" ({Math.Round(limit, 1)})");
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string GetTaskCount(byte playerId, bool comms, bool moveAndStop = false)
    {
        try
        {
            var taskState = Main.PlayerStates[playerId].TaskState;
            if (!taskState.hasTasks) return string.Empty;

            var info = GetPlayerInfoById(playerId);
            var TaskCompleteColor = HasTasks(info) ? Color.green : GetRoleColor(Main.PlayerStates[playerId].MainRole).ShadeColor(0.5f);
            var NonCompleteColor = HasTasks(info) ? Color.yellow : Color.white;

            if (Workhorse.IsThisRole(playerId))
                NonCompleteColor = Workhorse.RoleColor;

            var NormalColor = taskState.IsTaskFinished ? TaskCompleteColor : NonCompleteColor;
            if (Main.PlayerStates.TryGetValue(playerId, out var ps))
            {
                NormalColor = ps.MainRole switch
                {
                    CustomRoles.Crewpostor => Color.red,
                    CustomRoles.Cherokious => GetRoleColor(CustomRoles.Cherokious),
                    _ => NormalColor
                };
            }

            Color TextColor = comms ? Color.gray : NormalColor;
            string Completed = comms ? "?" : $"{taskState.CompletedTasksCount}";
            return ColorString(TextColor, $" {(moveAndStop ? "<size=1.6>" : string.Empty)}{Completed}/{taskState.AllTasksCount}{(moveAndStop ? "</size>" : string.Empty)}");
        }
        catch
        {
            return string.Empty;
        }
    }

    public static void ShowActiveSettingsHelp(byte PlayerId = byte.MaxValue)
    {
        SendMessage(GetString("CurrentActiveSettingsHelp") + ":", PlayerId);

        if (Options.DisableDevices.GetBool())
        {
            SendMessage(GetString("DisableDevicesInfo"), PlayerId);
        }

        if (Options.SyncButtonMode.GetBool())
        {
            SendMessage(GetString("SyncButtonModeInfo"), PlayerId);
        }

        if (Options.SabotageTimeControl.GetBool())
        {
            SendMessage(GetString("SabotageTimeControlInfo"), PlayerId);
        }

        if (Options.RandomMapsMode.GetBool())
        {
            SendMessage(GetString("RandomMapsModeInfo"), PlayerId);
        }

        if (Main.GM.Value)
        {
            SendMessage(GetRoleName(CustomRoles.GM) + GetString("GMInfoLong"), PlayerId);
        }

        foreach (var role in Enum.GetValues<CustomRoles>().Where(role => role.IsEnable() && !role.IsVanilla()))
        {
            SendMessage(GetRoleName(role) + GetRoleMode(role) + GetString(Enum.GetName(typeof(CustomRoles), role) + "InfoLong"), PlayerId);
        }

        if (Options.NoGameEnd.GetBool())
        {
            SendMessage(GetString("NoGameEndInfo"), PlayerId);
        }
    }

    /// <summary>
    /// Gets all players within a specified radius from the specified location
    /// </summary>
    /// <param name="radius">The radius</param>
    /// <param name="from">The location which the radius is counted from</param>
    /// <returns>A list containing all PlayerControls within the specified range from the specified location</returns>
    public static IEnumerable<PlayerControl> GetPlayersInRadius(float radius, Vector2 from) => from tg in Main.AllAlivePlayerControls let dis = Vector2.Distance(@from, tg.Pos()) where !Pelican.IsEaten(tg.PlayerId) && !tg.inVent where dis <= radius select tg;

    public static void ShowActiveSettings(byte PlayerId = byte.MaxValue)
    {
        if (Options.HideGameSettings.GetBool() && PlayerId != byte.MaxValue)
        {
            SendMessage(GetString("Message.HideGameSettings"), PlayerId);
            return;
        }

        if (Options.DIYGameSettings.GetBool())
        {
            SendMessage(GetString("Message.NowOverrideText"), PlayerId);
            return;
        }

        var sb = new StringBuilder();
        sb.Append(" ★ " + GetString("TabGroup.SystemSettings"));
        foreach (var opt in OptionItem.AllOptions.Where(x => x.GetBool() && x.Parent == null && x.Tab is TabGroup.SystemSettings && !x.IsHiddenOn(Options.CurrentGameMode)))
        {
            sb.Append($"\n{opt.GetName(true)}: {opt.GetString()}");
            //ShowChildrenSettings(opt, ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }

        sb.Append("\n\n ★ " + GetString("TabGroup.GameSettings"));
        foreach (var opt in OptionItem.AllOptions.Where(x => x.GetBool() && x.Parent == null && x.Tab is TabGroup.GameSettings && !x.IsHiddenOn(Options.CurrentGameMode)))
        {
            sb.Append($"\n{opt.GetName(true)}: {opt.GetString()}");
            //ShowChildrenSettings(opt, ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }

        SendMessage(sb.ToString(), PlayerId);
    }

    public static void ShowAllActiveSettings(byte PlayerId = byte.MaxValue)
    {
        if (Options.HideGameSettings.GetBool() && PlayerId != byte.MaxValue)
        {
            SendMessage(GetString("Message.HideGameSettings"), PlayerId);
            return;
        }

        if (Options.DIYGameSettings.GetBool())
        {
            SendMessage(GetString("Message.NowOverrideText"), PlayerId);
            return;
        }

        var sb = new StringBuilder();

        sb.Append(GetString("Settings")).Append(':');
        foreach (var role in Options.CustomRoleCounts)
        {
            if (!role.Key.IsEnable()) continue;

            string mode;
            try
            {
                mode = !role.Key.IsAdditionRole()
                    ? GetString($"Rate{role.Key.GetMode()}")
                    : GetString($"Rate{Options.CustomAdtRoleSpawnRate[role.Key].GetInt()}");
            }
            catch (KeyNotFoundException)
            {
                continue;
            }

            mode = mode.Replace("color=", string.Empty);

            sb.Append($"\n【{GetRoleName(role.Key)}:{mode} ×{role.Key.GetCount()}】\n");
            ShowChildrenSettings(Options.CustomRoleSpawnChances[role.Key], ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }

        foreach (var opt in OptionItem.AllOptions)
        {
            if (opt.GetBool() && opt.Parent == null && opt.Id is >= 80000 and < 640000 && !opt.IsHiddenOn(Options.CurrentGameMode))
            {
                if (opt.Name is "KillFlashDuration" or "RoleAssigningAlgorithm")
                    sb.Append($"\n【{opt.GetName(true)}: {opt.GetString()}】\n");
                else
                    sb.Append($"\n【{opt.GetName(true)}】\n");
                ShowChildrenSettings(opt, ref sb);
                var text = sb.ToString();
                sb.Clear().Append(text.RemoveHtmlTags());
            }
        }

        SendMessage(sb.ToString(), PlayerId);
    }

    public static void CopyCurrentSettings()
    {
        var sb = new StringBuilder();
        if (Options.HideGameSettings.GetBool() && !AmongUsClient.Instance.AmHost)
        {
            ClipboardHelper.PutClipboardString(GetString("Message.HideGameSettings"));
            return;
        }

        sb.Append($"━━━━━━━━━━━━【{GetString("Roles")}】━━━━━━━━━━━━");
        foreach (var role in Options.CustomRoleCounts)
        {
            if (!role.Key.IsEnable()) continue;

            string mode;
            try
            {
                mode = !role.Key.IsAdditionRole()
                    ? GetString($"Rate{role.Key.GetMode()}")
                    : GetString($"Rate{Options.CustomAdtRoleSpawnRate[role.Key].GetInt()}");
            }
            catch (KeyNotFoundException)
            {
                continue;
            }

            mode = mode.Replace("color=", string.Empty);

            sb.Append($"\n【{GetRoleName(role.Key)}:{mode} ×{role.Key.GetCount()}】\n");
            ShowChildrenSettings(Options.CustomRoleSpawnChances[role.Key], ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }

        sb.Append($"━━━━━━━━━━━━【{GetString("Settings")}】━━━━━━━━━━━━");
        foreach (var opt in OptionItem.AllOptions.Where(x => x.GetBool() && x.Parent == null && x.Id is >= 80000 and < 640000 && !x.IsHiddenOn(Options.CurrentGameMode)))
        {
            if (opt.Name == "KillFlashDuration")
                sb.Append($"\n【{opt.GetName(true)}: {opt.GetString()}】\n");
            else
                sb.Append($"\n【{opt.GetName(true)}】\n");
            ShowChildrenSettings(opt, ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }

        sb.Append("\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501");
        ClipboardHelper.PutClipboardString(sb.ToString());
    }

    public static void ShowActiveRoles(byte PlayerId = byte.MaxValue)
    {
        if (Options.HideGameSettings.GetBool() && PlayerId != byte.MaxValue)
        {
            SendMessage(GetString("Message.HideGameSettings"), PlayerId);
            return;
        }

        var sb = new StringBuilder();
        sb.Append($"\n{GetRoleName(CustomRoles.GM)}: {(Main.GM.Value ? GetString("RoleRate") : GetString("RoleOff"))}");

        var impsb = new StringBuilder();
        var neutralsb = new StringBuilder();
        var crewsb = new StringBuilder();
        var addonsb = new StringBuilder();

        foreach (var role in Options.CurrentGameMode == CustomGameMode.HideAndSeek ? HnSManager.AllHnSRoles : Enum.GetValues<CustomRoles>().Except(HnSManager.AllHnSRoles))
        {
            string mode;
            try
            {
                mode = !role.IsAdditionRole()
                    ? GetString($"Rate{role.GetMode()}")
                    : GetString($"Rate{Options.CustomAdtRoleSpawnRate[role].GetInt()}");
            }
            catch (KeyNotFoundException)
            {
                continue;
            }

            mode = mode.Replace("color=", string.Empty);

            if (role.IsEnable())
            {
                var roleDisplay = $"\n{GetRoleName(role)}: {mode} x{role.GetCount()}";
                if (role.IsAdditionRole()) addonsb.Append(roleDisplay);
                else if (role.IsCrewmate()) crewsb.Append(roleDisplay);
                else if (role.IsImpostor() || role.IsMadmate()) impsb.Append(roleDisplay);
                else if (role.IsNeutral()) neutralsb.Append(roleDisplay);
            }
        }

        SendMessage(sb.Append("\n.").ToString(), PlayerId, "<color=#ff5b70>【 ★ Roles ★ 】</color>");
        SendMessage(impsb.Append("\n.").ToString(), PlayerId, ColorString(GetRoleColor(CustomRoles.Impostor), "【 ★ Impostor Roles ★ 】"));
        SendMessage(crewsb.Append("\n.").ToString(), PlayerId, ColorString(GetRoleColor(CustomRoles.Crewmate), "【 ★ Crewmate Roles ★ 】"));
        SendMessage(neutralsb.Append("\n.").ToString(), PlayerId, "<color=#ffab1b>【 ★ Neutral Roles ★ 】</color>");
        SendMessage(addonsb.Append("\n.").ToString(), PlayerId, "<color=#ff9ace>【 ★ Add-ons ★ 】</color>");
    }

    public static void ShowChildrenSettings(OptionItem option, ref StringBuilder sb, int deep = 0, bool command = false, bool disableColor = true)
    {
        foreach (var opt in option.Children.Select((v, i) => new { Value = v, Index = i + 1 }))
        {
            if (command)
            {
                sb.Append("\n\n");
                command = false;
            }

            switch (opt.Value.Name)
            {
                case "DisableSkeldDevices" when Main.CurrentMap is not MapNames.Skeld and not MapNames.Dleks:
                case "DisableMiraHQDevices" when Main.CurrentMap != MapNames.Mira:
                case "DisablePolusDevices" when Main.CurrentMap != MapNames.Polus:
                case "DisableAirshipDevices" when Main.CurrentMap != MapNames.Airship:
                case "PolusReactorTimeLimit" when Main.CurrentMap != MapNames.Polus:
                case "AirshipReactorTimeLimit" when Main.CurrentMap != MapNames.Airship:
                    continue;
            }

            if (deep > 0)
            {
                sb.Append(string.Concat(Enumerable.Repeat("┃", Mathf.Max(deep - 1, 0))));
                sb.Append(opt.Index == option.Children.Count ? "┗ " : "┣ ");
            }

            var value = opt.Value.GetString().Replace("ON", "<#00ffa5>ON</color>").Replace("OFF", "<#ff0000>OFF</color>");
            string name = $"{opt.Value.GetName(disableColor: disableColor).Replace("color=", string.Empty)}</color>";
            sb.Append($"{name}: <#ffff00>{value}</color>\n");
            if (opt.Value.GetBool()) ShowChildrenSettings(opt.Value, ref sb, deep + 1, disableColor: disableColor);
        }
    }

    public static void ShowLastRoles(byte PlayerId = byte.MaxValue)
    {
        if (AmongUsClient.Instance.IsGameStarted)
        {
            SendMessage(GetString("CantUse.lastroles"), PlayerId);
            return;
        }

        var sb = new StringBuilder();

        sb.Append("<#ffffff><u>Role Summary:</u></color><size=70%>");

        List<byte> cloneRoles = [.. Main.PlayerStates.Keys];
        foreach (byte id in Main.WinnerList)
        {
            try
            {
                if (EndGamePatch.SummaryText[id].Contains("<INVALID:NotAssigned>")) continue;
                sb.Append("\n<#c4aa02>\u2605</color> ").Append(EndGamePatch.SummaryText[id] /*.RemoveHtmlTags()*/);
                cloneRoles.Remove(id);
            }
            catch (Exception ex)
            {
                ThrowException(ex);
            }
        }

        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.SoloKombat:
                List<(int, byte)> list = [];
                list.AddRange(cloneRoles.Select(id => (SoloKombatManager.GetRankOfScore(id), id)));

                list.Sort();
                foreach ((int, byte) id in list)
                {
                    try
                    {
                        sb.Append("\n\u3000 ").Append(EndGamePatch.SummaryText[id.Item2]);
                    }
                    catch (Exception ex)
                    {
                        ThrowException(ex);
                    }
                }

                break;
            case CustomGameMode.FFA:
                List<(int, byte)> list2 = [];
                list2.AddRange(cloneRoles.Select(id => (FFAManager.GetRankOfScore(id), id)));

                list2.Sort();
                foreach ((int, byte) id in list2)
                {
                    try
                    {
                        sb.Append("\n\u3000 ").Append(EndGamePatch.SummaryText[id.Item2]);
                    }
                    catch (Exception ex)
                    {
                        ThrowException(ex);
                    }
                }

                break;
            case CustomGameMode.MoveAndStop:
                List<(int, byte)> list3 = [];
                list3.AddRange(cloneRoles.Select(id => (MoveAndStopManager.GetRankOfScore(id), id)));

                list3.Sort();
                foreach ((int, byte) id in list3)
                {
                    try
                    {
                        sb.Append("\n\u3000 ").Append(EndGamePatch.SummaryText[id.Item2]);
                    }
                    catch (Exception ex)
                    {
                        ThrowException(ex);
                    }
                }

                break;
            case CustomGameMode.Speedrun:
            case CustomGameMode.HotPotato:
            case CustomGameMode.HideAndSeek:
                foreach (byte id in cloneRoles)
                {
                    try
                    {
                        sb.Append("\n\u3000 ").Append(EndGamePatch.SummaryText[id]);
                    }
                    catch (Exception ex)
                    {
                        ThrowException(ex);
                    }
                }

                break;
            default:
                foreach (byte id in cloneRoles)
                {
                    try
                    {
                        if (EndGamePatch.SummaryText[id].Contains("<INVALID:NotAssigned>")) continue;
                        sb.Append("\n\u3000 ").Append(EndGamePatch.SummaryText[id]);
                    }
                    catch (Exception ex)
                    {
                        ThrowException(ex);
                    }
                }

                break;
        }

        sb.Append("</size>");

        SendMessage("\n", PlayerId, sb.ToString());
    }

    public static void ShowKillLog(byte PlayerId = byte.MaxValue)
    {
        if (GameStates.IsInGame)
        {
            SendMessage(GetString("CantUse.killlog"), PlayerId);
            return;
        }

        if (EndGamePatch.KillLog != string.Empty) SendMessage(EndGamePatch.KillLog, PlayerId);
    }

    public static void ShowLastResult(byte PlayerId = byte.MaxValue)
    {
        if (GameStates.IsInGame)
        {
            SendMessage(GetString("CantUse.lastresult"), PlayerId);
            return;
        }

        if (Options.CurrentGameMode != CustomGameMode.Standard) return;

        var sb = new StringBuilder();
        if (SetEverythingUpPatch.LastWinsText != string.Empty) sb.Append($"<size=90%>{GetString("LastResult")} {SetEverythingUpPatch.LastWinsText}</size>");
        if (SetEverythingUpPatch.LastWinsReason != string.Empty) sb.Append($"\n<size=90%>{GetString("LastEndReason")} {SetEverythingUpPatch.LastWinsReason}</size>");
        if (sb.Length > 0) SendMessage("\n", PlayerId, sb.ToString());
    }

    public static void ShowLastAddOns(byte PlayerId = byte.MaxValue)
    {
        if (GameStates.IsInGame)
        {
            SendMessage(GetString("CantUse.lastresult"), PlayerId);
            return;
        }

        if (Options.CurrentGameMode != CustomGameMode.Standard) return;

        var result = Main.LastAddOns.Values.Join(delimiter: "\n");
        SendMessage("\n", PlayerId, result);
    }

    public static string GetSubRolesText(byte id, bool disableColor = false, bool intro = false, bool summary = false)
    {
        var SubRoles = Main.PlayerStates[id].SubRoles;
        if (SubRoles.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        if (intro)
        {
            bool isLovers = SubRoles.Contains(CustomRoles.Lovers) && Main.PlayerStates[id].MainRole is not CustomRoles.LovingCrewmate and not CustomRoles.LovingImpostor;
            SubRoles.RemoveAll(x => x is CustomRoles.NotAssigned or CustomRoles.LastImpostor or CustomRoles.Lovers);

            if (isLovers)
            {
                sb.Append($"{ColorString(GetRoleColor(CustomRoles.Lovers), " ♥")}");
            }

            if (SubRoles.Count == 0) return sb.ToString();

            sb.Append("<size=15%>");
            if (SubRoles.Count == 1)
            {
                CustomRoles role = SubRoles[0];

                var RoleText = ColorString(GetRoleColor(role), GetRoleName(role));
                sb.Append($"{ColorString(Color.gray, GetString("Modifier"))}{RoleText}");
            }
            else
            {
                sb.Append($"{ColorString(Color.gray, GetString("Modifiers"))}");
                for (int i = 0; i < SubRoles.Count; i++)
                {
                    if (i != 0) sb.Append(", ");
                    CustomRoles role = SubRoles[i];

                    var RoleText = ColorString(GetRoleColor(role), GetRoleName(role));
                    sb.Append(RoleText);
                }
            }

            sb.Append("</size>");
        }
        else if (!summary)
        {
            foreach (CustomRoles role in SubRoles)
            {
                if (role is CustomRoles.NotAssigned or CustomRoles.LastImpostor) continue;
                var RoleText = disableColor ? GetRoleName(role) : ColorString(GetRoleColor(role), GetRoleName(role));
                sb.Append($"{ColorString(Color.gray, " + ")}{RoleText}");
            }
        }

        return sb.ToString();
    }

    public static byte MsgToColor(string text, bool isHost = false)
    {
        text = text.ToLowerInvariant();
        text = text.Replace("色", string.Empty);
        int color;
        try
        {
            color = int.Parse(text);
        }
        catch
        {
            color = -1;
        }

        color = text switch
        {
            "0" or "红" or "紅" or "red" or "Red" or "крас" or "Крас" or "красн" or "Красн" or "красный" or "Красный" => 0,
            "1" or "蓝" or "藍" or "深蓝" or "blue" or "Blue" or "син" or "Син" or "синий" or "Синий" => 1,
            "2" or "绿" or "綠" or "深绿" or "green" or "Green" or "Зел" or "зел" or "Зелёный" or "Зеленый" or "зелёный" or "зеленый" => 2,
            "3" or "粉红" or "pink" or "Pink" or "Роз" or "роз" or "Розовый" or "розовый" => 3,
            "4" or "橘" or "orange" or "Orange" or "оранж" or "Оранж" or "оранжевый" or "Оранжевый" => 4,
            "5" or "黄" or "黃" or "yellow" or "Yellow" or "Жёлт" or "Желт" or "жёлт" or "желт" or "Жёлтый" or "Желтый" or "жёлтый" or "желтый" => 5,
            "6" or "黑" or "black" or "Black" or "Чёрный" or "Черный" or "чёрный" or "черный" => 6,
            "7" or "白" or "white" or "White" or "Белый" or "белый" => 7,
            "8" or "紫" or "purple" or "Purple" or "Фиол" or "фиол" or "Фиолетовый" or "фиолетовый" => 8,
            "9" or "棕" or "brown" or "Brown" or "Корич" or "корич" or "Коричневый" or "коричевый" => 9,
            "10" or "青" or "cyan" or "Cyan" or "Голуб" or "голуб" or "Голубой" or "голубой" => 10,
            "11" or "黄绿" or "黃綠" or "浅绿" or "lime" or "Lime" or "Лайм" or "лайм" or "Лаймовый" or "лаймовый" => 11,
            "12" or "红褐" or "紅褐" or "深红" or "maroon" or "Maroon" or "Борд" or "борд" or "Бордовый" or "бордовый" => 12,
            "13" or "玫红" or "玫紅" or "浅粉" or "rose" or "Rose" or "Светло роз" or "светло роз" or "Светло розовый" or "светло розовый" or "Сирень" or "сирень" or "Сиреневый" or "сиреневый" => 13,
            "14" or "焦黄" or "焦黃" or "淡黄" or "banana" or "Banana" or "Банан" or "банан" or "Банановый" or "банановый" => 14,
            "15" or "灰" or "gray" or "Gray" or "Сер" or "сер" or "Серый" or "серый" => 15,
            "16" or "茶" or "tan" or "Tan" or "Загар" or "загар" or "Загаровый" or "загаровый" => 16,
            "17" or "珊瑚" or "coral" or "Coral" or "Корал" or "корал" or "Коралл" or "коралл" or "Коралловый" or "коралловый" => 17,
            "18" or "隐藏" or "?" => 18,
            _ => color
        };

        return !isHost && color == 18 ? byte.MaxValue : color is < 0 or > 18 ? byte.MaxValue : Convert.ToByte(color);
    }

    public static void ShowHelp(byte ID)
    {
        var player = GetPlayerById(ID);
        SendMessage(ChatCommands.AllCommands.Where(x => x.CanUseCommand(player)).Aggregate("<size=70%>", (s, c) => s + $"\n<b>/{c.CommandForms.Where(f => f.All(char.IsAscii)).MinBy(f => f.Length)}{(c.Arguments.Length == 0 ? string.Empty : $" {c.Arguments.Split(' ').Select((x, i) => ColorString(GetColor(i), x)).Join(delimiter: " ")}")}</b> \u2192 {c.Description}"), ID, title: GetString("CommandList"));
        return;

        Color GetColor(int i) => i switch
        {
            0 => Palette.Orange,
            1 => Color.magenta,
            2 => Color.blue,
            3 => Color.red,
            4 => Palette.Brown,
            5 => Color.cyan,
            6 => Color.green,
            7 => Palette.Purple,

            _ => Color.yellow
        };
    }

    public static void CheckTerroristWin(NetworkedPlayerInfo Terrorist)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        var taskState = GetPlayerById(Terrorist.PlayerId).GetTaskState();
        if (taskState.IsTaskFinished && (!Main.PlayerStates[Terrorist.PlayerId].IsSuicide || Options.CanTerroristSuicideWin.GetBool()))
        {
            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                if (pc.Is(CustomRoles.Terrorist))
                {
                    Main.PlayerStates[pc.PlayerId].deathReason = Main.PlayerStates[pc.PlayerId].deathReason == PlayerState.DeathReason.Vote ? PlayerState.DeathReason.etc : PlayerState.DeathReason.Suicide;
                }
                else if (!pc.Data.IsDead)
                {
                    pc.Suicide(PlayerState.DeathReason.Bombed, Terrorist.Object);
                }
            }

            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Terrorist);
            CustomWinnerHolder.WinnerIds.Add(Terrorist.PlayerId);
        }
    }

    public static void CheckAndSpawnAdditionalRefugee(NetworkedPlayerInfo deadPlayer)
    {
        if (Options.CurrentGameMode != CustomGameMode.Standard || deadPlayer == null || deadPlayer.Object.Is(CustomRoles.Refugee) || Main.HasJustStarted || !GameStates.InGame || !Options.SpawnAdditionalRefugeeOnImpsDead.GetBool() || Main.AllAlivePlayerControls.Length < Options.SpawnAdditionalRefugeeMinAlivePlayers.GetInt() || CustomRoles.Refugee.RoleExist(countDead: true) || Main.AllAlivePlayerControls == null || Main.AllAlivePlayerControls.Length == 0 || Main.AllAlivePlayerControls.Any(x => x.PlayerId != deadPlayer.PlayerId && (x.Is(CustomRoleTypes.Impostor) || (x.IsNeutralKiller() && !Options.SpawnAdditionalRefugeeWhenNKAlive.GetBool())))) return;

        PlayerControl[] ListToChooseFrom = Options.UsePets.GetBool() ? Main.AllAlivePlayerControls.Where(x => x.PlayerId != deadPlayer.PlayerId && x.Is(CustomRoleTypes.Crewmate) && !x.Is(CustomRoles.Loyal)).ToArray() : Main.AllAlivePlayerControls.Where(x => x.PlayerId != deadPlayer.PlayerId && x.Is(CustomRoleTypes.Crewmate) && x.GetCustomRole().GetRoleTypes() == RoleTypes.Impostor && !x.Is(CustomRoles.Loyal)).ToArray();

        if (ListToChooseFrom.Length > 0)
        {
            var pc = ListToChooseFrom.RandomElement();
            pc.RpcSetCustomRole(CustomRoles.Refugee);
            pc.SetKillCooldown();
            Main.PlayerStates[pc.PlayerId].RemoveSubRole(CustomRoles.Madmate);
            Logger.Warn($"{pc.GetRealName()} is now a Refugee since all Impostors are dead", "Add Refugee");
        }
        else Logger.Msg("No Player to change to Refugee.", "Add Refugee");
    }

    public static int ToInt(this string input)
    {
        using MD5 md5 = MD5.Create();
        byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));

        int hashInt = BitConverter.ToInt32(hashBytes, 0);

        hashInt = Math.Abs(hashInt);

        string hashStr = hashInt.ToString().PadLeft(8, '0');
        if (hashStr.Length > 8)
        {
            hashStr = hashStr[..8];
        }

        return int.Parse(hashStr);
    }

    public static void SendMessage(string text, byte sendTo = byte.MaxValue, string title = "", bool noSplit = false)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (title == "") title = "<color=#8b32a8>" + GetString("DefaultSystemMessageTitle") + "</color>";
        if (title.Count(x => x == '\u2605') == 2 && !title.Contains('\n'))
        {
            if (title.Contains('<') && title.Contains('>') && title.Contains('#'))
                title = $"{title[..(title.IndexOf('>') + 1)]}\u27a1{title.Replace("\u2605", "")[..(title.LastIndexOf('<') - 2)]}\u2b05";
            else title = "\u27a1" + title.Replace("\u2605", "") + "\u2b05";
        }

        text = text.Replace("color=", string.Empty);

        if (text.Length >= 1200 && !noSplit)
        {
            var lines = text.Split('\n');
            var shortenedText = string.Empty;
            foreach (string line in lines)
            {
                if (shortenedText.Length + line.Length < 1200)
                {
                    shortenedText += line + "\n";
                    continue;
                }

                if (shortenedText.Length >= 1200) shortenedText.Chunk(1200).Do(x => SendMessage(new(x), sendTo, title, true));
                else SendMessage(shortenedText, sendTo, title, true);

                var sentText = shortenedText;
                shortenedText = line + "\n";

                if (sentText.Contains("<size") && !sentText.Contains("</size>"))
                {
                    var sizeTag = Regex.Match(sentText, @"<size=\d+\.?\d*%?>").Value;
                    shortenedText = sizeTag + shortenedText;
                }
            }

            if (shortenedText.Length > 0) SendMessage(shortenedText, sendTo, title, true);
            return;
        }

        if (text.RemoveHtmlTags().Length < 300 && title.RemoveHtmlTags().Length < 300) Logger.Info($" Message: {text.RemoveHtmlTags()} - To: {(sendTo == byte.MaxValue ? "Everyone" : $"{GetPlayerById(sendTo).GetRealName()}")} - Title: {title.RemoveHtmlTags()}", "SendMessage");

        Main.MessagesToSend.Add((text.RemoveHtmlTagsTemplate(), sendTo, title));
    }

    public static void ApplySuffix(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || player == null) return;
        if (Main.HostRealName == string.Empty) Main.HostRealName = player.name;
        if (!player.AmOwner && !player.FriendCode.GetDevUser().HasTag() && !ChatCommands.IsPlayerModerator(player.FriendCode)) return;
        string name = Main.AllPlayerNames.TryGetValue(player.PlayerId, out var n) ? n : string.Empty;
        if (Main.NickName != string.Empty && player.AmOwner) name = Main.NickName;
        if (name == string.Empty) return;
        if (AmongUsClient.Instance.IsGameStarted)
        {
            if (Options.FormatNameMode.GetInt() == 1 && Main.NickName == string.Empty)
                name = Palette.GetColorName(player.Data.DefaultOutfit.ColorId);
        }
        else
        {
            if (!GameStates.IsLobby) return;
            if (player.AmOwner)
            {
                if (GameStates.IsOnlineGame || GameStates.IsLocalGame)
                    name = $"<color={GetString("HostColor")}>{GetString("HostText")}</color><color={GetString("IconColor")}>{GetString("Icon")}</color><color={GetString("NameColor")}>{name}</color>";

                string modeText = GetString($"Mode{Options.CurrentGameMode}");
                name = Options.CurrentGameMode switch
                {
                    CustomGameMode.SoloKombat => $"<color=#f55252><size=1.7>{modeText}</size></color>\r\n" + name,
                    CustomGameMode.FFA => $"<color=#00ffff><size=1.7>{modeText}</size></color>\r\n" + name,
                    CustomGameMode.MoveAndStop => $"<color=#00ffa5><size=1.7>{modeText}</size></color>\r\n" + name,
                    CustomGameMode.HotPotato => $"<color=#e8cd46><size=1.7>{modeText}</size></color>\r\n" + name,
                    CustomGameMode.HideAndSeek => $"<color=#345eeb><size=1.7>{modeText}</size></color>\r\n" + name,
                    CustomGameMode.Speedrun => ColorString(GetRoleColor(CustomRoles.Speedrunner), $"<size=1.7>{modeText}</size>\r\n") + name,
                    _ => name
                };
            }

            DevUser devUser = player.FriendCode.GetDevUser();
            bool isMod = ChatCommands.IsPlayerModerator(player.FriendCode);
            bool hasTag = devUser.HasTag();
            if (hasTag || isMod)
            {
                string tag = hasTag ? devUser.GetTag() : string.Empty;
                if (tag == "null") tag = string.Empty;
                if (player.AmOwner || player.IsModClient())
                    name = (hasTag ? tag : string.Empty) + (isMod ? ("<size=1.4>" + GetString("ModeratorTag") + "\r\n</size>") : string.Empty) + name;
                else name = (hasTag ? tag.Replace("\r\n", " - ") : string.Empty) + (isMod ? ("<size=1.4>" + GetString("ModeratorTag") + " - </size>") : string.Empty) + name;
            }

            if (player.AmOwner)
            {
                name = Options.GetSuffixMode() switch
                {
                    SuffixModes.EHR => name + $"\r\n<color={Main.ModColor}>EHR v{Main.PluginDisplayVersion}</color>",
                    SuffixModes.Streaming => name + $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixMode.Streaming")}</color></size>",
                    SuffixModes.Recording => name + $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixMode.Recording")}</color></size>",
                    SuffixModes.RoomHost => name + $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixMode.RoomHost")}</color></size>",
                    SuffixModes.OriginalName => name + $"\r\n<size=1.7><color={Main.ModColor}>{DataManager.player.Customization.Name}</color></size>",
                    SuffixModes.DoNotKillMe => name + $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixModeText.DoNotKillMe")}</color></size>",
                    SuffixModes.NoAndroidPlz => name + $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixModeText.NoAndroidPlz")}</color></size>",
                    SuffixModes.AutoHost => name + $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixModeText.AutoHost")}</color></size>",
                    _ => name
                };
            }
        }

        if (name != player.name && player.CurrentOutfitType == PlayerOutfitType.Default)
            player.RpcSetName(name);
    }

    public static Dictionary<string, int> GetAllPlayerLocationsCount()
    {
        Dictionary<string, int> playerRooms = [];
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (!pc.IsAlive() || Pelican.IsEaten(pc.PlayerId)) return null;
            var Rooms = ShipStatus.Instance.AllRooms;
            if (Rooms == null) return null;
            foreach (PlainShipRoom room in Rooms)
            {
                if (!room.roomArea) continue;
                if (!pc.Collider.IsTouching(room.roomArea)) continue;
                var roomName = GetString($"{room.RoomId}");
                if (!playerRooms.TryAdd(roomName, 1)) playerRooms[roomName]++;
            }
        }

        return playerRooms;
    }

    public static float GetSettingNameAndValueForRole(CustomRoles role, string settingName)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
        var types = Assembly.GetExecutingAssembly().GetTypes();
        var field = types.SelectMany(x => x.GetFields(flags)).FirstOrDefault(x => x.Name == $"{role}{settingName}");
        if (field == null)
        {
            FieldInfo tempField = null;
            foreach (var x in types)
            {
                bool any = false;
                foreach (var f in x.GetFields(flags))
                {
                    if (f.Name.Contains(settingName))
                    {
                        any = true;
                        tempField = f;
                        break;
                    }
                }

                if (any && x.Name == $"{role}")
                {
                    field = tempField;
                    break;
                }
            }
        }

        float add;
        if (field == null)
        {
            add = float.MaxValue;
        }
        else
        {
            if (field.GetValue(null) is OptionItem optionItem) add = optionItem.GetFloat();
            else add = float.MaxValue;
        }

        return add;
    }

    public static string ColoredPlayerName(this byte id) => ColorString(Main.PlayerColors.GetValueOrDefault(id, Color.white), Main.AllPlayerNames.GetValueOrDefault(id, $"Someone (ID {id})"));

    public static PlayerControl GetPlayerById(int PlayerId, bool fast = true)
    {
        if (PlayerId is > byte.MaxValue or < byte.MinValue) return null;
        if (fast && GameStates.IsInGame && Main.PlayerStates.TryGetValue((byte)PlayerId, out var state) && state.Player != null) return state.Player;
        return Main.AllPlayerControls.FirstOrDefault(x => x.PlayerId == PlayerId);
    }

    public static NetworkedPlayerInfo GetPlayerInfoById(int PlayerId) => GameData.Instance.AllPlayers.ToArray().FirstOrDefault(info => info.PlayerId == PlayerId);

    public static async void NotifyRoles(bool isForMeeting = false, PlayerControl SpecifySeer = null, PlayerControl SpecifyTarget = null, bool NoCache = false, bool ForceLoop = false, bool CamouflageIsForMeeting = false, bool GuesserIsForMeeting = false, bool MushroomMixup = false)
    {
        //if (Options.DeepLowLoad.GetBool()) await Task.Run(() => { DoNotifyRoles(isForMeeting, SpecifySeer, NoCache, ForceLoop, CamouflageIsForMeeting, GuesserIsForMeeting); });
        /*else */

        if (!SetUpRoleTextPatch.IsInIntro && ((SpecifySeer != null && SpecifySeer.IsModClient()) || !AmongUsClient.Instance.AmHost || Main.AllPlayerControls == null || (GameStates.IsMeeting && !isForMeeting) || GameStates.IsLobby)) return;

        await DoNotifyRoles(isForMeeting, SpecifySeer, SpecifyTarget, NoCache, ForceLoop, CamouflageIsForMeeting, GuesserIsForMeeting, MushroomMixup);
    }

    public static Task DoNotifyRoles(bool isForMeeting = false, PlayerControl SpecifySeer = null, PlayerControl SpecifyTarget = null, bool NoCache = false, bool ForceLoop = false, bool CamouflageIsForMeeting = false, bool GuesserIsForMeeting = false, bool MushroomMixup = false)
    {
        //var caller = new System.Diagnostics.StackFrame(1, false);
        //var callerMethod = caller.GetMethod();
        //string callerMethodName = callerMethod.Name;
        //string callerClassName = callerMethod.DeclaringType.FullName;
        //Logger.Info("NotifyRoles was called from " + callerClassName + "." + callerMethodName, "NotifyRoles");

        PlayerControl[] seerList = SpecifySeer != null ? [SpecifySeer] : Main.AllPlayerControls.ToArray();
        PlayerControl[] targetList = SpecifyTarget != null ? [SpecifyTarget] : Main.AllPlayerControls.ToArray();

        Logger.Info($" Seers: {string.Join(", ", seerList.Select(x => x.GetRealName()))} ---- Targets: {string.Join(", ", targetList.Select(x => x.GetRealName()))}", "NR");

        // seer: Players who can see changes made here
        // target: Players subject to changes that seer can see
        foreach (PlayerControl seer in seerList)
        {
            try
            {
                if (seer == null || seer.Data.Disconnected || seer.IsModClient()) continue;

                // During intro scene, set team name for non-modded clients and skip the rest.
                string SelfName;
                Team seerTeam = seer.GetTeam();
                CustomRoles seerRole = seer.GetCustomRole();
                if (SetUpRoleTextPatch.IsInIntro && (seerRole.IsDesyncRole() || seer.Is(CustomRoles.Bloodlust)) && Options.CurrentGameMode == CustomGameMode.Standard)
                {
                    const string iconTextLeft = "<color=#ffffff>\u21e8</color>";
                    const string iconTextRight = "<color=#ffffff>\u21e6</color>";
                    const string roleNameUp = "</size><size=1100%>\n \n</size>";

                    string selfTeamName = $"<size=450%>{iconTextLeft} <font=\"VCR SDF\" material=\"VCR Black Outline\">{ColorString(seerTeam.GetTeamColor(), $"{seerTeam}")}</font> {iconTextRight}</size><size=900%>\n \n</size>";
                    SelfName = $"{selfTeamName}\r\n{seerRole.ToColoredString()}{roleNameUp}";

                    seer.RpcSetNamePrivate(SelfName, seer);
                    continue;
                }

                string fontSize = "1.7";
                if (isForMeeting && (seer.GetClient().PlatformData.Platform == Platforms.Playstation || seer.GetClient().PlatformData.Platform == Platforms.Switch)) fontSize = "70%";
                //Logger.Info("NotifyRoles-Loop1-" + seer.GetNameWithRole().RemoveHtmlTags() + ":START", "NotifyRoles");

                // Text containing progress, such as tasks
                string SelfTaskText = GameStates.IsLobby ? string.Empty : GetProgressText(seer);

                SelfMark.Clear();
                SelfSuffix.Clear();

                if (!GameStates.IsLobby)
                {
                    if (Options.CurrentGameMode != CustomGameMode.Standard) goto GameMode0;

                    SelfMark.Append(Snitch.GetWarningArrow(seer));
                    if (Main.LoversPlayers.Any(x => x.PlayerId == seer.PlayerId)) SelfMark.Append(ColorString(GetRoleColor(CustomRoles.Lovers), " ♥"));
                    if (BallLightning.IsGhost(seer)) SelfMark.Append(ColorString(GetRoleColor(CustomRoles.BallLightning), "■"));
                    SelfMark.Append(Medic.GetMark(seer, seer));
                    SelfMark.Append(Gamer.TargetMark(seer, seer));
                    SelfMark.Append(Sniper.GetShotNotify(seer.PlayerId));
                    if (Silencer.ForSilencer.Contains(seer.PlayerId)) SelfMark.Append(ColorString(GetRoleColor(CustomRoles.Silencer), "╳"));

                    GameMode0:

                    if (Options.CurrentGameMode is not CustomGameMode.Standard and not CustomGameMode.HideAndSeek) goto GameMode;

                    Main.PlayerStates.Values.Do(x => SelfSuffix.Append(x.Role.GetSuffix(seer, seer, isMeeting: isForMeeting)));

                    SelfSuffix.Append(Spurt.GetSuffix(seer));

                    SelfSuffix.Append(CustomTeamManager.GetSuffix(seer));

                    if (!isForMeeting)
                    {
                        if (Options.UsePets.GetBool() && Main.AbilityCD.TryGetValue(seer.PlayerId, out var time) && !seer.IsModClient())
                        {
                            var remainingCD = time.TOTALCD - (TimeStamp - time.START_TIMESTAMP) + 1;
                            SelfSuffix.Append(string.Format(GetString("CDPT"), remainingCD > 60 ? "> 60" : remainingCD));
                        }

                        if (seer.Is(CustomRoles.Asthmatic)) SelfSuffix.Append(Asthmatic.GetSuffixText(seer.PlayerId));
                        if (seer.Is(CustomRoles.Sonar)) SelfSuffix.Append(Sonar.GetSuffix(seer, isForMeeting));

                        SelfSuffix.Append(Bloodmoon.GetSuffix(seer));
                        SelfSuffix.Append(Haunter.GetSuffix(seer));

                        switch (seerRole)
                        {
                            case CustomRoles.SuperStar when Options.EveryOneKnowSuperStar.GetBool():
                                SelfMark.Append(ColorString(GetRoleColor(CustomRoles.SuperStar), "★"));
                                break;
                            case CustomRoles.Monitor:
                            case CustomRoles.AntiAdminer:
                                if (AntiAdminer.IsAdminWatch) SelfSuffix.Append(GetString("AntiAdminerAD"));
                                if (AntiAdminer.IsVitalWatch) SelfSuffix.Append(GetString("AntiAdminerVI"));
                                if (AntiAdminer.IsDoorLogWatch) SelfSuffix.Append(GetString("AntiAdminerDL"));
                                if (AntiAdminer.IsCameraWatch) SelfSuffix.Append(GetString("AntiAdminerCA"));
                                break;
                        }
                    }
                    else
                    {
                        SelfMark.Append(Witch.GetSpelledMark(seer.PlayerId, isForMeeting));
                    }

                    GameMode:

                    switch (Options.CurrentGameMode)
                    {
                        case CustomGameMode.FFA:
                            SelfSuffix.Append(FFAManager.GetPlayerArrow(seer));
                            break;
                        case CustomGameMode.SoloKombat:
                            SelfSuffix.Append(SoloKombatManager.GetDisplayHealth(seer));
                            break;
                        case CustomGameMode.MoveAndStop:
                            SelfSuffix.Append(MoveAndStopManager.GetSuffixText(seer));
                            break;
                        case CustomGameMode.HotPotato when seer.IsAlive() && !seer.IsModClient():
                            SelfSuffix.Append(HotPotatoManager.GetSuffixText(seer.PlayerId));
                            break;
                        case CustomGameMode.Speedrun:
                            SelfSuffix.Append(SpeedrunManager.GetSuffixText(seer));
                            break;
                        case CustomGameMode.HideAndSeek:
                            SelfSuffix.Append(HnSManager.GetSuffixText(seer, seer));
                            break;
                    }
                }

                string SeerRealName = seer.GetRealName(isForMeeting);

                if (!GameStates.IsLobby)
                {
                    if (Options.CurrentGameMode == CustomGameMode.FFA && FFAManager.FFATeamMode.GetBool())
                        SeerRealName = SeerRealName.ApplyNameColorData(seer, seer, isForMeeting);

                    if (!isForMeeting && MeetingStates.FirstMeeting && Options.ChangeNameToRoleInfo.GetBool() && Options.CurrentGameMode is not CustomGameMode.FFA and not CustomGameMode.MoveAndStop and not CustomGameMode.HotPotato and not CustomGameMode.Speedrun)
                    {
                        var team = CustomTeamManager.GetCustomTeam(seer.PlayerId);
                        if (team != null)
                        {
                            SeerRealName = ColorString(
                                team.RoleRevealScreenBackgroundColor == "*" || !ColorUtility.TryParseHtmlString(team.RoleRevealScreenBackgroundColor, out var teamColor)
                                    ? Color.yellow
                                    : teamColor,
                                string.Format(
                                    GetString("CustomTeamHelp"),
                                    team.RoleRevealScreenTitle == "*"
                                        ? team.TeamName
                                        : team.RoleRevealScreenTitle,
                                    team.RoleRevealScreenSubtitle == "*"
                                        ? string.Empty
                                        : team.RoleRevealScreenSubtitle));
                        }
                        else if (Options.CurrentGameMode == CustomGameMode.HideAndSeek)
                        {
                            SeerRealName = HnSManager.GetRoleInfoText(seer);
                        }
                        else
                        {
                            SeerRealName = !Options.ChangeNameToRoleInfo.GetBool()
                                ? SeerRealName
                                : seerTeam switch
                                {
                                    Team.Impostor when seerRole.IsMadmate() || seer.Is(CustomRoles.Madmate) => $"<color=#ff1919>{GetString("YouAreMadmate")}</color>\n<size=90%>{seer.GetRoleInfo()}</size>",
                                    Team.Impostor => $"\n<size=90%>{seer.GetRoleInfo()}</size>",
                                    Team.Crewmate => $"<color=#8cffff>{GetString("YouAreCrewmate")}</color>\n<size=90%>{seer.GetRoleInfo()}</size>",
                                    Team.Neutral => $"<color=#ffab1b>{GetString("YouAreNeutral")}</color>\n<size=90%>{seer.GetRoleInfo()}</size>",
                                    _ => SeerRealName
                                };
                        }
                    }
                }

                // Combine seer's job title and SelfTaskText with seer's player name and SelfMark
                string SelfRoleName = GameStates.IsLobby ? string.Empty : $"<size={fontSize}>{seer.GetDisplayRoleName()}{SelfTaskText}</size>";
                string SelfDeathReason = seer.KnowDeathReason(seer) && !GameStates.IsLobby ? $"\n<size=1.5>『{ColorString(GetRoleColor(CustomRoles.Doctor), GetVitalText(seer.PlayerId))}』</size>" : string.Empty;
                SelfName = $"{ColorString(GameStates.IsLobby ? Color.white : seer.GetRoleColor(), SeerRealName)}{SelfDeathReason}{SelfMark}";

                if (Options.CurrentGameMode != CustomGameMode.Standard || GameStates.IsLobby) goto GameMode2;

                SelfName = seerRole switch
                {
                    CustomRoles.Arsonist when seer.IsDouseDone() => $"{ColorString(seer.GetRoleColor(), GetString("EnterVentToWin"))}",
                    CustomRoles.Revolutionist when seer.IsDrawDone() => $">{ColorString(seer.GetRoleColor(), string.Format(GetString("EnterVentWinCountDown"), Revolutionist.RevolutionistCountdown.GetValueOrDefault(seer.PlayerId, 10)))}",
                    _ => SelfName
                };

                if (Pelican.IsEaten(seer.PlayerId)) SelfName = $"{ColorString(GetRoleColor(CustomRoles.Pelican), GetString("EatenByPelican"))}";
                if (Deathpact.IsInActiveDeathpact(seer)) SelfName = Deathpact.GetDeathpactString(seer);

                // Devourer
                if (Devourer.HideNameOfConsumedPlayer.GetBool() && Devourer.playerIdList.Any(x => Main.PlayerStates[x].Role is Devourer { IsEnable: true } dv && dv.PlayerSkinsCosumed.Contains(seer.PlayerId)) && !CamouflageIsForMeeting)
                    SelfName = GetString("DevouredName");
                // Camouflage
                if (((IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool() && (Main.NormalOptions.MapId != 5 || !Options.CommsCamouflageDisableOnFungle.GetBool())) || Camouflager.IsActive) && !CamouflageIsForMeeting)
                    SelfName = $"<size=0>{SelfName}</size>";

                GameMode2:

                if (!GameStates.IsLobby)
                {
                    if (NameNotifyManager.GetNameNotify(seer, out var name)) SelfName = name;

                    switch (Options.CurrentGameMode)
                    {
                        case CustomGameMode.SoloKombat:
                            SoloKombatManager.GetNameNotify(seer, ref SelfName);
                            SelfName = $"<size={fontSize}>{SelfTaskText}</size>\r\n{SelfName}";
                            break;
                        case CustomGameMode.FFA:
                            FFAManager.GetNameNotify(seer, ref SelfName);
                            SelfName = $"<size={fontSize}>{SelfTaskText}</size>\r\n{SelfName}";
                            break;
                        default:
                            SelfName = $"{SelfRoleName}\r\n{SelfName}";
                            break;
                    }

                    SelfName += SelfSuffix.ToString() == string.Empty ? string.Empty : $"\r\n{SelfSuffix}";
                    if (!isForMeeting) SelfName += "\r\n";
                }

                seer.RpcSetNamePrivate(SelfName, force: NoCache);

                // Run the second loop only when necessary, such as when seer is dead
                if (seer.Data.IsDead || !seer.IsAlive() || NoCache || CamouflageIsForMeeting || MushroomMixup || IsActive(SystemTypes.MushroomMixupSabotage) || ForceLoop || seerList.Length == 1 || targetList.Length == 1)
                {
                    foreach (PlayerControl target in targetList)
                    {
                        if (target.PlayerId == seer.PlayerId) continue;
                        Logger.Info($"NotifyRoles-Loop2-{target.GetNameWithRole().RemoveHtmlTags()}:START", "NotifyRoles");

                        if ((IsActive(SystemTypes.MushroomMixupSabotage) || MushroomMixup) && target.IsAlive() && !seer.Is(CustomRoleTypes.Impostor) && Main.ResetCamPlayerList.Contains(seer.PlayerId))
                        {
                            seer.RpcSetNamePrivate("<size=0%>", force: NoCache);
                        }
                        else
                        {
                            TargetMark.Clear();

                            if (Options.CurrentGameMode != CustomGameMode.Standard || GameStates.IsLobby) goto BeforeEnd2;

                            TargetMark.Append(Witch.GetSpelledMark(target.PlayerId, isForMeeting));

                            if (target.Is(CustomRoles.SuperStar) && Options.EveryOneKnowSuperStar.GetBool())
                                TargetMark.Append(ColorString(GetRoleColor(CustomRoles.SuperStar), "★"));

                            if (BallLightning.IsGhost(target))
                                TargetMark.Append(ColorString(GetRoleColor(CustomRoles.BallLightning), "■"));

                            TargetMark.Append(Snitch.GetWarningMark(seer, target));
                            TargetMark.Append(Marshall.GetWarningMark(seer, target));

                            if ((seer.Data.IsDead || Main.LoversPlayers.Any(x => x.PlayerId == seer.PlayerId)) && Main.LoversPlayers.Any(x => x.PlayerId == target.PlayerId))
                            {
                                TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Lovers)}> ♥</color>");
                            }

                            if (Randomizer.IsShielded(target)) TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Randomizer), "✚"));

                            switch (seerRole)
                            {
                                case CustomRoles.PlagueBearer:
                                    if (PlagueBearer.IsPlagued(seer.PlayerId, target.PlayerId))
                                    {
                                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.PlagueBearer)}>●</color>");
                                        PlagueBearer.SendRPC(seer, target);
                                    }

                                    break;
                                case CustomRoles.Arsonist:
                                    if (seer.IsDousedPlayer(target))
                                    {
                                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Arsonist)}>▲</color>");
                                    }

                                    else if (Arsonist.ArsonistTimer.TryGetValue(seer.PlayerId, out var ar_kvp) && ar_kvp.PLAYER == target)
                                    {
                                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Arsonist)}>△</color>");
                                    }

                                    break;
                                case CustomRoles.Revolutionist:
                                    if (seer.IsDrawPlayer(target))
                                    {
                                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Revolutionist)}>●</color>");
                                    }

                                    if (Revolutionist.RevolutionistTimer.TryGetValue(seer.PlayerId, out var ar_kvp1) && ar_kvp1.PLAYER == target)
                                    {
                                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Revolutionist)}>○</color>");
                                    }

                                    break;
                                case CustomRoles.Farseer:
                                    if (Farseer.FarseerTimer.TryGetValue(seer.PlayerId, out var ar_kvp2) && ar_kvp2.PLAYER == target)
                                    {
                                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Farseer)}>○</color>");
                                    }

                                    break;
                                case CustomRoles.Analyst:
                                    if ((Main.PlayerStates[seer.PlayerId].Role as Analyst).CurrentTarget.ID == target.PlayerId)
                                    {
                                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Analyst)}>○</color>");
                                    }

                                    break;
                                case CustomRoles.Samurai: // Same as Analyst
                                    if ((Main.PlayerStates[seer.PlayerId].Role as Samurai).Target.Id == target.PlayerId)
                                    {
                                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Samurai)}>○</color>");
                                    }

                                    break;
                                case CustomRoles.Puppeteer when Puppeteer.PuppeteerList.ContainsValue(seer.PlayerId) && Puppeteer.PuppeteerList.ContainsKey(target.PlayerId):
                                    TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Impostor)}>◆</color>");
                                    break;
                            }

                            BeforeEnd2:

                            string TargetRoleText =
                                (seer.Data.IsDead && Options.GhostCanSeeOtherRoles.GetBool()) ||
                                (seer.Is(CustomRoles.Mimic) && target.Data.IsDead && Options.MimicCanSeeDeadRoles.GetBool()) ||
                                (target.Is(CustomRoles.Gravestone) && target.Data.IsDead) ||
                                (Main.LoversPlayers.TrueForAll(x => x.PlayerId == seer.PlayerId || x.PlayerId == target.PlayerId) && Main.LoversPlayers.Count == 2 && Lovers.LoverKnowRoles.GetBool()) ||
                                (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoleTypes.Impostor) && Options.ImpKnowAlliesRole.GetBool()) ||
                                (seer.Is(CustomRoles.Madmate) && target.Is(CustomRoleTypes.Impostor) && Options.MadmateKnowWhosImp.GetBool()) ||
                                (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Madmate) && Options.ImpKnowWhosMadmate.GetBool()) ||
                                (seer.Is(CustomRoles.Crewpostor) && target.Is(CustomRoleTypes.Impostor) && Options.CrewpostorKnowsAllies.GetBool()) ||
                                (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Crewpostor) && Options.AlliesKnowCrewpostor.GetBool()) ||
                                (seer.Is(CustomRoles.Madmate) && target.Is(CustomRoles.Madmate) && Options.MadmateKnowWhosMadmate.GetBool()) ||
                                ((seer.Is(CustomRoles.Sidekick) || seer.Is(CustomRoles.Recruit) || seer.Is(CustomRoles.Jackal)) && (target.Is(CustomRoles.Sidekick) || target.Is(CustomRoles.Recruit) || target.Is(CustomRoles.Jackal))) ||
                                (target.Is(CustomRoles.Workaholic) && Workaholic.WorkaholicVisibleToEveryone.GetBool()) ||
                                (target.Is(CustomRoles.Doctor) && !target.HasEvilAddon() && Options.DoctorVisibleToEveryone.GetBool()) ||
                                (target.Is(CustomRoles.Mayor) && Mayor.MayorRevealWhenDoneTasks.GetBool() && target.GetTaskState().IsTaskFinished) ||
                                (seer.Is(CustomRoleTypes.Crewmate) && target.Is(CustomRoles.Marshall) && target.GetTaskState().IsTaskFinished) ||
                                (Main.PlayerStates[target.PlayerId].deathReason == PlayerState.DeathReason.Vote && Options.SeeEjectedRolesInMeeting.GetBool()) ||
                                CustomTeamManager.AreInSameCustomTeam(seer.PlayerId, target.PlayerId) && CustomTeamManager.IsSettingEnabledForPlayerTeam(seer.PlayerId, CTAOption.KnowRoles) ||
                                Main.PlayerStates.Values.Any(x => x.Role.KnowRole(seer, target)) ||
                                Markseeker.PlayerIdList.Any(x => Main.PlayerStates[x].Role is Markseeker { IsEnable: true, TargetRevealed: true } ms && ms.MarkedId == target.PlayerId) ||
                                Options.CurrentGameMode is CustomGameMode.FFA or CustomGameMode.MoveAndStop or CustomGameMode.HotPotato or CustomGameMode.Speedrun ||
                                (Options.CurrentGameMode == CustomGameMode.HideAndSeek && HnSManager.IsRoleTextEnabled(seer, target)) ||
                                (seer.IsRevealedPlayer(target) && !target.Is(CustomRoles.Trickster)) ||
                                seer.Is(CustomRoles.God) ||
                                target.Is(CustomRoles.GM)
                                    ? $"<size={fontSize}>{target.GetDisplayRoleName(seer.PlayerId != target.PlayerId && !seer.Data.IsDead)}{GetProgressText(target)}</size>\r\n"
                                    : string.Empty;

                            if (!GameStates.IsLobby)
                            {
                                if (!seer.Data.IsDead && seer.IsRevealedPlayer(target) && target.Is(CustomRoles.Trickster))
                                {
                                    TargetRoleText = Farseer.RandomRole[seer.PlayerId];
                                    TargetRoleText += Farseer.GetTaskState();
                                }

                                if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
                                    TargetRoleText = $"<size={fontSize}>{GetProgressText(target)}</size>\r\n";
                            }
                            else TargetRoleText = string.Empty;

                            string TargetPlayerName = target.GetRealName(isForMeeting);

                            if (GameStates.IsLobby) goto End;
                            if (Options.CurrentGameMode != CustomGameMode.Standard) goto BeforeEnd;

                            if (GuesserIsForMeeting || isForMeeting || (seerRole == CustomRoles.Mafia && !seer.IsAlive() && Options.MafiaCanKillNum.GetInt() >= 1))
                                TargetPlayerName = $"{ColorString(GetRoleColor(seerRole), target.PlayerId.ToString())} {TargetPlayerName}";

                            switch (seerRole)
                            {
                                case CustomRoles.EvilTracker:
                                    TargetMark.Append(EvilTracker.GetTargetMark(seer, target));
                                    if (isForMeeting && EvilTracker.IsTrackTarget(seer, target) && EvilTracker.CanSeeLastRoomInMeeting)
                                        TargetRoleText = $"<size={fontSize}>{EvilTracker.GetArrowAndLastRoom(seer, target)}</size>\r\n";
                                    break;
                                case CustomRoles.Scout:
                                    TargetMark.Append(Scout.GetTargetMark(seer, target));
                                    if (isForMeeting && Scout.IsTrackTarget(seer, target) && Scout.CanSeeLastRoomInMeeting)
                                        TargetRoleText = $"<size={fontSize}>{Scout.GetArrowAndLastRoom(seer, target)}</size>\r\n";
                                    break;
                                case CustomRoles.Psychic when seer.IsAlive() && Psychic.IsRedForPsy(target, seer) && isForMeeting:
                                    TargetPlayerName = ColorString(GetRoleColor(CustomRoles.Impostor), TargetPlayerName);
                                    break;
                                case CustomRoles.HeadHunter when (Main.PlayerStates[seer.PlayerId].Role as HeadHunter).Targets.Contains(target.PlayerId) && seer.IsAlive():
                                    TargetPlayerName = $"<color=#000000>{TargetPlayerName}</size>";
                                    break;
                                case CustomRoles.BountyHunter when (Main.PlayerStates[seer.PlayerId].Role as BountyHunter).GetTarget(seer) == target.PlayerId && seer.IsAlive():
                                    TargetPlayerName = $"<color=#000000>{TargetPlayerName}</size>";
                                    break;
                                case CustomRoles.Doomsayer when seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting:
                                    TargetPlayerName = $"{ColorString(GetRoleColor(CustomRoles.Doomsayer), $" {target.PlayerId}")} {TargetPlayerName}";
                                    break;
                                case CustomRoles.Lookout when seer.IsAlive() && target.IsAlive():
                                    TargetPlayerName = $"{ColorString(GetRoleColor(CustomRoles.Lookout), $" {target.PlayerId}")} {TargetPlayerName}";
                                    break;
                            }

                            BeforeEnd:

                            TargetPlayerName = TargetPlayerName.ApplyNameColorData(seer, target, isForMeeting);

                            if (Options.CurrentGameMode != CustomGameMode.Standard) goto End;

                            if (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Snitch) && target.Is(CustomRoles.Madmate) && target.GetTaskState().IsTaskFinished)
                                TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Impostor), "★"));
                            if (seer.Is(CustomRoleTypes.Crewmate) && target.Is(CustomRoles.Marshall) && target.GetTaskState().IsTaskFinished)
                                TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Marshall), "★"));

                            TargetMark.Append(Executioner.TargetMark(seer, target));
                            TargetMark.Append(Gamer.TargetMark(seer, target));
                            TargetMark.Append(Medic.GetMark(seer, target));
                            TargetMark.Append(Totocalcio.TargetMark(seer, target));
                            TargetMark.Append(Romantic.TargetMark(seer, target));
                            TargetMark.Append(Lawyer.LawyerMark(seer, target));
                            TargetMark.Append(Deathpact.GetDeathpactMark(seer, target));
                            TargetMark.Append(PlagueDoctor.GetMarkOthers(seer, target));

                            End:

                            TargetSuffix.Clear();

                            if (!GameStates.IsLobby)
                            {
                                switch (Options.CurrentGameMode)
                                {
                                    case CustomGameMode.SoloKombat:
                                        TargetSuffix.Append(SoloKombatManager.GetDisplayHealth(target));
                                        break;
                                    case CustomGameMode.HideAndSeek:
                                        TargetSuffix.Append(HnSManager.GetSuffixText(seer, target));
                                        break;
                                }

                                Main.PlayerStates.Values.Do(x => TargetSuffix.Append(x.Role.GetSuffix(seer, target, isMeeting: isForMeeting)));

                                if (MeetingStates.FirstMeeting && Main.FirstDied != string.Empty && Main.FirstDied == target.FriendCode && Main.ShieldPlayer != string.Empty && Options.CurrentGameMode is CustomGameMode.Standard or CustomGameMode.SoloKombat or CustomGameMode.FFA)
                                    TargetSuffix.Append(GetString("DiedR1Warning"));

                                TargetSuffix.Append(AFKDetector.GetSuffix(seer, target));
                            }

                            string TargetDeathReason = string.Empty;
                            if (seer.KnowDeathReason(target) && !GameStates.IsLobby)
                                TargetDeathReason = $"\n<size=1.7>({ColorString(GetRoleColor(CustomRoles.Doctor), GetVitalText(target.PlayerId))})</size>";

                            // Devourer
                            if (Devourer.HideNameOfConsumedPlayer.GetBool() && !GameStates.IsLobby && Devourer.playerIdList.Any(x => Main.PlayerStates[x].Role is Devourer { IsEnable: true } dv && dv.PlayerSkinsCosumed.Contains(seer.PlayerId)) && !CamouflageIsForMeeting)
                                TargetPlayerName = GetString("DevouredName");

                            // Camouflage
                            if (((IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool() && !GameStates.IsLobby && (Main.NormalOptions.MapId != 5 || !Options.CommsCamouflageDisableOnFungle.GetBool())) || Camouflager.IsActive) && !CamouflageIsForMeeting)
                                TargetPlayerName = $"<size=0>{TargetPlayerName}</size>";

                            string TargetName = $"{TargetRoleText}{TargetPlayerName}{TargetDeathReason}{TargetMark}";
                            TargetName += GameStates.IsLobby || TargetSuffix.ToString() == string.Empty ? string.Empty : $"\r\n{TargetSuffix}";

                            target.RpcSetNamePrivate(TargetName, seer, force: NoCache);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error for {seer.GetNameWithRole()}: {ex}", "NR");
            }
        }

        return Task.CompletedTask;
    }

    public static void MarkEveryoneDirtySettings()
    {
        PlayerGameOptionsSender.SetDirtyToAll();
    }

    public static void MarkEveryoneDirtySettingsV2()
    {
        PlayerGameOptionsSender.SetDirtyToAllV2();
    }

    public static void MarkEveryoneDirtySettingsV3()
    {
        PlayerGameOptionsSender.SetDirtyToAllV3();
    }

    public static void MarkEveryoneDirtySettingsV4()
    {
        PlayerGameOptionsSender.SetDirtyToAllV4();
    }

    public static void SyncAllSettings()
    {
        PlayerGameOptionsSender.SetDirtyToAll();
        Main.Instance.StartCoroutine(GameOptionsSender.SendAllGameOptionsAsync());
    }

    public static void RpcChangeSkin(PlayerControl pc, NetworkedPlayerInfo.PlayerOutfit newOutfit)
    {
        var sender = CustomRpcSender.Create(name: $"Utils.RpcChangeSkin({pc.Data.PlayerName})");

        pc.SetName(newOutfit.PlayerName);
        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetName)
            .Write(pc.Data.NetId)
            .Write(newOutfit.PlayerName)
            .EndRpc();

        Main.AllPlayerNames[pc.PlayerId] = newOutfit.PlayerName;

        pc.SetColor(newOutfit.ColorId);
        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetColor)
            .Write(pc.Data.NetId)
            .Write((byte)newOutfit.ColorId)
            .EndRpc();

        pc.SetHat(newOutfit.HatId, newOutfit.ColorId);
        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetHatStr)
            .Write(newOutfit.HatId)
            .Write(pc.GetNextRpcSequenceId(RpcCalls.SetHatStr))
            .EndRpc();

        pc.SetSkin(newOutfit.SkinId, newOutfit.ColorId);
        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetSkinStr)
            .Write(newOutfit.SkinId)
            .Write(pc.GetNextRpcSequenceId(RpcCalls.SetSkinStr))
            .EndRpc();

        pc.SetVisor(newOutfit.VisorId, newOutfit.ColorId);
        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetVisorStr)
            .Write(newOutfit.VisorId)
            .Write(pc.GetNextRpcSequenceId(RpcCalls.SetVisorStr))
            .EndRpc();

        pc.SetPet(newOutfit.PetId);
        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetPetStr)
            .Write(newOutfit.PetId)
            .Write(pc.GetNextRpcSequenceId(RpcCalls.SetPetStr))
            .EndRpc();

        pc.SetNamePlate(newOutfit.NamePlateId);
        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetNamePlateStr)
            .Write(newOutfit.NamePlateId)
            .Write(pc.GetNextRpcSequenceId(RpcCalls.SetNamePlateStr))
            .EndRpc();

        sender.SendMessage();
    }

    public static string GetGameStateData(bool clairvoyant = false)
    {
        var nums = Enum.GetValues<Options.GameStateInfo>().ToDictionary(x => x, _ => 0);

        if (CustomRoles.Romantic.RoleExist(countDead: true)) nums[Options.GameStateInfo.RomanticState] = 1;
        if (Romantic.HasPickedPartner) nums[Options.GameStateInfo.RomanticState] = 2;

        foreach (var pc in Main.AllAlivePlayerControls)
        {
            if (pc.IsMadmate()) nums[Options.GameStateInfo.MadmateCount]++;
            else if (pc.IsNeutralKiller()) nums[Options.GameStateInfo.NKCount]++;
            else if (pc.IsCrewmate()) nums[Options.GameStateInfo.CrewCount]++;
            else if (pc.Is(Team.Impostor)) nums[Options.GameStateInfo.ImpCount]++;
            else if (pc.Is(Team.Neutral)) nums[Options.GameStateInfo.NNKCount]++;
            if (pc.GetCustomSubRoles().Any(x => x.IsConverted())) nums[Options.GameStateInfo.ConvertedCount]++;
            if (Main.LoversPlayers.Any(x => x.PlayerId == pc.PlayerId)) nums[Options.GameStateInfo.LoversState]++;
            if (pc.Is(CustomRoles.Romantic)) nums[Options.GameStateInfo.RomanticState] *= 3;
            if (Romantic.PartnerId == pc.PlayerId) nums[Options.GameStateInfo.RomanticState] *= 4;
        }

        // All possible results of RomanticState from the above code:
        // 0: Romantic doesn't exist
        // 1: Romantic exists but hasn't picked a partner
        // 2: Romantic exists and has picked a partner
        // 3: Romantic exists, is alive, but hasn't picked a partner
        // 6: Romantic exists, has picked a partner who is dead, but Romantic is alive
        // 8: Romantic exists, has picked a partner who is alive, but Romantic is dead
        // 24: Romantic exists, has picked a partner who is alive, and Romantic is alive

        var sb = new StringBuilder();
        var checkDict = clairvoyant ? Clairvoyant.Settings : Options.GameStateSettings;
        nums[Options.GameStateInfo.Tasks] = GameData.Instance.CompletedTasks;
        var states = nums.ToDictionary(x => x.Key, x => x.Key == Options.GameStateInfo.RomanticState ? GetString($"GSRomanticState.{x.Value}") : (object)x.Value);
        states.DoIf(x => checkDict[x.Key].GetBool(), x => sb.AppendLine(string.Format(GetString($"GSInfo.{x.Key}"), x.Value)));
        return sb.ToString().TrimEnd();
    }

    public static void AddAbilityCD(CustomRoles role, byte playerId, bool includeDuration = true)
    {
        if (role.UsesPetInsteadOfKill())
        {
            Main.AbilityCD[playerId] = (TimeStamp, (int)Math.Round(Main.AllPlayerKillCooldown.TryGetValue(playerId, out var KCD) ? KCD : Options.DefaultKillCooldown));
            return;
        }

        int CD = role switch
        {
            CustomRoles.Mole => Mole.CD.GetInt(),
            CustomRoles.Doormaster => Doormaster.VentCooldown.GetInt(),
            CustomRoles.Tether => Tether.VentCooldown.GetInt(),
            CustomRoles.Mayor when Mayor.MayorHasPortableButton.GetBool() => (int)Math.Round(Options.DefaultKillCooldown),
            CustomRoles.Paranoia => (int)Math.Round(Options.DefaultKillCooldown),
            CustomRoles.Grenadier => Options.GrenadierSkillCooldown.GetInt() + (includeDuration ? Options.GrenadierSkillDuration.GetInt() : 0),
            CustomRoles.Lighter => Options.LighterSkillCooldown.GetInt() + (includeDuration ? Options.LighterSkillDuration.GetInt() : 0),
            CustomRoles.SecurityGuard => Options.SecurityGuardSkillCooldown.GetInt() + (includeDuration ? Options.SecurityGuardSkillDuration.GetInt() : 0),
            CustomRoles.TimeMaster => Options.TimeMasterSkillCooldown.GetInt() + (includeDuration ? Options.TimeMasterSkillDuration.GetInt() : 0),
            CustomRoles.Veteran => Options.VeteranSkillCooldown.GetInt() + (includeDuration ? Options.VeteranSkillDuration.GetInt() : 0),
            CustomRoles.Rhapsode => Rhapsode.AbilityCooldown.GetInt() + (includeDuration ? Rhapsode.AbilityDuration.GetInt() : 0),
            CustomRoles.Perceiver => Perceiver.CD.GetInt(),
            CustomRoles.Convener => Convener.CD.GetInt(),
            CustomRoles.DovesOfNeace => Options.DovesOfNeaceCooldown.GetInt(),
            CustomRoles.Alchemist => Alchemist.VentCooldown.GetInt(),
            CustomRoles.NiceHacker => playerId.IsPlayerModClient() ? -1 : NiceHacker.AbilityCD.GetInt(),
            CustomRoles.CameraMan => CameraMan.VentCooldown.GetInt(),
            CustomRoles.Tornado => Tornado.TornadoCooldown.GetInt(),
            CustomRoles.Sentinel => Sentinel.PatrolCooldown.GetInt(),
            CustomRoles.Druid => Druid.VentCooldown.GetInt(),
            CustomRoles.Sentry => EHR.Impostor.Sentry.ShowInfoCooldown.GetInt(),
            CustomRoles.ToiletMaster => ToiletMaster.AbilityCooldown.GetInt(),
            CustomRoles.Sniper => Options.DefaultShapeshiftCooldown.GetInt(),
            CustomRoles.Assassin => Assassin.AssassinateCooldownOpt.GetInt(),
            CustomRoles.Undertaker => Undertaker.UndertakerAssassinateCooldown.GetInt(),
            CustomRoles.Bomber => Options.BombCooldown.GetInt(),
            CustomRoles.Nuker => Options.NukeCooldown.GetInt(),
            CustomRoles.Sapper => Sapper.ShapeshiftCooldown.GetInt(),
            CustomRoles.Miner => Options.MinerSSCD.GetInt(),
            CustomRoles.Escapee => Options.EscapeeSSCD.GetInt(),
            CustomRoles.QuickShooter => QuickShooter.ShapeshiftCooldown.GetInt(),
            CustomRoles.Disperser => Disperser.DisperserShapeshiftCooldown.GetInt(),
            CustomRoles.Twister => Twister.ShapeshiftCooldown.GetInt(),
            CustomRoles.Warlock => Warlock.IsCursed ? -1 : (int)Options.DefaultKillCooldown,
            CustomRoles.Swiftclaw => Swiftclaw.DashCD.GetInt() + (includeDuration ? Swiftclaw.DashDuration.GetInt() : 0),
            CustomRoles.Parasite => (int)Parasite.SSCD + (includeDuration ? (int)Parasite.SSDur : 0),
            CustomRoles.Tiger => Tiger.EnrageCooldown.GetInt() + (includeDuration ? Tiger.EnrageDuration.GetInt() : 0),
            CustomRoles.Nonplus => Nonplus.BlindCooldown.GetInt() + (includeDuration ? Nonplus.BlindDuration.GetInt() : 0),
            CustomRoles.Cherokious => Cherokious.KillCooldown.GetInt(),
            _ => -1
        };
        if (CD == -1) return;

        if (Main.PlayerStates[playerId].SubRoles.Contains(CustomRoles.Energetic))
            CD = (int)Math.Round(CD * 0.75f);

        Main.AbilityCD[playerId] = (TimeStamp, CD);
    }

    public static void AfterMeetingTasks()
    {
        bool loversChat = Lovers.PrivateChat.GetBool();
        if (!Lovers.IsChatActivated && loversChat && !GameStates.IsEnded && Options.CurrentGameMode == CustomGameMode.Standard)
        {
            LateTask.New(SetChatVisibleForAll, 0.5f, log: false);
            Lovers.IsChatActivated = true;
            return;
        }

        if (loversChat) GameEndChecker.Prefix();

        Lovers.IsChatActivated = false;
        if (!Options.UseUnshiftTrigger.GetBool()) Main.ProcessShapeshifts = true;
        AFKDetector.NumAFK = 0;
        AFKDetector.PlayerData.Clear();

        foreach (var pc in Main.AllPlayerControls)
        {
            if (pc.IsAlive())
            {
                pc.AddKillTimerToDict();

                if (pc.Is(CustomRoles.Truant))
                {
                    float beforeSpeed = Main.AllPlayerSpeed[pc.PlayerId];
                    Main.AllPlayerSpeed[pc.PlayerId] = Main.MinSpeed;
                    pc.MarkDirtySettings();
                    LateTask.New(() =>
                        {
                            Main.AllPlayerSpeed[pc.PlayerId] = beforeSpeed;
                            pc.MarkDirtySettings();
                        }, Options.TruantWaitingTime.GetFloat(), $"Truant Waiting: {pc.GetNameWithRole()}");
                }

                if (Options.UsePets.GetBool()) pc.AddAbilityCD(includeDuration: false);

                if (pc.GetCustomRole().SimpleAbilityTrigger() && Options.UseUnshiftTrigger.GetBool() && (!pc.IsNeutralKiller() || Options.UseUnshiftTriggerForNKs.GetBool()))
                {
                    var target = Main.AllAlivePlayerControls.Without(pc).RandomElement();
                    var outfit = pc.Data.DefaultOutfit;
                    pc.RpcShapeshift(target, false);
                    Main.CheckShapeshift[pc.PlayerId] = false;
                    RpcChangeSkin(pc, outfit);
                }

                AFKDetector.RecordPosition(pc);

                Main.PlayerStates[pc.PlayerId].Role.AfterMeetingTasks();
            }
            else
            {
                TaskState taskState = pc.GetTaskState();
                if (pc.IsCrewmate() && !taskState.IsTaskFinished && taskState.hasTasks)
                    pc.Notify(GetString("DoYourTasksPlease"), 10f);

                GhostRolesManager.NotifyAboutGhostRole(pc);
            }

            if (pc.Is(CustomRoles.Specter) || pc.Is(CustomRoles.Haunter)) pc.RpcResetAbilityCooldown();

            Main.CheckShapeshift[pc.PlayerId] = false;
        }

        LateTask.New(() => Main.ProcessShapeshifts = true, 1f, log: false);

        CopyCat.ResetRoles();

        if (Options.DiseasedCDReset.GetBool())
        {
            var array = Main.KilledDiseased.Keys.ToArray();
            foreach (var pid in array)
            {
                Main.KilledDiseased[pid] = 0;
                GetPlayerById(pid)?.ResetKillCooldown();
            }

            Main.KilledDiseased.Clear();
        }

        if (Options.AntidoteCDReset.GetBool())
        {
            var array = Main.KilledAntidote.Keys.ToArray();
            foreach (var pid in array)
            {
                Main.KilledAntidote[pid] = 0;
                GetPlayerById(pid)?.ResetKillCooldown();
            }

            Main.KilledAntidote.Clear();
        }

        Damocles.AfterMeetingTasks();
        Stressed.AfterMeetingTasks();
        Circumvent.AfterMeetingTasks();

        if (Options.AirshipVariableElectrical.GetBool())
            AirshipElectricalDoors.Initialize();

        Main.DontCancelVoteList.Clear();

        DoorsReset.ResetDoors();
        RoleBlockManager.Reset();

        if ((MapNames)Main.NormalOptions.MapId == MapNames.Airship && AmongUsClient.Instance.AmHost && PlayerControl.LocalPlayer.Is(CustomRoles.GM))
        {
            LateTask.New(() => { PlayerControl.LocalPlayer.NetTransform.SnapTo(new(15.5f, 0.0f), (ushort)(PlayerControl.LocalPlayer.NetTransform.lastSequenceId + 8)); }, 11f, "GM Auto-TP Failsafe"); // TP to Main Hall
        }
    }

    public static void AfterPlayerDeathTasks(PlayerControl target, bool onMeeting = false)
    {
        try
        {
            // Record the first death
            if (Main.FirstDied == string.Empty)
                Main.FirstDied = target.FriendCode;

            switch (target.GetCustomRole())
            {
                case CustomRoles.Terrorist:
                    Logger.Info(target?.Data?.PlayerName + "Terrorist died", "MurderPlayer");
                    CheckTerroristWin(target?.Data);
                    break;
                case CustomRoles.Executioner:
                    if (Executioner.Target.ContainsKey(target.PlayerId))
                    {
                        Executioner.Target.Remove(target.PlayerId);
                        Executioner.SendRPC(target.PlayerId);
                    }

                    break;
                case CustomRoles.Lawyer:
                    if (Lawyer.Target.ContainsKey(target.PlayerId))
                    {
                        Lawyer.Target.Remove(target.PlayerId);
                        Lawyer.SendRPC(target.PlayerId);
                    }

                    break;
                case CustomRoles.PlagueDoctor:
                    PlagueDoctor.OnPDdeath(target.GetRealKiller(), target);
                    break;
                case CustomRoles.CyberStar:
                    if (GameStates.IsMeeting)
                    {
                        foreach (PlayerControl pc in Main.AllPlayerControls)
                        {
                            if ((!Options.ImpKnowCyberStarDead.GetBool() && pc.GetCustomRole().IsImpostor())
                                || (!Options.NeutralKnowCyberStarDead.GetBool() && pc.GetCustomRole().IsNeutral()))
                                continue;

                            SendMessage(string.Format(GetString("CyberStarDead"), target.GetRealName()), pc.PlayerId, ColorString(GetRoleColor(CustomRoles.CyberStar), GetString("CyberStarNewsTitle")));
                        }
                    }
                    else
                    {
                        if (!Main.CyberStarDead.Contains(target.PlayerId))
                            Main.CyberStarDead.Add(target.PlayerId);
                    }

                    break;
                case CustomRoles.Pelican:
                    Pelican.OnPelicanDied(target.PlayerId);
                    break;
                case CustomRoles.Devourer:
                    Devourer.OnDevourerDied(target.PlayerId);
                    break;
                case CustomRoles.Markseeker:
                    Markseeker.OnDeath(target);
                    break;
                case CustomRoles.Medic:
                    Medic.IsDead(target);
                    break;
            }

            if (target == null) return;

            Randomizer.OnAnyoneDeath(target);

            if (Executioner.Target.ContainsValue(target.PlayerId))
                Executioner.ChangeRoleByTarget(target);
            if (Lawyer.Target.ContainsValue(target.PlayerId))
                Lawyer.ChangeRoleByTarget(target);
            if (target.Is(CustomRoles.Stained))
                Stained.OnDeath(target, target.GetRealKiller());
            if (target.Is(CustomRoles.Spurt))
            {
                Spurt.DeathTask(target);
            }

            Postman.CheckAndResetTargets(target, isDeath: true);
            Hitman.CheckAndResetTargets();

            Hacker.AddDeadBody(target);
            Mortician.OnPlayerDead(target);
            Bloodhound.OnPlayerDead(target);
            Tracefinder.OnPlayerDead(target);
            Vulture.OnPlayerDead(target);
            Scout.OnPlayerDeath(target);
            Adventurer.OnAnyoneDead(target);
            Soothsayer.OnAnyoneDeath(target.GetRealKiller(), target);
            Amnesiac.OnAnyoneDeath(target);
            EHR.Impostor.Sentry.OnAnyoneMurder(target);

            if (QuizMaster.On) QuizMaster.Data.NumPlayersDeadThisRound++;

            FixedUpdatePatch.LoversSuicide(target.PlayerId, onMeeting);
            if (!target.HasGhostRole())
            {
                Main.AllPlayerSpeed[target.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                target.MarkDirtySettings();
            }
        }
        catch (Exception ex)
        {
            Logger.CurrentMethod();
            Logger.Exception(ex, "AfterPlayerDeathTasks");
        }
    }

    public static void CountAlivePlayers(bool sendLog = false)
    {
        int AliveImpostorCount = Main.AllAlivePlayerControls.Count(pc => pc.Is(CustomRoleTypes.Impostor));
        if (Main.AliveImpostorCount != AliveImpostorCount)
        {
            Logger.Info("Number of living Impostors: " + AliveImpostorCount, "CountAliveImpostors");
            Main.AliveImpostorCount = AliveImpostorCount;
            LastImpostor.SetSubRole();
        }

        if (sendLog)
        {
            var sb = new StringBuilder(100);
            if (Options.CurrentGameMode == CustomGameMode.Standard)
            {
                foreach (var countTypes in Enum.GetValues<CountTypes>())
                {
                    var playersCount = PlayersCount(countTypes);
                    if (playersCount == 0) continue;
                    sb.Append($"{countTypes}: {AlivePlayersCount(countTypes)}/{playersCount}, ");
                }
            }

            sb.Append($"All: {AllAlivePlayersCount}/{AllPlayersCount}");
            Logger.Info(sb.ToString(), "CountAlivePlayers");
        }

        if (AmongUsClient.Instance.AmHost && !Main.HasJustStarted)
            GameEndChecker.Prefix();
    }

    public static string GetVoteName(byte num)
    {
        var player = GetPlayerById(num);
        return num switch
        {
            < 15 when player != null => player.GetNameWithRole().RemoveHtmlTags(),
            253 => "Skip",
            254 => "None",
            255 => "Dead",
            _ => "invalid"
        };
    }

    public static string PadRightV2(this object text, int num)
    {
        var t = text.ToString();
        if (t == null) return string.Empty;
        int bc = t.Sum(c => Encoding.GetEncoding("UTF-8").GetByteCount(c.ToString()) == 1 ? 1 : 2);

        return t.PadRight(Mathf.Max(num - (bc - t.Length), 0));
    }

    public static void DumpLog(bool open = true)
    {
        string f = $"{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)}/EHR_Logs/";
        string t = DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss");
        string filename = $"{f}EHR-v{Main.PluginVersion}-{t}.log";
        if (!Directory.Exists(f)) Directory.CreateDirectory(f);
        FileInfo file = new($"{Environment.CurrentDirectory}/BepInEx/LogOutput.log");
        file.CopyTo(filename);
        if (!open) return;
        if (PlayerControl.LocalPlayer != null)
            HudManager.Instance?.Chat?.AddChat(PlayerControl.LocalPlayer, string.Format(GetString("Message.DumpfileSaved"), $"EHR v{Main.PluginVersion} {t}.log"));
        ProcessStartInfo psi = new("Explorer.exe")
            { Arguments = "/e,/select," + filename.Replace("/", "\\") };
        Process.Start(psi);
    }

    public static (int, int) GetDousedPlayerCount(byte playerId)
    {
        int doused = 0, all = 0;
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (pc.PlayerId == playerId)
                continue;
            all++;
            if (Arsonist.IsDoused.TryGetValue((playerId, pc.PlayerId), out var isDoused) && isDoused)
                doused++;
        }

        return (doused, all);
    }

    public static (int, int) GetDrawPlayerCount(byte playerId, out List<PlayerControl> winnerList)
    {
        int draw = 0;
        int all = Options.RevolutionistDrawCount.GetInt();
        int max = Main.AllAlivePlayerControls.Length;
        if (!Main.PlayerStates[playerId].IsDead) max--;
        winnerList = [];
        if (all > max) all = max;
        foreach (var pc in Main.AllPlayerControls.Where(pc => Revolutionist.IsDraw.TryGetValue((playerId, pc.PlayerId), out var isDraw) && isDraw).ToArray())
        {
            winnerList.Add(pc);
            draw++;
        }

        return (draw, all);
    }

    public static string SummaryTexts(byte id, bool disableColor = true, bool check = false)
    {
        //var RolePos = TranslationController.Instance.currentLanguage.languageID is SupportedLangs.English or SupportedLangs.Russian ? 37 : 34;
        //var KillsPos = TranslationController.Instance.currentLanguage.languageID is SupportedLangs.English or SupportedLangs.Russian ? 14 : 12;
        var name = Main.AllPlayerNames[id].RemoveHtmlTags().Replace("\r\n", string.Empty);
        if (id == PlayerControl.LocalPlayer.PlayerId) name = DataManager.player.Customization.Name;
        else name = GetPlayerById(id)?.Data.PlayerName ?? name;
        var taskState = Main.PlayerStates[id].TaskState;
        string TaskCount;
        if (taskState.hasTasks)
        {
            var info = GetPlayerInfoById(id);
            var TaskCompleteColor = HasTasks(info) ? Color.green : Color.cyan;
            var NonCompleteColor = HasTasks(info) ? Color.yellow : Color.white;

            if (Workhorse.IsThisRole(id))
                NonCompleteColor = Workhorse.RoleColor;

            var NormalColor = taskState.IsTaskFinished ? TaskCompleteColor : NonCompleteColor;
            if (Main.PlayerStates.TryGetValue(id, out var ps))
            {
                NormalColor = ps.MainRole switch
                {
                    CustomRoles.Crewpostor => Color.red,
                    CustomRoles.Cherokious => GetRoleColor(CustomRoles.Cherokious),
                    _ => NormalColor
                };
            }

            Color TextColor = NormalColor;
            string Completed = $"{taskState.CompletedTasksCount}";
            TaskCount = ColorString(TextColor, $" ({Completed}/{taskState.AllTasksCount})");
        }
        else
        {
            TaskCount = string.Empty;
        }

        string summary = $"{ColorString(Main.PlayerColors[id], name)} - {GetDisplayRoleName(id, true)}{TaskCount}{GetKillCountText(id)} ({GetVitalText(id, true)})";
        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.SoloKombat:
                summary = TranslationController.Instance.currentLanguage.languageID is SupportedLangs.SChinese or SupportedLangs.TChinese ? $"{GetProgressText(id)}\t<pos=22%>{ColorString(Main.PlayerColors[id], name)}</pos>" : $"{ColorString(Main.PlayerColors[id], name)}<pos=30%>{GetProgressText(id)}</pos>";
                if (GetProgressText(id).Trim() == string.Empty) return string.Empty;
                break;
            case CustomGameMode.FFA:
                summary = $"{ColorString(Main.PlayerColors[id], name)} {GetKillCountText(id, ffa: true)}";
                break;
            case CustomGameMode.Speedrun:
            case CustomGameMode.MoveAndStop:
                summary = $"{ColorString(Main.PlayerColors[id], name)} -{TaskCount.Replace("(", string.Empty).Replace(")", string.Empty)}  ({GetVitalText(id, true)})";
                break;
            case CustomGameMode.HotPotato:
                int time = HotPotatoManager.GetSurvivalTime(id);
                summary = $"{ColorString(Main.PlayerColors[id], name)} - <#e8cd46>Survived: <#ffffff>{(time == 0 ? "Until The End</color>" : $"{time}</color>s")}</color>  ({GetVitalText(id, true)})";
                break;
        }

        return check && GetDisplayRoleName(id, true).RemoveHtmlTags().Contains("INVALID:NotAssigned")
            ? "INVALID"
            : disableColor
                ? summary.RemoveHtmlTags()
                : summary;
    }

    public static string GetRemainingKillers(bool notify = false, bool forClairvoyant = false)
    {
        int impnum = 0;
        int neutralnum = 0;
        bool impShow = forClairvoyant || Options.ShowImpRemainOnEject.GetBool();
        bool nkShow = forClairvoyant || Options.ShowNKRemainOnEject.GetBool();

        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (impShow && pc.GetCustomRole().IsImpostor()) impnum++;
            if (nkShow && pc.IsNeutralKiller()) neutralnum++;
        }

        var sb = new StringBuilder();

        sb.Append(notify ? "<#777777>" : string.Empty);
        sb.Append(impnum == 1 ? GetString("RemainingText.Prefix.SingleImp") : GetString("RemainingText.Prefix.PluralImp"));
        sb.Append(notify ? " " : "\n");
        sb.Append(notify ? "<#ffffff>" : "<b>");
        sb.Append(impnum);
        sb.Append(notify ? "</color>" : "</b>");
        sb.Append(' ');
        sb.Append($"<#ff1919>{(impnum == 1 ? GetString("RemainingText.ImpSingle") : GetString("RemainingText.ImpPlural"))}</color>");
        sb.Append(" & ");
        sb.Append(notify ? "<#ffffff>" : "<b>");
        sb.Append(neutralnum);
        sb.Append(notify ? "</color>" : "</b>");
        sb.Append(' ');
        sb.Append($"<#ffab1b>{(neutralnum == 1 ? GetString("RemainingText.NKSingle") : GetString("RemainingText.NKPlural"))}</color>");
        sb.Append(GetString("RemainingText.Suffix"));
        sb.Append('.');
        sb.Append(notify ? "</color>" : string.Empty);

        return sb.ToString();
    }

    public static string RemoveHtmlTagsTemplate(this string str) => Regex.Replace(str, string.Empty, string.Empty);
    public static string RemoveHtmlTags(this string str) => Regex.Replace(str, "<[^>]*?>", string.Empty);

    public static bool CanMafiaKill()
    {
        if (Main.PlayerStates == null) return false;
        return !Main.AllAlivePlayerControls.Select(pc => pc.GetCustomRole()).Any(role => role != CustomRoles.Mafia && role.IsImpostor());
    }

    public static void FlashColor(Color color, float duration = 1f)
    {
        var hud = DestroyableSingleton<HudManager>.Instance;
        if (hud.FullScreen == null) return;
        var obj = hud.transform.FindChild("FlashColor_FullScreen")?.gameObject;
        if (obj == null)
        {
            obj = Object.Instantiate(hud.FullScreen.gameObject, hud.transform);
            obj.name = "FlashColor_FullScreen";
        }

        hud.StartCoroutine(Effects.Lerp(duration, new Action<float>(t =>
        {
            obj.SetActive(Math.Abs(t - 1f) > 0.1f);
            obj.GetComponent<SpriteRenderer>().color = new(color.r, color.g, color.b, Mathf.Clamp01((-2f * Mathf.Abs(t - 0.5f) + 1) * color.a / 2)); //アルファ値を0→目標→0に変化させる
        })));
    }

    public static Sprite LoadSprite(string path, float pixelsPerUnit = 1f)
    {
        try
        {
            if (CachedSprites.TryGetValue(path + pixelsPerUnit, out var sprite)) return sprite;
            Texture2D texture = LoadTextureFromResources(path);
            sprite = Sprite.Create(texture, new(0, 0, texture.width, texture.height), new(0.5f, 0.5f), pixelsPerUnit);
            sprite.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
            return CachedSprites[path + pixelsPerUnit] = sprite;
        }
        catch
        {
            Logger.Error($"读入Texture失败：{path}", "LoadImage");
        }

        return null;
    }

    public static Texture2D LoadTextureFromResources(string path)
    {
        try
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
            var texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            using MemoryStream ms = new();
            stream?.CopyTo(ms);
            ImageConversion.LoadImage(texture, ms.ToArray(), false);
            return texture;
        }
        catch
        {
            Logger.Error($"读入Texture失败：{path}", "LoadImage");
        }

        return null;
    }

    public static string ColorString(Color32 color, string str) => $"<color=#{color.r:x2}{color.g:x2}{color.b:x2}{color.a:x2}>{str}</color>";

    /// <summary>
    /// Darkness:Mix black and original color in a ratio of 1. If it is negative, it will be mixed with white.
    /// </summary>
    public static Color ShadeColor(this Color color, float Darkness = 0)
    {
        bool IsDarker = Darkness >= 0; //黒と混ぜる
        if (!IsDarker) Darkness = -Darkness;
        float Weight = IsDarker ? 0 : Darkness; //黒/白の比率
        float R = (color.r + Weight) / (Darkness + 1);
        float G = (color.g + Weight) / (Darkness + 1);
        float B = (color.b + Weight) / (Darkness + 1);
        return new(R, G, B, color.a);
    }

    public static void SetChatVisibleForAll()
    {
        if (!GameStates.IsInGame) return;
        MeetingHud.Instance = Object.Instantiate(HudManager.Instance.MeetingPrefab);
        MeetingHud.Instance.ServerStart(PlayerControl.LocalPlayer.PlayerId);
        AmongUsClient.Instance.Spawn(MeetingHud.Instance);
        MeetingHud.Instance.RpcClose();
    }

    public static bool TryCast<T>(this Il2CppObjectBase obj, out T casted) where T : Il2CppObjectBase
    {
        casted = obj.TryCast<T>();
        return casted != null;
    }

    public static string GetRegionName(IRegionInfo region = null)
    {
        region ??= ServerManager.Instance.CurrentRegion;

        string name = region.Name;

        if (AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame)
        {
            name = "Local Games";
            return name;
        }

        if (region.PingServer.EndsWith("among.us", StringComparison.Ordinal))
        {
            // Official server
            name = name switch
            {
                "North America" => "NA",
                "Europe" => "EU",
                "Asia" => "AS",
                _ => name
            };

            return name;
        }

        var Ip = region.Servers.FirstOrDefault()?.Ip ?? string.Empty;

        if (Ip.Contains("aumods.us", StringComparison.Ordinal) || Ip.Contains("duikbo.at", StringComparison.Ordinal))
        {
            // Official Modded Server
            if (Ip.Contains("au-eu")) name = "MEU";
            else if (Ip.Contains("au-as")) name = "MAS";
            else if (Ip.Contains("www.")) name = "MNA";

            return name;
        }

        if (name.Contains("nikocat233", StringComparison.OrdinalIgnoreCase))
        {
            name = name.Replace("nikocat233", "Niko233", StringComparison.OrdinalIgnoreCase);
        }

        return name;
    }

    private static int PlayersCount(CountTypes countTypes)
    {
        int count = 0;
        foreach (var state in Main.PlayerStates.Values)
        {
            if (state.countTypes == countTypes) count++;
        }

        return count;
    }

    public static int AlivePlayersCount(CountTypes countTypes) => Main.AllAlivePlayerControls.Count(pc => pc.Is(countTypes));

    public static bool IsPlayerModClient(this byte id) => Main.PlayerVersion.ContainsKey(id);
}