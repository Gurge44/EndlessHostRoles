using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using static EHR.Translator;


namespace EHR;

[HarmonyPatch(typeof(PingTracker), nameof(PingTracker.Update))]
internal static class PingTrackerUpdatePatch
{
    public static PingTracker Instance;
    private static readonly StringBuilder Sb = new();
    private static long LastUpdate;
    public static readonly List<float> LastFPS = [];

    public static bool Prefix(PingTracker __instance)
    {
        PingTracker instance = Instance == null ? __instance : Instance;

        if (AmongUsClient.Instance == null) return false;

        if (AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay)
        {
            instance.gameObject.SetActive(false);
            return false;
        }

        if (instance.name != "EHR_SettingsText")
        {
            instance.aspectPosition.DistanceFromEdge = !AmongUsClient.Instance.IsGameStarted ? instance.lobbyPos : instance.gamePos;
            instance.text.alignment = TextAlignmentOptions.Center;
            instance.text.text = Sb.ToString();
        }

        if (Instance == null) Instance = __instance;

        long now = Utils.TimeStamp;
        if (now == LastUpdate) return false;
        LastUpdate = now;

        Sb.Clear();

        Sb.Append(GameStates.IsLobby ? "\r\n<size=2>" : "<size=1.5>");

        Sb.Append(Main.CredentialsText);

        int ping = AmongUsClient.Instance.Ping;

        string color = ping switch
        {
            < 30 => "#44dfcc",
            < 100 => "#7bc690",
            < 200 => "#f3920e",
            < 400 => "#ff146e",
            _ => "#ff4500"
        };

        Sb.Append(GameStates.InGame ? "    -    " : "\r\n");
        Sb.Append($"<color={color}>{GetString("PingText")}: {ping}</color>");
        AppendSeparator();
        Sb.Append(string.Format(GetString("Server"), Utils.GetRegionName()));

        if (Main.ShowFps.Value)
        {
            float fps = LastFPS.Average();

            Color fpscolor = fps switch
            {
                < 10f => Color.red,
                < 25f => Color.yellow,
                < 50f => Color.green,
                _ => new Color32(0, 165, 255, 255)
            };

            AppendSeparator();
            Sb.Append($"{Utils.ColorString(fpscolor, Utils.ColorString(Color.cyan, GetString("FPSGame")) + (int)fps)}");
        }

        if (GameStates.InGame) Sb.Append("\r\n.");
        return false;

        void AppendSeparator() => Sb.Append(GameStates.InGame ? "    -    " : "  -  ");
    }
}

[HarmonyPatch(typeof(VersionShower), nameof(VersionShower.Start))]
internal static class VersionShowerStartPatch
{
    private static void Postfix(VersionShower __instance)
    {
#pragma warning disable CS0162 // Unreachable code detected
        // ReSharper disable once HeuristicUnreachableCode
        string testBuildIndicator = Main.TestBuild ? " <#ff0000>TEST</color>" : string.Empty;
#pragma warning restore CS0162 // Unreachable code detected

        Main.CredentialsText = $"<color={Main.ModColor}>Endless Host Roles</color> v{Main.PluginDisplayVersion}{testBuildIndicator} <color=#a54aff>by</color> <color=#ffff00>Gurge44</color>";

        if (Main.IsAprilFools) Main.CredentialsText = "<color=#00bfff>Endless Madness</color> v11.45.14 <color=#a54aff>by</color> <color=#ffff00>No one</color>";

        ErrorText.Create(__instance.text);

        if (Main.HasArgumentException && ErrorText.Instance != null)
            ErrorText.Instance.AddError(ErrorCode.Main_DictionaryError);

        VersionChecker.Check();
    }
}

// From TONX/Patches/AccountManagerPatch.cs, by KARPED1EM
[HarmonyPatch(typeof(AccountTab), nameof(AccountTab.Awake))]
public static class UpdateFriendCodeUIPatch
{
    private static GameObject VersionShower;

    public static void Prefix()
    {
        var credentialsText = $"<color={Main.ModColor}>Gurge44</color> \u00a9 2025";
        credentialsText += "\t\t\t";
        credentialsText += $"<color={Main.ModColor}>{Main.ModName}</color> - {Main.PluginVersion}";

        GameObject friendCode = GameObject.Find("FriendCode");

        if (friendCode != null && VersionShower == null)
        {
            VersionShower = Object.Instantiate(friendCode, friendCode.transform.parent);
            VersionShower.name = "EHR Version Shower";
            VersionShower.transform.localPosition = friendCode.transform.localPosition + new Vector3(3.2f, 0f, 0f);
            VersionShower.transform.localScale *= 1.7f;
            var tmp = VersionShower.GetComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Right;
            tmp.fontSize = 30f;
            tmp.SetText(credentialsText);
        }

        GameObject newRequest = GameObject.Find("NewRequest");

        if (newRequest != null)
        {
            newRequest.transform.localPosition -= new Vector3(0f, 0f, 10f);
            newRequest.transform.localScale = new(0.8f, 1f, 1f);
        }

        GameObject friendsButton = GameObject.Find("FriendsButton");

        if (friendsButton != null)
        {
            friendsButton.transform.FindChild("Highlight").GetComponent<SpriteRenderer>().color = new(0f, 0.647f, 1f, 1f);
            friendsButton.transform.FindChild("Inactive").GetComponent<SpriteRenderer>().color = new(0f, 0.847f, 1f, 1f);
        }
    }
}

[HarmonyPatch(typeof(FriendsListUI), nameof(FriendsListUI.Open))]
internal static class FriendsListUIOpenPatch
{
    public static bool Prefix(FriendsListUI __instance)
    {
        try
        {
            if (__instance.gameObject.activeSelf || __instance.currentSceneName == "")
                __instance.Close();
            else
            {
                FriendsListBar[] componentsInChildren = __instance.GetComponentsInChildren<FriendsListBar>(true);

                for (var index = 0; index < componentsInChildren.Length; ++index)
                {
                    if (componentsInChildren[index] != null)
                        Object.Destroy(componentsInChildren[index].gameObject);
                }

                Scene activeScene = SceneManager.GetActiveScene();
                __instance.currentSceneName = activeScene.name;
                __instance.UpdateFriendCodeUI();

                if ((DestroyableSingleton<HudManager>.InstanceExists && FastDestroyableSingleton<HudManager>.Instance != null && FastDestroyableSingleton<HudManager>.Instance.Chat != null && FastDestroyableSingleton<HudManager>.Instance.Chat.IsOpenOrOpening) || ShipStatus.Instance != null)
                    return false;

                __instance.friendBars = new();
                __instance.lobbyBars = new();
                __instance.notifBars = new();
                __instance.platformFriendBars = new();
                __instance.viewingAllFriends = true;
                __instance.gameObject.SetActive(true);
                __instance.guestAccountWarnings.ForEach((Action<FriendsListGuestWarning>)(t => t.gameObject.SetActive(false)));
                __instance.ViewRequestsButton.color = __instance.NoRequestsColor;
                __instance.ViewRequestsText.text = FastDestroyableSingleton<TranslationController>.Instance.GetString(StringNames.NoNewRequests);

                __instance.StartCoroutine(FastDestroyableSingleton<FriendsListManager>.Instance.RefreshFriendsList((Action)(() =>
                {
                    __instance.ClearNotifs();

                    if (FastDestroyableSingleton<EOSManager>.Instance.IsFriendsListAllowed())
                    {
                        __instance.AddFriendObjects.SetActive(true);
                        __instance.RefreshBlockedPlayers();
                        __instance.RefreshFriends();
                        __instance.RefreshNotifications();
                    }
                    else
                    {
                        __instance.AddFriendObjects.SetActive(false);
                        __instance.guestAccountWarnings.ForEach((Action<FriendsListGuestWarning>)(g => g.SetUp()));
                    }

                    __instance.RefreshRecentlyPlayed();
                    __instance.RefreshPlatformFriends();

                    foreach (FriendsListBar friendBar in __instance.friendBars)
                    {
                        foreach (PassiveButton passiveButton in friendBar.ControllerSelectable)
                            ControllerManager.Instance.AddSelectableUiElement(passiveButton);
                    }

                    foreach (FriendsListBar platformFriendBar in __instance.platformFriendBars)
                    {
                        foreach (PassiveButton passiveButton in platformFriendBar.ControllerSelectable)
                            ControllerManager.Instance.AddSelectableUiElement(passiveButton);
                    }

                    foreach (FriendsListBar notifBar in __instance.notifBars)
                    {
                        foreach (PassiveButton passiveButton in notifBar.ControllerSelectable)
                            ControllerManager.Instance.AddSelectableUiElement(passiveButton);
                    }

                    foreach (FriendsListBar lobbyBar in __instance.lobbyBars)
                    {
                        foreach (PassiveButton passiveButton in lobbyBar.ControllerSelectable)
                            ControllerManager.Instance.AddSelectableUiElement(passiveButton);
                    }

                    ControllerManager.Instance.PickTopSelectable();
                })));

                if (__instance.currentSceneName == "OnlineGame")
                {
                    __instance.RefreshLobbyPlayers();
                    __instance.LobbyPlayersTab.SetActive(true);
                    __instance.LobbyPlayersInactiveTab.SetActive(false);
                    __instance.OpenTab(0);
                }
                else
                {
                    __instance.LobbyPlayersInactiveTab.SetActive(true);
                    __instance.LobbyPlayersTab.SetActive(false);
                    __instance.OpenTab(2);
                }

                ControllerManager.Instance.OpenOverlayMenu(__instance.name, __instance.BackButton, __instance.DefaultButtonSelected, __instance.ControllerSelectable);
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }

        return false;
    }
}

[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
[HarmonyPriority(Priority.First)]
internal static class TitleLogoPatch
{
    private static GameObject ModStamp;

    private static GameObject Ambience;

    // public static GameObject LoadingHint;
    private static GameObject LeftPanel;
    public static GameObject RightPanel;
    private static GameObject CloseRightButton;
    private static GameObject Tint;
    private static GameObject BottomButtonBounds;

    public static Vector3 RightPanelOp;

    private static void Postfix(MainMenuManager __instance)
    {
        GameObject.Find("BackgroundTexture")?.SetActive(!MainMenuManagerPatch.ShowedBak);

        if (!(ModStamp = GameObject.Find("ModStamp"))) return;

        ModStamp.transform.localScale = new(0.3f, 0.3f, 0.3f);

        Ambience = GameObject.Find("Ambience");

        if (Ambience != null)
        {
            try { __instance.playButton.transform.gameObject.SetActive(true); }
            catch (Exception ex) { Logger.Warn(ex.ToString(), "MainMenuLoader"); }
        }

        if (!(LeftPanel = GameObject.Find("LeftPanel"))) return;

        LeftPanel.transform.localScale = new(0.7f, 0.7f, 0.7f);
        LeftPanel.ForEachChild((Il2CppSystem.Action<GameObject>)ResetParent);
        LeftPanel.SetActive(false);

        Color shade = new(0f, 0f, 0f, 0f);
        Sprite standardActiveSprite = __instance.newsButton.activeSprites.GetComponent<SpriteRenderer>().sprite;
        Sprite minorActiveSprite = __instance.quitButton.activeSprites.GetComponent<SpriteRenderer>().sprite;

        Dictionary<List<PassiveButton>, (Sprite, Color, Color, Color, Color)> mainButtons = new()
        {
            { [__instance.playButton, __instance.inventoryButton, __instance.shopButton], (standardActiveSprite, new(0f, 0.647f, 1f, 0.8f), shade, Color.white, Color.white) },
            { [__instance.newsButton, __instance.myAccountButton, __instance.settingsButton], (minorActiveSprite, new(0.825f, 0.825f, 0.286f, 0.8f), shade, Color.white, Color.white) },
            { [__instance.creditsButton, __instance.quitButton], (minorActiveSprite, new(0.226f, 1f, 0.792f, 0.8f), shade, Color.white, Color.white) }
        };

        foreach (KeyValuePair<List<PassiveButton>, (Sprite, Color, Color, Color, Color)> kvp in mainButtons)
            kvp.Key.Do(button => FormatButtonColor(button, kvp.Value.Item2, kvp.Value.Item3, kvp.Value.Item4, kvp.Value.Item5));

        try { mainButtons.Keys.Flatten().DoIf(x => x != null, x => x.buttonText.color = Color.white); }
        catch { }

        GameObject.Find("Divider")?.SetActive(false);

        if (!(RightPanel = GameObject.Find("RightPanel"))) return;

        var rpap = RightPanel.GetComponent<AspectPosition>();
        if (rpap) Object.Destroy(rpap);

        RightPanelOp = RightPanel.transform.localPosition;
        RightPanel.transform.localPosition = RightPanelOp + new Vector3(10f, 0f, 0f);
        RightPanel.GetComponent<SpriteRenderer>().color = new(1f, 0.78f, 0.9f, 1f);

        CloseRightButton = new("CloseRightPanelButton");
        CloseRightButton.transform.SetParent(RightPanel.transform);
        CloseRightButton.transform.localPosition = new(-4.78f, 1.3f, 1f);
        CloseRightButton.transform.localScale = new(1f, 1f, 1f);
        CloseRightButton.AddComponent<BoxCollider2D>().size = new(0.6f, 1.5f);
        var closeRightSpriteRenderer = CloseRightButton.AddComponent<SpriteRenderer>();
        closeRightSpriteRenderer.sprite = Utils.LoadSprite("EHR.Resources.Images.RightPanelCloseButton.png", 100f);
        closeRightSpriteRenderer.color = new(1f, 0.78f, 0.9f, 1f);
        var closeRightPassiveButton = CloseRightButton.AddComponent<PassiveButton>();
        closeRightPassiveButton.OnClick = new();
        closeRightPassiveButton.OnClick.AddListener((Action)MainMenuManagerPatch.HideRightPanel);
        closeRightPassiveButton.OnMouseOut = new();
        closeRightPassiveButton.OnMouseOut.AddListener((Action)(() => closeRightSpriteRenderer.color = new(1f, 0.78f, 0.9f, 1f)));
        closeRightPassiveButton.OnMouseOver = new();
        closeRightPassiveButton.OnMouseOver.AddListener((Action)(() => closeRightSpriteRenderer.color = new(1f, 0.68f, 0.99f, 1f)));

        Tint = __instance.screenTint.gameObject;
        var ttap = Tint.GetComponent<AspectPosition>();
        if (ttap) Object.Destroy(ttap);

        Tint.transform.SetParent(RightPanel.transform);
        Tint.transform.localPosition = new(-0.0824f, 0.0513f, Tint.transform.localPosition.z);
        Tint.transform.localScale = new(1f, 1f, 1f);
        __instance.howToPlayButton.gameObject.SetActive(false);
        __instance.howToPlayButton.transform.parent.Find("FreePlayButton").gameObject.SetActive(false);

        if (!(BottomButtonBounds = GameObject.Find("BottomButtonBounds"))) return;

        BottomButtonBounds.transform.localPosition -= new Vector3(0f, 0.1f, 0f);

        return;

        static void ResetParent(GameObject obj) => obj.transform.SetParent(LeftPanel.transform.parent);

        void FormatButtonColor(PassiveButton button, Color inActiveColor, Color activeColor, Color inActiveTextColor, Color activeTextColor)
        {
            button.activeSprites.transform.FindChild("Shine")?.gameObject.SetActive(false);
            button.inactiveSprites.transform.FindChild("Shine")?.gameObject.SetActive(false);
            var activeRenderer = button.activeSprites.GetComponent<SpriteRenderer>();
            var inActiveRenderer = button.inactiveSprites.GetComponent<SpriteRenderer>();
            activeRenderer.sprite = minorActiveSprite;
            inActiveRenderer.sprite = minorActiveSprite;
            activeRenderer.color = activeColor.a == 0f ? new(inActiveColor.r, inActiveColor.g, inActiveColor.b, 1f) : activeColor;
            inActiveRenderer.color = inActiveColor;
            button.activeTextColor = activeTextColor;
            button.inactiveTextColor = inActiveTextColor;
        }
    }
}

[HarmonyPatch(typeof(ModManager), nameof(ModManager.LateUpdate))]
internal static class ModManagerLateUpdatePatch
{
    public static bool Prefix(ModManager __instance)
    {
        __instance.ShowModStamp();

        LateTask.Update(Time.deltaTime);
        ChatBubbleShower.Update();

        if (LobbySharingAPI.LastRoomCode != string.Empty && Utils.TimeStamp - LobbySharingAPI.LastRequestTimeStamp > 60)
            LobbySharingAPI.NotifyLobbyStatusChanged(PlayerControl.LocalPlayer == null ? LobbyStatus.Closed : GameStates.InGame ? LobbyStatus.In_Game : LobbyStatus.In_Lobby);

        return false;
    }

    public static void Postfix(ModManager __instance)
    {
        __instance.localCamera = !DestroyableSingleton<HudManager>.InstanceExists
            ? Camera.main
            : FastDestroyableSingleton<HudManager>.Instance.GetComponentInChildren<Camera>();

        if (__instance.localCamera != null)
        {
            float offsetY = HudManager.InstanceExists ? 1.1f : 0.9f;

            __instance.ModStamp.transform.position = AspectPosition.ComputeWorldPosition(
                __instance.localCamera, AspectPosition.EdgeAlignments.RightTop,
                new(0.4f, offsetY, __instance.localCamera.nearClipPlane + 0.1f));
        }
    }
}

[HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Open))]
internal static class OptionsMenuBehaviourOpenPatch
{
    public static bool Prefix(OptionsMenuBehaviour __instance)
    {
        try
        {
            if (DestroyableSingleton<HudManager>.InstanceExists && !FastDestroyableSingleton<HudManager>.Instance.SettingsButton.activeSelf) return false;
        }
        catch { }

        __instance.ResetText();

        if (__instance.gameObject.activeSelf)
        {
            if (!__instance.Toggle) return false;

            __instance.GetComponent<TransitionOpen>().Close();
        }
        else
        {
            if (Minigame.Instance != null) Minigame.Instance.Close();

            __instance.OpenTabGroup(0);
            __instance.UpdateButtons();
            __instance.gameObject.SetActive(true);
            __instance.MenuButton?.SelectButton(true);
            if (DestroyableSingleton<HudManager>.InstanceExists) ConsoleJoystick.SetMode_MenuAdditive();

            if (!__instance.grabbedControllerButtons)
            {
                __instance.grabbedControllerButtons = true;
                __instance.GrabControllerButtons();
            }

            ControllerManager.Instance.OpenOverlayMenu("OptionsMenu", __instance.BackButton, __instance.DefaultButtonSelected, __instance.ControllerSelectable);
        }

        return false;
    }
}
