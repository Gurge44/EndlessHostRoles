using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
using UnityEngine.UI;
using static EHR.Modules.CustomRoleSelector;
using static EHR.Translator;
using DateTime = Il2CppSystem.DateTime;
using Exception = System.Exception;


namespace EHR;

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGame))]
internal static class ChangeRoleSettings
{
    public static bool BlockPopulateSkins;

    public static bool Prefix(AmongUsClient __instance)
    {
        if (!GameStates.IsLocalGame) return true;

        __instance.StartCoroutine(CoStartGame().WrapToIl2Cpp());
        return false;

        IEnumerator<object> CoStartGame()
        {
            AmongUsClient amongUsClient = __instance;

            if (FastDestroyableSingleton<HudManager>.Instance.GameMenu.IsOpen)
                FastDestroyableSingleton<HudManager>.Instance.GameMenu.Close();

            FastDestroyableSingleton<UnityTelemetry>.Instance.Init();
            amongUsClient.logger.Info("Received game start: " + amongUsClient.AmHost);
            yield return null;

            while (!DestroyableSingleton<HudManager>.InstanceExists)
                yield return null;

            while (!PlayerControl.LocalPlayer)
                yield return null;

            PlayerControl.LocalPlayer.moveable = false;
            PlayerControl.LocalPlayer.MyPhysics.inputHandler.enabled = true;
            var objectOfType1 = Object.FindObjectOfType<PlayerCustomizationMenu>();

            if (objectOfType1)
                objectOfType1.Close(false);

            var objectOfType2 = Object.FindObjectOfType<GameSettingMenu>();

            if (objectOfType2)
                objectOfType2.Close();

            if (DestroyableSingleton<GameStartManager>.InstanceExists)
            {
                amongUsClient.DisconnectHandlers.Remove(FastDestroyableSingleton<GameStartManager>.Instance.CastFast<IDisconnectHandler>());
                Object.Destroy(FastDestroyableSingleton<GameStartManager>.Instance.gameObject);
            }

            if (DestroyableSingleton<LobbyInfoPane>.InstanceExists)
                Object.Destroy(FastDestroyableSingleton<LobbyInfoPane>.Instance.gameObject);

            if (DestroyableSingleton<DiscordManager>.InstanceExists)
                FastDestroyableSingleton<DiscordManager>.Instance.SetPlayingGame();

            if (!string.IsNullOrEmpty(DataManager.Player.Store.ActiveCosmicube))
                AmongUsClient.Instance.SetActivePodType(FastDestroyableSingleton<CosmicubeManager>.Instance.GetCubeDataByID(DataManager.Player.Store.ActiveCosmicube).podId);
            else
            {
                PlayerStorageManager.CloudPlayerPrefs playerPrefs = FastDestroyableSingleton<PlayerStorageManager>.Instance.PlayerPrefs;
                AmongUsClient.Instance.SetActivePodType(playerPrefs.ActivePodType);
            }

            FastDestroyableSingleton<FriendsListManager>.Instance.ConfirmationScreen.Cancel();
            FastDestroyableSingleton<FriendsListManager>.Instance.Ui.Close(true);
            FastDestroyableSingleton<FriendsListManager>.Instance.ReparentUI();

            try { CosmeticsCache.ClearUnusedCosmetics(); }
            catch (Exception e) { Utils.ThrowException(e); }

            yield return FastDestroyableSingleton<HudManager>.Instance.CoFadeFullScreen(Color.clear, Color.black);
            ++DataManager.Player.Ban.BanPoints;
            DataManager.Player.Ban.PreviousGameStartDate = DateTime.UtcNow;
            DataManager.Player.Save();

            if (amongUsClient.AmHost)
                yield return amongUsClient.CoStartGameHost();
            else
            {
                yield return amongUsClient.CoStartGameClient();

                if (amongUsClient.AmHost)
                    yield return amongUsClient.CoStartGameHost();
            }

            for (var index = 0; index < GameData.Instance.PlayerCount; ++index)
            {
                PlayerControl player = GameData.Instance.AllPlayers[index].Object;

                if (player)
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

            FastDestroyableSingleton<FriendsListManager>.Instance.SetRecentlyPlayed(GameData.Instance.AllPlayers);
            GameData.TimeGameStarted = Time.realtimeSinceStartup;
            int map = Mathf.Clamp(GameOptionsManager.Instance.CurrentGameOptions.MapId, 0, Constants.MapNames.Length - 1);
            string gameName = GameCode.IntToGameName(AmongUsClient.Instance.GameId);
            FastDestroyableSingleton<DebugAnalytics>.Instance.Analytics.StartGame(PlayerControl.LocalPlayer.Data, GameData.Instance.PlayerCount, GameOptionsManager.Instance.CurrentGameOptions.NumImpostors, AmongUsClient.Instance.NetworkMode, (MapNames)map, GameOptionsManager.Instance.CurrentGameOptions.GameMode, gameName, FastDestroyableSingleton<ServerManager>.Instance.CurrentRegion.Name, GameOptionsManager.Instance.CurrentGameOptions, GameData.Instance.AllPlayers);

            try
            {
                FastDestroyableSingleton<UnityTelemetry>.Instance.StartGame(AmongUsClient.Instance.AmHost, GameData.Instance.PlayerCount, GameOptionsManager.Instance.CurrentGameOptions.NumImpostors, AmongUsClient.Instance.NetworkMode, DataManager.Player.Stats.GetStat(StatID.GamesAsImpostor), DataManager.Player.Stats.GetStat(StatID.GamesStarted), DataManager.Player.Stats.GetStat(StatID.CrewmateStreak));
                NetworkedPlayerInfo.PlayerOutfit defaultOutfit = PlayerControl.LocalPlayer.Data.DefaultOutfit;
                FastDestroyableSingleton<UnityTelemetry>.Instance.StartGameCosmetics(defaultOutfit.ColorId, defaultOutfit.HatId, defaultOutfit.SkinId, defaultOutfit.PetId, defaultOutfit.VisorId, defaultOutfit.NamePlateId);
            }
            catch { }

            GameDebugCommands.AddCommands();
        }
    }

    public static void Postfix(AmongUsClient __instance)
    {
        try { LobbySharingAPI.NotifyLobbyStatusChanged(LobbyStatus.In_Game); }
        catch (Exception e) { Utils.ThrowException(e); }

        SetUpRoleTextPatch.IsInIntro = true;

        Main.OverrideWelcomeMsg = string.Empty;

        try
        {
            new[]
            {
                RoleTypes.GuardianAngel,
                RoleTypes.Scientist,
                RoleTypes.Engineer,
                RoleTypes.Shapeshifter,
                RoleTypes.Noisemaker,
                RoleTypes.Phantom,
                RoleTypes.Tracker
            }.Do(x => Main.NormalOptions.roleOptions.SetRoleRate(x, 0, 0));

            if (Main.NormalOptions.MapId > 5) Logger.SendInGame(GetString("UnsupportedMap"));

            Utils.GameStartTimeStamp = Utils.TimeStamp;

            try { Main.AllRoleClasses.Do(x => x.Init()); }
            catch (Exception e) { Utils.ThrowException(e); }

            Main.PlayerStates = [];

            Main.AbilityUseLimit = [];

            Main.HasJustStarted = true;

            Main.AllPlayerKillCooldown = [];
            Main.AllPlayerSpeed = [];
            Main.KillTimers = [];
            Main.SleuthMsgs = [];
            Main.SuperStarDead = [];
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
            Main.GuesserGuessed = [];
            Main.GuesserGuessedMeeting = [];
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
            Lazy.BeforeMeetingPositions = [];

            ReportDeadBodyPatch.CanReport = [];
            SabotageMapPatch.TimerTexts = [];
            GuessManager.Guessers = [];

            Options.UsedButtonCount = 0;

            ChatCommands.Spectators.UnionWith(ChatCommands.ForcedSpectators);
            ChatCommands.ForcedSpectators.Clear();
            ChatCommands.LastSpectators.Clear();
            ChatCommands.LastSpectators.UnionWith(ChatCommands.Spectators);

            RPCHandlerPatch.RemoveExpiredWhiteList();

            try
            {
                (OptionItem MinSetting, OptionItem MaxSetting) impLimits = Options.FactionMinMaxSettings[Team.Impostor];
                int optImpNum = IRandom.Instance.Next(impLimits.MinSetting.GetInt(), impLimits.MaxSetting.GetInt() + 1);
                GameOptionsManager.Instance.currentNormalGameOptions.NumImpostors = optImpNum;
                GameOptionsManager.Instance.CurrentGameOptions.SetInt(Int32OptionNames.NumImpostors, optImpNum);
            }
            catch (Exception e) { Utils.ThrowException(e); }

            GameOptionsManager.Instance.currentNormalGameOptions.ConfirmImpostor = false;
            Main.RealOptionsData = new(GameOptionsManager.Instance.CurrentGameOptions);

            Main.IntroDestroyed = false;
            ShipStatusBeginPatch.RolesIsAssigned = false;
            GameEndChecker.Ended = false;

            ShipStatusFixedUpdatePatch.ClosestVent = [];
            ShipStatusFixedUpdatePatch.CanUseClosestVent = [];

            RandomSpawn.CustomNetworkTransformHandleRpcPatch.HasSpawned = [];
            Coven.Coven.CovenMeetingStartPatch.MeetingNum = 0;

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
                string[] invalidColor = Main.AllPlayerControls.Where(p => p.Data.DefaultOutfit.ColorId < 0 || Palette.PlayerColors.Length <= p.Data.DefaultOutfit.ColorId).Select(p => $"{p.name}").ToArray();

                if (invalidColor.Length > 0)
                {
                    string msg = GetString("Error.InvalidColor");
                    Logger.SendInGame(msg);
                    msg += "\n" + string.Join(",", invalidColor);
                    Utils.SendMessage(msg, sendOption: SendOption.None);
                    Logger.Error(msg, "CoStartGame");
                }
            }

            RoleResult = [];

            foreach (PlayerControl target in Main.AllPlayerControls)
            {
                foreach (PlayerControl seer in Main.AllPlayerControls)
                {
                    (byte, byte) pair = (target.PlayerId, seer.PlayerId);
                    Main.LastNotifyNames[pair] = target.name;
                }
            }

            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                int colorId = pc.Data.DefaultOutfit.ColorId;
                if (AmongUsClient.Instance.AmHost && Options.FormatNameMode.GetInt() == 1) pc.RpcSetName(Palette.GetColorName(colorId));

                Main.PlayerStates[pc.PlayerId] = new(pc.PlayerId);
                Main.AllPlayerSpeed[pc.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                Main.AllPlayerKillCooldown[pc.PlayerId] = Options.DefaultKillCooldown + Main.CurrentMap switch
                {
                    MapNames.Polus => Options.ExtraKillCooldownOnPolus.GetFloat(),
                    MapNames.Airship => Options.ExtraKillCooldownOnAirship.GetFloat(),
                    MapNames.Fungle => Options.ExtraKillCooldownOnFungle.GetFloat(),
                    _ => 0f
                };
                ReportDeadBodyPatch.CanReport[pc.PlayerId] = true;
                ReportDeadBodyPatch.WaitReport[pc.PlayerId] = [];
                RoleResult[pc.PlayerId] = CustomRoles.NotAssigned;
                pc.cosmetics.nameText.text = pc.name;
                RandomSpawn.CustomNetworkTransformHandleRpcPatch.HasSpawned.Clear();
                NetworkedPlayerInfo.PlayerOutfit outfit = pc.Data.DefaultOutfit;
                Camouflage.PlayerSkins[pc.PlayerId] = new NetworkedPlayerInfo.PlayerOutfit().Set(outfit.PlayerName, outfit.ColorId, outfit.HatId, outfit.SkinId, outfit.VisorId, outfit.PetId, outfit.NamePlateId);
                Main.ClientIdList.Add(pc.OwnerId);

                try { Main.PlayerColors[pc.PlayerId] = Palette.PlayerColors[colorId]; }
                catch (Exception e) { Utils.ThrowException(e); }
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
            catch (Exception ex) { Logger.Exception(ex, "Init Roles"); }

            Main.ChangedRole = false;

            try
            {
                SoloPVP.Init();
                FreeForAll.Init();
                MoveAndStop.Init();
                HotPotato.Init();
                CustomHnS.Init();
                Speedrun.Init();
            }
            catch (Exception e) { Utils.ThrowException(e); }

            try
            {
                CustomWinnerHolder.Reset();
                AntiBlackout.Reset();
                NameNotifyManager.Reset();
                SabotageSystemTypeRepairDamagePatch.Initialize();
                DoorsReset.Initialize();
                GhostRolesManager.Initialize();
                RoleBlockManager.Reset();
                ChatManager.ResetHistory();
                CustomNetObject.Reset();
            }
            catch (Exception e) { Utils.ThrowException(e); }

            IRandom.SetInstanceById(Options.RoleAssigningAlgorithm.GetValue());

            MeetingStates.MeetingNum = 0;
            MeetingStates.MeetingCalled = false;
            MeetingStates.FirstMeeting = true;
            GameStates.AlreadyDied = false;

            Main.Instance.StartCoroutine(PopulateSkinItems());
        }
        catch (Exception ex)
        {
            Utils.ErrorEnd("Change Role Setting Postfix");
            Utils.ThrowException(ex);
        }

        return;

        IEnumerator PopulateSkinItems()
        {
            while (!ShipStatus.Instance) yield return null;
            BlockPopulateSkins = false;
            LateTask.New(() => BlockPopulateSkins = true, 0.5f, log: false);
            yield return ShipStatus.Instance.CosmeticsCache.PopulateFromPlayers();
        }
    }
}

[HarmonyPatch(typeof(CosmeticsCache), nameof(CosmeticsCache.PopulateFromPlayers))]
internal static class BlockPopulateFromPlayersPatch
{
    public static bool Prefix()
    {
        return !ChangeRoleSettings.BlockPopulateSkins;
    }
}

[HarmonyPatch]
internal static class StartGameHostPatch
{
    private static AmongUsClient AUClient;

    public static readonly Dictionary<CustomRoles, List<byte>> BasisChangingAddons = [];
    private static Dictionary<RoleTypes, int> RoleTypeNums = [];

    public static readonly Dictionary<byte, bool> DataDisconnected = [];

    private static RoleOptionsCollectionV09 RoleOpt => Main.NormalOptions.roleOptions;

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

    private static IEnumerator WaitAndSmoothlyUpdate(this LoadingBarManager loadingBarManager, float startPercent, float targetPercent, float duration, string loadingText)
    {
        float startTime = Time.time;

        while (Time.time - startTime < duration)
        {
            float t = (Time.time - startTime) / duration;
            float newPercent = Mathf.Lerp(startPercent, targetPercent, t);

            try
            {
                loadingBarManager.SetLoadingPercent(newPercent, StringNames.LoadingBarGameStart);
                loadingBarManager.loadingBar.loadingText.text = loadingText;
            }
            catch (Exception e) { Utils.ThrowException(e); }

            yield return null;
        }

        try
        {
            loadingBarManager.SetLoadingPercent(targetPercent, StringNames.LoadingBarGameStart);
            loadingBarManager.loadingBar.loadingText.text = loadingText;
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGameHost))]
    [HarmonyPrefix]
    public static bool CoStartGameHost_Prefix(AmongUsClient __instance, ref Il2CppSystem.Collections.IEnumerator __result)
    {
        AUClient = __instance;
        __result = StartGameHost().WrapToIl2Cpp();
        return false;
    }

    private static IEnumerator StartGameHost()
    {
        string loadingTextText1 = GetString("LoadingBarText.1");
        LoadingBarManager loadingBarManager = FastDestroyableSingleton<LoadingBarManager>.Instance;

        try
        {
            loadingBarManager.ToggleLoadingBar(true);
            loadingBarManager.SetLoadingPercent(0f, StringNames.LoadingBarGameStart);
            loadingBarManager.loadingBar.loadingText.DestroyTranslator();
            loadingBarManager.loadingBar.loadingText.text = loadingTextText1;

            var loadingBarLogo = GameObject.Find("Loading Bar Manager/Loading Bar/Canvas/Logo")?.GetComponent<Image>();

            if (loadingBarLogo)
            {
                loadingBarLogo.sprite = Utils.LoadSprite("EHR.Resources.Images.EHR-Icon.png", 600f);
                loadingBarLogo.SetNativeSize();
            }

            var fillImage = GameObject.Find("Loading Bar Manager/Loading Bar/Canvas/Bar/Fill")?.GetComponent<Image>();
            if (fillImage) fillImage.color = new Color(0f, 0.647f, 1f, 1f);
        }
        catch (Exception e) { Utils.ThrowException(e); }

        if (LobbyBehaviour.Instance)
            LobbyBehaviour.Instance.Despawn();

        if (!ShipStatus.Instance)
        {
            int index = Mathf.Clamp(GameOptionsManager.Instance.CurrentGameOptions.MapId, 0, Constants.MapNames.Length - 1);
            AUClient.ShipLoadingAsyncHandle = AUClient.ShipPrefabs[index].InstantiateAsync();

            while (!AUClient.ShipLoadingAsyncHandle.IsDone)
            {
                float progress = AUClient.ShipLoadingAsyncHandle.PercentComplete;
                float displayPercent = Mathf.Lerp(0f, 10f, progress);
                loadingBarManager.SetLoadingPercent(displayPercent, StringNames.LoadingBarGameStart);
                loadingBarManager.loadingBar.loadingText.text = loadingTextText1;
                yield return null;
            }

            GameObject result = AUClient.ShipLoadingAsyncHandle.Result;
            ShipStatus.Instance = result.GetComponent<ShipStatus>();
            AUClient.Spawn(ShipStatus.Instance);
        }

        try
        {
            loadingBarManager.SetLoadingPercent(10f, StringNames.LoadingBarGameStart);
            loadingBarManager.loadingBar.loadingText.text = loadingTextText1;
        }
        catch (Exception e) { Utils.ThrowException(e); }

        DateTime start = DateTime.Now;

        while (true)
        {
            var flag = true;
            var num = 10;
            var totalSeconds = (float)(DateTime.Now - start).TotalSeconds;

            if (GameOptionsManager.Instance.CurrentGameOptions.MapId == 5 || GameOptionsManager.Instance.CurrentGameOptions.MapId == 4)
                num = 15;

            var clientsReady = 0;
            int allClientsCount = AUClient.allClients.Count;

            lock (AUClient.allClients)
            {
                for (var index = 0; index < AUClient.allClients.Count; ++index)
                {
                    ClientData allClient = AUClient.allClients[index];

                    if (allClient.Id != AUClient.ClientId && !allClient.IsReady)
                    {
                        if (totalSeconds < (double)num)
                            flag = false;
                        else
                        {
                            AUClient.SendLateRejection(allClient.Id, DisconnectReasons.ClientTimeout);
                            allClient.IsReady = true;
                            AUClient.OnPlayerLeft(allClient, DisconnectReasons.ClientTimeout);
                        }
                    }
                    else
                        ++clientsReady;
                }
            }

            try
            {
                if (totalSeconds < (double)num)
                {
                    loadingBarManager.SetLoadingPercent(10 + (float)(totalSeconds / (double)num * 80.0), StringNames.LoadingBarGameStartWaitingPlayers);

                    int timeoutIn = num - (int)totalSeconds;
                    loadingBarManager.loadingBar.loadingText.text = string.Format(GetString("LoadingBarText.2"), clientsReady, allClientsCount, timeoutIn);
                }
            }
            catch (Exception e) { Utils.ThrowException(e); }

            if (!flag)
                yield return new WaitForEndOfFrame();
            else
                break;
        }

        AUClient.SendClientReady();
        yield return AssignRoles();
    }

    private static IEnumerator AssignRoles()
    {
        if (AmongUsClient.Instance.IsGameOver || GameStates.IsLobby || GameEndChecker.Ended) yield break;

        RpcSetRoleReplacer.Initialize();

        SelectCustomRoles();
        SelectAddonRoles();
        CalculateVanillaRoleCount();

        UpdateRoleTypeNums();

        foreach ((RoleTypes roleTypes, int roleNum) in RoleTypeNums)
            RoleOpt.SetRoleRate(roleTypes, roleNum, roleNum > 0 ? 100 : RoleOpt.GetChancePerGame(roleTypes));

        Statistics.OnRoleSelectionComplete();

        #region BasisChangingAddonsSetup

        try
        {
            BasisChangingAddons.Clear();

            var random = IRandom.Instance;

            if (Options.CurrentGameMode == CustomGameMode.Standard)
            {
                bool bloodlustSpawn = random.Next(100) < (Options.CustomAdtRoleSpawnRate.TryGetValue(CustomRoles.Bloodlust, out IntegerOptionItem option0) ? option0.GetFloat() : 0) && CustomRoles.Bloodlust.IsEnable() && Options.RoleSubCategoryLimits[RoleOptionType.Neutral_Killing][2].GetInt() > 0;
                bool physicistSpawn = random.Next(100) < (Options.CustomAdtRoleSpawnRate.TryGetValue(CustomRoles.Physicist, out IntegerOptionItem option1) ? option1.GetFloat() : 0) && CustomRoles.Physicist.IsEnable();
                bool nimbleSpawn = random.Next(100) < (Options.CustomAdtRoleSpawnRate.TryGetValue(CustomRoles.Nimble, out IntegerOptionItem option2) ? option2.GetFloat() : 0) && CustomRoles.Nimble.IsEnable();
                bool finderSpawn = random.Next(100) < (Options.CustomAdtRoleSpawnRate.TryGetValue(CustomRoles.Finder, out IntegerOptionItem option3) ? option3.GetFloat() : 0) && CustomRoles.Finder.IsEnable();
                bool noisySpawn = random.Next(100) < (Options.CustomAdtRoleSpawnRate.TryGetValue(CustomRoles.Noisy, out IntegerOptionItem option4) ? option4.GetFloat() : 0) && CustomRoles.Noisy.IsEnable();

                if (Options.EveryoneCanVent.GetBool())
                {
                    nimbleSpawn = false;
                    physicistSpawn = false;
                    finderSpawn = false;
                    noisySpawn = false;
                }

                HashSet<byte> bloodlustList = [], nimbleList = [], physicistList = [], finderList = [], noisyList = [];
                bool hasBanned = Main.NeverSpawnTogetherCombos.TryGetValue(OptionItem.CurrentPreset, out Dictionary<CustomRoles, List<CustomRoles>> banned);

                if (nimbleSpawn || physicistSpawn || finderSpawn || noisySpawn || bloodlustSpawn)
                {
                    foreach (PlayerControl player in Main.AllPlayerControls)
                    {
                        if (IsBasisChangingPlayer(player.PlayerId, CustomRoles.Bloodlust)) continue;

                        KeyValuePair<byte, CustomRoles> kp = RoleResult.FirstOrDefault(x => x.Key == player.PlayerId);

                        bool bloodlustBanned = hasBanned && banned.Any(x => x.Key == kp.Value && x.Value.Contains(CustomRoles.Bloodlust));
                        bool nimbleBanned = hasBanned && banned.Any(x => x.Key == kp.Value && x.Value.Contains(CustomRoles.Nimble));
                        bool physicistBanned = hasBanned && banned.Any(x => x.Key == kp.Value && x.Value.Contains(CustomRoles.Physicist));
                        bool finderBanned = hasBanned && banned.Any(x => x.Key == kp.Value && x.Value.Contains(CustomRoles.Finder));
                        bool noisyBanned = hasBanned && banned.Any(x => x.Key == kp.Value && x.Value.Contains(CustomRoles.Noisy));

                        if (kp.Value.IsCrewmate())
                        {
                            if (!bloodlustBanned && !kp.Value.IsTaskBasedCrewmate()) bloodlustList.Add(player.PlayerId);
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

                Dictionary<CustomRoles, (bool SpawnFlag, HashSet<byte> RoleList)> roleSpawnMapping = new()
                {
                    { CustomRoles.Bloodlust, (bloodlustSpawn, bloodlustList) },
                    { CustomRoles.Nimble, (nimbleSpawn, nimbleList) },
                    { CustomRoles.Physicist, (physicistSpawn, physicistList) },
                    { CustomRoles.Finder, (finderSpawn, finderList) },
                    { CustomRoles.Noisy, (noisySpawn, noisyList) }
                };

                for (var i = 0; i < roleSpawnMapping.Count; i++)
                {
                    (CustomRoles addon, (bool SpawnFlag, HashSet<byte> RoleList) value) = roleSpawnMapping.ElementAt(i);
                    if (value.RoleList.Count == 0) value.SpawnFlag = false;
                    if (!value.SpawnFlag) value.RoleList.Clear();

                    if (Main.GM.Value) value.RoleList.Remove(0);
                    value.RoleList.ExceptWith(ChatCommands.Spectators);

                    if (Main.AlwaysSpawnTogetherCombos.TryGetValue(OptionItem.CurrentPreset, out Dictionary<CustomRoles, List<CustomRoles>> combos) && combos.Values.Any(l => l.Contains(addon)))
                    {
                        HashSet<CustomRoles> roles = combos.Where(x => x.Value.Contains(addon)).Select(x => x.Key).ToHashSet();
                        HashSet<byte> players = RoleResult.Where(x => roles.Contains(x.Value) && x.Value.IsCrewmate() && (addon != CustomRoles.Bloodlust || x.Value.IsTasklessCrewmate()) && (addon == CustomRoles.Nimble || x.Value.GetRoleTypes() == RoleTypes.Crewmate) && !IsBasisChangingPlayer(x.Key, CustomRoles.Bloodlust)).Select(x => x.Key).ToHashSet();

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
                        HashSet<byte> newRoleList = Main.SetAddOns.Where(x => x.Value.Contains(addon)).Select(x => x.Key).ToHashSet();
                        if (value.RoleList.Count != 1 || value.RoleList.First() != newRoleList.First()) value.RoleList = newRoleList;

                        roleSpawnMapping[addon] = value;
                    }
                }

                foreach ((CustomRoles addon, (bool spawnFlag, HashSet<byte> roleList)) in roleSpawnMapping)
                {
                    if (spawnFlag)
                    {
                        foreach ((CustomRoles otherAddon, (bool otherSpawnFlag, _)) in roleSpawnMapping)
                        {
                            if (otherAddon != addon && otherSpawnFlag && BasisChangingAddons.TryGetValue(otherAddon, out List<byte> otherList))
                                roleList.ExceptWith(otherList);
                        }

                        BasisChangingAddons[addon] = roleList.Shuffle().Take(addon.GetCount()).ToList();
                    }
                }
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }

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

        try
        {
            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                if (Main.PlayerStates[pc.PlayerId].MainRole != CustomRoles.NotAssigned) continue;

                CustomRoles role = Enum.TryParse($"{pc.Data.Role.Role}EHR", out CustomRoles parsedRole) ? parsedRole : CustomRoles.NotAssigned;
                if (role == CustomRoles.NotAssigned) Logger.SendInGame(string.Format(GetString("Error.InvalidRoleAssignment"), pc?.Data?.PlayerName));

                Main.PlayerStates[pc.PlayerId].SetMainRole(role);
            }

            foreach (KeyValuePair<byte, CustomRoles> kv in RoleResult)
            {
                if (kv.Value.IsDesyncRole() || IsBasisChangingPlayer(kv.Key, CustomRoles.Bloodlust)) continue;

                Main.PlayerStates[kv.Key].SetMainRole(kv.Value);
            }

            if (Options.CurrentGameMode != CustomGameMode.Standard)
            {
                foreach (KeyValuePair<byte, PlayerState> pair in Main.PlayerStates)
                    ExtendedPlayerControl.RpcSetCustomRole(pair.Key, pair.Value.MainRole);

                goto EndOfSelectRolePatch;
            }

            BasisChangingAddons.Do(x => x.Value.Do(y => Main.PlayerStates[y].SetSubRole(x.Key)));

            var overrideLovers = false;

            if (Main.SetAddOns.Count(x => x.Value.Contains(CustomRoles.Lovers)) == 2)
            {
                Main.LoversPlayers.Clear();
                Main.IsLoversDead = false;
                overrideLovers = true;
                Logger.Warn("Lovers overridden by host's pre-set add-ons", "CustomRoleSelector");
            }

            foreach (KeyValuePair<byte, List<CustomRoles>> item in Main.SetAddOns)
            {
                if (Main.PlayerStates.TryGetValue(item.Key, out PlayerState state))
                {
                    foreach (CustomRoles role in item.Value)
                    {
                        if (role is CustomRoles.Nimble or CustomRoles.Physicist or CustomRoles.Bloodlust or CustomRoles.Finder or CustomRoles.Noisy) continue;

                        state.SetSubRole(role);
                        if (overrideLovers && role == CustomRoles.Lovers) Main.LoversPlayers.Add(Utils.GetPlayerById(item.Key));

                        if (role.IsGhostRole()) GhostRolesManager.SpecificAssignGhostRole(item.Key, role, true);
                    }
                }
            }

            if (!overrideLovers && CustomRoles.Lovers.IsEnable() && (CustomRoles.FFF.IsEnable() ? -1 : IRandom.Instance.Next(1, 100)) <= Lovers.LoverSpawnChances.GetInt()) AssignLoversRolesFromList();

            // Add-on assignment
            PlayerControl[] aapc = Main.AllAlivePlayerControls.Shuffle();
            if (Main.GM.Value) aapc = aapc.Without(PlayerControl.LocalPlayer).ToArray();
            aapc = aapc.Where(x => !ChatCommands.Spectators.Contains(x.PlayerId)).ToArray();

            Dictionary<PlayerControl, int> addonNum = aapc.ToDictionary(x => x, _ => 0);

            AddonRolesList
                .Except(BasisChangingAddons.Keys)
                .Where(x => x.IsEnable() && aapc.Any(p => CustomRolesHelper.CheckAddonConflict(x, p)))
                .SelectMany(x => Enumerable.Repeat(x, Math.Clamp(x.GetCount(), 0, aapc.Length)))
                .Where(x => IRandom.Instance.Next(1, 100) <= (Options.CustomAdtRoleSpawnRate.TryGetValue(x, out IntegerOptionItem sc) ? sc.GetFloat() : 0))
                .OrderBy(x => Options.CustomAdtRoleSpawnRate.TryGetValue(x, out IntegerOptionItem sc) && sc.GetInt() == 100 ? IRandom.Instance.Next(100) : IRandom.Instance.Next(100, 1000))
                .Select(x =>
                {
                    PlayerControl suitablePlayer = aapc
                        .OrderBy(p => addonNum[p])
                        .FirstOrDefault(p => CustomRolesHelper.CheckAddonConflict(x, p));

                    if (suitablePlayer != null) addonNum[suitablePlayer]++;

                    return (Role: x, SuitablePlayer: suitablePlayer);
                })
                .DoIf(x => x.SuitablePlayer != null, x => Main.PlayerStates[x.SuitablePlayer.PlayerId].SetSubRole(x.Role));


            foreach (PlayerState state in Main.PlayerStates.Values)
            {
                if (Main.NeverSpawnTogetherCombos.TryGetValue(OptionItem.CurrentPreset, out Dictionary<CustomRoles, List<CustomRoles>> neverList) && neverList.TryGetValue(state.MainRole, out List<CustomRoles> bannedAddonList))
                    bannedAddonList.ForEach(x => state.RemoveSubRole(x));

                if (Main.AlwaysSpawnTogetherCombos.TryGetValue(OptionItem.CurrentPreset, out Dictionary<CustomRoles, List<CustomRoles>> alwaysList) && alwaysList.TryGetValue(state.MainRole, out List<CustomRoles> addonList))
                    addonList.ForEach(x => state.SetSubRole(x));

                if (!state.MainRole.IsImpostor() && !(state.MainRole == CustomRoles.Traitor && Traitor.CanGetImpostorOnlyAddons.GetBool()))
                    state.SubRoles.RemoveAll(x => x.IsImpOnlyAddon());
            }

            foreach (KeyValuePair<byte, PlayerState> pair in Main.PlayerStates)
            {
                ExtendedPlayerControl.RpcSetCustomRole(pair.Key, pair.Value.MainRole);

                StringBuilder sb = new();

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
                        Main.CheckShapeshift[pc.PlayerId] = false;

                    if (pc.AmOwner && pc.GetCustomRole().GetDYRole() is RoleTypes.Shapeshifter or RoleTypes.Phantom)
                    {
                        foreach (PlayerControl target in Main.AllPlayerControls)
                        {
                            target.Data.Role.CanBeKilled = true;

                            target.cosmetics.SetNameColor(Color.white);
                            target.Data.Role.NameColor = Color.white;
                        }
                    }
                }
                catch (Exception ex) { Logger.Error(ex.ToString(), "OnGameStartedPatch Add methods"); }
            }

            try
            {
                Stressed.Add();
                Asthmatic.Add();
                Circumvent.Add();
                Dynamo.Add();
                Spurt.Add();
                Allergic.Init();
                Lovers.Init();
            }
            catch (Exception e) { Utils.ThrowException(e); }

            LateTask.New(CustomTeamManager.InitializeCustomTeamPlayers, 7f, log: false);

            if (overrideLovers) Logger.Msg(Main.LoversPlayers.Join(x => x?.GetRealName()), "Lovers");

            EndOfSelectRolePatch:

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.HideAndSeek:
                    CustomHnS.StartSeekerBlindTime();
                    break;
                case CustomGameMode.CaptureTheFlag:
                    CaptureTheFlag.Init();
                    break;
                case CustomGameMode.NaturalDisasters:
                    NaturalDisasters.OnGameStart();
                    break;
                case CustomGameMode.KingOfTheZones:
                    KingOfTheZones.Init();
                    break;
                case CustomGameMode.Quiz:
                    Quiz.Init();
                    break;
            }

            HudManager.Instance.SetHudActive(true);

            foreach (PlayerControl pc in Main.AllPlayerControls)
                pc.ResetKillCooldown(false);


            foreach (KeyValuePair<RoleTypes, int> roleType in RoleTypeNums)
            {
                var roleNum = 0;
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
                case CustomGameMode.KingOfTheZones:
                    GameEndChecker.SetPredicateToKingOfTheZones();
                    break;
                case CustomGameMode.Quiz:
                    GameEndChecker.SetPredicateToQuiz();
                    break;
                case CustomGameMode.TheMindGame:
                    GameEndChecker.SetPredicateToTheMindGame();
                    break;
            }

            // Add players with unclassified roles to the list of players who require ResetCam.
            Main.ResetCamPlayerList.UnionWith(Main.PlayerStates.Where(p => (p.Value.MainRole.IsDesyncRole() && !p.Key.GetPlayer().UsesPetInsteadOfKill()) || p.Value.SubRoles.Contains(CustomRoles.Bloodlust)).Select(p => p.Key));
            Utils.CountAlivePlayers(true);

            LateTask.New(() =>
            {
                Main.SetRoles = [];
                Main.SetAddOns = [];
                ChatCommands.DraftResult = [];
                ChatCommands.DraftRoles = [];
            }, 7f, log: false);

            if ((MapNames)Main.NormalOptions.MapId == MapNames.Airship && AmongUsClient.Instance.AmHost && Main.GM.Value) LateTask.New(() => PlayerControl.LocalPlayer.NetTransform.SnapTo(new(15.5f, 0.0f), (ushort)(PlayerControl.LocalPlayer.NetTransform.lastSequenceId + 8)), 15f, "GM Auto-TP Failsafe"); // TP to Main Hall

            LateTask.New(() => Main.HasJustStarted = false, 12f, "HasJustStarted to false");
        }
        catch (Exception ex)
        {
            Utils.ErrorEnd("Select Role Postfix");
            Utils.ThrowException(ex);
            yield break;
        }


        foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
        {
            if (pc.Data == null)
                while (pc.Data == null)
                    yield return null;

            pc.Data.Disconnected = true;
            pc.Data.SendGameData();
        }

        LoadingBarManager loadingBarManager = FastDestroyableSingleton<LoadingBarManager>.Instance;
        yield return loadingBarManager.WaitAndSmoothlyUpdate(90f, 100f, 2f, GetString("LoadingBarText.1"));
        loadingBarManager.ToggleLoadingBar(false);

        Main.AllPlayerControls.Do(SetRoleSelf);

        RpcSetRoleReplacer.EndReplace();


        yield return new WaitForSeconds(1.2f);

        foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
        {
            if (pc == null || pc.Data == null) continue;

            bool disconnected = Main.PlayerStates.TryGetValue(pc.PlayerId, out var state) && state.IsDead && state.deathReason == PlayerState.DeathReason.Disconnected;
            pc.Data.Disconnected = disconnected;
            if (!disconnected) pc.Data.SendGameData();
        }

        LateTask.New(() =>
        {
            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                pc.SetKillCooldown(10f);
                pc.RpcResetAbilityCooldown();
            }
        }, 7f, log: false);
    }

    private static bool IsBasisChangingPlayer(byte id, CustomRoles role)
    {
        return BasisChangingAddons.TryGetValue(role, out List<byte> list) && list.Contains(id);
    }

    private static void AssignDesyncRole(CustomRoles role, PlayerControl player, Dictionary<byte, CustomRpcSender> senders, Dictionary<(byte, byte), (RoleTypes, CustomRoles)> rolesMap, RoleTypes baseRole, RoleTypes hostBaseRole = RoleTypes.Crewmate)
    {
        try
        {
            if (player == null) return;

            byte hostId = PlayerControl.LocalPlayer.PlayerId;
            bool isHost = player.PlayerId == hostId;

            Main.PlayerStates[player.PlayerId].SetMainRole(role);

            RoleTypes selfRole = isHost ? baseRole == RoleTypes.Shapeshifter ? RoleTypes.Shapeshifter : hostBaseRole : baseRole;
            RoleTypes othersRole = isHost ? RoleTypes.Crewmate : RoleTypes.Scientist;

            // Set Desync role for self and for others
            foreach (PlayerControl target in Main.AllPlayerControls)
            {
                try
                {
                    RoleTypes targetRoleType = othersRole;
                    CustomRoles targetCustomRole = RoleResult.GetValueOrDefault(target.PlayerId, CustomRoles.CrewmateEHR);

                    if (targetCustomRole.GetVNRole() is CustomRoles.Noisemaker) targetRoleType = RoleTypes.Noisemaker;

                    rolesMap[(player.PlayerId, target.PlayerId)] = player.PlayerId != target.PlayerId ? (targetRoleType, targetCustomRole) : (selfRole, role);
                }
                catch (Exception e) { Utils.ThrowException(e); }
            }

            // Set Desync role for others
            foreach (PlayerControl seer in Main.AllPlayerControls)
            {
                try
                {
                    if (player.PlayerId != seer.PlayerId)
                        rolesMap[(seer.PlayerId, player.PlayerId)] = (othersRole, role);
                }
                catch (Exception e) { Utils.ThrowException(e); }
            }


            RpcSetRoleReplacer.OverriddenSenderList.Add(senders[player.PlayerId]);

            // Set role for host, but not self
            // canOverride should be false for the host during assign
            if (!isHost) player.SetRole(othersRole, false);

            Logger.Info($"Registered Role: {player.Data?.PlayerName} => {role} : RoleType for self => {selfRole}, for others => {othersRole}", "AssignDesyncRoles");
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    private static void MakeDesyncSender(Dictionary<byte, CustomRpcSender> senders, Dictionary<(byte, byte), (RoleTypes, CustomRoles)> rolesMap)
    {
        try
        {
            foreach (PlayerControl seer in Main.AllPlayerControls)
            {
                foreach (PlayerControl target in Main.AllPlayerControls)
                {
                    try
                    {
                        if (seer.PlayerId == target.PlayerId || target.IsLocalPlayer()) continue;

                        if (rolesMap.TryGetValue((seer.PlayerId, target.PlayerId), out (RoleTypes, CustomRoles) roleMap))
                        {
                            int targetClientId = target.OwnerId;
                            if (targetClientId == -1) continue;

                            RoleTypes roleType = roleMap.Item1;
                            CustomRpcSender sender = senders[seer.PlayerId];
                            sender.RpcSetRole(seer, roleType, targetClientId);
                        }
                    }
                    catch (Exception e) { Utils.ThrowException(e); }
                }
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    private static void SetRoleSelf(PlayerControl target)
    {
        try
        {
            if (target == null) return;

            int targetClientId = target.OwnerId;
            if (targetClientId == -1) return;

            RoleTypes roleType = RpcSetRoleReplacer.RoleMap.TryGetValue((target.PlayerId, target.PlayerId), out (RoleTypes RoleType, CustomRoles CustomRole) roleMap)
                ? roleMap.RoleType
                : RpcSetRoleReplacer.StoragedData[target.PlayerId];

            bool forceDisplayCrewmate = Options.CurrentGameMode == CustomGameMode.Standard && target.Is(Team.Crewmate) && roleType is not (RoleTypes.Crewmate or RoleTypes.Scientist or RoleTypes.Engineer or RoleTypes.Noisemaker or RoleTypes.Tracker or RoleTypes.CrewmateGhost or RoleTypes.GuardianAngel);
            if (forceDisplayCrewmate) RpcSetRoleReplacer.OverriddenTeamRevealScreen[target.PlayerId] = roleType;

            target.RpcSetRoleDesync(forceDisplayCrewmate ? RoleTypes.Crewmate : roleType, targetClientId);
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    private static void AssignLoversRolesFromList()
    {
        try
        {
            if (CustomRoles.Lovers.IsEnable() && !RoleResult.ContainsValue(CustomRoles.Romantic))
            {
                Main.LoversPlayers.Clear();
                Main.IsLoversDead = false;
                AssignLoversRoles();
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    private static void AssignLoversRoles(int rawCount = -1)
    {
        try
        {
            if (Lovers.LegacyLovers.GetBool())
            {
                Main.LoversPlayers = Main.AllPlayerControls.Where(x => x.GetCustomRole() is CustomRoles.LovingCrewmate or CustomRoles.LovingImpostor).Take(2).ToList();
                return;
            }

            List<PlayerControl> allPlayers = Main.AllPlayerControls.Where(pc => (!Main.NeverSpawnTogetherCombos.TryGetValue(OptionItem.CurrentPreset, out Dictionary<CustomRoles, List<CustomRoles>> bannedCombos) || bannedCombos.All(x => !pc.Is(x.Key) || !x.Value.Contains(CustomRoles.Lovers))) && !pc.Is(CustomRoles.GM) && (!pc.HasSubRole() || pc.GetCustomSubRoles().Count < Options.NoLimitAddonsNumMax.GetInt()) && !pc.Is(CustomRoles.Dictator) && !pc.Is(CustomRoles.God) && !pc.Is(CustomRoles.FFF) && !pc.Is(CustomRoles.Bomber) && !pc.Is(CustomRoles.Nuker) && !pc.Is(CustomRoles.Curser) && !pc.Is(CustomRoles.Provocateur) && !pc.Is(CustomRoles.Altruist) && (!pc.IsCrewmate() || Lovers.CrewCanBeInLove.GetBool()) && (!pc.GetCustomRole().IsNeutral() || Lovers.NeutralCanBeInLove.GetBool()) && (!pc.Is(CustomRoleTypes.Coven) || Lovers.CovenCanBeInLove.GetBool()) && (!pc.IsImpostor() || Lovers.ImpCanBeInLove.GetBool())).ToList();
            const CustomRoles role = CustomRoles.Lovers;
            int count = Math.Clamp(rawCount, 0, allPlayers.Count);
            if (rawCount == -1) count = Math.Clamp(role.GetCount(), 0, allPlayers.Count);

            if (count <= 0) return;

            for (var i = 0; i < count; i++)
            {
                PlayerControl player = allPlayers.RandomElement();
                Main.LoversPlayers.Add(player);
                allPlayers.Remove(player);
                Main.PlayerStates[player.PlayerId].SetSubRole(role);
                Logger.Info($"Add-on assigned: {player.Data?.PlayerName} = {player.GetCustomRole()} + {role}", "Assign Lovers");
            }

            RPC.SyncLoversPlayers();
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    // https://github.com/0xDrMoe/TownofHost-Enhanced/blob/41566d58e4217c38542df5b91f507045a6394908/Patches/onGameStartedPatch.cs#L667
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSetRole))]
    [HarmonyPriority(Priority.High)]
    public static class RpcSetRoleReplacer
    {
        public static bool BlockSetRole;
        private static Dictionary<byte, CustomRpcSender> Senders = [];
        public static Dictionary<byte, RoleTypes> StoragedData = [];
        public static Dictionary<(byte SeerID, byte TargetID), (RoleTypes RoleType, CustomRoles CustomRole)> RoleMap = [];
        public static List<CustomRpcSender> OverriddenSenderList = [];
        public static Dictionary<byte, RoleTypes> OverriddenTeamRevealScreen = [];

        public static void Initialize()
        {
            BlockSetRole = true;
            Senders = [];
            RoleMap = [];
            StoragedData = [];
            OverriddenSenderList = [];
            OverriddenTeamRevealScreen = [];
        }

        // ReSharper disable once MemberHidesStaticFromOuterClass
        // ReSharper disable once UnusedMember.Global
        public static bool Prefix()
        {
            return !BlockSetRole;
        }

        public static void StartReplace()
        {
            try
            {
                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    try
                    {
                        Senders[pc.PlayerId] = CustomRpcSender.Create($"{pc.name}'s SetRole Sender", SendOption.Reliable)
                            .StartMessage(pc.OwnerId);
                    }
                    catch (Exception e) { Utils.ThrowException(e); }
                }
            }
            catch (Exception e) { Utils.ThrowException(e); }
        }

        public static void AssignDesyncRoles()
        {
            // Assign desync roles
            try
            {
                foreach ((byte playerId, CustomRoles role) in RoleResult)
                {
                    try
                    {
                        if (role.IsDesyncRole() || IsBasisChangingPlayer(playerId, CustomRoles.Bloodlust))
                            AssignDesyncRole(role, Utils.GetPlayerById(playerId), Senders, RoleMap, ForceImp(playerId) ? RoleTypes.Impostor : role.GetDYRole());
                    }
                    catch (Exception e) { Utils.ThrowException(e); }
                }
            }
            catch (Exception e) { Utils.ThrowException(e); }

            return;

            bool ForceImp(byte id) => IsBasisChangingPlayer(id, CustomRoles.Bloodlust) || (Options.CurrentGameMode == CustomGameMode.Speedrun && Speedrun.CanKill.Contains(id));
        }

        public static void SendRpcForDesync()
        {
            try { MakeDesyncSender(Senders, RoleMap); }
            catch (Exception e) { Utils.ThrowException(e); }
        }

        public static void AssignNormalRoles()
        {
            try
            {
                List<byte> doneIds = [];

                try
                {
                    foreach ((byte playerId, CustomRoles role) in RoleResult)
                    {
                        try
                        {
                            PlayerControl player = Utils.GetPlayerById(playerId);
                            if (player == null || role.IsDesyncRole()) continue;

                            if (Options.CurrentGameMode == CustomGameMode.Speedrun && Speedrun.CanKill.Contains(playerId)) continue;

                            RoleTypes roleType = role.GetRoleTypes();

                            if (BasisChangingAddons.FindFirst(x => x.Value.Contains(playerId), out KeyValuePair<CustomRoles, List<byte>> kvp))
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

                            StoragedData[playerId] = roleType;
                            doneIds.Add(playerId);

                            foreach (PlayerControl target in Main.AllPlayerControls)
                            {
                                try
                                {
                                    if (RoleResult.TryGetValue(target.PlayerId, out CustomRoles targetRole) && targetRole.IsDesyncRole() && !target.IsHost()) continue;
                                    RoleMap[(target.PlayerId, playerId)] = (roleType, role);
                                }
                                catch (Exception e) { Utils.ThrowException(e); }
                            }

                            if (playerId != PlayerControl.LocalPlayer.PlayerId)
                            {
                                // canOverride should be false for the host during assign
                                player.SetRole(roleType, false);
                            }

                            Logger.Info($"Set original role type => {player.GetRealName()}: {role} => {role.GetRoleTypes()}", "AssignNormalRoles");
                        }
                        catch (Exception e) { Utils.ThrowException(e); }
                    }
                }
                catch (Exception e) { Utils.ThrowException(e); }

                try
                {
                    foreach (PlayerControl pc in Main.AllPlayerControls)
                    {
                        try
                        {
                            if (!doneIds.Contains(pc.PlayerId))
                            {
                                StoragedData[pc.PlayerId] = RoleTypes.Crewmate;

                                foreach (PlayerControl target in Main.AllPlayerControls)
                                {
                                    try
                                    {
                                        if (RoleResult.TryGetValue(target.PlayerId, out CustomRoles targetRole) && targetRole.IsDesyncRole() && !target.IsHost()) continue;
                                        RoleMap[(target.PlayerId, pc.PlayerId)] = (RoleTypes.Crewmate, CustomRoles.CrewmateEHR);
                                    }
                                    catch (Exception e) { Utils.ThrowException(e); }
                                }
                            }
                        }
                        catch (Exception e) { Utils.ThrowException(e); }
                    }
                }
                catch (Exception e) { Utils.ThrowException(e); }
            }
            catch (Exception e) { Utils.ThrowException(e); }
        }

        public static void SendRpcForNormal()
        {
            foreach ((byte targetId, CustomRpcSender sender) in Senders)
            {
                try
                {
                    PlayerControl target = Utils.GetPlayerById(targetId);
                    if (OverriddenSenderList.Contains(sender)) continue;

                    if (sender.CurrentState != CustomRpcSender.State.InRootMessage) throw new InvalidOperationException("A CustomRpcSender had Invalid State.");

                    foreach ((byte seerId, RoleTypes roleType) in StoragedData)
                    {
                        try
                        {
                            if (targetId == seerId || targetId == PlayerControl.LocalPlayer.PlayerId) continue;

                            PlayerControl seer = Utils.GetPlayerById(seerId);
                            if (seer == null || target == null) continue;

                            int targetClientId = target.OwnerId;
                            if (targetClientId == -1) continue;

                            sender.RpcSetRole(seer, roleType, targetClientId, false);
                        }
                        catch (Exception e) { Utils.ThrowException(e); }
                    }

                    sender.EndMessage();
                }
                catch (Exception e) { Utils.ThrowException(e); }
            }
        }

        public static void Release()
        {
            try
            {
                BlockSetRole = false;

                foreach (CustomRpcSender sender in Senders.Values)
                {
                    try { sender.SendMessage(); }
                    catch (Exception e) { Utils.ThrowException(e); }
                }
            }
            catch (Exception e) { Utils.ThrowException(e); }
        }

        public static void EndReplace()
        {
            try
            {
                Senders = null;
                OverriddenSenderList = null;
                StoragedData = null;
            }
            catch (Exception e) { Utils.ThrowException(e); }
        }

        public static void SetActualSelfRolesAfterOverride()
        {
            foreach ((byte id, RoleTypes roleTypes) in OverriddenTeamRevealScreen)
            {
                PlayerControl pc = id.GetPlayer();
                if (pc == null || !pc.IsAlive()) continue;

                int targetClientId = pc.OwnerId;
                if (targetClientId == -1) continue;

                RoleTypes actualRoleType = BasisChangingAddons.FindFirst(x => x.Value.Contains(id), out KeyValuePair<CustomRoles, List<byte>> kvp)
                    ? kvp.Key switch
                    {
                        CustomRoles.Bloodlust when roleTypes is RoleTypes.Crewmate or RoleTypes.Engineer or RoleTypes.Scientist or RoleTypes.Tracker or RoleTypes.Noisemaker => RoleTypes.Impostor,
                        CustomRoles.Nimble when roleTypes is RoleTypes.Crewmate or RoleTypes.Scientist or RoleTypes.Noisemaker or RoleTypes.Tracker => RoleTypes.Engineer,
                        CustomRoles.Physicist when roleTypes == RoleTypes.Crewmate => RoleTypes.Scientist,
                        CustomRoles.Finder when roleTypes is RoleTypes.Crewmate or RoleTypes.Scientist or RoleTypes.Noisemaker => RoleTypes.Tracker,
                        CustomRoles.Noisy when roleTypes == RoleTypes.Crewmate => RoleTypes.Noisemaker,
                        _ => roleTypes
                    }
                    : roleTypes;

                pc.RpcSetRoleDesync(actualRoleType, targetClientId);

                LateTask.New(() =>
                {
                    pc.RpcResetAbilityCooldown();
                    pc.SetKillCooldown();
                }, 0.2f, log: false);
            }

            OverriddenTeamRevealScreen = null;
        }
    }
}