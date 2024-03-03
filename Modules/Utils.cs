using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AmongUs.Data;
using AmongUs.GameOptions;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes;
using InnerNet;
using TOHE.Modules;
using TOHE.Patches;
using TOHE.Roles.AddOns.Common;
using TOHE.Roles.AddOns.Crewmate;
using TOHE.Roles.AddOns.Impostor;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Translator;
using Object = UnityEngine.Object;

namespace TOHE;

public static class Utils
{
    private static readonly DateTime timeStampStartTime = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static long GetTimeStamp(DateTime? dateTime = null) => (long)((dateTime ?? DateTime.Now).ToUniversalTime() - timeStampStartTime).TotalSeconds;
    public static long TimeStamp => GetTimeStamp();

    public static void ErrorEnd(string text)
    {
        if (AmongUsClient.Instance.AmHost)
        {
            Logger.Fatal($"{text} error, triggering anti-black screen measures", "Anti-Blackout");
            ChatUpdatePatch.DoBlockChat = true;
            Main.OverrideWelcomeMsg = GetString("AntiBlackOutNotifyInLobby");
            _ = new LateTask(() => { Logger.SendInGame(GetString("AntiBlackOutLoggerSendInGame") /*, true*/); }, 3f, "Anti-Black Msg SendInGame");
            _ = new LateTask(() =>
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
                _ = new LateTask(() => { Logger.SendInGame(GetString("AntiBlackOutRequestHostToForceEnd") /*, true*/); }, 3f, "Anti-Black Msg SendInGame");
            }
            else
            {
                _ = new LateTask(() => { Logger.SendInGame(GetString("AntiBlackOutHostRejectForceEnd") /*, true*/); }, 3f, "Anti-Black Msg SendInGame");
                _ = new LateTask(() =>
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
        if (pc.inVent || pc.inMovingPlat || pc.onLadder || !pc.IsAlive() || pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation() || pc.MyPhysics.Animations.IsPlayingEnterVentAnimation())
        {
            if (log) Logger.Warn($"Target ({pc.GetNameWithRole().RemoveHtmlTags()}) is in an un-teleportable state - Teleporting canceled", "TP");
            return false;
        }

        var numHost = (ushort)(nt.lastSequenceId + 6);
        var numLocalClient = (ushort)(nt.lastSequenceId + 48);
        var numGlobal = (ushort)(nt.lastSequenceId + 100);

        // Host side
        if (AmongUsClient.Instance.AmHost)
        {
            nt.SnapTo(location, numHost);
        }

        if (PlayerControl.LocalPlayer.PlayerId != pc.PlayerId)
        {
            // Local Teleport For Client
            MessageWriter localMessageWriter = AmongUsClient.Instance.StartRpcImmediately(nt.NetId, (byte)RpcCalls.SnapTo, SendOption.None, pc.GetClientId());
            NetHelpers.WriteVector2(location, localMessageWriter);
            localMessageWriter.Write(numLocalClient);
            AmongUsClient.Instance.FinishRpcImmediately(localMessageWriter);
        }

        // Global Teleport
        MessageWriter globalMessageWriter = AmongUsClient.Instance.StartRpcImmediately(nt.NetId, (byte)RpcCalls.SnapTo, SendOption.None);
        NetHelpers.WriteVector2(location, globalMessageWriter);
        globalMessageWriter.Write(numGlobal);
        AmongUsClient.Instance.FinishRpcImmediately(globalMessageWriter);

        if (log) Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} => {location}", "TP");
        return true;
    }

    // ReSharper disable once InconsistentNaming
    public static bool TPtoRndVent(CustomNetworkTransform nt, bool log = true)
    {
        var vents = Object.FindObjectsOfType<Vent>();
        var vent = vents[IRandom.Instance.Next(0, vents.Count)];

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
        //Logger.Info($"SystemTypes:{type}", "IsActive");
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

    public static bool DoRPC => AmongUsClient.Instance.AmHost && Main.AllPlayerControls.Any(x => x.IsModClient() && x.PlayerId != 0);

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

        opt.SetFloat(
            FloatOptionNames.ImpostorLightMod,
            opt.GetFloat(FloatOptionNames.CrewLightMod));
        if (IsActive(SystemTypes.Electrical))
        {
            opt.SetFloat(
                FloatOptionNames.ImpostorLightMod,
                opt.GetFloat(FloatOptionNames.ImpostorLightMod) / 5);
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
        foreach (PlayerControl seer in Main.AllPlayerControls)
        {
            if (KillFlashCheck(killer, target, seer))
            {
                seer.KillFlash();
                continue;
            }

            if (target.Is(CustomRoles.CyberStar))
            {
                if (!Options.ImpKnowCyberStarDead.GetBool() && seer.GetCustomRole().IsImpostor()) continue;
                if (!Options.NeutralKnowCyberStarDead.GetBool() && seer.GetCustomRole().IsNeutral()) continue;
                seer.KillFlash();
                seer.Notify(ColorString(GetRoleColor(CustomRoles.CyberStar), GetString("OnCyberStarDead")));
            }
        }

        if (target.Is(CustomRoles.CyberStar) && !Main.CyberStarDead.Contains(target.PlayerId)) Main.CyberStarDead.Add(target.PlayerId);
        if (target.Is(CustomRoles.Demolitionist) && !Main.DemolitionistDead.Contains(target.PlayerId)) Main.DemolitionistDead.Add(target.PlayerId);
        if (target.Is(CustomRoles.Demolitionist))
        {
            killer.Notify(ColorString(GetRoleColor(CustomRoles.Demolitionist), GetString("OnDemolitionistDead")));
            killer.KillFlash();
            _ = new LateTask(() =>
            {
                if (!killer.inVent && (killer.PlayerId != target.PlayerId))
                {
                    if ((Options.DemolitionistKillerDiesOnMeetingCall.GetBool() || GameStates.IsInTask) && killer.IsAlive())
                    {
                        killer.Suicide(PlayerState.DeathReason.Demolished, target);
                        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    }
                }
                else
                {
                    if (killer.IsModClient()) RPC.PlaySoundRPC(killer.PlayerId, Sounds.TaskComplete);
                    else killer.RpcGuardAndKill(killer);
                    killer.SetKillCooldown(Main.AllPlayerKillCooldown[killer.PlayerId] - Options.DemolitionistVentTime.GetFloat());
                }
            }, Options.DemolitionistVentTime.GetFloat() + 0.5f);
        }
    }

    public static bool KillFlashCheck(PlayerControl killer, PlayerControl target, PlayerControl seer)
    {
        if (seer.Is(CustomRoles.GM) || seer.Is(CustomRoles.Seer)) return true;
        if (seer.Data.IsDead || killer == seer || target == seer) return false;
        return seer.Is(CustomRoles.EvilTracker) && EvilTracker.KillFlashCheck(killer, target);
    }

    public static void KillFlash(this PlayerControl player)
    {
        //Kill flash (blackout + reactor flash) processing

        var systemtypes = (MapNames)Main.NormalOptions.MapId switch
        {
            MapNames.Polus => SystemTypes.Laboratory,
            MapNames.Airship => SystemTypes.HeliSabotage,
            _ => SystemTypes.Reactor,
        };
        bool ReactorCheck = IsActive(systemtypes); //Checking whether the reactor sabotage is active

        var Duration = Options.KillFlashDuration.GetFloat();
        if (ReactorCheck) Duration += 0.2f; // Extend blackout during reactor

        // Execution
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
        else if (!ReactorCheck) player.ReactorFlash(); // Reactor flash

        player.MarkDirtySettings();
        _ = new LateTask(() =>
        {
            Main.PlayerStates[player.PlayerId].IsBlackOut = false; // Cancel blackout
            player.MarkDirtySettings();
        }, Duration, "RemoveKillFlash");
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
        string mode = GetString($"Rate{role.GetMode()}").RemoveHtmlTags();
        //string mode = role.GetMode() switch
        //{
        //    0 => GetString("RoleOffNoColor"),
        //    1 => GetString("RoleRateNoColor"),
        //    _ => GetString("RoleOnNoColor")
        //};
        return parentheses ? $"({mode})" : mode;
    }

    public static string GetDeathReason(PlayerState.DeathReason status)
    {
        return GetString("DeathReason." + Enum.GetName(typeof(PlayerState.DeathReason), status));
    }

    public static Color GetRoleColor(CustomRoles role)
    {
        var hexColor = Main.roleColors.GetValueOrDefault(role, "#ffffff");
        _ = ColorUtility.TryParseHtmlString(hexColor, out Color c);
        return c;
    }

    public static string GetRoleColorCode(CustomRoles role)
    {
        var hexColor = Main.roleColors.GetValueOrDefault(role, "#ffffff");
        return hexColor;
    }

    public static (string, Color) GetRoleText(byte seerId, byte targetId, bool pure = false)
    {
        var seerMainRole = Main.PlayerStates[seerId].MainRole;
        var seerSubRoles = Main.PlayerStates[seerId].SubRoles;

        var targetMainRole = Main.PlayerStates[targetId].MainRole;
        var targetSubRoles = Main.PlayerStates[targetId].SubRoles;

        var self = seerId == targetId || Main.PlayerStates[seerId].IsDead;

        string RoleText = GetRoleName(targetMainRole);
        Color RoleColor = GetRoleColor(targetMainRole);

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
                    //Logger.Fatal("This is concerning....", "Utils.GetRoleText");
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

    public static MessageWriter CreateCustomRoleRPC(CustomRPC rpc) => AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)rpc, SendOption.Reliable);
    public static void EndRPC(MessageWriter writer) => AmongUsClient.Instance.FinishRpcImmediately(writer);

    public static void IncreaseAbilityUseLimitOnKill(PlayerControl killer)
    {
        if (Main.PlayerStates[killer.PlayerId].Role is Mafioso { IsEnable: true } mo) mo.OnMurder(killer, null);
        var add = killer.GetCustomRole() switch
        {
            CustomRoles.Hacker => Hacker.HackerAbilityUseGainWithEachKill.GetFloat(),
            CustomRoles.Camouflager => Camouflager.CamoAbilityUseGainWithEachKill.GetFloat(),
            CustomRoles.Councillor => Councillor.CouncillorAbilityUseGainWithEachKill.GetFloat(),
            CustomRoles.Dazzler => Dazzler.DazzlerAbilityUseGainWithEachKill.GetFloat(),
            CustomRoles.Disperser => Disperser.DisperserAbilityUseGainWithEachKill.GetFloat(),
            CustomRoles.EvilDiviner => EvilDiviner.EDAbilityUseGainWithEachKill.GetFloat(),
            CustomRoles.Swooper => Swooper.SwooperAbilityUseGainWithEachKill.GetFloat(),
            CustomRoles.Hangman => Hangman.HangmanAbilityUseGainWithEachKill.GetFloat(),
            CustomRoles.Twister => Twister.TwisterAbilityUseGainWithEachKill.GetFloat(),
            CustomRoles.Kamikaze => Kamikaze.KamikazeAbilityUseGainWithEachKill.GetFloat(),
            _ => float.MaxValue,
        };
        killer.RpcIncreaseAbilityUseLimitBy(add);
    }

    public static void ThrowException(Exception ex)
    {
        try
        {
            StackTrace st = new(1, true);
            StackFrame[] stFrames = st.GetFrames();

            StackFrame firstFrame = stFrames.FirstOrDefault();

            var sb = new StringBuilder();
            sb.Append($"Exception: {ex.Message} ----");

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

                sb.Append($",      at {callerClassName}.{callerMethodName}");
            }

            Logger.Error(sb.ToString(), firstFrame?.GetMethod()?.ToString());
        }
        catch
        {
        }
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
        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.SoloKombat: return false;
            case CustomGameMode.FFA: return false;
            case CustomGameMode.MoveAndStop: return true;
            case CustomGameMode.HotPotato: return false;
        }

        //if (p.IsDead && Options.GhostIgnoreTasks.GetBool()) hasTasks = false;
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
            case CustomRoles.SoulHunter:
            case CustomRoles.Enderman:
            case CustomRoles.Mycologist:
            case CustomRoles.Bubble:
            case CustomRoles.Hookshot:
            case CustomRoles.Sprayer:
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
            //case CustomRoles.Pirate:
            //   case CustomRoles.Baker:
            //case CustomRoles.Famine:
            //case CustomRoles.NWitch:
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
            //      case CustomRoles.Chameleon:
            case CustomRoles.Juggernaut:
            //case CustomRoles.Reverie:
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
            //case CustomRoles.CursedSoul:
            case CustomRoles.Amnesiac:
            case CustomRoles.Monarch when !Options.UsePets.GetBool() || !Monarch.UsePet.GetBool():
            case CustomRoles.Deputy when !Options.UsePets.GetBool() || !Deputy.UsePet.GetBool():
            case CustomRoles.Virus:
            case CustomRoles.Farseer when !Options.UsePets.GetBool() || !Farseer.UsePet.GetBool():
            //case CustomRoles.Counterfeiter:
            case CustomRoles.Aid when !Options.UsePets.GetBool() || !Aid.UsePet.GetBool():
            case CustomRoles.Escort when !Options.UsePets.GetBool() || !Escort.UsePet.GetBool():
            case CustomRoles.DonutDelivery when !Options.UsePets.GetBool() || !DonutDelivery.UsePet.GetBool():
            case CustomRoles.Gaulois when !Options.UsePets.GetBool() || !Gaulois.UsePet.GetBool():
            case CustomRoles.Analyzer when !Options.UsePets.GetBool() || !Analyzer.UsePet.GetBool():
            case CustomRoles.Witness when !Options.UsePets.GetBool() || !Options.WitnessUsePet.GetBool():
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

        foreach (CustomRoles subRole in States.SubRoles)
        {
            switch (subRole)
            {
                case CustomRoles.Madmate:
                case CustomRoles.Charmed:
                case CustomRoles.Recruit:
                case CustomRoles.Egoist:
                case CustomRoles.EvilSpirit:
                case CustomRoles.Contagious:
                case CustomRoles.Rascal:
                    //ラバーズはタスクを勝利用にカウントしない
                    hasTasks &= !ForRecompute;
                    break;
            }
        }

        if (CopyCat.playerIdList.Contains(p.PlayerId) && ForRecompute && (!Options.UsePets.GetBool() || CopyCat.UsePet.GetBool())) hasTasks = false;

        if (!hasTasks && role.UsesPetInsteadOfKill()) hasTasks = true;

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
                   //(pc.Is(CustomRoles.Retributionist) && !Options.RetributionistCanBeMadmate.GetBool()) ||
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
        bool result = __instance.AmOwner || Options.CurrentGameMode is CustomGameMode.FFA or CustomGameMode.SoloKombat or CustomGameMode.MoveAndStop or CustomGameMode.HotPotato || Main.VisibleTasksCount && PlayerControl.LocalPlayer.Data.IsDead && Options.GhostCanSeeOtherRoles.GetBool() || PlayerControl.LocalPlayer.Is(CustomRoles.Mimic) && Main.VisibleTasksCount && __instance.Data.IsDead && Options.MimicCanSeeDeadRoles.GetBool();

        switch (__instance.GetCustomRole())
        {
            case CustomRoles.Crewpostor when PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && Options.CrewpostorKnowsAllies.GetBool():
            case CustomRoles.Jackal when PlayerControl.LocalPlayer.Is(CustomRoles.Sidekick):
            case CustomRoles.Jackal when PlayerControl.LocalPlayer.Is(CustomRoles.Recruit):
            case CustomRoles.Workaholic when Options.WorkaholicVisibleToEveryone.GetBool():
            case CustomRoles.Doctor when !__instance.GetCustomRole().IsEvilAddons() && Options.DoctorVisibleToEveryone.GetBool():
            case CustomRoles.Mayor when Options.MayorRevealWhenDoneTasks.GetBool() && __instance.GetTaskState().IsTaskFinished:
            case CustomRoles.Marshall when PlayerControl.LocalPlayer.Is(CustomRoleTypes.Crewmate) && __instance.GetTaskState().IsTaskFinished:
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
        if (Executioner.KnowRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (Succubus.KnowRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (Necromancer.KnowRole(PlayerControl.LocalPlayer, __instance)) result = true;
        //if (CursedSoul.KnowRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (Amnesiac.KnowRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (Virus.KnowRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (PlayerControl.LocalPlayer.IsRevealedPlayer(__instance)) result = true;
        if (PlayerControl.LocalPlayer.Is(CustomRoles.God)) result = true;
        if (PlayerControl.LocalPlayer.Is(CustomRoles.GM)) result = true;
        if (Totocalcio.KnowRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (Lawyer.KnowRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (EvilDiviner.IsShowTargetRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (Executioner.KnowRole(PlayerControl.LocalPlayer, __instance)) result = true;
        if (Main.GodMode.Value) result = true;

        return result;
    }

    public static void GetPetCDSuffix(PlayerControl pc, ref StringBuilder sb)
    {
        if (Options.UsePets.GetBool() && Main.AbilityCD.TryGetValue(pc.PlayerId, out var time) && !pc.IsModClient())
            sb.Append(string.Format(GetString("CDPT"), time.TOTALCD - (TimeStamp - time.START_TIMESTAMP) + 1));
    }

    public static string GetFormattedRoomName(string roomName) => roomName == "Outside" ? "<#00ffa5>Outside</color>" : $"<#ffffff>In</color> <#00ffa5>{roomName}</color>";
    public static string GetFormattedVectorText(Vector2 pos) => $"<#777777>(at {pos.ToString().Replace("(", string.Empty).Replace(")", string.Empty)})</color>";

    public static string GetProgressText(PlayerControl pc)
    {
        if (!Main.playerVersion.ContainsKey(0)) return string.Empty; //ホストがMODを入れていなければ未記入を返す
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
        if (!Main.playerVersion.ContainsKey(0)) return string.Empty; //ホストがMODを入れていなければ未記入を返す
        if (Options.CurrentGameMode == CustomGameMode.MoveAndStop) return GetTaskCount(playerId, comms, moveAndStop: true);
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

        if (pc.Is(CustomRoles.Damocles)) ProgressText.Append(' ' + Damocles.GetProgressText(playerId));
        if (pc.Is(CustomRoles.Stressed)) ProgressText.Append(' ' + Stressed.GetProgressText(playerId));

        if (ProgressText.Length != 0 && !ProgressText.ToString().RemoveHtmlTags().StartsWith(' '))
            ProgressText.Insert(0, ' ');

        return ProgressText.ToString();
    }

    public static string GetAbilityUseLimitDisplay(byte playerId, bool usingAbility = false)
    {
        try
        {
            float limit = playerId.GetAbilityUseLimit();
            if (float.IsNaN(limit)) return string.Empty;
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
            if (Main.PlayerStates.TryGetValue(playerId, out var ps) && ps.MainRole == CustomRoles.Crewpostor)
                NormalColor = Color.red;

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

        if (Options.EnableGM.GetBool())
        {
            SendMessage(GetRoleName(CustomRoles.GM) + GetString("GMInfoLong"), PlayerId);
        }

        foreach (var role in Enum.GetValues(typeof(CustomRoles)).Cast<CustomRoles>().Where(role => role.IsEnable() && !role.IsVanilla()))
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
    public static PlayerControl[] GetPlayersInRadius(float radius, Vector2 from)
    {
        var list = (from tg in Main.AllAlivePlayerControls let dis = Vector2.Distance(@from, tg.Pos()) where !Pelican.IsEaten(tg.PlayerId) && !Medic.ProtectList.Contains(tg.PlayerId) && !tg.inVent where !(dis > radius) select tg).ToList();

        return [.. list];
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
            string mode = GetString($"Rate{role.Key.GetMode()}");
            sb.Append($"\n【{GetRoleName(role.Key)}:{mode} ×{role.Key.GetCount()}】\n");
            ShowChildrenSettings(Options.CustomRoleSpawnChances[role.Key], ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }

        foreach (var opt in OptionItem.AllOptions.Where(x => x.GetBool() && x.Parent == null && x.Id is >= 80000 and < 640000 && !x.IsHiddenOn(Options.CurrentGameMode)))
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
            string mode = GetString($"Rate{role.Key.GetMode()}");
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
        sb.Append($"\n{GetRoleName(CustomRoles.GM)}: {Options.EnableGM.GetString().RemoveHtmlTags()}");

        var impsb = new StringBuilder();
        var neutralsb = new StringBuilder();
        var crewsb = new StringBuilder();
        var addonsb = new StringBuilder();
        //int headCount = -1;
        foreach (var role in EnumHelper.GetAllValues<CustomRoles>())
        {
            string mode = GetString($"Rate{role.GetMode()}");
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
        SendMessage(neutralsb.Append("\n.").ToString(), PlayerId, "<color=#7f8c8d>【 ★ Neutral Roles ★ 】</color>");
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
                case "Maximum":
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

        List<byte> cloneRoles = [..Main.PlayerStates.Keys];
        foreach (byte id in Main.winnerList.ToArray())
        {
            if (EndGamePatch.SummaryText[id].Contains("<INVALID:NotAssigned>")) continue;
            sb.Append("\n<#c4aa02>\u2605</color> ").Append(EndGamePatch.SummaryText[id] /*.RemoveHtmlTags()*/);
            cloneRoles.Remove(id);
        }

        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.SoloKombat:
                List<(int, byte)> list = [];
                list.AddRange(cloneRoles.ToArray().Select(id => (SoloKombatManager.GetRankOfScore(id), id)));

                list.Sort();
                foreach ((int, byte) id in list.ToArray())
                {
                    sb.Append("\n\u3000 ").Append(EndGamePatch.SummaryText[id.Item2]);
                }

                break;
            case CustomGameMode.FFA:
                List<(int, byte)> list2 = [];
                list2.AddRange(cloneRoles.ToArray().Select(id => (FFAManager.GetRankOfScore(id), id)));

                list2.Sort();
                foreach ((int, byte) id in list2.ToArray())
                {
                    sb.Append("\n\u3000 ").Append(EndGamePatch.SummaryText[id.Item2]);
                }

                break;
            case CustomGameMode.MoveAndStop:
                List<(int, byte)> list3 = [];
                list3.AddRange(cloneRoles.ToArray().Select(id => (MoveAndStopManager.GetRankOfScore(id), id)));

                list3.Sort();
                foreach ((int, byte) id in list3.ToArray())
                {
                    sb.Append("\n\u3000 ").Append(EndGamePatch.SummaryText[id.Item2]);
                }

                break;
            case CustomGameMode.HotPotato:
                List<byte> list4 = [];
                list4.AddRange(cloneRoles);

                foreach (byte id in list4.ToArray())
                {
                    sb.Append("\n\u3000 ").Append(EndGamePatch.SummaryText[id]);
                }

                break;
            default:
                foreach (byte id in cloneRoles.ToArray())
                {
                    if (EndGamePatch.SummaryText[id].Contains("<INVALID:NotAssigned>"))
                        continue;
                    sb.Append("\n\u3000 ").Append(EndGamePatch.SummaryText[id]);
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

        var sb = new StringBuilder();
        if (SetEverythingUpPatch.LastWinsText != string.Empty) sb.Append($"<size=90%>{GetString("LastResult")} {SetEverythingUpPatch.LastWinsText}</size>");
        if (SetEverythingUpPatch.LastWinsReason != string.Empty) sb.Append($"\n<size=90%>{GetString("LastEndReason")} {SetEverythingUpPatch.LastWinsReason}</size>");
        if (sb.Length > 0 && Options.CurrentGameMode is not CustomGameMode.SoloKombat and not CustomGameMode.FFA and not CustomGameMode.MoveAndStop and not CustomGameMode.HotPotato) SendMessage("\n", PlayerId, sb.ToString());
    }

    public static string EmptyMessage() => "<size=0>.</size>";

    public static string GetSubRolesText(byte id, bool disableColor = false, bool intro = false, bool summary = false)
    {
        var SubRoles = Main.PlayerStates[id].SubRoles;
        if (SubRoles.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        if (intro)
        {
            bool isLovers = SubRoles.Contains(CustomRoles.Lovers);
            SubRoles.RemoveAll(x => x is CustomRoles.NotAssigned or CustomRoles.LastImpostor or CustomRoles.Lovers);

            if (isLovers)
            {
                //var RoleText = disableColor ? GetRoleName(CustomRoles.Lovers) : ColorString(GetRoleColor(CustomRoles.Lovers), GetRoleName(CustomRoles.Lovers));
                sb.Append($"{ColorString(GetRoleColor(CustomRoles.Lovers), " ♥")}");
            }

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
        try
        {
            color = int.Parse(text);
        }
        catch
        {
            color = -1;
        }

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
                color = 0;
                break;
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
                color = 1;
                break;
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
                color = 2;
                break;
            case "3":
            case "粉红":
            case "pink":
            case "Pink":
            case "Роз":
            case "роз":
            case "Розовый":
            case "розовый":
                color = 3;
                break;
            case "4":
            case "橘":
            case "orange":
            case "Orange":
            case "оранж":
            case "Оранж":
            case "оранжевый":
            case "Оранжевый":
                color = 4;
                break;
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
                color = 5;
                break;
            case "6":
            case "黑":
            case "black":
            case "Black":
            case "Чёрный":
            case "Черный":
            case "чёрный":
            case "черный":
                color = 6;
                break;
            case "7":
            case "白":
            case "white":
            case "White":
            case "Белый":
            case "белый":
                color = 7;
                break;
            case "8":
            case "紫":
            case "purple":
            case "Purple":
            case "Фиол":
            case "фиол":
            case "Фиолетовый":
            case "фиолетовый":
                color = 8;
                break;
            case "9":
            case "棕":
            case "brown":
            case "Brown":
            case "Корич":
            case "корич":
            case "Коричневый":
            case "коричевый":
                color = 9;
                break;
            case "10":
            case "青":
            case "cyan":
            case "Cyan":
            case "Голуб":
            case "голуб":
            case "Голубой":
            case "голубой":
                color = 10;
                break;
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
                color = 11;
                break;
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
                color = 12;
                break;
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
                color = 13;
                break;
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
                color = 14;
                break;
            case "15":
            case "灰":
            case "gray":
            case "Gray":
            case "Сер":
            case "сер":
            case "Серый":
            case "серый":
                color = 15;
                break;
            case "16":
            case "茶":
            case "tan":
            case "Tan":
            case "Загар":
            case "загар":
            case "Загаровый":
            case "загаровый":
                color = 16;
                break;
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
                color = 17;
                break;

            case "18":
            case "隐藏":
            case "?":
                color = 18;
                break;
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
        var taskState = GetPlayerById(Terrorist.PlayerId).GetTaskState();
        if (taskState.IsTaskFinished && (!Main.PlayerStates[Terrorist.PlayerId].IsSuicide || Options.CanTerroristSuicideWin.GetBool())) //タスクが完了で（自殺じゃない OR 自殺勝ちが許可）されていれば
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

    public static void SendMessage(string text, byte sendTo = byte.MaxValue, string title = "")
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (title == "" || title == string.Empty) title = "<color=#aaaaff>" + GetString("DefaultSystemMessageTitle") + "</color>";
        text = text.Replace("color=", string.Empty);
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
                if ((GameStates.IsOnlineGame || GameStates.IsLocalGame))
                    name = $"<color={GetString("HostColor")}>{GetString("HostText")}</color><color={GetString("IconColor")}>{GetString("Icon")}</color><color={GetString("NameColor")}>{name}</color>";


                //name = $"<color=#902efd>{GetString("HostText")}</color><color=#4bf4ff>♥</color>" + name;

                switch (Options.CurrentGameMode)
                {
                    case CustomGameMode.SoloKombat:
                        name = $"<color=#f55252><size=1.7>{GetString("ModeSoloKombat")}</size></color>\r\n" + name;
                        break;
                    case CustomGameMode.FFA:
                        name = $"<color=#00ffff><size=1.7>{GetString("ModeFFA")}</size></color>\r\n" + name;
                        break;
                    case CustomGameMode.MoveAndStop:
                        name = $"<color=#00ffa5><size=1.7>{GetString("ModeMoveAndStop")}</size></color>\r\n" + name;
                        break;
                    case CustomGameMode.HotPotato:
                        name = $"<color=#e8cd46><size=1.7>{GetString("ModeHotPotato")}</size></color>\r\n" + name;
                        break;
                }
            }

            DevUser devUser = player.FriendCode.GetDevUser();
            bool isMod = ChatCommands.IsPlayerModerator(player.FriendCode);
            if (devUser.HasTag() || isMod)
            {
                string tag = devUser.GetTag();
                if (player.AmOwner || player.IsModClient())
                    name = tag + (isMod ? ("<size=1.4>" + GetString("ModeratorTag") + "\r\n</size>") : string.Empty) + name;
                else name = tag.Replace("\r\n", " - ") + (isMod ? ("<size=1.4>" + GetString("ModeratorTag") + " - </size>") : string.Empty) + name;
            }

            if (player.AmOwner)
            {
                name = Options.GetSuffixMode() switch
                {
                    SuffixModes.TOHE => name + $"\r\n<color={Main.ModColor}>TOHE+ v{Main.PluginDisplayVersion}</color>",
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
                if (!playerRooms.TryAdd(room.name, 1)) playerRooms[room.name]++;
            }
        }

        return playerRooms.Count > 0 ? playerRooms : null;
    }

    public static PlayerControl GetPlayerById(int PlayerId)
    {
        // ReSharper disable once LoopCanBeConvertedToQuery ---- Leave it like this for better performance
        foreach (var pc in Main.AllPlayerControls)
        {
            if (pc.PlayerId == PlayerId)
            {
                return pc;
            }
        }

        return null;
    }

    public static GameData.PlayerInfo GetPlayerInfoById(int PlayerId) => GameData.Instance.AllPlayers.ToArray().FirstOrDefault(info => info.PlayerId == PlayerId);

    private static StringBuilder SelfSuffix = new();
    private static readonly StringBuilder SelfMark = new(20);
    private static readonly StringBuilder TargetSuffix = new();
    private static readonly StringBuilder TargetMark = new(20);

    public static async void NotifyRoles(bool isForMeeting = false, PlayerControl SpecifySeer = null, PlayerControl SpecifyTarget = null, bool NoCache = false, bool ForceLoop = false, bool CamouflageIsForMeeting = false, bool GuesserIsForMeeting = false, bool MushroomMixup = false)
    {
        //if (Options.DeepLowLoad.GetBool()) await Task.Run(() => { DoNotifyRoles(isForMeeting, SpecifySeer, NoCache, ForceLoop, CamouflageIsForMeeting, GuesserIsForMeeting); });
        /*else */

        if ((SpecifySeer != null && SpecifySeer.IsModClient()) || !AmongUsClient.Instance.AmHost || Main.AllPlayerControls == null || GameStates.IsMeeting) return;

        await DoNotifyRoles(isForMeeting, SpecifySeer, SpecifyTarget, NoCache, ForceLoop, CamouflageIsForMeeting, GuesserIsForMeeting, MushroomMixup);
    }

    public static Task DoNotifyRoles(bool isForMeeting = false, PlayerControl SpecifySeer = null, PlayerControl SpecifyTarget = null, bool NoCache = false, bool ForceLoop = false, bool CamouflageIsForMeeting = false, bool GuesserIsForMeeting = false, bool MushroomMixup = false)
    {
        //var caller = new System.Diagnostics.StackFrame(1, false);
        //var callerMethod = caller.GetMethod();
        //string callerMethodName = callerMethod.Name;
        //string callerClassName = callerMethod.DeclaringType.FullName;
        //Logger.Info("NotifyRoles was called from " + callerClassName + "." + callerMethodName, "NotifyRoles");
        HudManagerPatch.NowCallNotifyRolesCount++;
        HudManagerPatch.LastSetNameDesyncCount = 0;

        PlayerControl[] seerList = SpecifySeer != null ? ( [SpecifySeer]) : Main.AllPlayerControls;
        PlayerControl[] targetList = SpecifyTarget != null ? ( [SpecifyTarget]) : Main.AllPlayerControls;

        Logger.Info($" Seers: {string.Join(", ", seerList.Select(x => x.GetRealName()))} ---- Targets: {string.Join(", ", targetList.Select(x => x.GetRealName()))}", "NR");

        //seer: Players who can see changes made here
        //target: Players subject to changes that seer can see
        foreach (PlayerControl seer in seerList)
        {
            try
            {
                if (seer == null || seer.Data.Disconnected || seer.IsModClient()) continue;

                string fontSize = "1.6";
                if (isForMeeting && (seer.GetClient().PlatformData.Platform == Platforms.Playstation || seer.GetClient().PlatformData.Platform == Platforms.Switch)) fontSize = "70%";
                //Logger.Info("NotifyRoles-Loop1-" + seer.GetNameWithRole().RemoveHtmlTags() + ":START", "NotifyRoles");

                // Text containing progress, such as tasks
                string SelfTaskText = GetProgressText(seer);
                SelfMark.Clear();

                if (Options.CurrentGameMode is CustomGameMode.FFA or CustomGameMode.MoveAndStop or CustomGameMode.HotPotato) goto GameMode0;

                SelfMark.Append(Snitch.GetWarningArrow(seer));
                if (seer.Is(CustomRoles.Lovers)) SelfMark.Append(ColorString(GetRoleColor(CustomRoles.Lovers), "♥"));
                if (BallLightning.IsGhost(seer)) SelfMark.Append(ColorString(GetRoleColor(CustomRoles.BallLightning), "■"));
                if ((Medic.InProtect(seer.PlayerId) || Medic.TempMarkProtected == seer.PlayerId)
                    && !seer.Is(CustomRoles.Medic)
                    && (Medic.WhoCanSeeProtect.GetInt() == 0 || Medic.WhoCanSeeProtect.GetInt() == 2))
                    SelfMark.Append(ColorString(GetRoleColor(CustomRoles.Medic), " ●"));
                SelfMark.Append(Gamer.TargetMark(seer, seer));
                SelfMark.Append(Sniper.GetShotNotify(seer.PlayerId));
                if (Blackmailer.ForBlackmailer.Contains(seer.PlayerId)) SelfMark.Append(ColorString(GetRoleColor(CustomRoles.Blackmailer), "╳"));

                GameMode0:

                SelfSuffix.Clear();

                if (Options.CurrentGameMode is CustomGameMode.FFA or CustomGameMode.MoveAndStop or CustomGameMode.HotPotato) goto GameMode;

                if (!isForMeeting)
                {
                    GetPetCDSuffix(seer, ref SelfSuffix);

                    if (seer.Is(CustomRoles.Asthmatic)) SelfSuffix.Append(Asthmatic.GetSuffixText(seer.PlayerId));

                    switch (seer.GetCustomRole())
                    {
                        case CustomRoles.Tether when !seer.IsModClient():
                            if (SelfSuffix.Length > 0 && Tether.TargetText(seer.PlayerId) != string.Empty) SelfSuffix.Append(", ");
                            SelfSuffix.Append(Tether.TargetText(seer.PlayerId));
                            break;
                        case CustomRoles.Druid when !seer.IsModClient():
                            if (SelfSuffix.Length > 0 && Druid.GetSuffixText(seer.PlayerId) != string.Empty) SelfSuffix.Append(", ");
                            SelfSuffix.Append(Druid.GetSuffixText(seer.PlayerId));
                            break;

                        // ---------------------------------------------------------------------------------------

                        case CustomRoles.SuperStar when Options.EveryOneKnowSuperStar.GetBool():
                            SelfMark.Append(ColorString(GetRoleColor(CustomRoles.SuperStar), "★"));
                            break;
                        case CustomRoles.Rabbit:
                            SelfSuffix.Append(Rabbit.GetSuffix(seer));
                            break;
                        case CustomRoles.Penguin:
                            SelfSuffix.Append(Penguin.GetSuffix(seer));
                            break;
                        case CustomRoles.BountyHunter:
                            SelfSuffix.Append(BountyHunter.GetTargetText(seer, false));
                            SelfSuffix.Append(BountyHunter.GetTargetArrow(seer));
                            break;
                        case CustomRoles.Hookshot:
                            SelfSuffix.Append(Hookshot.SuffixText(seer.PlayerId));
                            break;
                        case CustomRoles.Ricochet:
                            SelfSuffix.Append(Ricochet.TargetText(seer.PlayerId));
                            break;
                        case CustomRoles.Hitman:
                            SelfSuffix.Append(Hitman.GetTargetText(seer.PlayerId));
                            break;
                        case CustomRoles.Romantic:
                            SelfSuffix.Append(Romantic.GetTargetText(seer.PlayerId));
                            break;
                        case CustomRoles.VengefulRomantic:
                            SelfSuffix.Append(VengefulRomantic.GetTargetText(seer.PlayerId));
                            break;
                        case CustomRoles.Postman when !seer.IsModClient():
                            SelfSuffix.Append(Postman.TargetText(seer.PlayerId));
                            break;
                        case CustomRoles.Tornado when !seer.IsModClient():
                            SelfSuffix.Append(Tornado.GetSuffixText());
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
                            SelfSuffix.Append(YinYanger.ModeText(seer));
                            break;
                        case CustomRoles.FireWorks:
                            SelfSuffix.Append(FireWorks.GetStateText(seer));
                            break;
                        case CustomRoles.HexMaster:
                        case CustomRoles.Witch:
                            SelfSuffix.Append(Witch.GetSpellModeText(seer, false, isForMeeting));
                            break;
                        case CustomRoles.Monitor:
                        case CustomRoles.AntiAdminer:
                            if (AntiAdminer.IsAdminWatch) SelfSuffix.Append(GetString("AntiAdminerAD"));
                            if (AntiAdminer.IsVitalWatch) SelfSuffix.Append(GetString("AntiAdminerVI"));
                            if (AntiAdminer.IsDoorLogWatch) SelfSuffix.Append(GetString("AntiAdminerDL"));
                            if (AntiAdminer.IsCameraWatch) SelfSuffix.Append(GetString("AntiAdminerCA"));
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
                    SelfMark.Append(Witch.GetSpelledMark(seer.PlayerId, isForMeeting));
                }

                SelfSuffix.Append(Deathpact.GetDeathpactPlayerArrow(seer));

                GameMode:

                switch (Options.CurrentGameMode)
                {
                    case CustomGameMode.FFA:
                        SelfSuffix.Append(FFAManager.GetPlayerArrow(seer));
                        SelfSuffix.Append(SelfSuffix.Length > 0 && FFAManager.LatestChatMessage != string.Empty ? "\n" : string.Empty).Append(FFAManager.LatestChatMessage);
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
                }

                string SeerRealName = seer.GetRealName(isForMeeting);

                if (!isForMeeting && MeetingStates.FirstMeeting && Options.ChangeNameToRoleInfo.GetBool() && Options.CurrentGameMode is not CustomGameMode.FFA and not CustomGameMode.MoveAndStop and not CustomGameMode.HotPotato)
                {
                    if (seer.GetCustomRole().IsMadmate() || seer.Is(CustomRoles.Madmate))
                    {
                        SeerRealName = $"<color=#ff1919>{GetString("YouAreMadmate")}</color>\n<size=90%>{seer.GetRoleInfo()}</size>";
                    }

                    else if (seer.GetCustomRole().IsCrewmate())
                    {
                        SeerRealName = $"<color=#8cffff>{GetString("YouAreCrewmate")}</color>\n{seer.GetRoleInfo()}";
                    }

                    else if (seer.GetCustomRole().IsImpostor())
                    {
                        SeerRealName = $"<color=#ff1919></color>\n<size=90%>{seer.GetRoleInfo()}</size>";
                    }

                    else if (seer.GetCustomRole().IsNeutral())
                    {
                        SeerRealName = $"<color=#7f8c8d>{GetString("YouAreNeutral")}</color>\n<size=90%>{seer.GetRoleInfo()}</size>";
                    }
                }

                // Combine seer's job title and SelfTaskText with seer's player name and SelfMark
                string SelfRoleName = $"<size={fontSize}>{seer.GetDisplayRoleName()}{SelfTaskText}</size>";
                string SelfDeathReason = seer.KnowDeathReason(seer) ? $"\n<size=1.7>({ColorString(GetRoleColor(CustomRoles.Doctor), GetVitalText(seer.PlayerId))})</size>" : string.Empty;
                string SelfName = $"{ColorString(seer.GetRoleColor(), SeerRealName)}{SelfDeathReason}{SelfMark}";

                if (Options.CurrentGameMode is CustomGameMode.FFA or CustomGameMode.MoveAndStop or CustomGameMode.HotPotato) goto GameMode2;

                SelfName = seer.GetCustomRole() switch
                {
                    CustomRoles.Arsonist when seer.IsDouseDone() => $"{ColorString(seer.GetRoleColor(), GetString("EnterVentToWin"))}",
                    CustomRoles.Revolutionist when seer.IsDrawDone() => $">{ColorString(seer.GetRoleColor(), string.Format(GetString("EnterVentWinCountDown"), Main.RevolutionistCountdown.GetValueOrDefault(seer.PlayerId, 10)))}",
                    _ => SelfName
                };

                if (Pelican.IsEaten(seer.PlayerId))
                    SelfName = $"{ColorString(GetRoleColor(CustomRoles.Pelican), GetString("EatenByPelican"))}";
                if (Deathpact.IsInActiveDeathpact(seer))
                    SelfName = Deathpact.GetDeathpactString(seer);
                if (NameNotifyManager.GetNameNotify(seer, out var name))
                    SelfName = name;

                // Devourer
                if (Devourer.HideNameOfConsumedPlayer.GetBool() && Devourer.playerIdList.Any(x => Main.PlayerStates[x].Role is Devourer { IsEnable: true } dv && dv.PlayerSkinsCosumed.Contains(seer.PlayerId)) && !CamouflageIsForMeeting)
                    SelfName = GetString("DevouredName");
                // Camouflage
                if (((IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool() && (Main.NormalOptions.MapId != 5 || !Options.CommsCamouflageDisableOnFungle.GetBool())) || Camouflager.IsActive) && !CamouflageIsForMeeting)
                    SelfName = $"<size=0>{SelfName}</size>";

                GameMode2:

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

                SelfName += SelfSuffix.ToString() == string.Empty ? string.Empty : $"\r\n {SelfSuffix}";
                if (!isForMeeting) SelfName += "\r\n";

                seer.RpcSetNamePrivate(SelfName, true, force: NoCache);

                // Run the second loop only when necessary, such as when seer is dead
                if (seer.Data.IsDead || !seer.IsAlive() || NoCache || CamouflageIsForMeeting || MushroomMixup || IsActive(SystemTypes.MushroomMixupSabotage) || ForceLoop || seerList.Length == 1 || targetList.Length == 1)
                {
                    foreach (PlayerControl target in targetList)
                    {
                        if (target.PlayerId == seer.PlayerId) continue;
                        Logger.Info($"NotifyRoles-Loop2-{target.GetNameWithRole().RemoveHtmlTags()}:START", "NotifyRoles");

                        if ((IsActive(SystemTypes.MushroomMixupSabotage) || MushroomMixup) && target.IsAlive() && !seer.Is(CustomRoleTypes.Impostor) && Main.ResetCamPlayerList.Contains(seer.PlayerId))
                        {
                            seer.RpcSetNamePrivate("<size=0%>", true, force: NoCache);
                        }
                        else
                        {
                            TargetMark.Clear();

                            if (Options.CurrentGameMode is CustomGameMode.FFA or CustomGameMode.MoveAndStop or CustomGameMode.HotPotato) goto BeforeEnd2;

                            TargetMark.Append(Witch.GetSpelledMark(target.PlayerId, isForMeeting));

                            if (target.Is(CustomRoles.SuperStar) && Options.EveryOneKnowSuperStar.GetBool())
                                TargetMark.Append(ColorString(GetRoleColor(CustomRoles.SuperStar), "★"));

                            if (BallLightning.IsGhost(target))
                                TargetMark.Append(ColorString(GetRoleColor(CustomRoles.BallLightning), "■"));

                            TargetMark.Append(Snitch.GetWarningMark(seer, target));

                            if ((seer.Is(CustomRoles.Lovers) && target.Is(CustomRoles.Lovers))
                                || (seer.Data.IsDead && !seer.Is(CustomRoles.Lovers) && target.Is(CustomRoles.Lovers)))
                            {
                                TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Lovers)}>♥</color>");
                            }

                            if (Randomizer.IsShielded(target)) TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Randomizer), "✚"));

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

                                    else if (Main.ArsonistTimer.TryGetValue(seer.PlayerId, out var ar_kvp) && ar_kvp.PLAYER == target)
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
                                    if (Farseer.FarseerTimer.TryGetValue(seer.PlayerId, out var ar_kvp2) && ar_kvp2.PLAYER == target)
                                    {
                                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Farseer)}>○</color>");
                                    }

                                    break;
                                case CustomRoles.Analyzer:
                                    if ((Main.PlayerStates[seer.PlayerId].Role as Analyzer).CurrentTarget.ID == target.PlayerId)
                                    {
                                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Analyzer)}>○</color>");
                                    }

                                    break;
                                case CustomRoles.Puppeteer when Puppeteer.PuppeteerList.ContainsValue(seer.PlayerId) && Puppeteer.PuppeteerList.ContainsKey(target.PlayerId):
                                    TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Impostor)}>◆</color>");
                                    break;
                            }

                            BeforeEnd2:

                            // Other people's roles and tasks will only be visible if the ghost can see other people's roles and the seer is dead, otherwise it will be empty.
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
                                (target.Is(CustomRoles.Mayor) && Options.MayorRevealWhenDoneTasks.GetBool() && target.GetTaskState().IsTaskFinished) ||
                                (seer.Is(CustomRoleTypes.Crewmate) && target.Is(CustomRoles.Marshall) && target.GetTaskState().IsTaskFinished) ||
                                (Main.PlayerStates[target.PlayerId].deathReason == PlayerState.DeathReason.Vote && Options.SeeEjectedRolesInMeeting.GetBool()) ||
                                Totocalcio.KnowRole(seer, target) ||
                                Romantic.KnowRole(seer, target) ||
                                Lawyer.KnowRole(seer, target) ||
                                EvilDiviner.IsShowTargetRole(seer, target) ||
                                Executioner.KnowRole(seer, target) ||
                                Succubus.KnowRole(seer, target) ||
                                Necromancer.KnowRole(seer, target) ||
                                //CursedSoul.KnowRole(seer, target) ||
                                Amnesiac.KnowRole(seer, target) ||
                                Virus.KnowRole(seer, target) ||
                                Options.CurrentGameMode is CustomGameMode.FFA or CustomGameMode.MoveAndStop or CustomGameMode.HotPotato ||
                                (seer.IsRevealedPlayer(target) && !target.Is(CustomRoles.Trickster)) ||
                                seer.Is(CustomRoles.God) ||
                                target.Is(CustomRoles.GM)
                                    ? $"<size={fontSize}>{target.GetDisplayRoleName(seer.PlayerId != target.PlayerId && !seer.Data.IsDead)}{GetProgressText(target)}</size>\r\n"
                                    : string.Empty;

                            if (!seer.Data.IsDead && seer.IsRevealedPlayer(target) && target.Is(CustomRoles.Trickster))
                            {
                                TargetRoleText = Farseer.RandomRole[seer.PlayerId];
                                TargetRoleText += Farseer.GetTaskState();
                            }

                            if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
                                TargetRoleText = $"<size={fontSize}>{GetProgressText(target)}</size>\r\n";

                            string TargetPlayerName = target.GetRealName(isForMeeting);

                            if (Options.CurrentGameMode is CustomGameMode.FFA or CustomGameMode.MoveAndStop or CustomGameMode.HotPotato) goto BeforeEnd;

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
                                case CustomRoles.Psychic when seer.IsAlive() && Psychic.IsRedForPsy(target, seer) && isForMeeting:
                                    TargetPlayerName = ColorString(GetRoleColor(CustomRoles.Impostor), TargetPlayerName);
                                    break;
                                case CustomRoles.Mafia when !seer.IsAlive() && target.IsAlive():
                                    TargetPlayerName = $"{ColorString(GetRoleColor(CustomRoles.Mafia), target.PlayerId.ToString())} {TargetPlayerName}";
                                    break;
                                case CustomRoles.Judge when seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting:
                                    TargetPlayerName = $"{ColorString(GetRoleColor(CustomRoles.Judge), target.PlayerId.ToString())} {TargetPlayerName}";
                                    break;
                                case CustomRoles.NiceSwapper when seer.IsAlive() && target.IsAlive() && isForMeeting:
                                    TargetPlayerName = $"{ColorString(GetRoleColor(CustomRoles.NiceSwapper), target.PlayerId.ToString())} {TargetPlayerName}";
                                    break;
                                case CustomRoles.HeadHunter when (Main.PlayerStates[seer.PlayerId].Role as HeadHunter).Targets.Contains(target.PlayerId) && seer.IsAlive():
                                    TargetPlayerName = $"<color=#000000>{TargetPlayerName}</size>";
                                    break;
                                case CustomRoles.BountyHunter when (Main.PlayerStates[seer.PlayerId].Role as BountyHunter).GetTarget(seer) == target.PlayerId && seer.IsAlive():
                                    TargetPlayerName = $"<color=#000000>{TargetPlayerName}</size>";
                                    break;
                                case CustomRoles.ParityCop when seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting:
                                    TargetPlayerName = $"{ColorString(GetRoleColor(CustomRoles.ParityCop), target.PlayerId.ToString())} {TargetPlayerName}";
                                    break;
                                case CustomRoles.Councillor when seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting:
                                    TargetPlayerName = $"{ColorString(GetRoleColor(CustomRoles.Councillor), target.PlayerId.ToString())} {TargetPlayerName}";
                                    break;
                                case CustomRoles.Doomsayer when seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting:
                                    TargetPlayerName = $"{ColorString(GetRoleColor(CustomRoles.Doomsayer), $" {target.PlayerId}")} {TargetPlayerName}";
                                    break;
                                case CustomRoles.Lookout when seer.IsAlive() && target.IsAlive():
                                    TargetPlayerName = $"{ColorString(GetRoleColor(CustomRoles.Lookout), $" {target.PlayerId}")} {TargetPlayerName}";
                                    break;
                            }

                            // Guesser Mode ID
                            if (Options.GuesserMode.GetBool())
                            {
                                //Crewmates
                                if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting && !seer.Is(CustomRoles.Judge) && !seer.Is(CustomRoles.NiceSwapper) && !seer.Is(CustomRoles.ParityCop) && !seer.Is(CustomRoles.Lookout) && Options.CrewmatesCanGuess.GetBool() && seer.GetCustomRole().IsCrewmate())
                                {
                                    TargetPlayerName = $"{ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString())} {TargetPlayerName}";
                                }
                                else if (seer.Is(CustomRoles.NiceGuesser) && !Options.CrewmatesCanGuess.GetBool())
                                {
                                    if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting)
                                    {
                                        TargetPlayerName = $"{ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString())} {TargetPlayerName}";
                                    }
                                }

                                //Impostors
                                if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting && !seer.Is(CustomRoles.Councillor) && !seer.Is(CustomRoles.Mafia) && Options.ImpostorsCanGuess.GetBool() && seer.GetCustomRole().IsImpostor())
                                {
                                    TargetPlayerName = $"{ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString())} {TargetPlayerName}";
                                }
                                else if (seer.Is(CustomRoles.EvilGuesser) && !Options.ImpostorsCanGuess.GetBool())
                                {
                                    if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting)
                                    {
                                        TargetPlayerName = $"{ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString())} {TargetPlayerName}";
                                    }
                                }

                                // Neutrals
                                if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting && Options.NeutralKillersCanGuess.GetBool() && seer.GetCustomRole().IsNK())
                                {
                                    TargetPlayerName = $"{ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString())} {TargetPlayerName}";
                                }

                                if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting && Options.PassiveNeutralsCanGuess.GetBool() && seer.GetCustomRole().IsNonNK() && !seer.Is(CustomRoles.Doomsayer))
                                {
                                    TargetPlayerName = $"{ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString())} {TargetPlayerName}";
                                }
                            }
                            else // Off Guesser Mode ID
                            {
                                if (seer.Is(CustomRoles.NiceGuesser) || seer.Is(CustomRoles.EvilGuesser) || (seer.Is(CustomRoles.Guesser) && !seer.Is(CustomRoles.ParityCop) && !seer.Is(CustomRoles.NiceSwapper) && !seer.Is(CustomRoles.Lookout)))
                                {
                                    if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting)
                                    {
                                        TargetPlayerName = $"{ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString())} {TargetPlayerName}";
                                    }
                                }
                            }
                            //ターゲットのプレイヤー名の色を書き換えます。

                            BeforeEnd:

                            TargetPlayerName = TargetPlayerName.ApplyNameColorData(seer, target, isForMeeting);

                            if (Options.CurrentGameMode is CustomGameMode.FFA or CustomGameMode.MoveAndStop or CustomGameMode.HotPotato) goto End;

                            if (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Snitch) && target.Is(CustomRoles.Madmate) && target.GetTaskState().IsTaskFinished)
                                TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Impostor), "★"));
                            if (seer.Is(CustomRoleTypes.Crewmate) && target.Is(CustomRoles.Marshall) && target.GetTaskState().IsTaskFinished)
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

                            if (seer.Is(CustomRoles.Medic) && (Medic.InProtect(target.PlayerId) || Medic.TempMarkProtected == target.PlayerId) && (Medic.WhoCanSeeProtect.GetInt() is 0 or 1))
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

                            End:

                            TargetSuffix.Clear();

                            if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
                                TargetSuffix.Append(SoloKombatManager.GetDisplayHealth(target));

                            TargetSuffix.Append(PlagueDoctor.GetLowerTextOthers(seer, target));
                            TargetSuffix.Append(Stealth.GetSuffix(seer, target));
                            TargetSuffix.Append(Bubble.GetEncasedPlayerSuffix(seer, target));

                            if (target.Is(CustomRoles.Librarian)) TargetSuffix.Append(Librarian.GetNameTextForSuffix(target.PlayerId));

                            string TargetDeathReason = string.Empty;
                            if (seer.KnowDeathReason(target))
                                TargetDeathReason = $"\n<size=1.7>({ColorString(GetRoleColor(CustomRoles.Doctor), GetVitalText(target.PlayerId))})</size>";

                            // Devourer
                            if (Devourer.HideNameOfConsumedPlayer.GetBool() && Devourer.playerIdList.Any(x => Main.PlayerStates[x].Role is Devourer { IsEnable: true } dv && dv.PlayerSkinsCosumed.Contains(seer.PlayerId)) && !CamouflageIsForMeeting)
                                TargetPlayerName = GetString("DevouredName");

                            // Camouflage
                            if (((IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool() && (Main.NormalOptions.MapId != 5 || !Options.CommsCamouflageDisableOnFungle.GetBool())) || Camouflager.IsActive) && !CamouflageIsForMeeting)
                                TargetPlayerName = $"<size=0>{TargetPlayerName}</size>";

                            string TargetName = $"{TargetRoleText}{TargetPlayerName}{TargetDeathReason}{TargetMark}";
                            TargetName += TargetSuffix.ToString() == string.Empty ? string.Empty : ("\r\n" + TargetSuffix);

                            target.RpcSetNamePrivate(TargetName, true, seer, force: NoCache);
                        }

                        //Logger.Info($"NotifyRoles-Loop2-{target.GetNameWithRole().RemoveHtmlTags()}:END", "NotifyRoles");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error for {seer.GetNameWithRole()}: {ex}", "NR");
            }

            //Logger.Info($"NotifyRoles-Loop1-{seer.GetNameWithRole().RemoveHtmlTags()}:END", "NotifyRoles");
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

    public static void AddAbilityCD(CustomRoles role, byte playerId, bool includeDuration = true)
    {
        if (role.UsesPetInsteadOfKill())
        {
            Main.AbilityCD[playerId] = (TimeStamp, (int)Math.Round(Main.AllPlayerKillCooldown.TryGetValue(playerId, out var KCD) ? KCD : Options.DefaultKillCooldown));
            return;
        }

        int CD = role switch
        {
            CustomRoles.Doormaster => Doormaster.VentCooldown.GetInt(),
            CustomRoles.Tether => Tether.VentCooldown.GetInt(),
            CustomRoles.Mayor => (int)Math.Round(Options.DefaultKillCooldown),
            CustomRoles.Paranoia => (int)Math.Round(Options.DefaultKillCooldown),
            CustomRoles.Grenadier => Options.GrenadierSkillCooldown.GetInt() + (includeDuration ? Options.GrenadierSkillDuration.GetInt() : 0),
            CustomRoles.Lighter => Options.LighterSkillCooldown.GetInt() + (includeDuration ? Options.LighterSkillDuration.GetInt() : 0),
            CustomRoles.SecurityGuard => Options.SecurityGuardSkillCooldown.GetInt() + (includeDuration ? Options.SecurityGuardSkillDuration.GetInt() : 0),
            CustomRoles.TimeMaster => Options.TimeMasterSkillCooldown.GetInt() + (includeDuration ? Options.TimeMasterSkillDuration.GetInt() : 0),
            CustomRoles.Veteran => Options.VeteranSkillCooldown.GetInt() + (includeDuration ? Options.VeteranSkillDuration.GetInt() : 0),
            CustomRoles.Perceiver => Perceiver.CD.GetInt(),
            CustomRoles.Convener => Convener.CD.GetInt(),
            CustomRoles.DovesOfNeace => Options.DovesOfNeaceCooldown.GetInt(),
            CustomRoles.Alchemist => Alchemist.VentCooldown.GetInt(),
            CustomRoles.NiceHacker => playerId.IsPlayerModClient() ? -1 : NiceHacker.AbilityCD.GetInt(),
            CustomRoles.CameraMan => CameraMan.VentCooldown.GetInt(),
            CustomRoles.Tornado => Tornado.TornadoCooldown.GetInt(),
            CustomRoles.Sentinel => Sentinel.PatrolCooldown.GetInt(),
            CustomRoles.Sniper => Options.DefaultShapeshiftCooldown.GetInt(),
            CustomRoles.Assassin => Assassin.AssassinateCooldownOpt.GetInt(),
            CustomRoles.Undertaker => Undertaker.AssassinateCooldown.GetInt(),
            CustomRoles.Bomber => Options.BombCooldown.GetInt(),
            CustomRoles.Nuker => Options.NukeCooldown.GetInt(),
            CustomRoles.Sapper => Sapper.ShapeshiftCooldown.GetInt(),
            CustomRoles.Druid => Druid.VentCooldown.GetInt(),
            CustomRoles.Miner => Options.MinerSSCD.GetInt(),
            CustomRoles.Escapee => Options.EscapeeSSCD.GetInt(),
            CustomRoles.QuickShooter => QuickShooter.ShapeshiftCooldown.GetInt(),
            CustomRoles.Disperser => Disperser.DisperserShapeshiftCooldown.GetInt(),
            CustomRoles.Twister => Twister.ShapeshiftCooldown.GetInt(),
            CustomRoles.Swiftclaw => Swiftclaw.DashCD.GetInt() + (includeDuration ? Swiftclaw.DashDuration.GetInt() : 0),
            _ => -1,
        };
        if (CD == -1) return;

        Main.AbilityCD[playerId] = (TimeStamp, CD);
    }

    public static void AfterMeetingTasks()
    {
        foreach (var pc in Main.AllAlivePlayerControls)
        {
            pc.AddKillTimerToDict();

            if (pc.Is(CustomRoles.Truant))
            {
                float beforeSpeed = Main.AllPlayerSpeed[pc.PlayerId];
                Main.AllPlayerSpeed[pc.PlayerId] = Main.MinSpeed;
                pc.MarkDirtySettings();
                _ = new LateTask(() =>
                    {
                        Main.AllPlayerSpeed[pc.PlayerId] = beforeSpeed;
                        pc.MarkDirtySettings();
                    }, Options.TruantWaitingTime.GetFloat(), $"Truant Waiting: {pc.GetNameWithRole()}");
            }

            if (!pc.IsModClient()) pc.ReactorFlash(0.2f); // This should fix black screens

            if (Options.UsePets.GetBool()) pc.AddAbilityCD(includeDuration: false);

            Main.PlayerStates[pc.PlayerId].Role.AfterMeetingTasks();
        }

        CopyCat.ResetRole();

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

        Blackmailer.ForBlackmailer.Clear();

        Damocles.AfterMeetingTasks();
        Stressed.AfterMeetingTasks();

        if (Options.AirshipVariableElectrical.GetBool())
            AirshipElectricalDoors.Initialize();

        Main.DontCancelVoteList.Clear();

        DoorsReset.ResetDoors();

        if ((MapNames)Main.NormalOptions.MapId == MapNames.Airship && AmongUsClient.Instance.AmHost && PlayerControl.LocalPlayer.Is(CustomRoles.GM))
        {
            _ = new LateTask(() => { PlayerControl.LocalPlayer.NetTransform.SnapTo(new(15.5f, 0.0f), (ushort)(PlayerControl.LocalPlayer.NetTransform.lastSequenceId + 8)); }, 11f, "GM Auto-TP Failsafe"); // TP to Main Hall
        }
    }

    public static void AfterPlayerDeathTasks(PlayerControl target, bool onMeeting = false)
    {
        try
        {
            switch (target.GetCustomRole())
            {
                case CustomRoles.Terrorist:
                    Logger.Info(target?.Data?.PlayerName + "はTerroristだった", "MurderPlayer");
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
            }

            if (target == null) return;

            Randomizer.OnAnyoneDeath(target);

            if (Romantic.PartnerId == target.PlayerId)
                _ = new LateTask(Romantic.ChangeRole, 0.5f, "Romantic ChangeRole");

            if (Executioner.Target.ContainsValue(target.PlayerId))
                Executioner.ChangeRoleByTarget(target);
            if (Lawyer.Target.ContainsValue(target.PlayerId))
                Lawyer.ChangeRoleByTarget(target);

            Postman.CheckAndResetTargets(target, isDeath: true);
            Hitman.CheckAndResetTargets();

            Hacker.AddDeadBody(target);
            Mortician.OnPlayerDead(target);
            Bloodhound.OnPlayerDead(target);
            Tracefinder.OnPlayerDead(target);
            Vulture.OnPlayerDead(target);

            FixedUpdatePatch.LoversSuicide(target.PlayerId, onMeeting);
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
            if (Options.CurrentGameMode is not CustomGameMode.FFA and not CustomGameMode.MoveAndStop and not CustomGameMode.HotPotato)
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
        var player = GetPlayerById(num);
        return num switch
        {
            < 15 when player != null => player.GetNameWithRole().RemoveHtmlTags(),
            253 => "Skip",
            254 => "None",
            255 => "Dead",
            _ => "invalid",
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
        string f = $"{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)}/TOHE+_Logs/";
        string t = DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss");
        string filename = $"{f}TOHE-v{Main.PluginVersion}-{t}.log";
        if (!Directory.Exists(f)) Directory.CreateDirectory(f);
        FileInfo file = new($"{Environment.CurrentDirectory}/BepInEx/LogOutput.log");
        file.CopyTo(filename);
        if (!open) return;
        if (PlayerControl.LocalPlayer != null)
            HudManager.Instance?.Chat?.AddChat(PlayerControl.LocalPlayer, string.Format(GetString("Message.DumpfileSaved"), $"TOHE+ v{Main.PluginVersion} {t}.log"));
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
        var taskState = Main.PlayerStates[id].TaskState;
        string TaskCount;
        if (taskState.hasTasks)
        {
            var info = GetPlayerInfoById(id);
            var TaskCompleteColor = HasTasks(info) ? Color.green : Color.cyan; //タスク完了後の色
            var NonCompleteColor = HasTasks(info) ? Color.yellow : Color.white; //カウントされない人外は白色

            if (Workhorse.IsThisRole(id))
                NonCompleteColor = Workhorse.RoleColor;

            var NormalColor = taskState.IsTaskFinished ? TaskCompleteColor : NonCompleteColor;
            if (Main.PlayerStates.TryGetValue(id, out var ps) && ps.MainRole == CustomRoles.Crewpostor)
                NormalColor = Color.red;

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

    public static string GetRemainingKillers(bool notify = false)
    {
        int impnum = 0;
        int neutralnum = 0;
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (Options.ShowImpRemainOnEject.GetBool())
            {
                if (pc.GetCustomRole().IsImpostor())
                    impnum++;
            }

            if (Options.ShowNKRemainOnEject.GetBool())
            {
                if (pc.GetCustomRole().IsNK())
                    neutralnum++;
            }
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

    public static Dictionary<string, Sprite> CachedSprites = [];

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

    ///// <summary>
    ///// 乱数の簡易的なヒストグラムを取得する関数
    ///// <params name="nums">生成した乱数を格納したint配列</params>
    ///// <params name="scale">ヒストグラムの倍率 大量の乱数を扱う場合、この値を下げることをお勧めします。</params>
    ///// </summary>
    //public static string WriteRandomHistgram(int[] nums, float scale = 1.0f)
    //{
    //    int[] countData = new int[nums.Max() + 1];
    //    foreach (int num in nums)
    //    {
    //        if (0 <= num) countData[num]++;
    //    }
    //    StringBuilder sb = new();
    //    for (int i = 0; i < countData.Length; i++)
    //    {
    //        // 倍率適用
    //        countData[i] = (int)(countData[i] * scale);

    //        // 行タイトル
    //        sb.AppendFormat("{0:D2}", i).Append(" : ");

    //        // ヒストグラム部分
    //        for (int j = 0; j < countData[i]; j++)
    //            sb.Append('|');

    //        // 改行
    //        sb.Append('\n');
    //    }

    //    // その他の情報
    //    sb.Append("最大数 - 最小数: ").Append(countData.Max() - countData.Min());

    //    return sb.ToString();
    //}

    public static void SetChatVisible()
    {
        if (!GameStates.IsInGame) return;
        MeetingHud.Instance = Object.Instantiate(HudManager.Instance.MeetingPrefab);
        MeetingHud.Instance.ServerStart(PlayerControl.LocalPlayer.PlayerId);
        AmongUsClient.Instance.Spawn(MeetingHud.Instance);
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
    public static bool IsPlayerModClient(this byte id) => Main.playerVersion.ContainsKey(id);
}