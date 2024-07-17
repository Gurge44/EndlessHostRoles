using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.Data;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Neutral;
using HarmonyLib;
using InnerNet;
using TMPro;
using UnityEngine;
using static EHR.Translator;


namespace EHR;

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
public static class GameStartManagerUpdatePatch
{
    public static void Prefix(GameStartManager __instance)
    {
        try
        {
            __instance.MinPlayers = 1;
        }
        catch (Exception ex)
        {
            Logger.Error(ex.ToString(), "Surely this can't be causing an issue, right?");
        }
    }
}

public class GameStartManagerPatch
{
    public static float Timer { get; set; } = 600f;

    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
    public class GameStartManagerStartPatch
    {
        public static TextMeshPro HideName;
        public static TextMeshPro GameCountdown;

        public static void Postfix(GameStartManager __instance)
        {
            try
            {
                if (__instance == null) return;

                var temp = __instance.PlayerCounter;
                GameCountdown = Object.Instantiate(temp, __instance.StartButton.transform);
                GameCountdown.text = string.Empty;

                if (AmongUsClient.Instance.AmHost)
                {
                    __instance.GameStartTextParent.GetComponent<SpriteRenderer>().sprite = null;
                    __instance.StartButton.ChangeButtonText(DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.StartLabel));
                    __instance.GameStartText.transform.localPosition = new(__instance.GameStartText.transform.localPosition.x, 2f, __instance.GameStartText.transform.localPosition.z);
                }

                if (AmongUsClient.Instance == null || AmongUsClient.Instance.IsGameStarted || GameStates.IsInGame || __instance.startState == GameStartManager.StartingStates.Starting) return;

                __instance.GameRoomNameCode.text = GameCode.IntToGameName(AmongUsClient.Instance.GameId);
                // Reset lobby countdown timer
                Timer = 600f;

                HideName = Object.Instantiate(__instance.GameRoomNameCode, __instance.GameRoomNameCode.transform);
                HideName.text = ColorUtility.TryParseHtmlString(Main.HideColor.Value, out _)
                    ? $"<color={Main.HideColor.Value}>{Main.HideName.Value}</color>"
                    : $"<color={Main.ModColor}>{Main.HideName.Value}</color>";

                if (!AmongUsClient.Instance.AmHost) return;

                if (ModUpdater.isBroken || (ModUpdater.hasUpdate && ModUpdater.forceUpdate) || !Main.AllowPublicRoom)
                {
                    // __instance.MakePublicButton.color = Palette.DisabledClear;
                    // __instance.privatePublicText.color = Palette.DisabledClear;
                }

                if (Main.NormalOptions.KillCooldown == 0f)
                    Main.NormalOptions.KillCooldown = Main.LastKillCooldown.Value;

                AURoleOptions.SetOpt(Main.NormalOptions.Cast<IGameOptions>());
                if (AURoleOptions.ShapeshifterCooldown == 0f)
                    AURoleOptions.ShapeshifterCooldown = Main.LastShapeshifterCooldown.Value;

                AURoleOptions.GuardianAngelCooldown = Spiritcaller.SpiritAbilityCooldown.GetFloat();
                AURoleOptions.ProtectionDurationSeconds = 0f;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString(), "GameStartManagerStartPatch.Postfix (1)");
            }
        }
    }

    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
    public class GameStartManagerUpdatePatch
    {
        public static float ExitTimer = -1f;
        private static float MinWait, MaxWait;
        private static int MinPlayer;

        public static bool Prefix(GameStartManager __instance)
        {
            try
            {
                if (AmongUsClient.Instance == null) return false;

                if (AmongUsClient.Instance.AmHost)
                    VanillaUpdate(__instance);

                if (AmongUsClient.Instance.IsGameStarted || GameStates.IsInGame || __instance == null || __instance.startState == GameStartManager.StartingStates.Starting) return false;

                MinWait = Options.MinWaitAutoStart.GetFloat();
                MaxWait = Options.MaxWaitAutoStart.GetFloat();
                MinPlayer = Options.PlayerAutoStart.GetInt();
                MinWait = 600f - MinWait * 60f;
                MaxWait *= 60f;
                // Lobby code
                if (DataManager.Settings != null && DataManager.Settings.Gameplay != null)
                {
                    if (DataManager.Settings.Gameplay.StreamerMode)
                    {
                        if (__instance != null && __instance.GameRoomNameCode != null)
                            __instance.GameRoomNameCode.color = new(255, 255, 255, 0);
                        if (GameStartManagerStartPatch.HideName != null)
                            GameStartManagerStartPatch.HideName.enabled = true;
                    }
                    else
                    {
                        if (__instance != null && __instance.GameRoomNameCode != null)
                            __instance.GameRoomNameCode.color = new(255, 255, 255, 255);
                        if (GameStartManagerStartPatch.HideName != null)
                            GameStartManagerStartPatch.HideName.enabled = false;
                    }
                }

                if (AmongUsClient.Instance == null || GameData.Instance == null || !AmongUsClient.Instance.AmHost || !GameData.Instance) return true;

                if (Main.AutoStart != null && Main.AutoStart.Value)
                {
                    Main.UpdateTime++;
                    if (Main.UpdateTime >= 50)
                    {
                        Main.UpdateTime = 0;
                        if (((GameData.Instance?.PlayerCount >= MinPlayer && Timer <= MinWait) || Timer <= MaxWait) && !GameStates.IsCountDown)
                        {
                            var invalidColor = Main.AllPlayerControls.Where(p => p.Data.DefaultOutfit.ColorId < 0 || Palette.PlayerColors.Length <= p.Data.DefaultOutfit.ColorId).ToArray();

                            if (invalidColor.Length > 0)
                            {
                                Main.AllPlayerControls
                                    .Where(p => p.Data.DefaultOutfit.ColorId < 0 || Palette.PlayerColors.Length <= p.Data.DefaultOutfit.ColorId)
                                    .Do(p => AmongUsClient.Instance.KickPlayer(p.GetClientId(), false));

                                Logger.SendInGame(GetString("Error.InvalidColorPreventStart"));
                                var msg = GetString("Error.InvalidColor");
                                msg += "\n" + string.Join(",", invalidColor.Select(p => $"{p.GetRealName()}"));
                                Utils.SendMessage(msg);
                            }

                            if (Options.RandomMapsMode.GetBool())
                            {
                                Main.NormalOptions.MapId = GameStartRandomMap.SelectRandomMap();
                            }

                            GameStartManager.Instance.startState = GameStartManager.StartingStates.Countdown;
                            GameStartManager.Instance.countDownTimer = Options.AutoStartTimer.GetInt();
                            __instance?.StartButton.gameObject.SetActive(false);
                        }
                    }
                }
            }
            catch (NullReferenceException)
            {
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString(), "GameStartManagerUpdatePatch.Prefix (2)");
            }

            return false;
        }

        private static void VanillaUpdate(GameStartManager instance)
        {
            if (!GameData.Instance || !GameManager.Instance) return;

            instance.UpdateMapImage((MapNames)GameManager.Instance.LogicOptions.MapId);
            instance.CheckSettingsDiffs();
            instance.StartButton.gameObject.SetActive(true);
            instance.RulesPresetText.text = DestroyableSingleton<TranslationController>.Instance.GetString(GameOptionsManager.Instance.CurrentGameOptions.GetRulesPresetTitle());
            if (GameCode.IntToGameName(AmongUsClient.Instance.GameId) == null) instance.privatePublicPanelText.text = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.LocalButton);
            else if (AmongUsClient.Instance.IsGamePublic) instance.privatePublicPanelText.text = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.PublicHeader);
            else instance.privatePublicPanelText.text = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.PrivateHeader);
            instance.HostPrivateButton.gameObject.SetActive(!AmongUsClient.Instance.IsGamePublic);
            instance.HostPublicButton.gameObject.SetActive(AmongUsClient.Instance.IsGamePublic);
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.C))
                ClipboardHelper.PutClipboardString(GameCode.IntToGameName(AmongUsClient.Instance.GameId));
            if (GameData.Instance.PlayerCount != instance.LastPlayerCount)
            {
                instance.LastPlayerCount = GameData.Instance.PlayerCount;
                string text = "<color=#FF0000FF>";
                if (instance.LastPlayerCount > instance.MinPlayers) text = "<color=#00FF00FF>";
                if (instance.LastPlayerCount == instance.MinPlayers) text = "<color=#FFFF00FF>";
                instance.PlayerCounter.text = $"{text}{instance.LastPlayerCount}/{(AmongUsClient.Instance.NetworkMode == NetworkModes.LocalGame ? 15 : GameManager.Instance.LogicOptions.MaxPlayers)}";
                instance.StartButton.SetButtonEnableState(instance.LastPlayerCount >= instance.MinPlayers);
                ActionMapGlyphDisplay startButtonGlyph = instance.StartButtonGlyph;
                startButtonGlyph?.SetColor((instance.LastPlayerCount >= instance.MinPlayers) ? Palette.EnabledColor : Palette.DisabledClear);
                if (DestroyableSingleton<DiscordManager>.InstanceExists)
                {
                    if (AmongUsClient.Instance.AmHost && AmongUsClient.Instance.NetworkMode == NetworkModes.OnlineGame)
                        DestroyableSingleton<DiscordManager>.Instance.SetInLobbyHost(instance.LastPlayerCount, GameManager.Instance.LogicOptions.MaxPlayers, AmongUsClient.Instance.GameId);
                    else DestroyableSingleton<DiscordManager>.Instance.SetInLobbyClient(instance.LastPlayerCount, GameManager.Instance.LogicOptions.MaxPlayers, AmongUsClient.Instance.GameId);
                }
            }

            if (AmongUsClient.Instance.AmHost)
            {
                if (instance.startState == GameStartManager.StartingStates.Countdown)
                {
                    instance.StartButton.ChangeButtonText(GetString("Cancel"));
                    int num = Mathf.CeilToInt(instance.countDownTimer);
                    instance.countDownTimer -= Time.deltaTime;
                    int num2 = Mathf.CeilToInt(instance.countDownTimer);
                    if (!instance.GameStartTextParent.activeSelf) SoundManager.Instance.PlaySound(instance.gameStartSound, false);
                    instance.GameStartTextParent.SetActive(true);
                    instance.GameStartText.text = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.GameStarting, num2);
                    if (num != num2) PlayerControl.LocalPlayer.RpcSetStartCounter(num2);
                    if (num2 <= 0) instance.FinallyBegin();
                }
                else
                {
                    instance.StartButton.ChangeButtonText(DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.StartLabel));
                    instance.GameStartTextParent.SetActive(false);
                    instance.GameStartText.text = string.Empty;
                }
            }

            if (instance.LobbyInfoPane.gameObject.activeSelf && DestroyableSingleton<HudManager>.Instance.Chat.IsOpenOrOpening) instance.LobbyInfoPane.DeactivatePane();
            instance.LobbyInfoPane.gameObject.SetActive(!DestroyableSingleton<HudManager>.Instance.Chat.IsOpenOrOpening);
        }

        public static void Postfix(GameStartManager __instance)
        {
            try
            {
                if (AmongUsClient.Instance == null || AmongUsClient.Instance.IsGameStarted || GameStates.IsInGame || __instance == null || __instance.startState == GameStartManager.StartingStates.Starting) return;

                if (AmongUsClient.Instance.AmHost)
                {
                    bool canStartGame = true;
                    foreach (var client in AmongUsClient.Instance.allClients)
                    {
                        if (client.Character == null) continue;
                        var dummyComponent = client.Character.GetComponent<DummyBehaviour>();
                        if (dummyComponent != null && dummyComponent.enabled) continue;
                        if (!MatchVersions(client.Character.PlayerId, true))
                        {
                            canStartGame = false;
                        }
                    }

                    if (!canStartGame)
                    {
                        __instance.StartButton.gameObject.SetActive(false);
                    }
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
                    }
                }

                __instance.RulesPresetText.text = GetString($"Preset_{OptionItem.CurrentPreset + 1}");

                // Lobby timer
                if (!AmongUsClient.Instance.AmHost || !GameData.Instance || AmongUsClient.Instance.NetworkMode == NetworkModes.LocalGame) return;

                Timer = Mathf.Max(0f, Timer -= Time.deltaTime);
                int minutes = (int)Timer / 60;
                int seconds = (int)Timer % 60;
                string suffix = $"{minutes:00}:{seconds:00}";
                if (Timer <= 60) suffix = Utils.ColorString(Color.red, suffix);

                if (Mathf.Approximately(Timer, 60f) && AmongUsClient.Instance.AmHost)
                    PlayerControl.LocalPlayer.ShowPopUp(GetString("Warning.OneMinuteLeft"));

                TextMeshPro tmp = GameStartManagerStartPatch.GameCountdown;

                if (tmp.text == string.Empty)
                {
                    tmp.name = "LobbyTimer";
                    tmp.fontSize = tmp.fontSizeMin = tmp.fontSizeMax = 3f;
                    tmp.autoSizeTextContainer = true;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.outlineColor = Color.black;
                    tmp.outlineWidth = 0.4f;
                    tmp.transform.localPosition += new Vector3(-0.8f, -0.42f, 0f);
                    tmp.transform.localScale = new(0.5f, 0.5f, 1f);
                }

                tmp.text = suffix;
            }
            catch (NullReferenceException)
            {
            }
            catch (Exception e)
            {
                Logger.Error(e.ToString(), "GameStartManagerUpdatePatch.Postfix (3)");
            }
        }

        private static bool MatchVersions(byte playerId, bool acceptVanilla = false)
        {
            if (!Main.PlayerVersion.TryGetValue(playerId, out var version)) return acceptVanilla;
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
public class GameStartRandomMap
{
    public static bool Prefix(GameStartManager __instance)
    {
        var invalidColor = Main.AllPlayerControls.Where(p => p.Data.DefaultOutfit.ColorId < 0 || Palette.PlayerColors.Length <= p.Data.DefaultOutfit.ColorId).ToArray();
        if (invalidColor.Length > 0)
        {
            Logger.SendInGame(GetString("Error.InvalidColorPreventStart"));
            var msg = GetString("Error.InvalidColor");
            msg += "\n" + string.Join(",", invalidColor.Select(p => $"{p.GetRealName()}"));
            Utils.SendMessage(msg);
            return false;
        }

        if (__instance.startState == GameStartManager.StartingStates.Countdown)
        {
            Main.NormalOptions.KillCooldown = Options.DefaultKillCooldown;
        }
        else
        {
            Options.DefaultKillCooldown = Main.NormalOptions.KillCooldown;
            Main.LastKillCooldown.Value = Main.NormalOptions.KillCooldown;
            Main.NormalOptions.KillCooldown = 0f;
        }

        var opt = Main.NormalOptions.Cast<IGameOptions>();
        AURoleOptions.SetOpt(opt);

        if (__instance.startState == GameStartManager.StartingStates.Countdown)
        {
            AURoleOptions.ShapeshifterCooldown = Main.LastShapeshifterCooldown.Value;
        }
        else
        {
            Main.LastShapeshifterCooldown.Value = AURoleOptions.ShapeshifterCooldown;
            AURoleOptions.ShapeshifterCooldown = 0f;
        }

        PlayerControl.LocalPlayer.RpcSyncSettings(GameOptionsManager.Instance.gameOptionsFactory.ToBytes(opt, AprilFoolsMode.IsAprilFoolsModeToggledOn));

        __instance.ReallyBegin(false);
        return false;
    }

    public static bool Prefix( /*GameStartRandomMap __instance*/)
    {
        bool continueStart = true;
        if (Options.RandomMapsMode.GetBool())
        {
            Main.NormalOptions.MapId = SelectRandomMap();
        }

        return continueStart;
    }

    public static byte SelectRandomMap()
    {
        var rand = IRandom.Instance;
        List<byte> randomMaps = [];

        var tempRand = rand.Next(1, 100);

        if (tempRand <= Options.SkeldChance.GetInt()) randomMaps.Add(0);
        if (tempRand <= Options.MiraChance.GetInt()) randomMaps.Add(1);
        if (tempRand <= Options.PolusChance.GetInt()) randomMaps.Add(2);
        if (tempRand <= Options.DleksChance.GetInt()) randomMaps.Add(3);
        if (tempRand <= Options.AirshipChance.GetInt()) randomMaps.Add(4);
        if (tempRand <= Options.FungleChance.GetInt()) randomMaps.Add(5);

        if (randomMaps.Count > 0)
        {
            var mapsId = randomMaps[0];

            Logger.Info($"{mapsId}", "Chance Select MapId");
            return mapsId;
        }
        else
        {
            if (Options.SkeldChance.GetInt() > 0) randomMaps.Add(0);
            if (Options.MiraChance.GetInt() > 0) randomMaps.Add(1);
            if (Options.PolusChance.GetInt() > 0) randomMaps.Add(2);
            if (Options.DleksChance.GetInt() > 0) randomMaps.Add(3);
            if (Options.AirshipChance.GetInt() > 0) randomMaps.Add(4);
            if (Options.FungleChance.GetInt() > 0) randomMaps.Add(5);

            var mapsId = randomMaps.RandomElement();

            Logger.Info($"{mapsId}", "Random Select MapId");
            return mapsId;
        }
    }
}

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.ResetStartState))]
class ResetStartStatePatch
{
    public static void Prefix(GameStartManager __instance)
    {
        SoundManager.Instance.StopSound(__instance.gameStartSound);

        if (__instance.startState == GameStartManager.StartingStates.Countdown)
        {
            Main.NormalOptions.KillCooldown = Options.DefaultKillCooldown;
            PlayerControl.LocalPlayer.RpcSyncSettings(GameOptionsManager.Instance.gameOptionsFactory.ToBytes(GameOptionsManager.Instance.CurrentGameOptions, AprilFoolsMode.IsAprilFoolsModeToggledOn));
        }
    }
}

[HarmonyPatch(typeof(IGameOptionsExtensions), nameof(IGameOptionsExtensions.GetAdjustedNumImpostors))]
class UnrestrictedNumImpostorsPatch
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
    public class GameStartManagerStartPatch
    {
        public static bool Prefix(GameStartManager __instance)
        {
            if (!AmongUsClient.Instance.AmHost) return true;

            if (__instance.startState == GameStartManager.StartingStates.Countdown)
            {
                __instance.ResetStartState();
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