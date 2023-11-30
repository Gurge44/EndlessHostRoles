using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using TOHE.Modules;
using TOHE.Roles.AddOns.Common;
using TOHE.Roles.AddOns.Crewmate;
using TOHE.Roles.AddOns.Impostor;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using static TOHE.Modules.CustomRoleSelector;
using static TOHE.Translator;

namespace TOHE;

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGame))]
internal class ChangeRoleSettings
{
    public static void Postfix(AmongUsClient __instance)
    {
        Main.OverrideWelcomeMsg = string.Empty;
        try
        {
            //注:この時点では役職は設定されていません。
            Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.GuardianAngel, 0, 0);
            if (Options.DisableVanillaRoles.GetBool())
            {
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Scientist, 0, 0);
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Engineer, 0, 0);
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Shapeshifter, 0, 0);
            }

            Main.PlayerStates = [];

            Main.AllPlayerKillCooldown = [];
            Main.AllPlayerSpeed = [];
            Main.AllPlayerCustomRoles = [];
            Main.WarlockTimer = [];
            Main.AssassinTimer = [];
            Main.UndertakerTimer = [];
            Main.isDoused = [];
            Main.isDraw = [];
            Main.isRevealed = [];
            Main.ArsonistTimer = [];
            Main.RevolutionistTimer = [];
            Main.RevolutionistStart = [];
            Main.RevolutionistLastTime = [];
            Main.RevolutionistCountdown = [];
            Main.TimeMasterBackTrack = [];
            Main.TimeMasterNum = [];
            Main.FarseerTimer = [];
            Main.CursedPlayers = [];
            Main.MafiaRevenged = [];
            Main.RetributionistRevenged = [];
            Main.isCurseAndKill = [];
            Main.isCursed = false;
            Main.PuppeteerList = [];
            Main.PuppeteerDelayList = [];
            Main.TaglockedList = [];
            Main.DetectiveNotify = [];
            Main.ForCrusade = [];
            Main.KillGhoul = [];
            Main.CyberStarDead = [];
            Main.DemolitionistDead = [];
            Main.ExpressSpeedUp = [];
            Main.KilledDiseased = [];
            Main.KilledAntidote = [];
            Main.WorkaholicAlive = [];
            Main.SpeedrunnerAlive = [];
            Main.BaitAlive = [];
            Main.BoobyTrapBody = [];
            Main.KillerOfBoobyTrapBody = [];
            Main.CleanerBodies = [];
            Main.MedusaBodies = [];
            Main.InfectedBodies = [];
            Main.VirusNotify = [];
            Main.CrewpostorTasksDone = [];

            Main.LastEnteredVent = [];
            Main.LastEnteredVentLocation = [];
            Main.EscapeeLocation = [];

            Main.AfterMeetingDeathPlayers = [];
            Main.ResetCamPlayerList = [];
            Main.clientIdList = [];

            Main.CapitalismAddTask = [];
            Main.CapitalismAssignTask = [];
            Main.CheckShapeshift = [];
            Main.ShapeshiftTarget = [];
            Main.SpeedBoostTarget = [];
            Main.MayorUsedButtonCount = [];
            Main.ParaUsedButtonCount = [];
            Main.MarioVentCount = [];
            Main.VeteranInProtect = [];
            Main.VeteranNumOfUsed = [];
            Main.AllKillers = [];
            Main.GrenadierNumOfUsed = [];
            Main.LighterNumOfUsed = [];
            Main.TimeMasterNumOfUsed = [];
            Main.SecurityGuardNumOfUsed = [];
            Main.GrenadierBlinding = [];
            Main.Lighter = [];
            Main.BlockSabo = [];
            Main.BlockedVents = [];
            Main.MadGrenadierBlinding = [];
            Main.CursedWolfSpellCount = [];
            Main.JinxSpellCount = [];
            Main.PuppeteerDelay = [];
            Main.PuppeteerMaxPuppets = [];
            Main.OverDeadPlayerList = [];
            Main.Provoked = [];
            Main.ShieldPlayer = Options.ShieldPersonDiedFirst.GetBool() ? Main.FirstDied : byte.MaxValue;
            Main.FirstDied = byte.MaxValue;
            Main.MadmateNum = 0;
            Main.BardCreations = 0;
            Main.DovesOfNeaceNumOfUsed = [];
            Main.GodfatherTarget = byte.MaxValue;
            ChatManager.ResetHistory();

            ReportDeadBodyPatch.CanReport = [];

            Options.UsedButtonCount = 0;

            GameOptionsManager.Instance.currentNormalGameOptions.ConfirmImpostor = false;
            Main.RealOptionsData = new OptionBackupData(GameOptionsManager.Instance.CurrentGameOptions);

            Main.introDestroyed = false;

            RandomSpawn.CustomNetworkTransformPatch.NumOfTP = [];

            MeetingTimeManager.Init();
            Main.DefaultCrewmateVision = Main.RealOptionsData.GetFloat(FloatOptionNames.CrewLightMod);
            Main.DefaultImpostorVision = Main.RealOptionsData.GetFloat(FloatOptionNames.ImpostorLightMod);

            Main.LastNotifyNames = [];

            Main.currentDousingTarget = byte.MaxValue;
            Main.currentDrawTarget = byte.MaxValue;
            Main.PlayerColors = [];

            //名前の記録
            //Main.AllPlayerNames = new();
            RPC.SyncAllPlayerNames();

            Camouflage.Init();
            var invalidColor = Main.AllPlayerControls.Where(p => p.Data.DefaultOutfit.ColorId < 0 || Palette.PlayerColors.Length <= p.Data.DefaultOutfit.ColorId);
            if (invalidColor.Any())
            {
                var msg = GetString("Error.InvalidColor");
                Logger.SendInGame(msg);
                msg += "\n" + string.Join(",", invalidColor.Select(p => $"{p.name}"));
                Utils.SendMessage(msg);
                Logger.Error(msg, "CoStartGame");
            }

            foreach (PlayerControl target in Main.AllPlayerControls)
            {
                foreach (PlayerControl seer in Main.AllPlayerControls)
                {
                    var pair = (target.PlayerId, seer.PlayerId);
                    Main.LastNotifyNames[pair] = target.name;
                }
            }
            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                var colorId = pc.Data.DefaultOutfit.ColorId;
                if (AmongUsClient.Instance.AmHost && Options.FormatNameMode.GetInt() == 1)
                    pc.RpcSetName(Palette.GetColorName(colorId));
                Main.PlayerStates[pc.PlayerId] = new(pc.PlayerId);
                Main.PlayerColors[pc.PlayerId] = Palette.PlayerColors[colorId];
                Main.AllPlayerSpeed[pc.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                ReportDeadBodyPatch.CanReport[pc.PlayerId] = true;
                ReportDeadBodyPatch.WaitReport[pc.PlayerId] = [];
                pc.cosmetics.nameText.text = pc.name;
                RandomSpawn.CustomNetworkTransformPatch.NumOfTP.Add(pc.PlayerId, 0);
                var outfit = pc.Data.DefaultOutfit;
                Camouflage.PlayerSkins[pc.PlayerId] = new GameData.PlayerOutfit().Set(outfit.PlayerName, outfit.ColorId, outfit.HatId, outfit.SkinId, outfit.VisorId, outfit.PetId, outfit.NamePlateId);
                Main.clientIdList.Add(pc.GetClientId());
            }
            Main.VisibleTasksCount = true;
            if (__instance.AmHost)
            {
                RPC.SyncCustomSettingsRPC();
                Main.RefixCooldownDelay = 0;
            }
            Main.NiceSwapSend = false;
            ShapeshiftPatch.IgnoreNextSS.Clear();
            FallFromLadder.Reset();
            BountyHunter.Init();
            SerialKiller.Init();
            EvilDiviner.Init();
            FireWorks.Init();
            NiceSwapper.Init();
            Pickpocket.Init();
            Sniper.Init();
            Farseer.Init();
            Jailor.Init();
            Monitor.Init();
            Cleanser.Init();
            TimeThief.Init();
            //    Mare.Init();
            Witch.Init();
            HexMaster.Init();
            SabotageMaster.Init();
            Executioner.Init();
            Lawyer.Init();
            Jackal.Init();
            Sidekick.Init();
            Bandit.Init();
            Sheriff.Init();
            CopyCat.Init();
            SwordsMan.Init();
            EvilTracker.Init();
            Snitch.Init();
            Vampire.Init();
            Poisoner.Init();
            AntiAdminer.Init();
            TimeManager.Init();
            LastImpostor.Init();
            TargetArrow.Init();
            LocateArrow.Init();
            DoubleTrigger.Init();
            Workhorse.Init();
            Pelican.Init();
            //Counterfeiter.Init();
            Tether.Init();
            Librarian.Init();
            Benefactor.Init();
            Aid.Init();
            DonutDelivery.Init();
            Gaulois.Init();
            Analyzer.Init();
            Escort.Init();
            Consort.Init();
            Drainer.Init();
            Pursuer.Init();
            Gangster.Init();
            Medic.Init();
            Gamer.Init();
            BallLightning.Init();
            DarkHide.Init();
            Greedier.Init();
            Glitch.Init();
            Collector.Init();
            QuickShooter.Init();
            Camouflager.Init();
            Divinator.Init();
            Doormaster.Init();
            Ricochet.Init();
            Oracle.Init();
            Eraser.Init();
            Spy.Init();
            NiceEraser.Init();
            Assassin.Init();
            Undertaker.Init();
            Sans.Init();
            Juggernaut.Init();
            Hacker.Init();
            NiceHacker.Init();
            Psychic.Init();
            Hangman.Init();
            Judge.Init();
            Councillor.Init();
            Mortician.Init();
            Mediumshiper.Init();
            Swooper.Init();
            Wraith.Init();
            BloodKnight.Init();
            Totocalcio.Init();
            Romantic.Init();
            VengefulRomantic.Init();
            RuthlessRomantic.Init();
            Succubus.Init();
            CursedSoul.Init();
            Admirer.Init();
            Nullifier.Init();
            Deputy.Init();
            Chronomancer.Init();
            Damocles.Initialize();
            Amnesiac.Init();
            Infectious.Init();
            Monarch.Init();
            Virus.Init();
            Bloodhound.Init();
            Tracker.Init();
            Merchant.Init();
            Mastermind.Init();
            NSerialKiller.Init();
            PlagueDoctor.Init();
            Penguin.Init();
            Stealth.Init();
            Postman.Init();
            Mafioso.Init();
            Magician.Init();
            WeaponMaster.Init();
            Reckless.Init();
            Pyromaniac.Init();
            Eclipse.Init();
            Vengeance.Init();
            HeadHunter.Init();
            Imitator.Init();
            Ignitor.Init();
            Werewolf.Init();
            Maverick.Init();
            Jinx.Init();
            DoubleShot.Init();
            Dazzler.Init();
            YinYanger.Init();
            Blackmailer.Init();
            Cantankerous.Init();
            Duellist.Init();
            Druid.Init();
            GuessManagerRole.Init();
            Doppelganger.Init();
            FFF.Init();
            Sapper.Init();
            CameraMan.Init();
            Hitman.Init();
            Gambler.Init();
            RiftMaker.Init();
            Addict.Init();
            Alchemist.Init();
            Deathpact.Init();
            Tracefinder.Init();
            Devourer.Init();
            Ritualist.Init();
            //NWitch.Init();
            Traitor.Init();
            Spiritualist.Init();
            Vulture.Init();
            Chameleon.Init();
            Wildling.Init();
            Morphling.Init();
            ParityCop.Init(); // *giggle* party cop
            //Baker.Init();
            Spiritcaller.Init();
            Enigma.Init();
            Lurker.Init();
            PlagueBearer.Init();
            //Reverie.Init();
            Doomsayer.Init();
            Disperser.Init();
            Twister.Init();
            Agitater.Init();
            //Pirate.Init();


            SoloKombatManager.Init();
            FFAManager.Init();
            CustomWinnerHolder.Reset();
            AntiBlackout.Reset();
            NameNotifyManager.Reset();
            SabotageSystemTypeRepairDamagePatch.Initialize();
            DoorsReset.Initialize();

            IRandom.SetInstanceById(Options.RoleAssigningAlgorithm.GetValue());

            MeetingStates.MeetingCalled = false;
            MeetingStates.FirstMeeting = true;
            GameStates.AlreadyDied = false;
        }
        catch (Exception ex)
        {
            Utils.ErrorEnd("Change Role Setting Postfix");
            Logger.Fatal(ex.ToString(), "Change Role Setting Postfix");
        }
    }
}
[HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
internal class SelectRolesPatch
{
    public static void Prefix()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        try
        {
            //CustomRpcSenderとRpcSetRoleReplacerの初期化
            Dictionary<byte, CustomRpcSender> senders = [];
            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                senders[pc.PlayerId] = new CustomRpcSender($"{pc.name}'s SetRole Sender", SendOption.Reliable, false).StartMessage(pc.GetClientId());
            }
            RpcSetRoleReplacer.StartReplace(senders);

            if (Options.EnableGM.GetBool())
            {
                PlayerControl.LocalPlayer.RpcSetCustomRole(CustomRoles.GM);
                PlayerControl.LocalPlayer.RpcSetRole(RoleTypes.Crewmate);
                PlayerControl.LocalPlayer.Data.IsDead = true;
                Main.PlayerStates[PlayerControl.LocalPlayer.PlayerId].SetDead();
            }


            SelectCustomRoles();
            SelectAddonRoles();
            CalculateVanillaRoleCount();

            //指定原版特殊职业数量
            var roleOpt = Main.NormalOptions.roleOptions;
            int ScientistNum = Options.DisableVanillaRoles.GetBool() ? 0 : roleOpt.GetNumPerGame(RoleTypes.Scientist);
            roleOpt.SetRoleRate(RoleTypes.Scientist, ScientistNum + addScientistNum, addScientistNum > 0 ? 100 : roleOpt.GetChancePerGame(RoleTypes.Scientist));
            int EngineerNum = Options.DisableVanillaRoles.GetBool() ? 0 : roleOpt.GetNumPerGame(RoleTypes.Engineer);
            roleOpt.SetRoleRate(RoleTypes.Engineer, EngineerNum + addEngineerNum, addEngineerNum > 0 ? 100 : roleOpt.GetChancePerGame(RoleTypes.Engineer));
            int ShapeshifterNum = Options.DisableVanillaRoles.GetBool() ? 0 : roleOpt.GetNumPerGame(RoleTypes.Shapeshifter);
            roleOpt.SetRoleRate(RoleTypes.Shapeshifter, ShapeshifterNum + addShapeshifterNum, addShapeshifterNum > 0 ? 100 : roleOpt.GetChancePerGame(RoleTypes.Shapeshifter));

            Dictionary<(byte, byte), RoleTypes> rolesMap = [];

            // 注册反职业
            foreach (var kv in RoleResult.Where(x => x.Value.IsDesyncRole()))
                AssignDesyncRole(kv.Value, kv.Key, senders, rolesMap, BaseRole: kv.Value.GetDYRole());


            MakeDesyncSender(senders, rolesMap);

        }
        catch (Exception e)
        {
            Utils.ErrorEnd("Select Role Prefix");
            Logger.Fatal(e.Message, "Select Role Prefix");
        }
        //以下、バニラ側の役職割り当てが入る
    }

    public static void Postfix()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        try
        {
            List<(PlayerControl, RoleTypes)> newList = [];
            foreach ((PlayerControl PLAYER, RoleTypes ROLETYPE) in RpcSetRoleReplacer.StoragedData.ToArray())
            {
                var kp = RoleResult.FirstOrDefault(x => x.Key.PlayerId == PLAYER.PlayerId);
                newList.Add((PLAYER, kp.Value.GetRoleTypes()));
                if (ROLETYPE == kp.Value.GetRoleTypes())
                    Logger.Warn($"Register original role type => {PLAYER.GetRealName()}: {ROLETYPE}", "Override Role Select");
                else
                    Logger.Warn($"Register original role type => {PLAYER.GetRealName()}: {ROLETYPE} => {kp.Value.GetRoleTypes()}", "Override Role Select");
            }
            if (Options.EnableGM.GetBool()) newList.Add((PlayerControl.LocalPlayer, RoleTypes.Crewmate));
            RpcSetRoleReplacer.StoragedData = newList;

            RpcSetRoleReplacer.Release(); //保存していたSetRoleRpcを一気に書く
            RpcSetRoleReplacer.senders.Do(kvp => kvp.Value.SendMessage());

            // 不要なオブジェクトの削除
            RpcSetRoleReplacer.senders = null;
            RpcSetRoleReplacer.OverriddenSenderList = null;
            RpcSetRoleReplacer.StoragedData = null;

            //Utils.ApplySuffix();

            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                pc.Data.IsDead = false;
                if (Main.PlayerStates[pc.PlayerId].MainRole != CustomRoles.NotAssigned)
                    continue;
                var role = CustomRoles.NotAssigned;
                switch (pc.Data.Role.Role)
                {
                    case RoleTypes.Crewmate:
                        role = CustomRoles.Crewmate;
                        break;
                    case RoleTypes.Impostor:
                        role = CustomRoles.Impostor;
                        break;
                    case RoleTypes.Scientist:
                        role = CustomRoles.Scientist;
                        break;
                    case RoleTypes.Engineer:
                        role = CustomRoles.Engineer;
                        break;
                    case RoleTypes.GuardianAngel:
                        role = CustomRoles.GuardianAngel;
                        break;
                    case RoleTypes.Shapeshifter:
                        role = CustomRoles.Shapeshifter;
                        break;
                    default:
                        Logger.SendInGame(string.Format(GetString("Error.InvalidRoleAssignment"), pc?.Data?.PlayerName));
                        break;
                }
                Main.PlayerStates[pc.PlayerId].SetMainRole(role);
            }

            // 个人竞技模式用
            if (Options.CurrentGameMode == CustomGameMode.SoloKombat || Options.CurrentGameMode == CustomGameMode.FFA)
            {
                foreach (var pair in Main.PlayerStates)
                    ExtendedPlayerControl.RpcSetCustomRole(pair.Key, pair.Value.MainRole);
                goto EndOfSelectRolePatch;
            }

            var rd = IRandom.Instance;

            foreach (var kv in RoleResult)
            {
                if (kv.Value.IsDesyncRole()) continue;
                AssignCustomRole(kv.Value, kv.Key);
            }

            if (CustomRoles.Lovers.IsEnable() && (CustomRoles.FFF.IsEnable() ? -1 : rd.Next(1, 100)) <= Options.LoverSpawnChances.GetInt()) AssignLoversRolesFromList();
            foreach (CustomRoles role in AddonRolesList.ToArray())
            {
                if (rd.Next(1, 100) <= (Options.CustomAdtRoleSpawnRate.TryGetValue(role, out var sc) ? sc.GetFloat() : 0))
                    if (role.IsEnable()) AssignSubRoles(role);
            }

            //RPCによる同期
            foreach (var pair in Main.PlayerStates)
            {
                ExtendedPlayerControl.RpcSetCustomRole(pair.Key, pair.Value.MainRole);

                foreach (CustomRoles subRole in pair.Value.SubRoles.ToArray())
                {
                    ExtendedPlayerControl.RpcSetCustomRole(pair.Key, subRole);
                }
            }

            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                if (pc.Data.Role.Role == RoleTypes.Shapeshifter)
                    Main.CheckShapeshift.Add(pc.PlayerId, false);
                switch (pc.GetCustomRole())
                {
                    case CustomRoles.BountyHunter:
                        BountyHunter.Add(pc.PlayerId);
                        break;
                    case CustomRoles.SerialKiller:
                        SerialKiller.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Bandit:
                        Bandit.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Witch:
                        Witch.Add(pc.PlayerId);
                        break;
                    case CustomRoles.HexMaster:
                        HexMaster.Add(pc.PlayerId);
                        break;
                    case CustomRoles.NiceSwapper:
                        NiceSwapper.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Crusader:
                        Crusader.Add(pc.PlayerId);
                        Crusader.CrusaderLimit[pc.PlayerId] = Crusader.SkillLimitOpt.GetInt();
                        break;
                    case CustomRoles.Warlock:
                        Main.CursedPlayers.Add(pc.PlayerId, null);
                        Main.isCurseAndKill.Add(pc.PlayerId, false);
                        break;
                    case CustomRoles.FireWorks:
                        FireWorks.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Agitater:
                        Agitater.Add(pc.PlayerId);
                        break;
                    case CustomRoles.TimeThief:
                        TimeThief.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Sniper:
                        Sniper.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Vampire:
                        Vampire.Add(pc.PlayerId);
                        break;
                    case CustomRoles.SwordsMan:
                        SwordsMan.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Arsonist:
                        foreach (PlayerControl ar in Main.AllPlayerControls)
                        {
                            Main.isDoused.Add((pc.PlayerId, ar.PlayerId), false);
                        }
                        break;
                    case CustomRoles.Revolutionist:
                        foreach (PlayerControl ar in Main.AllPlayerControls)
                        {
                            Main.isDraw.Add((pc.PlayerId, ar.PlayerId), false);
                        }
                        break;
                    case CustomRoles.Farseer:
                        foreach (PlayerControl ar in Main.AllPlayerControls)
                        {
                            Main.isRevealed.Add((pc.PlayerId, ar.PlayerId), false);
                        }
                        Farseer.RandomRole.Add(pc.PlayerId, Farseer.GetRandomCrewRoleString());
                        Farseer.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Executioner:
                        Executioner.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Lawyer:
                        Lawyer.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Blackmailer:
                        Blackmailer.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Crewpostor:
                        Main.CrewpostorTasksDone[pc.PlayerId] = 0;
                        break;
                    case CustomRoles.Doppelganger:
                        Doppelganger.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Jackal:
                        Jackal.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Sidekick:
                        Sidekick.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Cleanser:
                        Cleanser.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Pickpocket:
                        Pickpocket.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Jailor:
                        Jailor.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Monitor:
                        Monitor.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Poisoner:
                        Poisoner.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Sheriff:
                        Sheriff.Add(pc.PlayerId);
                        break;
                    case CustomRoles.CopyCat:
                        CopyCat.Add(pc.PlayerId);
                        break;
                    case CustomRoles.QuickShooter:
                        QuickShooter.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Mayor:
                        Main.MayorUsedButtonCount[pc.PlayerId] = 0;
                        break;
                    case CustomRoles.TimeMaster:
                        Main.TimeMasterNum[pc.PlayerId] = 0;
                        Main.TimeMasterNumOfUsed.Add(pc.PlayerId, Options.TimeMasterMaxUses.GetInt());
                        break;
                    case CustomRoles.Paranoia:
                        Main.ParaUsedButtonCount[pc.PlayerId] = 0;
                        break;
                    case CustomRoles.SabotageMaster:
                        SabotageMaster.Add(pc.PlayerId);
                        break;
                    case CustomRoles.EvilTracker:
                        EvilTracker.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Snitch:
                        Snitch.Add(pc.PlayerId);
                        break;
                    case CustomRoles.AntiAdminer:
                        AntiAdminer.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Mario:
                        Main.MarioVentCount[pc.PlayerId] = 0;
                        break;
                    case CustomRoles.TimeManager:
                        TimeManager.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Pelican:
                        Pelican.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Tether:
                        Tether.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Benefactor:
                        Benefactor.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Librarian:
                        Librarian.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Aid:
                        Aid.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Escort:
                        Escort.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Consort:
                        Consort.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Drainer:
                        Drainer.Add(pc.PlayerId);
                        break;
                    case CustomRoles.DonutDelivery:
                        DonutDelivery.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Gaulois:
                        Gaulois.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Analyzer:
                        Analyzer.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Pursuer:
                        Pursuer.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Gangster:
                        Gangster.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Medic:
                        Medic.Add(pc.PlayerId);
                        break;
                    case CustomRoles.EvilDiviner:
                        EvilDiviner.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Ritualist:
                        Ritualist.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Divinator:
                        Divinator.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Ricochet:
                        Ricochet.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Doormaster:
                        Doormaster.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Oracle:
                        Oracle.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Gamer:
                        Gamer.Add(pc.PlayerId);
                        break;
                    case CustomRoles.BallLightning:
                        BallLightning.Add(pc.PlayerId);
                        break;
                    case CustomRoles.DarkHide:
                        DarkHide.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Greedier:
                        Greedier.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Glitch:
                        Glitch.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Imitator:
                        Imitator.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Ignitor:
                        Ignitor.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Collector:
                        Collector.Add(pc.PlayerId);
                        break;
                    case CustomRoles.CursedWolf:
                        Main.CursedWolfSpellCount[pc.PlayerId] = Options.GuardSpellTimes.GetInt();
                        break;
                    case CustomRoles.Jinx:
                        Main.JinxSpellCount[pc.PlayerId] = Jinx.JinxSpellTimes.GetInt();
                        Jinx.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Eraser:
                        Eraser.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Spy:
                        Spy.Add(pc.PlayerId);
                        break;
                    case CustomRoles.NiceEraser:
                        NiceEraser.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Assassin:
                        Assassin.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Undertaker:
                        Undertaker.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Sans:
                        Sans.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Juggernaut:
                        Juggernaut.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Hacker:
                        Hacker.Add(pc.PlayerId);
                        break;
                    case CustomRoles.NiceHacker:
                        NiceHacker.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Psychic:
                        Psychic.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Hangman:
                        Hangman.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Judge:
                        Judge.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Councillor:
                        Councillor.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Camouflager:
                        Camouflager.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Twister:
                        Twister.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Mortician:
                        Mortician.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Tracefinder:
                        Tracefinder.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Mediumshiper:
                        Mediumshiper.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Veteran:
                        Main.VeteranNumOfUsed.Add(pc.PlayerId, Options.VeteranSkillMaxOfUseage.GetInt());
                        break;
                    case CustomRoles.Grenadier:
                        Main.GrenadierNumOfUsed.Add(pc.PlayerId, Options.GrenadierSkillMaxOfUseage.GetInt());
                        break;
                    case CustomRoles.Lighter:
                        Main.LighterNumOfUsed.Add(pc.PlayerId, Options.LighterSkillMaxOfUseage.GetInt());
                        break;
                    case CustomRoles.SecurityGuard:
                        Main.SecurityGuardNumOfUsed.Add(pc.PlayerId, Options.SecurityGuardSkillMaxOfUseage.GetInt());
                        break;
                    case CustomRoles.Ventguard:
                        Main.VentguardNumberOfAbilityUses = Options.VentguardMaxGuards.GetInt();
                        break;
                    case CustomRoles.Swooper:
                        Swooper.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Wraith:
                        Wraith.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Chameleon:
                        Chameleon.Add(pc.PlayerId);
                        break;
                    case CustomRoles.BloodKnight:
                        BloodKnight.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Totocalcio:
                        Totocalcio.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Romantic:
                        Romantic.Add(pc.PlayerId);
                        break;
                    case CustomRoles.VengefulRomantic:
                        VengefulRomantic.Add(pc.PlayerId);
                        break;
                    case CustomRoles.RuthlessRomantic:
                        RuthlessRomantic.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Succubus:
                        Succubus.Add(pc.PlayerId);
                        break;
                    case CustomRoles.CursedSoul:
                        CursedSoul.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Admirer:
                        Admirer.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Amnesiac:
                        Amnesiac.Add(pc.PlayerId);
                        break;
                    case CustomRoles.DovesOfNeace:
                        Main.DovesOfNeaceNumOfUsed.Add(pc.PlayerId, Options.DovesOfNeaceMaxOfUseage.GetInt());
                        break;
                    case CustomRoles.Infectious:
                        Infectious.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Monarch:
                        Monarch.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Deputy:
                        Deputy.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Chronomancer:
                        Chronomancer.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Nullifier:
                        Nullifier.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Virus:
                        Virus.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Wildling:
                        Wildling.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Bloodhound:
                        Bloodhound.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Tracker:
                        Tracker.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Merchant:
                        Merchant.Add(pc.PlayerId);
                        break;
                    case CustomRoles.NSerialKiller:
                        NSerialKiller.Add(pc.PlayerId);
                        break;
                    case CustomRoles.PlagueDoctor:
                        PlagueDoctor.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Penguin:
                        Penguin.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Stealth:
                        Stealth.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Postman:
                        Postman.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Mafioso:
                        Mafioso.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Magician:
                        Magician.Add(pc.PlayerId);
                        break;
                    case CustomRoles.WeaponMaster:
                        WeaponMaster.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Reckless:
                        Reckless.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Eclipse:
                        Eclipse.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Pyromaniac:
                        Pyromaniac.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Vengeance:
                        Vengeance.Add(pc.PlayerId);
                        break;
                    case CustomRoles.HeadHunter:
                        HeadHunter.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Werewolf:
                        Werewolf.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Traitor:
                        Traitor.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Maverick:
                        Maverick.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Dazzler:
                        Dazzler.Add(pc.PlayerId);
                        break;
                    case CustomRoles.YinYanger:
                        YinYanger.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Cantankerous:
                        Cantankerous.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Duellist:
                        Duellist.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Druid:
                        Druid.Add(pc.PlayerId);
                        break;
                    case CustomRoles.FFF:
                        FFF.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Sapper:
                        Sapper.Add(pc.PlayerId);
                        break;
                    case CustomRoles.CameraMan:
                        CameraMan.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Hitman:
                        Hitman.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Enigma:
                        Enigma.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Gambler:
                        Gambler.Add(pc.PlayerId);
                        break;
                    case CustomRoles.RiftMaker:
                        RiftMaker.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Mastermind:
                        Mastermind.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Addict:
                        Addict.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Alchemist:
                        Alchemist.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Deathpact:
                        Deathpact.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Morphling:
                        Morphling.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Devourer:
                        Devourer.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Spiritualist:
                        Spiritualist.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Vulture:
                        Vulture.Add(pc.PlayerId);
                        break;
                    case CustomRoles.GuessManager:
                        GuessManagerRole.Add(pc.PlayerId);
                        break;
                    case CustomRoles.PlagueBearer:
                        PlagueBearer.Add(pc.PlayerId);
                        break;
                    case CustomRoles.ParityCop:
                        ParityCop.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Spiritcaller:
                        Spiritcaller.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Lurker:
                        Lurker.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Doomsayer:
                        Doomsayer.Add(pc.PlayerId);
                        break;
                    case CustomRoles.Disperser:
                        Disperser.Add(pc.PlayerId);
                        break;
                }
            }

        EndOfSelectRolePatch:

            HudManager.Instance.SetHudActive(true);
            List<PlayerControl> AllPlayers = [];
            CustomRpcSender sender = CustomRpcSender.Create("SelectRoles Sender", SendOption.Reliable);
            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                pc.ResetKillCooldown();
            }

            //役職の人数を戻す
            var roleOpt = Main.NormalOptions.roleOptions;
            int ScientistNum = Options.DisableVanillaRoles.GetBool() ? 0 : roleOpt.GetNumPerGame(RoleTypes.Scientist);
            ScientistNum -= addScientistNum;
            roleOpt.SetRoleRate(RoleTypes.Scientist, ScientistNum, roleOpt.GetChancePerGame(RoleTypes.Scientist));
            int EngineerNum = Options.DisableVanillaRoles.GetBool() ? 0 : roleOpt.GetNumPerGame(RoleTypes.Engineer);
            EngineerNum -= addEngineerNum;
            roleOpt.SetRoleRate(RoleTypes.Engineer, EngineerNum, roleOpt.GetChancePerGame(RoleTypes.Engineer));
            int ShapeshifterNum = Options.DisableVanillaRoles.GetBool() ? 0 : roleOpt.GetNumPerGame(RoleTypes.Shapeshifter);
            ShapeshifterNum -= addShapeshifterNum;
            roleOpt.SetRoleRate(RoleTypes.Shapeshifter, ShapeshifterNum, roleOpt.GetChancePerGame(RoleTypes.Shapeshifter));

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.Standard:
                    GameEndChecker.SetPredicateToNormal();
                    break;
                case CustomGameMode.SoloKombat:
                    GameEndChecker.SetPredicateToSoloKombat();
                    break;
                case CustomGameMode.FFA:
                    GameEndChecker.SetPredicateToFFA();
                    break;
            }

            GameOptionsSender.AllSenders.Clear();
            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                GameOptionsSender.AllSenders.Add(new PlayerGameOptionsSender(pc));
            }

            // Added players with unclassified roles to the list of players who require ResetCam.
            Main.ResetCamPlayerList.AddRange(Main.AllPlayerControls.Where(p => p.GetCustomRole() is CustomRoles.Arsonist or CustomRoles.Revolutionist or CustomRoles.Sidekick or CustomRoles.KB_Normal or CustomRoles.Killer or CustomRoles.Witness or CustomRoles.Innocent).Select(p => p.PlayerId));
            Utils.CountAlivePlayers(true);
            Utils.SyncAllSettings();
            SetColorPatch.IsAntiGlitchDisabled = false;
        }
        catch (Exception ex)
        {
            Utils.ErrorEnd("Select Role Postfix");
            Logger.Fatal(ex.ToString(), "Select Role Prefix");
        }
    }
    private static void AssignDesyncRole(CustomRoles role, PlayerControl player, Dictionary<byte, CustomRpcSender> senders, Dictionary<(byte, byte), RoleTypes> rolesMap, RoleTypes BaseRole, RoleTypes hostBaseRole = RoleTypes.Crewmate)
    {
        if (player == null) return;

        var hostId = PlayerControl.LocalPlayer.PlayerId;

        Main.PlayerStates[player.PlayerId].SetMainRole(role);

        var selfRole = player.PlayerId == hostId ? hostBaseRole : BaseRole;
        var othersRole = player.PlayerId == hostId ? RoleTypes.Crewmate : RoleTypes.Scientist;

        //Desync役職視点
        foreach (PlayerControl target in Main.AllPlayerControls)
        {
            rolesMap[(player.PlayerId, target.PlayerId)] = player.PlayerId != target.PlayerId ? othersRole : selfRole;
        }

        //他者視点
        foreach (var seer in Main.AllPlayerControls.Where(x => player.PlayerId != x.PlayerId).ToArray())
            rolesMap[(seer.PlayerId, player.PlayerId)] = othersRole;

        RpcSetRoleReplacer.OverriddenSenderList.Add(senders[player.PlayerId]);
        //ホスト視点はロール決定
        player.SetRole(othersRole);
        player.Data.IsDead = true;

        Logger.Info($"Register Modded Role：{player?.Data?.PlayerName} => {role}", "AssignRoles");
    }
    public static void MakeDesyncSender(Dictionary<byte, CustomRpcSender> senders, Dictionary<(byte, byte), RoleTypes> rolesMap)
    {
        foreach (PlayerControl seer in Main.AllPlayerControls)
        {
            var sender = senders[seer.PlayerId];
            foreach (PlayerControl target in Main.AllPlayerControls)
            {
                if (rolesMap.TryGetValue((seer.PlayerId, target.PlayerId), out var role))
                {
                    sender.RpcSetRole(seer, role, target.GetClientId());
                }
            }
        }
    }

    private static void AssignCustomRole(CustomRoles role, PlayerControl player)
    {
        if (player == null) return;
        SetColorPatch.IsAntiGlitchDisabled = true;

        Main.PlayerStates[player.PlayerId].SetMainRole(role);
        Logger.Info($"Register Modded Role：{player?.Data?.PlayerName} => {role}", "AssignRoles");

        SetColorPatch.IsAntiGlitchDisabled = false;
    }
    private static void ForceAssignRole(CustomRoles role, List<PlayerControl> AllPlayers, CustomRpcSender sender, RoleTypes BaseRole, RoleTypes hostBaseRole = RoleTypes.Crewmate, bool skip = false, int Count = -1)
    {
        var count = 1;

        if (Count != -1)
            count = Count;
        for (var i = 0; i < count; i++)
        {
            if (!AllPlayers.Any()) break;
            var rand = IRandom.Instance;
            var player = AllPlayers[rand.Next(0, AllPlayers.Count)];
            AllPlayers.Remove(player);
            Main.AllPlayerCustomRoles[player.PlayerId] = role;
            if (!skip)
            {
                if (!player.IsModClient())
                {
                    int playerCID = player.GetClientId();
                    sender.RpcSetRole(player, BaseRole, playerCID);
                    //Desyncする人視点で他プレイヤーを科学者にするループ
                    foreach (var pc in PlayerControl.AllPlayerControls)
                    {
                        if (pc == player) continue;
                        sender.RpcSetRole(pc, RoleTypes.Scientist, playerCID);
                    }
                    //他視点でDesyncする人の役職を科学者にするループ
                    foreach (var pc in PlayerControl.AllPlayerControls)
                    {
                        if (pc == player) continue;
                        if (pc.PlayerId == 0) player.SetRole(RoleTypes.Scientist); //ホスト視点用
                        else sender.RpcSetRole(player, RoleTypes.Scientist, pc.GetClientId());
                    }
                }
                else
                {
                    //ホストは別の役職にする
                    player.SetRole(hostBaseRole); //ホスト視点用
                    sender.RpcSetRole(player, hostBaseRole);
                }
            }
        }
    }

    private static void AssignLoversRolesFromList()
    {
        if (CustomRoles.Lovers.IsEnable())
        {
            //Loversを初期化
            Main.LoversPlayers.Clear();
            Main.isLoversDead = false;
            //ランダムに2人選出
            AssignLoversRoles();
        }
    }
    private static void AssignLoversRoles(int RawCount = -1)
    {
        var allPlayers = new List<PlayerControl>();
        foreach (PlayerControl pc in Main.AllPlayerControls)
        {
            if (pc.Is(CustomRoles.GM)
                || (pc.HasSubRole() && pc.GetCustomSubRoles().Count >= Options.NoLimitAddonsNumMax.GetInt())
                || pc.Is(CustomRoles.Dictator)
                || pc.Is(CustomRoles.God)
                || pc.Is(CustomRoles.FFF)
                || pc.Is(CustomRoles.Bomber)
                || pc.Is(CustomRoles.Nuker)
                || pc.Is(CustomRoles.Provocateur)
                || (pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeInLove.GetBool())
                || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeInLove.GetBool())
                || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeInLove.GetBool()))
                continue;
            allPlayers.Add(pc);
        }
        var role = CustomRoles.Lovers;
        var rd = IRandom.Instance;
        var count = Math.Clamp(RawCount, 0, allPlayers.Count);
        if (RawCount == -1) count = Math.Clamp(role.GetCount(), 0, allPlayers.Count);
        if (count <= 0) return;
        for (var i = 0; i < count; i++)
        {
            var player = allPlayers[rd.Next(0, allPlayers.Count)];
            Main.LoversPlayers.Add(player);
            allPlayers.Remove(player);
            Main.PlayerStates[player.PlayerId].SetSubRole(role);
            Logger.Info("Add-on assigned: " + player?.Data?.PlayerName + " = " + player.GetCustomRole().ToString() + " + " + role.ToString(), "AssignLovers");
        }
        RPC.SyncLoversPlayers();
    }
    private static void AssignSubRoles(CustomRoles role, int RawCount = -1)
    {
        var allPlayers = Main.AllAlivePlayerControls.Where(x => CustomRolesHelper.CheckAddonConflict(role, x)).ToArray();
        var count = Math.Clamp(RawCount, 0, allPlayers.Length);
        if (RawCount == -1) count = Math.Clamp(role.GetCount(), 0, allPlayers.Length);
        if (count <= 0) return;
        for (var i = 0; i < count; i++)
        {
            var player = allPlayers[IRandom.Instance.Next(0, allPlayers.Length)];
            Main.PlayerStates[player.PlayerId].SetSubRole(role);
            Logger.Info("Assigned add-on: " + player?.Data?.PlayerName + " = " + player.GetCustomRole().ToString() + " + " + role.ToString(), "Assign " + role.ToString());
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSetRole))]
    private class RpcSetRoleReplacer
    {
        public static bool doReplace;
        public static Dictionary<byte, CustomRpcSender> senders;
        public static List<(PlayerControl, RoleTypes)> StoragedData = [];
        // 役職Desyncなど別の処理でSetRoleRpcを書き込み済みなため、追加の書き込みが不要なSenderのリスト
        public static List<CustomRpcSender> OverriddenSenderList;
        public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] RoleTypes roleType)
        {
            if (doReplace && senders != null)
            {
                StoragedData.Add((__instance, roleType));
                return false;
            }
            else return true;
        }
        public static void Release()
        {
            foreach (var sender in senders)
            {
                if (OverriddenSenderList.Contains(sender.Value)) continue;
                if (sender.Value.CurrentState != CustomRpcSender.State.InRootMessage)
                    throw new InvalidOperationException("A CustomRpcSender had Invalid State.");

                foreach ((PlayerControl PLAYER, RoleTypes ROLETYPE) in StoragedData.ToArray())
                {
                    PLAYER.SetRole(ROLETYPE);
                    sender.Value.AutoStartRpc(PLAYER.NetId, (byte)RpcCalls.SetRole, Utils.GetPlayerById(sender.Key).GetClientId())
                        .Write((ushort)ROLETYPE)
                        .EndRpc();
                }
                sender.Value.EndMessage();
            }
            doReplace = false;
        }
        public static void StartReplace(Dictionary<byte, CustomRpcSender> senders)
        {
            RpcSetRoleReplacer.senders = senders;
            StoragedData = [];
            OverriddenSenderList = [];
            doReplace = true;
        }
    }
}