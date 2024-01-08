using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
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
using static TOHE.Translator;

namespace TOHE;

enum CustomRPC
{
    VersionCheck = 60,
    RequestRetryVersionCheck = 61,
    SyncCustomSettings = 80,
    SetDeathReason,
    EndGame,
    PlaySound,
    SetCustomRole,
    SetBountyTarget,
    SetHHTarget,
    SetKillOrSpell,
    SetKillOrHex,
    SetSheriffShotLimit,
    SetCopyCatMiscopyLimit,
    SetDousedPlayer,
    setPlaguedPlayer,
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
    //RestTOHESetting,
    PlayCustomSound,
    SetKillTimer,
    SyncAllPlayerNames,
    SyncNameNotify,
    ShowPopUp,
    KillFlash,

    //Roles
    SetDrawPlayer,
    SyncKamikazeLimit,
    KamikazeAddTarget,
    SyncMycologist,
    SyncBubble,
    AddTornado,
    RemoveTornado,
    SyncSprayer,
    SyncHookshot,
    SyncSentinel,
    SyncStressedTimer,
    SetLibrarianMode,
    SyncLibrarianList,
    GaulousAddPlayerToList,
    SetGauloisLimit,
    SyncYinYanger,
    SyncAnalyzer,
    SyncAnalyzerTarget,
    SyncCantankerousLimit,
    SyncDuellistTarget,
    SetDruidLimit,
    DruidSyncLastUpdate,
    DruidRemoveTrigger,
    DruidAddTrigger,
    DruidAddTriggerDelay,
    SyncBenefactorMarkedTask,
    SetDrainerLimit,
    SetConsortLimit,
    SetDonutLimit,
    SetEscortLimit,
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
    SetEclipseVision,
    SyncGlitchLongs,
    SyncGlitchTimers,
    SyncGlitchSS,
    SyncGlitchMimic,
    SyncGlitchKill,
    SyncGlitchHack,
    SyncVengeanceTimer,
    SyncVengeanceData,
    SpyRedNameSync,
    SpyAbilitySync,
    SpyRedNameRemove,
    SetDivinatorLimit,
    SetMediumshiperLimit,
    SetOracleLimit,
    SetSabotageMasterLimit,
    SetDoormasterLimit,
    SetRicochetLimit,
    SetTetherLimit,
    SetNiceHackerLimit,
    SetCameraManLimit,
    BloodhoundIncreaseAbilityUseByOne,
    SetChameleonLimit,
    SetCurrentDrawTarget,
    SetCPTasksDone,
    SetGamerHealth,
    SetPelicanEtenNum,
    SwordsManKill,
    //SetCounterfeiterSellLimit,
    SetPursuerSellLimit,
    SetMedicalerProtectLimit,
    SetGangsterRecruitLimit,
    SetGhostPlayer,
    SetDarkHiderKillCount,
    SetEvilDiviner,
    SetGreedierOE,
    SetImitatorOE,
    SetCursedWolfSpellCount,
    SetJinxSpellCount,
    SetCollectorVotes,
    SetQuickShooterShotLimit,
    SetEraseLimit,
    GuessKill,
    SetMarkedPlayer,
    SetMarkedPlayerV2,
    SetConcealerTimer,
    SetMedicalerProtectList,
    SetHackerHackLimit,
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
    SetWraithTimer,
    SetBKTimer,
    SyncTotocalcioTargetAndTimes,
    SyncRomanticTarget,
    SyncVengefulRomanticTarget,
    SetSuccubusCharmLimit,
    SetInfectiousBiteLimit,
    SetCursedSoulCurseLimit,
    SetMonarchKnightLimit,
    SetDeputyHandcuffLimit,
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
    SetChameleonTimer,
    DoPoison,
    SetAdmireLimit,
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
        if (callId == 11)
        {
            if (!ReportDeadBodyRPCs.ContainsKey(__instance.PlayerId)) ReportDeadBodyRPCs.TryAdd(__instance.PlayerId, 0);
            ReportDeadBodyRPCs[__instance.PlayerId]++;
            Logger.Info($"ReportDeadBody RPC count: {ReportDeadBodyRPCs[__instance.PlayerId]}, from {__instance?.Data?.PlayerName}", "EAC");
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
                Logger.Info("RPC设置职业:" + __instance.GetRealName() + " => " + role, "SetRole");
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
        }
        if (__instance.PlayerId != 0
            && Enum.IsDefined(typeof(CustomRPC), (int)callId)
            && !TrustedRpc(callId)) //ホストではなく、CustomRPCで、VersionCheckではない
        {
            Logger.Warn($"{__instance?.Data?.PlayerName}:{callId}({RPC.GetRpcName(callId)}) canceled because it was sent by someone other than the host.", "CustomRPC");
            if (AmongUsClient.Instance.AmHost)
            {
                if (!EAC.ReceiveInvalidRpc(__instance, callId)) return false;
                AmongUsClient.Instance.KickPlayer(__instance.GetClientId(), false);
                Logger.Warn($"The RPC received from {__instance?.Data?.PlayerName} is not trusted, so they were kicked.", "Kick");
                Logger.SendInGame(string.Format(GetString("Warning.InvalidRpc"), __instance?.Data?.PlayerName));
            }
            return false;
        }
        if (ReportDeadBodyRPCs.TryGetValue(__instance.PlayerId, out var times) && times > 4)
        {
            AmongUsClient.Instance.KickPlayer(__instance.GetClientId(), true);
            Logger.Warn($"{__instance?.Data?.PlayerName} has sent 5 or more ReportDeadBody RPCs in the last 1 second, they were banned for hacking.", "EAC");
            Logger.SendInGame(string.Format(GetString("Warning.ReportDeadBodyHack"), __instance?.Data?.PlayerName));
            return false;
        }
        return true;
    }
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        var rpcType = (CustomRPC)callId;
        switch (rpcType)
        {
            case CustomRPC.AntiBlackout:
                if (Options.EndWhenPlayerBug.GetBool())
                {
                    Logger.Fatal($"{__instance?.Data?.PlayerName}({__instance.PlayerId}): {reader.ReadString()} - Error, terminate the game according to settings", "Anti-blackout");
                    ChatUpdatePatch.DoBlockChat = true;
                    Main.OverrideWelcomeMsg = string.Format(GetString("RpcAntiBlackOutNotifyInLobby"), __instance?.Data?.PlayerName, GetString("EndWhenPlayerBug"));
                    _ = new LateTask(() =>
                    {
                        Logger.SendInGame(string.Format(GetString("RpcAntiBlackOutEndGame"), __instance?.Data?.PlayerName), true);
                    }, 3f, "Anti-Black Msg SendInGame");
                    _ = new LateTask(() =>
                    {
                        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Error);
                        GameManager.Instance.LogicFlow.CheckEndCriteria();
                        RPC.ForceEndGame(CustomWinner.Error);
                    }, 5.5f, "Anti-Black End Game");
                }
                else if (GameStates.IsOnlineGame)
                {
                    Logger.Fatal($"{__instance?.Data?.PlayerName}({__instance.PlayerId}): Change Role Setting Postfix - Error, continue the game according to settings", "Anti-blackout");
                    _ = new LateTask(() =>
                    {
                        Logger.SendInGame(string.Format(GetString("RpcAntiBlackOutIgnored"), __instance?.Data?.PlayerName), true);
                    }, 3f, "Anti-Black Msg SendInGame");
                }
                break;
            case CustomRPC.VersionCheck:
                try
                {
                    Version version = Version.Parse(reader.ReadString());
                    string tag = reader.ReadString();
                    string forkId = reader.ReadString();
                    Main.playerVersion[__instance.PlayerId] = new PlayerVersion(version, tag, forkId);

                    if (Main.VersionCheat.Value && __instance.PlayerId == 0) RPC.RpcVersionCheck();

                    if (Main.VersionCheat.Value && AmongUsClient.Instance.AmHost)
                        Main.playerVersion[__instance.PlayerId] = Main.playerVersion[0];

                    // Kick Unmached Player Start
                    if (AmongUsClient.Instance.AmHost && tag != $"{ThisAssembly.Git.Commit}({ThisAssembly.Git.Branch})")
                    {
                        if (forkId != Main.ForkId)
                            _ = new LateTask(() =>
                            {
                                if (__instance?.Data?.Disconnected is not null and not true)
                                {
                                    var msg = string.Format(GetString("KickBecauseDiffrentVersionOrMod"), __instance?.Data?.PlayerName);
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
                    Logger.Warn($"{__instance?.Data?.PlayerName}({__instance.PlayerId}): バージョン情報が無効です", "RpcVersionCheck");
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
            case CustomRPC.SetBountyTarget:
                BountyHunter.ReceiveRPC(reader);
                break;
            case CustomRPC.SetKillOrSpell:
                Witch.ReceiveRPC(reader, false);
                break;
            case CustomRPC.SetKillOrHex:
                HexMaster.ReceiveRPC(reader, false);
                break;

            case CustomRPC.SetSheriffShotLimit:
                Sheriff.ReceiveRPC(reader);
                break;
            case CustomRPC.SetCopyCatMiscopyLimit:
                CopyCat.ReceiveRPC(reader);
                break;
            case CustomRPC.SetCPTasksDone:
                RPC.CrewpostorTasksRecieveRPC(reader);
                break;
            case CustomRPC.SetLibrarianMode:
                Librarian.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncLibrarianList:
                Librarian.ReceiveRPCSyncList(reader);
                break;
            case CustomRPC.SetDousedPlayer:
                byte ArsonistId = reader.ReadByte();
                byte DousedId = reader.ReadByte();
                bool doused = reader.ReadBoolean();
                Main.isDoused[(ArsonistId, DousedId)] = doused;
                break;
            case CustomRPC.setPlaguedPlayer:
                PlagueBearer.ReceiveRPC(reader);
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
                Sniper.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncAnalyzer:
                Analyzer.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncAnalyzerTarget:
                Analyzer.ReceiveRPCSyncTarget(reader);
                break;
            case CustomRPC.SpyRedNameSync:
                Spy.ReceiveRPC(reader);
                break;
            case CustomRPC.SpyAbilitySync:
                Spy.ReceiveRPC(reader, isAbility: true);
                break;
            case CustomRPC.SpyRedNameRemove:
                Spy.ReceiveRPC(reader, isRemove: true);
                break;
            case CustomRPC.SetDivinatorLimit:
                Divinator.ReceiveRPC(reader);
                break;
            case CustomRPC.SetAlchemistPotion:
                Alchemist.ReceiveRPCData(reader);
                break;
            case CustomRPC.SetRicochetLimit:
                Ricochet.ReceiveRPC(reader);
                break;
            case CustomRPC.SetRicochetTarget:
                Ricochet.ReceiveRPCSyncTarget(reader);
                break;
            case CustomRPC.SyncYinYanger:
                YinYanger.ReceiveRPC(reader);
                break;
            case CustomRPC.SetTetherTarget:
                Tether.ReceiveRPCSyncTarget(reader);
                break;
            case CustomRPC.SetTetherLimit:
                Tether.ReceiveRPC(reader);
                break;
            case CustomRPC.SetHitmanTarget:
                Hitman.ReceiveRPC(reader);
                break;
            case CustomRPC.SetWeaponMasterMode:
                WeaponMaster.ReceiveRPC(reader);
                break;
            case CustomRPC.SetEclipseVision:
                Eclipse.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncGlitchHack:
                Glitch.ReceiveRPCSyncHack(reader);
                break;
            case CustomRPC.SyncGlitchKill:
                Glitch.ReceiveRPCSyncKill(reader);
                break;
            case CustomRPC.SyncGlitchLongs:
                Glitch.ReceiveRPCSyncLongs(reader);
                break;
            case CustomRPC.SyncGlitchMimic:
                Glitch.ReceiveRPCSyncMimic(reader);
                break;
            case CustomRPC.SyncGlitchSS:
                Glitch.ReceiveRPCSyncSS(reader);
                break;
            case CustomRPC.SyncGlitchTimers:
                Glitch.ReceiveRPCSyncTimers(reader);
                break;
            case CustomRPC.SyncVengeanceData:
                Vengeance.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncVengeanceTimer:
                Vengeance.ReceiveRPCSyncTimer(reader);
                break;
            case CustomRPC.SetMediumshiperLimit:
                Mediumshiper.ReceiveRPC(reader);
                break;
            case CustomRPC.SetOracleLimit:
                Oracle.ReceiveRPC(reader);
                break;
            case CustomRPC.DruidAddTrigger:
                Druid.ReceiveRPCAddTrigger(reader);
                break;
            case CustomRPC.DruidAddTriggerDelay:
                Druid.ReceiveRPCAddTriggerDelay(reader);
                break;
            case CustomRPC.SetDruidLimit:
                Druid.ReceiveRPCSyncAbilityUse(reader);
                break;
            case CustomRPC.DruidSyncLastUpdate:
                Druid.ReceiveRPCSyncLastUpdate(reader);
                break;
            case CustomRPC.DruidRemoveTrigger:
                Druid.ReceiveRPCRemoveTrigger(reader);
                break;
            case CustomRPC.SetSabotageMasterLimit:
                SabotageMaster.ReceiveRPC(reader);
                break;
            case CustomRPC.SetDoormasterLimit:
                Doormaster.ReceiveRPC(reader);
                break;
            case CustomRPC.SetNiceHackerLimit:
                NiceHacker.ReceiveRPC(reader);
                break;
            case CustomRPC.SetCameraManLimit:
                CameraMan.ReceiveRPC(reader);
                break;
            case CustomRPC.BloodhoundIncreaseAbilityUseByOne:
                Bloodhound.ReceiveRPCPlus(reader);
                break;
            case CustomRPC.SetChameleonLimit:
                Chameleon.ReceiveRPCPlus(reader);
                break;
            case CustomRPC.SetLoversPlayers:
                Main.LoversPlayers.Clear();
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                    Main.LoversPlayers.Add(Utils.GetPlayerById(reader.ReadByte()));
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
                FireWorks.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncMycologist:
                Mycologist.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncBubble:
                Bubble.ReceiveRPC(reader);
                break;
            case CustomRPC.KamikazeAddTarget:
                Kamikaze.ReceiveRPCAddTarget(reader);
                break;
            case CustomRPC.SyncKamikazeLimit:
                Kamikaze.ReceiveRPCSyncLimit(reader);
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
                Penguin.ReceiveRPC(reader);
                break;
            case CustomRPC.SetRealKiller:
                byte targetId = reader.ReadByte();
                byte killerId = reader.ReadByte();
                RPC.SetRealKiller(targetId, killerId);
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
            case CustomRPC.SetMedicalerProtectLimit:
                Medic.ReceiveRPC(reader);
                break;
            case CustomRPC.SetGangsterRecruitLimit:
                Gangster.ReceiveRPC(reader);
                break;
            case CustomRPC.SetJackalRecruitLimit:
                Jackal.ReceiveRPC(reader);
                break;
            case CustomRPC.SetAdmireLimit:
                Admirer.ReceiveRPC(reader);
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
                Greedier.ReceiveRPC(reader);
                break;
            case CustomRPC.SetCursedWolfSpellCount:
                byte CursedWolfId = reader.ReadByte();
                int GuardNum = reader.ReadInt32();
                if (Main.CursedWolfSpellCount.ContainsKey(CursedWolfId))
                    Main.CursedWolfSpellCount[CursedWolfId] = GuardNum;
                else
                    Main.CursedWolfSpellCount.Add(CursedWolfId, Options.GuardSpellTimes.GetInt());
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
            case CustomRPC.SetEraseLimit:
                Eraser.ReceiveRPC(reader);
                break;
            case CustomRPC.GuessKill:
                GuessManager.RpcClientGuess(Utils.GetPlayerById(reader.ReadByte()));
                break;
            case CustomRPC.SetMarkedPlayer:
                Assassin.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncChronomancer:
                Chronomancer.ReceiveRPC(reader);
                break;
            case CustomRPC.SetMarkedPlayerV2:
                Undertaker.ReceiveRPC(reader);
                break;
            case CustomRPC.SetMedicalerProtectList:
                Medic.ReceiveRPCForProtectList(reader);
                break;
            case CustomRPC.SetHackerHackLimit:
                Hacker.ReceiveRPC(reader);
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
            case CustomRPC.SetDonutLimit:
                DonutDelivery.ReceiveRPC(reader);
                break;
            case CustomRPC.SetGauloisLimit:
                Gaulois.ReceiveRPC(reader);
                break;
            case CustomRPC.GaulousAddPlayerToList:
                Gaulois.ReceiveRPCAddPlayerToList(reader);
                break;
            case CustomRPC.SetEscortLimit:
                Escort.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncDuellistTarget:
                Duellist.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncMafiosoData:
                Mafioso.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncMafiosoPistolCD:
                Mafioso.ReceiveRPCSyncPistolCD(reader);
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
                MafiaRevengeManager.ReceiveRPC(reader, __instance);
                break;
            //case CustomRPC.RetributionistRevenge:
            //    RetributionistRevengeManager.ReceiveRPC(reader, __instance);
            //    break;
            case CustomRPC.SetSwooperTimer:
                Swooper.ReceiveRPC(reader);
                break;
            case CustomRPC.SetWraithTimer:
                Wraith.ReceiveRPC(reader);
                break;
            case CustomRPC.SetChameleonTimer:
                Chameleon.ReceiveRPC(reader);
                break;
            case CustomRPC.SetAlchemistTimer:
                Alchemist.ReceiveRPC(reader);
                break;
            case CustomRPC.SetBKTimer:
                BloodKnight.ReceiveRPC(reader);
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
            case CustomRPC.SetInfectiousBiteLimit:
                Infectious.ReceiveRPC(reader);
                break;
            case CustomRPC.SetDrainerLimit:
                Drainer.ReceiveRPC(reader);
                break;
            case CustomRPC.SetConsortLimit:
                Consort.ReceiveRPC(reader);
                break;
            case CustomRPC.SetMonarchKnightLimit:
                Monarch.ReceiveRPC(reader);
                break;
            case CustomRPC.SetEvilDiviner:
                EvilDiviner.ReceiveRPC(reader);
                break;
            case CustomRPC.SetRitualist:
                Ritualist.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncSentinel:
                Sentinel.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncHookshot:
                Hookshot.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncSprayer:
                Sprayer.ReceiveRPC(reader);
                break;
            case CustomRPC.AddTornado:
                Tornado.ReceiveRPCAddTornado(reader);
                break;
            case CustomRPC.RemoveTornado:
                Tornado.ReceiveRPCRemoveTornado(reader);
                break;
            case CustomRPC.SetDoppelgangerStealLimit:
                Doppelganger.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncBenefactorMarkedTask:
                Benefactor.ReceiveRPC(reader);
                break;
            case CustomRPC.SetDeputyHandcuffLimit:
                Deputy.ReceiveRPC(reader);
                break;
            case CustomRPC.SyncStressedTimer:
                Stressed.ReceiveRPC(reader);
                break;
            case CustomRPC.SetVirusInfectLimit:
                Virus.ReceiveRPC(reader);
                break;
            case CustomRPC.KillFlash:
                Utils.FlashColor(new(1f, 0f, 0f, 0.3f));
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
            case CustomRPC.SyncCantankerousLimit:
                Cantankerous.ReceiveRPC(reader);
                break;
            case CustomRPC.SetWWTimer:
                Werewolf.ReceiveRPC(reader);
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
            SyncOptionsBetween(i * divideBy, (i + 1) * divideBy, amount, targetId);
        }
    }
    public static void SyncCustomSettingsRPCforOneOption(OptionItem option)
    {
        List<OptionItem> allOptions = new(OptionItem.AllOptions);
        var placement = allOptions.IndexOf(option);
        if (placement != -1)
            SyncOptionsBetween(placement, placement, OptionItem.AllOptions.Count);
    }
    static void SyncOptionsBetween(int startAmount, int lastAmount, int amountAllOptions, int targetId = -1)
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

        if (amountAllOptions != OptionItem.AllOptions.Count) amountAllOptions = OptionItem.AllOptions.Count;

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
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.PlaySound, SendOption.Reliable, -1);
        writer.Write(PlayerID);
        writer.Write((byte)sound);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void SyncAllPlayerNames()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncAllPlayerNames, SendOption.Reliable, -1);
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
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.Exiled, SendOption.Reliable, -1);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
        player.Exiled();
    }
    public static async void RpcVersionCheck()
    {
        while (PlayerControl.LocalPlayer == null) await Task.Delay(500);
        if (Main.playerVersion.ContainsKey(0) || !Main.VersionCheat.Value)
        {
            bool cheating = Main.VersionCheat.Value;
            MessageWriter writer = AmongUsClient.Instance.StartRpc(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.VersionCheck, SendOption.Reliable);
            writer.Write(cheating ? Main.playerVersion[0].version.ToString() : Main.PluginVersion);
            writer.Write(cheating ? Main.playerVersion[0].tag : $"{ThisAssembly.Git.Commit}({ThisAssembly.Git.Branch})");
            writer.Write(cheating ? Main.playerVersion[0].forkId : Main.ForkId);
            writer.EndMessage();
        }
        Main.playerVersion[PlayerControl.LocalPlayer.PlayerId] = new PlayerVersion(Main.PluginVersion, $"{ThisAssembly.Git.Commit}({ThisAssembly.Git.Branch})", Main.ForkId);
    }
    public static void SendDeathReason(byte playerId, PlayerState.DeathReason deathReason)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetDeathReason, SendOption.Reliable, -1);
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
        try { CustomWinnerHolder.ResetAndSetWinner(win); }
        catch { }
        if (AmongUsClient.Instance.AmHost)
        {
            ShipStatus.Instance.enabled = false;
            try { GameManager.Instance.LogicFlow.CheckEndCriteria(); }
            catch { }
            try { GameManager.Instance.RpcEndGame(GameOverReason.ImpostorDisconnect, false); }
            catch { }
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
                    SoundManager.Instance.PlaySound(PlayerControl.LocalPlayer.KillSfx, false, 1f);
                    break;
                case Sounds.TaskComplete:
                    SoundManager.Instance.PlaySound(DestroyableSingleton<HudManager>.Instance.TaskCompleteSound, false, 1f);
                    break;
                case Sounds.TaskUpdateSound:
                    SoundManager.Instance.PlaySound(DestroyableSingleton<HudManager>.Instance.TaskUpdateSound, false, 1f);
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
        else if (role >= CustomRoles.NotAssigned)   //500:NoSubRole 501~:SubRole
        {
            Main.PlayerStates[targetId].SetSubRole(role);
        }
        switch (role)
        {
            case CustomRoles.BountyHunter:
                BountyHunter.Add(targetId);
                break;
            case CustomRoles.SerialKiller:
                SerialKiller.Add(targetId);
                break;
            case CustomRoles.FireWorks:
                FireWorks.Add(targetId);
                break;
            case CustomRoles.TimeThief:
                TimeThief.Add(targetId);
                break;
            case CustomRoles.Sniper:
                Sniper.Add(targetId);
                break;
            case CustomRoles.Crusader:
                Crusader.Add(targetId);
                break;
            /*    case CustomRoles.Mare:
                    Mare.Add(targetId);
                    break; */
            case CustomRoles.EvilTracker:
                EvilTracker.Add(targetId);
                break;
            case CustomRoles.Crewpostor:
                Main.CrewpostorTasksDone[targetId] = 0;
                break;
            case CustomRoles.Witch:
                Witch.Add(targetId);
                break;
            case CustomRoles.Vampire:
                Vampire.Add(targetId);
                break;
            case CustomRoles.Executioner:
                Executioner.Add(targetId);
                break;
            case CustomRoles.Enigma:
                Enigma.Add(targetId);
                break;
            case CustomRoles.Farseer:
                Farseer.Add(targetId);
                break;
            case CustomRoles.Bandit:
                Bandit.Add(targetId);
                break;
            case CustomRoles.Lawyer:
                Lawyer.Add(targetId);
                break;
            case CustomRoles.HexMaster:
                HexMaster.Add(targetId);
                break;
            case CustomRoles.Jackal:
                Jackal.Add(targetId);
                break;
            case CustomRoles.Sidekick:
                Sidekick.Add(targetId);
                break;
            case CustomRoles.Agitater:
                Agitater.Add(targetId);
                break;
            case CustomRoles.Poisoner:
                Poisoner.Add(targetId);
                break;
            case CustomRoles.Sheriff:
                Sheriff.Add(targetId);
                break;
            case CustomRoles.CopyCat:
                CopyCat.Add(targetId);
                break;
            case CustomRoles.QuickShooter:
                QuickShooter.Add(targetId);
                break;
            case CustomRoles.SwordsMan:
                SwordsMan.Add(targetId);
                break;
            case CustomRoles.SabotageMaster:
                SabotageMaster.Add(targetId);
                break;
            case CustomRoles.Cleanser:
                Cleanser.Add(targetId);
                break;
            case CustomRoles.Jailor:
                Jailor.Add(targetId);
                break;
            case CustomRoles.Monitor:
                Monitor.Add(targetId);
                break;
            case CustomRoles.NiceSwapper:
                NiceSwapper.Add(targetId);
                break;
            case CustomRoles.Snitch:
                Snitch.Add(targetId);
                break;
            case CustomRoles.Marshall:
                Marshall.Add(targetId);
                break;
            case CustomRoles.AntiAdminer:
                AntiAdminer.Add(targetId);
                break;
            case CustomRoles.LastImpostor:
                LastImpostor.Add(targetId);
                break;
            case CustomRoles.TimeManager:
                TimeManager.Add(targetId);
                break;
            case CustomRoles.Workhorse:
                Workhorse.Add(targetId);
                break;
            case CustomRoles.Pelican:
                Pelican.Add(targetId);
                break;
            //case CustomRoles.Counterfeiter:
            //    Counterfeiter.Add(targetId);
            //    break;
            case CustomRoles.Tether:
                Tether.Add(targetId);
                break;
            case CustomRoles.Benefactor:
                Benefactor.Add(targetId);
                break;
            case CustomRoles.Librarian:
                Librarian.Add(targetId);
                break;
            case CustomRoles.Escort:
                Escort.Add(targetId);
                break;
            case CustomRoles.Drainer:
                Drainer.Add(targetId);
                break;
            case CustomRoles.Consort:
                Consort.Add(targetId);
                break;
            case CustomRoles.DonutDelivery:
                DonutDelivery.Add(targetId);
                break;
            case CustomRoles.Gaulois:
                Gaulois.Add(targetId);
                break;
            case CustomRoles.Analyzer:
                Analyzer.Add(targetId);
                break;
            case CustomRoles.Aid:
                Aid.Add(targetId);
                break;
            case CustomRoles.Pursuer:
                Pursuer.Add(targetId);
                break;
            case CustomRoles.Gangster:
                Gangster.Add(targetId);
                break;
            case CustomRoles.EvilDiviner:
                EvilDiviner.Add(targetId);
                break;
            case CustomRoles.Kamikaze:
                Kamikaze.Add(targetId);
                break;
            case CustomRoles.Disperser:
                Disperser.Add(targetId);
                break;
            case CustomRoles.Medic:
                Medic.Add(targetId);
                break;
            case CustomRoles.Divinator:
                Divinator.Add(targetId);
                break;
            case CustomRoles.Doormaster:
                Doormaster.Add(targetId);
                break;
            case CustomRoles.Ricochet:
                Ricochet.Add(targetId);
                break;
            case CustomRoles.Oracle:
                Oracle.Add(targetId);
                break;
            case CustomRoles.Gamer:
                Gamer.Add(targetId);
                break;
            case CustomRoles.BallLightning:
                BallLightning.Add(targetId);
                break;
            case CustomRoles.DarkHide:
                DarkHide.Add(targetId);
                break;
            case CustomRoles.Greedier:
                Greedier.Add(targetId);
                break;
            case CustomRoles.Glitch:
                Glitch.Add(targetId);
                break;
            case CustomRoles.Collector:
                Collector.Add(targetId);
                break;
            case CustomRoles.CursedWolf:
                Main.CursedWolfSpellCount[targetId] = Options.GuardSpellTimes.GetInt();
                break;
            case CustomRoles.Jinx:
                Main.JinxSpellCount[targetId] = Jinx.JinxSpellTimes.GetInt();
                Jinx.Add(targetId);
                break;
            case CustomRoles.Eraser:
                Eraser.Add(targetId);
                break;
            case CustomRoles.Spy:
                Spy.Add(targetId);
                break;
            case CustomRoles.Assassin:
                Assassin.Add(targetId);
                break;
            case CustomRoles.Undertaker:
                Undertaker.Add(targetId);
                break;
            case CustomRoles.Sans:
                Sans.Add(targetId);
                break;
            case CustomRoles.Juggernaut:
                Juggernaut.Add(targetId);
                break;
            //case CustomRoles.Reverie:
            //    Reverie.Add(targetId);
            //    break;
            case CustomRoles.Hacker:
                Hacker.Add(targetId);
                break;
            case CustomRoles.Psychic:
                Psychic.Add(targetId);
                break;
            case CustomRoles.Doppelganger:
                Doppelganger.Add(targetId);
                break;
            case CustomRoles.Hangman:
                Hangman.Add(targetId);
                break;
            case CustomRoles.Judge:
                Judge.Add(targetId);
                break;
            case CustomRoles.ParityCop:
                ParityCop.Add(targetId);
                break;
            //case CustomRoles.Baker:
            //    Baker.Add(targetId);
            //    break;
            case CustomRoles.Councillor:
                Councillor.Add(targetId);
                break;
            case CustomRoles.Mortician:
                Mortician.Add(targetId);
                break;
            case CustomRoles.Tracefinder:
                Tracefinder.Add(targetId);
                break;
            case CustomRoles.Mediumshiper:
                Mediumshiper.Add(targetId);
                break;
            case CustomRoles.Veteran:
                Main.VeteranNumOfUsed.Add(targetId, Options.VeteranSkillMaxOfUseage.GetInt());
                break;
            case CustomRoles.Grenadier:
                Main.GrenadierNumOfUsed.Add(targetId, Options.GrenadierSkillMaxOfUseage.GetInt());
                break;
            case CustomRoles.Lighter:
                Main.LighterNumOfUsed.Add(targetId, Options.LighterSkillMaxOfUseage.GetInt());
                break;
            case CustomRoles.SecurityGuard:
                Main.SecurityGuardNumOfUsed.Add(targetId, Options.SecurityGuardSkillMaxOfUseage.GetInt());
                break;
            case CustomRoles.Ventguard:
                Main.VentguardNumberOfAbilityUses = Options.VentguardMaxGuards.GetInt();
                break;
            case CustomRoles.TimeMaster:
                Main.TimeMasterNumOfUsed.Add(targetId, Options.TimeMasterMaxUses.GetInt());
                break;
            case CustomRoles.Swooper:
                Swooper.Add(targetId);
                break;
            case CustomRoles.Wraith:
                Wraith.Add(targetId);
                break;
            case CustomRoles.Chameleon:
                Chameleon.Add(targetId);
                break;
            case CustomRoles.BloodKnight:
                BloodKnight.Add(targetId);
                break;
            case CustomRoles.Totocalcio:
                Totocalcio.Add(targetId);
                break;
            case CustomRoles.Romantic:
                Romantic.Add(targetId);
                break;
            case CustomRoles.VengefulRomantic:
                VengefulRomantic.Add(targetId);
                break;
            case CustomRoles.RuthlessRomantic:
                RuthlessRomantic.Add(targetId);
                break;
            case CustomRoles.Succubus:
                Succubus.Add(targetId);
                break;
            //case CustomRoles.CursedSoul:
            //    CursedSoul.Add(targetId);
            //    break;
            case CustomRoles.Admirer:
                Admirer.Add(targetId);
                break;
            case CustomRoles.Amnesiac:
                Amnesiac.Add(targetId);
                break;
            case CustomRoles.DovesOfNeace:
                Main.DovesOfNeaceNumOfUsed.Add(targetId, Options.DovesOfNeaceMaxOfUseage.GetInt());
                break;
            case CustomRoles.Infectious:
                Infectious.Add(targetId);
                break;
            case CustomRoles.Monarch:
                Monarch.Add(targetId);
                break;
            case CustomRoles.Deputy:
                Deputy.Add(targetId);
                break;
            case CustomRoles.NiceEraser:
                NiceEraser.Add(targetId);
                break;
            case CustomRoles.Nullifier:
                Nullifier.Add(targetId);
                break;
            case CustomRoles.Chronomancer:
                Chronomancer.Add(targetId);
                break;
            case CustomRoles.Virus:
                Virus.Add(targetId);
                break;
            case CustomRoles.Bloodhound:
                Bloodhound.Add(targetId);
                break;
            case CustomRoles.Vulture:
                Vulture.Add(targetId);
                break;
            case CustomRoles.PlagueBearer:
                PlagueBearer.Add(targetId);
                break;
            case CustomRoles.Tracker:
                Tracker.Add(targetId);
                break;
            case CustomRoles.Merchant:
                Merchant.Add(targetId);
                break;
            case CustomRoles.NSerialKiller:
                NSerialKiller.Add(targetId);
                break;
            case CustomRoles.Enderman:
                Enderman.Add(targetId);
                break;
            case CustomRoles.Sentinel:
                Sentinel.Add(targetId);
                break;
            case CustomRoles.Mycologist:
                Mycologist.Add(targetId);
                break;
            case CustomRoles.Bubble:
                Bubble.Add(targetId);
                break;
            case CustomRoles.Tornado:
                Tornado.Add(targetId);
                break;
            case CustomRoles.Hookshot:
                Hookshot.Add(targetId);
                break;
            case CustomRoles.Sprayer:
                Sprayer.Add(targetId);
                break;
            case CustomRoles.PlagueDoctor:
                PlagueDoctor.Add(targetId);
                break;
            case CustomRoles.Penguin:
                Penguin.Add(targetId);
                break;
            case CustomRoles.Stealth:
                Stealth.Add(targetId);
                break;
            case CustomRoles.Magician:
                Magician.Add(targetId);
                break;
            case CustomRoles.Postman:
                Postman.Add(targetId);
                break;
            case CustomRoles.Mafioso:
                Mafioso.Add(targetId);
                break;
            case CustomRoles.WeaponMaster:
                WeaponMaster.Add(targetId);
                break;
            case CustomRoles.Reckless:
                Reckless.Add(targetId);
                break;
            case CustomRoles.Pyromaniac:
                Pyromaniac.Add(targetId);
                break;
            case CustomRoles.Eclipse:
                Eclipse.Add(targetId);
                break;
            case CustomRoles.Vengeance:
                Vengeance.Add(targetId);
                break;
            case CustomRoles.HeadHunter:
                HeadHunter.Add(targetId);
                break;
            case CustomRoles.Imitator:
                Imitator.Add(targetId);
                break;
            case CustomRoles.Ignitor:
                Ignitor.Add(targetId);
                break;
            case CustomRoles.Werewolf:
                Werewolf.Add(targetId);
                break;
            case CustomRoles.Traitor:
                Traitor.Add(targetId);
                break;
            //case CustomRoles.NWitch:
            //    NWitch.Add(targetId);
            //    break;
            case CustomRoles.Maverick:
                Maverick.Add(targetId);
                break;
            case CustomRoles.Dazzler:
                Dazzler.Add(targetId);
                break;
            case CustomRoles.YinYanger:
                YinYanger.Add(targetId);
                break;
            case CustomRoles.Duellist:
                Duellist.Add(targetId);
                break;
            case CustomRoles.Cantankerous:
                Cantankerous.Add(targetId);
                break;
            case CustomRoles.Blackmailer:
                Blackmailer.Add(targetId);
                break;
            case CustomRoles.Druid:
                Druid.Add(targetId);
                break;
            case CustomRoles.GuessManager:
                GuessManagerRole.Add(targetId);
                break;
            case CustomRoles.FFF:
                FFF.Add(targetId);
                break;
            case CustomRoles.Sapper:
                Sapper.Add(targetId);
                break;
            case CustomRoles.CameraMan:
                CameraMan.Add(targetId);
                break;
            case CustomRoles.Hitman:
                Hitman.Add(targetId);
                break;
            case CustomRoles.NiceHacker:
                NiceHacker.Add(targetId);
                break;
            case CustomRoles.Gambler:
                Gambler.Add(targetId);
                break;
            case CustomRoles.RiftMaker:
                RiftMaker.Add(targetId);
                break;
            case CustomRoles.Mastermind:
                Mastermind.Add(targetId);
                break;
            case CustomRoles.Addict:
                Addict.Add(targetId);
                break;
            case CustomRoles.Alchemist:
                Alchemist.Add(targetId);
                break;
            case CustomRoles.Deathpact:
                Deathpact.Add(targetId);
                break;
            case CustomRoles.Wildling:
                Wildling.Add(targetId);
                break;
            case CustomRoles.Morphling:
                Morphling.Add(targetId);
                break;
            case CustomRoles.Pickpocket:
                Pickpocket.Add(targetId);
                break;
            case CustomRoles.Devourer:
                Devourer.Add(targetId);
                break;
            case CustomRoles.Spiritualist:
                Spiritualist.Add(targetId);
                break;
            case CustomRoles.Spiritcaller:
                Spiritcaller.Add(targetId);
                break;
            case CustomRoles.Lurker:
                Lurker.Add(targetId);
                break;
            case CustomRoles.Doomsayer:
                Doomsayer.Add(targetId);
                break;
                //case CustomRoles.Pirate:
                //    Pirate.Add(targetId);
                //    break;
        }
        HudManager.Instance.SetHudActive(true);
        if (PlayerControl.LocalPlayer.PlayerId == targetId) RemoveDisableDevicesPatch.UpdateDisableDevices();
    }
    public static void RpcDoSpell(byte targetId, byte killerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.DoSpell, SendOption.Reliable, -1);
        writer.Write(targetId);
        writer.Write(killerId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void CrewpostorTasksSendRPC(byte cpID, int tasksDone)
    {
        if (PlayerControl.LocalPlayer.PlayerId == cpID)
        {
            if (Main.CrewpostorTasksDone.ContainsKey(cpID))
                Main.CrewpostorTasksDone[cpID] = tasksDone;
            else Main.CrewpostorTasksDone[cpID] = 0;
        }
        else
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetCPTasksDone, SendOption.Reliable, -1);
            writer.Write(cpID);
            writer.Write(tasksDone);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }
    public static void CrewpostorTasksRecieveRPC(MessageReader reader)
    {
        byte PlayerId = reader.ReadByte();
        int tasksDone = reader.ReadInt32();
        if (Main.CrewpostorTasksDone.ContainsKey(PlayerId))
            Main.CrewpostorTasksDone[PlayerId] = tasksDone;
        else
            Main.CrewpostorTasksDone.Add(PlayerId, 0);
    }
    public static void SyncLoversPlayers()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetLoversPlayers, SendOption.Reliable, -1);
        writer.Write(Main.LoversPlayers.Count);
        for (int i = 0; i < Main.LoversPlayers.Count; i++)
        {
            PlayerControl lp = Main.LoversPlayers[i];
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
        catch { }
        Logger.Info($"FromNetID: {targetNetId} ({from}) / TargetClientID: {targetClientId} ({target}) / CallID: {callId} ({rpcName})", "SendRPC");
    }
    public static string GetRpcName(byte callId)
    {
        string rpcName;
        if ((rpcName = Enum.GetName(typeof(RpcCalls), callId)) != null) { }
        else if ((rpcName = Enum.GetName(typeof(CustomRPC), callId)) != null) { }
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
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetCurrentDousingTarget, SendOption.Reliable, -1);
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
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetCurrentDrawTarget, SendOption.Reliable, -1);
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
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetCurrentRevealTarget, SendOption.Reliable, -1);
            writer.Write(arsonistId);
            writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }
    public static void SendRPCCursedWolfSpellCount(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetCursedWolfSpellCount, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(Main.CursedWolfSpellCount[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void SendRPCJinxSpellCount(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetJinxSpellCount, SendOption.Reliable, -1);
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
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetRealKiller, SendOption.Reliable, -1);
        writer.Write(targetId);
        writer.Write(killerId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
}
[HarmonyPatch(typeof(InnerNet.InnerNetClient), nameof(InnerNet.InnerNetClient.StartRpc))]
internal class StartRpcPatch
{
    public static void Prefix(/*InnerNet.InnerNetClient __instance,*/ [HarmonyArgument(0)] uint targetNetId, [HarmonyArgument(1)] byte callId)
    {
        RPC.SendRpcLogger(targetNetId, callId);
    }
}
[HarmonyPatch(typeof(InnerNet.InnerNetClient), nameof(InnerNet.InnerNetClient.StartRpcImmediately))]
internal class StartRpcImmediatelyPatch
{
    public static void Prefix(/*InnerNet.InnerNetClient __instance,*/ [HarmonyArgument(0)] uint targetNetId, [HarmonyArgument(1)] byte callId, [HarmonyArgument(3)] int targetClientId = -1)
    {
        RPC.SendRpcLogger(targetNetId, callId, targetClientId);
    }
}