using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.Data;
using AmongUs.GameOptions;
using EHR.Gamemodes;
using EHR.Patches;
using EHR.Roles;
using HarmonyLib;
using Hazel;
using InnerNet;
using TMPro;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

[HarmonyPatch]
public static class GameStartManagerPatch
{
    public static long TimerStartTS;
    private static TextMeshPro WarningText;
    public static float Timer => Math.Max(0, 597f - (Utils.TimeStamp - TimerStartTS));

    [HarmonyPatch(typeof(TimerTextTMP), nameof(TimerTextTMP.UpdateText))]
    public static class TimerTextTMPUpdateTextPatch
    {
        public static bool Prefix(TimerTextTMP __instance)
        {
            int seconds = __instance.GetSecondsRemaining();
            if (seconds < 60) return true;
            __instance.text.text = string.Format(GetString("LobbyTimer"), seconds / 60, seconds % 60);
            return false;
        }
    }

    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
    public static class GameStartManagerStartPatch
    {
        public static TextMeshPro HideName;
        public static TextMeshPro GameCountdown;

        public static void Postfix(GameStartManager __instance)
        {
            try
            {
                if (__instance == null) return;

                GameCountdown = Object.Instantiate(__instance.PlayerCounter, __instance.HostInfoPanel.transform);
                GameCountdown.text = string.Empty;

                if (GameData.Instance && HudManager.InstanceExists && AmongUsClient.Instance.NetworkMode != NetworkModes.LocalGame && GameStates.CurrentServerType == GameStates.ServerType.Vanilla)
                {
                    HudManager hudManager = HudManager.Instance;
                    hudManager.ShowLobbyTimer(597);
                    hudManager.LobbyTimerExtensionUI.timerText.transform.parent.transform.Find("Icon").gameObject.SetActive(false);
                    GameStartManagerUpdatePatch.Warned = false;
                }

                if (AmongUsClient.Instance.AmHost)
                {
                    __instance.GameStartTextParent.GetComponent<SpriteRenderer>().sprite = null;
                    __instance.StartButton.ChangeButtonText(TranslationController.Instance.GetString(StringNames.StartLabel));
                    __instance.GameStartText.transform.localPosition = new(__instance.GameStartText.transform.localPosition.x, 2f, __instance.GameStartText.transform.localPosition.z);
                    __instance.StartButton.activeTextColor = __instance.StartButton.inactiveTextColor = Color.white;

                    __instance.EditButton.activeTextColor = __instance.EditButton.inactiveTextColor = Color.black;
                    __instance.EditButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new(0f, 0.647f, 1f, 1f);
                    __instance.EditButton.activeSprites.GetComponent<SpriteRenderer>().color = new(0f, 0.847f, 1f, 1f);
                    __instance.EditButton.inactiveSprites.transform.Find("Shine").GetComponent<SpriteRenderer>().color = new(0f, 1f, 1f, 0.5f);

                    __instance.HostViewButton.activeTextColor = __instance.HostViewButton.inactiveTextColor = Color.black;
                    __instance.HostViewButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new(0f, 0.647f, 1f, 1f);
                    __instance.HostViewButton.activeSprites.GetComponent<SpriteRenderer>().color = new(0f, 0.847f, 1f, 1f);
                    __instance.HostViewButton.inactiveSprites.transform.Find("Shine").GetComponent<SpriteRenderer>().color = new(0f, 1f, 1f, 0.5f);
                }
                else
                {
                    __instance.ClientViewButton.activeTextColor = __instance.ClientViewButton.inactiveTextColor = Color.black;
                    __instance.ClientViewButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new(0f, 0.647f, 1f, 1f);
                    __instance.ClientViewButton.activeSprites.GetComponent<SpriteRenderer>().color = new(0f, 0.847f, 1f, 1f);
                    __instance.ClientViewButton.inactiveSprites.transform.Find("Shine").GetComponent<SpriteRenderer>().color = new(0f, 1f, 1f, 0.5f);
                }

                if (AmongUsClient.Instance == null || AmongUsClient.Instance.IsGameStarted || GameStates.IsInGame || __instance.startState == GameStartManager.StartingStates.Starting) return;

                __instance.GameRoomNameCode.text = GameCode.IntToGameName(AmongUsClient.Instance.GameId);
                // Reset lobby countdown timer
                TimerStartTS = Utils.TimeStamp;

                HideName = Object.Instantiate(__instance.GameRoomNameCode, __instance.GameRoomNameCode.transform);

                HideName.text = ColorUtility.TryParseHtmlString(Main.HideColor.Value, out _)
                    ? $"<color={Main.HideColor.Value}>{Main.HideName.Value}</color>"
                    : $"<color={Main.ModColor}>{Main.HideName.Value}</color>";

                WarningText = Object.Instantiate(__instance.GameStartText, __instance.transform.parent);
                WarningText.name = "WarningText";
                WarningText.transform.localPosition = new(0f, __instance.transform.localPosition.y + 3f, -1f);
                WarningText.gameObject.SetActive(false);

                if (!AmongUsClient.Instance.AmHost) return;

                if (ModUpdater.IsBroken || (ModUpdater.HasUpdate && ModUpdater.ForceUpdate) || !Main.AllowPublicRoom)
                {
                    // __instance.MakePublicButton.color = Palette.DisabledClear;
                    // __instance.privatePublicText.color = Palette.DisabledClear;
                }

                if (Main.NormalOptions.KillCooldown == 0f) Main.NormalOptions.KillCooldown = Main.LastKillCooldown.Value;

                AURoleOptions.SetOpt(Main.NormalOptions.CastFast<IGameOptions>());
                if (AURoleOptions.ShapeshifterCooldown == 0f) AURoleOptions.ShapeshifterCooldown = Main.LastShapeshifterCooldown.Value;

                AURoleOptions.GuardianAngelCooldown = Spiritcaller.SpiritAbilityCooldown.GetFloat();
                AURoleOptions.ProtectionDurationSeconds = 0f;
            }
            catch (Exception ex) { Logger.Error(ex.ToString(), "GameStartManagerStartPatch.Postfix (1)"); }
        }
    }

    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
    public static class GameStartManagerUpdatePatch
    {
        public static float ExitTimer = -1f;
        private static float MinWait, MaxWait;
        private static int MinPlayer;
        private static SpriteRenderer LobbyTimerBg;
        public static bool Warned;

        public static bool Prefix(GameStartManager __instance)
        {
            try
            {
                try { __instance.MinPlayers = 1; }
                catch (Exception ex) { Logger.Error(ex.ToString(), "Surely this can't be causing an issue, right?"); }
                
                if (AmongUsClient.Instance == null) return false;

                if (AmongUsClient.Instance.AmHost) VanillaUpdate(__instance);

                if (AmongUsClient.Instance.IsGameStarted || GameStates.IsInGame || __instance == null || __instance.startState == GameStartManager.StartingStates.Starting) return false;

                // Lobby code
                if (DataManager.Settings != null && DataManager.Settings.Gameplay != null)
                {
                    if (DataManager.Settings.Gameplay.StreamerMode)
                    {
                        if (__instance.GameRoomNameCode != null) __instance.GameRoomNameCode.color = new(255, 255, 255, 0);
                        if (GameStartManagerStartPatch.HideName != null) GameStartManagerStartPatch.HideName.enabled = true;
                    }
                    else
                    {
                        if (__instance.GameRoomNameCode != null) __instance.GameRoomNameCode.color = new(255, 255, 255, 255);
                        if (GameStartManagerStartPatch.HideName != null) GameStartManagerStartPatch.HideName.enabled = false;
                    }
                }

                if (AmongUsClient.Instance == null || GameData.Instance == null || !AmongUsClient.Instance.AmHost || !GameData.Instance) return true;

                CheckAutoStart(__instance);
            }
            catch (NullReferenceException) { }
            catch (Exception ex) { Logger.Error(ex.ToString(), "GameStartManagerUpdatePatch.Prefix (2)"); }

            return false;
        }

        private static void CheckAutoStart(GameStartManager __instance)
        {
            MinWait = Options.MinWaitAutoStart.GetFloat();
            MaxWait = Options.MaxWaitAutoStart.GetFloat();
            MinPlayer = Options.PlayerAutoStart.GetInt();
            MinWait = 600f - (MinWait * 60f);
            MaxWait *= 60f;

            bool votedToStart = (int)Math.Round(ChatCommands.VotedToStart.Count / (float)PlayerControl.AllPlayerControls.Count * 100f) > 50;

            if ((Main.AutoStart == null || !Main.AutoStart.Value) && !votedToStart) return;

            float timer = Timer;
            
            if (timer > 60 && GameSettingMenu.Instance != null) return;

            Main.UpdateTime++;
            if (Main.UpdateTime < 50) return;
            Main.UpdateTime = 0;

            if (GameStates.IsCountDown) return;
            if ((GameData.Instance?.PlayerCount < MinPlayer || timer > MinWait) && timer > MaxWait && !votedToStart) return;

            PlayerControl[] invalidColor = Main.EnumeratePlayerControls().Where(p => p.Data.DefaultOutfit.ColorId < 0 || Palette.PlayerColors.Length <= p.Data.DefaultOutfit.ColorId).ToArray();

            if (invalidColor.Length > 0)
            {
                Main.UpdateTime = -100;
                
                Main.EnumeratePlayerControls()
                    .Where(p => p.Data.DefaultOutfit.ColorId < 0 || Palette.PlayerColors.Length <= p.Data.DefaultOutfit.ColorId)
                    .Do(p => AmongUsClient.Instance.KickPlayer(p.OwnerId, false));

                Logger.SendInGame(GetString("Error.InvalidColorPreventStart"), Color.yellow);
                string msg = GetString("Error.InvalidColor");
                msg += "\n" + string.Join(",", invalidColor.Select(p => $"{p.GetRealName()}"));
                Utils.SendMessage(msg, importance: MessageImportance.Low);
            }

            if (Options.RandomMapsMode.GetBool())
            {
                Main.NormalOptions.MapId = GameStartRandomMap.SelectRandomMap();
                CreateOptionsPickerPatch.SetDleks = Main.CurrentMap == MapNames.Dleks;
            }
            else if (CreateOptionsPickerPatch.SetDleks) Main.NormalOptions.MapId = 3;
            else if (CreateOptionsPickerPatch.SetSubmerged) Main.NormalOptions.MapId = 6;

            if (Options.OverrideSpeedForEachMap.GetBool() && Options.MapSpeeds.TryGetValue(Main.CurrentMap, out var option))
                Main.NormalOptions.PlayerSpeedMod = option.GetFloat();

            if (Main.CurrentMap == MapNames.Dleks || Main.NormalOptions.MapId == 6)
            {
                var opt = Main.NormalOptions.CastFast<IGameOptions>();

                Options.DefaultKillCooldown = Main.NormalOptions.KillCooldown;
                Main.LastKillCooldown.Value = Main.NormalOptions.KillCooldown;
                Main.NormalOptions.KillCooldown = 0f;
                AURoleOptions.SetOpt(opt);
                Main.LastShapeshifterCooldown.Value = AURoleOptions.ShapeshifterCooldown;
                AURoleOptions.ShapeshifterCooldown = 0f;
                AURoleOptions.ImpostorsCanSeeProtect = false;

                GameManager.Instance.LogicOptions.SetDirty();
                OptionItem.SyncAllOptions();
            }

            GameStartManager.Instance.startState = GameStartManager.StartingStates.Countdown;
            GameStartManager.Instance.countDownTimer = Options.AutoStartTimer.GetInt();
            __instance?.StartButton.gameObject.SetActive(false);
        }

        private static void VanillaUpdate(GameStartManager instance)
        {
            if (!GameData.Instance || !GameManager.Instance || !AmongUsClient.Instance.AmHost) return;

            try { instance.UpdateMapImage((MapNames)GameManager.Instance.LogicOptions.MapId); }
            catch (Exception e)
            {
                if (!(GameManager.Instance.LogicOptions.MapId == 6 && SubmergedCompatibility.Loaded))
                {
                    if (GameManager.Instance.LogicOptions.MapId >= Enum.GetValues<MapNames>().Length)
                        ErrorText.Instance.AddError(ErrorCode.UnsupportedMap);

                    Utils.ThrowException(e);
                }
            }

            instance.CheckSettingsDiffs();
            instance.StartButton.gameObject.SetActive(true);
            instance.RulesPresetText.text = TranslationController.Instance.GetString(GameOptionsManager.Instance.CurrentGameOptions.GetRulesPresetTitle());

            if (GameCode.IntToGameName(AmongUsClient.Instance.GameId) == null) instance.privatePublicPanelText.text = TranslationController.Instance.GetString(StringNames.LocalButton);
            else if (AmongUsClient.Instance.IsGamePublic) instance.privatePublicPanelText.text = TranslationController.Instance.GetString(StringNames.PublicHeader);
            else instance.privatePublicPanelText.text = TranslationController.Instance.GetString(StringNames.PrivateHeader);

            instance.HostPrivateButton.gameObject.SetActive(!AmongUsClient.Instance.IsGamePublic);
            instance.HostPublicButton.gameObject.SetActive(AmongUsClient.Instance.IsGamePublic);

            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.C))
                ClipboardHelper.PutClipboardString(GameCode.IntToGameName(AmongUsClient.Instance.GameId));

            if (GameData.Instance.PlayerCount != instance.LastPlayerCount)
            {
                instance.LastPlayerCount = GameData.Instance.PlayerCount;
                var text = "<color=#FF0000FF>";
                if (instance.LastPlayerCount > instance.MinPlayers) text = "<color=#00FF00FF>";
                if (instance.LastPlayerCount == instance.MinPlayers) text = "<color=#FFFF00FF>";

                instance.PlayerCounter.text = $"{text}{instance.LastPlayerCount}/{(AmongUsClient.Instance.NetworkMode == NetworkModes.LocalGame ? 15 : GameManager.Instance.LogicOptions.MaxPlayers)}";
                instance.StartButton.SetButtonEnableState(instance.LastPlayerCount >= instance.MinPlayers);
                ActionMapGlyphDisplay startButtonGlyph = instance.StartButtonGlyph;
                startButtonGlyph?.SetColor(instance.LastPlayerCount >= instance.MinPlayers ? Palette.EnabledColor : Palette.DisabledClear);

                if (DiscordManager.InstanceExists)
                {
                    if (AmongUsClient.Instance.AmHost && AmongUsClient.Instance.NetworkMode == NetworkModes.OnlineGame)
                        DiscordManager.Instance.SetInLobbyHost(instance.LastPlayerCount, GameManager.Instance.LogicOptions.MaxPlayers, AmongUsClient.Instance.GameId);
                    else
                        DiscordManager.Instance.SetInLobbyClient(instance.LastPlayerCount, GameManager.Instance.LogicOptions.MaxPlayers, AmongUsClient.Instance.GameId);
                }
            }

            if (AmongUsClient.Instance.AmHost)
            {
                if (instance.startState == GameStartManager.StartingStates.Countdown)
                {
                    instance.StartButton.ChangeButtonText(GetString("Cancel"));

                    instance.StartButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new(0.8f, 0f, 0f, 1f);
                    instance.StartButton.activeSprites.GetComponent<SpriteRenderer>().color = Color.red;
                    instance.StartButton.inactiveSprites.transform.Find("Shine").GetComponent<SpriteRenderer>().color = new(0.8f, 0.4f, 0.4f, 1f);
                    instance.StartButton.activeTextColor = instance.StartButton.inactiveTextColor = Color.white;
                    int num = Mathf.CeilToInt(instance.countDownTimer);
                    instance.countDownTimer -= Time.deltaTime;
                    int num2 = Mathf.CeilToInt(instance.countDownTimer);
                    if (!instance.GameStartTextParent.activeSelf) SoundManager.Instance.PlaySound(instance.gameStartSound, false);

                    instance.GameStartTextParent.SetActive(true);
                    instance.GameStartText.text = TranslationController.Instance.GetString(StringNames.GameStarting, num2);
                    if (num != num2) PlayerControl.LocalPlayer.RpcSetStartCounter(num2);

                    if (num2 <= 0) instance.FinallyBegin();
                }
                else
                {
                    instance.StartButton.ChangeButtonText(TranslationController.Instance.GetString(StringNames.StartLabel));
                    instance.StartButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new(0.1f, 0.1f, 0.1f, 1f);
                    instance.StartButton.activeSprites.GetComponent<SpriteRenderer>().color = new(0.2f, 0.2f, 0.2f, 1f);
                    instance.StartButton.inactiveSprites.transform.Find("Shine").GetComponent<SpriteRenderer>().color = new(0.3f, 0.3f, 0.3f, 0.5f);
                    instance.StartButton.activeTextColor = instance.StartButton.inactiveTextColor = Color.white;
                    instance.GameStartTextParent.SetActive(false);
                    instance.GameStartText.text = string.Empty;
                }
            }
            
            if (!HudManager.InstanceExists) return;

            if (instance.LobbyInfoPane.gameObject.activeSelf && HudManager.Instance.Chat.IsOpenOrOpening)
                instance.LobbyInfoPane.DeactivatePane();

            instance.LobbyInfoPane.gameObject.SetActive(!HudManager.Instance.Chat.IsOpenOrOpening);
        }

        public static void Postfix(GameStartManager __instance)
        {
            try
            {
                if (AmongUsClient.Instance == null || AmongUsClient.Instance.IsGameStarted || GameStates.IsInGame || __instance == null || __instance.startState == GameStartManager.StartingStates.Starting) return;

                var canStartGame = true;
                var mismatchedClientName = string.Empty;

                var warningMessage = "";

                if (AmongUsClient.Instance.AmHost)
                {
                    ClientData[] allClients = AmongUsClient.Instance.allClients.ToArray();

                    lock (allClients)
                    {
                        // ReSharper disable once ForCanBeConvertedToForeach
                        for (var index = 0; index < allClients.Length; index++)
                        {
                            ClientData client = allClients[index];
                            if (client.Character == null) continue;

                            var dummyComponent = client.Character.GetComponent<DummyBehaviour>();
                            if (dummyComponent != null && dummyComponent.enabled) continue;

                            if (!MatchVersions(client.Character.PlayerId, true))
                            {
                                canStartGame = false;
                                mismatchedClientName = client.Character.PlayerId.ColoredPlayerName();
                            }
                        }
                    }

                    if (!canStartGame) __instance.StartButton.gameObject.SetActive(false);
                }
                else
                {
                    if (MatchVersions(0, true))
                        ExitTimer = 0;
                    else
                    {
                        ExitTimer += Time.deltaTime;

                        if (ExitTimer >= 5)
                        {
                            ExitTimer = 0;
                            AmongUsClient.Instance.ExitGame(DisconnectReasons.ExitGame);
                            SceneChanger.ChangeScene("MainMenu");
                        }

                        if (ExitTimer != 0)
                            warningMessage = Utils.ColorString(Color.red, string.Format(GetString("Warning.AutoExitAtMismatchedVersion"), $"<color={Main.ModColor}>{Main.ModName}</color>", ((int)Math.Round(5 - ExitTimer)).ToString()));
                    }
                }

                if (warningMessage == "")
                    WarningText.gameObject.SetActive(false);
                else
                {
                    WarningText.text = warningMessage;
                    WarningText.gameObject.SetActive(true);
                }

                __instance.RulesPresetText.text = GetString($"Preset_{OptionItem.CurrentPreset + 1}");

                
                int estimatedGameLength = Options.CurrentGameMode switch
                {
                    CustomGameMode.SoloPVP => SoloPVP.SoloPVP_GameTime.GetInt(),
                    CustomGameMode.FFA => Math.Clamp((FreeForAll.FFAKcd.GetInt() * (PlayerControl.AllPlayerControls.Count / 2)) + FreeForAll.FFAKcd.GetInt(), FreeForAll.FFAKcd.GetInt(), FreeForAll.FFAGameTime.GetInt()),
                    CustomGameMode.StopAndGo => ((Main.NormalOptions.NumShortTasks * 30) + (Main.NormalOptions.NumLongTasks * 60) + (Math.Min(3, Main.NormalOptions.NumCommonTasks) * 40)) / (int)(Main.NormalOptions.PlayerSpeedMod - ((Main.NormalOptions.PlayerSpeedMod - 1) / 2)),
                    CustomGameMode.HotPotato => HotPotato.GetKillInterval() * (PlayerControl.AllPlayerControls.Count - 1),
                    CustomGameMode.HideAndSeek => Math.Min((Seeker.KillCooldown.GetInt() * (PlayerControl.AllPlayerControls.Count - Main.NormalOptions.NumImpostors) / Main.NormalOptions.NumImpostors) + Seeker.BlindTime.GetInt() + 15, Math.Min(CustomHnS.MaximumGameLength, ((Main.NormalOptions.NumShortTasks * 20) + (Main.NormalOptions.NumLongTasks * 30) + (Math.Min(3, Main.NormalOptions.NumCommonTasks) * 20)) / (int)(Main.NormalOptions.PlayerSpeedMod - ((Main.NormalOptions.PlayerSpeedMod - 1) / 2)))),
                    CustomGameMode.Speedrun => (Speedrun.TimeLimitValue * (Main.NormalOptions.NumShortTasks + Main.NormalOptions.NumLongTasks + Main.NormalOptions.NumCommonTasks)) + (Speedrun.KCD * (PlayerControl.AllPlayerControls.Count / (Speedrun.RestrictedKilling ? 3 : 4))),
                    CustomGameMode.CaptureTheFlag => CaptureTheFlag.GameEndCriteriaType == 2 ? CaptureTheFlag.MaxGameLength : CaptureTheFlag.IsDeathPossible ? 40 : Math.Max(30, 1500 / (int)Math.Pow(CaptureTheFlag.KCD + 0.5f, 2) * CaptureTheFlag.TotalRoundsToPlay),
                    CustomGameMode.NaturalDisasters => 180 + (15 * NaturalDisasters.FrequencyOfDisasters * Math.Max(1, Math.Min(20, PlayerControl.AllPlayerControls.Count) / 4)),
                    CustomGameMode.RoomRush => (int)Math.Round((RoomRush.PointsSystem ? RoomRush.RawPointsToWin * 1.5f : PlayerControl.AllPlayerControls.Count - 1) * ((Main.NormalOptions.MapId is 0 or 3 ? 15 : 20) / Main.NormalOptions.PlayerSpeedMod)),
                    CustomGameMode.KingOfTheZones => Math.Min(KingOfTheZones.MaxGameTime, KingOfTheZones.MaxGameTimeByPoints),
                    CustomGameMode.Deathrace => Deathrace.LapsToWin * (int)Math.Ceiling(25 / Main.NormalOptions.PlayerSpeedMod),
                    _ => 0
                };

                string suffix = estimatedGameLength != 0 ? $"<size=70%>{GetString("EstimatedGameLength")} - {estimatedGameLength / 60:00}:{estimatedGameLength % 60:00}</size>" : " ";

                if (Options.NoGameEnd.GetBool())
                    suffix = Utils.ColorString(Color.yellow, $"{GetString("NoGameEnd").ToUpper()}");

                if (!canStartGame)
                    suffix = Utils.ColorString(Color.red, string.Format(GetString("VersionMismatch"), mismatchedClientName));

                TextMeshPro tmp = GameStartManagerStartPatch.GameCountdown;

                if (tmp.text == string.Empty)
                {
                    tmp.name = "LobbyInfoText";
                    tmp.fontSize = tmp.fontSizeMin = tmp.fontSizeMax = 3f;
                    tmp.autoSizeTextContainer = true;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.color = Color.cyan;
                    tmp.outlineColor = Color.black;
                    tmp.outlineWidth = 0.4f;
                    tmp.transform.localPosition += new Vector3(-0.625f, -0.12f, 0f);
                    tmp.transform.localScale = new(0.6f, 0.6f, 1f);
                }

                tmp.text = suffix;
                tmp.gameObject.SetActive(true);

                // Lobby timer
                if (GameData.Instance && HudManager.InstanceExists && AmongUsClient.Instance.NetworkMode != NetworkModes.LocalGame && GameStates.CurrentServerType == GameStates.ServerType.Vanilla)
                {
                    float timer = Timer;

                    if (LobbyTimerBg == null) LobbyTimerBg = HudManager.Instance.LobbyTimerExtensionUI.timerText.transform.parent.transform.Find("LabelBackground").GetComponent<SpriteRenderer>();
                    LobbyTimerBg.sprite = Utils.LoadSprite("EHR.Resources.Images.LobbyTimerBG.png", 100f);
                    LobbyTimerBg.color = GetTimerColor(timer);

                    if (timer <= 60 && !Warned && AmongUsClient.Instance.AmHost)
                    {
                        Warned = true;
                        LobbyTimerExtensionUI lobbyTimerExtensionUI = HudManager.Instance.LobbyTimerExtensionUI;
                        lobbyTimerExtensionUI.timerText.transform.parent.transform.Find("Icon").gameObject.SetActive(true);
                        SoundManager.Instance.PlaySound(lobbyTimerExtensionUI.lobbyTimerPopUpSound, false);
                        Utils.FlashColor(new(1f, 1f, 0f, 0.4f), 1.4f);
                    }
                }
            }
            catch (NullReferenceException) { }
            catch (Exception e) { Logger.Error(e.ToString(), "GameStartManagerUpdatePatch.Postfix (3)"); }
        }

        private static Color GetTimerColor(float timer)
        {
            switch (timer)
            {
                case >= 180f:
                {
                    return new Color32(0x00, 0x04, 0x44, 0xFF);
                }
                case >= 120f:
                {
                    // 120 → 180: #ffff00 → #00ffa5
                    float lerpT = (timer - 120f) / 60f;
                    return Color.Lerp(new Color32(0xFF, 0xFF, 0x00, 0xFF), new Color32(0x00, 0x04, 0x44, 0xFF), lerpT);
                }
                case >= 60f:
                {
                    // 60 → 120: #ff0000 → #ffff00
                    float lerpT = (timer - 60f) / 60f;
                    return Color.Lerp(new Color32(0xFF, 0x00, 0x00, 0xFF), new Color32(0xFF, 0xFF, 0x00, 0xFF), lerpT);
                }
                default:
                {
                    return new Color32(0xFF, 0x00, 0x00, 0xFF);
                }
            }
        }

        private static bool MatchVersions(byte playerId, bool acceptVanilla = false)
        {
            if (!Main.PlayerVersion.TryGetValue(playerId, out PlayerVersion version)) return acceptVanilla;

            return Main.ForkId == version.forkId
                   && Main.Version.CompareTo(version.version) == 0
                   && version.tag == $"{ThisAssembly.Git.Commit}({ThisAssembly.Git.Branch})";
        }
    }

    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.SetText))]
    public static class HiddenTextPatch
    {
        private static void Postfix(TextBoxTMP __instance)
        {
            if (__instance.name == "GameIdText") __instance.outputText.text = new('*', __instance.text.Length);
        }
    }
}

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.BeginGame))]
public static class GameStartRandomMap
{
    public static bool Prefix(GameStartManager __instance)
    {
        PlayerControl[] invalidColor = Main.EnumeratePlayerControls().Where(p => p.Data.DefaultOutfit.ColorId < 0 || Palette.PlayerColors.Length <= p.Data.DefaultOutfit.ColorId).ToArray();

        if (invalidColor.Length > 0)
        {
            Logger.SendInGame(GetString("Error.InvalidColorPreventStart"), Color.yellow);
            string msg = GetString("Error.InvalidColor");
            msg += "\n" + string.Join(",", invalidColor.Select(p => $"{p.GetRealName()}"));
            Utils.SendMessage(msg, importance: MessageImportance.Low);
            return false;
        }
        
        if (Options.RandomMapsMode.GetBool())
        {
            Main.NormalOptions.MapId = SelectRandomMap();
            CreateOptionsPickerPatch.SetDleks = Main.CurrentMap == MapNames.Dleks;
        }
        else if (CreateOptionsPickerPatch.SetDleks) Main.NormalOptions.MapId = 3;
        else if (CreateOptionsPickerPatch.SetSubmerged) Main.NormalOptions.MapId = 6;

        if (Options.OverrideSpeedForEachMap.GetBool() && Options.MapSpeeds.TryGetValue(Main.CurrentMap, out var option))
            Main.NormalOptions.PlayerSpeedMod = option.GetFloat();

        if (__instance.startState == GameStartManager.StartingStates.Countdown)
            Main.NormalOptions.KillCooldown = Main.LastKillCooldown.Value;
        else
        {
            Options.DefaultKillCooldown = Main.NormalOptions.KillCooldown;
            Main.LastKillCooldown.Value = Main.NormalOptions.KillCooldown;
            Main.NormalOptions.KillCooldown = 0f;
        }

        var opt = Main.NormalOptions.CastFast<IGameOptions>();
        AURoleOptions.SetOpt(opt);

        if (__instance.startState == GameStartManager.StartingStates.Countdown)
            AURoleOptions.ShapeshifterCooldown = Main.LastShapeshifterCooldown.Value;
        else
        {
            Main.LastShapeshifterCooldown.Value = AURoleOptions.ShapeshifterCooldown;
            AURoleOptions.ShapeshifterCooldown = 0f;
        }

        GameManager.Instance.LogicOptions.SetDirty();
        OptionItem.SyncAllOptions();

        __instance.ReallyBegin(false);
        return false;
    }

    public static byte SelectRandomMap()
    {
        Dictionary<byte, int> chance = Enumerable.Range(0, 6).ToDictionary(x => (byte)x, x => x switch
        {
            0 => Options.SkeldChance.GetInt(),
            1 => Options.MiraChance.GetInt(),
            2 => Options.PolusChance.GetInt(),
            3 => Options.DleksChance.GetInt(),
            4 => Options.AirshipChance.GetInt(),
            5 => Options.FungleChance.GetInt(),
            _ => 0
        });
        
        int playerCount = Main.AllPlayerControls.Count;
        if (playerCount < Options.MinPlayersForAirship.GetInt()) chance.Remove(4);
        if (playerCount < Options.MinPlayersForFungle.GetInt()) chance.Remove(5);
        
        byte[] pool = chance.SelectMany(x => Enumerable.Repeat(x.Key, x.Value / 5)).ToArray();
        return pool.Length == 0 ? chance.Keys.RandomElement() : pool.RandomElement();
    }
}

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.ResetStartState))]
internal static class ResetStartStatePatch
{
    public static void Prefix(GameStartManager __instance)
    {
        SoundManager.Instance.StopSound(__instance.gameStartSound);

        if (__instance.startState == GameStartManager.StartingStates.Countdown)
        {
            Main.NormalOptions.KillCooldown = Main.LastKillCooldown.Value;
            GameManager.Instance.LogicOptions.SetDirty();
            OptionItem.SyncAllOptions();
        }
    }
}

[HarmonyPatch(typeof(IGameOptionsExtensions), nameof(IGameOptionsExtensions.GetAdjustedNumImpostors))]
internal static class UnrestrictedNumImpostorsPatch
{
    public static bool Prefix(ref int __result)
    {
        __result = Main.NormalOptions.NumImpostors;
        return false;
    }
}

public static class GameStartManagerBeginPatch
{
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.ReallyBegin))]
    public static class GameStartManagerStartPatch
    {
        public static bool Prefix(GameStartManager __instance)
        {
            if (!AmongUsClient.Instance.AmHost) return true;

            if (__instance.startState == GameStartManager.StartingStates.Countdown)
            {
                __instance.ResetStartState();
                ChatCommands.VotedToStart.Clear();
                return false;
            }

            __instance.startState = GameStartManager.StartingStates.Countdown;
            __instance.GameSizePopup.SetActive(false);
            DataManager.Player.Onboarding.AlwaysShowMinPlayerWarning = false;
            DataManager.Player.Onboarding.ViewedMinPlayerWarning = true;
            DataManager.Player.Save();
            __instance.StartButton.gameObject.SetActive(false);
            __instance.StartButtonClient.gameObject.SetActive(false);
            __instance.GameStartTextParent.SetActive(false);
            __instance.countDownTimer = 5.0001f;
            __instance.startState = GameStartManager.StartingStates.Countdown;
            AmongUsClient.Instance.KickNotJoinedPlayers();
            return false;
        }
    }
}

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.FinallyBegin))]
public static class GameStartManagerFinallyBeginPatch
{
    public static void Prefix(GameStartManager __instance)
    {
        SoundManager.Instance.StopSound(__instance.gameStartSound);
    }

}
