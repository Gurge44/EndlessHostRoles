using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Patches;
using EHR.Roles.AddOns.Common;
using EHR.Roles.AddOns.Crewmate;
using EHR.Roles.AddOns.Impostor;
using EHR.Roles.Crewmate;
using EHR.Roles.Impostor;
using EHR.Roles.Neutral;
using HarmonyLib;
using Hazel;
using static EHR.Modules.CustomRoleSelector;
using static EHR.Translator;

namespace EHR;

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGame))]
internal class ChangeRoleSettings
{
    public static void Postfix(AmongUsClient __instance)
    {
        SetUpRoleTextPatch.IsInIntro = true;
        Utils.NotifyRoles(NoCache: true);

        Main.OverrideWelcomeMsg = string.Empty;
        try
        {
            Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.GuardianAngel, 0, 0);
            if (Options.DisableVanillaRoles.GetBool())
            {
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Scientist, 0, 0);
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Engineer, 0, 0);
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Shapeshifter, 0, 0);
            }

            // Reset previous roles
            if (Main.PlayerStates != null)
            {
                foreach (var state in Main.PlayerStates.Values)
                {
                    state.Role.Init();
                }
            }

            Main.PlayerStates = [];

            Main.AbilityUseLimit = [];

            Main.HasJustStarted = true;

            Main.AllPlayerKillCooldown = [];
            Main.AllPlayerSpeed = [];
            Main.KillTimers = [];
            Main.SleuthMsgs = [];
            Main.CyberStarDead = [];
            Main.KilledDiseased = [];
            Main.KilledAntidote = [];
            Main.BaitAlive = [];
            Main.DontCancelVoteList = [];
            Main.LastEnteredVent = [];
            Main.LastEnteredVentLocation = [];
            Main.AfterMeetingDeathPlayers = [];
            Main.ResetCamPlayerList = [];
            Main.ClientIdList = [];
            Main.CheckShapeshift = [];
            Main.ShapeshiftTarget = [];
            Main.ShieldPlayer = Options.ShieldPersonDiedFirst.GetBool() ? Main.FirstDied : int.MaxValue;
            Main.FirstDied = int.MaxValue;
            Main.MadmateNum = 0;

            Mayor.MayorUsedButtonCount = [];
            Paranoia.ParaUsedButtonCount = [];
            Mario.MarioVentCount = [];
            Cleaner.CleanerBodies = [];
            Virus.InfectedBodies = [];
            Workaholic.WorkaholicAlive = [];
            Virus.VirusNotify = [];
            Veteran.VeteranInProtect = [];
            Witness.AllKillers = [];
            Grenadier.GrenadierBlinding = [];
            SecurityGuard.BlockSabo = [];
            Ventguard.BlockedVents = [];
            Grenadier.MadGrenadierBlinding = [];
            OverKiller.OverDeadPlayerList = [];
            Warlock.WarlockTimer = [];
            Arsonist.IsDoused = [];
            Revolutionist.IsDraw = [];
            Farseer.IsRevealed = [];
            Arsonist.ArsonistTimer = [];
            Revolutionist.RevolutionistTimer = [];
            Revolutionist.RevolutionistStart = [];
            Revolutionist.RevolutionistLastTime = [];
            Revolutionist.RevolutionistCountdown = [];
            TimeMaster.TimeMasterBackTrack = [];
            TimeMaster.TimeMasterNum = [];
            Farseer.FarseerTimer = [];
            Warlock.CursedPlayers = [];
            Mafia.MafiaRevenged = [];
            Warlock.IsCurseAndKill = [];
            Warlock.IsCursed = false;
            Detective.DetectiveNotify = [];
            Provocateur.Provoked = [];
            Crusader.ForCrusade = [];
            Godfather.GodfatherTarget = byte.MaxValue;
            ChatManager.ResetHistory();

            ReportDeadBodyPatch.CanReport = [];
            SabotageMapPatch.TimerTexts = [];

            Options.UsedButtonCount = 0;

            GameOptionsManager.Instance.currentNormalGameOptions.ConfirmImpostor = false;
            if (Options.CurrentGameMode == CustomGameMode.MoveAndStop) GameOptionsManager.Instance.currentNormalGameOptions.NumImpostors = 0;
            Main.RealOptionsData = new(GameOptionsManager.Instance.CurrentGameOptions);

            Main.IntroDestroyed = false;

            RandomSpawn.CustomNetworkTransformPatch.NumOfTP = [];

            MeetingTimeManager.Init();
            Main.DefaultCrewmateVision = Main.RealOptionsData.GetFloat(FloatOptionNames.CrewLightMod);
            Main.DefaultImpostorVision = Main.RealOptionsData.GetFloat(FloatOptionNames.ImpostorLightMod);

            Main.LastNotifyNames = [];

            CheckForEndVotingPatch.EjectionText = string.Empty;

            Arsonist.CurrentDousingTarget = byte.MaxValue;
            Revolutionist.CurrentDrawTarget = byte.MaxValue;
            Main.PlayerColors = [];

            RPC.SyncAllPlayerNames();

            Camouflage.BlockCamouflage = false;
            Camouflage.Init();
            var invalidColor = Main.AllPlayerControls.Where(p => p.Data.DefaultOutfit.ColorId < 0 || Palette.PlayerColors.Length <= p.Data.DefaultOutfit.ColorId).Select(p => $"{p.name}").ToArray();
            if (invalidColor.Length > 0)
            {
                var msg = GetString("Error.InvalidColor");
                Logger.SendInGame(msg);
                msg += "\n" + string.Join(",", invalidColor);
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
                Main.ClientIdList.Add(pc.GetClientId());
            }

            Main.VisibleTasksCount = true;
            if (__instance.AmHost)
            {
                RPC.SyncCustomSettingsRPC();
                Main.RefixCooldownDelay = 0;
            }

            FallFromLadder.Reset();

            try
            {
                LastImpostor.Init();
                TargetArrow.Init();
                LocateArrow.Init();
                DoubleTrigger.Init();
                Workhorse.Init();
                Damocles.Initialize();
                Stressed.Init();
                Asthmatic.Init();
                DoubleShot.Init();
                Circumvent.Init();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "Init Roles");
            }

            Crewpostor.TasksDone = [];
            Express.SpeedNormal = [];
            Express.SpeedUp = [];

            Main.ChangedRole = false;

            SoloKombatManager.Init();
            FFAManager.Init();
            MoveAndStopManager.Init();
            HotPotatoManager.Init();
            HnSManager.Init();

            CustomWinnerHolder.Reset();
            AntiBlackout.Reset();
            NameNotifyManager.Reset();
            SabotageSystemTypeRepairDamagePatch.Initialize();
            DoorsReset.Initialize();
            GhostRolesManager.Initialize();
            RoleBlockManager.Reset();

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
    private static Dictionary<CustomRoles, List<byte>> BasisChangingAddons = [];

    public static void Prefix()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        try
        {
            // Initializing CustomRpcSender and RpcSetRoleReplacer
            Dictionary<byte, CustomRpcSender> senders = [];
            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                senders[pc.PlayerId] = new CustomRpcSender($"{pc.name}'s SetRole Sender", SendOption.Reliable, false).StartMessage(pc.GetClientId());
            }

            RpcSetRoleReplacer.StartReplace(senders);

            if (Main.GM.Value)
            {
                PlayerControl.LocalPlayer.RpcSetCustomRole(CustomRoles.GM);
                PlayerControl.LocalPlayer.RpcSetRole(RoleTypes.Crewmate);
                PlayerControl.LocalPlayer.Data.IsDead = true;
                Main.PlayerStates[PlayerControl.LocalPlayer.PlayerId].SetDead();
            }


            SelectCustomRoles();
            SelectAddonRoles();
            CalculateVanillaRoleCount();


            var roleOpt = Main.NormalOptions.roleOptions;
            int ScientistNum = Options.DisableVanillaRoles.GetBool() ? 0 : roleOpt.GetNumPerGame(RoleTypes.Scientist);
            roleOpt.SetRoleRate(RoleTypes.Scientist, ScientistNum + AddScientistNum, AddScientistNum > 0 ? 100 : roleOpt.GetChancePerGame(RoleTypes.Scientist));
            int EngineerNum = Options.DisableVanillaRoles.GetBool() ? 0 : roleOpt.GetNumPerGame(RoleTypes.Engineer);
            roleOpt.SetRoleRate(RoleTypes.Engineer, EngineerNum + AddEngineerNum, AddEngineerNum > 0 ? 100 : roleOpt.GetChancePerGame(RoleTypes.Engineer));
            int ShapeshifterNum = Options.DisableVanillaRoles.GetBool() ? 0 : roleOpt.GetNumPerGame(RoleTypes.Shapeshifter);
            roleOpt.SetRoleRate(RoleTypes.Shapeshifter, ShapeshifterNum + AddShapeshifterNum, AddShapeshifterNum > 0 ? 100 : roleOpt.GetChancePerGame(RoleTypes.Shapeshifter));


            var rd = IRandom.Instance;
            BasisChangingAddons.Remove(CustomRoles.Bloodlust);
            bool bloodlustSpawn = rd.Next(1, 100) <= (Options.CustomAdtRoleSpawnRate.TryGetValue(CustomRoles.Bloodlust, out var option3) ? option3.GetFloat() : 0) && CustomRoles.Bloodlust.IsEnable();
            HashSet<byte> bloodlustList = RoleResult.Where(x => x.Value.IsCrewmate() && !x.Value.IsTaskBasedCrewmate()).Select(x => x.Key.PlayerId).ToHashSet();
            if (bloodlustList.Count == 0) bloodlustSpawn = false;
            if (Main.SetAddOns.Values.Any(x => x.Contains(CustomRoles.Bloodlust)))
            {
                bloodlustSpawn = true;
                bloodlustList = Main.SetAddOns.Where(x => x.Value.Contains(CustomRoles.Bloodlust)).Select(x => x.Key).ToHashSet();
            }

            if (bloodlustSpawn) BasisChangingAddons[CustomRoles.Bloodlust] = bloodlustList.Shuffle().Take(CustomRoles.Bloodlust.GetCount()).ToList();


            Dictionary<(byte, byte), RoleTypes> rolesMap = [];

            // Register Desync Impostor Roles
            foreach (var kv in RoleResult.Where(x => x.Value.IsDesyncRole() || IsBloodlustPlayer(x.Key.PlayerId)))
                AssignDesyncRole(kv.Value, kv.Key, senders, rolesMap, BaseRole: IsBloodlustPlayer(kv.Key.PlayerId) ? RoleTypes.Impostor : kv.Value.GetDYRole());


            MakeDesyncSender(senders, rolesMap);
        }
        catch (Exception e)
        {
            Utils.ErrorEnd("Select Role Prefix");
            Logger.Fatal(e.Message, "Select Role Prefix");
        }

        return;

        bool IsBloodlustPlayer(byte id) => BasisChangingAddons.TryGetValue(CustomRoles.Bloodlust, out var list) && list.Contains(id);

        // Below is the role assignment on the vanilla side.
    }

    public static void Postfix()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        try
        {
            var rd = IRandom.Instance;

            BasisChangingAddons.Remove(CustomRoles.Nimble);
            BasisChangingAddons.Remove(CustomRoles.Physicist);

            bool physicistSpawn = rd.Next(100) < (Options.CustomAdtRoleSpawnRate.TryGetValue(CustomRoles.Physicist, out var option1) ? option1.GetFloat() : 0) && CustomRoles.Physicist.IsEnable();
            bool nimbleSpawn = rd.Next(100) < (Options.CustomAdtRoleSpawnRate.TryGetValue(CustomRoles.Nimble, out var option2) ? option2.GetFloat() : 0) && CustomRoles.Nimble.IsEnable();

            if (Options.EveryoneCanVent.GetBool())
            {
                nimbleSpawn = false;
                physicistSpawn = false;
            }

            HashSet<byte> nimbleList = [];
            HashSet<byte> physicistList = [];
            if (nimbleSpawn || physicistSpawn)
            {
                foreach ((PlayerControl PLAYER, RoleTypes _) in RpcSetRoleReplacer.StoragedData)
                {
                    if (IsBasisChangingPlayer(PLAYER.PlayerId, CustomRoles.Bloodlust)) continue;
                    var kp = RoleResult.FirstOrDefault(x => x.Key.PlayerId == PLAYER.PlayerId);
                    if (kp.Value.IsCrewmate())
                    {
                        nimbleList.Add(PLAYER.PlayerId);
                        if (kp.Value.GetRoleTypes() == RoleTypes.Crewmate)
                            physicistList.Add(PLAYER.PlayerId);
                    }
                }
            }

            if (nimbleList.Count == 0) nimbleSpawn = false;
            if (physicistList.Count == 0) physicistSpawn = false;

            if (Main.SetAddOns.Values.Any(x => x.Contains(CustomRoles.Nimble)))
            {
                nimbleSpawn = true;
                nimbleList = Main.SetAddOns.Where(x => x.Value.Contains(CustomRoles.Nimble)).Select(x => x.Key).ToHashSet();
            }

            if (Main.SetAddOns.Values.Any(x => x.Contains(CustomRoles.Physicist)))
            {
                physicistSpawn = true;
                var newPhysicistList = Main.SetAddOns.Where(x => x.Value.Contains(CustomRoles.Physicist)).Select(x => x.Key).ToHashSet();
                if (nimbleList.Count != 1 || physicistList.Count != 1 || nimbleList.First() != newPhysicistList.First())
                {
                    physicistList = newPhysicistList;
                }
            }

            if (nimbleSpawn)
            {
                BasisChangingAddons[CustomRoles.Nimble] = nimbleList.Shuffle().Take(CustomRoles.Nimble.GetCount()).ToList();
            }

            if (physicistSpawn)
            {
                if (nimbleSpawn) physicistList.ExceptWith(BasisChangingAddons[CustomRoles.Nimble]);
                BasisChangingAddons[CustomRoles.Physicist] = physicistList.Shuffle().Take(CustomRoles.Physicist.GetCount()).ToList();
            }

            List<(PlayerControl, RoleTypes)> newList = [];
            foreach ((PlayerControl PLAYER, RoleTypes ROLETYPE) in RpcSetRoleReplacer.StoragedData)
            {
                var kp = RoleResult.FirstOrDefault(x => x.Key.PlayerId == PLAYER.PlayerId);
                RoleTypes roleType = kp.Value.GetRoleTypes();

                if (IsBasisChangingPlayer(PLAYER.PlayerId, CustomRoles.Bloodlust))
                {
                    roleType = RoleTypes.Impostor;
                    Logger.Warn($"{PLAYER.GetRealName()} was assigned Bloodlust, their role basis was changed to Impostor", "Bloodlust");
                }
                else if (IsBasisChangingPlayer(PLAYER.PlayerId, CustomRoles.Nimble))
                {
                    if (roleType == RoleTypes.Crewmate)
                    {
                        roleType = RoleTypes.Engineer;
                        Logger.Warn($"{PLAYER.GetRealName()} was assigned Nimble, their role basis was changed to Engineer", "Nimble");
                    }
                    else
                    {
                        Logger.Info($"{PLAYER.GetRealName()} will be assigned Nimble, but their role is impostor based, so it won't be changed", "Nimble");
                    }
                }
                else if (IsBasisChangingPlayer(PLAYER.PlayerId, CustomRoles.Physicist))
                {
                    if (roleType == RoleTypes.Crewmate)
                    {
                        roleType = RoleTypes.Scientist;
                        Logger.Warn($"{PLAYER.GetRealName()} was assigned Physicist, their role basis was changed to Scientist", "Physicist");
                    }
                }

                if (Options.EveryoneCanVent.GetBool())
                {
                    if (roleType == RoleTypes.Crewmate || (roleType == RoleTypes.Scientist && Options.OverrideScientistBasedRoles.GetBool()))
                    {
                        roleType = RoleTypes.Engineer;
                        Logger.Info($"Everyone can vent => {PLAYER.GetRealName()}'s role was changed to Engineer", "SetRoleReplacer");
                    }
                }

                newList.Add((PLAYER, roleType));
                Logger.Warn(ROLETYPE == roleType ? $"Register original role type => {PLAYER.GetRealName()}: {ROLETYPE}" : $"Register original role type => {PLAYER.GetRealName()}: {ROLETYPE} => {roleType}", "Override Role Select");
            }

            if (Main.GM.Value) newList.Add((PlayerControl.LocalPlayer, RoleTypes.Crewmate));
            RpcSetRoleReplacer.StoragedData = newList;

            RpcSetRoleReplacer.Release(); // Write the saved SetRoleRpc all at once
            RpcSetRoleReplacer.Senders.Do(kvp => kvp.Value.SendMessage());

            // Delete unnecessary objects
            RpcSetRoleReplacer.Senders = null;
            RpcSetRoleReplacer.OverriddenSenderList = null;
            RpcSetRoleReplacer.StoragedData = null;

            //Utils.ApplySuffix();

            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                pc.Data.IsDead = false;
                if (Main.PlayerStates[pc.PlayerId].MainRole != CustomRoles.NotAssigned) continue;
                var role = pc.Data.Role.Role switch
                {
                    RoleTypes.Crewmate => CustomRoles.Crewmate,
                    RoleTypes.Impostor => CustomRoles.Impostor,
                    RoleTypes.Scientist => CustomRoles.Scientist,
                    RoleTypes.Engineer => CustomRoles.Engineer,
                    RoleTypes.GuardianAngel => CustomRoles.GuardianAngel,
                    RoleTypes.Shapeshifter => CustomRoles.Shapeshifter,
                    _ => CustomRoles.NotAssigned
                };
                if (role == CustomRoles.NotAssigned) Logger.SendInGame(string.Format(GetString("Error.InvalidRoleAssignment"), pc?.Data?.PlayerName));
                Main.PlayerStates[pc.PlayerId].SetMainRole(role);
            }

            // For other gamemodes:
            if (Options.CurrentGameMode != CustomGameMode.Standard)
            {
                foreach (var pair in Main.PlayerStates)
                    ExtendedPlayerControl.RpcSetCustomRole(pair.Key, pair.Value.MainRole);
                goto EndOfSelectRolePatch;
            }

            foreach (var kv in RoleResult)
            {
                if (kv.Value.IsDesyncRole() || IsBasisChangingPlayer(kv.Key.PlayerId, CustomRoles.Bloodlust)) continue;
                AssignCustomRole(kv.Value, kv.Key);
            }

            BasisChangingAddons.Do(x => x.Value.Do(y => Main.PlayerStates[y].SetSubRole(x.Key)));

            bool overrideLovers = false;
            if (Main.SetAddOns.Count(x => x.Value.Contains(CustomRoles.Lovers)) == 2)
            {
                Main.LoversPlayers.Clear();
                Main.IsLoversDead = false;
                overrideLovers = true;
                Logger.Warn("Lovers overridden by host's pre-set add-ons", "CustomRoleSelector");
            }

            foreach (var item in Main.SetAddOns)
            {
                if (Main.PlayerStates.TryGetValue(item.Key, out var state))
                {
                    foreach (var role in item.Value)
                    {
                        if (role is CustomRoles.Nimble or CustomRoles.Physicist or CustomRoles.Bloodlust) continue;
                        state.SetSubRole(role);
                        if (overrideLovers && role == CustomRoles.Lovers) Main.LoversPlayers.Add(Utils.GetPlayerById(item.Key));
                        if (role.IsGhostRole()) GhostRolesManager.SpecificAssignGhostRole(item.Key, role, true);
                    }
                }
            }

            if (!overrideLovers && CustomRoles.Lovers.IsEnable() && (CustomRoles.FFF.IsEnable() ? -1 : rd.Next(1, 100)) <= Lovers.LoverSpawnChances.GetInt()) AssignLoversRolesFromList();

            var aapc = Main.AllAlivePlayerControls;
            AddonRolesList
                .Where(x => x.IsEnable())
                .SelectMany(x => Enumerable.Repeat(x, Math.Clamp(x.GetCount(), 0, aapc.Length)))
                .Shuffle()
                .Chunk(aapc.Length)
                .Do(c => c.Zip(aapc).DoIf(x => CustomRolesHelper.CheckAddonConflict(x.First, x.Second) && IRandom.Instance.Next(1, 100) <= (Options.CustomAdtRoleSpawnRate.TryGetValue(x.First, out var sc) ? sc.GetFloat() : 0), x => Main.PlayerStates[x.Second.PlayerId].SetSubRole(x.First)));


            foreach (var state in Main.PlayerStates.Values)
            {
                if (Main.NeverSpawnTogetherCombos.TryGetValue(state.MainRole, out var bannedAddonList))
                {
                    bannedAddonList.ForEach(x => state.RemoveSubRole(x));
                    continue;
                }

                if (Main.AlwaysSpawnTogetherCombos.TryGetValue(state.MainRole, out var addonList))
                {
                    addonList.ForEach(x => state.SetSubRole(x));
                }
            }

            foreach (var pair in Main.PlayerStates)
            {
                ExtendedPlayerControl.RpcSetCustomRole(pair.Key, pair.Value.MainRole);

                var sb = new StringBuilder();
                foreach (CustomRoles subRole in pair.Value.SubRoles)
                {
                    ExtendedPlayerControl.RpcSetCustomRole(pair.Key, subRole);
                    sb.Append(subRole).Append(", ");
                }

                if (sb.Length > 0)
                {
                    sb.Remove(sb.Length - 2, 2);
                    Logger.Info($"{Main.AllPlayerNames[pair.Key]} has sub roles: {sb}", "SelectRolesPatch");
                }
            }

            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                try
                {
                    if (pc.Data.Role.Role == RoleTypes.Shapeshifter)
                        Main.CheckShapeshift.Add(pc.PlayerId, false);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString(), "OnGameStartedPatch Add methods");
                }
            }

            Stressed.Add();
            Asthmatic.Add();
            Circumvent.Add();
            Dynamo.Add();

            LateTask.New(CustomTeamManager.InitializeCustomTeamPlayers, 7f, log: false);

            if (overrideLovers) Logger.Msg(Main.LoversPlayers.Join(x => x?.GetRealName()), "Lovers");

            EndOfSelectRolePatch:

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.HotPotato:
                    HotPotatoManager.OnGameStart();
                    break;
                case CustomGameMode.HideAndSeek:
                    HnSManager.StartSeekerBlindTime();
                    break;
            }

            HudManager.Instance.SetHudActive(true);

            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                pc.ResetKillCooldown();
            }

            var roleOpt = Main.NormalOptions.roleOptions;
            int ScientistNum = Options.DisableVanillaRoles.GetBool() ? 0 : roleOpt.GetNumPerGame(RoleTypes.Scientist);
            ScientistNum -= AddScientistNum;
            roleOpt.SetRoleRate(RoleTypes.Scientist, ScientistNum, roleOpt.GetChancePerGame(RoleTypes.Scientist));
            int EngineerNum = Options.DisableVanillaRoles.GetBool() ? 0 : roleOpt.GetNumPerGame(RoleTypes.Engineer);
            EngineerNum -= AddEngineerNum;
            roleOpt.SetRoleRate(RoleTypes.Engineer, EngineerNum, roleOpt.GetChancePerGame(RoleTypes.Engineer));
            int ShapeshifterNum = Options.DisableVanillaRoles.GetBool() ? 0 : roleOpt.GetNumPerGame(RoleTypes.Shapeshifter);
            ShapeshifterNum -= AddShapeshifterNum;
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
                case CustomGameMode.MoveAndStop:
                    GameEndChecker.SetPredicateToMoveAndStop();
                    break;
                case CustomGameMode.HotPotato:
                    GameEndChecker.SetPredicateToHotPotato();
                    break;
                case CustomGameMode.HideAndSeek:
                    GameEndChecker.SetPredicateToHideAndSeek();
                    break;
            }

            GameOptionsSender.AllSenders.Clear();
            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                GameOptionsSender.AllSenders.Add(new PlayerGameOptionsSender(pc));
            }

            // Add players with unclassified roles to the list of players who require ResetCam.
            Main.ResetCamPlayerList.AddRange(Main.AllPlayerControls.Where(p => p.GetCustomRole() is CustomRoles.Arsonist or CustomRoles.Revolutionist or CustomRoles.Sidekick or CustomRoles.KB_Normal or CustomRoles.Killer or CustomRoles.Tasker or CustomRoles.Potato or CustomRoles.Seeker or CustomRoles.Hider or CustomRoles.Fox or CustomRoles.Troll or CustomRoles.Jumper or CustomRoles.Detector or CustomRoles.Jet or CustomRoles.Dasher or CustomRoles.Locator or CustomRoles.Venter or CustomRoles.Agent or CustomRoles.Taskinator or CustomRoles.Innocent || (p.Is(CustomRoles.Witness) && (!Options.UsePets.GetBool() || Options.WitnessUsePet.GetBool()))).Select(p => p.PlayerId));
            Utils.CountAlivePlayers(true);
            Utils.SyncAllSettings();

            LateTask.New(() =>
            {
                Main.SetRoles = [];
                Main.SetAddOns = [];
            }, 7f, log: false);

            if ((MapNames)Main.NormalOptions.MapId == MapNames.Airship && AmongUsClient.Instance.AmHost && Main.GM.Value)
            {
                LateTask.New(() => { PlayerControl.LocalPlayer.NetTransform.SnapTo(new(15.5f, 0.0f), (ushort)(PlayerControl.LocalPlayer.NetTransform.lastSequenceId + 8)); }, 15f, "GM Auto-TP Failsafe"); // TP to Main Hall
            }

            LateTask.New(() => { Main.HasJustStarted = false; }, 10f, "HasJustStarted to false");
        }
        catch (Exception ex)
        {
            Utils.ErrorEnd("Select Role Postfix");
            Logger.Fatal(ex.ToString(), "Select Role Postfix");
        }

        return;

        bool IsBasisChangingPlayer(byte id, CustomRoles role) => BasisChangingAddons.TryGetValue(role, out var list) && list.Contains(id);
    }

    private static void AssignDesyncRole(CustomRoles role, PlayerControl player, IReadOnlyDictionary<byte, CustomRpcSender> senders, IDictionary<(byte, byte), RoleTypes> rolesMap, RoleTypes BaseRole, RoleTypes hostBaseRole = RoleTypes.Crewmate)
    {
        if (player == null) return;

        var hostId = PlayerControl.LocalPlayer.PlayerId;

        Main.PlayerStates[player.PlayerId].SetMainRole(role);

        var selfRole = player.PlayerId == hostId ? hostBaseRole : BaseRole;
        var othersRole = player.PlayerId == hostId ? RoleTypes.Crewmate : RoleTypes.Scientist;

        // Desync position perspective
        foreach (PlayerControl target in Main.AllPlayerControls)
        {
            rolesMap[(player.PlayerId, target.PlayerId)] = player.PlayerId != target.PlayerId ? othersRole : selfRole;
        }

        // Other's point of view
        foreach (var seer in Main.AllPlayerControls.Where(x => player.PlayerId != x.PlayerId))
            rolesMap[(seer.PlayerId, player.PlayerId)] = othersRole;

        RpcSetRoleReplacer.OverriddenSenderList.Add(senders[player.PlayerId]);
        // Host perspective determines role
        player.SetRole(othersRole);
        player.Data.IsDead = true;

        Logger.Info($"Register Modded Role：{player.Data?.PlayerName} => {role}", "AssignRoles");
    }

    private static void MakeDesyncSender(Dictionary<byte, CustomRpcSender> senders, Dictionary<(byte, byte), RoleTypes> rolesMap)
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
        Main.PlayerStates[player.PlayerId].SetMainRole(role);
        Logger.Info($"Register Modded Role：{player.Data?.PlayerName} => {role}", "AssignRoles");
    }

    private static void AssignLoversRolesFromList()
    {
        if (CustomRoles.Lovers.IsEnable() && !RoleResult.ContainsValue(CustomRoles.Romantic))
        {
            Main.LoversPlayers.Clear();
            Main.IsLoversDead = false;
            AssignLoversRoles();
        }
    }

    private static void AssignLoversRoles(int RawCount = -1)
    {
        if (Lovers.LegacyLovers.GetBool())
        {
            Main.LoversPlayers = Main.AllPlayerControls.Where(x => x.GetCustomRole() is CustomRoles.LovingCrewmate or CustomRoles.LovingImpostor).Take(2).ToList();
            return;
        }

        var allPlayers = Main.AllPlayerControls.Where(pc => !pc.Is(CustomRoles.GM) && (!pc.HasSubRole() || pc.GetCustomSubRoles().Count < Options.NoLimitAddonsNumMax.GetInt()) && !pc.Is(CustomRoles.Dictator) && !pc.Is(CustomRoles.God) && !pc.Is(CustomRoles.FFF) && !pc.Is(CustomRoles.Bomber) && !pc.Is(CustomRoles.Nuker) && !pc.Is(CustomRoles.Provocateur) && (!pc.IsCrewmate() || Lovers.CrewCanBeInLove.GetBool()) && (!pc.GetCustomRole().IsNeutral() || Lovers.NeutralCanBeInLove.GetBool()) && (!pc.GetCustomRole().IsImpostor() || Lovers.ImpCanBeInLove.GetBool())).ToList();
        const CustomRoles role = CustomRoles.Lovers;
        var count = Math.Clamp(RawCount, 0, allPlayers.Count);
        if (RawCount == -1) count = Math.Clamp(role.GetCount(), 0, allPlayers.Count);
        if (count <= 0) return;
        for (var i = 0; i < count; i++)
        {
            var player = allPlayers.RandomElement();
            Main.LoversPlayers.Add(player);
            allPlayers.Remove(player);
            Main.PlayerStates[player.PlayerId].SetSubRole(role);
            Logger.Info("Add-on assigned: " + player.Data?.PlayerName + " = " + player.GetCustomRole() + " + " + role, "Assign Lovers");
        }

        RPC.SyncLoversPlayers();
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSetRole))]
    private class RpcSetRoleReplacer
    {
        private static bool DoReplace;
        public static Dictionary<byte, CustomRpcSender> Senders;

        public static List<(PlayerControl, RoleTypes)> StoragedData = [];

        // A list of Senders that does not require additional writing because SetRoleRpc has already been written in another process such as role Desync.
        public static List<CustomRpcSender> OverriddenSenderList;

        public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] RoleTypes roleType)
        {
            if (DoReplace && Senders != null)
            {
                StoragedData.Add((__instance, roleType));
                return false;
            }

            return true;
        }

        public static void Release()
        {
            foreach (var sender in Senders)
            {
                if (OverriddenSenderList.Contains(sender.Value)) continue;
                if (sender.Value.CurrentState != CustomRpcSender.State.InRootMessage)
                    throw new InvalidOperationException("A CustomRpcSender had Invalid State.");

                foreach ((PlayerControl PLAYER, RoleTypes ROLETYPE) in StoragedData)
                {
                    try
                    {
                        PLAYER.SetRole(ROLETYPE);
                        sender.Value.AutoStartRpc(PLAYER.NetId, (byte)RpcCalls.SetRole, Utils.GetPlayerById(sender.Key).GetClientId())
                            .Write((ushort)ROLETYPE)
                            .EndRpc();
                    }
                    catch
                    {
                    }
                }

                sender.Value.EndMessage();
            }

            DoReplace = false;
        }

        public static void StartReplace(Dictionary<byte, CustomRpcSender> senders)
        {
            Senders = senders;
            StoragedData = [];
            OverriddenSenderList = [];
            DoReplace = true;
        }
    }
}