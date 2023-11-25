using AmongUs.Data;
using AmongUs.GameOptions;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes;
using InnerNet;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TOHE.Modules;
using TOHE.Roles.AddOns.Crewmate;
using TOHE.Roles.AddOns.Impostor;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE;

public static class Utils
{
    private static readonly DateTime timeStampStartTime = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static long GetTimeStamp(DateTime? dateTime = null) => (long)((dateTime ?? DateTime.Now).ToUniversalTime() - timeStampStartTime).TotalSeconds;
    public static void ErrorEnd(string text)
    {
        if (AmongUsClient.Instance.AmHost)
        {
            Logger.Fatal($"{text} 错误，触发防黑屏措施", "Anti-black");
            ChatUpdatePatch.DoBlockChat = true;
            Main.OverrideWelcomeMsg = GetString("AntiBlackOutNotifyInLobby");
            _ = new LateTask(() =>
            {
                Logger.SendInGame(GetString("AntiBlackOutLoggerSendInGame"), true);
            }, 3f, "Anti-Black Msg SendInGame");
            _ = new LateTask(() =>
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Error);
                GameManager.Instance.LogicFlow.CheckEndCriteria();
                RPC.ForceEndGame(CustomWinner.Error);
            }, 5.5f, "Anti-Black End Game");
        }
        else
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpc(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.AntiBlackout, SendOption.Reliable);
            writer.Write(text);
            writer.EndMessage();
            if (Options.EndWhenPlayerBug.GetBool())
            {
                _ = new LateTask(() =>
                {
                    Logger.SendInGame(GetString("AntiBlackOutRequestHostToForceEnd"), true);
                }, 3f, "Anti-Black Msg SendInGame");
            }
            else
            {
                _ = new LateTask(() =>
                {
                    Logger.SendInGame(GetString("AntiBlackOutHostRejectForceEnd"), true);
                }, 3f, "Anti-Black Msg SendInGame");
                _ = new LateTask(() =>
                {
                    AmongUsClient.Instance.ExitGame(DisconnectReasons.Custom);
                    Logger.Fatal($"{text} 错误，已断开游戏", "Anti-black");
                }, 8f, "Anti-Black Exit Game");
            }
        }
    }
    public static void TPAll(Vector2 location)
    {
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            TP(pc.NetTransform, location);
        }
    }

    public static void TP(CustomNetworkTransform nt, Vector2 location)
    {
        var pc = nt.myPlayer;
        if (pc.inVent || pc.inMovingPlat || !pc.IsAlive())
        {
            Logger.Warn($"Target ({pc.GetNameWithRole().RemoveHtmlTags()}) is in an un-teleportable state - Teleporting canceled", "TP");
            return;
        }

        // Modded
        if (AmongUsClient.Instance.AmHost) nt.SnapTo(location, (ushort)(nt.lastSequenceId + 8));

        // Vanilla
        MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(nt.NetId, (byte)RpcCalls.SnapTo, SendOption.Reliable);
        NetHelpers.WriteVector2(location, messageWriter);
        messageWriter.Write(nt.lastSequenceId + 100U);
        AmongUsClient.Instance.FinishRpcImmediately(messageWriter);

        Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} => {location}", "TP");
    }
    public static void TP(this PlayerControl pc, Vector2 location)
    {
        TP(pc.NetTransform, location);
    }
    public static void TPtoRndVent(this PlayerControl pc)
    {
        TPtoRndVent(pc.NetTransform);
    }
    public static void TPtoRndVent(CustomNetworkTransform nt)
    {
        var vents = UnityEngine.Object.FindObjectsOfType<Vent>();
        var vent = vents[IRandom.Instance.Next(0, vents.Count)];

        Logger.Info($"{nt.myPlayer.GetNameWithRole().RemoveHtmlTags()} => {vent.transform.position} (vent)", "TP");

        TP(nt, new Vector2(vent.transform.position.x, vent.transform.position.y + 0.3636f));
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
        //Logger.Info($"SystemTypes:{type}", "IsActive");
        int mapId = Main.NormalOptions.MapId;
        switch (type)
        {
            case SystemTypes.Electrical:
                {
                    if (mapId == 5) return false; // if The Fungle return false
                    var SwitchSystem = ShipStatus.Instance.Systems[type].Cast<SwitchSystem>();
                    return SwitchSystem != null && SwitchSystem.IsActive;
                }
            case SystemTypes.Reactor:
                {
                    if (mapId == 2) return false; // if Polus return false
                    else if (mapId is 4) // Only Airhip
                    {
                        var HeliSabotageSystem = ShipStatus.Instance.Systems[type].Cast<HeliSabotageSystem>();
                        return HeliSabotageSystem != null && HeliSabotageSystem.IsActive;
                    }
                    else
                    {
                        var ReactorSystemType = ShipStatus.Instance.Systems[type].Cast<ReactorSystemType>();
                        return ReactorSystemType != null && ReactorSystemType.IsActive;
                    }
                }
            case SystemTypes.Laboratory:
                {
                    if (mapId != 2) return false; // Only Polus
                    var ReactorSystemType = ShipStatus.Instance.Systems[type].Cast<ReactorSystemType>();
                    return ReactorSystemType != null && ReactorSystemType.IsActive;
                }
            case SystemTypes.LifeSupp:
                {
                    if (mapId is 2 or 4 or 5) return false; // Only Skeld & Mira HQ
                    var LifeSuppSystemType = ShipStatus.Instance.Systems[type].Cast<LifeSuppSystemType>();
                    return LifeSuppSystemType != null && LifeSuppSystemType.IsActive;
                }
            case SystemTypes.Comms:
                {
                    if (mapId is 1 or 5) // Only Mira HQ & The Fungle
                    {
                        var HqHudSystemType = ShipStatus.Instance.Systems[type].Cast<HqHudSystemType>();
                        return HqHudSystemType != null && HqHudSystemType.IsActive;
                    }
                    else
                    {
                        var HudOverrideSystemType = ShipStatus.Instance.Systems[type].Cast<HudOverrideSystemType>();
                        return HudOverrideSystemType != null && HudOverrideSystemType.IsActive;
                    }
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
            opt.SetFloat(
                FloatOptionNames.CrewLightMod,
                opt.GetFloat(FloatOptionNames.ImpostorLightMod));
            if (IsActive(SystemTypes.Electrical))
            {
                opt.SetFloat(
                FloatOptionNames.CrewLightMod,
                opt.GetFloat(FloatOptionNames.CrewLightMod) * 5);
            }
            return;
        }
        else
        {
            opt.SetFloat(
                FloatOptionNames.ImpostorLightMod,
                opt.GetFloat(FloatOptionNames.CrewLightMod));
            if (IsActive(SystemTypes.Electrical))
            {
                opt.SetFloat(
                FloatOptionNames.ImpostorLightMod,
                opt.GetFloat(FloatOptionNames.ImpostorLightMod) / 5);
            }
            return;
        }
    }
    public static void SetVisionV2(this IGameOptions opt)
    {
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, opt.GetFloat(FloatOptionNames.CrewLightMod));
        if (IsActive(SystemTypes.Electrical))
        {
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, opt.GetFloat(FloatOptionNames.ImpostorLightMod) / 5);
        }
        return;
    }
    //誰かが死亡したときのメソッド
    public static void TargetDies(PlayerControl killer, PlayerControl target)
    {
        if (!target.Data.IsDead || GameStates.IsMeeting) return;
        foreach (PlayerControl seer in Main.AllPlayerControls)
        {
            if (KillFlashCheck(killer, target, seer))
            {
                seer.KillFlash();
                continue;
            }
            else if (target.Is(CustomRoles.CyberStar))
            {
                if (!Options.ImpKnowCyberStarDead.GetBool() && seer.GetCustomRole().IsImpostor()) continue;
                if (!Options.NeutralKnowCyberStarDead.GetBool() && seer.GetCustomRole().IsNeutral()) continue;
                seer.KillFlash();
                seer.Notify(ColorString(GetRoleColor(CustomRoles.CyberStar), GetString("OnCyberStarDead")));
            }
            else if (target.Is(CustomRoles.Demolitionist))
            {
                killer.Notify(ColorString(GetRoleColor(CustomRoles.Demolitionist), GetString("OnDemolitionistDead")));
                _ = new LateTask(() =>
                {
                    if (!killer.inVent && (killer.PlayerId != target.PlayerId))
                    {
                        if ((Options.DemolitionistKillerDiesOnMeetingCall.GetBool() || GameStates.IsInTask) && killer.IsAlive())
                        {
                            killer.SetRealKiller(target);
                            killer.Kill(killer);
                            RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                            Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Demolished;
                        }
                    }
                    else
                    {
                        if (killer.IsModClient()) RPC.PlaySoundRPC(killer.PlayerId, Sounds.TaskComplete);
                        else killer.RpcGuardAndKill(killer);
                        killer.SetKillCooldown(Main.AllPlayerKillCooldown[killer.PlayerId] - Options.DemolitionistVentTime.GetFloat());
                    }
                }, Options.DemolitionistVentTime.GetFloat() + 0.5f
                );
            }
        }
        if (target.Is(CustomRoles.CyberStar) && !Main.CyberStarDead.Contains(target.PlayerId)) Main.CyberStarDead.Add(target.PlayerId);
        if (target.Is(CustomRoles.Demolitionist) && !Main.DemolitionistDead.Contains(target.PlayerId)) Main.DemolitionistDead.Add(target.PlayerId);
    }
    public static bool KillFlashCheck(PlayerControl killer, PlayerControl target, PlayerControl seer)
    {
        if (seer.Is(CustomRoles.GM) || seer.Is(CustomRoles.Seer)) return true;
        if (seer.Data.IsDead || killer == seer || target == seer) return false;
        if (seer.Is(CustomRoles.EvilTracker)) return EvilTracker.KillFlashCheck(killer, target);
        return false;
    }
    public static void KillFlash(this PlayerControl player)
    {
        //Kill flash (blackout + reactor flash) processing
        bool ReactorCheck = false; //Checking whether the reactor sabotage is active

        var systemtypes = (MapNames)Main.NormalOptions.MapId switch
        {
            MapNames.Polus => SystemTypes.Laboratory,
            MapNames.Airship => SystemTypes.HeliSabotage,
            _ => SystemTypes.Reactor,
        };
        ReactorCheck = IsActive(systemtypes);

        var Duration = Options.KillFlashDuration.GetFloat();
        if (ReactorCheck) Duration += 0.2f; // Extend blackout during reactor

        //実行
        Main.PlayerStates[player.PlayerId].IsBlackOut = true; // Blackout
        if (player.AmOwner)
        {
            FlashColor(new(1f, 0f, 0f, 0.3f));
            if (Constants.ShouldPlaySfx()) RPC.PlaySound(player.PlayerId, Sounds.KillSound);
        }
        else if (player.IsModClient())
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.KillFlash, SendOption.Reliable, player.GetClientId());
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        else if (!ReactorCheck) player.ReactorFlash(0f); // reactor flash
        player.MarkDirtySettings();
        _ = new LateTask(() =>
        {
            Main.PlayerStates[player.PlayerId].IsBlackOut = false; // Cancel blackout
            player.MarkDirtySettings();
        }, Options.KillFlashDuration.GetFloat(), "RemoveKillFlash");
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
        return;
    }
    public static string GetDisplayRoleName(byte playerId, bool pure = false)
    {
        var TextData = GetRoleText(playerId, playerId, pure);
        return ColorString(TextData.Item2, TextData.Item1);
    }
    public static string GetRoleName(CustomRoles role, bool forUser = true)
    {
        return GetRoleString(Enum.GetName(typeof(CustomRoles), role), forUser);
    }
    public static string GetRoleMode(CustomRoles role, bool parentheses = true)
    {
        if (Options.HideGameSettings.GetBool() && Main.AllPlayerControls.Length > 1)
            return string.Empty;
        string mode = role.GetMode() switch
        {
            0 => GetString("RoleOffNoColor"),
            1 => GetString("RoleRateNoColor"),
            _ => GetString("RoleOnNoColor")
        };
        return parentheses ? $"({mode})" : mode;
    }
    public static string GetDeathReason(PlayerState.DeathReason status)
    {
        return GetString("DeathReason." + Enum.GetName(typeof(PlayerState.DeathReason), status));
    }
    public static Color GetRoleColor(CustomRoles role)
    {
        if (!Main.roleColors.TryGetValue(role, out var hexColor)) hexColor = "#ffffff";
        _ = ColorUtility.TryParseHtmlString(hexColor, out Color c);
        return c;
    }
    public static string GetRoleColorCode(CustomRoles role)
    {
        if (!Main.roleColors.TryGetValue(role, out var hexColor)) hexColor = "#ffffff";
        return hexColor;
    }
    public static (string, Color) GetRoleText(byte seerId, byte targetId, bool pure = false)
    {
        string RoleText = "Invalid Role";
        Color RoleColor;

        var seerMainRole = Main.PlayerStates[seerId].MainRole;
        var seerSubRoles = Main.PlayerStates[seerId].SubRoles;

        var targetMainRole = Main.PlayerStates[targetId].MainRole;
        var targetSubRoles = Main.PlayerStates[targetId].SubRoles;

        var self = seerId == targetId || Main.PlayerStates[seerId].IsDead;

        RoleText = GetRoleName(targetMainRole);
        RoleColor = GetRoleColor(targetMainRole);

        if (LastImpostor.currentId == targetId)
            RoleText = GetRoleString("Last-") + RoleText;

        if (Options.NameDisplayAddons.GetBool() && !pure && self)
        {
            if (Options.AddBracketsToAddons.GetBool())
            {
                if (Options.ImpEgoistVisibalToAllies.GetBool())
                {
                    foreach (var subRole in targetSubRoles.Where(x => x is not CustomRoles.LastImpostor and not CustomRoles.Madmate and not CustomRoles.Charmed and not CustomRoles.Recruit and not CustomRoles.Admired and not CustomRoles.Soulless and not CustomRoles.Lovers and not CustomRoles.Infected and not CustomRoles.Contagious))
                        RoleText = ColorString(GetRoleColor(subRole), GetString("PrefixB." + subRole.ToString())) + RoleText;
                }
                if (!Options.ImpEgoistVisibalToAllies.GetBool())
                {
                    foreach (var subRole in targetSubRoles.Where(x => x is not CustomRoles.LastImpostor and not CustomRoles.Madmate and not CustomRoles.Charmed and not CustomRoles.Recruit and not CustomRoles.Admired and not CustomRoles.Soulless and not CustomRoles.Lovers and not CustomRoles.Infected and not CustomRoles.Contagious))
                        RoleText = ColorString(GetRoleColor(subRole), GetString("PrefixB." + subRole.ToString())) + RoleText;
                }
            }
            else if (!Options.AddBracketsToAddons.GetBool())
            {
                if (Options.ImpEgoistVisibalToAllies.GetBool())
                {
                    foreach (var subRole in targetSubRoles.Where(x => x is not CustomRoles.LastImpostor and not CustomRoles.Madmate and not CustomRoles.Charmed and not CustomRoles.Recruit and not CustomRoles.Admired and not CustomRoles.Soulless and not CustomRoles.Lovers and not CustomRoles.Infected and not CustomRoles.Contagious))
                        RoleText = ColorString(GetRoleColor(subRole), GetString("Prefix." + subRole.ToString())) + RoleText;
                }
                if (!Options.ImpEgoistVisibalToAllies.GetBool())
                {
                    foreach (var subRole in targetSubRoles.Where(x => x is not CustomRoles.LastImpostor and not CustomRoles.Madmate and not CustomRoles.Charmed and not CustomRoles.Recruit and not CustomRoles.Admired and not CustomRoles.Soulless and not CustomRoles.Lovers and not CustomRoles.Infected and not CustomRoles.Contagious))
                        RoleText = ColorString(GetRoleColor(subRole), GetString("Prefix." + subRole.ToString())) + RoleText;
                }
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
        if (targetSubRoles.Contains(CustomRoles.Soulless))
        {
            RoleColor = GetRoleColor(CustomRoles.Soulless);
            RoleText = GetRoleString("Soulless-") + RoleText;
        }
        if (targetSubRoles.Contains(CustomRoles.Infected) && (self || pure || seerMainRole == CustomRoles.Infectious || (Infectious.TargetKnowOtherTarget.GetBool() && seerSubRoles.Contains(CustomRoles.Infected))))
        {
            RoleColor = GetRoleColor(CustomRoles.Infected);
            RoleText = GetRoleString("Infected-") + RoleText;
        }
        if (targetSubRoles.Contains(CustomRoles.Contagious) && (self || pure || seerMainRole == CustomRoles.Virus || (Virus.TargetKnowOtherTarget.GetBool() && seerSubRoles.Contains(CustomRoles.Contagious))))
        {
            RoleColor = GetRoleColor(CustomRoles.Contagious);
            RoleText = GetRoleString("Contagious-") + RoleText;
        }
        if (targetSubRoles.Contains(CustomRoles.Admired))
        {
            RoleColor = GetRoleColor(CustomRoles.Admired);
            RoleText = GetRoleString("Admired-") + RoleText;
        }

        return (RoleText, RoleColor);
    }
    public static string GetKillCountText(byte playerId, bool ffa = false)
    {
        if (!Main.PlayerStates.Any(x => x.Value.GetRealKiller() == playerId) && !ffa) return string.Empty;
        return ' ' + ColorString(new Color32(255, 69, 0, byte.MaxValue), string.Format(GetString("KillCount"), Main.PlayerStates.Count(x => x.Value.GetRealKiller() == playerId)));
    }
    public static string GetVitalText(byte playerId, bool RealKillerColor = false)
    {
        var state = Main.PlayerStates[playerId];
        string deathReason = state.IsDead ? GetString("DeathReason." + state.deathReason) : GetString("Alive");
        if (RealKillerColor)
        {
            var KillerId = state.GetRealKiller();
            Color color = KillerId != byte.MaxValue ? Main.PlayerColors[KillerId] : GetRoleColor(CustomRoles.Doctor);
            if (state.deathReason == PlayerState.DeathReason.Disconnected) color = new Color(255, 255, 255, 50);
            deathReason = ColorString(color, deathReason);
        }
        return deathReason;
    }

    public static bool HasTasks(GameData.PlayerInfo p, bool ForRecompute = true)
    {
        if (GameStates.IsLobby) return false;
        //Tasksがnullの場合があるのでその場合タスク無しとする
        if (p.Tasks == null) return false;
        if (p.Role == null) return false;

        var hasTasks = true;
        var States = Main.PlayerStates[p.PlayerId];
        if (p.Disconnected) return false;
        if (p.Role.IsImpostor)
            hasTasks = false; //タスクはCustomRoleを元に判定する
        if (Options.CurrentGameMode == CustomGameMode.SoloKombat || Options.CurrentGameMode == CustomGameMode.FFA) return false;
        //if (p.IsDead && Options.GhostIgnoreTasks.GetBool()) hasTasks = false;
        var role = States.MainRole;
        switch (role)
        {
            case CustomRoles.GM:
            case CustomRoles.Sheriff:
            case CustomRoles.Arsonist:
            case CustomRoles.Jackal:
            case CustomRoles.Sidekick:
            case CustomRoles.Poisoner:
            case CustomRoles.Eclipse:
            case CustomRoles.Pyromaniac:
            case CustomRoles.NSerialKiller:
            case CustomRoles.Doppelganger:
            case CustomRoles.PlagueDoctor:
            case CustomRoles.Postman:
            case CustomRoles.Reckless:
            case CustomRoles.WeaponMaster:
            case CustomRoles.Magician:
            case CustomRoles.Vengeance:
            case CustomRoles.HeadHunter:
            case CustomRoles.Imitator:
            case CustomRoles.Werewolf:
            case CustomRoles.Bandit:
            case CustomRoles.Jailor:
            case CustomRoles.Traitor:
            case CustomRoles.Glitch:
            case CustomRoles.Pickpocket:
            case CustomRoles.Maverick:
            case CustomRoles.Jinx:
            case CustomRoles.Parasite:
            case CustomRoles.Agitater:
            case CustomRoles.Crusader:
            case CustomRoles.Refugee:
            case CustomRoles.Jester:
            //case CustomRoles.Pirate:
            //   case CustomRoles.Baker:
            //case CustomRoles.Famine:
            //case CustomRoles.NWitch:
            case CustomRoles.Mario:
            case CustomRoles.Vulture:
            case CustomRoles.God:
            case CustomRoles.SwordsMan:
            case CustomRoles.Innocent:
            case CustomRoles.Pelican:
            case CustomRoles.Medusa:
            case CustomRoles.Revolutionist:
            case CustomRoles.FFF:
            case CustomRoles.Gamer:
            case CustomRoles.HexMaster:
            case CustomRoles.Wraith:
            //      case CustomRoles.Chameleon:
            case CustomRoles.Juggernaut:
            //case CustomRoles.Reverie:
            case CustomRoles.Ritualist:
            case CustomRoles.DarkHide:
            case CustomRoles.Collector:
            case CustomRoles.ImperiusCurse:
            case CustomRoles.Provocateur:
            case CustomRoles.Medic:
            case CustomRoles.BloodKnight:
            case CustomRoles.Camouflager:
            case CustomRoles.Totocalcio:
            case CustomRoles.Romantic:
            case CustomRoles.VengefulRomantic:
            case CustomRoles.RuthlessRomantic:
            case CustomRoles.Succubus:
            case CustomRoles.CursedSoul:
            case CustomRoles.Admirer:
            case CustomRoles.Amnesiac:
            case CustomRoles.Infectious:
            case CustomRoles.Monarch:
            case CustomRoles.Deputy:
            case CustomRoles.Virus:
            case CustomRoles.Farseer:
            //case CustomRoles.Counterfeiter:
            case CustomRoles.Aid:
            case CustomRoles.Escort:
            case CustomRoles.DonutDelivery:
            case CustomRoles.Analyzer:
            case CustomRoles.Witness:
            case CustomRoles.Pursuer:
            case CustomRoles.Spiritcaller:
            case CustomRoles.PlagueBearer:
            case CustomRoles.Pestilence:
            //case CustomRoles.Masochist:
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
            case CustomRoles.Phantom:
                //case CustomRoles.Baker:
                //   case CustomRoles.Famine:
                if (ForRecompute)
                    hasTasks = false;
                break;
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

        foreach (CustomRoles subRole in States.SubRoles.ToArray())
        {
            switch (subRole)
            {
                case CustomRoles.Madmate:
                case CustomRoles.Charmed:
                case CustomRoles.Recruit:
                case CustomRoles.Egoist:
                case CustomRoles.Infected:
                case CustomRoles.EvilSpirit:
                case CustomRoles.Contagious:
                case CustomRoles.Soulless:
                case CustomRoles.Rascal:
                    //ラバーズはタスクを勝利用にカウントしない
                    hasTasks &= !ForRecompute;
                    break;
            }
        }

        if (CopyCat.playerIdList.Contains(p.PlayerId) && ForRecompute) hasTasks = false;

        return hasTasks;
    }

    public static bool CanBeMadmate(this PlayerControl pc)
    {
        return pc != null && pc.GetCustomRole().IsCrewmate() && !pc.Is(CustomRoles.Madmate)
        && !(
            (pc.Is(CustomRoles.Sheriff) && !Options.SheriffCanBeMadmate.GetBool()) ||
            (pc.Is(CustomRoles.Mayor) && !Options.MayorCanBeMadmate.GetBool()) ||
            (pc.Is(CustomRoles.NiceGuesser) && !Options.NGuesserCanBeMadmate.GetBool()) ||
            (pc.Is(CustomRoles.Snitch) && !Options.SnitchCanBeMadmate.GetBool()) ||
            (pc.Is(CustomRoles.Judge) && !Options.JudgeCanBeMadmate.GetBool()) ||
            (pc.Is(CustomRoles.Marshall) && !Options.MarshallCanBeMadmate.GetBool()) ||
            (pc.Is(CustomRoles.Farseer) && !Options.FarseerCanBeMadmate.GetBool()) ||
            (pc.Is(CustomRoles.Retributionist) && !Options.RetributionistCanBeMadmate.GetBool()) ||
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
        bool result = false;
        if (__instance.AmOwner || Options.CurrentGameMode == CustomGameMode.FFA || Options.CurrentGameMode == CustomGameMode.SoloKombat) result = true; //自分ならロールを表示
        if (Main.VisibleTasksCount && PlayerControl.LocalPlayer.Data.IsDead && Options.GhostCanSeeOtherRoles.GetBool()) result = true; //他プレイヤーでVisibleTasksCountが有効なおかつ自分が死んでいるならロールを表示
        if (PlayerControl.LocalPlayer.Is(CustomRoles.Mimic) && Main.VisibleTasksCount && __instance.Data.IsDead && Options.MimicCanSeeDeadRoles.GetBool()) result = true; //他プレイヤーでVisibleTasksCountが有効なおかつ自分が死んでいるならロールを表示
                                                                                                                                                                          //if (__instance.GetCustomRole() == (CustomRoles.Ntr) && Options.LoverKnowRoles.GetBool()) result = true;
        switch (__instance.GetCustomRole())
        {
            case CustomRoles.Crewpostor when PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && Options.CrewpostorKnowsAllies.GetBool():
                result = true;
                break;
            case CustomRoles.Jackal when PlayerControl.LocalPlayer.Is(CustomRoles.Sidekick):
                result = true;
                break;
            case CustomRoles.Jackal when PlayerControl.LocalPlayer.Is(CustomRoles.Recruit):
                result = true;
                break;
            case CustomRoles.Workaholic when Options.WorkaholicVisibleToEveryone.GetBool():
                result = true;
                break;
            case CustomRoles.Doctor when !__instance.GetCustomRole().IsEvilAddons() && Options.DoctorVisibleToEveryone.GetBool():
                result = true;
                break;
            case CustomRoles.Mayor when Options.MayorRevealWhenDoneTasks.GetBool() && __instance.GetPlayerTaskState().IsTaskFinished:
                result = true;
                break;
            case CustomRoles.Marshall when PlayerControl.LocalPlayer.Is(CustomRoleTypes.Crewmate) && __instance.GetPlayerTaskState().IsTaskFinished:
                result = true;
                break;
        }

        if (__instance.Is(CustomRoles.Sidekick) && PlayerControl.LocalPlayer.Is(CustomRoles.Jackal)) result = true;
        if (__instance.Is(CustomRoles.Madmate) && PlayerControl.LocalPlayer.Is(CustomRoles.Madmate) && Options.MadmateKnowWhosMadmate.GetBool()) result = true;
        if (__instance.Is(CustomRoles.Rogue) && PlayerControl.LocalPlayer.Is(CustomRoles.Rogue) && Options.RogueKnowEachOther.GetBool() && Options.RogueKnowEachOtherRoles.GetBool()) result = true;
        if (__instance.Is(CustomRoles.Sidekick) && PlayerControl.LocalPlayer.Is(CustomRoles.Sidekick)) result = true;
        if (__instance.Is(CustomRoles.Mimic) && Main.VisibleTasksCount && __instance.Data.IsDead) result = true;
        if (__instance.Is(CustomRoles.Lovers) && PlayerControl.LocalPlayer.Is(CustomRoles.Lovers) && Options.LoverKnowRoles.GetBool()) result = true;
        if (__instance.Is(CustomRoles.Madmate) && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && Options.ImpKnowWhosMadmate.GetBool()) result = true;
        if (__instance.Is(CustomRoleTypes.Impostor) && PlayerControl.LocalPlayer.Is(CustomRoles.Crewpostor) && Options.AlliesKnowCrewpostor.GetBool()) result = true;
        if (__instance.Is(CustomRoleTypes.Impostor) && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && Options.ImpKnowAlliesRole.GetBool()) result = true;
        if (__instance.Is(CustomRoleTypes.Impostor) && PlayerControl.LocalPlayer.Is(CustomRoles.Madmate) && Options.MadmateKnowWhosImp.GetBool()) result = true;
        if (Totocalcio.KnowRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (Romantic.KnowRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (Lawyer.KnowRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (EvilDiviner.IsShowTargetRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (Ritualist.IsShowTargetRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (Executioner.KnowRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (Succubus.KnowRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (CursedSoul.KnowRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (Admirer.KnowRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (Amnesiac.KnowRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (Infectious.KnowRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (Virus.KnowRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (PlayerControl.LocalPlayer.IsRevealedPlayer(__instance)) result = true;
        if (PlayerControl.LocalPlayer.Is(CustomRoles.God)) result = true;
        if (PlayerControl.LocalPlayer.Is(CustomRoles.GM)) result = true;
        if (Totocalcio.KnowRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (Lawyer.KnowRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (EvilDiviner.IsShowTargetRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (Ritualist.IsShowTargetRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (Executioner.KnowRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (Main.GodMode.Value) result = true;

        return result;
    }
    public static string GetProgressText(PlayerControl pc)
    {
        if (!Main.playerVersion.ContainsKey(0)) return string.Empty; //ホストがMODを入れていなければ未記入を返す
        var taskState = pc.GetPlayerTaskState();
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
        if (!Main.playerVersion.ContainsKey(0)) return string.Empty; //ホストがMODを入れていなければ未記入を返す
        var ProgressText = new StringBuilder();
        var role = Main.PlayerStates[playerId].MainRole;
        try
        {
            switch (role)
            {
                case CustomRoles.Arsonist:
                    var doused = GetDousedPlayerCount(playerId);
                    if (!Options.ArsonistCanIgniteAnytime.GetBool()) ProgressText.Append(ColorString(GetRoleColor(CustomRoles.Arsonist).ShadeColor(0.25f), $"<color=#777777>-</color> {doused.Item1}/{doused.Item2}"));
                    else ProgressText.Append(ColorString(GetRoleColor(CustomRoles.Arsonist).ShadeColor(0.25f), $"<color=#777777>-</color> {doused.Item1}/{Options.ArsonistMaxPlayersToIgnite.GetInt()}"));
                    break;
                case CustomRoles.Sheriff:
                    if (Sheriff.ShowShotLimit.GetBool()) ProgressText.Append(Sheriff.GetShotLimit(playerId));
                    break;
                case CustomRoles.Analyzer:
                    ProgressText.Append(Analyzer.GetProgressText());
                    break;
                case CustomRoles.Alchemist:
                    ProgressText.Append(Alchemist.GetProgressText(playerId));
                    if (Options.UsePets.GetBool() && Main.AlchemistCD.TryGetValue(playerId, out var time) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), Alchemist.VentCooldown.GetInt() - (GetTimeStamp() - time) + 1));
                    break;
                case CustomRoles.Bandit:
                    ProgressText.Append(Bandit.GetStealLimit(playerId));
                    break;
                case CustomRoles.Cleanser:
                    ProgressText.Append(Cleanser.GetProgressText(playerId));
                    break;
                case CustomRoles.Doppelganger:
                    ProgressText.Append(Doppelganger.GetStealLimit(playerId));
                    break;
                case CustomRoles.SerialKiller:
                    if (SerialKiller.SuicideTimer.ContainsKey(playerId))
                    {
                        int SKTime = SerialKiller.TimeLimit.GetInt() - (int)SerialKiller.SuicideTimer[playerId];
                        Color SKColor = SKTime < 10 ? SKTime % 2 == 1 ? Color.yellow : Color.red : Color.white;
                        if (SKTime <= 20) ProgressText.Append(ColorString(SKColor, $"<color=#777777>-</color> {SKTime}s"));
                    }
                    break;
                case CustomRoles.PlagueDoctor:
                    ProgressText.Append(PlagueDoctor.GetProgressText(comms));
                    break;
                case CustomRoles.BountyHunter:
                    if (BountyHunter.ChangeTimer.ContainsKey(playerId))
                    {
                        int BHTime = (int)(BountyHunter.TargetChangeTime - (float)BountyHunter.ChangeTimer[playerId]);
                        if (BHTime <= 15) ProgressText.Append(ColorString(Color.white, $"<color=#777777>-</color> <color=#00ffa5>SWAP:</color> {BHTime}s"));
                    }
                    break;
                case CustomRoles.Camouflager:
                    Color TextColorCamo;
                    if (Camouflager.CamoLimit[playerId] < 1) TextColorCamo = Color.grey;
                    else TextColorCamo = Color.white;
                    ProgressText.Append(ColorString(TextColorCamo, $"<color=#777777>-</color> {Math.Round(Camouflager.CamoLimit[playerId], 1)}"));
                    break;
                case CustomRoles.Councillor:
                    Color TextColorCoun;
                    if (Councillor.MurderLimit[playerId] < 1) TextColorCoun = Color.grey;
                    else TextColorCoun = Color.white;
                    ProgressText.Append(ColorString(TextColorCoun, $"<color=#777777>-</color> {Math.Round(Councillor.MurderLimit[playerId], 1)}"));
                    break;
                case CustomRoles.WeaponMaster:
                    if (!GetPlayerById(playerId).IsModClient()) ProgressText.Append(WeaponMaster.GetHudAndProgressText());
                    break;
                case CustomRoles.Dazzler:
                    Color TextColorDazzler;
                    if (Dazzler.DazzleLimit[playerId] < 1) TextColorDazzler = Color.grey;
                    else TextColorDazzler = Color.white;
                    ProgressText.Append(ColorString(TextColorDazzler, $"<color=#777777>-</color> {Math.Round(Dazzler.DazzleLimit[playerId], 1)}"));
                    break;
                case CustomRoles.Disperser:
                    Color TextColorDisperser;
                    if (Disperser.DisperserLimit[playerId] < 1) TextColorDisperser = Color.grey;
                    else TextColorDisperser = Color.white;
                    ProgressText.Append(ColorString(TextColorDisperser, $"<color=#777777>-</color> {Math.Round(Disperser.DisperserLimit[playerId], 1)}"));
                    if (Options.UsePets.GetBool() && Main.DisperserCD.TryGetValue(playerId, out var time11) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), Disperser.DisperserShapeshiftCooldown.GetInt() - (GetTimeStamp() - time11) + 1));
                    break;
                case CustomRoles.Hangman:
                    Color TextColorHangman;
                    if (Hangman.HangLimit[playerId] < 1) TextColorHangman = Color.grey;
                    else TextColorHangman = Color.white;
                    ProgressText.Append(ColorString(TextColorHangman, $"<color=#777777>-</color> {Math.Round(Hangman.HangLimit[playerId], 1)}"));
                    break;
                case CustomRoles.Twister:
                    Color TextColorTwister;
                    if (Twister.TwistLimit[playerId] < 1) TextColorTwister = Color.grey;
                    else TextColorTwister = Color.white;
                    ProgressText.Append(ColorString(TextColorTwister, $"<color=#777777>-</color> {Math.Round(Twister.TwistLimit[playerId], 1)}"));
                    if (Options.UsePets.GetBool() && Main.TwisterCD.TryGetValue(playerId, out var time12) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), Twister.ShapeshiftCooldown.GetInt() - (GetTimeStamp() - time12) + 1));
                    break;
                case CustomRoles.EvilDiviner:
                    Color TextColorED;
                    if (EvilDiviner.DivinationCount[playerId] < 1) TextColorED = Color.grey;
                    else TextColorED = Color.white;
                    ProgressText.Append(ColorString(TextColorED, $"<color=#777777>-</color> {Math.Round(EvilDiviner.DivinationCount[playerId], 1)}"));
                    break;
                case CustomRoles.Swooper:
                    Color TextColorSwooper;
                    if (Swooper.SwoopLimit[playerId] < 1) TextColorSwooper = Color.grey;
                    else TextColorSwooper = Color.white;
                    ProgressText.Append(ColorString(TextColorSwooper, $"<color=#777777>-</color> {Math.Round(Swooper.SwoopLimit[playerId], 1)}"));
                    break;
                case CustomRoles.Jailor:
                    ProgressText.Append(Jailor.GetProgressText(playerId));
                    break;
                case CustomRoles.NiceSwapper:
                    ProgressText.Append(GetTaskCount(playerId, comms));
                    ProgressText.Append(NiceSwapper.GetNiceSwappermax(playerId));
                    break;
                case CustomRoles.Veteran:
                    Color TextColor21;
                    if (Main.VeteranNumOfUsed[playerId] < 1) TextColor21 = Color.red;
                    else if (Main.VeteranInProtect.ContainsKey(playerId)) TextColor21 = Color.green;
                    else TextColor21 = Color.white;
                    ProgressText.Append(GetTaskCount(playerId, comms));
                    ProgressText.Append(ColorString(TextColor21, $" <color=#777777>-</color> {Math.Round(Main.VeteranNumOfUsed[playerId], 1)}"));
                    if (Options.UsePets.GetBool() && Main.VeteranCD.TryGetValue(playerId, out var time2) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), Options.VeteranSkillCooldown.GetInt() + Options.VeteranSkillDuration.GetInt() - (GetTimeStamp() - time2) + 1));
                    break;
                case CustomRoles.Grenadier:
                    Color TextColor31;
                    if (Main.GrenadierNumOfUsed[playerId] < 1) TextColor31 = Color.red;
                    else if (Main.GrenadierBlinding.ContainsKey(playerId)) TextColor31 = Color.green;
                    else TextColor31 = Color.white;
                    ProgressText.Append(GetTaskCount(playerId, comms));
                    ProgressText.Append(ColorString(TextColor31, $" <color=#777777>-</color> {Math.Round(Main.GrenadierNumOfUsed[playerId], 1)}"));
                    if (Options.UsePets.GetBool() && Main.GrenadierCD.TryGetValue(playerId, out var time3) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), Options.GrenadierSkillCooldown.GetInt() + Options.GrenadierSkillDuration.GetInt() - (GetTimeStamp() - time3) + 1));
                    break;
                case CustomRoles.Divinator:
                    Color TextColor41;
                    if (Divinator.CheckLimit[playerId] < 1) TextColor41 = Color.red;
                    else TextColor41 = Color.white;
                    ProgressText.Append(GetTaskCount(playerId, comms));
                    ProgressText.Append(ColorString(TextColor41, $" <color=#777777>-</color> {Math.Round(Divinator.CheckLimit[playerId])}"));
                    break;
                case CustomRoles.DovesOfNeace:
                    Color TextColor51;
                    if (Main.DovesOfNeaceNumOfUsed[playerId] < 1) TextColor51 = Color.red;
                    else TextColor51 = Color.white;
                    ProgressText.Append(GetTaskCount(playerId, comms));
                    ProgressText.Append(ColorString(TextColor51, $" <color=#777777>-</color> {Math.Round(Main.DovesOfNeaceNumOfUsed[playerId], 1)}"));
                    if (Options.UsePets.GetBool() && Main.DovesOfNeaceCD.TryGetValue(playerId, out var time4) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), Options.DovesOfNeaceCooldown.GetInt() - (GetTimeStamp() - time4) + 1));
                    break;
                case CustomRoles.TimeMaster:
                    Color TextColor61;
                    if (Main.TimeMasterNumOfUsed[playerId] < 1) TextColor61 = Color.red;
                    else if (Main.TimeMasterInProtect.ContainsKey(playerId)) TextColor61 = Color.green;
                    else TextColor61 = Color.white;
                    ProgressText.Append(GetTaskCount(playerId, comms));
                    ProgressText.Append(ColorString(TextColor61, $" <color=#777777>-</color> {Math.Round(Main.TimeMasterNumOfUsed[playerId], 1)}"));
                    if (Options.UsePets.GetBool() && Main.TimeMasterCD.TryGetValue(playerId, out var time5) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), Options.TimeMasterSkillCooldown.GetInt() + Options.TimeMasterSkillDuration.GetInt() - (GetTimeStamp() - time5) + 1));
                    break;
                case CustomRoles.Mediumshiper:
                    Color TextColor71;
                    if (Mediumshiper.ContactLimit[playerId] < 1) TextColor71 = Color.red;
                    else TextColor71 = Color.white;
                    ProgressText.Append(GetTaskCount(playerId, comms));
                    ProgressText.Append(ColorString(TextColor71, $" <color=#777777>-</color> {Math.Round(Mediumshiper.ContactLimit[playerId], 1)}"));
                    break;
                case CustomRoles.ParityCop:
                    Color TextColor81;
                    if (ParityCop.MaxCheckLimit[playerId] < 1) TextColor81 = Color.red;
                    else TextColor81 = Color.white;
                    ProgressText.Append(GetTaskCount(playerId, comms));
                    ProgressText.Append(ColorString(TextColor81, $" <color=#777777>-</color> {Math.Round(ParityCop.MaxCheckLimit[playerId], 1)}"));
                    break;
                case CustomRoles.Oracle:
                    Color TextColor91;
                    if (Oracle.CheckLimit[playerId] < 1) TextColor91 = Color.red;
                    else TextColor91 = Color.white;
                    ProgressText.Append(GetTaskCount(playerId, comms));
                    ProgressText.Append(ColorString(TextColor91, $" <color=#777777>-</color> {Math.Round(Oracle.CheckLimit[playerId], 1)}"));
                    break;
                case CustomRoles.SabotageMaster:
                    Color TextColor101;
                    if (SabotageMaster.SkillLimit.GetFloat() - SabotageMaster.UsedSkillCount < 1) TextColor101 = Color.red;
                    else TextColor101 = Color.white;
                    ProgressText.Append(GetTaskCount(playerId, comms));
                    ProgressText.Append(ColorString(TextColor101, $" <color=#777777>-</color> {Math.Round(SabotageMaster.SkillLimit.GetFloat() - SabotageMaster.UsedSkillCount, 1)}"));
                    break;
                case CustomRoles.Tracker:
                    Color TextColor111;
                    if (Tracker.TrackLimit[playerId] < 1) TextColor111 = Color.red;
                    else TextColor111 = Color.white;
                    ProgressText.Append(GetTaskCount(playerId, comms));
                    ProgressText.Append(ColorString(TextColor111, $" <color=#777777>-</color> {Math.Round(Tracker.TrackLimit[playerId], 1)}"));
                    break;
                case CustomRoles.Bloodhound:
                    Color TextColor121;
                    if (Bloodhound.UseLimit[playerId] < 1) TextColor121 = Color.red;
                    else TextColor121 = Color.white;
                    ProgressText.Append(GetTaskCount(playerId, comms));
                    ProgressText.Append(ColorString(TextColor121, $" <color=#777777>-</color> {Math.Round(Bloodhound.UseLimit[playerId], 1)}"));
                    break;
                case CustomRoles.Chameleon:
                    Color TextColor131;
                    if (Chameleon.UseLimit[playerId] < 1) TextColor131 = Color.red;
                    else if (Chameleon.IsInvis(playerId)) TextColor131 = Color.green;
                    else TextColor131 = Color.white;
                    ProgressText.Append(GetTaskCount(playerId, comms));
                    ProgressText.Append(ColorString(TextColor131, $" <color=#777777>-</color> {Math.Round(Chameleon.UseLimit[playerId], 1)}"));
                    break;
                case CustomRoles.Lighter:
                    Color TextColor141;
                    if (Main.LighterNumOfUsed[playerId] < 1) TextColor141 = Color.red;
                    else if (Main.Lighter.ContainsKey(playerId)) TextColor141 = Color.green;
                    else TextColor141 = Color.white;
                    ProgressText.Append(GetTaskCount(playerId, comms));
                    ProgressText.Append(ColorString(TextColor141, $" <color=#777777>-</color> {Math.Round(Main.LighterNumOfUsed[playerId], 1)}"));
                    if (Options.UsePets.GetBool() && Main.LighterCD.TryGetValue(playerId, out var time6) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), Options.LighterSkillCooldown.GetInt() + Options.LighterSkillDuration.GetInt() - (GetTimeStamp() - time6) + 1));
                    break;
                case CustomRoles.Ventguard:
                    Color TextColor151;
                    if (Main.VentguardNumberOfAbilityUses < 1) TextColor151 = Color.red;
                    else TextColor151 = Color.white;
                    ProgressText.Append(GetTaskCount(playerId, comms));
                    ProgressText.Append(ColorString(TextColor151, $" <color=#777777>-</color> {Math.Round(Main.VentguardNumberOfAbilityUses, 1)}"));
                    break;
                case CustomRoles.SecurityGuard:
                    Color TextColor161;
                    if (Main.SecurityGuardNumOfUsed[playerId] < 1) TextColor161 = Color.red;
                    else if (Main.BlockSabo.ContainsKey(playerId)) TextColor161 = Color.green;
                    else TextColor161 = Color.white;
                    ProgressText.Append(GetTaskCount(playerId, comms));
                    ProgressText.Append(ColorString(TextColor161, $" <color=#777777>-</color> {Math.Round(Main.SecurityGuardNumOfUsed[playerId], 1)}"));
                    if (Options.UsePets.GetBool() && Main.SecurityGuardCD.TryGetValue(playerId, out var time7) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), Options.SecurityGuardSkillCooldown.GetInt() + Options.SecurityGuardSkillDuration.GetInt() - (GetTimeStamp() - time7) + 1));
                    break;
                //case CustomRoles.Pirate:
                //    ProgressText.Append(ColorString(GetRoleColor(CustomRoles.Pirate).ShadeColor(0.25f), $"({Pirate.NumWin}/{Pirate.SuccessfulDuelsToWin.GetInt()})"));
                //    break;
                case CustomRoles.Crusader:
                    ProgressText.Append(Crusader.GetSkillLimit(playerId));
                    break;
                case CustomRoles.Benefactor:
                    ProgressText.Append(Benefactor.GetProgressText(playerId));
                    break;
                case CustomRoles.TaskManager:
                    var taskState1 = Main.PlayerStates?[playerId].GetTaskState();
                    Color TextColor1;
                    var TaskCompleteColor1 = Color.green;
                    var NonCompleteColor1 = Color.yellow;
                    var NormalColor1 = taskState1.IsTaskFinished ? TaskCompleteColor1 : NonCompleteColor1;
                    TextColor1 = comms ? Color.gray : NormalColor1;
                    string Completed1 = comms ? "?" : $"{taskState1.CompletedTasksCount}";
                    string totalCompleted1 = comms ? "?" : $"{GameData.Instance.CompletedTasks}";
                    ProgressText.Append(ColorString(TextColor1, $"<color=#777777>-</color> {Completed1}/{taskState1.AllTasksCount}"));
                    ProgressText.Append($" <color=#777777>-</color> <color=#00ffa5>{totalCompleted1}</color><color=#ffffff>/{GameData.Instance.TotalTasks}</color>");
                    break;
                case CustomRoles.CameraMan:
                    ProgressText.Append(CameraMan.GetProgressText(playerId, comms));
                    if (Options.UsePets.GetBool() && Main.CameraManCD.TryGetValue(playerId, out var time21) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), CameraMan.VentCooldown.GetInt() - (GetTimeStamp() - time21) + 1));
                    break;
                case CustomRoles.NiceHacker:
                    ProgressText.Append(NiceHacker.GetProgressText(playerId, comms));
                    if (Options.UsePets.GetBool() && Main.HackerCD.TryGetValue(playerId, out var time8) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), NiceHacker.AbilityCD.GetInt() - (GetTimeStamp() - time8) + 1));
                    break;
                case CustomRoles.RiftMaker:
                    ProgressText.Append(RiftMaker.GetProgressText());
                    break;
                case CustomRoles.Ricochet:
                    ProgressText.Append(Ricochet.GetProgressText(playerId, comms));
                    break;
                case CustomRoles.Doormaster:
                    ProgressText.Append(Doormaster.GetProgressText(playerId, comms));
                    if (Options.UsePets.GetBool() && Main.DoormasterCD.TryGetValue(playerId, out var time9) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), Doormaster.VentCooldown.GetInt() - (GetTimeStamp() - time9) + 1));
                    break;
                case CustomRoles.Sapper:
                    if (Options.UsePets.GetBool() && Main.SapperCD.TryGetValue(playerId, out var time22) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), Sapper.ShapeshiftCooldown.GetInt() - (GetTimeStamp() - time22) + 1));
                    break;
                case CustomRoles.Druid:
                    if (Options.UsePets.GetBool() && Main.DruidCD.TryGetValue(playerId, out var time23) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), Sapper.ShapeshiftCooldown.GetInt() - (GetTimeStamp() - time23) + 1));
                    break;
                case CustomRoles.CopyCat:
                    ProgressText.Append(ColorString(GetRoleColor(CustomRoles.CopyCat).ShadeColor(0.25f), $"({(CopyCat.MiscopyLimit.TryGetValue(playerId, out var count2) ? count2 : 0)})"));
                    break;
                case CustomRoles.PlagueBearer:
                    var plagued = PlagueBearer.PlaguedPlayerCount(playerId);
                    ProgressText.Append(ColorString(GetRoleColor(CustomRoles.PlagueBearer).ShadeColor(0.25f), $"<color=#777777>-</color> {plagued.Item1}/{plagued.Item2}"));
                    break;
                case CustomRoles.Doomsayer:
                    var doomsayerguess = Doomsayer.GuessedPlayerCount(playerId);
                    ProgressText.Append(ColorString(GetRoleColor(CustomRoles.Doomsayer).ShadeColor(0.25f), $"<color=#777777>-</color> {doomsayerguess.Item1}/{doomsayerguess.Item2}"));
                    break;

                case CustomRoles.Sniper:
                    ProgressText.Append(Sniper.GetBulletCount(playerId));
                    if (Options.UsePets.GetBool() && Main.SniperCD.TryGetValue(playerId, out var time13) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), Options.DefaultShapeshiftCooldown.GetInt() - (GetTimeStamp() - time13) + 1));
                    break;
                case CustomRoles.EvilTracker:
                    ProgressText.Append(EvilTracker.GetMarker(playerId));
                    break;
                case CustomRoles.TimeThief:
                    ProgressText.Append(TimeThief.GetProgressText(playerId));
                    break;
                case CustomRoles.Mario:
                    ProgressText.Append(ColorString(GetRoleColor(CustomRoles.Mario).ShadeColor(0.25f), $"<color=#777777>-</color> {(Main.MarioVentCount.TryGetValue(playerId, out var count) ? count : 0)}/{Options.MarioVentNumWin.GetInt()}"));
                    break;
                case CustomRoles.Vulture:
                    ProgressText.Append(ColorString(GetRoleColor(CustomRoles.Vulture).ShadeColor(0.25f), $"<color=#777777>-</color> {(Vulture.BodyReportCount.TryGetValue(playerId, out var count1) ? count1 : 0)}/{Vulture.NumberOfReportsToWin.GetInt()}"));
                    break;
                //case CustomRoles.Masochist:
                //    ProgressText.Append(ColorString(GetRoleColor(CustomRoles.Masochist).ShadeColor(0.25f), $"<color=#777777>-</color> {(Main.MasochistKillMax.TryGetValue(playerId, out var count3) ? count3 : 0)}/{Options.MasochistKillMax.GetInt()}"));
                //    break;
                case CustomRoles.QuickShooter:
                    ProgressText.Append(QuickShooter.GetShotLimit(playerId));
                    if (Options.UsePets.GetBool() && Main.QuickShooterCD.TryGetValue(playerId, out var time14) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), QuickShooter.ShapeshiftCooldown.GetInt() - (GetTimeStamp() - time14) + 1));
                    break;
                case CustomRoles.SwordsMan:
                    ProgressText.Append(SwordsMan.GetKillLimit(playerId));
                    break;
                case CustomRoles.Pelican:
                    ProgressText.Append(Pelican.GetProgressText(playerId));
                    break;
                //case CustomRoles.Counterfeiter:
                //    ProgressText.Append(Counterfeiter.GetSeelLimit(playerId));
                //    break;
                case CustomRoles.Tether:
                    ProgressText.Append(Tether.GetProgressText(playerId, comms));
                    if (Options.UsePets.GetBool() && Main.TetherCD.TryGetValue(playerId, out var time10) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), Tether.VentCooldown.GetInt() - (GetTimeStamp() - time10) + 1));
                    break;
                case CustomRoles.Spy:
                    ProgressText.Append(Spy.GetProgressText(playerId, comms));
                    break;
                case CustomRoles.Aid:
                    ProgressText.Append(Aid.GetProgressText(playerId, comms));
                    break;
                case CustomRoles.Mafioso:
                    if (!GetPlayerById(playerId).IsModClient()) ProgressText.Append(Mafioso.GetProgressText());
                    break;
                case CustomRoles.Consort:
                    ProgressText.Append(Consort.GetProgressText());
                    break;
                case CustomRoles.Drainer:
                    ProgressText.Append(Drainer.GetProgressText());
                    break;
                case CustomRoles.DonutDelivery:
                    ProgressText.Append(DonutDelivery.GetProgressText());
                    break;
                case CustomRoles.Escort:
                    ProgressText.Append(Escort.GetProgressText());
                    break;
                case CustomRoles.Pursuer:
                    ProgressText.Append(Pursuer.GetSeelLimit(playerId));
                    break;
                case CustomRoles.Revolutionist:
                    var draw = GetDrawPlayerCount(playerId, out var _);
                    ProgressText.Append(ColorString(GetRoleColor(CustomRoles.Revolutionist).ShadeColor(0.25f), $"<color=#777777>-</color> {draw.Item1}/{draw.Item2}"));
                    break;
                case CustomRoles.Gangster:
                    ProgressText.Append(Gangster.GetRecruitLimit(playerId));
                    break;
                case CustomRoles.Medic:
                    ProgressText.Append(Medic.GetSkillLimit(playerId));
                    break;
                case CustomRoles.CursedWolf:
                    int SpellCount = Main.CursedWolfSpellCount[playerId];
                    ProgressText.Append(ColorString(GetRoleColor(CustomRoles.CursedWolf), $"({SpellCount})"));
                    break;
                case CustomRoles.Jinx:
                    int JinxSpellCount = Main.JinxSpellCount[playerId];
                    ProgressText.Append(ColorString(GetRoleColor(CustomRoles.Jinx), $"({JinxSpellCount})"));
                    break;
                case CustomRoles.Collector:
                    ProgressText.Append(Collector.GetProgressText(playerId));
                    break;
                case CustomRoles.Eraser:
                    ProgressText.Append(Eraser.GetProgressText(playerId));
                    break;
                //case CustomRoles.NiceEraser:
                //    ProgressText.Append(NiceEraser.GetProgressText(playerId));
                //    break;
                case CustomRoles.Hacker:
                    ProgressText.Append(Hacker.GetHackLimit(playerId));
                    break;
                case CustomRoles.KB_Normal:
                    ProgressText.Append(SoloKombatManager.GetDisplayScore(playerId));
                    break;
                case CustomRoles.Bomber:
                    if (Options.UsePets.GetBool() && Main.BomberCD.TryGetValue(playerId, out var time15) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), Options.BombCooldown.GetInt() - (GetTimeStamp() - time15) + 1));
                    break;
                case CustomRoles.Nuker:
                    if (Options.UsePets.GetBool() && Main.NukerCD.TryGetValue(playerId, out var time16) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), Options.NukeCooldown.GetInt() - (GetTimeStamp() - time16) + 1));
                    break;
                case CustomRoles.Escapee:
                    if (Options.UsePets.GetBool() && Main.EscapeeCD.TryGetValue(playerId, out var time17) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), Options.EscapeeSSCD.GetInt() - (GetTimeStamp() - time17) + 1));
                    break;
                case CustomRoles.Miner:
                    if (Options.UsePets.GetBool() && Main.MinerCD.TryGetValue(playerId, out var time18) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), Options.MinerSSCD.GetInt() - (GetTimeStamp() - time18) + 1));
                    break;
                case CustomRoles.Assassin:
                    if (Options.UsePets.GetBool() && Main.AssassinCD.TryGetValue(playerId, out var time19) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), Assassin.AssassinateCooldown.GetInt() - (GetTimeStamp() - time19) + 1));
                    break;
                case CustomRoles.Undertaker:
                    if (Options.UsePets.GetBool() && Main.UndertakerCD.TryGetValue(playerId, out var time20) && !GetPlayerById(playerId).IsModClient())
                        ProgressText.Append(' ' + string.Format(GetString("CDPT"), Undertaker.AssassinateCooldown.GetInt() - (GetTimeStamp() - time20) + 1));
                    break;
                case CustomRoles.Killer:
                    ProgressText.Append(FFAManager.GetDisplayScore(playerId));
                    break;
                case CustomRoles.Totocalcio:
                    ProgressText.Append(Totocalcio.GetProgressText(playerId));
                    break;
                case CustomRoles.Succubus:
                    ProgressText.Append(Succubus.GetCharmLimit());
                    break;
                case CustomRoles.CursedSoul:
                    ProgressText.Append(CursedSoul.GetCurseLimit());
                    break;
                case CustomRoles.Admirer:
                    ProgressText.Append(Admirer.GetAdmireLimit());
                    break;
                case CustomRoles.Infectious:
                    ProgressText.Append(Infectious.GetBiteLimit());
                    break;
                case CustomRoles.Monarch:
                    ProgressText.Append(Monarch.GetKnightLimit());
                    break;
                case CustomRoles.Deputy:
                    ProgressText.Append(Deputy.GetHandcuffLimit());
                    break;
                case CustomRoles.Virus:
                    ProgressText.Append(Virus.GetInfectLimit());
                    break;
                case CustomRoles.Ritualist:
                    ProgressText.Append(Ritualist.GetRitualCount(playerId));
                    break;
                case CustomRoles.Jackal:
                    if (Jackal.CanRecruitSidekick.GetBool())
                        ProgressText.Append(Jackal.GetRecruitLimit(playerId));
                    break;
                case CustomRoles.Spiritcaller:
                    ProgressText.Append(Spiritcaller.GetSpiritLimit());
                    break;
                default:
                    //タスクテキスト
                    ProgressText.Append(GetTaskCount(playerId, comms));
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"For {GetPlayerById(playerId).GetNameWithRole().RemoveHtmlTags()}, failed to get progress text:  " + ex.ToString(), "Utils.GetProgressText");
        }
        if (GetPlayerById(playerId).Is(CustomRoles.Damocles))
        {
            ProgressText.Append(' ' + Damocles.GetProgressText());
        }
        if (ProgressText.Length != 0 && !ProgressText.ToString().StartsWith(' '))
            ProgressText.Insert(0, ' '); //空じゃなければ空白を追加

        return ProgressText.ToString();
    }
    public static string GetTaskCount(byte playerId, bool comms)
    {
        var taskState = Main.PlayerStates?[playerId].GetTaskState();
        if (taskState.hasTasks)
        {
            Color TextColor;
            var info = GetPlayerInfoById(playerId);
            var TaskCompleteColor = HasTasks(info) ? Color.green : GetRoleColor(Main.PlayerStates[playerId].MainRole).ShadeColor(0.5f); //タスク完了後の色
            var NonCompleteColor = HasTasks(info) ? Color.yellow : Color.white; //カウントされない人外は白色

            if (Workhorse.IsThisRole(playerId))
                NonCompleteColor = Workhorse.RoleColor;

            var NormalColor = taskState.IsTaskFinished ? TaskCompleteColor : NonCompleteColor;
            if (Main.PlayerStates.TryGetValue(playerId, out var ps) && ps.MainRole == CustomRoles.Crewpostor)
                NormalColor = Color.red;

            TextColor = comms ? Color.gray : NormalColor;
            string Completed = comms ? "?" : $"{taskState.CompletedTasksCount}";
            return ColorString(TextColor, $"<color=#777777>-</color> {Completed}/{taskState.AllTasksCount}");
        }
        else
        {
            return string.Empty;
        }
    }
    public static void ShowActiveSettingsHelp(byte PlayerId = byte.MaxValue)
    {
        SendMessage(GetString("CurrentActiveSettingsHelp") + ":", PlayerId);

        if (Options.DisableDevices.GetBool()) { SendMessage(GetString("DisableDevicesInfo"), PlayerId); }
        if (Options.SyncButtonMode.GetBool()) { SendMessage(GetString("SyncButtonModeInfo"), PlayerId); }
        if (Options.SabotageTimeControl.GetBool()) { SendMessage(GetString("SabotageTimeControlInfo"), PlayerId); }
        if (Options.RandomMapsMode.GetBool()) { SendMessage(GetString("RandomMapsModeInfo"), PlayerId); }
        if (Options.EnableGM.GetBool()) { SendMessage(GetRoleName(CustomRoles.GM) + GetString("GMInfoLong"), PlayerId); }

        foreach (var role in Enum.GetValues(typeof(CustomRoles)).Cast<CustomRoles>().Where(role => role.IsEnable() && !role.IsVanilla()))
        {
            SendMessage(GetRoleName(role) + GetRoleMode(role) + GetString(Enum.GetName(typeof(CustomRoles), role) + "InfoLong"), PlayerId);
        }

        if (Options.NoGameEnd.GetBool()) { SendMessage(GetString("NoGameEndInfo"), PlayerId); }
    }
    /// <summary>
    /// Gets all players within a specified radius from the specified location
    /// </summary>
    /// <param name="radius">The radius</param>
    /// <param name="from">The location which the radius is counted from</param>
    /// <returns>A list containing all PlayerControls within the specified range from the specified location</returns>
    public static List<PlayerControl> GetPlayersInRadius(float radius, Vector2 from)
    {
        var list = new List<PlayerControl>();
        foreach (PlayerControl tg in Main.AllAlivePlayerControls)
        {
            var dis = Vector2.Distance(from, tg.transform.position);
            if (Pelican.IsEaten(tg.PlayerId) || Medic.ProtectList.Contains(tg.PlayerId) || tg.inVent)
                continue;
            if (dis > radius)
                continue;
            list.Add(tg);
        }
        return list;
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
        var mapId = Main.NormalOptions.MapId;
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
            string mode = role.Key.GetMode() == 1 ? GetString("RoleRateNoColor") : GetString("RoleOnNoColor");
            sb.Append($"\n【{GetRoleName(role.Key)}:{mode} ×{role.Key.GetCount()}】\n");
            ShowChildrenSettings(Options.CustomRoleSpawnChances[role.Key], ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }
        foreach (var opt in OptionItem.AllOptions.Where(x => x.GetBool() && x.Parent == null && x.Id >= 80000 && !x.IsHiddenOn(Options.CurrentGameMode)))
        {
            if (opt.Name is "KillFlashDuration" or "RoleAssigningAlgorithm")
                sb.Append($"\n【{opt.GetName(true)}: {opt.GetString()}】\n");
            else
                sb.Append($"\n【{opt.GetName(true)}】\n");
            ShowChildrenSettings(opt, ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
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
            string mode = role.Key.GetMode() == 1 ? GetString("RoleRateNoColor") : GetString("RoleOnNoColor");
            sb.Append($"\n【{GetRoleName(role.Key)}:{mode} ×{role.Key.GetCount()}】\n");
            ShowChildrenSettings(Options.CustomRoleSpawnChances[role.Key], ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }
        sb.Append($"━━━━━━━━━━━━【{GetString("Settings")}】━━━━━━━━━━━━");
        foreach (var opt in OptionItem.AllOptions.Where(x => x.GetBool() && x.Parent == null && x.Id >= 80000 && !x.IsHiddenOn(Options.CurrentGameMode)))
        {
            if (opt.Name == "KillFlashDuration")
                sb.Append($"\n【{opt.GetName(true)}: {opt.GetString()}】\n");
            else
                sb.Append($"\n【{opt.GetName(true)}】\n");
            ShowChildrenSettings(opt, ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }
        sb.Append($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
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
        sb.AppendFormat("\n{0}: {1}", GetRoleName(CustomRoles.GM), Options.EnableGM.GetString().RemoveHtmlTags());

        var impsb = new StringBuilder();
        var neutralsb = new StringBuilder();
        var crewsb = new StringBuilder();
        var addonsb = new StringBuilder();
        //int headCount = -1;
        System.Collections.IList list = Enum.GetValues(typeof(CustomRoles));
        for (int i = 0; i < list.Count; i++)
        {
            CustomRoles role = (CustomRoles)list[i];
            string mode = role.GetMode() == 1 ? GetString("RoleRateNoColor") : GetString("RoleOnNoColor");
            if (role.IsEnable())
            {
                var roleDisplay = $"\n{GetRoleName(role)}:{mode} x{role.GetCount()}";
                if (role.IsAdditionRole()) addonsb.Append(roleDisplay);
                else if (role.IsCrewmate()) crewsb.Append(roleDisplay);
                else if (role.IsImpostor() || role.IsMadmate()) impsb.Append(roleDisplay);
                else if (role.IsNeutral()) neutralsb.Append(roleDisplay);
            }


            //headCount++;
            //if (role.IsImpostor() && headCount == 0) sb.Append("\n\n● " + GetString("TabGroup.ImpostorRoles"));
            //else if (role.IsCrewmate() && headCount == 1) sb.Append("\n\n● " + GetString("TabGroup.CrewmateRoles"));
            //else if (role.IsNeutral() && headCount == 2) sb.Append("\n\n● " + GetString("TabGroup.NeutralRoles"));
            //else if (role.IsAdditionRole() && headCount == 3) sb.Append("\n\n● " + GetString("TabGroup.Addons"));
            //else headCount--;

            //string mode = role.GetMode() == 1 ? GetString("RoleRateNoColor") : GetString("RoleOnNoColor");
            //if (role.IsEnable()) sb.AppendFormat("\n{0}:{1} x{2}", GetRoleName(role), $"{mode}", role.GetCount());
        }
        //  SendMessage(sb.ToString(), PlayerId);
        SendMessage(sb.Append("\n.").ToString(), PlayerId, "<color=#ff5b70>【 ★ Roles ★ 】</color>");
        SendMessage(impsb.Append("\n.").ToString(), PlayerId, ColorString(GetRoleColor(CustomRoles.Impostor), "【 ★ Impostor Roles ★ 】"));
        SendMessage(crewsb.Append("\n.").ToString(), PlayerId, ColorString(GetRoleColor(CustomRoles.Crewmate), "【 ★ Crewmate Roles ★ 】"));
        SendMessage(neutralsb.Append("\n.").ToString(), PlayerId, "<color=#7f8c8d>【 ★ Neutral Roles ★ 】</color>");
        SendMessage(addonsb.Append("\n.").ToString(), PlayerId, "<color=#ff9ace>【 ★ Add-ons ★ 】</color>");
        //foreach (string roleList in sb.ToString().Split("\n\n●"))
        //    SendMessage("\n\n●" + roleList + "\n\n.", PlayerId);
    }
    public static void ShowChildrenSettings(OptionItem option, ref StringBuilder sb, int deep = 0, bool command = false)
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
                case "Maximum":
                case "DisableSkeldDevices" when !Options.IsActiveSkeld:
                case "DisableMiraHQDevices" when !Options.IsActiveMiraHQ:
                case "DisablePolusDevices" when !Options.IsActivePolus:
                case "DisableAirshipDevices" when !Options.IsActiveAirship:
                case "PolusReactorTimeLimit" when !Options.IsActivePolus:
                case "AirshipReactorTimeLimit" when !Options.IsActiveAirship:
                    continue;
            }
            if (deep > 0)
            {
                sb.Append(string.Concat(Enumerable.Repeat("┃", Mathf.Max(deep - 1, 0))));
                sb.Append(opt.Index == option.Children.Count ? "┗ " : "┣ ");
            }
            var value = opt.Value.GetString().Replace("ON", "<#00ffa5>ON</color>").Replace("OFF", "<#ff0000>OFF</color>");
            string name = $"<#000000>{opt.Value.GetName(disableColor: true).Replace("<color=#ffffff>", string.Empty).Replace("<color=#ffffffff>", string.Empty).Replace("color=", string.Empty)}</color>";
            sb.Append($"{name}: <b>{value}</b>\n");
            if (opt.Value.GetBool()) ShowChildrenSettings(opt.Value, ref sb, deep + 1);
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

        sb.Append("<color=#ffffff><u>Role Summary:</u></color><size=70%>");

        List<byte> cloneRoles = new(Main.PlayerStates.Keys);
        foreach (byte id in Main.winnerList.ToArray())
        {
            if (EndGamePatch.SummaryText[id].Contains("<INVALID:NotAssigned>")) continue;
            sb.Append($"\n<color=#c4aa02>★</color> ").Append(EndGamePatch.SummaryText[id]/*.RemoveHtmlTags()*/);
            cloneRoles.Remove(id);
        }
        if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
        {
            List<(int, byte)> list = [];
            foreach (byte id in cloneRoles.ToArray())
            {
                list.Add((SoloKombatManager.GetRankOfScore(id), id));
            }

            list.Sort();

            foreach ((int, byte) id in list.ToArray())
            {
                sb.Append($"\n　 ").Append(EndGamePatch.SummaryText[id.Item2]);
            }
        }
        else if (Options.CurrentGameMode == CustomGameMode.FFA)
        {
            List<(int, byte)> list = [];
            foreach (byte id in cloneRoles.ToArray())
            {
                list.Add((FFAManager.GetRankOfScore(id), id));
            }

            list.Sort();

            foreach ((int, byte) id in list.ToArray())
            {
                sb.Append($"\n　 ").Append(EndGamePatch.SummaryText[id.Item2]);
            }
        }
        else
        {
            foreach (byte id in cloneRoles.ToArray())
            {
                if (EndGamePatch.SummaryText[id].Contains("<INVALID:NotAssigned>"))
                    continue;
                sb.Append($"\n　 ").Append(EndGamePatch.SummaryText[id]);
            }
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
        var sb = new StringBuilder();
        if (SetEverythingUpPatch.LastWinsText != string.Empty) sb.Append($"<size=90%>{GetString("LastResult")} {SetEverythingUpPatch.LastWinsText}</size>");
        if (SetEverythingUpPatch.LastWinsReason != string.Empty) sb.Append($"\n<size=90%>{GetString("LastEndReason")} {SetEverythingUpPatch.LastWinsReason}</size>");
        if (sb.Length > 0 && Options.CurrentGameMode != CustomGameMode.SoloKombat && Options.CurrentGameMode != CustomGameMode.FFA) SendMessage("\n", PlayerId, sb.ToString());
    }
    public static string EmptyMessage() => "<size=0>.</size>";
    public static string GetSubRolesText(byte id, bool disableColor = false, bool intro = false, bool summary = false)
    {
        var SubRoles = Main.PlayerStates[id].SubRoles;
        if (!SubRoles.Any()) return string.Empty;
        var sb = new StringBuilder();
        bool isLovers = false;
        if (intro)
        {
            foreach (CustomRoles role in SubRoles.ToArray())
            {
                if (role is CustomRoles.NotAssigned or CustomRoles.LastImpostor) SubRoles.Remove(role);
                if (role is CustomRoles.Lovers)
                {
                    SubRoles.Remove(role);
                    isLovers = true;
                }
            }

            if (intro && isLovers)
            {
                //var RoleText = disableColor ? GetRoleName(CustomRoles.Lovers) : ColorString(GetRoleColor(CustomRoles.Lovers), GetRoleName(CustomRoles.Lovers));
                sb.Append($"{ColorString(GetRoleColor(CustomRoles.Lovers), " ♥")}");
            }

            sb.Append("<size=15%>");
            if (SubRoles.Count == 1)
            {
                CustomRoles role = SubRoles[0];

                var RoleText = ColorString(GetRoleColor(role), GetRoleName(role));
                sb.Append($"{ColorString(Color.gray, "\nModifier: ")}{RoleText}");
            }
            else
            {
                sb.Append($"{ColorString(Color.gray, "\nModifiers: ")}");
                for (int i = 0; i < SubRoles.Count; i++)
                {
                    if (i != 0) sb.Append(", ");
                    CustomRoles role = SubRoles[i];
                    if (role is CustomRoles.NotAssigned or CustomRoles.LastImpostor) continue;

                    var RoleText = ColorString(GetRoleColor(role), GetRoleName(role));
                    sb.Append($"{RoleText}");
                }
            }
            sb.Append("</size>");
        }
        else if (!summary)
        {
            foreach (CustomRoles role in SubRoles.ToArray())
            {
                if (role is CustomRoles.NotAssigned or CustomRoles.LastImpostor)
                    continue;
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
        try { color = int.Parse(text); } catch { color = -1; }
        switch (text)
        {
            case "0":
            case "红":
            case "紅":
            case "red":
            case "Red":
            case "крас":
            case "Крас":
            case "красн":
            case "Красн":
            case "красный":
            case "Красный":
                color = 0; break;
            case "1":
            case "蓝":
            case "藍":
            case "深蓝":
            case "blue":
            case "Blue":
            case "син":
            case "Син":
            case "синий":
            case "Синий":
                color = 1; break;
            case "2":
            case "绿":
            case "綠":
            case "深绿":
            case "green":
            case "Green":
            case "Зел":
            case "зел":
            case "Зелёный":
            case "Зеленый":
            case "зелёный":
            case "зеленый":
                color = 2; break;
            case "3":
            case "粉红":
            case "pink":
            case "Pink":
            case "Роз":
            case "роз":
            case "Розовый":
            case "розовый":
                color = 3; break;
            case "4":
            case "橘":
            case "orange":
            case "Orange":
            case "оранж":
            case "Оранж":
            case "оранжевый":
            case "Оранжевый":
                color = 4; break;
            case "5":
            case "黄":
            case "黃":
            case "yellow":
            case "Yellow":
            case "Жёлт":
            case "Желт":
            case "жёлт":
            case "желт":
            case "Жёлтый":
            case "Желтый":
            case "жёлтый":
            case "желтый":
                color = 5; break;
            case "6":
            case "黑":
            case "black":
            case "Black":
            case "Чёрный":
            case "Черный":
            case "чёрный":
            case "черный":
                color = 6; break;
            case "7":
            case "白":
            case "white":
            case "White":
            case "Белый":
            case "белый":
                color = 7; break;
            case "8":
            case "紫":
            case "purple":
            case "Purple":
            case "Фиол":
            case "фиол":
            case "Фиолетовый":
            case "фиолетовый":
                color = 8; break;
            case "9":
            case "棕":
            case "brown":
            case "Brown":
            case "Корич":
            case "корич":
            case "Коричневый":
            case "коричевый":
                color = 9; break;
            case "10":
            case "青":
            case "cyan":
            case "Cyan":
            case "Голуб":
            case "голуб":
            case "Голубой":
            case "голубой":
                color = 10; break;
            case "11":
            case "黄绿":
            case "黃綠":
            case "浅绿":
            case "lime":
            case "Lime":
            case "Лайм":
            case "лайм":
            case "Лаймовый":
            case "лаймовый":
                color = 11; break;
            case "12":
            case "红褐":
            case "紅褐":
            case "深红":
            case "maroon":
            case "Maroon":
            case "Борд":
            case "борд":
            case "Бордовый":
            case "бордовый":
                color = 12; break;
            case "13":
            case "玫红":
            case "玫紅":
            case "浅粉":
            case "rose":
            case "Rose":
            case "Светло роз":
            case "светло роз":
            case "Светло розовый":
            case "светло розовый":
            case "Сирень":
            case "сирень":
            case "Сиреневый":
            case "сиреневый":
                color = 13; break;
            case "14":
            case "焦黄":
            case "焦黃":
            case "淡黄":
            case "banana":
            case "Banana":
            case "Банан":
            case "банан":
            case "Банановый":
            case "банановый":
                color = 14; break;
            case "15":
            case "灰":
            case "gray":
            case "Gray":
            case "Сер":
            case "сер":
            case "Серый":
            case "серый":
                color = 15; break;
            case "16":
            case "茶":
            case "tan":
            case "Tan":
            case "Загар":
            case "загар":
            case "Загаровый":
            case "загаровый":
                color = 16; break;
            case "17":
            case "珊瑚":
            case "coral":
            case "Coral":
            case "Корал":
            case "корал":
            case "Коралл":
            case "коралл":
            case "Коралловый":
            case "коралловый":
                color = 17; break;

            case "18": case "隐藏": case "?": color = 18; break;
        }
        return !isHost && color == 18 ? byte.MaxValue : color is < 0 or > 18 ? byte.MaxValue : Convert.ToByte(color);
    }

    public static void ShowHelpToClient(byte ID)
    {
        SendMessage(
            GetString("CommandList")
            + $"\n  ○ /n {GetString("Command.now")}"
            + $"\n  ○ /r {GetString("Command.roles")}"
            + $"\n  ○ /m {GetString("Command.myrole")}"
            + $"\n  ○ /xf {GetString("Command.solvecover")}"
            + $"\n  ○ /l {GetString("Command.lastresult")}"
            + $"\n  ○ /win {GetString("Command.winner")}"
            + "\n\n" + GetString("CommandOtherList")
            + $"\n  ○ /color {GetString("Command.color")}"
            + $"\n  ○ /qt {GetString("Command.quit")}"
            , ID);
    }
    public static void ShowHelp(byte ID)
    {
        SendMessage(
            GetString("CommandList")
            + $"\n  ○ /n {GetString("Command.now")}"
            + $"\n  ○ /r {GetString("Command.roles")}"
            + $"\n  ○ /m {GetString("Command.myrole")}"
            + $"\n  ○ /l {GetString("Command.lastresult")}"
            + $"\n  ○ /win {GetString("Command.winner")}"
            + "\n\n" + GetString("CommandOtherList")
            + $"\n  ○ /color {GetString("Command.color")}"
            + $"\n  ○ /rn {GetString("Command.rename")}"
            + $"\n  ○ /qt {GetString("Command.quit")}"
            + "\n\n" + GetString("CommandHostList")
            + $"\n  ○ /s {GetString("Command.say")}"
            + $"\n  ○ /rn {GetString("Command.rename")}"
            + $"\n  ○ /xf {GetString("Command.solvecover")}"
            + $"\n  ○ /mw {GetString("Command.mw")}"
            + $"\n  ○ /kill {GetString("Command.kill")}"
            + $"\n  ○ /exe {GetString("Command.exe")}"
            + $"\n  ○ /level {GetString("Command.level")}"
            + $"\n  ○ /id {GetString("Command.idlist")}"
            + $"\n  ○ /qq {GetString("Command.qq")}"
            + $"\n  ○ /dump {GetString("Command.dump")}"
            , ID);
    }
    public static void CheckTerroristWin(GameData.PlayerInfo Terrorist)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        var taskState = GetPlayerById(Terrorist.PlayerId).GetPlayerTaskState();
        if (taskState.IsTaskFinished && (!Main.PlayerStates[Terrorist.PlayerId].IsSuicide() || Options.CanTerroristSuicideWin.GetBool())) //タスクが完了で（自殺じゃない OR 自殺勝ちが許可）されていれば
        {
            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                if (pc.Is(CustomRoles.Terrorist))
                {
                    if (Main.PlayerStates[pc.PlayerId].deathReason == PlayerState.DeathReason.Vote)
                    {
                        Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.etc;
                    }
                    else
                    {
                        Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.Suicide;
                    }
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
    public static void SendMessage(string text, byte sendTo = byte.MaxValue, string title = "")
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (title == "") title = "<color=#aaaaff>" + GetString("DefaultSystemMessageTitle") + "</color>";
        Main.MessagesToSend.Add((text.RemoveHtmlTagsTemplate(), sendTo, title));
    }
    public static void ApplySuffix(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || player == null) return;
        if (!(player.AmOwner || player.FriendCode.GetDevUser().HasTag())) return;
        string name = Main.AllPlayerNames.TryGetValue(player.PlayerId, out var n) ? n : string.Empty;
        if (Main.nickName != string.Empty && player.AmOwner) name = Main.nickName;
        if (name == string.Empty) return;
        if (AmongUsClient.Instance.IsGameStarted)
        {
            if (Options.FormatNameMode.GetInt() == 1 && Main.nickName == string.Empty) name = Palette.GetColorName(player.Data.DefaultOutfit.ColorId);
        }
        else
        {
            if (!GameStates.IsLobby) return;
            if (player.AmOwner)
            {
                if (GameStates.IsOnlineGame || GameStates.IsLocalGame)
                    name = $"<color={GetString("HostColor")}>{GetString("HostText")}</color><color={GetString("IconColor")}>{GetString("Icon")}</color><color={GetString("NameColor")}>{name}</color>";

                //name = $"<color=#902efd>{GetString("HostText")}</color><color=#4bf4ff>♥</color>" + name;

                if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
                    name = $"<color=#f55252><size=1.7>{GetString("ModeSoloKombat")}</size></color>\r\n" + name;
                if (Options.CurrentGameMode == CustomGameMode.FFA)
                    name = $"<color=#00ffff><size=1.7>{GetString("ModeFFA")}</size></color>\r\n" + name;
            }
            if (!name.Contains('\r') && player.FriendCode.GetDevUser().HasTag())
                name = player.FriendCode.GetDevUser().GetTag() + name;
            else
                name = Options.GetSuffixMode() switch
                {
                    SuffixModes.TOHE => name += $"\r\n<color={Main.ModColor}>TOHE-R v{Main.PluginDisplayVersion}</color>",
                    SuffixModes.Streaming => name += $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixMode.Streaming")}</color></size>",
                    SuffixModes.Recording => name += $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixMode.Recording")}</color></size>",
                    SuffixModes.RoomHost => name += $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixMode.RoomHost")}</color></size>",
                    SuffixModes.OriginalName => name += $"\r\n<size=1.7><color={Main.ModColor}>{DataManager.player.Customization.Name}</color></size>",
                    SuffixModes.DoNotKillMe => name += $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixModeText.DoNotKillMe")}</color></size>",
                    SuffixModes.NoAndroidPlz => name += $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixModeText.NoAndroidPlz")}</color></size>",
                    SuffixModes.AutoHost => name += $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixModeText.AutoHost")}</color></size>",
                    _ => name
                };
        }
        if (name != player.name && player.CurrentOutfitType == PlayerOutfitType.Default)
            player.RpcSetName(name);
    }
    public static PlayerControl GetPlayerById(int PlayerId)
    {
        return Main.AllPlayerControls.FirstOrDefault(pc => pc.PlayerId == PlayerId);
    }
    public static GameData.PlayerInfo GetPlayerInfoById(int PlayerId) =>
        GameData.Instance.AllPlayers.ToArray().FirstOrDefault(info => info.PlayerId == PlayerId);
    private static readonly StringBuilder SelfSuffix = new();
    private static readonly StringBuilder SelfMark = new(20);
    private static readonly StringBuilder TargetSuffix = new();
    private static readonly StringBuilder TargetMark = new(20);

    public static async void NotifyRoles(bool isForMeeting = false, PlayerControl SpecifySeer = null, PlayerControl SpecifyTarget = null, bool NoCache = false, bool ForceLoop = false, bool CamouflageIsForMeeting = false, bool GuesserIsForMeeting = false, bool MushroomMixup = false)
    {
        //if (Options.DeepLowLoad.GetBool()) await Task.Run(() => { DoNotifyRoles(isForMeeting, SpecifySeer, NoCache, ForceLoop, CamouflageIsForMeeting, GuesserIsForMeeting); });
        /*else */
        await DoNotifyRoles(isForMeeting, SpecifySeer, SpecifyTarget, NoCache, ForceLoop, CamouflageIsForMeeting, GuesserIsForMeeting, MushroomMixup);
    }

    public static Task DoNotifyRoles(bool isForMeeting = false, PlayerControl SpecifySeer = null, PlayerControl SpecifyTarget = null, bool NoCache = false, bool ForceLoop = false, bool CamouflageIsForMeeting = false, bool GuesserIsForMeeting = false, bool MushroomMixup = false)
    {
        if (!AmongUsClient.Instance.AmHost) return Task.CompletedTask;
        if (Main.AllPlayerControls == null) return Task.CompletedTask;

        if (GameStates.IsMeeting) return Task.CompletedTask;

        var caller = new System.Diagnostics.StackFrame(1, false);
        var callerMethod = caller.GetMethod();
        string callerMethodName = callerMethod.Name;
        string callerClassName = callerMethod.DeclaringType.FullName;
        Logger.Info("NotifyRoles was called from " + callerClassName + "." + callerMethodName, "NotifyRoles");
        HudManagerPatch.NowCallNotifyRolesCount++;
        HudManagerPatch.LastSetNameDesyncCount = 0;

        PlayerControl[] seerList = SpecifySeer != null ? ([SpecifySeer]) : Main.AllPlayerControls;
        PlayerControl[] targetList = SpecifyTarget != null ? ([SpecifyTarget]) : Main.AllPlayerControls;

        //seer: Players who can see changes made here
        //target: Players subject to changes that seer can see
        foreach (PlayerControl seer in seerList)
        {
            try
            {
                if (seer == null || seer.Data.Disconnected || seer.IsModClient()) continue;

                string fontSize = "1.6";
                if (isForMeeting && (seer.GetClient().PlatformData.Platform == Platforms.Playstation || seer.GetClient().PlatformData.Platform == Platforms.Switch)) fontSize = "70%";
                Logger.Info("NotifyRoles-Loop1-" + seer.GetNameWithRole().RemoveHtmlTags() + ":START", "NotifyRoles");

                // Text containing progress, such as tasks
                string SelfTaskText = GetProgressText(seer);
                SelfMark.Clear();

                if (Snitch.IsEnable) SelfMark.Append(Snitch.GetWarningArrow(seer));
                if (seer.Is(CustomRoles.Lovers)) SelfMark.Append(ColorString(GetRoleColor(CustomRoles.Lovers), "♥"));
                if (BallLightning.IsGhost(seer) && BallLightning.IsEnable) SelfMark.Append(ColorString(GetRoleColor(CustomRoles.BallLightning), "■"));
                if (Medic.IsEnable
                    && (Medic.InProtect(seer.PlayerId) || Medic.TempMarkProtected == seer.PlayerId)
                    && !seer.Is(CustomRoles.Medic)
                    && (Medic.WhoCanSeeProtect.GetInt() == 0 || Medic.WhoCanSeeProtect.GetInt() == 2))
                    SelfMark.Append(ColorString(GetRoleColor(CustomRoles.Medic), " ●"));
                if (Gamer.IsEnable) SelfMark.Append(Gamer.TargetMark(seer, seer));
                if (Sniper.IsEnable) SelfMark.Append(Sniper.GetShotNotify(seer.PlayerId));
                if (Blackmailer.ForBlackmailer.Contains(seer.PlayerId)) SelfMark.Append(ColorString(GetRoleColor(CustomRoles.Blackmailer), "╳"));

                SelfSuffix.Clear();

                if (!isForMeeting)
                {
                    switch (seer.GetCustomRole())
                    {
                        case CustomRoles.SuperStar when Options.EveryOneKnowSuperStar.GetBool():
                            SelfMark.Append(ColorString(GetRoleColor(CustomRoles.SuperStar), "★"));
                            break;
                        case CustomRoles.BountyHunter:
                            SelfSuffix.Append(BountyHunter.GetTargetText(seer, false));
                            SelfSuffix.Append(BountyHunter.GetTargetArrow(seer));
                            break;
                        case CustomRoles.Ricochet:
                            SelfSuffix.Append(Ricochet.TargetText);
                            break;
                        case CustomRoles.Tether when !seer.IsModClient():
                            SelfSuffix.Append(Tether.TargetText);
                            break;
                        case CustomRoles.Hitman:
                            SelfSuffix.Append(Hitman.GetTargetText());
                            break;
                        case CustomRoles.Romantic:
                            SelfSuffix.Append(Romantic.GetTargetText(seer.PlayerId));
                            break;
                        case CustomRoles.VengefulRomantic:
                            SelfSuffix.Append(VengefulRomantic.GetTargetText(seer.PlayerId));
                            break;
                        case CustomRoles.Postman when !seer.IsModClient():
                            SelfSuffix.Append(Postman.TargetText);
                            break;
                        case CustomRoles.Druid when !seer.IsModClient():
                            SelfSuffix.Append(Druid.GetSuffixText(seer.PlayerId));
                            break;
                        case CustomRoles.Mortician:
                            SelfSuffix.Append(Mortician.GetTargetArrow(seer));
                            break;
                        case CustomRoles.Tracefinder:
                            SelfSuffix.Append(Tracefinder.GetTargetArrow(seer));
                            break;
                        case CustomRoles.Vulture when Vulture.ArrowsPointingToDeadBody.GetBool():
                            SelfSuffix.Append(Vulture.GetTargetArrow(seer));
                            break;
                        case CustomRoles.YinYanger when !seer.IsModClient():
                            SelfSuffix.Append(YinYanger.ModeText);
                            break;
                        case CustomRoles.FireWorks:
                            SelfSuffix.Append(FireWorks.GetStateText(seer));
                            break;
                        case CustomRoles.Witch:
                            SelfSuffix.Append(Witch.GetSpellModeText(seer, false, isForMeeting));
                            break;
                        case CustomRoles.HexMaster:
                            SelfSuffix.Append(HexMaster.GetHexModeText(seer, false, isForMeeting));
                            break;
                        case CustomRoles.AntiAdminer:
                            if (AntiAdminer.IsAdminWatch) SelfSuffix.Append(GetString("AntiAdminerAD"));
                            if (AntiAdminer.IsVitalWatch) SelfSuffix.Append(GetString("AntiAdminerVI"));
                            if (AntiAdminer.IsDoorLogWatch) SelfSuffix.Append(GetString("AntiAdminerDL"));
                            if (AntiAdminer.IsCameraWatch) SelfSuffix.Append(GetString("AntiAdminerCA"));
                            break;
                        case CustomRoles.Monitor:
                            if (Monitor.IsAdminWatch) SelfSuffix.Append(ColorString(GetRoleColor(CustomRoles.Monitor), GetString("AdminWarning")));
                            if (Monitor.IsVitalWatch) SelfSuffix.Append(ColorString(GetRoleColor(CustomRoles.Monitor), GetString("VitalsWarning")));
                            if (Monitor.IsDoorLogWatch) SelfSuffix.Append(ColorString(GetRoleColor(CustomRoles.Monitor), GetString("DoorlogWarning")));
                            if (Monitor.IsCameraWatch) SelfSuffix.Append(ColorString(GetRoleColor(CustomRoles.Monitor), GetString("CameraWarning")));
                            break;
                        case CustomRoles.Bloodhound:
                            SelfSuffix.Append(Bloodhound.GetTargetArrow(seer));
                            break;
                        case CustomRoles.Tracker:
                            SelfSuffix.Append(Tracker.GetTrackerArrow(seer));
                            break;
                        case CustomRoles.Spiritualist:
                            SelfSuffix.Append(Spiritualist.GetSpiritualistArrow(seer));
                            break;
                        case CustomRoles.Snitch:
                            SelfSuffix.Append(Snitch.GetSnitchArrow(seer));
                            break;
                        case CustomRoles.EvilTracker:
                            SelfSuffix.Append(EvilTracker.GetTargetArrow(seer, seer));
                            break;
                    }
                }
                else
                {
                    if (Witch.IsEnable) SelfMark.Append(Witch.GetSpelledMark(seer.PlayerId, isForMeeting));
                    if (HexMaster.IsEnable) SelfMark.Append(HexMaster.GetHexedMark(seer.PlayerId, isForMeeting));
                }

                if (Deathpact.IsEnable)
                    SelfSuffix.Append(Deathpact.GetDeathpactPlayerArrow(seer));

                switch (Options.CurrentGameMode)
                {
                    case CustomGameMode.FFA:
                        SelfSuffix.Append(FFAManager.GetPlayerArrow(seer));
                        break;
                    case CustomGameMode.SoloKombat:
                        SelfSuffix.Append(SoloKombatManager.GetDisplayHealth(seer));
                        break;
                }

                string SeerRealName = seer.GetRealName(isForMeeting);

                if (!isForMeeting && MeetingStates.FirstMeeting && Options.ChangeNameToRoleInfo.GetBool() && Options.CurrentGameMode != CustomGameMode.FFA)
                {
                    if (seer.GetCustomRole().IsCrewmate() && !seer.Is(CustomRoles.Madmate))
                    {
                        SeerRealName = $"<color=#8cffff>" + GetString("YouAreCrewmate") + $"</color>\n" + seer.GetRoleInfo();
                    }
                    if (seer.GetCustomRole().IsImpostor())
                    {
                        SeerRealName = $"<color=#ff1919>"/* + GetString("YouAreImpostor")*/ + $"</color>\n<size=90%>" + seer.GetRoleInfo() + $"</size>";
                    }
                    if (seer.GetCustomRole().IsNeutral() && !seer.GetCustomRole().IsMadmate())
                    {
                        SeerRealName = $"<color=#7f8c8d>" + GetString("YouAreNeutral") + $"</color>\n<size=90%>" + seer.GetRoleInfo() + $"</size>";
                    }
                    if (seer.GetCustomRole().IsMadmate())
                    {
                        SeerRealName = $"<color=#ff1919>" + GetString("YouAreMadmate") + $"</color>\n<size=90%>" + seer.GetRoleInfo() + $"</size>";
                    }
                    if (seer.Is(CustomRoles.Madmate))
                    {
                        SeerRealName = $"<color=#ff1919>" + GetString("YouAreMadmate") + $"</color>\n<size=90%>" + seer.GetRoleInfo() + $"</size>";
                    }
                }

                // Combine seer's job title and SelfTaskText with seer's player name and SelfMark
                string SelfRoleName = $"<size={fontSize}>{seer.GetDisplayRoleName()}{SelfTaskText}</size>";
                string SelfDeathReason = seer.KnowDeathReason(seer) ? $"({ColorString(GetRoleColor(CustomRoles.Doctor), GetVitalText(seer.PlayerId))})" : string.Empty;
                string SelfName = $"{ColorString(seer.GetRoleColor(), SeerRealName)}{SelfDeathReason}{SelfMark}";

                switch (seer.GetCustomRole())
                {
                    case CustomRoles.PlagueBearer when PlagueBearer.IsPlaguedAll(seer):
                        seer.RpcSetCustomRole(CustomRoles.Pestilence);
                        seer.Notify(GetString("PlagueBearerToPestilence"));
                        seer.RpcGuardAndKill(seer);
                        if (!PlagueBearer.PestilenceList.Contains(seer.PlayerId))
                            PlagueBearer.PestilenceList.Add(seer.PlayerId);
                        PlagueBearer.SetKillCooldownPestilence(seer.PlayerId);
                        PlagueBearer.playerIdList.Remove(seer.PlayerId);
                        break;
                    case CustomRoles.Arsonist when seer.IsDouseDone():
                        SelfName = $"{ColorString(seer.GetRoleColor(), GetString("EnterVentToWin"))}";
                        break;
                    case CustomRoles.Revolutionist when seer.IsDrawDone():
                        SelfName = $">{ColorString(seer.GetRoleColor(), string.Format(GetString("EnterVentWinCountDown"), Main.RevolutionistCountdown.TryGetValue(seer.PlayerId, out var x) ? x : 10))}";
                        break;
                }

                if (Pelican.IsEaten(seer.PlayerId))
                    SelfName = $"{ColorString(GetRoleColor(CustomRoles.Pelican), GetString("EatenByPelican"))}";
                if (Deathpact.IsInActiveDeathpact(seer))
                    SelfName = Deathpact.GetDeathpactString(seer);
                if (NameNotifyManager.GetNameNotify(seer, out var name))
                    SelfName = name;

                // Devourer
                if (Devourer.HideNameOfConsumedPlayer.GetBool() && Devourer.PlayerSkinsCosumed.Any(a => a.Value.Contains(seer.PlayerId)) && !CamouflageIsForMeeting)
                    SelfName = GetString("DevouredName");
                // Camouflage
                if (((IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool() && (Main.NormalOptions.MapId != 5 || !Options.CommsCamouflageDisableOnFungle.GetBool())) || Camouflager.IsActive) && !CamouflageIsForMeeting)
                    SelfName = $"<size=0>{SelfName}</size>";

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
                        SelfName = SelfRoleName + "\r\n" + SelfName;
                        break;
                }

                SelfName += SelfSuffix.ToString() == string.Empty ? string.Empty : "\r\n " + SelfSuffix.ToString();
                if (!isForMeeting) SelfName += "\r\n";

                seer.RpcSetNamePrivate(SelfName, true, force: NoCache);


                // Run the second loop only when necessary, such as when seer is dead
                if (seer.Data.IsDead || !seer.IsAlive() || NoCache || CamouflageIsForMeeting || MushroomMixup || IsActive(SystemTypes.MushroomMixupSabotage) || ForceLoop || seerList.Length == 1)
                {
                    foreach (PlayerControl target in targetList)
                    {
                        if (target.PlayerId == seer.PlayerId) continue;
                        Logger.Info("NotifyRoles-Loop2-" + target.GetNameWithRole().RemoveHtmlTags() + ":START", "NotifyRoles");


                        if ((IsActive(SystemTypes.MushroomMixupSabotage) || MushroomMixup) && target.IsAlive() && !seer.Is(CustomRoleTypes.Impostor) && Main.ResetCamPlayerList.Contains(seer.PlayerId))
                        {
                            seer.RpcSetNamePrivate("<size=0%>", true, force: NoCache);
                        }
                        else
                        {

                            TargetMark.Clear();

                            if (Witch.IsEnable) TargetMark.Append(Witch.GetSpelledMark(target.PlayerId, isForMeeting));
                            if (HexMaster.IsEnable) TargetMark.Append(HexMaster.GetHexedMark(target.PlayerId, isForMeeting));

                            if (target.Is(CustomRoles.SuperStar) && Options.EveryOneKnowSuperStar.GetBool())
                                TargetMark.Append(ColorString(GetRoleColor(CustomRoles.SuperStar), "★"));

                            if (BallLightning.IsGhost(target) && BallLightning.IsEnable)
                                TargetMark.Append(ColorString(GetRoleColor(CustomRoles.BallLightning), "■"));

                            if (Snitch.IsEnable) TargetMark.Append(Snitch.GetWarningMark(seer, target));

                            if (seer.Is(CustomRoles.Lovers) && target.Is(CustomRoles.Lovers)
                                || seer.Data.IsDead && !seer.Is(CustomRoles.Lovers) && target.Is(CustomRoles.Lovers))
                            {
                                TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Lovers)}>♥</color>");
                            }

                            switch (seer.GetCustomRole())
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
                                    if (Main.ArsonistTimer.TryGetValue(seer.PlayerId, out var ar_kvp) && ar_kvp.PLAYER == target)
                                    {
                                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Arsonist)}>△</color>");
                                    }
                                    break;
                                case CustomRoles.Revolutionist:
                                    if (seer.IsDrawPlayer(target))
                                    {
                                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Revolutionist)}>●</color>");
                                    }
                                    if (Main.RevolutionistTimer.TryGetValue(seer.PlayerId, out var ar_kvp1) && ar_kvp1.PLAYER == target)
                                    {
                                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Revolutionist)}>○</color>");
                                    }
                                    break;
                                case CustomRoles.Farseer:
                                    if (Main.FarseerTimer.TryGetValue(seer.PlayerId, out var ar_kvp2) && ar_kvp2.PLAYER == target)
                                    {
                                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Farseer)}>○</color>");
                                    }
                                    break;
                                case CustomRoles.Analyzer:
                                    if (Analyzer.CurrentTarget.ID == target.PlayerId)
                                    {
                                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Analyzer)}>○</color>");
                                    }
                                    break;
                                case CustomRoles.Puppeteer when Main.PuppeteerList.ContainsValue(seer.PlayerId) && Main.PuppeteerList.ContainsKey(target.PlayerId):
                                    TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Impostor)}>◆</color>");
                                    break;
                            }

                            // Other people's roles and tasks will only be visible if the ghost can see other people's roles and the seer is dead. otherwise it will be empty.
                            string TargetRoleText =
                                (seer.Data.IsDead && Options.GhostCanSeeOtherRoles.GetBool()) ||
                                (seer.Is(CustomRoles.Mimic) && target.Data.IsDead && Options.MimicCanSeeDeadRoles.GetBool()) ||
                                (target.Is(CustomRoles.Gravestone) && target.Data.IsDead) ||
                                (seer.Is(CustomRoles.Lovers) && target.Is(CustomRoles.Lovers) && Options.LoverKnowRoles.GetBool()) ||
                                //(target.Is(CustomRoles.Ntr) && Options.LoverKnowRoles.GetBool()) ||
                                (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoleTypes.Impostor) && Options.ImpKnowAlliesRole.GetBool()) ||
                                (seer.Is(CustomRoles.Madmate) && target.Is(CustomRoleTypes.Impostor) && Options.MadmateKnowWhosImp.GetBool()) ||
                                (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Madmate) && Options.ImpKnowWhosMadmate.GetBool()) ||
                                (seer.Is(CustomRoles.Crewpostor) && target.Is(CustomRoleTypes.Impostor) && Options.CrewpostorKnowsAllies.GetBool()) ||
                                (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Crewpostor) && Options.AlliesKnowCrewpostor.GetBool()) ||
                                (seer.Is(CustomRoles.Madmate) && target.Is(CustomRoles.Madmate) && Options.MadmateKnowWhosMadmate.GetBool()) ||
                                (seer.Is(CustomRoles.Rogue) && target.Is(CustomRoles.Rogue) && Options.RogueKnowEachOther.GetBool() && Options.RogueKnowEachOtherRoles.GetBool()) ||
                                (seer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Sidekick)) ||
                                (seer.Is(CustomRoles.Jackal) && target.Is(CustomRoles.Sidekick)) ||
                                (seer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Jackal)) ||
                                (seer.Is(CustomRoles.Recruit) && target.Is(CustomRoles.Jackal)) ||
                                (target.Is(CustomRoles.Workaholic) && Options.WorkaholicVisibleToEveryone.GetBool()) ||
                                (target.Is(CustomRoles.Doctor) && !target.GetCustomRole().IsEvilAddons() && Options.DoctorVisibleToEveryone.GetBool()) ||
                                (target.Is(CustomRoles.Mayor) && Options.MayorRevealWhenDoneTasks.GetBool() && target.GetPlayerTaskState().IsTaskFinished) ||
                                (seer.Is(CustomRoleTypes.Crewmate) && target.Is(CustomRoles.Marshall) && target.GetPlayerTaskState().IsTaskFinished) ||
                                (Main.PlayerStates[target.PlayerId].deathReason == PlayerState.DeathReason.Vote && Options.SeeEjectedRolesInMeeting.GetBool()) ||
                                Totocalcio.KnowRole(seer, target) ||
                                Romantic.KnowRole(seer, target) ||
                                Lawyer.KnowRole(seer, target) ||
                                EvilDiviner.IsShowTargetRole(seer, target) ||
                                Ritualist.IsShowTargetRole(seer, target) ||
                                Executioner.KnowRole(seer, target) ||
                                Succubus.KnowRole(seer, target) ||
                                CursedSoul.KnowRole(seer, target) ||
                                Admirer.KnowRole(seer, target) ||
                                Amnesiac.KnowRole(seer, target) ||
                                Infectious.KnowRole(seer, target) ||
                                Virus.KnowRole(seer, target) ||
                                Options.CurrentGameMode == CustomGameMode.FFA ||
                                (seer.IsRevealedPlayer(target) && !target.Is(CustomRoles.Trickster)) ||
                                seer.Is(CustomRoles.God) ||
                                target.Is(CustomRoles.GM)
                                ? $"<size={fontSize}>{target.GetDisplayRoleName(seer.PlayerId != target.PlayerId && !seer.Data.IsDead)}{GetProgressText(target)}</size>\r\n" : string.Empty;

                            if (!seer.Data.IsDead && seer.IsRevealedPlayer(target) && target.Is(CustomRoles.Trickster))
                            {
                                TargetRoleText = Farseer.RandomRole[seer.PlayerId];
                                TargetRoleText += Farseer.GetTaskState();
                            }

                            if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
                                TargetRoleText = $"<size={fontSize}>{GetProgressText(target)}</size>\r\n";

                            string TargetPlayerName = target.GetRealName(isForMeeting);

                            switch (seer.GetCustomRole())
                            {
                                case CustomRoles.EvilTracker:
                                    TargetMark.Append(EvilTracker.GetTargetMark(seer, target));
                                    if (isForMeeting && EvilTracker.IsTrackTarget(seer, target) && EvilTracker.CanSeeLastRoomInMeeting)
                                        TargetRoleText = $"<size={fontSize}>{EvilTracker.GetArrowAndLastRoom(seer, target)}</size>\r\n";
                                    break;
                                case CustomRoles.Tracker:
                                    TargetMark.Append(Tracker.GetTargetMark(seer, target));
                                    if (isForMeeting && Tracker.IsTrackTarget(seer, target) && Tracker.CanSeeLastRoomInMeeting)
                                        TargetRoleText = $"<size={fontSize}>{Tracker.GetArrowAndLastRoom(seer, target)}</size>\r\n";
                                    break;
                                case CustomRoles.Psychic when seer.IsAlive() && target.IsRedForPsy(seer) && isForMeeting:
                                    TargetPlayerName = ColorString(GetRoleColor(CustomRoles.Impostor), TargetPlayerName);
                                    break;
                                case CustomRoles.Mafia when !seer.IsAlive() && target.IsAlive():
                                    TargetPlayerName = ColorString(GetRoleColor(CustomRoles.Mafia), target.PlayerId.ToString()) + ' ' + TargetPlayerName;
                                    break;
                                case CustomRoles.Retributionist when !seer.IsAlive() && target.IsAlive():
                                    TargetPlayerName = ColorString(GetRoleColor(CustomRoles.Retributionist), target.PlayerId.ToString()) + ' ' + TargetPlayerName;
                                    break;
                                case CustomRoles.Judge when seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting:
                                    TargetPlayerName = ColorString(GetRoleColor(CustomRoles.Judge), target.PlayerId.ToString()) + ' ' + TargetPlayerName;
                                    break;
                                case CustomRoles.NiceSwapper when seer.IsAlive() && target.IsAlive() && isForMeeting:
                                    TargetPlayerName = ColorString(GetRoleColor(CustomRoles.NiceSwapper), target.PlayerId.ToString()) + ' ' + TargetPlayerName;
                                    break;
                                case CustomRoles.HeadHunter when HeadHunter.Targets.Contains(target.PlayerId) && seer.IsAlive():
                                    TargetPlayerName = "<color=#000000>" + TargetPlayerName + "</size>";
                                    break;
                                case CustomRoles.BountyHunter when BountyHunter.GetTarget(seer) == target.PlayerId && seer.IsAlive():
                                    TargetPlayerName = "<color=#000000>" + TargetPlayerName + "</size>";
                                    break;
                                case CustomRoles.ParityCop when seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting:
                                    TargetPlayerName = ColorString(GetRoleColor(CustomRoles.ParityCop), target.PlayerId.ToString()) + ' ' + TargetPlayerName;
                                    break;
                                case CustomRoles.Councillor when seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting:
                                    TargetPlayerName = ColorString(GetRoleColor(CustomRoles.Councillor), target.PlayerId.ToString()) + ' ' + TargetPlayerName;
                                    break;
                                case CustomRoles.Doomsayer when seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting:
                                    TargetPlayerName = ColorString(GetRoleColor(CustomRoles.Doomsayer), ' ' + target.PlayerId.ToString()) + ' ' + TargetPlayerName;
                                    break;
                                case CustomRoles.Lookout when seer.IsAlive() && target.IsAlive():
                                    TargetPlayerName = ColorString(GetRoleColor(CustomRoles.Lookout), ' ' + target.PlayerId.ToString()) + ' ' + TargetPlayerName;
                                    break;
                            }

                            // Guesser Mode ID
                            if (Options.GuesserMode.GetBool())
                            {
                                //Crewmates
                                if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting && !seer.Is(CustomRoles.Judge) && !seer.Is(CustomRoles.NiceSwapper) && !seer.Is(CustomRoles.ParityCop) && !seer.Is(CustomRoles.Lookout) && Options.CrewmatesCanGuess.GetBool() && seer.GetCustomRole().IsCrewmate())
                                {
                                    TargetPlayerName = ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + ' ' + TargetPlayerName;
                                }
                                else if (seer.Is(CustomRoles.NiceGuesser) && !Options.CrewmatesCanGuess.GetBool())
                                {
                                    if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting)
                                    {
                                        TargetPlayerName = ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + ' ' + TargetPlayerName;
                                    }
                                }

                                //Impostors
                                if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting && !seer.Is(CustomRoles.Councillor) && !seer.Is(CustomRoles.Mafia) && Options.ImpostorsCanGuess.GetBool() && seer.GetCustomRole().IsImpostor())
                                {
                                    TargetPlayerName = ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + ' ' + TargetPlayerName;
                                }
                                else if (seer.Is(CustomRoles.EvilGuesser) && !Options.ImpostorsCanGuess.GetBool())
                                {
                                    if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting)
                                    {
                                        TargetPlayerName = ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + ' ' + TargetPlayerName;
                                    }
                                }

                                // Neutrals
                                if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting && Options.NeutralKillersCanGuess.GetBool() && seer.GetCustomRole().IsNK())
                                {
                                    TargetPlayerName = ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + ' ' + TargetPlayerName;
                                }
                                if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting && Options.PassiveNeutralsCanGuess.GetBool() && seer.GetCustomRole().IsNonNK() && !seer.Is(CustomRoles.Doomsayer))
                                {
                                    TargetPlayerName = ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + ' ' + TargetPlayerName;
                                }
                            }
                            else // Off Guesser Mode ID
                            {
                                if (seer.Is(CustomRoles.NiceGuesser) || seer.Is(CustomRoles.EvilGuesser) || (seer.Is(CustomRoles.Guesser) && !seer.Is(CustomRoles.ParityCop) && !seer.Is(CustomRoles.NiceSwapper) && !seer.Is(CustomRoles.Lookout)))
                                {
                                    if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting)
                                    {
                                        TargetPlayerName = ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + ' ' + TargetPlayerName;
                                    }
                                }
                            }
                            //ターゲットのプレイヤー名の色を書き換えます。
                            TargetPlayerName = TargetPlayerName.ApplyNameColorData(seer, target, isForMeeting);

                            if (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Snitch) && target.Is(CustomRoles.Madmate) && target.GetPlayerTaskState().IsTaskFinished)
                                TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Impostor), "★"));
                            if (seer.Is(CustomRoleTypes.Crewmate) && target.Is(CustomRoles.Marshall) && target.GetPlayerTaskState().IsTaskFinished)
                                TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Marshall), "★"));
                            /*if (seer.Is(CustomRoles.Jackal) && target.Is(CustomRoles.Sidekick))
                                TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Jackal), " ♥"));
                            //   if (seer.Is(CustomRoles.Monarch) && target.Is(CustomRoles.Knighted))
                            // TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Knighted), " 亗"));
                            if (seer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Sidekick) && Options.SidekickKnowOtherSidekick.GetBool())
                                TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Jackal), " ♥")); */

                            TargetMark.Append(Executioner.TargetMark(seer, target));

                            //   TargetMark.Append(Lawyer.TargetMark(seer, target));

                            TargetMark.Append(Gamer.TargetMark(seer, target));

                            if (seer.Is(CustomRoles.Medic) && (Medic.InProtect(target.PlayerId) || Medic.TempMarkProtected == target.PlayerId) && (Medic.WhoCanSeeProtect.GetInt() == 0 || Medic.WhoCanSeeProtect.GetInt() == 1))
                            {
                                TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Medic), " ●"));
                            }
                            else if (seer.Data.IsDead && Medic.InProtect(target.PlayerId) && !seer.Is(CustomRoles.Medic))
                            {
                                TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Medic), " ●"));
                            }

                            TargetMark.Append(Totocalcio.TargetMark(seer, target));
                            TargetMark.Append(Romantic.TargetMark(seer, target));
                            TargetMark.Append(Lawyer.LawyerMark(seer, target));
                            TargetMark.Append(Deathpact.GetDeathpactMark(seer, target));
                            TargetMark.Append(PlagueDoctor.GetMarkOthers(seer, target));

                            //KB目标玩家名字后缀
                            TargetSuffix.Clear();

                            if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
                                TargetSuffix.Append(SoloKombatManager.GetDisplayHealth(target));

                            TargetSuffix.Append(PlagueDoctor.GetLowerTextOthers(seer, target));
                            TargetSuffix.Append(Stealth.GetSuffix(seer, target));

                            string TargetDeathReason = string.Empty;
                            if (seer.KnowDeathReason(target))
                                TargetDeathReason = $"({ColorString(GetRoleColor(CustomRoles.Doctor), GetVitalText(target.PlayerId))})";

                            //Devourer
                            if (Devourer.HideNameOfConsumedPlayer.GetBool() && Devourer.PlayerSkinsCosumed.Any(a => a.Value.Contains(target.PlayerId)) && !CamouflageIsForMeeting)
                                TargetPlayerName = GetString("DevouredName");

                            // Camouflage
                            if (((IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool() && (Main.NormalOptions.MapId != 5 || !Options.CommsCamouflageDisableOnFungle.GetBool())) || Camouflager.IsActive) && !CamouflageIsForMeeting)
                                TargetPlayerName = $"<size=0>{TargetPlayerName}</size>";

                            //全てのテキストを合成します。
                            string TargetName = $"{TargetRoleText}{TargetPlayerName}{TargetDeathReason}{TargetMark}";
                            TargetName += TargetSuffix.ToString() == string.Empty ? string.Empty : ("\r\n" + TargetSuffix.ToString());

                            target.RpcSetNamePrivate(TargetName, true, seer, force: NoCache);
                        }

                        Logger.Info("NotifyRoles-Loop2-" + target.GetNameWithRole().RemoveHtmlTags() + ":END", "NotifyRoles");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error for {seer.GetNameWithRole()}: {ex}", "NotifyRoles");
            }

            Logger.Info("NotifyRoles-Loop1-" + seer.GetNameWithRole().RemoveHtmlTags() + ":END", "NotifyRoles");

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
        GameOptionsSender.SendAllGameOptions();
    }
    public static void AfterMeetingTasks()
    {
        if (Options.DiseasedCDReset.GetBool())
        {
            var array = Main.KilledDiseased.Keys.ToArray();
            foreach (var pid in array)
            {
                Main.KilledDiseased[pid] = 0;
                GetPlayerById(pid).ResetKillCooldown();
            }
            Main.KilledDiseased.Clear();
        }
        if (Options.AntidoteCDReset.GetBool())
        {
            var array = Main.KilledAntidote.Keys.ToArray();
            foreach (var pid in array)
            {
                Main.KilledAntidote[pid] = 0;
                GetPlayerById(pid).ResetKillCooldown();
            }
            Main.KilledAntidote.Clear();
        }

        if (Blackmailer.IsEnable) Blackmailer.ForBlackmailer.Clear();
        if (Glitch.IsEnable) Glitch.AfterMeetingTasks();
        if (Swooper.IsEnable) Swooper.AfterMeetingTasks();
        if (Wraith.IsEnable) Wraith.AfterMeetingTasks();
        if (Werewolf.IsEnable) Werewolf.AfterMeetingTasks();
        if (Chameleon.IsEnable) Chameleon.AfterMeetingTasks();
        if (NiceEraser.IsEnable) NiceEraser.AfterMeetingTasks();
        if (Eraser.IsEnable) Eraser.AfterMeetingTasks();
        if (Cleanser.IsEnable) Cleanser.AfterMeetingTasks();
        if (BountyHunter.IsEnable) BountyHunter.AfterMeetingTasks();
        if (EvilTracker.IsEnable) EvilTracker.AfterMeetingTasks();
        if (SerialKiller.IsEnable()) SerialKiller.AfterMeetingTasks();
        if (Spiritualist.IsEnable) Spiritualist.AfterMeetingTasks();
        if (Jailor.IsEnable) Jailor.AfterMeetingTasks();
        if (PlagueDoctor.IsEnable) PlagueDoctor.AfterMeetingTasks();
        if (Penguin.IsEnable) Penguin.AfterMeetingTasks();
        if (Chronomancer.IsEnable) Chronomancer.OnReportDeadBody();
        if (Benefactor.IsEnable) Benefactor.AfterMeetingTasks();
        if (Vulture.IsEnable) Vulture.AfterMeetingTasks();
        //if (Baker.IsEnable()) Baker.AfterMeetingTasks();
        if (CopyCat.IsEnable()) CopyCat.AfterMeetingTasks();
        //Pirate.AfterMeetingTask();
        Damocles.AfterMeetingTasks();

        if (Options.UsePets.GetBool())
        {
            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                switch (pc.GetCustomRole())
                {
                    case CustomRoles.Doormaster:
                        Main.DoormasterCD.TryAdd(pc.PlayerId, GetTimeStamp());
                        break;
                    case CustomRoles.Tether:
                        Main.TetherCD.TryAdd(pc.PlayerId, GetTimeStamp());
                        break;
                    case CustomRoles.Mayor:
                        Main.MayorCD.TryAdd(pc.PlayerId, GetTimeStamp());
                        break;
                    case CustomRoles.Paranoia:
                        Main.ParanoiaCD.TryAdd(pc.PlayerId, GetTimeStamp());
                        break;
                    case CustomRoles.Grenadier:
                        Main.GrenadierCD.TryAdd(pc.PlayerId, GetTimeStamp() - Options.GrenadierSkillDuration.GetInt());
                        break;
                    case CustomRoles.Lighter:
                        Main.LighterCD.TryAdd(pc.PlayerId, GetTimeStamp() - Options.LighterSkillDuration.GetInt());
                        break;
                    case CustomRoles.SecurityGuard:
                        Main.SecurityGuardCD.TryAdd(pc.PlayerId, GetTimeStamp() - Options.SecurityGuardSkillDuration.GetInt());
                        break;
                    case CustomRoles.DovesOfNeace:
                        Main.DovesOfNeaceCD.TryAdd(pc.PlayerId, GetTimeStamp());
                        break;
                    case CustomRoles.Alchemist:
                        Main.AlchemistCD.TryAdd(pc.PlayerId, GetTimeStamp());
                        break;
                    case CustomRoles.TimeMaster:
                        Main.TimeMasterCD.TryAdd(pc.PlayerId, GetTimeStamp() - Options.TimeMasterSkillDuration.GetInt());
                        break;
                    case CustomRoles.Veteran:
                        Main.VeteranCD.TryAdd(pc.PlayerId, GetTimeStamp() - Options.VeteranSkillDuration.GetInt());
                        break;
                    case CustomRoles.NiceHacker:
                        Main.HackerCD.TryAdd(pc.PlayerId, GetTimeStamp());
                        break;
                    case CustomRoles.CameraMan:
                        Main.CameraManCD.TryAdd(pc.PlayerId, GetTimeStamp());
                        break;
                    case CustomRoles.Sniper:
                        Main.SniperCD.TryAdd(pc.PlayerId, GetTimeStamp());
                        break;
                    case CustomRoles.Assassin:
                        Main.AssassinCD.TryAdd(pc.PlayerId, GetTimeStamp());
                        break;
                    case CustomRoles.Undertaker:
                        Main.UndertakerCD.TryAdd(pc.PlayerId, GetTimeStamp());
                        break;
                    case CustomRoles.Bomber:
                        Main.BomberCD.TryAdd(pc.PlayerId, GetTimeStamp());
                        break;
                    case CustomRoles.Nuker:
                        Main.NukerCD.TryAdd(pc.PlayerId, GetTimeStamp());
                        break;
                    case CustomRoles.Sapper:
                        Main.SapperCD.TryAdd(pc.PlayerId, GetTimeStamp());
                        break;
                    case CustomRoles.Druid:
                        Main.DruidCD.TryAdd(pc.PlayerId, GetTimeStamp());
                        break;
                    case CustomRoles.Miner:
                        Main.MinerCD.TryAdd(pc.PlayerId, GetTimeStamp());
                        break;
                    case CustomRoles.Escapee:
                        Main.EscapeeCD.TryAdd(pc.PlayerId, GetTimeStamp());
                        break;
                    case CustomRoles.QuickShooter:
                        Main.QuickShooterCD.TryAdd(pc.PlayerId, GetTimeStamp());
                        break;
                    case CustomRoles.Disperser:
                        Main.DisperserCD.TryAdd(pc.PlayerId, GetTimeStamp());
                        break;
                    case CustomRoles.Twister:
                        Main.TwisterCD.TryAdd(pc.PlayerId, GetTimeStamp());
                        break;
                }
            }
        }

        if (Options.AirshipVariableElectrical.GetBool())
            AirshipElectricalDoors.Initialize();

        DoorsReset.ResetDoors();

        if ((MapNames)Main.NormalOptions.MapId == MapNames.Airship && AmongUsClient.Instance.AmHost && PlayerControl.LocalPlayer.Is(CustomRoles.GM))
        {
            _ = new LateTask(() => { PlayerControl.LocalPlayer.TP(new(15.5f, 0.0f)); }, 2f, "GM Auto-TP Failsafe"); // TP to Main Hall
        }
    }
    public static void AfterPlayerDeathTasks(PlayerControl target, bool onMeeting = false)
    {
        switch (target.GetCustomRole())
        {
            case CustomRoles.Terrorist:
                Logger.Info(target?.Data?.PlayerName + "はTerroristだった", "MurderPlayer");
                CheckTerroristWin(target.Data);
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
                PlagueDoctor.OnPDdeath(target.GetRealKiller());
                break;
            case CustomRoles.CyberStar:
                if (GameStates.IsMeeting)
                {
                    foreach (PlayerControl pc in Main.AllPlayerControls)
                    {
                        if (!Options.ImpKnowCyberStarDead.GetBool() && pc.GetCustomRole().IsImpostor()
                            || !Options.NeutralKnowCyberStarDead.GetBool() && pc.GetCustomRole().IsNeutral())
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
            case CustomRoles.Romantic:
                Romantic.isRomanticAlive = false;
                break;
            case CustomRoles.Devourer:
                Devourer.OnDevourerDied(target.PlayerId);
                break;
        }

        if (Romantic.BetPlayer.ContainsValue(target.PlayerId))
            Romantic.ChangeRole(target.PlayerId);

        if (Executioner.Target.ContainsValue(target.PlayerId))
            Executioner.ChangeRoleByTarget(target);
        if (Lawyer.Target.ContainsValue(target.PlayerId))
            Lawyer.ChangeRoleByTarget(target);
        if (Postman.Target == target.PlayerId)
            Postman.OnTargetDeath();
        if (Hitman.targetId == target.PlayerId)
            Hitman.targetId = byte.MaxValue;

        FixedUpdatePatch.LoversSuicide(target.PlayerId, onMeeting);
    }
    public static void ChangeInt(ref int ChangeTo, int input, int max)
    {
        var tmp = ChangeTo * 10;
        tmp += input;
        ChangeTo = Math.Clamp(tmp, 0, max);
    }
    public static void CountAlivePlayers(bool sendLog = false)
    {
        int AliveImpostorCount = Main.AllAlivePlayerControls.Count(pc => pc.Is(CustomRoleTypes.Impostor));
        if (Main.AliveImpostorCount != AliveImpostorCount)
        {
            Logger.Info("Number of living Impostors:" + AliveImpostorCount, "CountAliveImpostors");
            Main.AliveImpostorCount = AliveImpostorCount;
            LastImpostor.SetSubRole();
        }

        if (sendLog)
        {
            var sb = new StringBuilder(100);
            if (Options.CurrentGameMode != CustomGameMode.FFA)
            {
                foreach (var countTypes in Enum.GetValues(typeof(CountTypes)).Cast<CountTypes>())
                {
                    var playersCount = PlayersCount(countTypes);
                    if (playersCount == 0) continue;
                    sb.Append($"{countTypes}: {AlivePlayersCount(countTypes)}/{playersCount}, ");
                }
            }
            sb.Append($"All: {AllAlivePlayersCount}/{AllPlayersCount}");
            Logger.Info(sb.ToString(), "CountAlivePlayers");
        }
    }
    public static string GetVoteName(byte num)
    {
        string name = "invalid";
        var player = GetPlayerById(num);
        if (num < 15 && player != null) name = player?.GetNameWithRole().RemoveHtmlTags();
        if (num == 253) name = "Skip";
        if (num == 254) name = "None";
        if (num == 255) name = "Dead";
        return name;
    }
    public static string PadRightV2(this object text, int num)
    {
        int bc = 0;
        var t = text.ToString();
        for (int i = 0; i < t.Length; i++)
        {
            char c = t[i];
            bc += Encoding.GetEncoding("UTF-8").GetByteCount(c.ToString()) == 1 ? 1 : 2;
        }

        return t?.PadRight(Mathf.Max(num - (bc - t.Length), 0));
    }
    public static void DumpLog()
    {
        string f = $"{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)}/TOHE-logs/";
        string t = DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss");
        string filename = $"{f}TOHE-v{Main.PluginVersion}-{t}.log";
        if (!Directory.Exists(f)) Directory.CreateDirectory(f);
        FileInfo file = new(@$"{Environment.CurrentDirectory}/BepInEx/LogOutput.log");
        file.CopyTo(@filename);
        if (PlayerControl.LocalPlayer != null)
            HudManager.Instance?.Chat?.AddChat(PlayerControl.LocalPlayer, string.Format(GetString("Message.DumpfileSaved"), $"TOHE - v{Main.PluginVersion}-{t}.log"));
        System.Diagnostics.ProcessStartInfo psi = new("Explorer.exe")
        { Arguments = "/e,/select," + @filename.Replace("/", "\\") };
        System.Diagnostics.Process.Start(psi);
    }
    public static (int, int) GetDousedPlayerCount(byte playerId)
    {
        int doused = 0, all = 0;
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (pc.PlayerId == playerId)
                continue;
            all++;
            if (Main.isDoused.TryGetValue((playerId, pc.PlayerId), out var isDoused) && isDoused)
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
        foreach (var pc in Main.AllPlayerControls.Where(pc => Main.isDraw.TryGetValue((playerId, pc.PlayerId), out var isDraw) && isDraw).ToArray())
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
        var taskState = Main.PlayerStates?[id].GetTaskState();
        string TaskCount;
        if (taskState.hasTasks)
        {
            Color TextColor;
            var info = GetPlayerInfoById(id);
            var TaskCompleteColor = HasTasks(info) ? Color.green : Color.cyan; //タスク完了後の色
            var NonCompleteColor = HasTasks(info) ? Color.yellow : Color.white; //カウントされない人外は白色

            if (Workhorse.IsThisRole(id))
                NonCompleteColor = Workhorse.RoleColor;

            var NormalColor = taskState.IsTaskFinished ? TaskCompleteColor : NonCompleteColor;
            if (Main.PlayerStates.TryGetValue(id, out var ps) && ps.MainRole == CustomRoles.Crewpostor)
                NormalColor = Color.red;

            TextColor = NormalColor;
            string Completed = $"{taskState.CompletedTasksCount}";
            TaskCount = ColorString(TextColor, $" ({Completed}/{taskState.AllTasksCount})");
        }
        else { TaskCount = string.Empty; }
        string summary = $"{ColorString(Main.PlayerColors[id], name)} - {GetDisplayRoleName(id, true)}{TaskCount}{GetKillCountText(id)} ({GetVitalText(id, true)})";
        if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
        {
            if (TranslationController.Instance.currentLanguage.languageID is SupportedLangs.SChinese or SupportedLangs.TChinese)
                summary = $"{GetProgressText(id)}\t<pos=22%>{ColorString(Main.PlayerColors[id], name)}</pos>";
            else summary = $"{ColorString(Main.PlayerColors[id], name)}<pos=30%>{GetProgressText(id)}</pos>";
            if (GetProgressText(id).Trim() == string.Empty) return string.Empty;
        }
        if (Options.CurrentGameMode == CustomGameMode.FFA)
        {
            summary = $"{ColorString(Main.PlayerColors[id], name)} {GetKillCountText(id, ffa: true)}";
        }
        return check && GetDisplayRoleName(id, true).RemoveHtmlTags().Contains("INVALID:NotAssigned")
            ? "INVALID"
            : disableColor ? summary.RemoveHtmlTags() : summary;
    }
    public static string RemoveHtmlTagsTemplate(this string str) => Regex.Replace(str, string.Empty, string.Empty);
    public static string RemoveHtmlTags(this string str) => Regex.Replace(str, "<[^>]*?>", string.Empty);
    public static bool CanMafiaKill()
    {
        if (Main.PlayerStates == null) return false;
        //マフィアを除いた生きているインポスターの人数  Number of Living Impostors excluding mafia
        int LivingImpostorsNum = 0;
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            var role = pc.GetCustomRole();
            if (role != CustomRoles.Mafia && role.IsImpostor()) LivingImpostorsNum++;
        }

        return LivingImpostorsNum <= 0;
    }
    public static void FlashColor(Color color, float duration = 1f)
    {
        var hud = DestroyableSingleton<HudManager>.Instance;
        if (hud.FullScreen == null) return;
        var obj = hud.transform.FindChild("FlashColor_FullScreen")?.gameObject;
        if (obj == null)
        {
            obj = UnityEngine.Object.Instantiate(hud.FullScreen.gameObject, hud.transform);
            obj.name = "FlashColor_FullScreen";
        }
        hud.StartCoroutine(Effects.Lerp(duration, new Action<float>((t) =>
        {
            obj.SetActive(t != 1f);
            obj.GetComponent<SpriteRenderer>().color = new(color.r, color.g, color.b, Mathf.Clamp01((-2f * Mathf.Abs(t - 0.5f) + 1) * color.a / 2)); //アルファ値を0→目標→0に変化させる
        })));
    }

    public static Dictionary<string, Sprite> CachedSprites = [];
    public static Sprite LoadSprite(string path, float pixelsPerUnit = 1f)
    {
        try
        {
            if (CachedSprites.TryGetValue(path + pixelsPerUnit, out var sprite)) return sprite;
            Texture2D texture = LoadTextureFromResources(path);
            sprite = Sprite.Create(texture, new(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
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
            stream.CopyTo(ms);
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
        return new Color(R, G, B, color.a);
    }

    /// <summary>
    /// 乱数の簡易的なヒストグラムを取得する関数
    /// <params name="nums">生成した乱数を格納したint配列</params>
    /// <params name="scale">ヒストグラムの倍率 大量の乱数を扱う場合、この値を下げることをお勧めします。</params>
    /// </summary>
    public static string WriteRandomHistgram(int[] nums, float scale = 1.0f)
    {
        int[] countData = new int[nums.Max() + 1];
        foreach (int num in nums)
        {
            if (0 <= num) countData[num]++;
        }
        StringBuilder sb = new();
        for (int i = 0; i < countData.Length; i++)
        {
            // 倍率適用
            countData[i] = (int)(countData[i] * scale);

            // 行タイトル
            sb.AppendFormat("{0:D2}", i).Append(" : ");

            // ヒストグラム部分
            for (int j = 0; j < countData[i]; j++)
                sb.Append('|');

            // 改行
            sb.Append('\n');
        }

        // その他の情報
        sb.Append("最大数 - 最小数: ").Append(countData.Max() - countData.Min());

        return sb.ToString();
    }

    public static void SetChatVisible()
    {
        if (!GameStates.IsInGame) return;
        MeetingHud.Instance = UnityEngine.Object.Instantiate(HudManager.Instance.MeetingPrefab);
        MeetingHud.Instance.ServerStart(PlayerControl.LocalPlayer.PlayerId);
        AmongUsClient.Instance.Spawn(MeetingHud.Instance, -2, SpawnFlags.None);
        MeetingHud.Instance.RpcClose();
    }

    public static bool TryCast<T>(this Il2CppObjectBase obj, out T casted)
    where T : Il2CppObjectBase
    {
        casted = obj.TryCast<T>();
        return casted != null;
    }
    public static int AllPlayersCount => Main.PlayerStates.Values.Count(state => state.countTypes != CountTypes.OutOfGame);
    public static int AllAlivePlayersCount => Main.AllAlivePlayerControls.Count(pc => !pc.Is(CountTypes.OutOfGame));
    public static bool IsAllAlive => Main.PlayerStates.Values.All(state => state.countTypes == CountTypes.OutOfGame || !state.IsDead);
    public static int PlayersCount(CountTypes countTypes) => Main.PlayerStates.Values.Count(state => state.countTypes == countTypes);
    public static int AlivePlayersCount(CountTypes countTypes) => Main.AllAlivePlayerControls.Count(pc => pc.Is(countTypes));
}