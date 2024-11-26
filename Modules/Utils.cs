using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
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
using EHR.Patches;
using HarmonyLib;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;
using Newtonsoft.Json;
using UnityEngine;
using static EHR.Translator;

namespace EHR
{
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
        public const string EmptyMessage = "<size=0>.</size>";
        private static readonly DateTime TimeStampStartTime = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly StringBuilder SelfSuffix = new();
        private static readonly StringBuilder SelfMark = new(20);
        private static readonly StringBuilder TargetSuffix = new();
        private static readonly StringBuilder TargetMark = new(20);

        private static readonly Dictionary<string, Sprite> CachedSprites = [];

        private static long LastNotifyRolesErrorTS = TimeStamp;

        public static long GameStartTimeStamp;

        public static readonly Dictionary<byte, (string Text, int Duration, bool Long)> LongRoleDescriptions = [];
        public static long TimeStamp => (long)(DateTime.Now.ToUniversalTime() - TimeStampStartTime).TotalSeconds;
        public static bool DoRPC => AmongUsClient.Instance.AmHost && Main.AllPlayerControls.Any(x => x.IsModClient() && !x.IsHost());
        public static int TotalTaskCount => Main.RealOptionsData.GetInt(Int32OptionNames.NumCommonTasks) + Main.RealOptionsData.GetInt(Int32OptionNames.NumLongTasks) + Main.RealOptionsData.GetInt(Int32OptionNames.NumShortTasks);
        private static int AllPlayersCount => Main.PlayerStates.Values.Count(state => state.countTypes != CountTypes.OutOfGame);
        public static int AllAlivePlayersCount => Main.AllAlivePlayerControls.Count(pc => !pc.Is(CountTypes.OutOfGame));
        public static bool IsAllAlive => Main.PlayerStates.Values.All(state => state.countTypes == CountTypes.OutOfGame || !state.IsDead);

        public static long GetTimeStamp(DateTime? dateTime = null)
        {
            return (long)((dateTime ?? DateTime.Now).ToUniversalTime() - TimeStampStartTime).TotalSeconds;
        }

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

                LateTask.New(() => ChatUpdatePatch.DoBlockChat = false, 6f, log: false);
            }
            else
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpc(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.AntiBlackout);
                writer.Write(text);
                writer.EndMessage();

                if (Options.EndWhenPlayerBug.GetBool())
                    LateTask.New(() => { Logger.SendInGame(GetString("AntiBlackOutRequestHostToForceEnd") /*, true*/); }, 3f, "Anti-Black Msg SendInGame");
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

        public static void CheckAndSetVentInteractions()
        {
            var shouldPerformVentInteractions = false;

            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                if (VentilationSystemDeterioratePatch.BlockVentInteraction(pc))
                {
                    VentilationSystemDeterioratePatch.LastClosestVent[pc.PlayerId] = pc.GetVentsFromClosest()[0].Id;
                    shouldPerformVentInteractions = true;
                }
            }

            if (shouldPerformVentInteractions) SetAllVentInteractions();
        }

        public static void TPAll(Vector2 location, bool log = true)
        {
            foreach (PlayerControl pc in Main.AllAlivePlayerControls) TP(pc.NetTransform, location, log);
        }

        public static bool TP(CustomNetworkTransform nt, Vector2 location, bool noCheckState = false, bool log = true)
        {
            PlayerControl pc = nt.myPlayer;

            if (!noCheckState)
            {
                if (pc.Is(CustomRoles.AntiTP)) return false;

                if (pc.inVent || pc.inMovingPlat || pc.onLadder || !pc.IsAlive() || pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation() || pc.MyPhysics.Animations.IsPlayingEnterVentAnimation())
                {
                    if (log) Logger.Warn($"Target ({pc.GetNameWithRole().RemoveHtmlTags()}) is in an un-teleportable state - Teleporting canceled", "TP");

                    return false;
                }
            }

            if (AmongUsClient.Instance.AmHost)
            {
                nt.SnapTo(location, (ushort)(nt.lastSequenceId + 328));
                nt.SetDirtyBit(uint.MaxValue);
            }

            var newSid = (ushort)(nt.lastSequenceId + 8);
            MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(nt.NetId, (byte)RpcCalls.SnapTo, SendOption.Reliable);
            NetHelpers.WriteVector2(location, messageWriter);
            messageWriter.Write(newSid);
            AmongUsClient.Instance.FinishRpcImmediately(messageWriter);

            if (log) Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} => {location}", "TP");

            CheckInvalidMovementPatch.LastPosition[pc.PlayerId] = location;
            CheckInvalidMovementPatch.ExemptedPlayers.Add(pc.PlayerId);

            return true;
        }

        public static bool TPToRandomVent(CustomNetworkTransform nt, bool log = true)
        {
            Il2CppReferenceArray<Vent> vents = ShipStatus.Instance.AllVents;
            Vent vent = vents.RandomElement();

            Logger.Info($"{nt.myPlayer.GetNameWithRole().RemoveHtmlTags()} => {vent.transform.position} (vent)", "TP");

            return TP(nt, new(vent.transform.position.x, vent.transform.position.y + 0.3636f), log);
        }

        public static ClientData GetClientById(int id)
        {
            try
            {
                ClientData client = AmongUsClient.Instance.allClients.ToArray().FirstOrDefault(cd => cd.Id == id);
                return client;
            }
            catch
            {
                return null;
            }
        }

        public static bool IsActive(SystemTypes type)
        {
            try
            {
                if (GameStates.IsLobby || !ShipStatus.Instance.Systems.TryGetValue(type, out ISystemType systemType)) return false;

                int mapId = Main.NormalOptions.MapId;

                switch (type)
                {
                    case SystemTypes.Electrical:
                    {
                        if (mapId == 5) return false; // if The Fungle return false

                        var SwitchSystem = systemType.TryCast<SwitchSystem>();
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
                                var HeliSabotageSystem = systemType.TryCast<HeliSabotageSystem>();
                                return HeliSabotageSystem != null && HeliSabotageSystem.IsActive;
                            }
                            default:
                            {
                                var ReactorSystemType = systemType.TryCast<ReactorSystemType>();
                                return ReactorSystemType is { IsActive: true };
                            }
                        }
                    }
                    case SystemTypes.Laboratory:
                    {
                        if (mapId != 2) return false; // Only Polus

                        var ReactorSystemType = systemType.TryCast<ReactorSystemType>();
                        return ReactorSystemType is { IsActive: true };
                    }
                    case SystemTypes.LifeSupp:
                    {
                        if (mapId is 2 or 4 or 5) return false; // Only Skeld & Mira HQ

                        var LifeSuppSystemType = systemType.TryCast<LifeSuppSystemType>();
                        return LifeSuppSystemType is { IsActive: true };
                    }
                    case SystemTypes.Comms:
                    {
                        if (mapId is 1 or 5) // Only Mira HQ & The Fungle
                        {
                            var HqHudSystemType = systemType.TryCast<HqHudSystemType>();
                            return HqHudSystemType is { IsActive: true };
                        }

                        var HudOverrideSystemType = systemType.TryCast<HudOverrideSystemType>();
                        return HudOverrideSystemType is { IsActive: true };
                    }
                    case SystemTypes.HeliSabotage:
                    {
                        if (mapId != 4) return false; // Only Airhip

                        var HeliSabotageSystem = systemType.TryCast<HeliSabotageSystem>();
                        return HeliSabotageSystem != null && HeliSabotageSystem.IsActive;
                    }
                    case SystemTypes.MushroomMixupSabotage:
                    {
                        if (mapId != 5) return false; // Only The Fungle

                        var MushroomMixupSabotageSystem = systemType.TryCast<MushroomMixupSabotageSystem>();
                        return MushroomMixupSabotageSystem != null && MushroomMixupSabotageSystem.IsActive;
                    }
                    default:
                        return false;
                }
            }
            catch (Exception e)
            {
                ThrowException(e);
                return false;
            }
        }

        public static void SetVision(this IGameOptions opt, bool HasImpVision)
        {
            if (HasImpVision)
            {
                opt.SetFloat(FloatOptionNames.CrewLightMod, opt.GetFloat(FloatOptionNames.ImpostorLightMod));

                if (IsActive(SystemTypes.Electrical)) opt.SetFloat(FloatOptionNames.CrewLightMod, opt.GetFloat(FloatOptionNames.CrewLightMod) * 5);

                return;
            }

            opt.SetFloat(FloatOptionNames.ImpostorLightMod, opt.GetFloat(FloatOptionNames.CrewLightMod));

            if (IsActive(SystemTypes.Electrical)) opt.SetFloat(FloatOptionNames.ImpostorLightMod, opt.GetFloat(FloatOptionNames.ImpostorLightMod) / 5);
        }

        public static void SetVisionV2(this IGameOptions opt)
        {
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, opt.GetFloat(FloatOptionNames.CrewLightMod));
            if (IsActive(SystemTypes.Electrical)) opt.SetFloat(FloatOptionNames.ImpostorLightMod, opt.GetFloat(FloatOptionNames.ImpostorLightMod) / 5);
        }

        public static void TargetDies(PlayerControl killer, PlayerControl target)
        {
            if (!target.Data.IsDead || GameStates.IsMeeting) return;

            CustomRoles targetRole = target.GetCustomRole();

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
                    Il2CppSystem.Collections.Generic.Dictionary<int, Il2CppSystem.Collections.Generic.Dictionary<string, Il2CppSystem.Collections.Generic.List<string>>> data = new();
                    Dictionary<int, Dictionary<CustomRoles, List<CustomRoles>>> dict = path.Contains("Always") ? Main.AlwaysSpawnTogetherCombos : Main.NeverSpawnTogetherCombos;

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
                    Dictionary<int, Dictionary<CustomRoles, List<CustomRoles>>> dict = path.Contains("Always") ? Main.AlwaysSpawnTogetherCombos : Main.NeverSpawnTogetherCombos;
                    dict.Clear();

                    foreach (Il2CppSystem.Collections.Generic.KeyValuePair<int, Il2CppSystem.Collections.Generic.Dictionary<string, Il2CppSystem.Collections.Generic.List<string>>> kvp in data)
                    {
                        dict[kvp.Key] = [];

                        foreach (Il2CppSystem.Collections.Generic.KeyValuePair<string, Il2CppSystem.Collections.Generic.List<string>> pair in kvp.Value)
                        {
                            var key = Enum.Parse<CustomRoles>(pair.Key);
                            dict[kvp.Key][key] = [];
                            foreach (string n in pair.Value) dict[kvp.Key][key].Add(Enum.Parse<CustomRoles>(n));
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

        public static void RemovePlayerFromPreviousRoleData(PlayerControl target)
        {
            switch (target.GetCustomRole())
            {
                case CustomRoles.Enigma:
                    Enigma.PlayerIdList.Remove(target.PlayerId);
                    break;
                case CustomRoles.Mediumshiper:
                    Mediumshiper.PlayerIdList.Remove(target.PlayerId);
                    break;
                case CustomRoles.Mortician:
                    Mortician.PlayerIdList.Remove(target.PlayerId);
                    break;
                case CustomRoles.Spiritualist:
                    Spiritualist.PlayerIdList.Remove(target.PlayerId);
                    break;
            }
        }

        public static string GetDisplayRoleName(byte playerId, bool pure = false, bool seeTargetBetrayalAddons = false)
        {
            (string, Color) TextData = GetRoleText(playerId, playerId, pure, seeTargetBetrayalAddons);
            return ColorString(TextData.Item2, TextData.Item1);
        }

        public static string GetRoleName(CustomRoles role, bool forUser = true)
        {
            return GetRoleString(role.ToString(), forUser);
        }

        public static string GetRoleMode(CustomRoles role, bool parentheses = true)
        {
            if (Options.HideGameSettings.GetBool() && Main.AllPlayerControls.Length > 1) return string.Empty;

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

        public static Color GetRoleColor(CustomRoles role)
        {
            string hexColor = Main.RoleColors.GetValueOrDefault(role, "#ffffff");
            _ = ColorUtility.TryParseHtmlString(hexColor, out Color c);
            return c;
        }

        public static string GetRoleColorCode(CustomRoles role)
        {
            string hexColor = Main.RoleColors.GetValueOrDefault(role, "#ffffff");
            return hexColor;
        }

        public static (string, Color) GetRoleText(byte seerId, byte targetId, bool pure = false, bool seeTargetBetrayalAddons = false)
        {
            CustomRoles seerMainRole = Main.PlayerStates[seerId].MainRole;
            List<CustomRoles> seerSubRoles = Main.PlayerStates[seerId].SubRoles;

            CustomRoles targetMainRole = Main.PlayerStates[targetId].MainRole;
            List<CustomRoles> targetSubRoles = Main.PlayerStates[targetId].SubRoles;

            bool self = seerId == targetId || Main.PlayerStates[seerId].IsDead;

            if (!self && Main.DiedThisRound.Contains(seerId)) return (string.Empty, Color.white);

            bool isHnsAgentOverride = CustomGameMode.HideAndSeek.IsActiveOrIntegrated() && targetMainRole == CustomRoles.Agent && HnSManager.PlayerRoles[seerId].Interface.Team != Team.Impostor;
            var loversShowDifferentRole = false;

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
            Color RoleColor = GetRoleColor(isHnsAgentOverride ? CustomRoles.Hider : loversShowDifferentRole ? CustomRoles.Impostor : targetMainRole);

            if (LastImpostor.CurrentId == targetId) RoleText = GetRoleString("Last-") + RoleText;

            if (Options.NameDisplayAddons.GetBool() && !pure && self)
            {
                foreach (CustomRoles subRole in targetSubRoles)
                {
                    if (subRole is not CustomRoles.LastImpostor and not CustomRoles.Madmate and not CustomRoles.Charmed and not CustomRoles.Recruit and not CustomRoles.Lovers and not CustomRoles.Contagious and not CustomRoles.Bloodlust)
                    {
                        string str = GetString("Prefix." + subRole);
                        if (!subRole.IsAdditionRole()) str = GetString(subRole.ToString());

                        RoleText = ColorString(GetRoleColor(subRole), (Options.AddBracketsToAddons.GetBool() ? "<#ffffff>(</color>" : string.Empty) + str + (Options.AddBracketsToAddons.GetBool() ? "<#ffffff>)</color>" : string.Empty) + " ") + RoleText;
                    }
                }
            }

            if (seerMainRole == CustomRoles.LovingImpostor && self) RoleColor = GetRoleColor(CustomRoles.LovingImpostor);

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

            if (targetSubRoles.Contains(CustomRoles.Charmed) && (self || pure || seeTargetBetrayalAddons || seerMainRole == CustomRoles.Succubus || (Succubus.TargetKnowOtherTarget.GetBool() && seerSubRoles.Contains(CustomRoles.Charmed))))
            {
                RoleColor = GetRoleColor(CustomRoles.Charmed);
                RoleText = GetRoleString("Charmed-") + RoleText;
            }

            if (targetSubRoles.Contains(CustomRoles.Contagious) && (self || pure || seeTargetBetrayalAddons || seerMainRole == CustomRoles.Virus || (Virus.TargetKnowOtherTarget.GetBool() && seerSubRoles.Contains(CustomRoles.Contagious))))
            {
                RoleColor = GetRoleColor(CustomRoles.Contagious);
                RoleText = GetRoleString("Contagious-") + RoleText;
            }

            if (targetSubRoles.Contains(CustomRoles.Bloodlust) && (self || pure || seeTargetBetrayalAddons))
            {
                RoleColor = GetRoleColor(CustomRoles.Bloodlust);
                RoleText = $"{GetString("Prefix.Bloodlust")} {RoleText}";
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
            PlayerState state = Main.PlayerStates[playerId];
            string deathReason = state.IsDead ? GetString("DeathReason." + state.deathReason) : GetString("Alive");

            if (realKillerColor)
            {
                byte KillerId = state.GetRealKiller();
                Color color = KillerId != byte.MaxValue ? Main.PlayerColors[KillerId] : GetRoleColor(CustomRoles.Doctor);
                if (state.deathReason == PlayerState.DeathReason.Disconnected) color = new(255, 255, 255, 50);

                deathReason = ColorString(color, deathReason);
            }

            return deathReason;
        }

        public static MessageWriter CreateRPC(CustomRPC rpc)
        {
            return AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)rpc, SendOption.Reliable);
        }

        public static void EndRPC(MessageWriter writer)
        {
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void SendRPC(CustomRPC rpc, params object[] data)
        {
            if (!DoRPC) return;

            MessageWriter w;

            try
            {
                w = CreateRPC(rpc);
            }
            catch
            {
                return;
            }

            try
            {
                foreach (object o in data)
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
                        case InnerNetObject ino:
                            w.WriteNetObject(ino);
                            break;
                        default:
                            try
                            {
                                if (o != null && Enum.TryParse(o.GetType(), o.ToString(), out object e) && e != null) w.WritePacked((int)e);
                            }
                            catch (InvalidCastException e)
                            {
                                ThrowException(e);
                            }

                            break;
                    }
                }
            }
            finally
            {
                EndRPC(w);
            }
        }

        public static void IncreaseAbilityUseLimitOnKill(PlayerControl killer)
        {
            if (Main.PlayerStates[killer.PlayerId].Role is Mafioso { IsEnable: true } mo) mo.OnMurder(killer, null);

            float add = GetSettingNameAndValueForRole(killer.GetCustomRole(), "AbilityUseGainWithEachKill");
            killer.RpcIncreaseAbilityUseLimitBy(add);
        }

        public static void ThrowException(Exception ex, [CallerFilePath] string fileName = "", [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string callerMemberName = "")
        {
            try
            {
                StackTrace st = new(1, true);
                StackFrame[] stFrames = st.GetFrames();

                StackFrame firstFrame = stFrames.FirstOrDefault();

                StringBuilder sb = new();
                sb.Append($" {ex.GetType().Name}: {ex.Message}\n      thrown by {ex.Source}\n      at {ex.TargetSite}\n      in {fileName.Split('\\')[^1]}\n      at line {lineNumber}\n      in method \"{callerMemberName}\"\n------ Method Stack Trace ------");

                var skip = true;

                foreach (StackFrame sf in stFrames)
                {
                    if (skip)
                    {
                        skip = false;
                        continue;
                    }

                    MethodBase callerMethod = sf.GetMethod();

                    string callerMethodName = callerMethod?.Name;
                    string callerClassName = callerMethod?.DeclaringType?.FullName;

                    sb.Append($"\n      at {callerClassName}.{callerMethodName}");
                }

                sb.Append("\n------ End of Method Stack Trace ------");
                sb.Append("\n------ Exception ------\n   ");

                sb.Append(ex.StackTrace?.Replace("\r\n", "\n").Replace("\\n", "\n").Replace("\n", "\n   "));

                sb.Append("\n------ End of Exception ------");
                sb.Append("\n------ Exception Stack Trace ------\n");

                StackTrace stEx = new(ex, true);
                StackFrame[] stFramesEx = stEx.GetFrames();

                foreach (StackFrame sf in stFramesEx)
                {
                    MethodBase callerMethod = sf.GetMethod();

                    string callerMethodName = callerMethod?.Name;
                    string callerClassName = callerMethod?.DeclaringType?.FullName;

                    sb.Append($"\n      at {callerClassName}.{callerMethodName} in {sf.GetFileName()}, line {sf.GetFileLineNumber()}");
                }

                sb.Append("\n------ End of Exception Stack Trace ------");

                Logger.Error(sb.ToString(), firstFrame?.GetMethod()?.ToString(), multiLine: true);
            }
            catch { }
        }

        public static void SetAllVentInteractions()
        {
            VentilationSystemDeterioratePatch.SerializeV2(ShipStatus.Instance.Systems[SystemTypes.Ventilation].Cast<VentilationSystem>());
        }

        public static bool HasTasks(NetworkedPlayerInfo p, bool ForRecompute = true)
        {
            if (GameStates.IsLobby) return false;

            if (p.Tasks == null) return false;

            if (p.Role == null) return false;

            var hasTasks = true;
            PlayerState state = Main.PlayerStates[p.PlayerId];
            if (p.Disconnected) return false;

            if (p.Role.IsImpostor) hasTasks = false;

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.SoloKombat: return false;
                case CustomGameMode.FFA: return false;
                case CustomGameMode.MoveAndStop: return !p.IsDead;
                case CustomGameMode.HotPotato: return false;
                case CustomGameMode.Speedrun: return !p.IsDead;
                case CustomGameMode.CaptureTheFlag: return false;
                case CustomGameMode.NaturalDisasters: return false;
                case CustomGameMode.RoomRush: return false;
                case CustomGameMode.HideAndSeek: return HnSManager.HasTasks(p);
                case CustomGameMode.AllInOne: return true;
            }

            CustomRoles role = state.MainRole;

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
                case CustomRoles.NoteKiller:
                case CustomRoles.Vortex:
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
                case CustomRoles.Technician:
                case CustomRoles.Tank:
                case CustomRoles.Gaslighter:
                case CustomRoles.Impartial:
                case CustomRoles.Backstabber:
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
                case CustomRoles.Dad when ((Dad)state.Role).DoneTasks:
                case CustomRoles.Workaholic:
                case CustomRoles.Terrorist:
                case CustomRoles.Sunnyboy:
                case CustomRoles.Convict:
                case CustomRoles.Opportunist:
                case CustomRoles.Executioner:
                case CustomRoles.Lawyer:
                case CustomRoles.Phantasm:
                    if (ForRecompute) hasTasks = false;

                    break;
                case CustomRoles.Cherokious:
                case CustomRoles.Crewpostor:
                    if (ForRecompute && !p.IsDead) hasTasks = false;

                    if (p.IsDead) hasTasks = false;

                    break;
                case CustomRoles.Wizard:
                    hasTasks = true;
                    break;
                default:
                    if (role.IsImpostor()) hasTasks = false;

                    break;
            }

            foreach (CustomRoles subRole in state.SubRoles)
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
                    case CustomRoles.Specter:
                    case CustomRoles.Haunter:
                        hasTasks = !ForRecompute;
                        break;
                }
            }

            if (CopyCat.Instances.Any(x => x.CopyCatPC.PlayerId == p.PlayerId) && ForRecompute && (!Options.UsePets.GetBool() || CopyCat.UsePet.GetBool())) hasTasks = false;

            hasTasks |= role.UsesPetInsteadOfKill() && role is not (CustomRoles.Refugee or CustomRoles.Necromancer or CustomRoles.Deathknight or CustomRoles.Sidekick);

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
                       (pc.Is(CustomRoles.President) && !Options.PresidentCanBeMadmate.GetBool()) ||
                       pc.Is(CustomRoles.NiceSwapper) ||
                       pc.Is(CustomRoles.Speedrunner) ||
                       pc.Is(CustomRoles.Needy) ||
                       pc.Is(CustomRoles.Loyal) ||
                       pc.Is(CustomRoles.SuperStar) ||
                       pc.Is(CustomRoles.CyberStar) ||
                       pc.Is(CustomRoles.Egoist) ||
                       pc.Is(CustomRoles.DualPersonality)
                   );
        }

        public static bool IsRoleTextEnabled(PlayerControl __instance)
        {
            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.CaptureTheFlag or CustomGameMode.NaturalDisasters or CustomGameMode.RoomRush:
                case CustomGameMode.Standard when CustomRoles.Altruist.RoleExist() && Main.DiedThisRound.Contains(PlayerControl.LocalPlayer.PlayerId):
                    return PlayerControl.LocalPlayer.Is(CustomRoles.GM);
                case CustomGameMode.FFA or CustomGameMode.SoloKombat or CustomGameMode.MoveAndStop or CustomGameMode.HotPotato or CustomGameMode.Speedrun or CustomGameMode.AllInOne:
                case CustomGameMode.HideAndSeek when HnSManager.IsRoleTextEnabled(PlayerControl.LocalPlayer, __instance):
                    return true;
            }

            if ((Main.VisibleTasksCount && PlayerControl.LocalPlayer.Data.IsDead && Options.GhostCanSeeOtherRoles.GetBool()) || (PlayerControl.LocalPlayer.Is(CustomRoles.Mimic) && Main.VisibleTasksCount && __instance.Data.IsDead && Options.MimicCanSeeDeadRoles.GetBool())) return true;

            if (__instance.IsLocalPlayer()) return true;

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
                case CustomRoles.Marshall when Marshall.CanSeeMarshall(PlayerControl.LocalPlayer) && __instance.GetTaskState().IsTaskFinished:
                    return true;
            }

            return (__instance.IsMadmate() && PlayerControl.LocalPlayer.IsMadmate() && Options.MadmateKnowWhosMadmate.GetBool()) ||
                   (__instance.IsMadmate() && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && Options.ImpKnowWhosMadmate.GetBool()) ||
                   (__instance.Is(CustomRoleTypes.Impostor) && PlayerControl.LocalPlayer.IsMadmate() && Options.MadmateKnowWhosImp.GetBool()) ||
                   (__instance.Is(CustomRoles.Mimic) && Main.VisibleTasksCount && __instance.Data.IsDead) ||
                   (__instance.Is(CustomRoleTypes.Impostor) && PlayerControl.LocalPlayer.Is(CustomRoles.Crewpostor) && Options.AlliesKnowCrewpostor.GetBool()) ||
                   (__instance.Is(CustomRoleTypes.Impostor) && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && Options.ImpKnowAlliesRole.GetBool()) ||
                   (Main.LoversPlayers.TrueForAll(x => x.PlayerId == __instance.PlayerId || x.IsLocalPlayer()) && Main.LoversPlayers.Count == 2 && Lovers.LoverKnowRoles.GetBool()) ||
                   (CustomTeamManager.AreInSameCustomTeam(__instance.PlayerId, PlayerControl.LocalPlayer.PlayerId) && CustomTeamManager.IsSettingEnabledForPlayerTeam(__instance.PlayerId, CTAOption.KnowRoles)) ||
                   Main.PlayerStates.Values.Any(x => x.Role.KnowRole(PlayerControl.LocalPlayer, __instance)) ||
                   PlayerControl.LocalPlayer.IsRevealedPlayer(__instance) ||
                   (PlayerControl.LocalPlayer.Is(CustomRoles.God) && God.KnowInfo.GetValue() == 2) ||
                   PlayerControl.LocalPlayer.Is(CustomRoles.GM) ||
                   Markseeker.PlayerIdList.Any(x => Main.PlayerStates[x].Role is Markseeker { IsEnable: true, TargetRevealed: true } ms && ms.MarkedId == __instance.PlayerId) ||
                   Main.GodMode.Value;
        }

        public static string GetFormattedRoomName(string roomName)
        {
            return roomName == "Outside" ? "<#00ffa5>Outside</color>" : $"<#ffffff>In</color> <#00ffa5>{roomName}</color>";
        }

        public static string GetFormattedVectorText(Vector2 pos)
        {
            return $"<#777777>(at {pos.ToString().Replace("(", string.Empty).Replace(")", string.Empty)})</color>";
        }

        public static string GetProgressText(PlayerControl pc)
        {
            TaskState taskState = pc.GetTaskState();
            var Comms = false;

            if (taskState.HasTasks)
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
                case CustomGameMode.MoveAndStop: return GetTaskCount(playerId, comms, true);
                case CustomGameMode.Speedrun: return string.Empty;
            }

            StringBuilder ProgressText = new();
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

            if (ProgressText.Length != 0 && !ProgressText.ToString().RemoveHtmlTags().StartsWith(' ')) ProgressText.Insert(0, ' ');

            return ProgressText.ToString();
        }

        public static string GetAbilityUseLimitDisplay(byte playerId, bool usingAbility = false)
        {
            try
            {
                float limit = playerId.GetAbilityUseLimit();
                if (float.IsNaN(limit) /* || limit is > 100 or < 0*/) return string.Empty;

                Color TextColor;

                if (limit < 1)
                    TextColor = Color.red;
                else if (usingAbility)
                    TextColor = Color.green;
                else
                    TextColor = GetRoleColor(Main.PlayerStates[playerId].MainRole).ShadeColor(0.25f);

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
                if (playerId == 0 && Main.GM.Value) return string.Empty;

                TaskState taskState = Main.PlayerStates[playerId].TaskState;
                if (!taskState.HasTasks) return string.Empty;

                NetworkedPlayerInfo info = GetPlayerInfoById(playerId);
                Color TaskCompleteColor = HasTasks(info) ? Color.green : GetRoleColor(Main.PlayerStates[playerId].MainRole).ShadeColor(0.5f);
                Color NonCompleteColor = HasTasks(info) ? Color.yellow : Color.white;

                if (Workhorse.IsThisRole(playerId)) NonCompleteColor = Workhorse.RoleColor;

                Color NormalColor = taskState.IsTaskFinished ? TaskCompleteColor : NonCompleteColor;

                if (Main.PlayerStates.TryGetValue(playerId, out PlayerState ps))
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
                return ColorString(TextColor, $" {(moveAndStop ? "<size=2>" : string.Empty)}{Completed}/{taskState.AllTasksCount}{(moveAndStop ? $" <#ffffff>({MoveAndStop.GetLivesRemaining(playerId)} \u2665)</color></size>" : string.Empty)}");
            }
            catch
            {
                return string.Empty;
            }
        }

        public static void ShowActiveSettingsHelp(byte PlayerId = byte.MaxValue)
        {
            SendMessage(GetString("CurrentActiveSettingsHelp") + ":", PlayerId);

            if (Options.DisableDevices.GetBool()) SendMessage(GetString("DisableDevicesInfo"), PlayerId);
            if (Options.SyncButtonMode.GetBool()) SendMessage(GetString("SyncButtonModeInfo"), PlayerId);
            if (Options.SabotageTimeControl.GetBool()) SendMessage(GetString("SabotageTimeControlInfo"), PlayerId);
            if (Options.RandomMapsMode.GetBool()) SendMessage(GetString("RandomMapsModeInfo"), PlayerId);

            if (Main.GM.Value) SendMessage(GetRoleName(CustomRoles.GM) + GetString("GMInfoLong"), PlayerId);

            foreach (CustomRoles role in Enum.GetValues<CustomRoles>().Where(role => role.IsEnable() && !role.IsVanilla()))
                SendMessage(GetRoleName(role) + GetRoleMode(role) + GetString($"{role}InfoLong"), PlayerId);

            if (Options.NoGameEnd.GetBool()) SendMessage(GetString("NoGameEndInfo"), PlayerId);
        }

        /// <summary>
        ///     Gets all players within a specified radius from the specified location
        /// </summary>
        /// <param name="radius">The radius</param>
        /// <param name="from">The location which the radius is counted from</param>
        /// <returns>A list containing all PlayerControls within the specified range from the specified location</returns>
        public static IEnumerable<PlayerControl> GetPlayersInRadius(float radius, Vector2 from)
        {
            return from tg in Main.AllAlivePlayerControls let dis = Vector2.Distance(@from, tg.Pos()) where !Pelican.IsEaten(tg.PlayerId) && !tg.inVent where dis <= radius select tg;
        }

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

            StringBuilder sb = new();
            sb.Append($" \u2605 {GetString("TabGroup.SystemSettings")}");
            Options.GroupedOptions[TabGroup.SystemSettings].Do(CheckAndAppendOptionString);
            sb.Append($"\n\n \u2605 {GetString("TabGroup.GameSettings")}");
            Options.GroupedOptions[TabGroup.GameSettings].Do(CheckAndAppendOptionString);

            SendMessage(sb.ToString().RemoveHtmlTags(), PlayerId);
            return;

            void CheckAndAppendOptionString(OptionItem item)
            {
                if (item.GetBool() && item.Parent == null && !item.IsCurrentlyHidden())
                {
                    sb.Append($"\n{item.GetName(true)}: {item.GetString()}");
                }
            }
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

            StringBuilder sb = new();

            sb.Append(GetString("Settings")).Append(':');

            foreach (KeyValuePair<CustomRoles, OptionItem> role in Options.CustomRoleCounts)
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
            }

            foreach (OptionItem opt in OptionItem.AllOptions)
            {
                if (opt.GetBool() && opt.Parent == null && opt.Id is >= 80000 and < 640000 && !opt.IsCurrentlyHidden())
                {
                    if (opt.Name is "KillFlashDuration" or "RoleAssigningAlgorithm")
                        sb.Append($"\n【{opt.GetName(true)}: {opt.GetString()}】\n");
                    else
                        sb.Append($"\n【{opt.GetName(true)}】\n");

                    ShowChildrenSettings(opt, ref sb);
                }
            }

            SendMessage(sb.ToString().RemoveHtmlTags(), PlayerId);
        }

        public static void CopyCurrentSettings()
        {
            StringBuilder sb = new();

            if (Options.HideGameSettings.GetBool() && !AmongUsClient.Instance.AmHost)
            {
                ClipboardHelper.PutClipboardString(GetString("Message.HideGameSettings"));
                return;
            }

            sb.Append($"━━━━━━━━━━━━【{GetString("Roles")}】━━━━━━━━━━━━");

            foreach (KeyValuePair<CustomRoles, OptionItem> role in Options.CustomRoleCounts)
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
            }

            sb.Append($"━━━━━━━━━━━━【{GetString("Settings")}】━━━━━━━━━━━━");

            foreach (OptionItem opt in OptionItem.AllOptions.Where(x => x.GetBool() && x.Parent == null && x.Id is >= 80000 and < 640000 && !x.IsCurrentlyHidden()))
            {
                if (opt.Name == "KillFlashDuration")
                    sb.Append($"\n【{opt.GetName(true)}: {opt.GetString()}】\n");
                else
                    sb.Append($"\n【{opt.GetName(true)}】\n");

                ShowChildrenSettings(opt, ref sb);
            }

            sb.Append("\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501");
            ClipboardHelper.PutClipboardString(sb.ToString().RemoveHtmlTags());
        }

        public static void ShowActiveRoles(byte PlayerId = byte.MaxValue)
        {
            if (Options.HideGameSettings.GetBool() && PlayerId != byte.MaxValue)
            {
                SendMessage(GetString("Message.HideGameSettings"), PlayerId);
                return;
            }

            StringBuilder sb = new();
            sb.Append($"\n{GetRoleName(CustomRoles.GM)}: {(Main.GM.Value ? GetString("RoleRate") : GetString("RoleOff"))}");

            StringBuilder impsb = new();
            StringBuilder neutralsb = new();
            StringBuilder crewsb = new();
            StringBuilder addonsb = new();
            StringBuilder ghostsb = new();

            foreach (CustomRoles role in CustomGameMode.HideAndSeek.IsActiveOrIntegrated() ? HnSManager.AllHnSRoles : Enum.GetValues<CustomRoles>().Except(HnSManager.AllHnSRoles))
            {
                string mode;

                try
                {
                    mode = !role.IsAdditionRole() || role.IsGhostRole()
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
                    var roleDisplay = $"\n{ColorString(GetRoleColor(role).ShadeColor(0.25f), GetString(role.ToString()))}: {mode} x{role.GetCount()}";

                    if (role.IsGhostRole())
                        ghostsb.Append(roleDisplay);
                    else if (role.IsAdditionRole())
                        addonsb.Append(roleDisplay);
                    else if (role.IsCrewmate())
                        crewsb.Append(roleDisplay);
                    else if (role.IsImpostor() || role.IsMadmate())
                        impsb.Append(roleDisplay);
                    else if (role.IsNeutral()) neutralsb.Append(roleDisplay);
                }
            }

            SendMessage(sb.Append("\n.").ToString(), PlayerId, GetString("GMRoles"));
            SendMessage(impsb.Append("\n.").ToString(), PlayerId, ColorString(GetRoleColor(CustomRoles.Impostor), GetString("ImpostorRoles")));
            SendMessage(crewsb.Append("\n.").ToString(), PlayerId, ColorString(GetRoleColor(CustomRoles.Crewmate), GetString("CrewmateRoles")));
            SendMessage(neutralsb.Append("\n.").ToString(), PlayerId, GetString("NeutralRoles"));
            SendMessage(ghostsb.Append("\n.").ToString(), PlayerId, GetString("GhostRoles"));
            SendMessage(addonsb.Append("\n.").ToString(), PlayerId, GetString("AddonRoles"));
        }

        public static void ShowChildrenSettings(OptionItem option, ref StringBuilder sb, int deep = 0, bool f1 = false, bool disableColor = true)
        {
            foreach (var opt in option.Children.Select((v, i) => new { Value = v, Index = i + 1 }))
            {
                switch (opt.Value.Name)
                {
                    case "DisableSkeldDevices" when Main.CurrentMap is not MapNames.Skeld and not MapNames.Dleks:
                    case "DisableMiraHQDevices" when Main.CurrentMap != MapNames.Mira:
                    case "DisablePolusDevices" when Main.CurrentMap != MapNames.Polus:
                    case "DisableAirshipDevices" when Main.CurrentMap != MapNames.Airship:
                    case "PolusReactorTimeLimit" when Main.CurrentMap != MapNames.Polus:
                    case "AirshipReactorTimeLimit" when Main.CurrentMap != MapNames.Airship:
                    case "ImpCanBeRole" or "CrewCanBeRole" or "NeutralCanBeRole" when f1:
                        continue;
                }

                if (deep > 0)
                {
                    sb.Append(string.Concat(Enumerable.Repeat("┃", Mathf.Max(deep - 1, 0))));
                    sb.Append(opt.Index == option.Children.Count ? "┗ " : "┣ ");
                }

                string value = opt.Value.GetString().Replace("ON", "<#00ffa5>ON</color>").Replace("OFF", "<#ff0000>OFF</color>");
                var name = $"{opt.Value.GetName(disableColor).Replace("color=", string.Empty)}</color>";
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

            StringBuilder sb = new();

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
                    list2.AddRange(cloneRoles.Select(id => (FFAManager.GetRankFromScore(id), id)));

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
                    list3.AddRange(cloneRoles.Select(id => (MoveAndStop.GetRankFromScore(id), id)));

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
                case CustomGameMode.AllInOne:
                case CustomGameMode.RoomRush:
                case CustomGameMode.NaturalDisasters:
                case CustomGameMode.CaptureTheFlag:
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
                            if (!EndGamePatch.SummaryText.TryGetValue(id, out string summaryText)) continue;

                            if (summaryText.Contains("<INVALID:NotAssigned>")) continue;

                            sb.Append("\n\u3000 ").Append(summaryText);
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

            if (!CustomGameMode.Standard.IsActiveOrIntegrated()) return;

            StringBuilder sb = new();
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

            if (!CustomGameMode.Standard.IsActiveOrIntegrated()) return;

            string result = Main.LastAddOns.Values.Join(delimiter: "\n");
            SendMessage("\n", PlayerId, result);
        }

        public static string GetSubRolesText(byte id, bool disableColor = false, bool intro = false, bool summary = false)
        {
            List<CustomRoles> SubRoles = Main.PlayerStates[id].SubRoles;
            if (SubRoles.Count == 0) return string.Empty;

            StringBuilder sb = new();

            if (intro)
            {
                bool isLovers = SubRoles.Contains(CustomRoles.Lovers) && Main.PlayerStates[id].MainRole is not CustomRoles.LovingCrewmate and not CustomRoles.LovingImpostor;
                SubRoles.RemoveAll(x => x is CustomRoles.NotAssigned or CustomRoles.LastImpostor or CustomRoles.Lovers);

                if (isLovers) sb.Append($"{ColorString(GetRoleColor(CustomRoles.Lovers), " ♥")}");

                if (SubRoles.Count == 0) return sb.ToString();

                sb.Append("<size=15%>");

                if (SubRoles.Count == 1)
                {
                    CustomRoles role = SubRoles[0];

                    string RoleText = ColorString(GetRoleColor(role), GetRoleName(role));
                    sb.Append($"{ColorString(Color.gray, GetString("Modifier"))}{RoleText}");
                }
                else
                {
                    sb.Append($"{ColorString(Color.gray, GetString("Modifiers"))}");

                    for (var i = 0; i < SubRoles.Count; i++)
                    {
                        if (i != 0) sb.Append(", ");

                        CustomRoles role = SubRoles[i];

                        string RoleText = ColorString(GetRoleColor(role), GetRoleName(role));
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

                    string RoleText = disableColor ? GetRoleName(role) : ColorString(GetRoleColor(role), GetRoleName(role));
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
            PlayerControl player = GetPlayerById(ID);
            SendMessage(ChatCommands.AllCommands.Where(x => x.CanUseCommand(player, false)).Aggregate("<size=70%>", (s, c) => s + $"\n<b>/{c.CommandForms.Where(f => f.All(char.IsAscii)).MinBy(f => f.Length)}{(c.Arguments.Length == 0 ? string.Empty : $" {c.Arguments.Split(' ').Select((x, i) => ColorString(GetColor(i), x)).Join(delimiter: " ")}")}</b> \u2192 {c.Description}"), ID, GetString("CommandList"));
            return;

            Color GetColor(int i)
            {
                return i switch
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
        }

        public static void CheckTerroristWin(NetworkedPlayerInfo Terrorist)
        {
            if (!AmongUsClient.Instance.AmHost) return;

            TaskState taskState = GetPlayerById(Terrorist.PlayerId).GetTaskState();

            if (taskState.IsTaskFinished && (!Main.PlayerStates[Terrorist.PlayerId].IsSuicide || Options.CanTerroristSuicideWin.GetBool()))
            {
                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    if (pc.Is(CustomRoles.Terrorist))
                        Main.PlayerStates[pc.PlayerId].deathReason = Main.PlayerStates[pc.PlayerId].deathReason == PlayerState.DeathReason.Vote ? PlayerState.DeathReason.etc : PlayerState.DeathReason.Suicide;
                    else if (!pc.Data.IsDead) pc.Suicide(PlayerState.DeathReason.Bombed, Terrorist.Object);
                }

                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Terrorist);
                CustomWinnerHolder.WinnerIds.Add(Terrorist.PlayerId);
            }
        }

        public static void CheckAndSpawnAdditionalRefugee(NetworkedPlayerInfo deadPlayer)
        {
            try
            {
                if (!CustomGameMode.Standard.IsActiveOrIntegrated() || deadPlayer == null || deadPlayer.Object.Is(CustomRoles.Refugee) || Main.HasJustStarted || !GameStates.InGame || !Options.SpawnAdditionalRefugeeOnImpsDead.GetBool() || Main.AllAlivePlayerControls.Length < Options.SpawnAdditionalRefugeeMinAlivePlayers.GetInt() || CustomRoles.Refugee.RoleExist(true) || Main.AllAlivePlayerControls == null || Main.AllAlivePlayerControls.Length == 0 || Main.AllAlivePlayerControls.Any(x => x.PlayerId != deadPlayer.PlayerId && (x.Is(CustomRoleTypes.Impostor) || (x.IsNeutralKiller() && !Options.SpawnAdditionalRefugeeWhenNKAlive.GetBool())))) return;

                PlayerControl[] ListToChooseFrom = Main.AllAlivePlayerControls.Where(x => x.PlayerId != deadPlayer.PlayerId && x.Is(CustomRoleTypes.Crewmate) && !x.Is(CustomRoles.Loyal)).ToArray();

                if (ListToChooseFrom.Length > 0)
                {
                    PlayerControl pc = ListToChooseFrom.RandomElement();
                    pc.RpcSetCustomRole(CustomRoles.Refugee);
                    pc.SetKillCooldown();
                    Main.PlayerStates[pc.PlayerId].RemoveSubRole(CustomRoles.Madmate);
                    Logger.Warn($"{pc.GetRealName()} is now a Refugee since all Impostors are dead", "Add Refugee");
                }
                else
                    Logger.Msg("No Player to change to Refugee.", "Add Refugee");
            }
            catch (Exception e)
            {
                ThrowException(e);
            }
        }

        public static void SendMessage(string text, byte sendTo = byte.MaxValue, string title = "", bool noSplit = false)
        {
            if (!AmongUsClient.Instance.AmHost)
            {
                if (sendTo == PlayerControl.LocalPlayer.PlayerId)
                {
                    MessageWriter w = AmongUsClient.Instance.StartRpc(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.RequestSendMessage);
                    w.Write(text);
                    w.Write(sendTo);
                    w.Write(title);
                    w.Write(noSplit);
                    w.EndMessage();
                }

                return;
            }

            if (title == "") title = "<color=#8b32a8>" + GetString("DefaultSystemMessageTitle") + "</color>";

            if (title.Count(x => x == '\u2605') == 2 && !title.Contains('\n'))
            {
                if (title.Contains('<') && title.Contains('>') && title.Contains('#'))
                    title = $"{title[..(title.IndexOf('>') + 1)]}\u27a1{title.Replace("\u2605", "")[..(title.LastIndexOf('<') - 2)]}\u2b05";
                else
                    title = "\u27a1" + title.Replace("\u2605", "") + "\u2b05";
            }

            text = text.Replace("color=", string.Empty);

            if (text.Length >= 1200 && !noSplit)
            {
                string[] lines = text.Split('\n');
                var shortenedText = string.Empty;

                foreach (string line in lines)
                {
                    if (shortenedText.Length + line.Length < 1200)
                    {
                        shortenedText += line + "\n";
                        continue;
                    }

                    if (shortenedText.Length >= 1200)
                        shortenedText.Chunk(1200).Do(x => SendMessage(new(x), sendTo, title, true));
                    else
                        SendMessage(shortenedText, sendTo, title, true);

                    string sentText = shortenedText;
                    shortenedText = line + "\n";

                    if (Regex.Matches(sentText, "<size").Count > Regex.Matches(sentText, "</size>").Count)
                    {
                        string sizeTag = Regex.Matches(sentText, @"<size=\d+\.?\d*%?>")[^1].Value;
                        shortenedText = sizeTag + shortenedText;
                    }
                }

                if (shortenedText.Length > 0) SendMessage(shortenedText, sendTo, title, true);

                return;
            }

            try
            {
                string pureText = text.RemoveHtmlTags();
                string pureTitle = title.RemoveHtmlTags();
                Logger.Info($" Message: {pureText[..(pureText.Length <= 300 ? pureText.Length : 300)]} - To: {(sendTo == byte.MaxValue ? "Everyone" : $"{GetPlayerById(sendTo)?.GetRealName()}")} - Title: {pureTitle[..(pureTitle.Length <= 300 ? pureTitle.Length : 300)]}", "SendMessage");
            }
            catch
            {
                Logger.Info(" Message sent", "SendMessage");
            }

            text = text.RemoveHtmlTagsTemplate();

            if (sendTo == byte.MaxValue)
                Main.MessagesToSend.Add((text, sendTo, title));
            else
                ChatUpdatePatch.SendMessage(Main.AllAlivePlayerControls.MinBy(x => x.PlayerId) ?? Main.AllPlayerControls.MinBy(x => x.PlayerId) ?? PlayerControl.LocalPlayer, text, sendTo, title);
        }

        public static void ApplySuffix(PlayerControl player)
        {
            if (!AmongUsClient.Instance.AmHost || player == null) return;

            if (!player.AmOwner && !player.FriendCode.GetDevUser().HasTag() && !ChatCommands.IsPlayerModerator(player.FriendCode) && !ChatCommands.IsPlayerVIP(player.FriendCode)) return;

            string name = Main.AllPlayerNames.TryGetValue(player.PlayerId, out string n) ? n : string.Empty;
            if (Main.NickName != string.Empty && player.AmOwner) name = Main.NickName;

            if (name == string.Empty) return;

            if (AmongUsClient.Instance.IsGameStarted)
            {
                if (Options.FormatNameMode.GetInt() == 1 && Main.NickName == string.Empty) name = Palette.GetColorName(player.Data.DefaultOutfit.ColorId);
            }
            else
            {
                if (!GameStates.IsLobby) return;

                if (player.AmOwner)
                {
                    if (GameStates.IsOnlineGame || GameStates.IsLocalGame) name = $"<color={GetString("HostColor")}>{GetString("HostText")}</color><color={GetString("IconColor")}>{GetString("Icon")}</color><color={GetString("NameColor")}>{name}</color>";

                    string modeText = GetString($"Mode{Options.CurrentGameMode}");

                    name = Options.CurrentGameMode switch
                    {
                        CustomGameMode.SoloKombat => $"<color=#f55252><size=1.7>{modeText}</size></color>\r\n{name}",
                        CustomGameMode.FFA => $"<color=#00ffff><size=1.7>{modeText}</size></color>\r\n{name}",
                        CustomGameMode.MoveAndStop => $"<color=#00ffa5><size=1.7>{modeText}</size></color>\r\n{name}",
                        CustomGameMode.HotPotato => $"<color=#e8cd46><size=1.7>{modeText}</size></color>\r\n{name}",
                        CustomGameMode.HideAndSeek => $"<color=#345eeb><size=1.7>{modeText}</size></color>\r\n{name}",
                        CustomGameMode.CaptureTheFlag => $"<color=#1313c2><size=1.7>{modeText}</size></color>\r\n{name}",
                        CustomGameMode.NaturalDisasters => $"<color=#03fc4a><size=1.7>{modeText}</size></color>\r\n{name}",
                        CustomGameMode.RoomRush => $"<color=#ffab1b><size=1.7>{modeText}</size></color>\r\n{name}",
                        CustomGameMode.AllInOne => $"<color=#f542ad><size=1.7>{modeText}</size></color>\r\n{name}",
                        CustomGameMode.Speedrun => ColorString(GetRoleColor(CustomRoles.Speedrunner), $"<size=1.7>{modeText}</size>\r\n") + name,
                        _ => name
                    };
                }

                DevUser devUser = player.FriendCode.GetDevUser();
                bool isMod = ChatCommands.IsPlayerModerator(player.FriendCode);
                bool isVIP = ChatCommands.IsPlayerVIP(player.FriendCode);
                bool hasTag = devUser.HasTag();

                if (hasTag || isMod || isVIP)
                {
                    string tag = hasTag ? devUser.GetTag() : string.Empty;
                    if (tag == "null") tag = string.Empty;

                    if (player.AmOwner || player.IsModClient())
                    {
                        var modTagModded = $"<size=1.4>{GetString("ModeratorTag")}\r\n</size>";
                        var vipTagModded = $"<size=1.4>{GetString("VIPTag")}\r\n</size>";
                        name = $"{(hasTag ? tag : string.Empty)}{(isMod ? modTagModded : string.Empty)}{(isVIP ? vipTagModded : string.Empty)}{name}";
                    }
                    else
                    {
                        var modTagVanilla = $"<size=1.4>{GetString("ModeratorTag")} - </size>";
                        var vipTagVanilla = $"<size=1.4>{GetString("VIPTag")} - </size>";
                        name = $"{(hasTag ? tag.Replace("\r\n", " - ") : string.Empty)}{(isMod ? modTagVanilla : string.Empty)}{(isVIP ? vipTagVanilla : string.Empty)}{name}";
                    }
                }

                if (player.AmOwner)
                {
                    name = Options.GetSuffixMode() switch
                    {
                        SuffixModes.EHR => $"{name}\r\n<color={Main.ModColor}>EHR v{Main.PluginDisplayVersion}</color>",
                        SuffixModes.Streaming => $"{name}\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixMode.Streaming")}</color></size>",
                        SuffixModes.Recording => $"{name}\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixMode.Recording")}</color></size>",
                        SuffixModes.RoomHost => $"{name}\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixMode.RoomHost")}</color></size>",
                        SuffixModes.OriginalName => $"{name}\r\n<size=1.7><color={Main.ModColor}>{DataManager.player.Customization.Name}</color></size>",
                        SuffixModes.DoNotKillMe => $"{name}\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixModeText.DoNotKillMe")}</color></size>",
                        SuffixModes.NoAndroidPlz => $"{name}\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixModeText.NoAndroidPlz")}</color></size>",
                        SuffixModes.AutoHost => $"{name}\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixModeText.AutoHost")}</color></size>",
                        _ => name
                    };
                }
            }

            if (name != player.name && player.CurrentOutfitType == PlayerOutfitType.Default) player.RpcSetName(name);
        }

        public static Dictionary<string, int> GetAllPlayerLocationsCount()
        {
            Dictionary<string, int> playerRooms = [];

            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                if (!pc.IsAlive() || Pelican.IsEaten(pc.PlayerId)) return null;

                Il2CppReferenceArray<PlainShipRoom> Rooms = ShipStatus.Instance.AllRooms;
                if (Rooms == null) return null;

                foreach (PlainShipRoom room in Rooms)
                {
                    if (!room.roomArea) continue;

                    if (!pc.Collider.IsTouching(room.roomArea)) continue;

                    string roomName = GetString($"{room.RoomId}");
                    if (!playerRooms.TryAdd(roomName, 1)) playerRooms[roomName]++;
                }
            }

            return playerRooms;
        }

        public static float GetSettingNameAndValueForRole(CustomRoles role, string settingName)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            Type[] types = Assembly.GetExecutingAssembly().GetTypes();
            FieldInfo field = types.SelectMany(x => x.GetFields(flags)).FirstOrDefault(x => x.Name == $"{role}{settingName}");

            if (field == null)
            {
                FieldInfo tempField = null;

                foreach (Type x in types)
                {
                    var any = false;

                    foreach (FieldInfo f in x.GetFields(flags))
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
                add = float.MaxValue;
            else
            {
                if (field.GetValue(null) is OptionItem optionItem)
                    add = optionItem.GetFloat();
                else
                    add = float.MaxValue;
            }

            return add;
        }

        public static string ColoredPlayerName(this byte id)
        {
            return ColorString(Main.PlayerColors.GetValueOrDefault(id, Color.white), Main.AllPlayerNames.GetValueOrDefault(id, GetPlayerById(id)?.GetRealName() ?? $"Someone (ID {id})"));
        }

        public static PlayerControl GetPlayer(this byte id)
        {
            return GetPlayerById(id);
        }

        public static PlayerControl GetPlayerById(int PlayerId, bool fast = true)
        {
            if (PlayerId is > byte.MaxValue or < byte.MinValue) return null;

            if (fast && GameStates.IsInGame && Main.PlayerStates.TryGetValue((byte)PlayerId, out PlayerState state) && state.Player != null) return state.Player;

            return Main.AllPlayerControls.FirstOrDefault(x => x.PlayerId == PlayerId);
        }

        public static NetworkedPlayerInfo GetPlayerInfoById(int PlayerId)
        {
            return GameData.Instance.AllPlayers.ToArray().FirstOrDefault(info => info.PlayerId == PlayerId);
        }

        public static IEnumerator NotifyEveryoneAsync()
        {
            var count = 0;
            PlayerControl[] aapc = Main.AllAlivePlayerControls;

            foreach (PlayerControl seer in aapc)
            {
                foreach (PlayerControl target in aapc)
                {
                    NotifyRoles(SpecifySeer: seer, SpecifyTarget: target);
                    if (count++ % 2 == 0) yield return null;
                }
            }
        }

        public static void NotifyRoles(bool isForMeeting = false, PlayerControl SpecifySeer = null, PlayerControl SpecifyTarget = null, bool NoCache = false, bool ForceLoop = false, bool CamouflageIsForMeeting = false, bool GuesserIsForMeeting = false, bool MushroomMixup = false)
        {
            if (!SetUpRoleTextPatch.IsInIntro && ((SpecifySeer != null && SpecifySeer.IsModClient() && (CustomGameMode.Standard.IsActiveOrIntegrated() || SpecifySeer.IsHost())) || !AmongUsClient.Instance.AmHost || Main.AllPlayerControls == null || (GameStates.IsMeeting && !isForMeeting))) return;

            DoNotifyRoles(isForMeeting, SpecifySeer, SpecifyTarget, NoCache, ForceLoop, CamouflageIsForMeeting, GuesserIsForMeeting, MushroomMixup);
        }

        public static void DoNotifyRoles(bool isForMeeting = false, PlayerControl SpecifySeer = null, PlayerControl SpecifyTarget = null, bool NoCache = false, bool ForceLoop = false, bool CamouflageIsForMeeting = false, bool GuesserIsForMeeting = false, bool MushroomMixup = false)
        {
            PlayerControl[] apc = Main.AllPlayerControls;

            PlayerControl[] seerList = SpecifySeer != null ? [SpecifySeer] : apc;
            PlayerControl[] targetList = SpecifyTarget != null ? [SpecifyTarget] : apc;

            long now = TimeStamp;

            // seer: Players who can see changes made here
            // target: Players subject to changes that seer can see
            foreach (PlayerControl seer in seerList)
            {
                try
                {
                    if (seer == null || seer.Data.Disconnected || (seer.IsModClient() && (seer.IsHost() || CustomGameMode.Standard.IsActiveOrIntegrated()))) continue;

                    // During intro scene, set team name for non-modded clients and skip the rest.
                    string SelfName;
                    Team seerTeam = seer.GetTeam();
                    CustomRoles seerRole = seer.GetCustomRole();

                    if (SetUpRoleTextPatch.IsInIntro && (seerRole.IsDesyncRole() || seer.Is(CustomRoles.Bloodlust)) && CustomGameMode.Standard.IsActiveOrIntegrated())
                    {
                        const string iconTextLeft = "<color=#ffffff>\u21e8</color>";
                        const string iconTextRight = "<color=#ffffff>\u21e6</color>";
                        const string roleNameUp = "</size><size=1450%>\n \n</size>";

                        var selfTeamName = $"<size=450%>{iconTextLeft} <font=\"VCR SDF\" material=\"VCR Black Outline\">{ColorString(seerTeam.GetTeamColor(), $"{seerTeam}")}</font> {iconTextRight}</size><size=500%>\n \n</size>";
                        SelfName = $"{selfTeamName}\r\n<size=150%>{seerRole.ToColoredString()}</size>{roleNameUp}";

                        seer.RpcSetNamePrivate(SelfName, seer);
                        continue;
                    }

                    if (GameStates.IsLobby)
                    {
                        SelfName = seer.GetRealName();
                        seer.RpcSetNameEx(SelfName);
                        continue;
                    }

                    if (seer.Is(CustomRoles.Car) && !isForMeeting)
                    {
                        seer.RpcSetNamePrivate(Car.Name, force: NoCache);
                        continue;
                    }

                    var fontSize = "1.7";
                    if (isForMeeting && (seer.GetClient().PlatformData.Platform == Platforms.Playstation || seer.GetClient().PlatformData.Platform == Platforms.Switch)) fontSize = "70%";

                    // Text containing progress, such as tasks
                    string SelfTaskText = GameStates.IsLobby ? string.Empty : GetProgressText(seer);

                    SelfMark.Clear();
                    SelfSuffix.Clear();

                    if (!GameStates.IsLobby)
                    {
                        if (AntiBlackout.SkipTasks) SelfSuffix.AppendLine(GetString("AntiBlackoutSkipTasks"));

                        if (!CustomGameMode.Standard.IsActiveOrIntegrated()) goto GameMode0;

                        SelfMark.Append(Snitch.GetWarningArrow(seer));
                        if (Main.LoversPlayers.Exists(x => x.PlayerId == seer.PlayerId)) SelfMark.Append(ColorString(GetRoleColor(CustomRoles.Lovers), " ♥"));

                        if (BallLightning.IsGhost(seer)) SelfMark.Append(ColorString(GetRoleColor(CustomRoles.BallLightning), "■"));

                        SelfMark.Append(Medic.GetMark(seer, seer));
                        SelfMark.Append(Gaslighter.GetMark(seer, seer, isForMeeting));
                        SelfMark.Append(Gamer.TargetMark(seer, seer));
                        SelfMark.Append(Sniper.GetShotNotify(seer.PlayerId));
                        if (Silencer.ForSilencer.Contains(seer.PlayerId)) SelfMark.Append(ColorString(GetRoleColor(CustomRoles.Silencer), "╳"));

                        GameMode0:

                        if (Options.CurrentGameMode is not CustomGameMode.Standard and not CustomGameMode.HideAndSeek) goto GameMode;

                        Main.PlayerStates.Values.Do(x => SelfSuffix.Append(x.Role.GetSuffix(seer, seer, meeting: isForMeeting)));

                        SelfSuffix.Append(Spurt.GetSuffix(seer));

                        SelfSuffix.Append(CustomTeamManager.GetSuffix(seer));

                        if (!isForMeeting)
                        {
                            if (Options.UsePets.GetBool() && Main.AbilityCD.TryGetValue(seer.PlayerId, out (long StartTimeStamp, int TotalCooldown) time))
                            {
                                long remainingCD = time.TotalCooldown - (now - time.StartTimeStamp) + 1;
                                SelfSuffix.Append(string.Format(GetString("CDPT"), remainingCD > 60 ? "> 60" : remainingCD));
                            }

                            if (seer.Is(CustomRoles.Asthmatic)) SelfSuffix.Append(Asthmatic.GetSuffixText(seer.PlayerId));
                            if (seer.Is(CustomRoles.Sonar)) SelfSuffix.Append(Sonar.GetSuffix(seer, isForMeeting));
                            if (seer.Is(CustomRoles.Deadlined)) SelfSuffix.Append(Deadlined.GetSuffix(seer));
                            if (seer.Is(CustomRoles.Introvert)) SelfSuffix.Append(Introvert.GetSelfSuffix(seer));
                            if (seer.Is(CustomRoles.Allergic)) SelfSuffix.Append(Allergic.GetSelfSuffix(seer));

                            SelfSuffix.Append(Bloodmoon.GetSuffix(seer));
                            SelfSuffix.Append(Haunter.GetSuffix(seer));

                            switch (seerRole)
                            {
                                case CustomRoles.SuperStar when Options.EveryOneKnowSuperStar.GetBool():
                                    SelfMark.Append(ColorString(GetRoleColor(CustomRoles.SuperStar), "★"));
                                    break;
                                case CustomRoles.Monitor:
                                    if (AntiAdminer.IsAdminWatch) SelfSuffix.Append($"{GetString("AntiAdminerAD")} ({AntiAdminer.PlayersNearDevices.Where(x => x.Value.Contains(AntiAdminer.Device.Admin)).Select(x => x.Key.ColoredPlayerName()).Join()})");
                                    if (AntiAdminer.IsVitalWatch) SelfSuffix.Append($"{GetString("AntiAdminerVI")} ({AntiAdminer.PlayersNearDevices.Where(x => x.Value.Contains(AntiAdminer.Device.Vitals)).Select(x => x.Key.ColoredPlayerName()).Join()})");
                                    if (AntiAdminer.IsDoorLogWatch) SelfSuffix.Append($"{GetString("AntiAdminerDL")} ({AntiAdminer.PlayersNearDevices.Where(x => x.Value.Contains(AntiAdminer.Device.DoorLog)).Select(x => x.Key.ColoredPlayerName()).Join()})");
                                    if (AntiAdminer.IsCameraWatch) SelfSuffix.Append($"{GetString("AntiAdminerCA")} ({AntiAdminer.PlayersNearDevices.Where(x => x.Value.Contains(AntiAdminer.Device.Camera)).Select(x => x.Key.ColoredPlayerName()).Join()})");
                                    break;
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
                            if (isForMeeting) SelfMark.Append(Wasp.GetStungMark(seer.PlayerId));
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
                                SelfSuffix.Append(MoveAndStop.GetSuffixText(seer));
                                break;
                            case CustomGameMode.HotPotato:
                                SelfSuffix.Append(HotPotatoManager.GetSuffixText(seer.PlayerId));
                                break;
                            case CustomGameMode.Speedrun:
                                SelfSuffix.Append(SpeedrunManager.GetSuffixText(seer));
                                break;
                            case CustomGameMode.HideAndSeek:
                                SelfSuffix.Append(HnSManager.GetSuffixText(seer, seer));
                                break;
                            case CustomGameMode.CaptureTheFlag:
                                SelfSuffix.Append(CTFManager.GetSuffixText(seer, seer));
                                break;
                            case CustomGameMode.NaturalDisasters:
                                SelfSuffix.Append(NaturalDisasters.SuffixText());
                                break;
                            case CustomGameMode.RoomRush:
                                SelfSuffix.Append(RoomRush.GetSuffix(seer));
                                break;
                            case CustomGameMode.AllInOne:
                                bool alive = seer.IsAlive();
                                if (alive) SelfSuffix.Append(SoloKombatManager.GetDisplayHealth(seer) + "\n");
                                if (alive) SelfSuffix.Append(MoveAndStop.GetSuffixText(seer) + "\n");
                                SelfSuffix.Append(HotPotatoManager.GetSuffixText(seer.PlayerId) + "\n");
                                if (alive) SelfSuffix.Append(string.Format(GetString("DamoclesTimeLeft"), SpeedrunManager.Timers[seer.PlayerId]) + "\n");
                                SelfSuffix.Append(NaturalDisasters.SuffixText() + "\n");
                                const StringSplitOptions splitFlags = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;
                                SelfSuffix.Append(RoomRush.GetSuffix(seer).Split('\n', splitFlags).Join(delimiter: " - "));
                                break;
                        }
                    }

                    string SeerRealName = seer.GetRealName(isForMeeting);

                    if (!GameStates.IsLobby)
                    {
                        if ((CustomGameMode.FFA.IsActiveOrIntegrated() && FFAManager.FFATeamMode.GetBool()) || CustomGameMode.HotPotato.IsActiveOrIntegrated())
                            SeerRealName = SeerRealName.ApplyNameColorData(seer, seer, isForMeeting);

                        if (!isForMeeting && MeetingStates.FirstMeeting && Options.ChangeNameToRoleInfo.GetBool() && Options.CurrentGameMode is not CustomGameMode.FFA and not CustomGameMode.MoveAndStop and not CustomGameMode.HotPotato and not CustomGameMode.Speedrun and not CustomGameMode.CaptureTheFlag and not CustomGameMode.NaturalDisasters and not CustomGameMode.RoomRush and not CustomGameMode.AllInOne)
                        {
                            CustomTeamManager.CustomTeam team = CustomTeamManager.GetCustomTeam(seer.PlayerId);

                            if (team != null)
                            {
                                SeerRealName = ColorString(
                                    team.RoleRevealScreenBackgroundColor == "*" || !ColorUtility.TryParseHtmlString(team.RoleRevealScreenBackgroundColor, out Color teamColor)
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
                            else if (CustomGameMode.HideAndSeek.IsActiveOrIntegrated())
                            {
                                if (GameStartTimeStamp + 40 > now) SeerRealName = HnSManager.GetRoleInfoText(seer);
                            }
                            else if (Options.ChangeNameToRoleInfo.GetBool() && !seer.IsModClient())
                            {
                                bool showLongInfo = LongRoleDescriptions.TryGetValue(seer.PlayerId, out (string Text, int Duration, bool Long) description) && GameStartTimeStamp + description.Duration > now;
                                string mHelp = (!showLongInfo || description.Long) && CustomGameMode.Standard.IsActiveOrIntegrated() ? "\n" + GetString("MyRoleCommandHelp") : string.Empty;

                                SeerRealName = seerTeam switch
                                {
                                    Team.Impostor when seer.IsMadmate() => $"<size=150%><color=#ff1919>{GetString("YouAreMadmate")}</size></color>\n<size=90%>{(showLongInfo ? description.Text : seer.GetRoleInfo()) + mHelp}</size>",
                                    Team.Impostor => $"\n<size=90%>{(showLongInfo ? description.Text : seer.GetRoleInfo()) + mHelp}</size>",
                                    Team.Crewmate => $"<size=150%><color=#8cffff>{GetString("YouAreCrewmate")}</size></color>\n<size=90%>{(showLongInfo ? description.Text : seer.GetRoleInfo()) + mHelp}</size>",
                                    Team.Neutral => $"<size=150%><color=#ffab1b>{GetString("YouAreNeutral")}</size></color>\n<size=90%>{(showLongInfo ? description.Text : seer.GetRoleInfo()) + mHelp}</size>",
                                    _ => SeerRealName
                                };
                            }
                        }
                    }

                    // Combine seer's job title and SelfTaskText with seer's player name and SelfMark
                    string SelfRoleName = GameStates.IsLobby ? string.Empty : $"<size={fontSize}>{seer.GetDisplayRoleName()}{SelfTaskText}</size>";
                    string SelfDeathReason = seer.KnowDeathReason(seer) && !GameStates.IsLobby ? $"\n<size=1.5>『{ColorString(GetRoleColor(CustomRoles.Doctor), GetVitalText(seer.PlayerId))}』</size>" : string.Empty;
                    SelfName = $"{ColorString(GameStates.IsLobby ? Color.white : seer.GetRoleColor(), SeerRealName)}{SelfDeathReason}{SelfMark}";

                    if (!CustomGameMode.Standard.IsActiveOrIntegrated() || GameStates.IsLobby) goto GameMode2;

                    SelfName = seerRole switch
                    {
                        CustomRoles.Arsonist when seer.IsDouseDone() => $"{ColorString(seer.GetRoleColor(), GetString("EnterVentToWin"))}",
                        CustomRoles.Revolutionist when seer.IsDrawDone() => $">{ColorString(seer.GetRoleColor(), string.Format(GetString("EnterVentWinCountDown"), Revolutionist.RevolutionistCountdown.GetValueOrDefault(seer.PlayerId, 10)))}",
                        _ => SelfName
                    };

                    if (Pelican.IsEaten(seer.PlayerId)) SelfName = $"{ColorString(GetRoleColor(CustomRoles.Pelican), GetString("EatenByPelican"))}";

                    if (Deathpact.IsInActiveDeathpact(seer)) SelfName = Deathpact.GetDeathpactString(seer);

                    // Devourer
                    if (Devourer.HideNameOfConsumedPlayer.GetBool() && Devourer.PlayerIdList.Any(x => Main.PlayerStates[x].Role is Devourer { IsEnable: true } dv && dv.PlayerSkinsCosumed.Contains(seer.PlayerId)) && !CamouflageIsForMeeting) SelfName = GetString("DevouredName");

                    // Camouflage
                    if (Camouflage.IsCamouflage && !CamouflageIsForMeeting) SelfName = $"<size=0>{SelfName}</size>";

                    GameMode2:

                    if (!GameStates.IsLobby)
                    {
                        if (NameNotifyManager.GetNameNotify(seer, out string name) && name.Length > 0) SelfName = name;

                        switch (Options.CurrentGameMode)
                        {
                            case CustomGameMode.SoloKombat:
                                SoloKombatManager.GetNameNotify(seer, ref SelfName);
                                SelfName = $"<size={fontSize}>{SelfTaskText}</size>\r\n{SelfName}";
                                break;
                            case CustomGameMode.FFA:
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

                            if (target.Is(CustomRoles.Car) && !isForMeeting)
                            {
                                target.RpcSetNamePrivate(Car.Name, seer, NoCache);
                                continue;
                            }

                            if ((IsActive(SystemTypes.MushroomMixupSabotage) || MushroomMixup) && target.IsAlive() && !seer.Is(CustomRoleTypes.Impostor) && Main.ResetCamPlayerList.Contains(seer.PlayerId))
                            {
                                target.RpcSetNamePrivate("<size=0%>", force: NoCache);
                            }
                            else
                            {
                                TargetMark.Clear();

                                if (!CustomGameMode.Standard.IsActiveOrIntegrated() || GameStates.IsLobby) goto BeforeEnd2;

                                TargetMark.Append(Witch.GetSpelledMark(target.PlayerId, isForMeeting));
                                if (isForMeeting) TargetMark.Append(Wasp.GetStungMark(target.PlayerId));

                                if (target.Is(CustomRoles.SuperStar) && Options.EveryOneKnowSuperStar.GetBool())
                                    TargetMark.Append(ColorString(GetRoleColor(CustomRoles.SuperStar), "★"));

                                if (BallLightning.IsGhost(target)) TargetMark.Append(ColorString(GetRoleColor(CustomRoles.BallLightning), "■"));

                                TargetMark.Append(Snitch.GetWarningMark(seer, target));

                                if ((seer.Data.IsDead || Main.LoversPlayers.Exists(x => x.PlayerId == seer.PlayerId)) && Main.LoversPlayers.Exists(x => x.PlayerId == target.PlayerId))
                                    TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Lovers)}> ♥</color>");

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
                                            TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Arsonist)}>▲</color>");

                                        else if (Arsonist.ArsonistTimer.TryGetValue(seer.PlayerId, out (PlayerControl Player, float Timer) ar_kvp) && ar_kvp.Player == target) TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Arsonist)}>△</color>");

                                        break;
                                    case CustomRoles.Revolutionist:
                                        if (seer.IsDrawPlayer(target)) TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Revolutionist)}>●</color>");

                                        if (Revolutionist.RevolutionistTimer.TryGetValue(seer.PlayerId, out (PlayerControl PLAYER, float TIMER) ar_kvp1) && ar_kvp1.PLAYER == target) TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Revolutionist)}>○</color>");

                                        break;
                                    case CustomRoles.Farseer:
                                        if (Farseer.FarseerTimer.TryGetValue(seer.PlayerId, out (PlayerControl PLAYER, float TIMER) ar_kvp2) && ar_kvp2.PLAYER == target) TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Farseer)}>○</color>");

                                        break;
                                    case CustomRoles.Analyst:
                                        if ((Main.PlayerStates[seer.PlayerId].Role as Analyst).CurrentTarget.ID == target.PlayerId) TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Analyst)}>○</color>");

                                        break;
                                    case CustomRoles.Samurai: // Same as Analyst
                                        if ((Main.PlayerStates[seer.PlayerId].Role as Samurai).Target.Id == target.PlayerId) TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Samurai)}>○</color>");

                                        break;
                                    case CustomRoles.Puppeteer when Puppeteer.PuppeteerList.ContainsValue(seer.PlayerId) && Puppeteer.PuppeteerList.ContainsKey(target.PlayerId):
                                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Impostor)}>◆</color>");
                                        break;
                                }

                                BeforeEnd2:

                                bool shouldSeeTargetAddons = seer.PlayerId == target.PlayerId || new[] { seer, target }.All(x => x.Is(Team.Impostor));

                                string TargetRoleText =
                                    (seer.Data.IsDead && Options.GhostCanSeeOtherRoles.GetBool()) ||
                                    (seer.Is(CustomRoles.Mimic) && target.Data.IsDead && Options.MimicCanSeeDeadRoles.GetBool()) ||
                                    (target.Is(CustomRoles.Gravestone) && target.Data.IsDead) ||
                                    (Main.LoversPlayers.TrueForAll(x => x.PlayerId == seer.PlayerId || x.PlayerId == target.PlayerId) && Main.LoversPlayers.Count == 2 && Lovers.LoverKnowRoles.GetBool()) ||
                                    (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoleTypes.Impostor) && Options.ImpKnowAlliesRole.GetBool()) ||
                                    (seer.IsMadmate() && target.Is(CustomRoleTypes.Impostor) && Options.MadmateKnowWhosImp.GetBool()) ||
                                    (seer.Is(CustomRoleTypes.Impostor) && target.IsMadmate() && Options.ImpKnowWhosMadmate.GetBool()) ||
                                    (seer.Is(CustomRoles.Crewpostor) && target.Is(CustomRoleTypes.Impostor) && Options.CrewpostorKnowsAllies.GetBool()) ||
                                    (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Crewpostor) && Options.AlliesKnowCrewpostor.GetBool()) ||
                                    (seer.IsMadmate() && target.IsMadmate() && Options.MadmateKnowWhosMadmate.GetBool()) ||
                                    ((seer.Is(CustomRoles.Sidekick) || seer.Is(CustomRoles.Recruit) || seer.Is(CustomRoles.Jackal)) && (target.Is(CustomRoles.Sidekick) || target.Is(CustomRoles.Recruit) || target.Is(CustomRoles.Jackal))) ||
                                    (target.Is(CustomRoles.Workaholic) && Workaholic.WorkaholicVisibleToEveryone.GetBool()) ||
                                    (target.Is(CustomRoles.Doctor) && !target.HasEvilAddon() && Options.DoctorVisibleToEveryone.GetBool()) ||
                                    (target.Is(CustomRoles.Mayor) && Mayor.MayorRevealWhenDoneTasks.GetBool() && target.GetTaskState().IsTaskFinished) ||
                                    (Marshall.CanSeeMarshall(seer) && target.Is(CustomRoles.Marshall) && target.GetTaskState().IsTaskFinished) ||
                                    (Main.PlayerStates[target.PlayerId].deathReason == PlayerState.DeathReason.Vote && Options.SeeEjectedRolesInMeeting.GetBool()) ||
                                    (CustomTeamManager.AreInSameCustomTeam(seer.PlayerId, target.PlayerId) && CustomTeamManager.IsSettingEnabledForPlayerTeam(seer.PlayerId, CTAOption.KnowRoles)) ||
                                    Main.PlayerStates.Values.Any(x => x.Role.KnowRole(seer, target)) ||
                                    Markseeker.PlayerIdList.Any(x => Main.PlayerStates[x].Role is Markseeker { IsEnable: true, TargetRevealed: true } ms && ms.MarkedId == target.PlayerId) ||
                                    Options.CurrentGameMode is CustomGameMode.FFA or CustomGameMode.MoveAndStop or CustomGameMode.HotPotato or CustomGameMode.Speedrun ||
                                    (CustomGameMode.HideAndSeek.IsActiveOrIntegrated() && HnSManager.IsRoleTextEnabled(seer, target)) ||
                                    (seer.IsRevealedPlayer(target) && !target.Is(CustomRoles.Trickster)) ||
                                    (seer.Is(CustomRoles.God) && God.KnowInfo.GetValue() == 2) ||
                                    target.Is(CustomRoles.GM)
                                        ? $"<size={fontSize}>{target.GetDisplayRoleName(seeTargetBetrayalAddons: shouldSeeTargetAddons)}{GetProgressText(target)}</size>\r\n"
                                        : string.Empty;

                                if (CustomRoles.Altruist.RoleExist() && Main.DiedThisRound.Contains(seer.PlayerId)) TargetRoleText = string.Empty;

                                if (Options.CurrentGameMode is CustomGameMode.CaptureTheFlag or CustomGameMode.NaturalDisasters or CustomGameMode.RoomRush) TargetRoleText = string.Empty;

                                if (!GameStates.IsLobby)
                                {
                                    if (!seer.Data.IsDead && seer.IsRevealedPlayer(target) && target.Is(CustomRoles.Trickster))
                                    {
                                        TargetRoleText = Farseer.RandomRole[seer.PlayerId];
                                        TargetRoleText += Farseer.GetTaskState();
                                    }

                                    if (CustomGameMode.SoloKombat.IsActiveOrIntegrated()) TargetRoleText = $"<size={fontSize}>{GetProgressText(target)}</size>\r\n";
                                }
                                else
                                    TargetRoleText = string.Empty;

                                string TargetPlayerName = target.GetRealName(isForMeeting);

                                if (GameStates.IsLobby) goto End;

                                if (!CustomGameMode.Standard.IsActiveOrIntegrated()) goto BeforeEnd;

                                if (GuesserIsForMeeting || isForMeeting || (seerRole == CustomRoles.Mafia && !seer.IsAlive() && Options.MafiaCanKillNum.GetInt() >= 1)) TargetPlayerName = $"{ColorString(GetRoleColor(seerRole), target.PlayerId.ToString())} {TargetPlayerName}";

                                switch (seerRole)
                                {
                                    case CustomRoles.EvilTracker:
                                        TargetMark.Append(EvilTracker.GetTargetMark(seer, target));
                                        if (isForMeeting && EvilTracker.IsTrackTarget(seer, target) && EvilTracker.CanSeeLastRoomInMeeting) TargetRoleText = $"<size={fontSize}>{EvilTracker.GetArrowAndLastRoom(seer, target)}</size>\r\n";

                                        break;
                                    case CustomRoles.Scout:
                                        TargetMark.Append(Scout.GetTargetMark(seer, target));
                                        if (isForMeeting && Scout.IsTrackTarget(seer, target) && Scout.CanSeeLastRoomInMeeting) TargetRoleText = $"<size={fontSize}>{Scout.GetArrowAndLastRoom(seer, target)}</size>\r\n";

                                        break;
                                    case CustomRoles.Psychic when seer.IsAlive() && Psychic.IsRedForPsy(target, seer) && isForMeeting:
                                        TargetPlayerName = ColorString(GetRoleColor(CustomRoles.Impostor), TargetPlayerName);
                                        break;
                                    case CustomRoles.HeadHunter when (Main.PlayerStates[seer.PlayerId].Role as HeadHunter).Targets.Contains(target.PlayerId) && seer.IsAlive():
                                    case CustomRoles.BountyHunter when (Main.PlayerStates[seer.PlayerId].Role as BountyHunter).GetTarget(seer) == target.PlayerId && seer.IsAlive():
                                        TargetPlayerName = $"<color=#000000>{TargetPlayerName}</size>";
                                        break;
                                    case CustomRoles.Lookout when seer.IsAlive() && target.IsAlive() && !isForMeeting:
                                        TargetPlayerName = $"{ColorString(GetRoleColor(CustomRoles.Lookout), $" {target.PlayerId}")} {TargetPlayerName}";
                                        break;
                                }

                                BeforeEnd:

                                TargetPlayerName = TargetPlayerName.ApplyNameColorData(seer, target, isForMeeting);

                                if (!CustomGameMode.Standard.IsActiveOrIntegrated()) goto End;

                                if (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Snitch) && target.Is(CustomRoles.Madmate) && target.GetTaskState().IsTaskFinished)
                                    TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Impostor), "★"));

                                if (Marshall.CanSeeMarshall(seer) && target.Is(CustomRoles.Marshall) && target.GetTaskState().IsTaskFinished)
                                    TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Marshall), "★"));

                                TargetMark.Append(Executioner.TargetMark(seer, target));
                                TargetMark.Append(Gamer.TargetMark(seer, target));
                                TargetMark.Append(Medic.GetMark(seer, target));
                                TargetMark.Append(Gaslighter.GetMark(seer, target, isForMeeting));
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
                                        case CustomGameMode.CaptureTheFlag:
                                            TargetSuffix.Append(CTFManager.GetSuffixText(seer, target));
                                            break;
                                    }

                                    Main.PlayerStates.Values.Do(x => TargetSuffix.Append(x.Role.GetSuffix(seer, target, meeting: isForMeeting)));

                                    if (MeetingStates.FirstMeeting && Main.ShieldPlayer == target.FriendCode && !string.IsNullOrEmpty(target.FriendCode) && Options.CurrentGameMode is CustomGameMode.Standard or CustomGameMode.FFA or CustomGameMode.Speedrun) TargetSuffix.Append(GetString("DiedR1Warning"));

                                    TargetSuffix.Append(AFKDetector.GetSuffix(seer, target));
                                }

                                var TargetDeathReason = string.Empty;
                                if (seer.KnowDeathReason(target) && !GameStates.IsLobby) TargetDeathReason = $"\n<size=1.7>({ColorString(GetRoleColor(CustomRoles.Doctor), GetVitalText(target.PlayerId))})</size>";

                                // Devourer
                                if (Devourer.HideNameOfConsumedPlayer.GetBool() && !GameStates.IsLobby && Devourer.PlayerIdList.Any(x => Main.PlayerStates[x].Role is Devourer { IsEnable: true } dv && dv.PlayerSkinsCosumed.Contains(seer.PlayerId)) && !CamouflageIsForMeeting) TargetPlayerName = GetString("DevouredName");

                                // Camouflage
                                if (Camouflage.IsCamouflage && !CamouflageIsForMeeting) TargetPlayerName = $"<size=0>{TargetPlayerName}</size>";

                                var TargetName = $"{TargetRoleText}{TargetPlayerName}{TargetDeathReason}{TargetMark}";
                                TargetName += GameStates.IsLobby || TargetSuffix.ToString() == string.Empty ? string.Empty : $"\r\n{TargetSuffix}";

                                target.RpcSetNamePrivate(TargetName, seer, NoCache);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (LastNotifyRolesErrorTS != now)
                    {
                        Logger.Error($"Error for {seer.GetNameWithRole()}:", "NR");
                        ThrowException(ex);
                        LastNotifyRolesErrorTS = now;
                    }
                    else
                        Logger.Error($"Error for {seer.GetNameWithRole()}: {ex}", "NR");
                }
            }

            if (!CustomGameMode.Standard.IsActiveOrIntegrated()) return;

            string seers = seerList.Length == apc.Length ? "Everyone" : string.Join(", ", seerList.Select(x => x.GetRealName()));
            string targets = targetList.Length == apc.Length ? "Everyone" : string.Join(", ", targetList.Select(x => x.GetRealName()));
            if (seers.Length == 0) seers = "\u2205";

            if (targets.Length == 0) targets = "\u2205";

            Logger.Info($" Seers: {seers} ---- Targets: {targets}", "NR");
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
            Camouflage.SetPetForOutfitIfNecessary(newOutfit);

            var sender = CustomRpcSender.Create($"Utils.RpcChangeSkin({pc.Data.PlayerName})");

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
            pc.Data.DefaultOutfit.HatSequenceId += 10;

            sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetHatStr)
                .Write(newOutfit.HatId)
                .Write(pc.GetNextRpcSequenceId(RpcCalls.SetHatStr))
                .EndRpc();

            pc.SetSkin(newOutfit.SkinId, newOutfit.ColorId);
            pc.Data.DefaultOutfit.SkinSequenceId += 10;

            sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetSkinStr)
                .Write(newOutfit.SkinId)
                .Write(pc.GetNextRpcSequenceId(RpcCalls.SetSkinStr))
                .EndRpc();

            pc.SetVisor(newOutfit.VisorId, newOutfit.ColorId);
            pc.Data.DefaultOutfit.VisorSequenceId += 10;

            sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetVisorStr)
                .Write(newOutfit.VisorId)
                .Write(pc.GetNextRpcSequenceId(RpcCalls.SetVisorStr))
                .EndRpc();

            pc.SetPet(newOutfit.PetId);
            pc.Data.DefaultOutfit.PetSequenceId += 10;

            sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetPetStr)
                .Write(newOutfit.PetId)
                .Write(pc.GetNextRpcSequenceId(RpcCalls.SetPetStr))
                .EndRpc();

            pc.SetNamePlate(newOutfit.NamePlateId);
            pc.Data.DefaultOutfit.NamePlateSequenceId += 10;

            sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetNamePlateStr)
                .Write(newOutfit.NamePlateId)
                .Write(pc.GetNextRpcSequenceId(RpcCalls.SetNamePlateStr))
                .EndRpc();

            sender.SendMessage();
        }

        public static string GetGameStateData(bool clairvoyant = false)
        {
            Dictionary<Options.GameStateInfo, int> nums = Enum.GetValues<Options.GameStateInfo>().ToDictionary(x => x, _ => 0);

            if (CustomRoles.Romantic.RoleExist(true)) nums[Options.GameStateInfo.RomanticState] = 1;

            if (Romantic.HasPickedPartner) nums[Options.GameStateInfo.RomanticState] = 2;

            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                if (pc.IsMadmate())
                    nums[Options.GameStateInfo.MadmateCount]++;
                else if (pc.IsNeutralKiller())
                    nums[Options.GameStateInfo.NKCount]++;
                else if (pc.IsCrewmate())
                    nums[Options.GameStateInfo.CrewCount]++;
                else if (pc.Is(Team.Impostor))
                    nums[Options.GameStateInfo.ImpCount]++;
                else if (pc.Is(Team.Neutral)) nums[Options.GameStateInfo.NNKCount]++;

                if (pc.GetCustomSubRoles().Any(x => x.IsConverted())) nums[Options.GameStateInfo.ConvertedCount]++;

                if (Main.LoversPlayers.Exists(x => x.PlayerId == pc.PlayerId)) nums[Options.GameStateInfo.LoversState]++;

                if (pc.Is(CustomRoles.Romantic)) nums[Options.GameStateInfo.RomanticState] *= 3;

                if (Romantic.PartnerId == pc.PlayerId) nums[Options.GameStateInfo.RomanticState] *= 4;
            }

            // All possible results of RomanticState from the above code:
            // 0: Romantic doesn't exist
            // 1: Romantic exists but hasn't picked a partner (and is dead)
            // 2: Romantic exists and has picked a partner (but both of them are dead)
            // 3: Romantic exists, is alive, but hasn't picked a partner
            // 6: Romantic exists, has picked a partner who is dead, but Romantic is alive
            // 8: Romantic exists, has picked a partner who is alive, but Romantic is dead
            // 24: Romantic exists, has picked a partner who is alive, and Romantic is alive

            StringBuilder sb = new();
            Dictionary<Options.GameStateInfo, OptionItem> checkDict = clairvoyant ? Clairvoyant.Settings : Options.GameStateSettings;
            nums[Options.GameStateInfo.Tasks] = GameData.Instance.CompletedTasks;
            Dictionary<Options.GameStateInfo, object> states = nums.ToDictionary(x => x.Key, x => x.Key == Options.GameStateInfo.RomanticState ? GetString($"GSRomanticState.{x.Value}") : (object)x.Value);
            states.DoIf(x => checkDict[x.Key].GetBool(), x => sb.AppendLine(string.Format(GetString($"GSInfo.{x.Key}"), x.Value, GameData.Instance.TotalTasks)));
            return $"<#ffffff><size=90%>{sb.ToString().TrimEnd()}</size></color>";
        }

        public static void AddAbilityCD(CustomRoles role, byte playerId, bool includeDuration = true)
        {
            if (role.UsesPetInsteadOfKill())
            {
                var kcd = (int)Math.Round(Main.AllPlayerKillCooldown.TryGetValue(playerId, out float KCD) ? KCD : Options.DefaultKillCooldown);
                Main.AbilityCD[playerId] = (TimeStamp, kcd);
                SendRPC(CustomRPC.SyncAbilityCD, 1, playerId, kcd);
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
                CustomRoles.Whisperer => Whisperer.Cooldown.GetInt() + (includeDuration ? Whisperer.Duration.GetInt() : 0),
                CustomRoles.Perceiver => Perceiver.CD.GetInt(),
                CustomRoles.Convener => Convener.CD.GetInt(),
                CustomRoles.DovesOfNeace => Options.DovesOfNeaceCooldown.GetInt(),
                CustomRoles.Alchemist => Alchemist.VentCooldown.GetInt(),
                CustomRoles.NiceHacker => playerId.IsPlayerModClient() ? -1 : NiceHacker.AbilityCD.GetInt(),
                CustomRoles.CameraMan => CameraMan.VentCooldown.GetInt(),
                CustomRoles.Tornado => Tornado.TornadoCooldown.GetInt(),
                CustomRoles.Sentinel => Sentinel.PatrolCooldown.GetInt(),
                CustomRoles.Druid => Druid.VentCooldown.GetInt(),
                CustomRoles.Catcher => Catcher.AbilityCooldown.GetInt(),
                CustomRoles.Sentry => Crewmate.Sentry.ShowInfoCooldown.GetInt(),
                CustomRoles.ToiletMaster => ToiletMaster.AbilityCooldown.GetInt(),
                CustomRoles.Sniper => Options.DefaultShapeshiftCooldown.GetInt(),
                CustomRoles.Assassin => Assassin.AssassinateCooldownOpt.GetInt(),
                CustomRoles.Undertaker => Undertaker.UndertakerAssassinateCooldown.GetInt(),
                CustomRoles.Bomber => Bomber.BombCooldown.GetInt(),
                CustomRoles.Nuker => Bomber.NukeCooldown.GetInt(),
                CustomRoles.Sapper => Sapper.ShapeshiftCooldown.GetInt(),
                CustomRoles.Miner => Options.MinerSSCD.GetInt(),
                CustomRoles.Escapee => Options.EscapeeSSCD.GetInt(),
                CustomRoles.QuickShooter => QuickShooter.ShapeshiftCooldown.GetInt(),
                CustomRoles.Disperser => Disperser.DisperserShapeshiftCooldown.GetInt(),
                CustomRoles.Twister => Twister.ShapeshiftCooldown.GetInt(),
                CustomRoles.Abyssbringer => Abyssbringer.BlackHolePlaceCooldown.GetInt(),
                CustomRoles.Warlock => Warlock.IsCursed ? -1 : Warlock.ShapeshiftCooldown.GetInt(),
                CustomRoles.Swiftclaw => Swiftclaw.DashCD.GetInt() + (includeDuration ? Swiftclaw.DashDuration.GetInt() : 0),
                CustomRoles.Hypnotist => Hypnotist.AbilityCooldown.GetInt() + (includeDuration ? Hypnotist.AbilityDuration.GetInt() : 0),
                CustomRoles.Parasite => (int)Parasite.SSCD + (includeDuration ? (int)Parasite.SSDur : 0),
                CustomRoles.Tiger => Tiger.EnrageCooldown.GetInt() + (includeDuration ? Tiger.EnrageDuration.GetInt() : 0),
                CustomRoles.Nonplus => Nonplus.BlindCooldown.GetInt() + (includeDuration ? Nonplus.BlindDuration.GetInt() : 0),
                CustomRoles.Cherokious => Cherokious.KillCooldown.GetInt(),
                CustomRoles.Shifter => Shifter.KillCooldown.GetInt(),
                CustomRoles.NoteKiller => NoteKiller.AbilityCooldown.GetInt(),
                CustomRoles.Weatherman => Weatherman.AbilityCooldown.GetInt(),
                _ => -1
            };

            if (CD == -1) return;

            if (Main.PlayerStates[playerId].SubRoles.Contains(CustomRoles.Energetic)) CD = (int)Math.Round(CD * 0.75f);

            Main.AbilityCD[playerId] = (TimeStamp, CD);
            SendRPC(CustomRPC.SyncAbilityCD, 1, playerId, CD);

            if (Options.UseUnshiftTrigger.GetBool() && role.SimpleAbilityTrigger() && (!role.IsNeutral() || Options.UseUnshiftTriggerForNKs.GetBool()) && !role.AlwaysUsesUnshift()) GetPlayerById(playerId)?.RpcResetAbilityCooldown();
        }

        public static (RoleTypes RoleType, CustomRoles CustomRole) GetRoleMap(byte seerId, byte targetId = byte.MaxValue)
        {
            if (targetId == byte.MaxValue) targetId = seerId;

            return StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(seerId, targetId)];
        }

        public static void AfterMeetingTasks()
        {
            bool loversChat = Lovers.PrivateChat.GetBool() && Main.LoversPlayers.TrueForAll(x => x.IsAlive());

            if (loversChat && Lovers.PrivateChatForLoversOnly.GetBool())
                Main.LoversPlayers.ForEach(x => x.SetChatVisible());
            else if (!Lovers.IsChatActivated && loversChat && !GameStates.IsEnded && CustomGameMode.Standard.IsActiveOrIntegrated())
            {
                LateTask.New(SetChatVisibleForAll, 0.5f, log: false);
                Lovers.IsChatActivated = true;
                return;
            }

            if (loversChat) GameEndChecker.Prefix();

            Lovers.IsChatActivated = false;
            AFKDetector.NumAFK = 0;
            AFKDetector.PlayerData.Clear();

            Camouflage.CheckCamouflage();

            foreach (PlayerControl pc in Main.AllPlayerControls)
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

                    if (Options.UsePets.GetBool()) pc.AddAbilityCD(false);

                    pc.CheckAndSetUnshiftState(false);

                    AFKDetector.RecordPosition(pc);

                    Main.PlayerStates[pc.PlayerId].Role.AfterMeetingTasks();
                }
                else
                {
                    TaskState taskState = pc.GetTaskState();
                    if (pc.IsCrewmate() && !taskState.IsTaskFinished && taskState.HasTasks) pc.Notify(GetString("DoYourTasksPlease"), 10f);

                    GhostRolesManager.NotifyAboutGhostRole(pc);
                }

                if (pc.Is(CustomRoles.Specter) || pc.Is(CustomRoles.Haunter)) pc.RpcResetAbilityCooldown();

                Main.CheckShapeshift[pc.PlayerId] = false;
            }

            LateTask.New(() => Main.ProcessShapeshifts = true, 1f, log: false);

            CopyCat.ResetRoles();

            if (Options.DiseasedCDReset.GetBool())
            {
                Main.KilledDiseased.SetAllValues(0);
                Main.KilledDiseased.Keys.Select(x => x.GetPlayer()).Do(x => x?.ResetKillCooldown());
                Main.KilledDiseased.Clear();
            }

            if (Options.AntidoteCDReset.GetBool())
            {
                Main.KilledAntidote.SetAllValues(0);
                Main.KilledAntidote.Keys.Select(x => x.GetPlayer()).Do(x => x?.ResetKillCooldown());
                Main.KilledAntidote.Clear();
            }

            Damocles.AfterMeetingTasks();
            Stressed.AfterMeetingTasks();
            Circumvent.AfterMeetingTasks();
            Deadlined.AfterMeetingTasks();

            if (Options.AirshipVariableElectrical.GetBool()) AirshipElectricalDoors.Initialize();

            Main.DontCancelVoteList.Clear();

            DoorsReset.ResetDoors();
            RoleBlockManager.Reset();
            PhantomRolePatch.AfterMeeting();

            if ((MapNames)Main.NormalOptions.MapId == MapNames.Airship && AmongUsClient.Instance.AmHost && PlayerControl.LocalPlayer.Is(CustomRoles.GM)) LateTask.New(() => { PlayerControl.LocalPlayer.NetTransform.SnapTo(new(15.5f, 0.0f), (ushort)(PlayerControl.LocalPlayer.NetTransform.lastSequenceId + 8)); }, 11f, "GM Auto-TP Failsafe"); // TP to Main Hall

            LateTask.New(() => Asthmatic.RunChecks = true, 2f, log: false);
        }

        public static void AfterPlayerDeathTasks(PlayerControl target, bool onMeeting = false, bool disconnect = false)
        {
            try
            {
                if (!onMeeting) Main.DiedThisRound.Add(target.PlayerId);

                // Record the first death
                if (Main.FirstDied == string.Empty) Main.FirstDied = target.FriendCode;

                switch (target.GetCustomRole())
                {
                    case CustomRoles.Curser:
                        ((Curser)Main.PlayerStates[target.PlayerId].Role).OnDeath();
                        break;
                    case CustomRoles.Camouflager when Camouflager.IsActive:
                        Camouflager.IsDead();
                        break;
                    case CustomRoles.Terrorist when !disconnect:
                        Logger.Info(target?.Data?.PlayerName + " Terrorist died", "MurderPlayer");
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
                    case CustomRoles.PlagueDoctor when !disconnect:
                        PlagueDoctor.OnPDdeath(target.GetRealKiller(), target);
                        break;
                    case CustomRoles.CyberStar when !disconnect:
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
                            if (!Main.CyberStarDead.Contains(target.PlayerId)) Main.CyberStarDead.Add(target.PlayerId);
                        }

                        break;
                    case CustomRoles.Pelican:
                        Pelican.OnPelicanDied(target.PlayerId);
                        break;
                    case CustomRoles.Devourer:
                        Devourer.OnDevourerDied(target.PlayerId);
                        break;
                    case CustomRoles.Markseeker when !disconnect:
                        Markseeker.OnDeath(target);
                        break;
                    case CustomRoles.Medic:
                        Medic.IsDead(target);
                        break;
                }

                if (target == null) return;

                if (!disconnect) Randomizer.OnAnyoneDeath(target);
                if (Executioner.Target.ContainsValue(target.PlayerId)) Executioner.ChangeRoleByTarget(target);
                if (Lawyer.Target.ContainsValue(target.PlayerId)) Lawyer.ChangeRoleByTarget(target);
                if (!disconnect && target.Is(CustomRoles.Stained)) Stained.OnDeath(target, target.GetRealKiller());
                if (!disconnect && target.Is(CustomRoles.Spurt)) Spurt.DeathTask(target);

                Postman.CheckAndResetTargets(target, true);
                Hitman.CheckAndResetTargets();

                if (!disconnect && !onMeeting)
                {
                    Hacker.AddDeadBody(target);
                    Mortician.OnPlayerDead(target);
                    Bloodhound.OnPlayerDead(target);
                    Tracefinder.OnPlayerDead(target);
                    Vulture.OnPlayerDead(target);
                    Scout.OnPlayerDeath(target);
                    Amnesiac.OnAnyoneDeath(target);
                    Dad.OnAnyoneDeath(target);
                    Occultist.OnAnyoneDied(target);
                    Crewmate.Sentry.OnAnyoneMurder(target);
                    Soothsayer.OnAnyoneDeath(target.GetRealKiller(), target);

                    TargetDies(target.GetRealKiller(), target);
                }

                Adventurer.OnAnyoneDead(target);
                Whisperer.OnAnyoneDied(target);

                if (QuizMaster.On) QuizMaster.Data.NumPlayersDeadThisRound++;

                FixedUpdatePatch.LoversSuicide(target.PlayerId, guess: onMeeting);

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
                StringBuilder sb = new(100);

                if (CustomGameMode.Standard.IsActiveOrIntegrated())
                {
                    foreach (CountTypes countTypes in Enum.GetValues<CountTypes>())
                    {
                        int playersCount = PlayersCount(countTypes);
                        if (playersCount == 0) continue;

                        sb.Append($"{countTypes}: {AlivePlayersCount(countTypes)}/{playersCount}, ");
                    }
                }

                sb.Append($"All: {AllAlivePlayersCount}/{AllPlayersCount}");
                Logger.Info(sb.ToString(), "CountAlivePlayers");
            }

            if (AmongUsClient.Instance.AmHost && !Main.HasJustStarted) GameEndChecker.Prefix();
        }

        public static string GetVoteName(byte num)
        {
            PlayerControl player = GetPlayerById(num);

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
            var t = DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss");
            var f = $"{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)}/EHR_Logs/{t}";
            if (!Directory.Exists(f)) Directory.CreateDirectory(f);

            var filename = $"{f}EHR-v{Main.PluginVersion}-LOG";
            FileInfo[] files = [new($"{Environment.CurrentDirectory}/BepInEx/LogOutput.log"), new($"{Environment.CurrentDirectory}/BepInEx/log.html")];
            files.Do(x => x.CopyTo($"{filename}{x.Extension}"));

            if (!open) return;

            if (PlayerControl.LocalPlayer != null) HudManager.Instance?.Chat?.AddChat(PlayerControl.LocalPlayer, string.Format(GetString("Message.DumpfileSaved"), "EHR" + filename.Split("EHR")[1]));

            ProcessStartInfo psi = new("Explorer.exe")
                { Arguments = "/e,/select," + filename.Replace("/", "\\") };

            Process.Start(psi);
        }

        public static (int Doused, int All) GetDousedPlayerCount(byte playerId)
        {
            int doused = 0, all = 0;

            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                if (pc.PlayerId == playerId) continue;

                all++;

                if (Arsonist.IsDoused.TryGetValue((playerId, pc.PlayerId), out bool isDoused) && isDoused)
                    doused++;
            }

            return (doused, all);
        }

        public static (int Drawn, int All) GetDrawPlayerCount(byte playerId, out List<PlayerControl> winnerList)
        {
            var draw = 0;
            int all = Options.RevolutionistDrawCount.GetInt();
            int max = Main.AllAlivePlayerControls.Length;
            if (!Main.PlayerStates[playerId].IsDead) max--;

            winnerList = [];
            if (all > max) all = max;

            foreach (PlayerControl pc in Main.AllPlayerControls.Where(pc => Revolutionist.IsDraw.TryGetValue((playerId, pc.PlayerId), out bool isDraw) && isDraw).ToArray())
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
            string name = Main.AllPlayerNames[id].RemoveHtmlTags().Replace("\r\n", string.Empty);

            if (id == PlayerControl.LocalPlayer.PlayerId)
                name = DataManager.player.Customization.Name;
            else
                name = GetPlayerById(id)?.Data.PlayerName ?? name;

            TaskState taskState = Main.PlayerStates[id].TaskState;
            string TaskCount;

            if (taskState.HasTasks)
            {
                NetworkedPlayerInfo info = GetPlayerInfoById(id);
                Color TaskCompleteColor = HasTasks(info) ? Color.green : Color.cyan;
                Color NonCompleteColor = HasTasks(info) ? Color.yellow : Color.white;

                if (Workhorse.IsThisRole(id)) NonCompleteColor = Workhorse.RoleColor;

                Color NormalColor = taskState.IsTaskFinished ? TaskCompleteColor : NonCompleteColor;

                if (Main.PlayerStates.TryGetValue(id, out PlayerState ps))
                {
                    NormalColor = ps.MainRole switch
                    {
                        CustomRoles.Crewpostor => Color.red,
                        CustomRoles.Cherokious => GetRoleColor(CustomRoles.Cherokious),
                        _ => NormalColor
                    };
                }

                Color TextColor = NormalColor;
                var Completed = $"{taskState.CompletedTasksCount}";
                TaskCount = ColorString(TextColor, $" ({Completed}/{taskState.AllTasksCount})");
            }
            else
                TaskCount = string.Empty;

            var summary = $"{ColorString(Main.PlayerColors[id], name)} - {GetDisplayRoleName(id, true)}{TaskCount}{GetKillCountText(id)} ({GetVitalText(id, true)})";

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.SoloKombat:
                    summary = TranslationController.Instance.currentLanguage.languageID is SupportedLangs.SChinese or SupportedLangs.TChinese ? $"{GetProgressText(id)}\t<pos=22%>{ColorString(Main.PlayerColors[id], name)}</pos>" : $"{ColorString(Main.PlayerColors[id], name)}<pos=30%>{GetProgressText(id)}</pos>";
                    if (GetProgressText(id).Trim() == string.Empty) return string.Empty;

                    break;
                case CustomGameMode.FFA:
                    summary = $"{ColorString(Main.PlayerColors[id], name)} {GetKillCountText(id, true)}";
                    break;
                case CustomGameMode.Speedrun:
                case CustomGameMode.MoveAndStop:
                    summary = $"{ColorString(Main.PlayerColors[id], name)} -{TaskCount.Replace("(", string.Empty).Replace(")", string.Empty)}  ({GetVitalText(id, true)})";
                    break;
                case CustomGameMode.HotPotato:
                    int time = HotPotatoManager.GetSurvivalTime(id);
                    summary = $"{ColorString(Main.PlayerColors[id], name)} - <#e8cd46>{GetString("SurvivedTimePrefix")}: <#ffffff>{(time == 0 ? $"{GetString("SurvivedUntilTheEnd")}</color>" : $"{time}</color>s")}</color>  ({GetVitalText(id, true)})";
                    break;
                case CustomGameMode.NaturalDisasters:
                    int time2 = NaturalDisasters.SurvivalTime(id);
                    summary = $"{ColorString(Main.PlayerColors[id], name)} - <#e8cd46>{GetString("SurvivedTimePrefix")}: <#ffffff>{(time2 == 0 ? $"{GetString("SurvivedUntilTheEnd")}</color>" : $"{time2}</color>s")}</color>  ({GetVitalText(id, true)})";
                    break;
                case CustomGameMode.RoomRush:
                    int time3 = RoomRush.GetSurvivalTime(id);
                    summary = $"{ColorString(Main.PlayerColors[id], name)} - <#e8cd46>{GetString("SurvivedTimePrefix")}: <#ffffff>{(time3 == 0 ? $"{GetString("SurvivedUntilTheEnd")}</color>" : $"{time3}</color>s")}</color>  ({GetVitalText(id, true)})";
                    break;
                case CustomGameMode.CaptureTheFlag:
                    summary = $"{ColorString(Main.PlayerColors[id], name)}: {CTFManager.GetStatistics(id)}";
                    if (CTFManager.IsDeathPossible) summary += $"  ({GetVitalText(id, true)})";
                    break;
                case CustomGameMode.AllInOne:
                    string survivalTimeText = !Main.PlayerStates[id].IsDead ? string.Empty : $" ({GetString("SurvivedTimePrefix")}: <#f542ad>{RoomRush.GetSurvivalTime(id)}s</color>)";
                    summary = $"{ColorString(Main.PlayerColors[id], name)} -{TaskCount}{GetKillCountText(id, true)} ({GetVitalText(id, true)}){survivalTimeText}";
                    break;
            }

            return check && GetDisplayRoleName(id, true).RemoveHtmlTags().Contains("INVALID:NotAssigned")
                ? "INVALID"
                : disableColor
                    ? summary.RemoveHtmlTags()
                    : summary;
        }

        public static string GetRemainingKillers(bool notify = false, bool president = false)
        {
            var impnum = 0;
            var neutralnum = 0;
            bool impShow = president || Options.ShowImpRemainOnEject.GetBool();
            bool nkShow = president || Options.ShowNKRemainOnEject.GetBool();

            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                if (impShow && pc.Is(Team.Impostor))
                    impnum++;
                else if (nkShow && pc.IsNeutralKiller()) neutralnum++;
            }

            StringBuilder sb = new();

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

        public static string RemoveHtmlTagsTemplate(this string str)
        {
            return Regex.Replace(str, string.Empty, string.Empty);
        }

        public static string RemoveHtmlTags(this string str)
        {
            return Regex.Replace(str, "<[^>]*?>", string.Empty);
        }

        public static bool CanMafiaKill()
        {
            if (Main.PlayerStates == null) return false;

            return !Main.AllAlivePlayerControls.Select(pc => pc.GetCustomRole()).Any(role => role != CustomRoles.Mafia && role.IsImpostor());
        }

        public static void FlashColor(Color color, float duration = 1f)
        {
            HudManager hud = DestroyableSingleton<HudManager>.Instance;
            if (hud.FullScreen == null) return;

            GameObject obj = hud.transform.FindChild("FlashColor_FullScreen")?.gameObject;

            if (obj == null)
            {
                obj = Object.Instantiate(hud.FullScreen.gameObject, hud.transform);
                obj.name = "FlashColor_FullScreen";
            }

            hud.StartCoroutine(Effects.Lerp(duration, new Action<float>(t =>
            {
                obj.SetActive(Math.Abs(t - 1f) > 0.1f);
                obj.GetComponent<SpriteRenderer>().color = new(color.r, color.g, color.b, Mathf.Clamp01(((-2f * Mathf.Abs(t - 0.5f)) + 1) * color.a / 2));
            })));
        }

        public static Sprite LoadSprite(string path, float pixelsPerUnit = 1f)
        {
            try
            {
                if (CachedSprites.TryGetValue(path + pixelsPerUnit, out Sprite sprite)) return sprite;

                Texture2D texture = LoadTextureFromResources(path);
                sprite = Sprite.Create(texture, new(0, 0, texture.width, texture.height), new(0.5f, 0.5f), pixelsPerUnit);
                sprite.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
                return CachedSprites[path + pixelsPerUnit] = sprite;
            }
            catch
            {
                Logger.Error($"Error loading texture from: {path}", "LoadImage");
            }

            return null;
        }

        public static Texture2D LoadTextureFromResources(string path)
        {
            try
            {
                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
                Texture2D texture = new(1, 1, TextureFormat.ARGB32, false);
                using MemoryStream ms = new();
                stream?.CopyTo(ms);
                texture.LoadImage(ms.ToArray(), false);
                return texture;
            }
            catch
            {
                Logger.Error($"Error loading texture: {path}", "LoadImage");
            }

            return null;
        }

        public static string ColorString(Color32 color, string str)
        {
            return $"<#{color.r:x2}{color.g:x2}{color.b:x2}{color.a:x2}>{str}</color>";
        }

        /// <summary>
        ///     Darkness:Mix black and original color in a ratio of 1. If it is negative, it will be mixed with white.
        /// </summary>
        public static Color ShadeColor(this Color color, float Darkness = 0)
        {
            bool IsDarker = Darkness >= 0;
            if (!IsDarker) Darkness = -Darkness;

            float Weight = IsDarker ? 0 : Darkness;
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
                name = "Local Game";
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

            string Ip = region.Servers.FirstOrDefault()?.Ip ?? string.Empty;

            if (Ip.Contains("aumods.us", StringComparison.Ordinal) || Ip.Contains("duikbo.at", StringComparison.Ordinal))
            {
                // Official Modded Server
                if (Ip.Contains("au-eu"))
                    name = "MEU";
                else if (Ip.Contains("au-as"))
                    name = "MAS";
                else if (Ip.Contains("www.")) name = "MNA";

                return name;
            }

            if (name.Contains("nikocat233", StringComparison.OrdinalIgnoreCase)) name = name.Replace("nikocat233", "Niko233", StringComparison.OrdinalIgnoreCase);

            return name;
        }

        private static int PlayersCount(CountTypes countTypes)
        {
            var count = 0;

            foreach (PlayerState state in Main.PlayerStates.Values)
                if (state.countTypes == countTypes)
                    count++;

            return count;
        }

        public static int AlivePlayersCount(CountTypes countTypes)
        {
            return Main.AllAlivePlayerControls.Count(pc => pc.Is(countTypes));
        }

        public static bool IsPlayerModClient(this byte id)
        {
            return Main.PlayerVersion.ContainsKey(id);
        }

        public static float CalculatePingDelay()
        {
            // The value of AmongUsClient.Instance.Ping is in milliseconds (ms), so ÷1000 to convert to seconds
            float divice = !CustomGameMode.Standard.IsActiveOrIntegrated() ? 3000f : 2000f;
            float minTime = Mathf.Max(0.02f, AmongUsClient.Instance.Ping / divice * 6f);
            return minTime;
        }
    }
}