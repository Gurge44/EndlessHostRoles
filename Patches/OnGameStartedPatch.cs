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
using Il2CppSystem.Collections;
using InnerNet;
using UnityEngine;
using static EHR.Modules.CustomRoleSelector;
using static EHR.Translator;
using DateTime = Il2CppSystem.DateTime;
using Exception = System.Exception;


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
            Main.DiedThisRound = [];
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
            VentilationSystemDeterioratePatch.LastClosestVent = [];

            Options.UsedButtonCount = 0;

            GameOptionsManager.Instance.currentNormalGameOptions.ConfirmImpostor = false;
            Main.RealOptionsData = new(GameOptionsManager.Instance.CurrentGameOptions);

            Main.IntroDestroyed = false;
            ShipStatusBeginPatch.RolesIsAssigned = false;
            GameEndChecker.ShowAllRolesWhenGameEnd = false;

            RandomSpawn.CustomNetworkTransformPatch.NumOfTP = [];

            AFKDetector.ShieldedPlayers.Clear();

            ChatCommands.MutedPlayers.Clear();

            MeetingTimeManager.Init();
            Main.DefaultCrewmateVision = Main.RealOptionsData.GetFloat(FloatOptionNames.CrewLightMod);
            Main.DefaultImpostorVision = Main.RealOptionsData.GetFloat(FloatOptionNames.ImpostorLightMod);

            Main.LastNotifyNames = [];

            CheckForEndVotingPatch.EjectionText = string.Empty;
            CoShowIntroPatch.IntroStarted = false;

            Arsonist.CurrentDousingTarget = byte.MaxValue;
            Revolutionist.CurrentDrawTarget = byte.MaxValue;
            Main.PlayerColors = [];

            RPC.SyncAllPlayerNames();
            RPC.SyncAllClientRealNames();

            Camouflage.BlockCamouflage = false;
            Camouflage.Init();

            if (AmongUsClient.Instance.AmHost)
            {
                var invalidColor = Main.AllPlayerControls.Where(p => p.Data.DefaultOutfit.ColorId < 0 || Palette.PlayerColors.Length <= p.Data.DefaultOutfit.ColorId).Select(p => $"{p.name}").ToArray();
                if (invalidColor.Length > 0)
                {
                    var msg = GetString("Error.InvalidColor");
                    Logger.SendInGame(msg);
                    msg += "\n" + string.Join(",", invalidColor);
                    Utils.SendMessage(msg);
                    Logger.Error(msg, "CoStartGame");
                }
            }

            RoleResult = [];

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
                VentilationSystemDeterioratePatch.LastClosestVent[pc.PlayerId] = 0;
                RoleResult[pc.PlayerId] = CustomRoles.NotAssigned;
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

[HarmonyPatch]
internal static class StartGameHostPatch
{
    private static AmongUsClient AUClient;

    public static readonly Dictionary<CustomRoles, List<byte>> BasisChangingAddons = [];
    private static Dictionary<RoleTypes, int> RoleTypeNums = [];

    private static readonly Dictionary<byte, bool> DataDisconnected = [];

    private static RoleOptionsCollectionV08 RoleOpt => Main.NormalOptions.roleOptions;

    private static void UpdateRoleTypeNums()
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

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGameHost))]
    [HarmonyPrefix]
    public static bool CoStartGameHost_Prefix(AmongUsClient __instance, ref IEnumerator __result)
    {
        AUClient = __instance;
        __result = StartGameHost().WrapToIl2Cpp();
        return false;
    }

    private static System.Collections.IEnumerator StartGameHost()
    {
        if (LobbyBehaviour.Instance) LobbyBehaviour.Instance.Despawn();

        if (!ShipStatus.Instance)
        {
            int num = Mathf.Clamp(GameOptionsManager.Instance.CurrentGameOptions.MapId, 0, Constants.MapNames.Length - 1);
            AUClient.ShipLoadingAsyncHandle = AUClient.ShipPrefabs.ToArray()[num].InstantiateAsync();
            yield return AUClient.ShipLoadingAsyncHandle;
            GameObject result = AUClient.ShipLoadingAsyncHandle.Result;
            ShipStatus.Instance = result.GetComponent<ShipStatus>();
            AUClient.Spawn(ShipStatus.Instance);
        }

        float timer = 0f;
        while (true)
        {
            bool stopWaiting = true;
            int maxTimer = GameOptionsManager.Instance.CurrentGameOptions.MapId is 5 or 4 ? 17 : 12;
            lock (AUClient.allClients)
            {
                // For loop is necessary, or else when a client times out, a foreach loop will throw:
                // System.InvalidOperationException: Collection was modified; enumeration operation may not execute.
                for (int i = 0; i < AUClient.allClients.Count; i++)
                {
                    ClientData clientData = AUClient.allClients[i]; // False error
                    if (clientData.Id != AUClient.ClientId && !clientData.IsReady)
                    {
                        if (timer < maxTimer)
                        {
                            stopWaiting = false;
                        }
                        else
                        {
                            AUClient.SendLateRejection(clientData.Id, DisconnectReasons.ClientTimeout);
                            clientData.IsReady = true;
                            AUClient.OnPlayerLeft(clientData, DisconnectReasons.ClientTimeout);
                        }
                    }
                }
            }

            yield return null;
            if (stopWaiting) break;
            timer += Time.deltaTime;
        }

        AUClient.SendClientReady();
        yield return new WaitForSeconds(2f);
        yield return AssignRoles();
    }

    private static System.Collections.IEnumerator AssignRoles()
    {
        if (AmongUsClient.Instance.IsGameOver || GameStates.IsLobby || GameEndChecker.ShowAllRolesWhenGameEnd) yield break;

        RpcSetRoleReplacer.Initialize();

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

        try
        {
            #region BasisChangingAddonsSetup

            BasisChangingAddons.Clear();

            var random = IRandom.Instance;

            if (Options.CurrentGameMode == CustomGameMode.Standard)
            {
                bool bloodlustSpawn = random.Next(1, 100) <= (Options.CustomAdtRoleSpawnRate.TryGetValue(CustomRoles.Bloodlust, out var option0) ? option0.GetFloat() : 0) && CustomRoles.Bloodlust.IsEnable();
                bool physicistSpawn = random.Next(100) < (Options.CustomAdtRoleSpawnRate.TryGetValue(CustomRoles.Physicist, out var option1) ? option1.GetFloat() : 0) && CustomRoles.Physicist.IsEnable();
                bool nimbleSpawn = random.Next(100) < (Options.CustomAdtRoleSpawnRate.TryGetValue(CustomRoles.Nimble, out var option2) ? option2.GetFloat() : 0) && CustomRoles.Nimble.IsEnable();
                bool finderSpawn = random.Next(100) < (Options.CustomAdtRoleSpawnRate.TryGetValue(CustomRoles.Finder, out var option3) ? option3.GetFloat() : 0) && CustomRoles.Finder.IsEnable();
                bool noisySpawn = random.Next(100) < (Options.CustomAdtRoleSpawnRate.TryGetValue(CustomRoles.Noisy, out var option4) ? option4.GetFloat() : 0) && CustomRoles.Noisy.IsEnable();

                if (Options.EveryoneCanVent.GetBool())
                {
                    nimbleSpawn = false;
                    physicistSpawn = false;
                    finderSpawn = false;
                    noisySpawn = false;
                }

                HashSet<byte> bloodlustList = [], nimbleList = [], physicistList = [], finderList = [], noisyList = [];
                var hasBanned = Main.NeverSpawnTogetherCombos.TryGetValue(OptionItem.CurrentPreset, out var banned);
                if (nimbleSpawn || physicistSpawn || finderSpawn || noisySpawn)
                {
                    foreach (var player in Main.AllPlayerControls)
                    {
                        if (IsBasisChangingPlayer(player.PlayerId, CustomRoles.Bloodlust)) continue;
                        var kp = RoleResult.FirstOrDefault(x => x.Key == player.PlayerId);

                        bool bloodlustBanned = hasBanned && banned.Any(x => x.Key == kp.Value && x.Value.Contains(CustomRoles.Bloodlust));
                        bool nimbleBanned = hasBanned && banned.Any(x => x.Key == kp.Value && x.Value.Contains(CustomRoles.Nimble));
                        bool physicistBanned = hasBanned && banned.Any(x => x.Key == kp.Value && x.Value.Contains(CustomRoles.Physicist));
                        bool finderBanned = hasBanned && banned.Any(x => x.Key == kp.Value && x.Value.Contains(CustomRoles.Finder));
                        bool noisyBanned = hasBanned && banned.Any(x => x.Key == kp.Value && x.Value.Contains(CustomRoles.Noisy));

                        if (kp.Value.IsCrewmate())
                        {
                            if (!bloodlustBanned && !kp.Value.IsTasklessCrewmate()) bloodlustList.Add(player.PlayerId);
                            if (!nimbleBanned) nimbleList.Add(player.PlayerId);
                            if (kp.Value.GetRoleTypes() == RoleTypes.Crewmate)
                            {
                                if (!physicistBanned) physicistList.Add(player.PlayerId);
                                if (!finderBanned) finderList.Add(player.PlayerId);
                                if (!noisyBanned) noisyList.Add(player.PlayerId);
                            }
                        }
                    }
                }

                var roleSpawnMapping = new Dictionary<CustomRoles, (bool SpawnFlag, HashSet<byte> RoleList)>
                {
                    { CustomRoles.Bloodlust, (bloodlustSpawn, bloodlustList) },
                    { CustomRoles.Nimble, (nimbleSpawn, nimbleList) },
                    { CustomRoles.Physicist, (physicistSpawn, physicistList) },
                    { CustomRoles.Finder, (finderSpawn, finderList) },
                    { CustomRoles.Noisy, (noisySpawn, noisyList) }
                };

                for (int i = 0; i < roleSpawnMapping.Count; i++)
                {
                    (CustomRoles addon, (bool SpawnFlag, HashSet<byte> RoleList) value) = roleSpawnMapping.ElementAt(i);
                    if (value.RoleList.Count == 0) value.SpawnFlag = false;

                    if (Main.GM.Value) value.RoleList.Remove(0);

                    if (Main.AlwaysSpawnTogetherCombos.TryGetValue(OptionItem.CurrentPreset, out var combos) && combos.Values.Any(l => l.Contains(addon)))
                    {
                        var roles = combos.Where(x => x.Value.Contains(addon)).Select(x => x.Key).ToHashSet();
                        var players = RoleResult.Where(x => roles.Contains(x.Value) && x.Value.IsCrewmate() && (addon != CustomRoles.Bloodlust || x.Value.IsTasklessCrewmate()) && (addon == CustomRoles.Nimble || x.Value.GetRoleTypes() == RoleTypes.Crewmate) && !IsBasisChangingPlayer(x.Key, CustomRoles.Bloodlust)).Select(x => x.Key).ToHashSet();
                        if (players.Count > 0)
                        {
                            value.RoleList = players;
                            value.SpawnFlag = true;
                            roleSpawnMapping[addon] = value;
                        }
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
            }

            #endregion

            // Start CustomRpcSender
            RpcSetRoleReplacer.StartReplace();

            // Assign roles and create role maps for desync roles
            RpcSetRoleReplacer.AssignDesyncRoles();
            RpcSetRoleReplacer.SendRpcForDesync();

            // Assign roles and create role maps for normal roles
            RpcSetRoleReplacer.AssignNormalRoles();
            RpcSetRoleReplacer.SendRpcForNormal();

            // Send all RPCs
            RpcSetRoleReplacer.Release();

            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                //pc.Data.IsDead = false;
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
                if (kv.Value.IsDesyncRole() || IsBasisChangingPlayer(kv.Key, CustomRoles.Bloodlust)) continue;
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

            if (!overrideLovers && CustomRoles.Lovers.IsEnable() && (CustomRoles.FFF.IsEnable() ? -1 : random.Next(1, 100)) <= Lovers.LoverSpawnChances.GetInt()) AssignLoversRolesFromList();

            // Add-on assignment
            var aapc = Main.AllAlivePlayerControls.Shuffle();
            if (Main.GM.Value) aapc = aapc.Without(PlayerControl.LocalPlayer).ToArray();
            var addonNum = aapc.ToDictionary(x => x, _ => 0);
            AddonRolesList
                .Except(BasisChangingAddons.Keys)
                .Where(x => x.IsEnable() && aapc.Any(p => CustomRolesHelper.CheckAddonConflict(x, p)))
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

                if (!state.MainRole.IsImpostor()) state.SubRoles.RemoveAll(x => x.IsImpOnlyAddon());
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

                    if (pc.AmOwner && pc.GetCustomRole().GetDYRole() is RoleTypes.Shapeshifter or RoleTypes.Phantom)
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
                case CustomGameMode.CaptureTheFlag:
                    CTFManager.OnGameStart();
                    break;
                case CustomGameMode.NaturalDisasters:
                    NaturalDisasters.OnGameStart();
                    break;
                case CustomGameMode.RoomRush:
                    RoomRush.OnGameStart();
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
                case CustomGameMode.CaptureTheFlag:
                    GameEndChecker.SetPredicateToCaptureTheFlag();
                    break;
                case CustomGameMode.NaturalDisasters:
                    GameEndChecker.SetPredicateToNaturalDisasters();
                    break;
                case CustomGameMode.RoomRush:
                    GameEndChecker.SetPredicateToRoomRush();
                    break;
            }

            // Add players with unclassified roles to the list of players who require ResetCam.
            Main.ResetCamPlayerList.UnionWith(Main.PlayerStates.Where(p => (p.Value.MainRole.IsDesyncRole() && !p.Value.MainRole.UsesPetInsteadOfKill()) || p.Value.SubRoles.Contains(CustomRoles.Bloodlust)).Select(p => p.Key));
            Utils.CountAlivePlayers(true);
            Utils.SyncAllSettings();

            LateTask.New(() =>
            {
                Main.SetRoles = [];
                Main.SetAddOns = [];
                ChatCommands.DraftResult = [];
                ChatCommands.DraftRoles = [];
            }, 7f, log: false);

            if ((MapNames)Main.NormalOptions.MapId == MapNames.Airship && AmongUsClient.Instance.AmHost && Main.GM.Value)
            {
                LateTask.New(() => PlayerControl.LocalPlayer.NetTransform.SnapTo(new(15.5f, 0.0f), (ushort)(PlayerControl.LocalPlayer.NetTransform.lastSequenceId + 8)), 15f, "GM Auto-TP Failsafe"); // TP to Main Hall
            }

            LateTask.New(() => Main.HasJustStarted = false, 12f, "HasJustStarted to false");
        }
        catch (Exception ex)
        {
            Utils.ErrorEnd("Select Role Postfix");
            Utils.ThrowException(ex);
            yield break;
        }

        Logger.Info("Others assign finished", "AssignRoleTypes");
        yield return new WaitForSeconds(1f);

        Logger.Info("Send rpc disconnected for all", "AssignRoleTypes");
        DataDisconnected.Clear();
        RpcSetDisconnected(disconnected: true);

        yield return new WaitForSeconds(4f);

        Logger.Info("Assign self", "AssignRoleTypes");
        SetRoleSelf();

        RpcSetRoleReplacer.EndReplace();
    }

    private static bool IsBasisChangingPlayer(byte id, CustomRoles role) => BasisChangingAddons.TryGetValue(role, out var list) && list.Contains(id);

    private static void AssignDesyncRole(CustomRoles role, PlayerControl player, Dictionary<byte, CustomRpcSender> senders, Dictionary<(byte, byte), (RoleTypes, CustomRoles)> rolesMap, RoleTypes BaseRole, RoleTypes hostBaseRole = RoleTypes.Crewmate)
    {
        if (player == null) return;

        var hostId = PlayerControl.LocalPlayer.PlayerId;
        var isHost = player.PlayerId == hostId;

        Main.PlayerStates[player.PlayerId].SetMainRole(role);

        var selfRole = isHost ? BaseRole == RoleTypes.Shapeshifter ? RoleTypes.Shapeshifter : hostBaseRole : BaseRole;
        var othersRole = isHost ? RoleTypes.Crewmate : RoleTypes.Scientist;

        // Set Desync role for self and for others
        foreach (var target in Main.AllPlayerControls)
        {
            var targetRoleType = othersRole;
            var targetCustomRole = RoleResult.GetValueOrDefault(target.PlayerId, CustomRoles.CrewmateEHR);

            if (targetCustomRole.GetVNRole() is CustomRoles.Noisemaker)
                targetRoleType = RoleTypes.Noisemaker;

            rolesMap[(player.PlayerId, target.PlayerId)] = player.PlayerId != target.PlayerId ? (targetRoleType, targetCustomRole) : (selfRole, role);
        }

        // Set Desync role for others
        foreach (var seer in Main.AllPlayerControls.Where(x => player.PlayerId != x.PlayerId).ToArray())
            rolesMap[(seer.PlayerId, player.PlayerId)] = (othersRole, role);


        RpcSetRoleReplacer.OverriddenSenderList.Add(senders[player.PlayerId]);

        // Set role for host, but not self
        // canOverride should be false for the host during assign
        if (!isHost)
        {
            player.SetRole(othersRole, false);
        }

        Logger.Info($"Registered Role: {player.Data?.PlayerName} => {role} : RoleType for self => {selfRole}, for others => {othersRole}", "AssignDesyncRoles");
    }

    private static void MakeDesyncSender(Dictionary<byte, CustomRpcSender> senders, Dictionary<(byte, byte), (RoleTypes, CustomRoles)> rolesMap)
    {
        foreach (var seer in Main.AllPlayerControls)
        {
            foreach (var target in Main.AllPlayerControls)
            {
                if (seer.PlayerId == target.PlayerId || target.PlayerId == PlayerControl.LocalPlayer.PlayerId) continue;

                if (rolesMap.TryGetValue((seer.PlayerId, target.PlayerId), out var roleMap))
                {
                    try
                    {
                        var targetClientId = target.GetClientId();
                        if (targetClientId == -1) continue;

                        var roleType = roleMap.Item1;
                        var sender = senders[seer.PlayerId];
                        sender.RpcSetRole(seer, roleType, targetClientId);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }

    private static void SetRoleSelf()
    {
        foreach (var pc in Main.AllPlayerControls)
        {
            try
            {
                SetRoleSelf(pc);
            }
            catch
            {
            }
        }
    }

    private static void SetRoleSelf(PlayerControl target)
    {
        if (target == null) return;

        int targetClientId = target.GetClientId();
        if (targetClientId == -1) return;

        RoleTypes roleType = RpcSetRoleReplacer.RoleMap.TryGetValue((target.PlayerId, target.PlayerId), out var roleMap)
            ? roleMap.RoleType
            : RpcSetRoleReplacer.StoragedData[target.PlayerId];

        target.RpcSetRoleDesync(roleType, targetClientId);
    }

    public static void RpcSetDisconnected(bool disconnected)
    {
        foreach (var playerInfo in GameData.Instance.AllPlayers)
        {
            if (disconnected)
            {
                // if player left the game, remember current data
                DataDisconnected[playerInfo.PlayerId] = playerInfo.Disconnected;

                playerInfo.Disconnected = true;
                playerInfo.IsDead = false;
            }
            else
            {
                var data = DataDisconnected.GetValueOrDefault(playerInfo.PlayerId, true);
                playerInfo.Disconnected = data;
                playerInfo.IsDead = data;
            }

            var stream = MessageWriter.Get(SendOption.Reliable);
            stream.StartMessage(5);
            stream.Write(AmongUsClient.Instance.GameId);
            {
                stream.StartMessage(1);
                stream.WritePacked(playerInfo.NetId);
                playerInfo.Serialize(stream, false);
                stream.EndMessage();
            }
            stream.EndMessage();
            AmongUsClient.Instance.SendOrDisconnect(stream);
            stream.Recycle();
        }
    }

    private static void AssignCustomRole(CustomRoles role, byte id)
    {
        Main.PlayerStates[id].countTypes = role.GetCountTypes();
        Main.PlayerStates[id].SetMainRole(role);
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
            Logger.Info($"Add-on assigned: {player.Data?.PlayerName} = {player.GetCustomRole()} + {role}", "Assign Lovers");
        }

        RPC.SyncLoversPlayers();
    }

    // https://github.com/0xDrMoe/TownofHost-Enhanced/blob/41566d58e4217c38542df5b91f507045a6394908/Patches/onGameStartedPatch.cs#L667
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSetRole)), HarmonyPriority(Priority.High)]
    public static class RpcSetRoleReplacer
    {
        public static bool BlockSetRole;
        private static Dictionary<byte, CustomRpcSender> Senders = [];
        public static Dictionary<byte, RoleTypes> StoragedData = [];
        public static Dictionary<(byte SeerID, byte TargetID), (RoleTypes RoleType, CustomRoles CustomRole)> RoleMap = [];
        public static List<CustomRpcSender> OverriddenSenderList = [];

        public static void Initialize()
        {
            BlockSetRole = true;
            Senders = [];
            RoleMap = [];
            StoragedData = [];
            OverriddenSenderList = [];
        }

        // ReSharper disable once MemberHidesStaticFromOuterClass
        // ReSharper disable once UnusedMember.Global
        public static bool Prefix()
        {
            return !BlockSetRole;
        }

        public static void StartReplace()
        {
            foreach (var pc in Main.AllPlayerControls)
            {
                Senders[pc.PlayerId] = new CustomRpcSender($"{pc.name}'s SetRole Sender", SendOption.Reliable, false)
                    .StartMessage(pc.GetClientId());
            }
        }

        public static void AssignDesyncRoles()
        {
            // Assign desync roles
            foreach ((byte playerId, CustomRoles role) in RoleResult.Where(x => x.Value.IsDesyncRole() || IsBasisChangingPlayer(x.Key, CustomRoles.Bloodlust)).ToArray())
                AssignDesyncRole(role, Utils.GetPlayerById(playerId), Senders, RoleMap, BaseRole: ForceImp(playerId) ? RoleTypes.Impostor : role.GetDYRole());

            return;

            bool ForceImp(byte id) => IsBasisChangingPlayer(id, CustomRoles.Bloodlust) || (Options.CurrentGameMode == CustomGameMode.Speedrun && SpeedrunManager.CanKill.Contains(id));
        }

        public static void SendRpcForDesync()
        {
            MakeDesyncSender(Senders, RoleMap);
        }

        public static void AssignNormalRoles()
        {
            foreach ((byte playerId, CustomRoles role) in RoleResult)
            {
                var player = Utils.GetPlayerById(playerId);
                if (player == null || role.IsDesyncRole()) continue;
                if (Options.CurrentGameMode == CustomGameMode.Speedrun && SpeedrunManager.CanKill.Contains(playerId)) continue;

                var roleType = role.GetRoleTypes();

                if (BasisChangingAddons.FindFirst(x => x.Value.Contains(playerId), out var kvp))
                {
                    if (kvp.Key == CustomRoles.Bloodlust) continue;
                    roleType = kvp.Key switch
                    {
                        CustomRoles.Nimble => RoleTypes.Engineer,
                        CustomRoles.Physicist => RoleTypes.Scientist,
                        CustomRoles.Finder => RoleTypes.Tracker,
                        CustomRoles.Noisy => RoleTypes.Noisemaker,
                        _ => roleType
                    };
                }

                StoragedData.Add(playerId, roleType);

                foreach (var target in Main.AllPlayerControls)
                {
                    if (RoleResult.TryGetValue(target.PlayerId, out var targetRole) && targetRole.IsDesyncRole() && !target.IsHost()) continue;
                    RoleMap[(target.PlayerId, playerId)] = (roleType, role);
                }

                if (playerId != PlayerControl.LocalPlayer.PlayerId)
                {
                    // canOverride should be false for the host during assign
                    player.SetRole(roleType, false);
                }

                Logger.Info($"Set original role type => {player.GetRealName()}: {role} => {role.GetRoleTypes()}", "AssignNormalRoles");
            }
        }

        public static void SendRpcForNormal()
        {
            foreach ((byte targetId, CustomRpcSender sender) in Senders)
            {
                var target = Utils.GetPlayerById(targetId);
                if (OverriddenSenderList.Contains(sender)) continue;
                if (sender.CurrentState != CustomRpcSender.State.InRootMessage)
                    throw new InvalidOperationException("A CustomRpcSender had Invalid State.");

                foreach ((byte seerId, RoleTypes roleType) in StoragedData)
                {
                    if (targetId == seerId || targetId == PlayerControl.LocalPlayer.PlayerId) continue;
                    var seer = Utils.GetPlayerById(seerId);
                    if (seer == null || target == null) continue;

                    try
                    {
                        var targetClientId = target.GetClientId();
                        if (targetClientId == -1) continue;

                        // send rpc set role for other clients
                        sender.AutoStartRpc(seer.NetId, (byte)RpcCalls.SetRole, targetClientId)
                            .Write((ushort)roleType)
                            .Write(true) // canOverride
                            .EndRpc();
                    }
                    catch
                    {
                    }
                }

                sender.EndMessage();
            }
        }

        public static void Release()
        {
            BlockSetRole = false;
            Senders.Do(kvp => kvp.Value.SendMessage());
        }

        public static void EndReplace()
        {
            Senders = null;
            OverriddenSenderList = null;
            StoragedData = null;
        }
    }
}

[HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
static class FixIntroPatch
{
    public static void Postfix()
    {
        LateTask.New(() =>
        {
            if (CoShowIntroPatch.IntroStarted) return;
            Logger.Warn("Starting intro manually", "StartGameHostPatch");
            PlayerControl.AllPlayerControls.ForEach((Action<PlayerControl>)(PlayerNameColor.Set));
            PlayerControl.LocalPlayer.StopAllCoroutines();
            DestroyableSingleton<HudManager>.Instance.StartCoroutine(DestroyableSingleton<HudManager>.Instance.CoShowIntro());
            DestroyableSingleton<HudManager>.Instance.HideGameLoader();
        }, 1f, log: false);
    }
}