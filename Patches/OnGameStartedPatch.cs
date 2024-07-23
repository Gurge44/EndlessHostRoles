using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmongUs.Data;
using AmongUs.GameOptions;
using Assets.CoreScripts;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using EHR.AddOns.Common;
using EHR.AddOns.Crewmate;
using EHR.AddOns.Impostor;
using EHR.Crewmate;
using EHR.Impostor;
using EHR.Modules;
using EHR.Neutral;
using EHR.Patches;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;
using static EHR.Modules.CustomRoleSelector;
using static EHR.Translator;
using DateTime = Il2CppSystem.DateTime;


namespace EHR;

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGame))]
internal class ChangeRoleSettings
{
    public static bool Prefix(AmongUsClient __instance)
    {
        if (!GameStates.IsLocalGame) return true;

        __instance.StartCoroutine(CoStartGame().WrapToIl2Cpp());
        return false;

        IEnumerator<object> CoStartGame()
        {
            AmongUsClient amongUsClient = __instance;
            if (DestroyableSingleton<HudManager>.Instance.GameMenu.IsOpen) DestroyableSingleton<HudManager>.Instance.GameMenu.Close();
            DestroyableSingleton<UnityTelemetry>.Instance.Init();
            amongUsClient.logger.Info($"Received game start: {amongUsClient.AmHost}");
            yield return null;
            while (!DestroyableSingleton<HudManager>.InstanceExists) yield return null;
            while (PlayerControl.LocalPlayer == null) yield return null;
            PlayerControl.LocalPlayer.moveable = false;
            PlayerControl.LocalPlayer.MyPhysics.inputHandler.enabled = true;
            PlayerCustomizationMenu objectOfType1 = Object.FindObjectOfType<PlayerCustomizationMenu>();
            if (objectOfType1 != null) objectOfType1.Close(false);
            GameSettingMenu objectOfType2 = Object.FindObjectOfType<GameSettingMenu>();
            if (objectOfType2 != null) objectOfType2.Close();
            if (DestroyableSingleton<GameStartManager>.InstanceExists)
            {
                // amongUsClient.DisconnectHandlers.Remove((IDisconnectHandler) DestroyableSingleton<GameStartManager>.Instance);
                Object.Destroy(DestroyableSingleton<GameStartManager>.Instance.gameObject);
            }

            if (DestroyableSingleton<LobbyInfoPane>.InstanceExists) Object.Destroy(DestroyableSingleton<LobbyInfoPane>.Instance.gameObject);
            if (DestroyableSingleton<DiscordManager>.InstanceExists) DestroyableSingleton<DiscordManager>.Instance.SetPlayingGame();
            if (!string.IsNullOrEmpty(DataManager.Player.Store.ActiveCosmicube))
            {
                AmongUsClient.Instance.SetActivePodType(DestroyableSingleton<CosmicubeManager>.Instance.GetCubeDataByID(DataManager.Player.Store.ActiveCosmicube).podId);
            }
            else
            {
                PlayerStorageManager.CloudPlayerPrefs playerPrefs = DestroyableSingleton<PlayerStorageManager>.Instance.PlayerPrefs;
                AmongUsClient.Instance.SetActivePodType(playerPrefs.ActivePodType);
            }

            DestroyableSingleton<FriendsListManager>.Instance.ConfirmationScreen.Cancel();
            DestroyableSingleton<FriendsListManager>.Instance.Ui.Close(true);
            DestroyableSingleton<FriendsListManager>.Instance.ReparentUI();
            // CosmeticsCache.ClearUnusedCosmetics();
            yield return DestroyableSingleton<HudManager>.Instance.CoFadeFullScreen(Color.clear, Color.black, showLoader: true);
            ++StatsManager.Instance.BanPoints;
            StatsManager.Instance.LastGameStarted = DateTime.UtcNow;
            if (amongUsClient.AmHost)
            {
                yield return amongUsClient.CoStartGameHost();
            }
            else
            {
                yield return amongUsClient.CoStartGameClient();
                if (amongUsClient.AmHost)
                    yield return amongUsClient.CoStartGameHost();
            }

            for (int index = 0; index < GameData.Instance.PlayerCount; ++index)
            {
                PlayerControl player = GameData.Instance.AllPlayers.ToArray()[index].Object;
                if (player != null)
                {
                    player.moveable = true;
                    player.NetTransform.enabled = true;
                    player.MyPhysics.enabled = true;
                    player.MyPhysics.Awake();
                    player.MyPhysics.ResetMoveState();
                    player.Collider.enabled = true;
                    ShipStatus.Instance.SpawnPlayer(player, GameData.Instance.PlayerCount, true);
                }
            }

            DestroyableSingleton<FriendsListManager>.Instance.SetRecentlyPlayed(GameData.Instance.AllPlayers);
            GameData.TimeGameStarted = Time.realtimeSinceStartup;
            int map = Mathf.Clamp(GameOptionsManager.Instance.CurrentGameOptions.MapId, 0, Constants.MapNames.Length - 1);
            string gameName = GameCode.IntToGameName(AmongUsClient.Instance.GameId);
            DestroyableSingleton<DebugAnalytics>.Instance.Analytics.StartGame(PlayerControl.LocalPlayer.Data, GameData.Instance.PlayerCount, GameOptionsManager.Instance.CurrentGameOptions.NumImpostors, AmongUsClient.Instance.NetworkMode, (MapNames)map, GameOptionsManager.Instance.CurrentGameOptions.GameMode, gameName, DestroyableSingleton<ServerManager>.Instance.CurrentRegion.Name, GameOptionsManager.Instance.CurrentGameOptions, GameData.Instance.AllPlayers);
            try
            {
                DestroyableSingleton<UnityTelemetry>.Instance.StartGame(AmongUsClient.Instance.AmHost, GameData.Instance.PlayerCount, GameOptionsManager.Instance.CurrentGameOptions.NumImpostors, AmongUsClient.Instance.NetworkMode, StatsManager.Instance.GetStat(StringNames.StatsGamesImpostor), StatsManager.Instance.GetStat(StringNames.StatsGamesStarted), StatsManager.Instance.GetStat(StringNames.StatsCrewmateStreak));
                NetworkedPlayerInfo.PlayerOutfit defaultOutfit = PlayerControl.LocalPlayer.Data.DefaultOutfit;
                DestroyableSingleton<UnityTelemetry>.Instance.StartGameCosmetics(defaultOutfit.ColorId, defaultOutfit.HatId, defaultOutfit.SkinId, defaultOutfit.PetId, defaultOutfit.VisorId, defaultOutfit.NamePlateId);
            }
            catch
            {
            }

            GameDebugCommands.AddCommands();
        }
    }

    public static void Postfix(AmongUsClient __instance)
    {
        SetUpRoleTextPatch.IsInIntro = true;

        Main.OverrideWelcomeMsg = string.Empty;
        try
        {
            Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.GuardianAngel, 0, 0);
            if (Options.DisableVanillaRoles.GetBool())
            {
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Scientist, 0, 0);
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Engineer, 0, 0);
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Shapeshifter, 0, 0);
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Noisemaker, 0, 0);
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Phantom, 0, 0);
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Tracker, 0, 0);
            }

            if (Main.NormalOptions.MapId > 5) Logger.SendInGame(GetString("UnsupportedMap"));

            try
            {
                Main.AllRoleClasses.Do(x => x.Init());
            }
            catch (Exception e)
            {
                Utils.ThrowException(e);
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
            Main.LoversPlayers = [];
            Main.ShieldPlayer = Options.ShieldPersonDiedFirst.GetBool() ? Main.FirstDied : string.Empty;
            Main.FirstDied = string.Empty;
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
            Crewpostor.TasksDone = [];
            Express.SpeedNormal = [];
            Express.SpeedUp = [];
            Messenger.Sent = [];

            ReportDeadBodyPatch.CanReport = [];
            SabotageMapPatch.TimerTexts = [];

            Options.UsedButtonCount = 0;

            GameOptionsManager.Instance.currentNormalGameOptions.ConfirmImpostor = false;
            Main.RealOptionsData = new(GameOptionsManager.Instance.CurrentGameOptions);

            Main.IntroDestroyed = false;

            RandomSpawn.CustomNetworkTransformPatch.NumOfTP = [];

            AFKDetector.ShieldedPlayers.Clear();

            MeetingTimeManager.Init();
            Main.DefaultCrewmateVision = Main.RealOptionsData.GetFloat(FloatOptionNames.CrewLightMod);
            Main.DefaultImpostorVision = Main.RealOptionsData.GetFloat(FloatOptionNames.ImpostorLightMod);

            Main.LastNotifyNames = [];

            CheckForEndVotingPatch.EjectionText = string.Empty;

            Arsonist.CurrentDousingTarget = byte.MaxValue;
            Revolutionist.CurrentDrawTarget = byte.MaxValue;
            Main.PlayerColors = [];

            if (Options.CurrentGameMode == CustomGameMode.Speedrun && !Options.UsePets.GetBool())
            {
                Options.UsePets.SetValue(1);
                PlayerControl.LocalPlayer.ShowPopUp(GetString("PetsForceEnabled"));
            }

            RPC.SyncAllPlayerNames();
            RPC.SyncAllClientRealNames();

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
                Camouflage.PlayerSkins[pc.PlayerId] = new NetworkedPlayerInfo.PlayerOutfit().Set(outfit.PlayerName, outfit.ColorId, outfit.HatId, outfit.SkinId, outfit.VisorId, outfit.PetId, outfit.NamePlateId);
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

            Main.ChangedRole = false;

            try
            {
                SoloKombatManager.Init();
                FFAManager.Init();
                MoveAndStopManager.Init();
                HotPotatoManager.Init();
                HnSManager.Init();
                SpeedrunManager.Init();
            }
            catch (Exception e)
            {
                Utils.ThrowException(e);
            }

            CustomWinnerHolder.Reset();
            AntiBlackout.Reset();
            NameNotifyManager.Reset();
            SabotageSystemTypeRepairDamagePatch.Initialize();
            DoorsReset.Initialize();
            GhostRolesManager.Initialize();
            RoleBlockManager.Reset();
            ChatManager.ResetHistory();
            CustomNetObject.Reset();

            IRandom.SetInstanceById(Options.RoleAssigningAlgorithm.GetValue());

            MeetingStates.MeetingCalled = false;
            MeetingStates.FirstMeeting = true;
            GameStates.AlreadyDied = false;
        }
        catch (Exception ex)
        {
            Utils.ErrorEnd("Change Role Setting Postfix");
            Utils.ThrowException(ex);
        }
    }
}

[HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
internal class SelectRolesPatch
{
    private static readonly Dictionary<CustomRoles, List<byte>> BasisChangingAddons = [];
    private static Dictionary<RoleTypes, int> RoleTypeNums = [];

    private static RoleOptionsCollectionV08 RoleOpt => Main.NormalOptions.roleOptions;

    public static void UpdateRoleTypeNums()
    {
        RoleTypeNums = new()
        {
            { RoleTypes.Scientist, AddScientistNum },
            { RoleTypes.Engineer, AddEngineerNum },
            { RoleTypes.Shapeshifter, AddShapeshifterNum },
            { RoleTypes.Noisemaker, AddNoisemakerNum },
            { RoleTypes.Phantom, AddPhantomNum },
            { RoleTypes.Tracker, AddTrackerNum }
        };
    }

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


            UpdateRoleTypeNums();
            foreach (var roleType in RoleTypeNums)
            {
                int roleNum = Options.DisableVanillaRoles.GetBool() ? 0 : RoleOpt.GetNumPerGame(roleType.Key);
                roleNum += roleType.Value;
                RoleOpt.SetRoleRate(roleType.Key, roleNum, roleType.Value > 0 ? 100 : RoleOpt.GetChancePerGame(roleType.Key));
            }


            BasisChangingAddons.Clear();

            try
            {
                var rd = IRandom.Instance;
                bool bloodlustSpawn = rd.Next(1, 100) <= (Options.CustomAdtRoleSpawnRate.TryGetValue(CustomRoles.Bloodlust, out var option3) ? option3.GetFloat() : 0) && CustomRoles.Bloodlust.IsEnable();
                HashSet<byte> bloodlustList = RoleResult.Where(x => x.Value.IsCrewmate() && !x.Value.IsTaskBasedCrewmate()).Select(x => x.Key.PlayerId).ToHashSet();
                if (bloodlustList.Count == 0) bloodlustSpawn = false;

                if (Main.AlwaysSpawnTogetherCombos.TryGetValue(OptionItem.CurrentPreset, out var combos) && combos.Values.Any(l => l.Contains(CustomRoles.Bloodlust)))
                {
                    var roles = combos.Where(x => x.Value.Contains(CustomRoles.Bloodlust)).Select(x => x.Key).ToHashSet();
                    var players = RoleResult.Where(x => roles.Contains(x.Value) && x.Value.IsCrewmate() && !x.Value.IsTaskBasedCrewmate()).Select(x => x.Key.PlayerId).ToHashSet();
                    if (players.Count > 0)
                    {
                        bloodlustList = players;
                        bloodlustSpawn = true;
                    }

                    combos.Do(x => x.Value.Remove(CustomRoles.Bloodlust));
                }

                if (Main.SetAddOns.Values.Any(x => x.Contains(CustomRoles.Bloodlust)))
                {
                    bloodlustSpawn = true;
                    bloodlustList = Main.SetAddOns.Where(x => x.Value.Contains(CustomRoles.Bloodlust)).Select(x => x.Key).ToHashSet();
                }

                if (bloodlustSpawn) BasisChangingAddons[CustomRoles.Bloodlust] = bloodlustList.Shuffle().Take(CustomRoles.Bloodlust.GetCount()).ToList();
            }
            catch (Exception e)
            {
                Utils.ThrowException(e);
            }


            Dictionary<(byte, byte), RoleTypes> rolesMap = [];

            // Register Desync Impostor Roles
            foreach (var kv in RoleResult.Where(x => x.Value.IsDesyncRole() || IsBloodlustPlayer(x.Key.PlayerId)))
                AssignDesyncRole(kv.Value, kv.Key, senders, rolesMap, BaseRole: IsBloodlustPlayer(kv.Key.PlayerId) || (Options.CurrentGameMode == CustomGameMode.Speedrun && SpeedrunManager.CanKill.Contains(kv.Key.PlayerId)) ? RoleTypes.Impostor : kv.Value.GetDYRole());


            MakeDesyncSender(senders, rolesMap);
        }
        catch (Exception e)
        {
            Utils.ErrorEnd("Select Role Prefix");
            Utils.ThrowException(e);
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

            bool physicistSpawn = rd.Next(100) < (Options.CustomAdtRoleSpawnRate.TryGetValue(CustomRoles.Physicist, out var option1) ? option1.GetFloat() : 0) && CustomRoles.Physicist.IsEnable();
            bool nimbleSpawn = rd.Next(100) < (Options.CustomAdtRoleSpawnRate.TryGetValue(CustomRoles.Nimble, out var option2) ? option2.GetFloat() : 0) && CustomRoles.Nimble.IsEnable();
            bool finderSpawn = rd.Next(100) < (Options.CustomAdtRoleSpawnRate.TryGetValue(CustomRoles.Finder, out var option3) ? option3.GetFloat() : 0) && CustomRoles.Finder.IsEnable();
            bool noisySpawn = rd.Next(100) < (Options.CustomAdtRoleSpawnRate.TryGetValue(CustomRoles.Noisy, out var option4) ? option4.GetFloat() : 0) && CustomRoles.Noisy.IsEnable();

            if (Options.EveryoneCanVent.GetBool())
            {
                nimbleSpawn = false;
                physicistSpawn = false;
                finderSpawn = false;
                noisySpawn = false;
            }

            HashSet<byte> nimbleList = [], physicistList = [];
            if (nimbleSpawn || physicistSpawn || finderSpawn || noisySpawn)
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

            HashSet<byte> finderList = physicistList.ToHashSet(), noisyList = physicistList.ToHashSet();

            var roleSpawnMapping = new Dictionary<CustomRoles, (bool SpawnFlag, HashSet<byte> RoleList)>
            {
                { CustomRoles.Nimble, (nimbleSpawn, nimbleList) },
                { CustomRoles.Physicist, (physicistSpawn, physicistList) },
                { CustomRoles.Finder, (finderSpawn, finderList) },
                { CustomRoles.Noisy, (noisySpawn, noisyList) }
            };

            for (int i = 0; i < roleSpawnMapping.Count; i++)
            {
                (CustomRoles addon, (bool SpawnFlag, HashSet<byte> RoleList) value) = roleSpawnMapping.ElementAt(i);
                if (value.RoleList.Count == 0) value.SpawnFlag = false;

                if (Main.AlwaysSpawnTogetherCombos.TryGetValue(OptionItem.CurrentPreset, out var combos) && combos.Values.Any(l => l.Contains(addon)))
                {
                    var roles = combos.Where(x => x.Value.Contains(addon)).Select(x => x.Key).ToHashSet();
                    var players = RoleResult.Where(x => roles.Contains(x.Value) && x.Value.IsCrewmate() && (addon == CustomRoles.Nimble || x.Value.GetRoleTypes() == RoleTypes.Crewmate) && !IsBasisChangingPlayer(x.Key.PlayerId, CustomRoles.Bloodlust)).Select(x => x.Key.PlayerId).ToHashSet();
                    if (players.Count > 0)
                    {
                        value.RoleList = players;
                        value.SpawnFlag = true;
                        roleSpawnMapping[addon] = value;
                    }

                    combos.Do(x => x.Value.Remove(addon));
                }

                if (Main.SetAddOns.Values.Any(x => x.Contains(addon)))
                {
                    value.SpawnFlag = true;
                    var newRoleList = Main.SetAddOns.Where(x => x.Value.Contains(addon)).Select(x => x.Key).ToHashSet();
                    if (value.RoleList.Count != 1 || value.RoleList.First() != newRoleList.First())
                    {
                        value.RoleList = newRoleList;
                    }

                    roleSpawnMapping[addon] = value;
                }
            }

            foreach ((CustomRoles addon, (bool spawnFlag, HashSet<byte> roleList)) in roleSpawnMapping)
            {
                if (spawnFlag)
                {
                    foreach ((CustomRoles otherAddon, (bool otherSpawnFlag, _)) in roleSpawnMapping)
                    {
                        if (otherAddon != addon && otherSpawnFlag && BasisChangingAddons.TryGetValue(otherAddon, out var otherList))
                        {
                            roleList.ExceptWith(otherList);
                        }
                    }

                    BasisChangingAddons[addon] = roleList.Shuffle().Take(addon.GetCount()).ToList();
                }
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
                else if (IsBasisChangingPlayer(PLAYER.PlayerId, CustomRoles.Finder))
                {
                    if (roleType == RoleTypes.Crewmate)
                    {
                        roleType = RoleTypes.Tracker;
                        Logger.Warn($"{PLAYER.GetRealName()} was assigned Finder, their role basis was changed to Tracker", "Finder");
                    }
                }
                else if (IsBasisChangingPlayer(PLAYER.PlayerId, CustomRoles.Noisy))
                {
                    if (roleType == RoleTypes.Crewmate)
                    {
                        roleType = RoleTypes.Noisemaker;
                        Logger.Warn($"{PLAYER.GetRealName()} was assigned Noisy, their role basis was changed to Noisemaker", "Noisy");
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
                    RoleTypes.Noisemaker => CustomRoles.Noisemaker,
                    RoleTypes.Phantom => CustomRoles.Phantom,
                    RoleTypes.Tracker => CustomRoles.Tracker,
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
                        if (role is CustomRoles.Nimble or CustomRoles.Physicist or CustomRoles.Bloodlust or CustomRoles.Finder or CustomRoles.Noisy) continue;
                        state.SetSubRole(role);
                        if (overrideLovers && role == CustomRoles.Lovers) Main.LoversPlayers.Add(Utils.GetPlayerById(item.Key));
                        if (role.IsGhostRole()) GhostRolesManager.SpecificAssignGhostRole(item.Key, role, true);
                    }
                }
            }

            if (!overrideLovers && CustomRoles.Lovers.IsEnable() && (CustomRoles.FFF.IsEnable() ? -1 : rd.Next(1, 100)) <= Lovers.LoverSpawnChances.GetInt()) AssignLoversRolesFromList();

            // Add-on assignment
            var aapc = Main.AllAlivePlayerControls.Shuffle();
            if (Main.GM.Value) aapc = aapc.Where(x => x.PlayerId != 0).ToArray();
            var addonNum = aapc.ToDictionary(x => x, _ => 0);
            AddonRolesList
                .Except(BasisChangingAddons.Keys)
                .Where(x => x.IsEnable())
                .SelectMany(x => Enumerable.Repeat(x, Math.Clamp(x.GetCount(), 0, aapc.Length)))
                .Where(x => IRandom.Instance.Next(1, 100) <= (Options.CustomAdtRoleSpawnRate.TryGetValue(x, out var sc) ? sc.GetFloat() : 0))
                .OrderBy(x => Options.CustomAdtRoleSpawnRate.TryGetValue(x, out var sc) && sc.GetInt() == 100 ? IRandom.Instance.Next(100) : IRandom.Instance.Next(100, 1000))
                .Select(x =>
                {
                    var suitablePlayer = aapc
                        .OrderBy(p => addonNum[p])
                        .FirstOrDefault(p => CustomRolesHelper.CheckAddonConflict(x, p));
                    if (suitablePlayer != null) addonNum[suitablePlayer]++;
                    return (Role: x, SuitablePlayer: suitablePlayer);
                })
                .DoIf(x => x.SuitablePlayer != null, x => Main.PlayerStates[x.SuitablePlayer.PlayerId].SetSubRole(x.Role));


            foreach (var state in Main.PlayerStates.Values)
            {
                if (Main.NeverSpawnTogetherCombos.TryGetValue(OptionItem.CurrentPreset, out var neverList) && neverList.TryGetValue(state.MainRole, out var bannedAddonList))
                {
                    bannedAddonList.ForEach(x => state.RemoveSubRole(x));
                }

                if (Main.AlwaysSpawnTogetherCombos.TryGetValue(OptionItem.CurrentPreset, out var alwaysList) && alwaysList.TryGetValue(state.MainRole, out var addonList))
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

                    if (pc.GetCustomRole().GetDYRole() is RoleTypes.Shapeshifter or RoleTypes.Phantom)
                    {
                        foreach (var target in Main.AllPlayerControls)
                        {
                            target.Data.Role.CanBeKilled = true;

                            target.cosmetics.SetNameColor(Color.white);
                            target.Data.Role.NameColor = Color.white;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString(), "OnGameStartedPatch Add methods");
                }
            }

            try
            {
                Stressed.Add();
                Asthmatic.Add();
                Circumvent.Add();
                Dynamo.Add();
                Spurt.Add();
                Lovers.Init();
            }
            catch (Exception e)
            {
                Utils.ThrowException(e);
            }

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


            foreach (var roleType in RoleTypeNums)
            {
                int roleNum = Options.DisableVanillaRoles.GetBool() ? 0 : RoleOpt.GetNumPerGame(roleType.Key);
                roleNum -= roleType.Value;
                RoleOpt.SetRoleRate(roleType.Key, roleNum, RoleOpt.GetChancePerGame(roleType.Key));
            }


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
                case CustomGameMode.Speedrun:
                    GameEndChecker.SetPredicateToSpeedrun();
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
            Main.ResetCamPlayerList.UnionWith(Main.PlayerStates.Where(p => (p.Value.MainRole.IsDesyncRole() && !p.Value.MainRole.UsesPetInsteadOfKill()) || p.Value.SubRoles.Contains(CustomRoles.Bloodlust)).Select(p => p.Key));
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
            Utils.ThrowException(ex);
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

        // Others' point of view
        foreach (var seer in Main.AllPlayerControls.Where(x => player.PlayerId != x.PlayerId))
            rolesMap[(seer.PlayerId, player.PlayerId)] = othersRole;

        RpcSetRoleReplacer.OverriddenSenderList.Add(senders[player.PlayerId]);
        // Host perspective determines the role
        player.SetRole(othersRole);

        // Override RoleType for host
        if (player.PlayerId == hostId && BaseRole == RoleTypes.Shapeshifter)
        {
            DestroyableSingleton<RoleManager>.Instance.SetRole(player, BaseRole);
            DestroyableSingleton<RoleBehaviour>.Instance.CanBeKilled = true;
        }

        player.Data.IsDead = true;

        Logger.Info($"Register Modded Role：{player.Data?.PlayerName} => {role}", "AssignRoles");
    }

    private static void MakeDesyncSender(Dictionary<byte, CustomRpcSender> senders, Dictionary<(byte, byte), RoleTypes> rolesMap)
    {
        foreach (PlayerControl seer in Main.AllPlayerControls)
        {
            foreach (PlayerControl target in Main.AllPlayerControls)
            {
                try
                {
                    if (rolesMap.TryGetValue((seer.PlayerId, target.PlayerId), out var role))
                    {
                        // Change Scientist to Noisemaker when the role is desync and target have the Noisemaker role << Thanks: TommyXL
                        if (role is RoleTypes.Scientist && RoleResult.Any(x => x.Key.PlayerId == seer.PlayerId && x.Value is CustomRoles.NoisemakerEHR or CustomRoles.Noisemaker))
                        {
                            Logger.Info($"seer: {seer.PlayerId}, target: {target.PlayerId}, {role} => {RoleTypes.Noisemaker}", "OverrideRoleForDesync");
                            role = RoleTypes.Noisemaker;
                        }

                        var sender = senders[seer.PlayerId];
                        sender.RpcSetRole(seer, role, target.GetClientId());
                    }
                }
                catch
                {
                }
            }
        }
    }

    private static void AssignCustomRole(CustomRoles role, PlayerControl player)
    {
        if (player == null) return;
        Main.PlayerStates[player.PlayerId].countTypes = role.GetCountTypes();
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

        var allPlayers = Main.AllPlayerControls.Where(pc => !pc.Is(CustomRoles.GM) && (!pc.HasSubRole() || pc.GetCustomSubRoles().Count < Options.NoLimitAddonsNumMax.GetInt()) && !pc.Is(CustomRoles.Dictator) && !pc.Is(CustomRoles.God) && !pc.Is(CustomRoles.FFF) && !pc.Is(CustomRoles.Bomber) && !pc.Is(CustomRoles.Nuker) && !pc.Is(CustomRoles.Provocateur) && (!pc.IsCrewmate() || Lovers.CrewCanBeInLove.GetBool()) && (!pc.GetCustomRole().IsNeutral() || Lovers.NeutralCanBeInLove.GetBool()) && (!pc.IsImpostor() || Lovers.ImpCanBeInLove.GetBool())).ToList();
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
                            .Write(false)
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