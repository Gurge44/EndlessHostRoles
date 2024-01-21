using HarmonyLib;
using System;
using System.Linq;
using System.Text;
using TMPro;
using TOHE.Modules;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE;

[HarmonyPatch(typeof(PingTracker), nameof(PingTracker.Update))]
internal class PingTrackerUpdatePatch
{
    private static readonly StringBuilder sb = new();
    private static long LastUpdate = 0;
    private static void Postfix(PingTracker __instance)
    {
        __instance.text.alignment = TextAlignmentOptions.TopRight;
        __instance.text.text = sb.ToString();

        long now = Utils.GetTimeStamp();
        if (now + 1 <= LastUpdate) return; // Only update every 2 seconds
        LastUpdate = now;

        sb.Clear();

        sb.Append(Main.credentialsText);

        var ping = AmongUsClient.Instance.Ping;
        string color = "#ff4500";
        if (ping < 30) color = "#44dfcc";
        else if (ping < 100) color = "#7bc690";
        else if (ping < 200) color = "#f3920e";
        else if (ping < 400) color = "#ff146e";
        sb.Append("\r\n").Append($"<color={color}>Ping: {ping} ms</color>");

        if (Options.NoGameEnd.GetBool()) sb.Append("\r\n").Append(Utils.ColorString(Color.red, GetString("NoGameEnd")));
        //if (Options.AllowConsole.GetBool()) sb.Append("\r\n").Append(Utils.ColorString(Color.red, GetString("AllowConsole")));
        if (!GameStates.IsModHost) sb.Append("\r\n").Append(Utils.ColorString(Color.red, GetString("Warning.NoModHost")));
        if (DebugModeManager.IsDebugMode) sb.Append("\r\n").Append(Utils.ColorString(Color.green, GetString("DebugMode")));
        //if (Options.LowLoadMode.GetBool()) sb.Append("\r\n").Append(Utils.ColorString(Color.green, GetString("LowLoadMode")));
        //if (Options.GuesserMode.GetBool()) sb.Append("\r\n").Append(Utils.ColorString(Color.yellow, GetString("GuesserMode")));

        var offset_x = 1.2f; //右端からのオフセット
        if (HudManager.InstanceExists && HudManager._instance.Chat.chatButton.active) offset_x += 0.8f; //チャットボタンがある場合の追加オフセット
        if (FriendsListManager.InstanceExists && FriendsListManager._instance.FriendsListButton.Button.active) offset_x += 0.8f; //フレンドリストボタンがある場合の追加オフセット
        __instance.GetComponent<AspectPosition>().DistanceFromEdge = new Vector3(offset_x, 0f, 0f);
    }
}
[HarmonyPatch(typeof(VersionShower), nameof(VersionShower.Start))]
internal class VersionShowerStartPatch
{
    //public static GameObject OVersionShower;
    //private static TextMeshPro SpecialEventText;
    //private static TextMeshPro VisitText;

    private static void Postfix(VersionShower __instance)
    {
        Main.credentialsText = $"<size=1.5><color={Main.ModColor}>{Main.ModName}</color> v{Main.PluginDisplayVersion} <color=#a54aff>by <color=#ffff00>Gurge44</color>";
        string menuText = $"\r\n<color={Main.ModColor}>{Main.ModName}</color> v{Main.PluginDisplayVersion}\r\n<color=#a54aff>By <color=#ffff00>Gurge44</color>";

        if (Main.IsAprilFools) Main.credentialsText = $"\r\n<color=#00bfff>Town Of Host</color> v11.45.14";

        var credentials = UnityEngine.Object.Instantiate(__instance.text);
        credentials.text = menuText;
        credentials.alignment = TextAlignmentOptions.Right;
        credentials.transform.position = new Vector3(1f, 2.79f, -2f);
        credentials.fontSize = credentials.fontSizeMax = credentials.fontSizeMin = 2f;

        ErrorText.Create(__instance.text);
        if (Main.hasArgumentException && ErrorText.Instance != null)
        {
            ErrorText.Instance.AddError(ErrorCode.Main_DictionaryError);
        }

        VersionChecker.Check();
    }
}

[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
internal class TitleLogoPatch
{
    public static GameObject Ambience;
    public static GameObject LoadingHint;

    //private static readonly Color themeColor1 = new(0.99f, 0.55f, 0.56f);
    //private static readonly Color themeColor2 = new(1f, 0.31f, 0.09f);
    //private static readonly Color themeColor3 = new(0.08f, 0.03f, 0.12f);
    //private static readonly Color themeColor4 = new(0.11f, 0.13f, 0.59f);
    //private static readonly Color themeColor5 = new(0.63f, 0.53f, 0.89f);
    //private static readonly Color themeColor6 = new(0.69f, 0.2f, 0.65f);
    //private static readonly Color themeColor7 = new(0.97f, 0.41f, 0.61f);
    //private static readonly Color themeColor8 = new(0.27f, 0.21f, 0.7f);
    //private static readonly Color themeColor9 = new(0.42f, 0.3f, 0.8f);
    //private static readonly Color themeColor10 = new(0.18f, 0.2f, 0.59f);
    //private static readonly Color themeColor11 = new(0.96f, 0.88f, 0.86f);

    private static readonly Color themeColor1 = new(0.98f, 0.7f, 0.44f);
    private static readonly Color themeColor2 = new(0.98f, 0.61f, 0.42f);
    private static readonly Color themeColor3 = new(0.04f, 0.41f, 0.75f);
    private static readonly Color themeColor4 = new(0.12f, 0.17f, 0.47f);
    private static readonly Color themeColor5 = new(0.38f, 0.44f, 0.66f);
    private static readonly Color themeColor6 = new(0.72f, 0.68f, 0.79f);
    private static readonly Color themeColor7 = themeColor1.ShadeColor(0.1f);
    private static readonly Color themeColor8 = new(0.19f, 0.24f, 0.37f);
    private static readonly Color themeColor9 = themeColor5.ShadeColor(0.1f);
    private static readonly Color themeColor10 = themeColor4.ShadeColor(0.1f);
    private static readonly Color themeColor11 = themeColor6.ShadeColor(0.1f);

    private static void Postfix(MainMenuManager __instance)
    {
        if (!Options.IsLoaded)
        {
            LoadingHint = new GameObject("LoadingHint");
            LoadingHint.transform.position = Vector3.down;
            var LoadingHintText = LoadingHint.AddComponent<TextMeshPro>();
            LoadingHintText.text = GetString("Loading");
            LoadingHintText.alignment = TextAlignmentOptions.Center;
            LoadingHintText.fontSize = 5f;
            __instance.playButton.transform.gameObject.SetActive(false);
        }
        if ((Ambience = GameObject.Find("Ambience")) != null)
        {
            try
            {
                if (Options.IsLoaded) __instance.playButton.transform.gameObject.SetActive(true);

                SpriteRenderer activeSpriteRender = __instance.playButton.activeSprites.GetComponent<SpriteRenderer>();
                activeSpriteRender.color = themeColor1;

                SpriteRenderer inactiveSpriteRender = __instance.playButton.inactiveSprites.GetComponent<SpriteRenderer>();
                inactiveSpriteRender.color = themeColor2;
                inactiveSpriteRender.sprite = activeSpriteRender.sprite;

                __instance.playLocalButton.activeSprites.GetComponent<SpriteRenderer>().color = Color.yellow;
                __instance.PlayOnlineButton.activeSprites.GetComponent<SpriteRenderer>().color = Color.yellow;

                __instance.howToPlayButton.activeSprites.GetComponent<SpriteRenderer>().color = Color.magenta;
                __instance.howToPlayButton.inactiveSprites.GetComponent<SpriteRenderer>().color = Color.blue;
                __instance.howToPlayButton.activeTextColor = Color.white;
                __instance.howToPlayButton.inactiveTextColor = Color.white;
                __instance.accountCTAButton.activeSprites.GetComponent<SpriteRenderer>().color = Color.yellow;

                __instance.playButton.activeTextColor = themeColor3;
                __instance.playButton.inactiveTextColor = themeColor3;

                __instance.inventoryButton.inactiveSprites.GetComponent<SpriteRenderer>().color = themeColor4;
                __instance.inventoryButton.activeSprites.GetComponent<SpriteRenderer>().color = themeColor5;
                __instance.inventoryButton.activeTextColor = Color.white;
                __instance.inventoryButton.inactiveTextColor = Color.white;

                __instance.shopButton.inactiveSprites.GetComponent<SpriteRenderer>().color = themeColor4;
                __instance.shopButton.activeSprites.GetComponent<SpriteRenderer>().color = themeColor5;
                __instance.shopButton.activeTextColor = Color.white;
                __instance.shopButton.inactiveTextColor = Color.white;

                __instance.newsButton.inactiveSprites.GetComponent<SpriteRenderer>().color = themeColor6;
                __instance.newsButton.activeSprites.GetComponent<SpriteRenderer>().color = themeColor7;
                __instance.newsButton.activeTextColor = Color.white;
                __instance.newsButton.inactiveTextColor = Color.white;

                __instance.myAccountButton.inactiveSprites.GetComponent<SpriteRenderer>().color = themeColor6;
                __instance.myAccountButton.activeSprites.GetComponent<SpriteRenderer>().color = themeColor7;
                __instance.myAccountButton.activeTextColor = Color.white;
                __instance.myAccountButton.inactiveTextColor = Color.white;

                __instance.settingsButton.inactiveSprites.GetComponent<SpriteRenderer>().color = themeColor6;
                __instance.settingsButton.activeSprites.GetComponent<SpriteRenderer>().color = themeColor7;
                __instance.settingsButton.activeTextColor = Color.white;
                __instance.settingsButton.inactiveTextColor = Color.white;

                __instance.quitButton.inactiveSprites.GetComponent<SpriteRenderer>().color = themeColor8;
                __instance.quitButton.activeSprites.GetComponent<SpriteRenderer>().color = themeColor9;
                __instance.quitButton.activeTextColor = Color.white;
                __instance.quitButton.inactiveTextColor = Color.white;

                __instance.creditsButton.inactiveSprites.GetComponent<SpriteRenderer>().color = themeColor10;
                __instance.creditsButton.activeSprites.GetComponent<SpriteRenderer>().color = themeColor11;
                __instance.creditsButton.activeTextColor = Color.white;
                __instance.creditsButton.inactiveTextColor = Color.white;

                GameObject.Find("WindowShine")?.transform.gameObject.SetActive(false);
                GameObject.Find("ScreenCover")?.transform.gameObject.SetActive(false);
                GameObject.Find("BackgroundTexture")?.transform.gameObject.SetActive(false);

                Ambience.SetActive(false);
                var CustomBG = new GameObject("CustomBG");
                CustomBG.transform.position = new Vector3(0f, 0f, 520f);
                var bgRenderer = CustomBG.AddComponent<SpriteRenderer>();
                bgRenderer.sprite = Utils.LoadSprite("TOHE.Resources.Images.WinterBG.jpg", 180f);

                if (__instance.screenTint != null)
                {
                    __instance.screenTint.gameObject.transform.localPosition += new Vector3(1000f, 0f);
                    __instance.screenTint.enabled = false;
                }
                __instance.rightPanelMask?.SetActive(true);

                GameObject leftPanel = GameObject.Find("LeftPanel")?.transform.gameObject;
                GameObject rightPanel = GameObject.Find("RightPanel")?.transform.gameObject;
                rightPanel.gameObject.GetComponent<SpriteRenderer>().enabled = false;
                GameObject maskedBlackScreen = GameObject.Find("MaskedBlackScreen")?.transform.gameObject;
                if (maskedBlackScreen != null)
                {
                    maskedBlackScreen.GetComponent<SpriteRenderer>().enabled = false;
                    maskedBlackScreen.transform.localPosition = new Vector3(-2.5f, 0.6f);
                    maskedBlackScreen.transform.localScale = new Vector3(7.35f, 4.5f, 4f);
                }

                GameObject.Find("Shine")?.transform.gameObject.SetActive(false);

                leftPanel?.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.name == "Shine").Do(r => r.color = new Color(0f, 0f, 1f, 0.1f));

                if (leftPanel != null) leftPanel.gameObject.GetComponent<SpriteRenderer>().enabled = false;
                GameObject.Find("LeftPanel")?.transform.Find("Divider")?.gameObject.SetActive(false);

                PlayerParticles particles = UnityEngine.Object.FindObjectOfType<PlayerParticles>();
                particles?.gameObject.SetActive(false);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.ToString(), "MainMenuLoader");
            }
        }
    }
}
[HarmonyPatch(typeof(ModManager), nameof(ModManager.LateUpdate))]
internal class ModManagerLateUpdatePatch
{
    public static void Prefix(ModManager __instance)
    {
        __instance.ShowModStamp();

        LateTask.Update(Time.deltaTime);
        CheckMurderPatch.Update();
    }
    public static void Postfix(ModManager __instance)
    {
        var offset_y = HudManager.InstanceExists ? 1.6f : 0.9f;
        __instance.ModStamp.transform.position = AspectPosition.ComputeWorldPosition(
            __instance.localCamera, AspectPosition.EdgeAlignments.RightTop,
            new Vector3(0.4f, offset_y, __instance.localCamera.nearClipPlane + 0.1f));
    }
}