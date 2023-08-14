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
using System.Text;
using System.Text.RegularExpressions;
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
            new LateTask(() =>
            {
                Logger.SendInGame(GetString("AntiBlackOutLoggerSendInGame"), true);
            }, 3f, "Anti-Black Msg SendInGame");
            new LateTask(() =>
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
                new LateTask(() =>
                {
                    Logger.SendInGame(GetString("AntiBlackOutRequestHostToForceEnd"), true);
                }, 3f, "Anti-Black Msg SendInGame");
            }
            else
            {
                new LateTask(() =>
                {
                    Logger.SendInGame(GetString("AntiBlackOutHostRejectForceEnd"), true);
                }, 3f, "Anti-Black Msg SendInGame");
                new LateTask(() =>
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
            TP(pc.NetTransform, location);
    }

    public static void TP(CustomNetworkTransform nt, Vector2 location)
    {
        location += new Vector2(0, 0.3636f);
        if (AmongUsClient.Instance.AmHost) nt.SnapTo(location);
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(nt.NetId, (byte)RpcCalls.SnapTo, SendOption.None);
        //nt.WriteVector2(location, writer);
        NetHelpers.WriteVector2(location, writer);
        writer.Write(nt.lastSequenceId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static ClientData GetClientById(int id)
    {
        try
        {
            var client = AmongUsClient.Instance.allClients.ToArray().Where(cd => cd.Id == id).FirstOrDefault();
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
                    var SwitchSystem = ShipStatus.Instance.Systems[type].Cast<SwitchSystem>();
                    return SwitchSystem != null && SwitchSystem.IsActive;
                }
            case SystemTypes.Reactor:
                {
                    if (mapId == 2) return false;
                    else if (mapId == 4)
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
                    if (mapId != 2) return false;
                    var ReactorSystemType = ShipStatus.Instance.Systems[type].Cast<ReactorSystemType>();
                    return ReactorSystemType != null && ReactorSystemType.IsActive;
                }
            case SystemTypes.LifeSupp:
                {
                    if (mapId is 2 or 4) return false;
                    var LifeSuppSystemType = ShipStatus.Instance.Systems[type].Cast<LifeSuppSystemType>();
                    return LifeSuppSystemType != null && LifeSuppSystemType.IsActive;
                }
            case SystemTypes.Comms:
                {
                    if (mapId == 1)
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
        foreach (var seer in Main.AllPlayerControls)
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
                new LateTask(() =>
                {
                    if (!killer.inVent && (killer.PlayerId != target.PlayerId))
                    {
                        if ((Options.DemolitionistKillerDiesOnMeetingCall.GetBool() || GameStates.IsInTask) && killer.IsAlive()) {
                            killer.RpcMurderPlayerV3(killer);
                            RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                            Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Demolished;
                        }
                    } else RPC.PlaySoundRPC(killer.PlayerId, Sounds.TaskComplete);
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
        //キルフラッシュ(ブラックアウト+リアクターフラッシュ)の処理
        bool ReactorCheck = false; //リアクターフラッシュの確認
        if (Main.NormalOptions.MapId == 2) ReactorCheck = IsActive(SystemTypes.Laboratory);
        else ReactorCheck = IsActive(SystemTypes.Reactor);

        var Duration = Options.KillFlashDuration.GetFloat();
        if (ReactorCheck) Duration += 0.2f; //リアクター中はブラックアウトを長くする

        //実行
        Main.PlayerStates[player.PlayerId].IsBlackOut = true; //ブラックアウト
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
        else if (!ReactorCheck) player.ReactorFlash(0f); //リアクターフラッシュ
        player.MarkDirtySettings();
        new LateTask(() =>
        {
            Main.PlayerStates[player.PlayerId].IsBlackOut = false; //ブラックアウト解除
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
        if (Options.HideGameSettings.GetBool() && Main.AllPlayerControls.Count() > 1)
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
        ColorUtility.TryParseHtmlString(hexColor, out Color c);
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
    public static string GetKillCountText(byte playerId)
    {
        int count = Main.PlayerStates.Count(x => x.Value.GetRealKiller() == playerId);
        if (count < 1) return "";
        return ColorString(new Color32(255, 69, 0, byte.MaxValue), string.Format(GetString("KillCount"), count));
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
        if (Options.CurrentGameMode == CustomGameMode.SoloKombat) return false;
        if (p.IsDead && Options.GhostIgnoreTasks.GetBool()) hasTasks = false;
        var role = States.MainRole;
        switch (role)
        {
            case CustomRoles.GM:
            case CustomRoles.Sheriff:
            case CustomRoles.Arsonist:
            case CustomRoles.Jackal:
            case CustomRoles.Sidekick:
            case CustomRoles.Poisoner:
            case CustomRoles.NSerialKiller:
            case CustomRoles.Traitor:
            case CustomRoles.Glitch:
            case CustomRoles.Pickpocket:
            case CustomRoles.Maverick:
            case CustomRoles.Jinx:
            case CustomRoles.Parasite:
            case CustomRoles.Crusader:
            case CustomRoles.Refugee:
            case CustomRoles.Jester:
            //case CustomRoles.Pirate:
         //   case CustomRoles.Baker:
            case CustomRoles.Famine:
            case CustomRoles.NWitch:
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
            case CustomRoles.Reverie:
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
            case CustomRoles.Counterfeiter:
            case CustomRoles.Witness:
            case CustomRoles.Pursuer:
            case CustomRoles.Spiritcaller:
            case CustomRoles.PlagueBearer:
            case CustomRoles.Pestilence:
            case CustomRoles.Masochist:
            case CustomRoles.Doomsayer:
                hasTasks = false;
                break;
            case CustomRoles.Workaholic:
            case CustomRoles.Terrorist:
            case CustomRoles.Sunnyboy:
            case CustomRoles.Convict:
            case CustomRoles.Opportunist:
            case CustomRoles.Phantom:
            case CustomRoles.Baker:
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
            case CustomRoles.Executioner:
                if (Executioner.ChangeRolesAfterTargetKilled.GetValue() != 1)
                    hasTasks = !ForRecompute;
                else hasTasks = false;
                break;
            case CustomRoles.Lawyer:
                if (Lawyer.ChangeRolesAfterTargetKilled.GetValue() != 1)
                    hasTasks = !ForRecompute;
                else hasTasks = false;
                break;
            default:
                if (role.IsImpostor()) hasTasks = false;
                break;
        }

        foreach (var subRole in States.SubRoles)
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
            pc.Is(CustomRoles.TaskManager) ||
            pc.Is(CustomRoles.Egoist) ||
            pc.Is(CustomRoles.DualPersonality)
            );
    }
    public static string GetProgressText(PlayerControl pc)
    {
        if (!Main.playerVersion.ContainsKey(0)) return ""; //ホストがMODを入れていなければ未記入を返す
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
        if (!Main.playerVersion.ContainsKey(0)) return ""; //ホストがMODを入れていなければ未記入を返す
        var ProgressText = new StringBuilder();
        var role = Main.PlayerStates[playerId].MainRole;
        switch (role)
        {
            case CustomRoles.Arsonist:
                var doused = GetDousedPlayerCount(playerId);
                ProgressText.Append(ColorString(GetRoleColor(CustomRoles.Arsonist).ShadeColor(0.25f), $"<color=#777777>-</color> {doused.Item1}/{doused.Item2}"));
                break;
            case CustomRoles.Sheriff:
                if (Sheriff.ShowShotLimit.GetBool()) ProgressText.Append(Sheriff.GetShotLimit(playerId));
                break;
            case CustomRoles.Veteran:
                var taskState2 = Main.PlayerStates?[playerId].GetTaskState();
                Color TextColor2;
                var TaskCompleteColor2 = Color.green;
                var NonCompleteColor2 = Color.yellow;
                var NormalColor2 = taskState2.IsTaskFinished ? TaskCompleteColor2 : NonCompleteColor2;
                TextColor2 = comms ? Color.gray : NormalColor2;
                string Completed2 = comms ? "?" : $"{taskState2.CompletedTasksCount}";
                Color TextColor21;
                if (Main.VeteranNumOfUsed[playerId] < 1) TextColor21 = Color.red;
                else TextColor21 = Color.white;
                ProgressText.Append(ColorString(TextColor2, $"<color=#777777>-</color> {Completed2}/{taskState2.AllTasksCount}"));
                ProgressText.Append(ColorString(TextColor21, $" <color=#777777>-</color> {Math.Round(Main.VeteranNumOfUsed[playerId], 1)}"));
                break;
            case CustomRoles.Grenadier:
                var taskState3 = Main.PlayerStates?[playerId].GetTaskState();
                Color TextColor3;
                var TaskCompleteColor3 = Color.green;
                var NonCompleteColor3 = Color.yellow;
                var NormalColor3 = taskState3.IsTaskFinished ? TaskCompleteColor3 : NonCompleteColor3;
                TextColor3 = comms ? Color.gray : NormalColor3;
                string Completed3 = comms ? "?" : $"{taskState3.CompletedTasksCount}";
                Color TextColor31;
                if (Main.GrenadierNumOfUsed[playerId] < 1) TextColor31 = Color.red;
                else TextColor31 = Color.white;
                ProgressText.Append(ColorString(TextColor3, $"<color=#777777>-</color> {Completed3}/{taskState3.AllTasksCount}"));
                ProgressText.Append(ColorString(TextColor31, $" <color=#777777>-</color> {Math.Round(Main.GrenadierNumOfUsed[playerId], 1)}"));
                break;
            case CustomRoles.Divinator:
                var taskState4 = Main.PlayerStates?[playerId].GetTaskState();
                Color TextColor4;
                var TaskCompleteColor4 = Color.green;
                var NonCompleteColor4 = Color.yellow;
                var NormalColor4 = taskState4.IsTaskFinished ? TaskCompleteColor4 : NonCompleteColor4;
                TextColor4 = comms ? Color.gray : NormalColor4;
                string Completed4 = comms ? "?" : $"{taskState4.CompletedTasksCount}";
                Color TextColor41;
                if (Divinator.CheckLimit[playerId] < 1) TextColor41 = Color.red;
                else TextColor41 = Color.white;
                ProgressText.Append(ColorString(TextColor4, $"<color=#777777>-</color> {Completed4}/{taskState4.AllTasksCount}"));
                ProgressText.Append(ColorString(TextColor41, $" <color=#777777>-</color> {Math.Round(Divinator.CheckLimit[playerId])}"));
                break;
            case CustomRoles.DovesOfNeace:
                var taskState5 = Main.PlayerStates?[playerId].GetTaskState();
                Color TextColor5;
                var TaskCompleteColor5 = Color.green;
                var NonCompleteColor5 = Color.yellow;
                var NormalColor5 = taskState5.IsTaskFinished ? TaskCompleteColor5 : NonCompleteColor5;
                TextColor5 = comms ? Color.gray : NormalColor5;
                string Completed5 = comms ? "?" : $"{taskState5.CompletedTasksCount}";
                Color TextColor51;
                if (Main.DovesOfNeaceNumOfUsed[playerId] < 1) TextColor51 = Color.red;
                else TextColor51 = Color.white;
                ProgressText.Append(ColorString(TextColor5, $"<color=#777777>-</color> {Completed5}/{taskState5.AllTasksCount}"));
                ProgressText.Append(ColorString(TextColor51, $" <color=#777777>-</color> {Math.Round(Main.DovesOfNeaceNumOfUsed[playerId], 1)}"));
                break;
            case CustomRoles.TimeMaster:
                var taskState6 = Main.PlayerStates?[playerId].GetTaskState();
                Color TextColor6;
                var TaskCompleteColor6 = Color.green;
                var NonCompleteColor6 = Color.yellow;
                var NormalColor6 = taskState6.IsTaskFinished ? TaskCompleteColor6 : NonCompleteColor6;
                TextColor6 = comms ? Color.gray : NormalColor6;
                string Completed6 = comms ? "?" : $"{taskState6.CompletedTasksCount}";
                Color TextColor61;
                if (Main.TimeMasterNumOfUsed[playerId] < 1) TextColor61 = Color.red;
                else TextColor61 = Color.white;
                ProgressText.Append(ColorString(TextColor6, $"<color=#777777>-</color> {Completed6}/{taskState6.AllTasksCount}"));
                ProgressText.Append(ColorString(TextColor61, $" <color=#777777>-</color> {Math.Round(Main.TimeMasterNumOfUsed[playerId], 1)}"));
                break;
            case CustomRoles.Mediumshiper:
                var taskState7 = Main.PlayerStates?[playerId].GetTaskState();
                Color TextColor7;
                var TaskCompleteColor7 = Color.green;
                var NonCompleteColor7 = Color.yellow;
                var NormalColor7 = taskState7.IsTaskFinished ? TaskCompleteColor7 : NonCompleteColor7;
                TextColor7 = comms ? Color.gray : NormalColor7;
                string Completed7 = comms ? "?" : $"{taskState7.CompletedTasksCount}";
                Color TextColor71;
                if (Mediumshiper.ContactLimit[playerId] < 1) TextColor71 = Color.red;
                else TextColor71 = Color.white;
                ProgressText.Append(ColorString(TextColor7, $"<color=#777777>-</color> {Completed7}/{taskState7.AllTasksCount}"));
                ProgressText.Append(ColorString(TextColor71, $" <color=#777777>-</color> {Math.Round(Mediumshiper.ContactLimit[playerId], 1)}"));
                break;
            case CustomRoles.ParityCop:
                var taskState8 = Main.PlayerStates?[playerId].GetTaskState();
                Color TextColor8;
                var TaskCompleteColor8 = Color.green;
                var NonCompleteColor8 = Color.yellow;
                var NormalColor8 = taskState8.IsTaskFinished ? TaskCompleteColor8 : NonCompleteColor8;
                TextColor8 = comms ? Color.gray : NormalColor8;
                string Completed8 = comms ? "?" : $"{taskState8.CompletedTasksCount}";
                Color TextColor81;
                if (ParityCop.MaxCheckLimit[playerId] < 1) TextColor81 = Color.red;
                else TextColor81 = Color.white;
                ProgressText.Append(ColorString(TextColor8, $"<color=#777777>-</color> {Completed8}/{taskState8.AllTasksCount}"));
                ProgressText.Append(ColorString(TextColor81, $" <color=#777777>-</color> {Math.Round(ParityCop.MaxCheckLimit[playerId], 1)}"));
                break;
            case CustomRoles.Oracle:
                var taskState9 = Main.PlayerStates?[playerId].GetTaskState();
                Color TextColor9;
                var TaskCompleteColor9 = Color.green;
                var NonCompleteColor9 = Color.yellow;
                var NormalColor9 = taskState9.IsTaskFinished ? TaskCompleteColor9 : NonCompleteColor9;
                TextColor9 = comms ? Color.gray : NormalColor9;
                string Completed9 = comms ? "?" : $"{taskState9.CompletedTasksCount}";
                Color TextColor91;
                if (Oracle.CheckLimit[playerId] < 1) TextColor91 = Color.red;
                else TextColor91 = Color.white;
                ProgressText.Append(ColorString(TextColor9, $"<color=#777777>-</color> {Completed9}/{taskState9.AllTasksCount}"));
                ProgressText.Append(ColorString(TextColor91, $" <color=#777777>-</color> {Math.Round(Oracle.CheckLimit[playerId], 1)}"));
                break;
            case CustomRoles.SabotageMaster:
                var taskState10 = Main.PlayerStates?[playerId].GetTaskState();
                Color TextColor10;
                var TaskCompleteColor10 = Color.green;
                var NonCompleteColor10 = Color.yellow;
                var NormalColor10 = taskState10.IsTaskFinished ? TaskCompleteColor10 : NonCompleteColor10;
                TextColor10 = comms ? Color.gray : NormalColor10;
                string Completed10 = comms ? "?" : $"{taskState10.CompletedTasksCount}";
                Color TextColor101;
                if (SabotageMaster.SkillLimit.GetFloat() - SabotageMaster.UsedSkillCount > 1) TextColor101 = Color.red;
                else TextColor101 = Color.white;
                ProgressText.Append(ColorString(TextColor10, $"<color=#777777>-</color> {Completed10}/{taskState10.AllTasksCount}"));
                ProgressText.Append(ColorString(TextColor101, $" <color=#777777>-</color> {Math.Round(SabotageMaster.SkillLimit.GetFloat() - SabotageMaster.UsedSkillCount, 1)}"));
                break;
            case CustomRoles.Tracker:
                var taskState11 = Main.PlayerStates?[playerId].GetTaskState();
                Color TextColor11;
                var TaskCompleteColor11 = Color.green;
                var NonCompleteColor11 = Color.yellow;
                var NormalColor11 = taskState11.IsTaskFinished ? TaskCompleteColor11 : NonCompleteColor11;
                TextColor11 = comms ? Color.gray : NormalColor11;
                string Completed11 = comms ? "?" : $"{taskState11.CompletedTasksCount}";
                Color TextColor111;
                if (Tracker.TrackLimit[playerId] < 1) TextColor111 = Color.red;
                else TextColor111 = Color.white;
                ProgressText.Append(ColorString(TextColor11, $"<color=#777777>-</color> {Completed11}/{taskState11.AllTasksCount}"));
                ProgressText.Append(ColorString(TextColor111, $" <color=#777777>-</color> {Math.Round(Tracker.TrackLimit[playerId], 1)}"));
                break;
            case CustomRoles.Bloodhound:
                var taskState12 = Main.PlayerStates?[playerId].GetTaskState();
                Color TextColor12;
                var TaskCompleteColor12 = Color.green;
                var NonCompleteColor12 = Color.yellow;
                var NormalColor12 = taskState12.IsTaskFinished ? TaskCompleteColor12 : NonCompleteColor12;
                TextColor12 = comms ? Color.gray : NormalColor12;
                string Completed12 = comms ? "?" : $"{taskState12.CompletedTasksCount}";
                Color TextColor121;
                if (Bloodhound.UseLimit[playerId] < 1) TextColor121 = Color.red;
                else TextColor121 = Color.white;
                ProgressText.Append(ColorString(TextColor12, $"<color=#777777>-</color> {Completed12}/{taskState12.AllTasksCount}"));
                ProgressText.Append(ColorString(TextColor121, $" <color=#777777>-</color> {Math.Round(Bloodhound.UseLimit[playerId], 1)}"));
                break;
            case CustomRoles.Chameleon:
                var taskState13 = Main.PlayerStates?[playerId].GetTaskState();
                Color TextColor13;
                var TaskCompleteColor13 = Color.green;
                var NonCompleteColor13 = Color.yellow;
                var NormalColor13 = taskState13.IsTaskFinished ? TaskCompleteColor13 : NonCompleteColor13;
                TextColor13 = comms ? Color.gray : NormalColor13;
                string Completed13 = comms ? "?" : $"{taskState13.CompletedTasksCount}";
                Color TextColor131;
                if (Chameleon.UseLimit[playerId] < 1) TextColor131 = Color.red;
                else TextColor131 = Color.white;
                ProgressText.Append(ColorString(TextColor13, $"<color=#777777>-</color> {Completed13}/{taskState13.AllTasksCount}"));
                ProgressText.Append(ColorString(TextColor131, $" <color=#777777>-</color> {Math.Round(Chameleon.UseLimit[playerId], 1)}"));
                break;
            case CustomRoles.Lighter:
                var taskState14 = Main.PlayerStates?[playerId].GetTaskState();
                Color TextColor14;
                var TaskCompleteColor14 = Color.green;
                var NonCompleteColor14 = Color.yellow;
                var NormalColor14 = taskState14.IsTaskFinished ? TaskCompleteColor14 : NonCompleteColor14;
                TextColor14 = comms ? Color.gray : NormalColor14;
                string Completed14 = comms ? "?" : $"{taskState14.CompletedTasksCount}";
                Color TextColor141;
                if (Main.LighterNumOfUsed[playerId] < 1) TextColor141 = Color.red;
                else TextColor141 = Color.white;
                ProgressText.Append(ColorString(TextColor14, $"<color=#777777>-</color> {Completed14}/{taskState14.AllTasksCount}"));
                ProgressText.Append(ColorString(TextColor141, $" <color=#777777>-</color> {Math.Round(Main.LighterNumOfUsed[playerId], 1)}"));
                break;
            //case CustomRoles.Pirate:
            //    ProgressText.Append(ColorString(GetRoleColor(CustomRoles.Pirate).ShadeColor(0.25f), $"({Pirate.NumWin}/{Pirate.SuccessfulDuelsToWin.GetInt()})"));
            //    break;
            case CustomRoles.Crusader:
                ProgressText.Append(Crusader.GetSkillLimit(playerId));
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
            case CustomRoles.Masochist:
                ProgressText.Append(ColorString(GetRoleColor(CustomRoles.Masochist).ShadeColor(0.25f), $"<color=#777777>-</color> {(Main.MasochistKillMax.TryGetValue(playerId, out var count3) ? count3 : 0)}/{Options.MasochistKillMax.GetInt()}"));
                break;            
            case CustomRoles.QuickShooter:
                ProgressText.Append(QuickShooter.GetShotLimit(playerId));
                break;
            case CustomRoles.SwordsMan:
                ProgressText.Append(SwordsMan.GetKillLimit(playerId));
                break;
            case CustomRoles.Pelican:
                ProgressText.Append(Pelican.GetProgressText(playerId));
                break;
            case CustomRoles.Counterfeiter:
                ProgressText.Append(Counterfeiter.GetSeelLimit(playerId));
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
            case CustomRoles.Totocalcio:
                ProgressText.Append(Totocalcio.GetProgressText(playerId));
                break;
            case CustomRoles.Romantic:
                ProgressText.Append(Romantic.GetProgressText(playerId));
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
            case CustomRoles.EvilDiviner:
                ProgressText.Append(EvilDiviner.GetDivinationCount(playerId));
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
                var taskState = Main.PlayerStates?[playerId].GetTaskState();
                if (taskState.hasTasks)
                {
                    Color TextColor;
                    var info = GetPlayerInfoById(playerId);
                    var TaskCompleteColor = HasTasks(info) ? Color.green : GetRoleColor(role).ShadeColor(0.5f); //タスク完了後の色
                    var NonCompleteColor = HasTasks(info) ? Color.yellow : Color.white; //カウントされない人外は白色

                    if (Workhorse.IsThisRole(playerId))
                        NonCompleteColor = Workhorse.RoleColor;

                    var NormalColor = taskState.IsTaskFinished ? TaskCompleteColor : NonCompleteColor;
                    if (Main.PlayerStates.TryGetValue(playerId, out var ps) && ps.MainRole == CustomRoles.Crewpostor)
                        NormalColor = Color.red;

                    TextColor = comms ? Color.gray : NormalColor;
                    string Completed = comms ? "?" : $"{taskState.CompletedTasksCount}";
                    ProgressText.Append(ColorString(TextColor, $"<color=#777777>-</color> {Completed}/{taskState.AllTasksCount}"));
                }
                break;
        }
        if (ProgressText.Length != 0)
            ProgressText.Insert(0, " "); //空じゃなければ空白を追加

        return ProgressText.ToString();
    }
    public static void ShowActiveSettingsHelp(byte PlayerId = byte.MaxValue)
    {
        SendMessage(GetString("CurrentActiveSettingsHelp") + ":", PlayerId);

        if (Options.DisableDevices.GetBool()) { SendMessage(GetString("DisableDevicesInfo"), PlayerId); }
        if (Options.SyncButtonMode.GetBool()) { SendMessage(GetString("SyncButtonModeInfo"), PlayerId); }
        if (Options.SabotageTimeControl.GetBool()) { SendMessage(GetString("SabotageTimeControlInfo"), PlayerId); }
        if (Options.RandomMapsMode.GetBool()) { SendMessage(GetString("RandomMapsModeInfo"), PlayerId); }
        if (Options.EnableGM.GetBool()) { SendMessage(GetRoleName(CustomRoles.GM) + GetString("GMInfoLong"), PlayerId); }
        foreach (var role in Enum.GetValues(typeof(CustomRoles)).Cast<CustomRoles>())
        {
            if (role.IsEnable() && !role.IsVanilla()) SendMessage(GetRoleName(role) + GetRoleMode(role) + GetString(Enum.GetName(typeof(CustomRoles), role) + "InfoLong"), PlayerId);
        }

        if (Options.NoGameEnd.GetBool()) { SendMessage(GetString("NoGameEndInfo"), PlayerId); }
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

        sb.Append(GetString("Settings")).Append(":");
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
        foreach (CustomRoles role in Enum.GetValues(typeof(CustomRoles)))
        {
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
        SendMessage(crewsb.Append("\n.").ToString(), PlayerId, ColorString(Utils.GetRoleColor(CustomRoles.Crewmate), "【 ★ Crewmate Roles ★ 】"));
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

            if (opt.Value.Name == "Maximum") continue; //Maximumの項目は飛ばす
            if (opt.Value.Name == "DisableSkeldDevices" && !Options.IsActiveSkeld) continue;
            if (opt.Value.Name == "DisableMiraHQDevices" && !Options.IsActiveMiraHQ) continue;
            if (opt.Value.Name == "DisablePolusDevices" && !Options.IsActivePolus) continue;
            if (opt.Value.Name == "DisableAirshipDevices" && !Options.IsActiveAirship) continue;
            if (opt.Value.Name == "PolusReactorTimeLimit" && !Options.IsActivePolus) continue;
            if (opt.Value.Name == "AirshipReactorTimeLimit" && !Options.IsActiveAirship) continue;
            if (deep > 0)
            {
                sb.Append(string.Concat(Enumerable.Repeat("┃", Mathf.Max(deep - 1, 0))));
                sb.Append(opt.Index == option.Children.Count ? "┗ " : "┣ ");
            }
            sb.Append($"{opt.Value.GetName(true)}: {opt.Value.GetString()}\n");
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

        sb.Append(GetString("PlayerInfo")).Append(":");
        List<byte> cloneRoles = new(Main.PlayerStates.Keys);
        foreach (var id in Main.winnerList)
        {
            if (EndGamePatch.SummaryText[id].Contains("<INVALID:NotAssigned>")) continue;
            sb.Append($"\n★ ").Append(EndGamePatch.SummaryText[id].RemoveHtmlTags());
            cloneRoles.Remove(id);
        }
        if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
        {
            List<(int, byte)> list = new();
            foreach (var id in cloneRoles) list.Add((SoloKombatManager.GetRankOfScore(id), id));
            list.Sort();
            foreach (var id in list)
                sb.Append($"\n　").Append(EndGamePatch.SummaryText[id.Item2]);
        }
        else
        {
            foreach (var id in cloneRoles)
            {
                if (EndGamePatch.SummaryText[id].Contains("<INVALID:NotAssigned>")) continue;
                sb.Append($"\n　").Append(EndGamePatch.SummaryText[id].RemoveHtmlTags());
            }
        }
        SendMessage(sb.ToString(), PlayerId);
    }
    public static void ShowKillLog(byte PlayerId = byte.MaxValue)
    {
        if (GameStates.IsInGame)
        {
            SendMessage(GetString("CantUse.killlog"), PlayerId);
            return;
        }
        if (EndGamePatch.KillLog != "") SendMessage(EndGamePatch.KillLog, PlayerId);
    }
    public static void ShowLastResult(byte PlayerId = byte.MaxValue)
    {
        if (GameStates.IsInGame)
        {
            SendMessage(GetString("CantUse.lastresult"), PlayerId);
            return;
        }
        var sb = new StringBuilder();
        if (SetEverythingUpPatch.LastWinsText != "") sb.Append($"{GetString("LastResult")}: {SetEverythingUpPatch.LastWinsText}");
        if (SetEverythingUpPatch.LastWinsReason != "") sb.Append($"\n{GetString("LastEndReason")}: {SetEverythingUpPatch.LastWinsReason}");
        if (sb.Length > 0 && Options.CurrentGameMode != CustomGameMode.SoloKombat) SendMessage(sb.ToString(), PlayerId);
    }
    public static string GetSubRolesText(byte id, bool disableColor = false, bool intro = false, bool summary = false)
    {
        var SubRoles = Main.PlayerStates[id].SubRoles;
        if (SubRoles.Count == 0 && intro == false) return "";
        var sb = new StringBuilder();
        foreach (var role in SubRoles)
        {
            if (role is CustomRoles.NotAssigned or
                        CustomRoles.LastImpostor) continue;
            if (summary && role is CustomRoles.Madmate or CustomRoles.Charmed or CustomRoles.Recruit or CustomRoles.Admired or CustomRoles.Infected or CustomRoles.Contagious or CustomRoles.Soulless) continue;

            var RoleText = disableColor ? GetRoleName(role) : ColorString(GetRoleColor(role), GetRoleName(role));
            sb.Append($"{ColorString(Color.gray, " + ")}{RoleText}");
        }

        if (intro && !SubRoles.Contains(CustomRoles.Lovers) && !SubRoles.Contains(CustomRoles.Ntr) && CustomRolesHelper.RoleExist(CustomRoles.Ntr))
        {
            var RoleText = disableColor ? GetRoleName(CustomRoles.Lovers) : ColorString(GetRoleColor(CustomRoles.Lovers), GetRoleName(CustomRoles.Lovers));
            sb.Append($"{ColorString(Color.gray, " + ")}{RoleText}");
        }

        return sb.ToString();
    }

    public static byte MsgToColor(string text, bool isHost = false)
    {
        text = text.ToLowerInvariant();
        text = text.Replace("色", string.Empty);
        int color = -1;
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
            foreach (var pc in Main.AllPlayerControls)
            {
                if (pc.Is(CustomRoles.Terrorist))
                {
                    if (Main.PlayerStates[pc.PlayerId].deathReason == PlayerState.DeathReason.Vote)
                    {
                        //追放された場合は生存扱い
                        Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.etc;
                        //生存扱いのためSetDeadは必要なし
                    }
                    else
                    {
                        //キルされた場合は自爆扱い
                        Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.Suicide;
                    }
                }
                else if (!pc.Data.IsDead)
                {
                    //生存者は爆死
                    pc.SetRealKiller(Terrorist.Object);
                    pc.RpcMurderPlayerV3(pc);
                    Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
                    Main.PlayerStates[pc.PlayerId].SetDead();
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
        if (!(player.AmOwner || (player.FriendCode.GetDevUser().HasTag()))) return;
        string name = Main.AllPlayerNames.TryGetValue(player.PlayerId, out var n) ? n : "";
        if (Main.nickName != "" && player.AmOwner) name = Main.nickName;
        if (name == "") return;
        if (AmongUsClient.Instance.IsGameStarted)
        {
            if (Options.FormatNameMode.GetInt() == 1 && Main.nickName == "") name = Palette.GetColorName(player.Data.DefaultOutfit.ColorId);
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
        return Main.AllPlayerControls.Where(pc => pc.PlayerId == PlayerId).FirstOrDefault();
    }
    public static GameData.PlayerInfo GetPlayerInfoById(int PlayerId) =>
        GameData.Instance.AllPlayers.ToArray().Where(info => info.PlayerId == PlayerId).FirstOrDefault();
    private static StringBuilder SelfSuffix = new();
    private static StringBuilder SelfMark = new(20);
    private static StringBuilder TargetSuffix = new();
    private static StringBuilder TargetMark = new(20);
    public static void NotifyRoles(bool isForMeeting = false, PlayerControl SpecifySeer = null, bool NoCache = false, bool ForceLoop = false, bool CamouflageIsForMeeting = false, bool GuesserIsForMeeting = false)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (Main.AllPlayerControls == null) return;

        //ミーティング中の呼び出しは不正
        if (GameStates.IsMeeting) return;

        var caller = new System.Diagnostics.StackFrame(1, false);
        var callerMethod = caller.GetMethod();
        string callerMethodName = callerMethod.Name;
        string callerClassName = callerMethod.DeclaringType.FullName;
        var logger = Logger.Handler("NotifyRoles");
        logger.Info("NotifyRolesが" + callerClassName + "." + callerMethodName + "から呼び出されました");
        HudManagerPatch.NowCallNotifyRolesCount++;
        HudManagerPatch.LastSetNameDesyncCount = 0;

        var seerList = PlayerControl.AllPlayerControls;
        if (SpecifySeer != null)
        {
            seerList = new();
            seerList.Add(SpecifySeer);
        }
        //seer:ここで行われた変更を見ることができるプレイヤー
        //target:seerが見ることができる変更の対象となるプレイヤー
        foreach (var seer in seerList)
        {
            //seerが落ちているときに何もしない
            if (seer == null || seer.Data.Disconnected) continue;

            if (seer.IsModClient()) continue;
            string fontSize = "1.5";
            if (isForMeeting && (seer.GetClient().PlatformData.Platform == Platforms.Playstation || seer.GetClient().PlatformData.Platform == Platforms.Switch)) fontSize = "70%";
            logger.Info("NotifyRoles-Loop1-" + seer.GetNameWithRole() + ":START");

            //タスクなど進行状況を含むテキスト
            string SelfTaskText = GetProgressText(seer);

            //名前の後ろに付けるマーカー
            SelfMark.Clear();

            //インポスター/キル可能なニュートラルに対するSnitch警告
            SelfMark.Append(Snitch.GetWarningArrow(seer));

            //ハートマークを付ける(自分に)
            if (seer.Is(CustomRoles.Lovers) || CustomRolesHelper.RoleExist(CustomRoles.Ntr)) SelfMark.Append(ColorString(GetRoleColor(CustomRoles.Lovers), "♥"));

            //呪われている場合
            SelfMark.Append(Witch.GetSpelledMark(seer.PlayerId, isForMeeting));
            SelfMark.Append(HexMaster.GetHexedMark(seer.PlayerId, isForMeeting));
            if (Baker.IsPoisoned(seer) && isForMeeting && seer.IsAlive())
                SelfMark.Append(ColorString(GetRoleColor(CustomRoles.Famine), "θ"));


            //如果是大明星
            if (seer.Is(CustomRoles.SuperStar) && Options.EveryOneKnowSuperStar.GetBool())
                SelfMark.Append(ColorString(GetRoleColor(CustomRoles.SuperStar), "★"));

            //球状闪电提示
            if (BallLightning.IsGhost(seer))
                SelfMark.Append(ColorString(GetRoleColor(CustomRoles.BallLightning), "■"));

            //医生护盾提示
            if ((Medic.InProtect(seer.PlayerId) || Medic.TempMarkProtected == seer.PlayerId) && !seer.Is(CustomRoles.Medic) && (Medic.WhoCanSeeProtect.GetInt() == 0 || Medic.WhoCanSeeProtect.GetInt() == 2))
                SelfMark.Append(ColorString(GetRoleColor(CustomRoles.Medic), " ●"));

            //玩家自身血量提示
            SelfMark.Append(Gamer.TargetMark(seer, seer));

            //銃声が聞こえるかチェック
            SelfMark.Append(Sniper.GetShotNotify(seer.PlayerId));
            //Markとは違い、改行してから追記されます。
            SelfSuffix.Clear();

            if (seer.Is(CustomRoles.BountyHunter) && !isForMeeting)
            {
                SelfSuffix.Append(BountyHunter.GetTargetText(seer, false));
                SelfSuffix.Append(BountyHunter.GetTargetArrow(seer));
            }
            if (seer.Is(CustomRoles.Mortician) && !isForMeeting)
            {
                SelfSuffix.Append(Mortician.GetTargetArrow(seer));
            }
            if (seer.Is(CustomRoles.Tracefinder) && !isForMeeting)
            {
                SelfSuffix.Append(Tracefinder.GetTargetArrow(seer));
            }
            if (seer.Is(CustomRoles.Vulture) && Vulture.ArrowsPointingToDeadBody.GetBool() && !isForMeeting)
            {
                SelfSuffix.Append(Vulture.GetTargetArrow(seer));
            }
            if (seer.Is(CustomRoles.FireWorks) && !isForMeeting)
            {
                string stateText = FireWorks.GetStateText(seer);
                SelfSuffix.Append(stateText);
            }
            if (seer.Is(CustomRoles.Witch))
            {
                SelfSuffix.Append(Witch.GetSpellModeText(seer, false, isForMeeting));
            }
            if (seer.Is(CustomRoles.HexMaster))
            {
                SelfSuffix.Append(HexMaster.GetHexModeText(seer, false, isForMeeting));
            }
            if (seer.Is(CustomRoles.AntiAdminer) && !isForMeeting)
            {
                if (AntiAdminer.IsAdminWatch) SelfSuffix.Append("★").Append(GetString("AntiAdminerAD"));
                if (AntiAdminer.IsVitalWatch) SelfSuffix.Append("★").Append(GetString("AntiAdminerVI"));
                if (AntiAdminer.IsDoorLogWatch) SelfSuffix.Append("★").Append(GetString("AntiAdminerDL"));
                if (AntiAdminer.IsCameraWatch) SelfSuffix.Append("★").Append(GetString("AntiAdminerCA"));
            }
            if (seer.Is(CustomRoles.Bloodhound) && !isForMeeting)
            {
                SelfSuffix.Append(Bloodhound.GetTargetArrow(seer));
            }
            if (seer.Is(CustomRoles.Tracker) && !isForMeeting)
            {
                SelfSuffix.Append(Tracker.GetTrackerArrow(seer));
            }
            if (seer.Is(CustomRoles.Spiritualist) && !isForMeeting)
            {
                SelfSuffix.Append(Spiritualist.GetSpiritualistArrow(seer));
            }

            //タスクを終えたSnitchがインポスター/キル可能なニュートラルの方角を確認できる
            if (seer.Is(CustomRoles.Snitch) && !isForMeeting)
            {
            SelfSuffix.Append(Snitch.GetSnitchArrow(seer));
            }
            if (seer.Is(CustomRoles.EvilTracker) && !isForMeeting)
            {
            SelfSuffix.Append(EvilTracker.GetTargetArrow(seer, seer));
            }
            
            SelfSuffix.Append(Deathpact.GetDeathpactPlayerArrow(seer));

            

            //KB自身名字后缀

            if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
                SelfSuffix.Append(SoloKombatManager.GetDisplayHealth(seer));

            //RealNameを取得 なければ現在の名前をRealNamesに書き込む
            string SeerRealName = seer.GetRealName(isForMeeting);

            if (seer.GetCustomRole().IsCrewmate() && !seer.Is(CustomRoles.Madmate))
            {
                if (!isForMeeting && MeetingStates.FirstMeeting && Options.ChangeNameToRoleInfo.GetBool())
                SeerRealName = $"<size=110%><color=#8cffff>" + GetString("YouAreCrewmate") + $"</color></size>\n" + seer.GetRoleInfo();
            }
            if (seer.GetCustomRole().IsImpostor())
            {
                if (!isForMeeting && MeetingStates.FirstMeeting && Options.ChangeNameToRoleInfo.GetBool())
                SeerRealName = $"<size=110%><color=#ff1919>" + GetString("YouAreImpostor") + $"</color></size>\n<size=130%>" + seer.GetRoleInfo() + $"</size>";
            }
            if (seer.GetCustomRole().IsNeutral() && !seer.GetCustomRole().IsMadmate())
            {
                if (!isForMeeting && MeetingStates.FirstMeeting && Options.ChangeNameToRoleInfo.GetBool())
                SeerRealName = $"<size=110%><color=#7f8c8d>" + GetString("YouAreNeutral") + $"</color></size>\n<size=130%>" + seer.GetRoleInfo() + $"</size>";
            }
            if (seer.GetCustomRole().IsMadmate())
            {
                if (!isForMeeting && MeetingStates.FirstMeeting && Options.ChangeNameToRoleInfo.GetBool())
                SeerRealName = $"<size=110%><color=#ff1919>" + GetString("YouAreMadmate") + $"</color></size>\n<size=130%>" + seer.GetRoleInfo() + $"</size>";
            }
            if (seer.Is(CustomRoles.Madmate))
            {
                if (!isForMeeting && MeetingStates.FirstMeeting && Options.ChangeNameToRoleInfo.GetBool())
                SeerRealName = $"<size=110%><color=#ff1919>" + GetString("YouAreMadmate") + $"</color></size>\n<size=130%>" + seer.GetRoleInfo() + $"</size>";
            }
            //seerの役職名とSelfTaskTextとseerのプレイヤー名とSelfMarkを合成
            string SelfRoleName = $"<size={fontSize}>{seer.GetDisplayRoleName()}{SelfTaskText}</size>";
            string SelfDeathReason = seer.KnowDeathReason(seer) ? $"({ColorString(GetRoleColor(CustomRoles.Doctor), GetVitalText(seer.PlayerId))})" : "";
            string SelfName = $"{ColorString(seer.GetRoleColor(), SeerRealName)}{SelfDeathReason}{SelfMark}";
            if (seer.Is(CustomRoles.PlagueBearer) && PlagueBearer.IsPlaguedAll(seer))
            {
                seer.RpcSetCustomRole(CustomRoles.Pestilence);
                seer.Notify(GetString("PlagueBearerToPestilence"));
                seer.RpcGuardAndKill(seer);
                if (!PlagueBearer.PestilenceList.Contains(seer.PlayerId))
                    PlagueBearer.PestilenceList.Add(seer.PlayerId);
                PlagueBearer.SetKillCooldownPestilence(seer.PlayerId);
                PlagueBearer.playerIdList.Remove(seer.PlayerId);
            }

            if (seer.Is(CustomRoles.Arsonist) && seer.IsDouseDone())
                SelfName = $"{ColorString(seer.GetRoleColor(), GetString("EnterVentToWin"))}";
            if (seer.Is(CustomRoles.Revolutionist) && seer.IsDrawDone())
                SelfName = $">{ColorString(seer.GetRoleColor(), string.Format(GetString("EnterVentWinCountDown"), Main.RevolutionistCountdown.TryGetValue(seer.PlayerId, out var x) ? x : 10))}";
            
            if (Pelican.IsEaten(seer.PlayerId))
                SelfName = $"{ColorString(GetRoleColor(CustomRoles.Pelican), GetString("EatenByPelican"))}";
            if (Deathpact.IsInActiveDeathpact(seer))
                SelfName = Deathpact.GetDeathpactString(seer);
            if (NameNotifyManager.GetNameNotify(seer, out var name))
                SelfName = name;

            if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
            {
                SoloKombatManager.GetNameNotify(seer, ref SelfName);
                SelfName = $"<size={fontSize}>{SelfTaskText}</size>\r\n{SelfName}";
            }
            else SelfName = SelfRoleName + "\r\n" + SelfName;
            SelfName += SelfSuffix.ToString() == "" ? "" : "\r\n " + SelfSuffix.ToString();
            bool playerDevoured = Devourer.HideNameOfConsumedPlayer.GetBool() && Devourer.PlayerSkinsCosumed.Any(a => a.Value.Contains(seer.PlayerId));
            if (((IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool()) || Camouflager.IsActive || playerDevoured) && !CamouflageIsForMeeting)
                SelfName = SelfRoleName;
            if (!isForMeeting) SelfName += "\r\n";

            //適用
            seer.RpcSetNamePrivate(SelfName, true, force: NoCache);

            //seerが死んでいる場合など、必要なときのみ第二ループを実行する
            foreach (var target in Main.AllPlayerControls)
            {
                //targetがseer自身の場合は何もしない
                if (target.PlayerId == seer.PlayerId) continue;
                logger.Info("NotifyRoles-Loop2-" + target.GetNameWithRole() + ":START");

                //名前の後ろに付けるマーカー
                TargetMark.Clear();

                //呪われている人
                TargetMark.Append(Witch.GetSpelledMark(target.PlayerId, isForMeeting));
                TargetMark.Append(HexMaster.GetHexedMark(target.PlayerId, isForMeeting));
             //   TargetMark.Append(Baker.GetPoisonMark(target, isForMeeting));


                //如果是大明星
                if (target.Is(CustomRoles.SuperStar) && Options.EveryOneKnowSuperStar.GetBool())
                    TargetMark.Append(ColorString(GetRoleColor(CustomRoles.SuperStar), "★"));


                // Necroview
             /*   if (seer.Is(CustomRoles.Necroview) && isForMeeting)
                {
                    if (target.Is(CustomRoleTypes.Crewmate) && !(target.Is(CustomRoles.Madmate) || target.Is(CustomRoles.Egoist) || target.Is(CustomRoles.Charmed) || target.Is(CustomRoles.Recruit) || target.Is(CustomRoles.Infected) || target.Is(CustomRoles.Contagious) || target.Is(CustomRoles.Rogue) || target.Is(CustomRoles.Rascal) || target.Is(CustomRoles.Soulless)) && target.Data.IsDead || target.Is(CustomRoles.Admired) && target.Data.IsDead)
                        TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Bait), "★"));

                    if ((target.Is(CustomRoleTypes.Impostor) || target.Is(CustomRoles.Madmate) || target.Is(CustomRoles.Rascal) || target.Is(CustomRoles.Parasite) || target.Is(CustomRoles.Refugee) || target.Is(CustomRoles.Crewpostor) || target.Is(CustomRoles.Convict)) && target.Data.IsDead || !target.Is(CustomRoles.Admired) && target.Data.IsDead)
                        TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Impostor), "★"));

                    if ((target.Is(CustomRoleTypes.Neutral) || target.Is(CustomRoles.Rogue) || target.Is(CustomRoles.Contagious) || target.Is(CustomRoles.Charmed) || target.Is(CustomRoles.Recruit) || target.Is(CustomRoles.Infected) || target.Is(CustomRoles.Egoist) || target.Is(CustomRoles.Soulless)) && target.Data.IsDead || !target.Is(CustomRoles.Admired) && target.Data.IsDead || (target.Is(CustomRoles.Parasite) || target.Is(CustomRoles.Refugee) || target.Is(CustomRoles.Crewpostor) || target.Is(CustomRoles.Convict)) && target.Data.IsDead)
                        TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Executioner), "★"));
                }
                // Visionary
                if (seer.Is(CustomRoles.Visionary) && isForMeeting)
                {
                    if (target.Is(CustomRoleTypes.Crewmate) && !(target.Is(CustomRoles.Madmate) || target.Is(CustomRoles.Egoist) || target.Is(CustomRoles.Charmed) || target.Is(CustomRoles.Recruit) || target.Is(CustomRoles.Infected) || target.Is(CustomRoles.Contagious) || target.Is(CustomRoles.Rogue) || target.Is(CustomRoles.Rascal) || target.Is(CustomRoles.Soulless)) && !target.Data.IsDead || target.Is(CustomRoles.Admired) && !target.Data.IsDead)
                        TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Bait), "★"));

                    if ((target.Is(CustomRoleTypes.Impostor) || target.Is(CustomRoles.Madmate) || target.Is(CustomRoles.Rascal) || target.Is(CustomRoles.Parasite) || target.Is(CustomRoles.Refugee) || target.Is(CustomRoles.Crewpostor) || target.Is(CustomRoles.Convict)) && !target.Data.IsDead || !target.Is(CustomRoles.Admired) && !target.Data.IsDead)
                        TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Impostor), "★"));

                    if ((target.Is(CustomRoleTypes.Neutral) || target.Is(CustomRoles.Rogue) || target.Is(CustomRoles.Contagious) || target.Is(CustomRoles.Charmed) || target.Is(CustomRoles.Recruit) || target.Is(CustomRoles.Infected) || target.Is(CustomRoles.Egoist) || target.Is(CustomRoles.Soulless)) && !target.Data.IsDead || !target.Is(CustomRoles.Admired) && !target.Data.IsDead || (target.Is(CustomRoles.Parasite) || target.Is(CustomRoles.Refugee) || target.Is(CustomRoles.Crewpostor) || target.Is(CustomRoles.Convict)) && !target.Data.IsDead)
                        TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Executioner), "★"));
                } */


                //球状闪电提示
                if (BallLightning.IsGhost(target))
                    TargetMark.Append(ColorString(GetRoleColor(CustomRoles.BallLightning), "■"));

                //タスク完了直前のSnitchにマークを表示
                TargetMark.Append(Snitch.GetWarningMark(seer, target));

                //ハートマークを付ける(相手に)
                if (seer.Is(CustomRoles.Lovers) && target.Is(CustomRoles.Lovers))
                {
                    TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Lovers)}>♥</color>");
                }
                //霊界からラバーズ視認
                else if (seer.Data.IsDead && !seer.Is(CustomRoles.Lovers) && target.Is(CustomRoles.Lovers))
                {
                    TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Lovers)}>♥</color>");
                }
                else if (target.Is(CustomRoles.Ntr) || seer.Is(CustomRoles.Ntr))
                {
                    TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Lovers)}>♥</color>");
                }

                if (seer.Is(CustomRoles.PlagueBearer))
                {
                    if (PlagueBearer.isPlagued(seer.PlayerId, target.PlayerId))
                    {
                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.PlagueBearer)}>●</color>");
                        PlagueBearer.SendRPC(seer, target);
                    }
                }

                if (seer.Is(CustomRoles.Arsonist))//seerがアーソニストの時
                {
                    if (seer.IsDousedPlayer(target)) //seerがtargetに既にオイルを塗っている(完了)
                    {
                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Arsonist)}>▲</color>");
                    }
                    if (
                        Main.ArsonistTimer.TryGetValue(seer.PlayerId, out var ar_kvp) && //seerがオイルを塗っている途中(現在進行)
                        ar_kvp.Item1 == target //オイルを塗っている対象がtarget
                    )
                    {
                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Arsonist)}>△</color>");
                    }
                }
                if (seer.Is(CustomRoles.Revolutionist))//seer是革命家时
                {
                    if (seer.IsDrawPlayer(target)) //seer已完成拉拢船员
                    {
                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Revolutionist)}>●</color>");
                    }
                    if (Main.RevolutionistTimer.TryGetValue(seer.PlayerId, out var ar_kvp) && ar_kvp.Item1 == target)//seer正在拉拢船员
                    {
                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Revolutionist)}>○</color>");
                    }
                }
                if (seer.Is(CustomRoles.Farseer))//seerがアーソニストの時
                {
                    if (
                        Main.FarseerTimer.TryGetValue(seer.PlayerId, out var ar_kvp) && //seerがオイルを塗っている途中(現在進行)
                        ar_kvp.Item1 == target //オイルを塗っている対象がtarget
                    )
                    {
                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Farseer)}>○</color>");
                    }
                }
                if (seer.Is(CustomRoles.Puppeteer) &&
                Main.PuppeteerList.ContainsValue(seer.PlayerId) &&
                Main.PuppeteerList.ContainsKey(target.PlayerId))
                    TargetMark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Impostor)}>◆</color>");
                if (seer.Is(CustomRoles.NWitch) &&
                    Main.TaglockedList.ContainsValue(seer.PlayerId) &&
                    Main.TaglockedList.ContainsKey(target.PlayerId))
                    TargetMark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.NWitch)}>◆</color>");

                    //他人の役職とタスクは幽霊が他人の役職を見れるようになっていてかつ、seerが死んでいる場合のみ表示されます。それ以外の場合は空になります。
                    string TargetRoleText =
                        (seer.Data.IsDead && Options.GhostCanSeeOtherRoles.GetBool()) ||
                        (seer.Is(CustomRoles.Mimic) && target.Data.IsDead && Options.MimicCanSeeDeadRoles.GetBool()) ||
                        (target.Is(CustomRoles.Gravestone) && target.Data.IsDead) ||
                        (seer.Is(CustomRoles.Lovers) && target.Is(CustomRoles.Lovers) && Options.LoverKnowRoles.GetBool()) ||
                        (target.Is(CustomRoles.Ntr) && Options.LoverKnowRoles.GetBool()) ||
                        (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoleTypes.Impostor) && Options.ImpKnowAlliesRole.GetBool()) ||
                        (seer.Is(CustomRoles.Madmate) && target.Is(CustomRoleTypes.Impostor) && Options.MadmateKnowWhosImp.GetBool()) ||
                        (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Madmate) && Options.ImpKnowWhosMadmate.GetBool()) ||
                        (seer.Is(CustomRoles.Crewpostor) && target.Is(CustomRoleTypes.Impostor) && Options.CrewpostorKnowsAllies.GetBool()) ||
                        (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Crewpostor) && Options.AlliesKnowCrewpostor.GetBool()) ||
                        (seer.Is(CustomRoles.Madmate) && target.Is(CustomRoles.Madmate) && Options.MadmateKnowWhosMadmate.GetBool()) ||
                        (seer.Is(CustomRoles.Rogue) && target.Is(CustomRoles.Rogue) && Options.RogueKnowEachOther.GetBool() && Options.RogueKnowEachOtherRoles.GetBool()) ||
                        (seer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Sidekick)) ||
                        (seer.Is(CustomRoles.Jackal) && target.Is(CustomRoles.Sidekick)) ||
                        (seer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Jackal))||
                        (seer.Is(CustomRoles.Recruit) && target.Is(CustomRoles.Jackal))||
                        (target.Is(CustomRoles.Workaholic) && Options.WorkaholicVisibleToEveryone.GetBool()) ||
                        (target.Is(CustomRoles.Doctor) && !target.GetCustomRole().IsEvilAddons() && Options.DoctorVisibleToEveryone.GetBool()) ||
                        (target.Is(CustomRoles.Mayor) && Options.MayorRevealWhenDoneTasks.GetBool() && target.GetPlayerTaskState().IsTaskFinished) ||
                        (seer.Is(CustomRoleTypes.Crewmate) && target.Is(CustomRoles.Marshall) && target.GetPlayerTaskState().IsTaskFinished) ||
                        (Totocalcio.KnowRole(seer, target)) ||
                        (Romantic.KnowRole(seer, target)) ||
                        (Lawyer.KnowRole(seer, target)) ||
                        (EvilDiviner.IsShowTargetRole(seer, target)) ||
                        (Ritualist.IsShowTargetRole(seer, target)) ||
                        (Executioner.KnowRole(seer, target)) ||
                        (Succubus.KnowRole(seer, target)) ||
                        (CursedSoul.KnowRole(seer, target)) ||
                        (Admirer.KnowRole(seer, target)) ||
                        (Amnesiac.KnowRole(seer, target)) ||
                        (Infectious.KnowRole(seer, target)) ||
                        (Virus.KnowRole(seer, target)) ||
                        (seer.IsRevealedPlayer(target) && !target.Is(CustomRoles.Trickster)) ||
                        (seer.Is(CustomRoles.God)) ||
                        (target.Is(CustomRoles.GM))
                        ? $"<size={fontSize}>{target.GetDisplayRoleName(seer.PlayerId != target.PlayerId && !seer.Data.IsDead)}{GetProgressText(target)}</size>\r\n" : "";

                if (!seer.Data.IsDead && seer.IsRevealedPlayer(target) && target.Is(CustomRoles.Trickster))
                {
                    TargetRoleText = Farseer.RandomRole[seer.PlayerId];
                    TargetRoleText += Farseer.GetTaskState();
                }

                if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
                    TargetRoleText = $"<size={fontSize}>{GetProgressText(target)}</size>\r\n";

                if (seer.Is(CustomRoles.EvilTracker))
                {
                    TargetMark.Append(EvilTracker.GetTargetMark(seer, target));
                    if (isForMeeting && EvilTracker.IsTrackTarget(seer, target) && EvilTracker.CanSeeLastRoomInMeeting)
                        TargetRoleText = $"<size={fontSize}>{EvilTracker.GetArrowAndLastRoom(seer, target)}</size>\r\n";
                }

                if (seer.Is(CustomRoles.Tracker))
                {
                    TargetMark.Append(Tracker.GetTargetMark(seer, target));
                    if (isForMeeting && Tracker.IsTrackTarget(seer, target) && Tracker.CanSeeLastRoomInMeeting)
                        TargetRoleText = $"<size={fontSize}>{Tracker.GetArrowAndLastRoom(seer, target)}</size>\r\n";
                }

                //RealNameを取得 なければ現在の名前をRealNamesに書き込む
                string TargetPlayerName = target.GetRealName(isForMeeting);

                if (seer.Is(CustomRoles.Psychic) && seer.IsAlive() && target.IsRedForPsy(seer) && isForMeeting)
                {
                    TargetPlayerName = ColorString(GetRoleColor(CustomRoles.Impostor), TargetPlayerName);
                }
                if (seer.Is(CustomRoles.Mafia) && !seer.IsAlive() && target.IsAlive())
                {
                    TargetPlayerName = ColorString(GetRoleColor(CustomRoles.Mafia), target.PlayerId.ToString()) + " " + TargetPlayerName;
                }
                if (seer.Is(CustomRoles.Retributionist) && !seer.IsAlive() && target.IsAlive())
                {
                    TargetPlayerName = ColorString(GetRoleColor(CustomRoles.Retributionist), target.PlayerId.ToString()) + " " + TargetPlayerName;
                }
                if (seer.Is(CustomRoles.Judge))
                {
                    if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting)
                    {
                        TargetPlayerName = ColorString(GetRoleColor(CustomRoles.Judge), target.PlayerId.ToString()) + " " + TargetPlayerName;
                    }
                }
                if (seer.Is(CustomRoles.ParityCop))
                {
                    if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting)
                    {
                        TargetPlayerName = ColorString(GetRoleColor(CustomRoles.ParityCop), target.PlayerId.ToString()) + " " + TargetPlayerName;
                    }
                }
                if (seer.Is(CustomRoles.Councillor))
                {
                    if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting)
                    {
                        TargetPlayerName = ColorString(GetRoleColor(CustomRoles.Councillor), target.PlayerId.ToString()) + " " + TargetPlayerName;
                    }
                }

                // Guesser Mode ID
                if (Options.GuesserMode.GetBool())
                {
                    //Crewmates
                    if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting && !seer.Is(CustomRoles.Judge) && !seer.Is(CustomRoles.ParityCop) && Options.CrewmatesCanGuess.GetBool() && seer.GetCustomRole().IsCrewmate())
                    {
                        TargetPlayerName = ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + " " + TargetPlayerName;
                    }
                    else if (seer.Is(CustomRoles.NiceGuesser) && !Options.CrewmatesCanGuess.GetBool())
                    {
                        if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting)
                        {
                            TargetPlayerName = ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + " " + TargetPlayerName;
                        }
                    }

                    //Impostors
                    if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting && !seer.Is(CustomRoles.Councillor) && !seer.Is(CustomRoles.Mafia) && Options.ImpostorsCanGuess.GetBool() && seer.GetCustomRole().IsImpostor())
                    {
                        TargetPlayerName = ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + " " + TargetPlayerName;
                    }
                    else if (seer.Is(CustomRoles.EvilGuesser) && !Options.ImpostorsCanGuess.GetBool())
                    {
                        if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting)
                        {
                            TargetPlayerName = ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + " " + TargetPlayerName;
                        }
                    }

                    // Neutrals
                    if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting && Options.NeutralKillersCanGuess.GetBool() && seer.GetCustomRole().IsNK())
                    {
                        TargetPlayerName = ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + " " + TargetPlayerName;
                    }
                    if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting && Options.PassiveNeutralsCanGuess.GetBool() && seer.GetCustomRole().IsNonNK() && !seer.Is(CustomRoles.Doomsayer))
                    {
                        TargetPlayerName = ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + " " + TargetPlayerName;
                    }
                }
                else // Off Guesser Mode ID
                {
                    if (seer.Is(CustomRoles.NiceGuesser) || seer.Is(CustomRoles.EvilGuesser) || seer.Is(CustomRoles.Doomsayer) || seer.Is(CustomRoles.Guesser))
                    {
                        if (seer.IsAlive() && target.IsAlive() && GuesserIsForMeeting)
                        {
                            TargetPlayerName = ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + " " + TargetPlayerName;
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
    //            if (seer.Is(CustomRoles.Monarch) && target.Is(CustomRoles.Knighted))
     //               TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Knighted), " 亗"));
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

                //KB目标玩家名字后缀
                TargetSuffix.Clear();

                if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
                    TargetSuffix.Append(SoloKombatManager.GetDisplayHealth(target));

                string TargetDeathReason = "";
                if (seer.KnowDeathReason(target))
                    TargetDeathReason = $"({ColorString(GetRoleColor(CustomRoles.Doctor), GetVitalText(target.PlayerId))})";

                if (((IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool()) || Camouflager.IsActive) && !CamouflageIsForMeeting)
                    TargetPlayerName = $"<size=0%>{TargetPlayerName}</size>";

                bool targetDevoured = Devourer.HideNameOfConsumedPlayer.GetBool() && Devourer.PlayerSkinsCosumed.Any(a => a.Value.Contains(target.PlayerId));
                if (targetDevoured && !CamouflageIsForMeeting)
                    TargetPlayerName = GetString("DevouredName");

                //全てのテキストを合成します。
                string TargetName = $"{TargetRoleText}{TargetPlayerName}{TargetDeathReason}{TargetMark}";
                TargetName += (TargetSuffix.ToString() == "" ? "" : ("\r\n" + TargetSuffix.ToString()));

                //適用
                target.RpcSetNamePrivate(TargetName, true, seer, force: NoCache);

                logger.Info("NotifyRoles-Loop2-" + target.GetNameWithRole() + ":END");
            }
            logger.Info("NotifyRoles-Loop1-" + seer.GetNameWithRole() + ":END");
        }
    }
    public static void MarkEveryoneDirtySettings()
    {
        PlayerGameOptionsSender.SetDirtyToAll();
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
            foreach (var pid in Main.KilledDiseased.Keys)
            {
                Main.KilledDiseased[pid] = 0;
                Utils.GetPlayerById(pid).ResetKillCooldown();
            }
            Main.KilledDiseased.Clear();
        }
            //Main.KilledDiseased.Clear();
        if (Options.AntidoteCDReset.GetBool())
        {
            foreach (var pid in Main.KilledAntidote.Keys)
            {
                Main.KilledAntidote[pid] = 0;
                Utils.GetPlayerById(pid).ResetKillCooldown();
            }
            Main.KilledAntidote.Clear();
        }
        Swooper.AfterMeetingTasks();
        Wraith.AfterMeetingTasks();
        Chameleon.AfterMeetingTasks();
        NiceEraser.AfterMeetingTasks();
        Eraser.AfterMeetingTasks();
        BountyHunter.AfterMeetingTasks();
        EvilTracker.AfterMeetingTasks();
        SerialKiller.AfterMeetingTasks();
        Spiritualist.AfterMeetingTasks();
        Vulture.AfterMeetingTasks();
        Baker.AfterMeetingTasks();
        CopyCat.AfterMeetingTasks();
        //Pirate.AfterMeetingTask();


        if (Options.AirshipVariableElectrical.GetBool())
            AirshipElectricalDoors.Initialize();
     /*   if (Options.DiseasedCDReset.GetBool())
            Main.KilledDiseased.Clear();
        if (Options.AntidoteCDReset.GetBool())
            Main.KilledAntidote.Clear(); */

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
            case CustomRoles.CyberStar:
                if (GameStates.IsMeeting)
                {
                    //网红死亡消息提示
                    foreach (var pc in Main.AllPlayerControls)
                    {
                        if (!Options.ImpKnowCyberStarDead.GetBool() && pc.GetCustomRole().IsImpostor()) continue;
                        if (!Options.NeutralKnowCyberStarDead.GetBool() && pc.GetCustomRole().IsNeutral()) continue;
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

        if (Romantic.BetPlayer.TryGetValue(target.PlayerId, out var bet))
            Romantic.ChangeRole(bet);

        if (Executioner.Target.ContainsValue(target.PlayerId))
            Executioner.ChangeRoleByTarget(target);
        if (Lawyer.Target.ContainsValue(target.PlayerId))
            Lawyer.ChangeRoleByTarget(target);

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
            Logger.Info("存活内鬼人数:" + AliveImpostorCount + "人", "CountAliveImpostors");
            Main.AliveImpostorCount = AliveImpostorCount;
            LastImpostor.SetSubRole();
        }

        if (sendLog)
        {
            var sb = new StringBuilder(100);
            foreach (var countTypes in Enum.GetValues(typeof(CountTypes)).Cast<CountTypes>())
            {
                var playersCount = PlayersCount(countTypes);
                if (playersCount == 0) continue;
                sb.Append($"{countTypes}:{AlivePlayersCount(countTypes)}/{playersCount}, ");
            }
            sb.Append($"All:{AllAlivePlayersCount}/{AllPlayersCount}");
            Logger.Info(sb.ToString(), "CountAlivePlayers");
        }
    }
    public static string GetVoteName(byte num)
    {
        string name = "invalid";
        var player = GetPlayerById(num);
        if (num < 15 && player != null) name = player?.GetNameWithRole();
        if (num == 253) name = "Skip";
        if (num == 254) name = "None";
        if (num == 255) name = "Dead";
        return name;
    }
    public static string PadRightV2(this object text, int num)
    {
        int bc = 0;
        var t = text.ToString();
        foreach (char c in t) bc += Encoding.GetEncoding("UTF-8").GetByteCount(c.ToString()) == 1 ? 1 : 2;
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
        System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("Explorer.exe")
        { Arguments = "/e,/select," + @filename.Replace("/", "\\") };
        System.Diagnostics.Process.Start(psi);
    }
    public static (int, int) GetDousedPlayerCount(byte playerId)
    {
        int doused = 0, all = 0; //学校で習った書き方
                                 //多分この方がMain.isDousedでforeachするより他のアーソニストの分ループ数少なくて済む
        foreach (var pc in Main.AllAlivePlayerControls)
        {
            if (pc.PlayerId == playerId) continue; //塗れない人は除外 (死んでたり切断済みだったり あとアーソニスト自身も)

            all++;
            if (Main.isDoused.TryGetValue((playerId, pc.PlayerId), out var isDoused) && isDoused)
                //塗れている場合
                doused++;
        }

        return (doused, all);
    }
    public static (int, int) GetDrawPlayerCount(byte playerId, out List<PlayerControl> winnerList)
    {
        int draw = 0;
        int all = Options.RevolutionistDrawCount.GetInt();
        int max = Main.AllAlivePlayerControls.Count();
        if (!Main.PlayerStates[playerId].IsDead) max--;
        winnerList = new();
        if (all > max) all = max;
        foreach (var pc in Main.AllPlayerControls)
        {
            if (Main.isDraw.TryGetValue((playerId, pc.PlayerId), out var isDraw) && isDraw)
            {
                winnerList.Add(pc);
                draw++;
            }
        }
        return (draw, all);
    }
    public static string SummaryTexts(byte id, bool disableColor = true, bool check = false)
    {
        var RolePos = TranslationController.Instance.currentLanguage.languageID is SupportedLangs.English or SupportedLangs.Russian ? 37 : 34;
        var KillsPos = TranslationController.Instance.currentLanguage.languageID is SupportedLangs.English or SupportedLangs.Russian ? 14 : 12;
        var name = Main.AllPlayerNames[id].RemoveHtmlTags().Replace("\r\n", string.Empty);
        if (id == PlayerControl.LocalPlayer.PlayerId) name = DataManager.player.Customization.Name;
        else name = GetPlayerById(id)?.Data.PlayerName ?? name;
        string summary = $"{ColorString(Main.PlayerColors[id], name)}<pos=14%>{GetProgressText(id)}</pos><pos=24%> {GetKillCountText(id)}</pos><pos={22 + KillsPos}%> {GetVitalText(id, true)}</pos><pos={RolePos + KillsPos}%> {GetDisplayRoleName(id, true)}{GetSubRolesText(id, summary: true)}</pos>";
        if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
        {
            if (TranslationController.Instance.currentLanguage.languageID is SupportedLangs.SChinese or SupportedLangs.TChinese)
                summary = $"{GetProgressText(id)}\t<pos=22%>{ColorString(Main.PlayerColors[id], name)}</pos>";
            else summary = $"{ColorString(Main.PlayerColors[id], name)}<pos=30%>{GetProgressText(id)}</pos>";
            if (GetProgressText(id).Trim() == "") return "INVALID";
        }
        return check && GetDisplayRoleName(id, true).RemoveHtmlTags().Contains("INVALID:NotAssigned")
            ? "INVALID"
            : disableColor ? summary.RemoveHtmlTags() : summary;
    }
    public static string RemoveHtmlTagsTemplate(this string str) => Regex.Replace(str, "", "");
    public static string RemoveHtmlTags(this string str) => Regex.Replace(str, "<[^>]*?>", "");
    public static bool CanMafiaKill()
    {
        if (Main.PlayerStates == null) return false;
        //マフィアを除いた生きているインポスターの人数  Number of Living Impostors excluding mafia
        int LivingImpostorsNum = 0;
        foreach (var pc in Main.AllAlivePlayerControls)
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

    public static Dictionary<string, Sprite> CachedSprites = new();
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
    /// Darkness:１の比率で黒色と元の色を混ぜる。マイナスだと白色と混ぜる。
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
        foreach (var num in nums)
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