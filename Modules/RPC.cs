using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using InnerNet;
using System;
using System.Collections.Generic;
using System.Linq;
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

public enum CustomRPC
{
    VersionCheck = 60,
    RequestRetryVersionCheck = 61,
    SyncCustomSettings = 80,
    SetDeathReason,
    EndGame,
    PlaySound,
    SetCustomRole,
    SetBountyTarget,
    SetKillOrSpell,
    SetKillOrHex,
    SetDousedPlayer,
    SetPlaguedPlayer,
    SetNameColorData,
    DoSpell,
    DoHex,
    SniperSync,
    SetLoversPlayers,
    SetExecutionerTarget,
    RemoveExecutionerTarget,
    SetLawyerTarget,
    RemoveLawyerTarget,
    SendFireWorksState,
    SetCurrentDousingTarget,
    SetEvilTrackerTarget,
    SetRealKiller,

    // TOHE
    AntiBlackout,
    PlayCustomSound,
    SetKillTimer,
    SyncAllPlayerNames,
    SyncNameNotify,
    ShowPopUp,
    KillFlash,
    SyncAbilityUseLimit,

    //Roles
    SetDrawPlayer,
    SyncHeadHunter,
    SyncRabbit,
    SyncSoulHunter,
    SyncMycologist,
    SyncBubble,
    AddTornado,
    SyncHookshot,
    SyncStressedTimer,
    SetLibrarianMode,
    SyncYinYanger,
    DruidAddTrigger,
    SyncBenefactorMarkedTask,
    SyncMafiosoData,
    SyncMafiosoPistolCD,
    SyncDamoclesTimer,
    SyncChronomancer,
    StealthDarken,
    PenguinSync,
    SyncPlagueDoctor,
    SetAlchemistPotion,
    SetRicochetTarget,
    SetTetherTarget,
    SetHitmanTarget,
    SetWeaponMasterMode,
    SyncGlitchTimers,
    SyncSpy,
    SetSabotageMasterLimit,
    SetNiceHackerLimit,
    SetCurrentDrawTarget,
    SetCPTasksDone,
    SetGamerHealth,
    SetPelicanEtenNum,
    SwordsManKill,
    SetPursuerSellLimit,
    SetGhostPlayer,
    SetDarkHiderKillCount,
    SetEvilDiviner,
    SetGreedierOE,
    SetImitatorOE,
    SetJinxSpellCount,
    SetCollectorVotes,
    SetQuickShooterShotLimit,
    GuessKill,
    SetMarkedPlayer,
    SetMedicalerProtectList,
    SyncPsychicRedList,
    SetMorticianArrow,
    SetTracefinderArrow,
    SetCleanserCleanLimit,
    SetJailorTarget,
    SetDoppelgangerStealLimit,
    SetJailorExeLimit,
    SetWWTimer,
    SetNiceSwapperVotes,
    Judge,
    Guess,
    MeetingKill,
    MafiaRevenge,
    RetributionistRevenge,
    SetSwooperTimer,
    SetBanditStealLimit,
    SetBKTimer,
    SyncTotocalcioTargetAndTimes,
    SyncRomanticTarget,
    SyncVengefulRomanticTarget,
    SetSuccubusCharmLimit,
    SetVirusInfectLimit,
    SetRevealedPlayer,
    SetCurrentRevealTarget,
    SetJackalRecruitLimit,
    SetBloodhoundArrow,
    SetVultureArrow,
    SetSpiritcallerSpiritLimit,
    SetDoomsayerProgress,
    SetTrackerTarget,
    RpcPassBomb,
    SetAlchemistTimer,

    //SoloKombat
    SyncKBPlayer,
    SyncKBBackCountdown,
    SyncKBNameNotify,
    SetRitualist,
    SetRememberLimit,
    SyncFFAPlayer,
    SyncFFANameNotify
}

public enum Sounds
{
    KillSound,
    TaskComplete,
    TaskUpdateSound,
    ImpTransform,

    Test,
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
internal class RPCHandlerPatch
{
    public static Dictionary<byte, int> ReportDeadBodyRPCs = [];

    public static bool TrustedRpc(byte id)
        => (CustomRPC)id is CustomRPC.VersionCheck or CustomRPC.RequestRetryVersionCheck or CustomRPC.AntiBlackout or CustomRPC.Judge or CustomRPC.SetNiceSwapperVotes or CustomRPC.MeetingKill or CustomRPC.Guess or CustomRPC.MafiaRevenge or CustomRPC.RetributionistRevenge;

    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        var rpcType = (RpcCalls)callId;
        MessageReader subReader = MessageReader.Get(reader);
        if (EAC.ReceiveRpc(__instance, callId, reader)) return false;
        Logger.Info($"From ID: {__instance?.Data?.PlayerId} ({(__instance?.Data?.PlayerId == 0 ? "Host" : __instance?.Data?.PlayerName)}) : {callId} ({RPC.GetRpcName(callId)})", "ReceiveRPC");
        if (callId == 11 && __instance != null)
        {
            if (!ReportDeadBodyRPCs.ContainsKey(__instance.PlayerId)) ReportDeadBodyRPCs.TryAdd(__instance.PlayerId, 0);
            ReportDeadBodyRPCs[__instance.PlayerId]++;
            Logger.Info($"ReportDeadBody RPC count: {ReportDeadBodyRPCs[__instance.PlayerId]}, from {__instance.Data?.PlayerName}", "EAC");
        }

        switch (rpcType)
        {
            case RpcCalls.SetName: //SetNameRPC
                string name = subReader.ReadString();
                if (subReader.BytesRemaining > 0 && subReader.ReadBoolean()) return false;
                Logger.Info("RPC名称修改:" + __instance.GetNameWithRole().RemoveHtmlTags() + " => " + name, "SetName");
                break;
            case RpcCalls.SetRole: //SetNameRPC
                var role = (RoleTypes)subReader.ReadUInt16();
                Logger.Info("RPCSetRole:" + __instance.GetRealName() + " => " + role, "SetRole");
                break;
            case RpcCalls.SendChat:
                var text = subReader.ReadString();
                Logger.Info($"{__instance.GetNameWithRole().RemoveHtmlTags()}:{text}", "ReceiveChat");
                ChatCommands.OnReceiveChat(__instance, text, out var canceled);
                if (canceled) return false;
                break;
            case RpcCalls.StartMeeting:
                var p = Utils.GetPlayerById(subReader.ReadByte());
                Logger.Info($"{__instance.GetNameWithRole().RemoveHtmlTags()} => {p?.GetNameWithRole() ?? "null"}", "StartMeeting");
                break;
            case RpcCalls.Pet:
                Logger.Info($"{__instance.GetNameWithRole().RemoveHtmlTags()} petted their pet", "RpcHandlerPatch");
                break;
            case RpcCalls.SetScanner when Main.HasJustStarted:
                __instance?.Revive();
                return false;
        }

        if (__instance != null && __instance.PlayerId != 0
                               && Enum.IsDefined(typeof(CustomRPC), (int)callId)
                               && !TrustedRpc(callId)) //ホストではなく、CustomRPCで、VersionCheckではない
        {
            Logger.Warn($"{__instance.Data?.PlayerName}:{callId}({RPC.GetRpcName(callId)}) canceled because it was sent by someone other than the host.", "CustomRPC");
            if (!AmongUsClient.Instance.AmHost) return false;
            if (!EAC.ReceiveInvalidRpc(__instance, callId)) return false;

            AmongUsClient.Instance.KickPlayer(__instance.GetClientId(), false);
            Logger.Warn($"The RPC received from {__instance.Data?.PlayerName} is not trusted, so they were kicked.", "Kick");
            Logger.SendInGame(string.Format(GetString("Warning.InvalidRpc"), __instance.Data?.PlayerName));
            return false;
        }

        if (__instance != null && (!ReportDeadBodyRPCs.TryGetValue(__instance.PlayerId, out var times) || times <= 4)) return true;

        AmongUsClient.Instance.KickPlayer(__instance.GetClientId(), true);
        Logger.Warn($"{__instance?.Data?.PlayerName} has sent 5 or more ReportDeadBody RPCs in the last 1 second, they were banned for hacking.", "EAC");
        Logger.SendInGame(string.Format(GetString("Warning.ReportDeadBodyHack"), __instance?.Data?.PlayerName));
        return false;
    }

    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        var rpcType = (CustomRPC)callId;
        switch (rpcType)
        {
            case CustomRPC.AntiBlackout:
                if (Options.EndWhenPlayerBug.GetBool())
                {
                    Logger.Fatal($"{__instance?.Data?.PlayerName}({__instance?.PlayerId}): {reader.ReadString()} - Error, terminate the game according to settings", "Anti-blackout");
                    ChatUpdatePatch.DoBlockChat = true;
                    Main.OverrideWelcomeMsg = string.Format(GetString("RpcAntiBlackOutNotifyInLobby"), __instance?.Data?.PlayerName, GetString("EndWhenPlayerBug"));
                    _ = new LateTask(() => { Logger.SendInGame(string.Format(GetString("RpcAntiBlackOutEndGame"), __instance?.Data?.PlayerName) /*, true*/); }, 3f, "Anti-Black Msg SendInGame");
                    _ = new LateTask(() =>
                    {
                        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Error);
                        GameManager.Instance.LogicFlow.CheckEndCriteria();
                        RPC.ForceEndGame(CustomWinner.Error);
                    }, 5.5f, "Anti-Black End Game");
                }
                else if (GameStates.IsOnlineGame)
                {
                    Logger.Fatal($"{__instance?.Data?.PlayerName}({__instance?.PlayerId}): Change Role Setting Postfix - Error, continue the game according to settings", "Anti-blackout");
                    _ = new LateTask(() => { Logger.SendInGame(string.Format(GetString("RpcAntiBlackOutIgnored"), __instance?.Data?.PlayerName) /*, true*/); }, 3f, "Anti-Black Msg SendInGame");
                }

                break;
            case CustomRPC.VersionCheck:
                try
                {
                    Version version = Version.Parse(reader.ReadString());
                    string tag = reader.ReadString();
                    string forkId = reader.ReadString();
                    Main.playerVersion[__instance.PlayerId] = new(version, tag, forkId);

                    if (Main.VersionCheat.Value && __instance.PlayerId == 0) RPC.RpcVersionCheck();

                    if (Main.VersionCheat.Value && AmongUsClient.Instance.AmHost)
                        Main.playerVersion[__instance.PlayerId] = Main.playerVersion[0];

                    // Kick Unmached Player Start
                    if (AmongUsClient.Instance.AmHost && tag != $"{ThisAssembly.Git.Commit}({ThisAssembly.Git.Branch})")
                    {
                        if (forkId != Main.ForkId)
                            _ = new LateTask(() =>
                            {
                                if (__instance.Data?.Disconnected is not null and not true)
                                {
                                    var msg = string.Format(GetString("KickBecauseDiffrentVersionOrMod"), __instance.Data?.PlayerName);
                                    Logger.Warn(msg, "Version Kick");
                                    Logger.SendInGame(msg);
                                    AmongUsClient.Instance.KickPlayer(__instance.GetClientId(), false);
                                }
                            }, 5f, "Kick");
                    }
                    // Kick Unmached Player End
                }
                catch
                {
                    Logger.Warn($"{__instance?.Data?.PlayerName}({__instance?.PlayerId}): バージョン情報が無効です", "RpcVersionCheck");
                    _ = new LateTask(() =>
                    {
                        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.RequestRetryVersionCheck, SendOption.Reliable, __instance.GetClientId());
                        AmongUsClient.Instance.FinishRpcImmediately(writer);
                    }, 1f, "Retry Version Check Task");
                }

                break;
            case CustomRPC.RequestRetryVersionCheck:
                RPC.RpcVersionCheck();
                break;
            case CustomRPC.SyncCustomSettings:
                if (AmongUsClient.Instance.AmHost) break;

                List<OptionItem> listOptions = [];
                var startAmount = reader.ReadInt32();
                var lastAmount = reader.ReadInt32();

                var countAllOptions = OptionItem.AllOptions.Count;

                // Add Options
                for (var option = startAmount; option < countAllOptions && option <= lastAmount; option++)
                {
                    listOptions.Add(OptionItem.AllOptions[option]);
                }

                var countOptions = listOptions.Count;
                Logger.Msg($"StartAmount: {startAmount} - LastAmount: {lastAmount} ({startAmount}/{lastAmount}) :--: ListOptionsCount: {countOptions} - AllOptions: {countAllOptions} ({countOptions}/{countAllOptions})", "SyncCustomSettings");

                // Sync Settings
                foreach (var option in listOptions.ToArray())
                {
                    option.SetValue(reader.ReadPackedInt32());
                }

                OptionShower.GetText();
                break;
            case CustomRPC.SetDeathReason:
                RPC.GetDeathReason(reader);
                break;
            case CustomRPC.EndGame:
                RPC.EndGame(reader);
                break;
            case CustomRPC.PlaySound:
                byte playerID = reader.ReadByte();
                Sounds sound = (Sounds)reader.ReadByte();
                RPC.PlaySound(playerID, sound);
                break;
            case CustomRPC.ShowPopUp:
                string msg = reader.ReadString();
                HudManager.Instance.ShowPopUp(msg);
                break;
            case CustomRPC.SetCustomRole:
                byte CustomRoleTargetId = reader.ReadByte();
                CustomRoles role = (CustomRoles)reader.ReadPackedInt32();
                RPC.SetCustomRole(CustomRoleTargetId, role);
                break;
            case CustomRPC.SyncAbilityUseLimit:
                var pc = Utils.GetPlayerById(reader.ReadByte());
                pc.SetAbilityUseLimit(reader.ReadSingle(), rpc: false);
                break;
            case CustomRPC.SetBountyTarget:
            {
                byte bountyId = reader.ReadByte();
                byte targetId = reader.ReadByte();
                (Main.PlayerStates[bountyId].Role as BountyHunter)?.ReceiveRPC(bountyId, targetId);
            }
                break;
            case CustomRPC.SetKillOrSpell:
                Witch.ReceiveRPC(reader, false);
                break;
            case CustomRPC.SetKillOrHex:
                HexMaster.ReceiveRPC(reader, false);
                break;

            case CustomRPC.SetCPTasksDone:
                Crewpostor.RecieveRPC(reader);
                break;
            case CustomRPC.SetLibrarianMode:
                Librarian.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncSoulHunter:
                SoulHunter.ReceiveRPC(reader);
                break;
            case CustomRPC.SetDousedPlayer:
                byte ArsonistId = reader.ReadByte();
                byte dousedId = reader.ReadByte();
                bool doused = reader.ReadBoolean();
                Main.isDoused[(ArsonistId, dousedId)] = doused;
                break;
            case CustomRPC.SetPlaguedPlayer:
                PlagueBearer.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncHeadHunter:
                HeadHunter.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncDamoclesTimer:
                Damocles.ReceiveRPC(reader);
                break;
            case CustomRPC.SetDrawPlayer:
                byte RevolutionistId = reader.ReadByte();
                byte DrawId = reader.ReadByte();
                bool drawed = reader.ReadBoolean();
                Main.isDraw[(RevolutionistId, DrawId)] = drawed;
                break;
            case CustomRPC.SetRevealedPlayer:
                byte FarseerId = reader.ReadByte();
                byte RevealId = reader.ReadByte();
                bool revealed = reader.ReadBoolean();
                Main.isRevealed[(FarseerId, RevealId)] = revealed;
                break;
            case CustomRPC.SetNameColorData:
                NameColorManager.ReceiveRPC(reader);
                break;
            case CustomRPC.RpcPassBomb:
                Agitater.ReceiveRPC(reader);
                break;
            case CustomRPC.DoSpell:
                Witch.ReceiveRPC(reader, true);
                break;
            case CustomRPC.DoHex:
                HexMaster.ReceiveRPC(reader, true);
                break;
            case CustomRPC.SetBanditStealLimit:
                Bandit.ReceiveRPC(reader);
                break;
            case CustomRPC.SniperSync:
            {
                byte id = reader.ReadByte();
                (Main.PlayerStates[id].Role as Sniper)?.ReceiveRPC(reader);
            }
                break;
            case CustomRPC.SyncSpy:
                Spy.ReceiveRPC(reader);
                break;
            case CustomRPC.SetAlchemistPotion:
                Alchemist.ReceiveRPCData(reader);
                break;
            case CustomRPC.SetRicochetTarget:
                Ricochet.ReceiveRPCSyncTarget(reader);
                break;
            case CustomRPC.SyncRabbit:
                Rabbit.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncYinYanger:
                YinYanger.ReceiveRPC(reader);
                break;
            case CustomRPC.SetTetherTarget:
                Tether.ReceiveRPCSyncTarget(reader);
                break;
            case CustomRPC.SetHitmanTarget:
            {
                byte hitmanId = reader.ReadByte();
                byte targetId = reader.ReadByte();
                (Main.PlayerStates[hitmanId].Role as Hitman)?.ReceiveRPC(targetId);
            }
                break;
            case CustomRPC.SetWeaponMasterMode:
                WeaponMaster.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncGlitchTimers:
                Glitch.ReceiveRPCSyncTimers(reader);
                break;
            case CustomRPC.DruidAddTrigger:
                Druid.ReceiveRPCAddTrigger(reader);
                break;
            case CustomRPC.SetSabotageMasterLimit:
                SabotageMaster.ReceiveRPC(reader);
                break;
            case CustomRPC.SetNiceHackerLimit:
                NiceHacker.ReceiveRPC(reader);
                break;
            case CustomRPC.SetLoversPlayers:
            {
                Main.LoversPlayers.Clear();
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                    Main.LoversPlayers.Add(Utils.GetPlayerById(reader.ReadByte()));
            }
                break;
            case CustomRPC.SetExecutionerTarget:
                Executioner.ReceiveRPC(reader, SetTarget: true);
                break;
            case CustomRPC.RemoveExecutionerTarget:
                Executioner.ReceiveRPC(reader, SetTarget: false);
                break;
            case CustomRPC.SetLawyerTarget:
                Lawyer.ReceiveRPC(reader, SetTarget: true);
                break;
            case CustomRPC.RemoveLawyerTarget:
                Lawyer.ReceiveRPC(reader, SetTarget: false);
                break;
            case CustomRPC.SendFireWorksState:
            {
                byte id = reader.ReadByte();
                int count = reader.ReadInt32();
                int state = reader.ReadInt32();
                FireWorks.FireWorksState newState = (FireWorks.FireWorksState)state;
                (Main.PlayerStates[id].Role as FireWorks)?.ReceiveRPC(count, newState);
            }
                break;
            case CustomRPC.SyncMycologist:
                Mycologist.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncBubble:
                Bubble.ReceiveRPC(reader);
                break;
            case CustomRPC.SetCurrentDousingTarget:
                byte arsonistId = reader.ReadByte();
                byte dousingTargetId = reader.ReadByte();
                if (PlayerControl.LocalPlayer.PlayerId == arsonistId)
                    Main.currentDousingTarget = dousingTargetId;
                break;
            case CustomRPC.SetCurrentDrawTarget:
                byte arsonistId1 = reader.ReadByte();
                byte doTargetId = reader.ReadByte();
                if (PlayerControl.LocalPlayer.PlayerId == arsonistId1)
                    Main.currentDrawTarget = doTargetId;
                break;
            case CustomRPC.SetEvilTrackerTarget:
                EvilTracker.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncPlagueDoctor:
                PlagueDoctor.ReceiveRPC(reader);
                break;
            case CustomRPC.PenguinSync:
            {
                byte id = reader.ReadByte();
                byte victim = reader.ReadByte();
                (Main.PlayerStates[id].Role as Penguin)?.ReceiveRPC(victim);
            }
                break;
            case CustomRPC.SetRealKiller:
            {
                byte targetId = reader.ReadByte();
                byte killerId = reader.ReadByte();
                RPC.SetRealKiller(targetId, killerId);
            }
                break;
            case CustomRPC.SetGamerHealth:
                Gamer.ReceiveRPC(reader);
                break;
            case CustomRPC.SetPelicanEtenNum:
                Pelican.ReceiveRPC(reader);
                break;
            case CustomRPC.SetDoomsayerProgress:
                Doomsayer.ReceiveRPC(reader);
                break;
            case CustomRPC.SwordsManKill:
                SwordsMan.ReceiveRPC(reader);
                break;
            //case CustomRPC.SetCounterfeiterSellLimit:
            //    Counterfeiter.ReceiveRPC(reader);
            //    break;
            case CustomRPC.SetPursuerSellLimit:
                Pursuer.ReceiveRPC(reader);
                break;
            case CustomRPC.SetJackalRecruitLimit:
                Jackal.ReceiveRPC(reader);
                break;
            case CustomRPC.SetRememberLimit:
                Amnesiac.ReceiveRPC(reader);
                break;
            case CustomRPC.PlayCustomSound:
                CustomSoundsManager.ReceiveRPC(reader);
                break;
            case CustomRPC.SetGhostPlayer:
                BallLightning.ReceiveRPC(reader);
                break;
            case CustomRPC.SetDarkHiderKillCount:
                DarkHide.ReceiveRPC(reader);
                break;
            case CustomRPC.SetGreedierOE:
            {
                byte id = reader.ReadByte();
                bool isOdd = reader.ReadBoolean();
                (Main.PlayerStates[id].Role as Greedier)?.ReceiveRPC(isOdd);
            }
                break;
            case CustomRPC.SetJinxSpellCount:
                byte JinxId = reader.ReadByte();
                int JinxGuardNum = reader.ReadInt32();
                if (Main.JinxSpellCount.ContainsKey(JinxId))
                    Main.JinxSpellCount[JinxId] = JinxGuardNum;
                else
                    Main.JinxSpellCount.Add(JinxId, Jinx.JinxSpellTimes.GetInt());
                break;
            case CustomRPC.SetCollectorVotes:
                Collector.ReceiveRPC(reader);
                break;
            case CustomRPC.SetQuickShooterShotLimit:
                QuickShooter.ReceiveRPC(reader);
                break;
            //case CustomRPC.RestTOHESetting:
            //    OptionItem.AllOptions.ToArray().Where(x => x.Id > 0).Do(x => x.SetValueNoRpc(x.DefaultValue));
            //    OptionShower.GetText();
            //    break;
            case CustomRPC.GuessKill:
                GuessManager.RpcClientGuess(Utils.GetPlayerById(reader.ReadByte()));
                break;
            case CustomRPC.SetMarkedPlayer:
                Assassin.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncChronomancer:
            {
                byte id = reader.ReadByte();
                bool isRampaging = reader.ReadBoolean();
                int chargePercent = reader.ReadInt32();
                long lastUpdate = long.Parse(reader.ReadString());
                (Main.PlayerStates[id].Role as Chronomancer)?.ReceiveRPC(isRampaging, chargePercent, lastUpdate);
            }
                break;
            case CustomRPC.SetMedicalerProtectList:
                Medic.ReceiveRPCForProtectList(reader);
                break;
            case CustomRPC.SyncPsychicRedList:
                Psychic.ReceiveRPC(reader);
                break;
            case CustomRPC.SetKillTimer:
                float time = reader.ReadSingle();
                PlayerControl.LocalPlayer.SetKillTimer(time);
                break;
            case CustomRPC.SyncKBPlayer:
                SoloKombatManager.ReceiveRPCSyncKBPlayer(reader);
                break;
            case CustomRPC.SyncFFAPlayer:
                FFAManager.ReceiveRPCSyncFFAPlayer(reader);
                break;
            case CustomRPC.SyncAllPlayerNames:
                Main.AllPlayerNames = [];
                int num = reader.ReadInt32();
                for (int i = 0; i < num; i++)
                    Main.AllPlayerNames.TryAdd(reader.ReadByte(), reader.ReadString());
                break;
            case CustomRPC.SyncKBBackCountdown:
                SoloKombatManager.ReceiveRPCSyncBackCountdown(reader);
                break;
            case CustomRPC.SyncKBNameNotify:
                SoloKombatManager.ReceiveRPCSyncNameNotify(reader);
                break;
            case CustomRPC.SyncFFANameNotify:
                FFAManager.ReceiveRPCSyncNameNotify(reader);
                break;
            case CustomRPC.SyncMafiosoData:
            {
                byte id = reader.ReadByte();
                (Main.PlayerStates[id].Role as Mafioso)?.ReceiveRPC(reader);
            }
                break;
            case CustomRPC.SyncMafiosoPistolCD:
            {
                byte id = reader.ReadByte();
                (Main.PlayerStates[id].Role as Mafioso)?.ReceiveRPCSyncPistolCD(reader);
            }
                break;
            case CustomRPC.SetMorticianArrow:
                Mortician.ReceiveRPC(reader);
                break;
            case CustomRPC.SetTracefinderArrow:
                Tracefinder.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncNameNotify:
                NameNotifyManager.ReceiveRPC(reader);
                break;
            case CustomRPC.Judge:
                Judge.ReceiveRPC(reader, __instance);
                break;
            case CustomRPC.MeetingKill:
                Councillor.ReceiveRPC(reader, __instance);
                break;
            case CustomRPC.Guess:
                GuessManager.ReceiveRPC(reader, __instance);
                break;
            case CustomRPC.MafiaRevenge:
                Mafia.ReceiveRPC(reader, __instance);
                break;
            //case CustomRPC.RetributionistRevenge:
            //    RetributionistRevengeManager.ReceiveRPC(reader, __instance);
            //    break;
            case CustomRPC.SetSwooperTimer:
            {
                byte id = reader.ReadByte();
                (Main.PlayerStates[id].Role as Swooper)?.ReceiveRPC(reader);
            }
                break;
            case CustomRPC.SetAlchemistTimer:
                Alchemist.ReceiveRPC(reader);
                break;
            case CustomRPC.SetBKTimer:
                Wildling.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncTotocalcioTargetAndTimes:
                Totocalcio.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncRomanticTarget:
                Romantic.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncVengefulRomanticTarget:
                VengefulRomantic.ReceiveRPC(reader);
                break;
            case CustomRPC.SetSuccubusCharmLimit:
                Succubus.ReceiveRPC(reader);
                break;
            //case CustomRPC.SetCursedSoulCurseLimit:
            //    CursedSoul.ReceiveRPC(reader);
            //    break;
            case CustomRPC.SetEvilDiviner:
            {
                byte id = reader.ReadByte();
                byte targetId = reader.ReadByte();
                (Main.PlayerStates[id].Role as EvilDiviner)?.ReceiveRPC(targetId);
            }
                break;
            case CustomRPC.SetRitualist:
                Ritualist.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncHookshot:
                Hookshot.ReceiveRPC(reader);
                break;
            case CustomRPC.AddTornado:
                Tornado.ReceiveRPCAddTornado(reader);
                break;
            case CustomRPC.SetDoppelgangerStealLimit:
                Doppelganger.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncBenefactorMarkedTask:
                Benefactor.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncStressedTimer:
                Stressed.ReceiveRPC(reader);
                break;
            case CustomRPC.SetVirusInfectLimit:
                Virus.ReceiveRPC(reader);
                break;
            case CustomRPC.KillFlash:
                Utils.FlashColor(new Color(1f, 0f, 0f, 0.3f));
                if (Constants.ShouldPlaySfx()) RPC.PlaySound(PlayerControl.LocalPlayer.PlayerId, Sounds.KillSound);
                break;
            case CustomRPC.SetBloodhoundArrow:
                Bloodhound.ReceiveRPC(reader);
                break;
            case CustomRPC.SetVultureArrow:
                Vulture.ReceiveRPC(reader);
                break;
            //case CustomRPC.DoPoison:
            //    Baker.ReceiveRPC(reader);
            //    break;
            case CustomRPC.SetCleanserCleanLimit:
                Cleanser.ReceiveRPC(reader);
                break;
            case CustomRPC.StealthDarken:
                Stealth.ReceiveRPC(reader);
                break;
            case CustomRPC.SetJailorExeLimit:
                Jailor.ReceiveRPC(reader, setTarget: false);
                break;
            case CustomRPC.SetJailorTarget:
                Jailor.ReceiveRPC(reader, setTarget: true);
                break;
            case CustomRPC.SetWWTimer:
            {
                byte id = reader.ReadByte();
                (Main.PlayerStates[id].Role as Werewolf)?.ReceiveRPC(reader);
            }
                break;
            case CustomRPC.SetNiceSwapperVotes:
                NiceSwapper.ReceiveRPC(reader, __instance);
                break;
            case CustomRPC.SetImitatorOE:
                Imitator.ReceiveRPC(reader);
                break;
            case CustomRPC.SetSpiritcallerSpiritLimit:
                Spiritcaller.ReceiveRPC(reader);
                break;
            case CustomRPC.SetTrackerTarget:
                Tracker.ReceiveRPC(reader);
                break;
        }
    }
}

internal static class RPC
{
    //来源：https://github.com/music-discussion/TownOfHost-TheOtherRoles/blob/main/Modules/RPC.cs
    public static void SyncCustomSettingsRPC(int targetId = -1)
    {
        if (targetId != -1)
        {
            var client = Utils.GetClientById(targetId);
            if (client == null || client.Character == null || !Main.playerVersion.ContainsKey(client.Character.PlayerId)) return;
        }

        if (!AmongUsClient.Instance.AmHost || PlayerControl.AllPlayerControls.Count <= 1 || (AmongUsClient.Instance.AmHost == false && PlayerControl.LocalPlayer == null)) return;

        var amount = OptionItem.AllOptions.Count;
        int divideBy = amount / 10;
        for (var i = 0; i <= 10; i++)
        {
            SyncOptionsBetween(i * divideBy, (i + 1) * divideBy, targetId);
        }
    }

    /*
        public static void SyncCustomSettingsRPCforOneOption(OptionItem option)
        {
            List<OptionItem> allOptions = new(OptionItem.AllOptions);
            var placement = allOptions.IndexOf(option);
            if (placement != -1)
                SyncOptionsBetween(placement, placement, OptionItem.AllOptions.Count);
        }
    */
    static void SyncOptionsBetween(int startAmount, int lastAmount, int targetId = -1)
    {
        if (targetId != -1)
        {
            var client = Utils.GetClientById(targetId);
            if (client == null || client.Character == null || !Main.playerVersion.ContainsKey(client.Character.PlayerId))
            {
                return;
            }
        }

        if (!AmongUsClient.Instance.AmHost || PlayerControl.AllPlayerControls.Count <= 1 || (AmongUsClient.Instance.AmHost == false && PlayerControl.LocalPlayer == null)) return;

        var amountAllOptions = OptionItem.AllOptions.Count;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, 80, SendOption.Reliable, targetId);
        writer.Write(startAmount);
        writer.Write(lastAmount);

        List<OptionItem> listOptions = [];

        // Add Options
        for (var option = startAmount; option < amountAllOptions && option <= lastAmount; option++)
        {
            listOptions.Add(OptionItem.AllOptions[option]);
        }

        var countListOptions = listOptions.Count;
        Logger.Msg($"StartAmount: {startAmount} - LastAmount: {lastAmount} ({startAmount}/{lastAmount}) :--: ListOptionsCount: {countListOptions} - AllOptions: {amountAllOptions} ({countListOptions}/{amountAllOptions})", "SyncCustomSettings");

        // Sync Settings
        foreach (var option in listOptions.ToArray())
        {
            writer.WritePacked(option.GetValue());
        }

        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void PlaySoundRPC(byte PlayerID, Sounds sound)
    {
        if (AmongUsClient.Instance.AmHost)
            PlaySound(PlayerID, sound);
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.PlaySound, SendOption.Reliable);
        writer.Write(PlayerID);
        writer.Write((byte)sound);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void SyncAllPlayerNames()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncAllPlayerNames, SendOption.Reliable);
        writer.Write(Main.AllPlayerNames.Count);
        foreach (var name in Main.AllPlayerNames)
        {
            writer.Write(name.Key);
            writer.Write(name.Value);
        }

        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void SendGameData(int clientId = -1)
    {
        MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
        writer.StartMessage((byte)(clientId == -1 ? 5 : 6)); //0x05 GameData
        {
            writer.Write(AmongUsClient.Instance.GameId);
            if (clientId != -1)
                writer.WritePacked(clientId);
            writer.StartMessage(1); //0x01 Data
            {
                writer.WritePacked(GameData.Instance.NetId);
                GameData.Instance.Serialize(writer, true);
            }
            writer.EndMessage();
        }
        writer.EndMessage();

        AmongUsClient.Instance.SendOrDisconnect(writer);
        writer.Recycle();
    }

    public static void ShowPopUp(this PlayerControl pc, string msg)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.ShowPopUp, SendOption.Reliable, pc.GetClientId());
        writer.Write(msg);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ExileAsync(PlayerControl player)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.Exiled, SendOption.Reliable);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
        player.Exiled();
    }

    public static async void RpcVersionCheck()
    {
        while (PlayerControl.LocalPlayer == null) await Task.Delay(500);
        if (Main.playerVersion.ContainsKey(0) || !Main.VersionCheat.Value)
        {
            bool cheating = Main.VersionCheat.Value;
            MessageWriter writer = AmongUsClient.Instance.StartRpc(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.VersionCheck);
            writer.Write(cheating ? Main.playerVersion[0].version.ToString() : Main.PluginVersion);
            writer.Write(cheating ? Main.playerVersion[0].tag : $"{ThisAssembly.Git.Commit}({ThisAssembly.Git.Branch})");
            writer.Write(cheating ? Main.playerVersion[0].forkId : Main.ForkId);
            writer.EndMessage();
        }

        Main.playerVersion[PlayerControl.LocalPlayer.PlayerId] = new PlayerVersion(Main.PluginVersion, $"{ThisAssembly.Git.Commit}({ThisAssembly.Git.Branch})", Main.ForkId);
    }

    public static void SendDeathReason(byte playerId, PlayerState.DeathReason deathReason)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetDeathReason, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write((int)deathReason);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void GetDeathReason(MessageReader reader)
    {
        var playerId = reader.ReadByte();
        var deathReason = (PlayerState.DeathReason)reader.ReadInt32();
        Main.PlayerStates[playerId].deathReason = deathReason;
        Main.PlayerStates[playerId].IsDead = true;
    }

    public static void ForceEndGame(CustomWinner win)
    {
        if (ShipStatus.Instance == null) return;
        try
        {
            CustomWinnerHolder.ResetAndSetWinner(win);
        }
        catch
        {
        }

        if (AmongUsClient.Instance.AmHost)
        {
            ShipStatus.Instance.enabled = false;
            try
            {
                GameManager.Instance.LogicFlow.CheckEndCriteria();
            }
            catch
            {
            }

            try
            {
                GameManager.Instance.RpcEndGame(GameOverReason.ImpostorDisconnect, false);
            }
            catch
            {
            }
        }
    }

    public static void EndGame(MessageReader reader)
    {
        try
        {
            CustomWinnerHolder.ReadFrom(reader);
        }
        catch (Exception ex)
        {
            Logger.Error($"正常にEndGameを行えませんでした。\n{ex}", "EndGame", false);
        }
    }

    public static void PlaySound(byte playerID, Sounds sound)
    {
        if (PlayerControl.LocalPlayer.PlayerId == playerID)
        {
            switch (sound)
            {
                case Sounds.KillSound:
                    SoundManager.Instance.PlaySound(PlayerControl.LocalPlayer.KillSfx, false);
                    break;
                case Sounds.TaskComplete:
                    SoundManager.Instance.PlaySound(DestroyableSingleton<HudManager>.Instance.TaskCompleteSound, false);
                    break;
                case Sounds.TaskUpdateSound:
                    SoundManager.Instance.PlaySound(DestroyableSingleton<HudManager>.Instance.TaskUpdateSound, false);
                    break;
                case Sounds.ImpTransform:
                    SoundManager.Instance.PlaySound(DestroyableSingleton<HnSImpostorScreamSfx>.Instance.HnSOtherImpostorTransformSfx, false, 0.8f);
                    break;
            }
        }
    }

    public static void SetCustomRole(byte targetId, CustomRoles role)
    {
        if (role < CustomRoles.NotAssigned)
        {
            Main.PlayerStates[targetId].SetMainRole(role);
        }
        else
        {
            Main.PlayerStates[targetId].SetSubRole(role);
        }

        HudManager.Instance.SetHudActive(true);
        if (PlayerControl.LocalPlayer.PlayerId == targetId) RemoveDisableDevicesPatch.UpdateDisableDevices();
    }

    public static void SyncLoversPlayers()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetLoversPlayers, SendOption.Reliable);
        writer.Write(Main.LoversPlayers.Count);
        foreach (var lp in Main.LoversPlayers)
        {
            writer.Write(lp.PlayerId);
        }

        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void SendRpcLogger(uint targetNetId, byte callId, int targetClientId = -1)
    {
        if (!DebugModeManager.AmDebugger) return;
        string rpcName = GetRpcName(callId);
        string from = targetNetId.ToString();
        string target = targetClientId.ToString();
        try
        {
            target = targetClientId < 0 ? "All" : AmongUsClient.Instance.GetClient(targetClientId).PlayerName;
            from = Main.AllPlayerControls.FirstOrDefault(c => c.NetId == targetNetId)?.Data?.PlayerName;
        }
        catch
        {
        }

        Logger.Info($"FromNetID: {targetNetId} ({from}) / TargetClientID: {targetClientId} ({target}) / CallID: {callId} ({rpcName})", "SendRPC");
    }

    public static string GetRpcName(byte callId)
    {
        string rpcName;
        if ((rpcName = Enum.GetName(typeof(RpcCalls), callId)) != null)
        {
        }
        else if ((rpcName = Enum.GetName(typeof(CustomRPC), callId)) != null)
        {
        }
        else rpcName = callId.ToString();

        return rpcName;
    }

    public static void SetCurrentDousingTarget(byte arsonistId, byte targetId)
    {
        if (PlayerControl.LocalPlayer.PlayerId == arsonistId)
        {
            Main.currentDousingTarget = targetId;
        }
        else
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetCurrentDousingTarget, SendOption.Reliable);
            writer.Write(arsonistId);
            writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }

    public static void SetCurrentDrawTarget(byte arsonistId, byte targetId)
    {
        if (PlayerControl.LocalPlayer.PlayerId == arsonistId)
        {
            Main.currentDrawTarget = targetId;
        }
        else
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetCurrentDrawTarget, SendOption.Reliable);
            writer.Write(arsonistId);
            writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }

    public static void SetCurrentRevealTarget(byte arsonistId, byte targetId)
    {
        if (PlayerControl.LocalPlayer.PlayerId == arsonistId)
        {
            Main.currentDrawTarget = targetId;
        }
        else
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetCurrentRevealTarget, SendOption.Reliable);
            writer.Write(arsonistId);
            writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }

    public static void SendRPCJinxSpellCount(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetJinxSpellCount, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write(Main.JinxSpellCount[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ResetCurrentDousingTarget(byte arsonistId) => SetCurrentDousingTarget(arsonistId, 255);
    public static void ResetCurrentDrawTarget(byte arsonistId) => SetCurrentDrawTarget(arsonistId, 255);
    public static void ResetCurrentRevealTarget(byte arsonistId) => SetCurrentRevealTarget(arsonistId, 255);

    public static void SetRealKiller(byte targetId, byte killerId)
    {
        var state = Main.PlayerStates[targetId];
        state.RealKiller.TIMESTAMP = DateTime.Now;
        state.RealKiller.ID = killerId;

        if (!AmongUsClient.Instance.AmHost) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetRealKiller, SendOption.Reliable);
        writer.Write(targetId);
        writer.Write(killerId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.StartRpc))]
internal class StartRpcPatch
{
    public static void Prefix( /*InnerNet.InnerNetClient __instance,*/ [HarmonyArgument(0)] uint targetNetId, [HarmonyArgument(1)] byte callId)
    {
        RPC.SendRpcLogger(targetNetId, callId);
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.StartRpcImmediately))]
internal class StartRpcImmediatelyPatch
{
    public static void Prefix( /*InnerNet.InnerNetClient __instance,*/ [HarmonyArgument(0)] uint targetNetId, [HarmonyArgument(1)] byte callId, [HarmonyArgument(3)] int targetClientId = -1)
    {
        RPC.SendRpcLogger(targetNetId, callId, targetClientId);
    }
}