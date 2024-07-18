using System;
using System.Collections.Generic;
using System.Text;
using EHR.Modules;
using HarmonyLib;
using TMPro;
using UnityEngine;
using static EHR.Translator;


namespace EHR;

[HarmonyPatch(typeof(PingTracker), nameof(PingTracker.Update))]
internal class PingTrackerUpdatePatch
{
    public static PingTracker Instance;
    private static readonly StringBuilder Sb = new();
    private static long LastUpdate;
    private static int Delay => GameStates.IsInTask ? 8 : 1;

    private static void Postfix(PingTracker __instance)
    {
        Instance = __instance;

        Instance.text.alignment = TextAlignmentOptions.Center;
        Instance.text.text = Sb.ToString();

        long now = Utils.TimeStamp;
        if (now + Delay <= LastUpdate) return; // Only update every 2 seconds
        LastUpdate = now;

        Sb.Clear();

        if (GameStates.IsLobby) Sb.Append("\r\n");

        Sb.Append(Main.CredentialsText);

        var ping = AmongUsClient.Instance.Ping;
        string color = ping switch
        {
            < 30 => "#44dfcc",
            < 100 => "#7bc690",
            < 200 => "#f3920e",
            < 400 => "#ff146e",
            _ => "#ff4500"
        };
        Sb.Append(GameStates.InGame ? "    -    " : "\r\n");
        Sb.Append($"<color={color}>{GetString("PingText")}: {ping} ms</color>");
        Sb.Append(GameStates.InGame ? "    -    " : "\r\n");
        Sb.Append(string.Format(GetString("Server"), Utils.GetRegionName()));
        if (GameStates.InGame) Sb.Append("\r\n.");

        // if (Options.NoGameEnd.GetBool()) Sb.Append("\r\n<size=1.2>").Append(Utils.ColorString(Color.red, GetString("NoGameEnd"))).Append("</size>");
        // if (!GameStates.IsModHost) Sb.Append("\r\n<size=1.2>").Append(Utils.ColorString(Color.red, GetString("Warning.NoModHost"))).Append("</size>");
        // if (DebugModeManager.IsDebugMode) Sb.Append("\r\n<size=1.2>").Append(Utils.ColorString(Color.green, GetString("DebugMode"))).Append("</size>");
        //
        // if (Main.IsAprilFools || Options.AprilFoolsMode.GetBool()) Sb.Append("\r\n<size=1.2>").Append(Utils.ColorString(Color.yellow, "CHEESE")).Append("</size>");
    }
}

[HarmonyPatch(typeof(VersionShower), nameof(VersionShower.Start))]
internal class VersionShowerStartPatch
{
    private static void Postfix(VersionShower __instance)
    {
        Main.CredentialsText = $"<size=1.5><color={Main.ModColor}>Endless Host Roles</color> v{Main.PluginDisplayVersion} <color=#a54aff>by</color> <color=#ffff00>Gurge44</color>";
        const string menuText = $"<color={Main.ModColor}>Endless Host Roles</color> v{Main.PluginDisplayVersion}\r\n<color=#a54aff>By</color> <color=#ffff00>Gurge44</color>";

        if (Main.IsAprilFools) Main.CredentialsText = "<color=#00bfff>Endless Madness</color> v11.45.14 <color=#a54aff>by</color> <color=#ffff00>No one</color>";

        var credentials = Object.Instantiate(__instance.text);
        credentials.text = menuText;
        credentials.alignment = TextAlignmentOptions.Right;
        credentials.transform.position = new(1f, 2.67f, -2f);
        credentials.fontSize = credentials.fontSizeMax = credentials.fontSizeMin = 2f;

        ErrorText.Create(__instance.text);
        if (Main.HasArgumentException && ErrorText.Instance != null)
        {
            ErrorText.Instance.AddError(ErrorCode.Main_DictionaryError);
        }

        VersionChecker.Check();
    }
}

[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start)), HarmonyPriority(Priority.First)]
internal class TitleLogoPatch
{
    public static GameObject ModStamp;

    public static GameObject Ambience;

    // public static GameObject LoadingHint;
    public static GameObject LeftPanel;
    public static GameObject RightPanel;
    public static GameObject CloseRightButton;
    public static GameObject Tint;
    public static GameObject BottomButtonBounds;

    public static Vector3 RightPanelOp;

    private static void Postfix(MainMenuManager __instance)
    {
        GameObject.Find("BackgroundTexture")?.SetActive(!MainMenuManagerPatch.ShowedBak);

        if (!(ModStamp = GameObject.Find("ModStamp"))) return;
        ModStamp.transform.localScale = new(0.3f, 0.3f, 0.3f);

        // if (!Options.IsLoaded)
        // {
        //     LoadingHint = new("LoadingHint")
        //     {
        //         transform =
        //         {
        //             position = Vector3.down
        //         }
        //     };
        //     var loadingHintText = LoadingHint.AddComponent<TextMeshPro>();
        //     loadingHintText.text = GetString("Loading");
        //     loadingHintText.alignment = TextAlignmentOptions.Center;
        //     loadingHintText.fontSize = 5f;
        //     __instance.playButton.transform.gameObject.SetActive(false);
        // }

        Ambience = GameObject.Find("Ambience");
        if (Ambience != null)
        {
            try
            {
                __instance.playButton.transform.gameObject.SetActive(true);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.ToString(), "MainMenuLoader");
            }
        }

        if (!(LeftPanel = GameObject.Find("LeftPanel"))) return;
        LeftPanel.transform.localScale = new(0.7f, 0.7f, 0.7f);
        LeftPanel.ForEachChild((Il2CppSystem.Action<GameObject>)ResetParent);
        LeftPanel.SetActive(false);

        Color shade = new(0f, 0f, 0f, 0f);
        var standardActiveSprite = __instance.newsButton.activeSprites.GetComponent<SpriteRenderer>().sprite;
        var minorActiveSprite = __instance.quitButton.activeSprites.GetComponent<SpriteRenderer>().sprite;

        Dictionary<List<PassiveButton>, (Sprite, Color, Color, Color, Color)> mainButtons = new()
        {
            { [__instance.playButton, __instance.inventoryButton, __instance.shopButton], (standardActiveSprite, new(1f, 0.524f, 0.549f, 0.8f), shade, Color.white, Color.white) },
            { [__instance.newsButton, __instance.myAccountButton, __instance.settingsButton], (minorActiveSprite, new(1f, 0.825f, 0.686f, 0.8f), shade, Color.white, Color.white) },
            { [__instance.creditsButton, __instance.quitButton], (minorActiveSprite, new(0.526f, 1f, 0.792f, 0.8f), shade, Color.white, Color.white) },
        };

        foreach (var kvp in mainButtons) kvp.Key.Do(button => FormatButtonColor(button, kvp.Value.Item2, kvp.Value.Item3, kvp.Value.Item4, kvp.Value.Item5));

        try
        {
            mainButtons.Keys.Flatten().DoIf(x => x != null, x => x.buttonText.color = Color.white);
        }
        catch
        {
        }

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
internal class ModManagerLateUpdatePatch
{
    public static bool Prefix(ModManager __instance)
    {
        __instance.ShowModStamp();

        LateTask.Update(Time.deltaTime);
        CheckMurderPatch.Update();

        return false;
    }

    public static void Postfix(ModManager __instance)
    {
        __instance.localCamera = !DestroyableSingleton<HudManager>.InstanceExists ? Camera.main : DestroyableSingleton<HudManager>.Instance.GetComponentInChildren<Camera>();
        if (__instance.localCamera != null)
        {
            var offsetY = HudManager.InstanceExists ? 1.1f : 0.9f;
            __instance.ModStamp.transform.position = AspectPosition.ComputeWorldPosition(
                __instance.localCamera, AspectPosition.EdgeAlignments.RightTop,
                new(0.4f, offsetY, __instance.localCamera.nearClipPlane + 0.1f));
        }
    }
}

[HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Open))]
class OptionsMenuBehaviourOpenPatch
{
    public static bool Prefix(OptionsMenuBehaviour __instance)
    {
        try
        {
            if (DestroyableSingleton<HudManager>.InstanceExists && !DestroyableSingleton<HudManager>.Instance.SettingsButton.activeSelf)
                return false;
        }
        catch
        {
        }

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

    public static void Postfix()
    {
        if (GameStates.InGame && GameStates.IsMeeting)
            GuessManager.DestroyIDLabels();
    }
}