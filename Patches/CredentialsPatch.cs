using System;
using System.Linq;
using System.Text;
using EHR.Modules;
using HarmonyLib;
using TMPro;
using UnityEngine;
using static EHR.Translator;
using Object = UnityEngine.Object;

namespace EHR;

[HarmonyPatch(typeof(PingTracker), nameof(PingTracker.Update))]
internal class PingTrackerUpdatePatch
{
    private static readonly StringBuilder Sb = new();
    private static long LastUpdate;
    private static int Delay => GameStates.IsInTask ? 8 : 1;

    private static void Postfix(PingTracker __instance)
    {
        __instance.text.alignment = TextAlignmentOptions.TopRight;
        __instance.text.text = Sb.ToString();

        long now = Utils.TimeStamp;
        if (now + Delay <= LastUpdate) return; // Only update every 2 seconds
        LastUpdate = now;

        Sb.Clear();

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
        Sb.Append("\r\n").Append($"<color={color}>{GetString("PingText")}: {ping} ms</color>");

        if (Options.NoGameEnd.GetBool()) Sb.Append("\r\n").Append(Utils.ColorString(Color.red, GetString("NoGameEnd")));
        if (!GameStates.IsModHost) Sb.Append("\r\n").Append(Utils.ColorString(Color.red, GetString("Warning.NoModHost")));
        if (DebugModeManager.IsDebugMode) Sb.Append("\r\n").Append(Utils.ColorString(Color.green, GetString("DebugMode")));

        if (Main.IsAprilFools || Options.AprilFoolsMode.GetBool()) Sb.Append("\r\n").Append(Utils.ColorString(Color.yellow, "CHEESE"));

        var offsetX = 1.2f;
        if (HudManager.InstanceExists && HudManager._instance.Chat.chatButton.active) offsetX += 0.8f;
        if (FriendsListManager.InstanceExists && FriendsListManager._instance.FriendsListButton.Button.active) offsetX += 0.8f;
        __instance.GetComponent<AspectPosition>().DistanceFromEdge = new(offsetX, 0f, 0f);
    }
}

[HarmonyPatch(typeof(VersionShower), nameof(VersionShower.Start))]
internal class VersionShowerStartPatch
{
    private static void Postfix(VersionShower __instance)
    {
        Main.CredentialsText = $"<size=1.5><color={Main.ModColor}>Endless Host Roles</color> v{Main.PluginDisplayVersion} <color=#a54aff>by</color> <color=#ffff00>Gurge44</color>";
        string menuText = $"\r\n<color={Main.ModColor}>Endless Host Roles</color> v{Main.PluginDisplayVersion}\r\n<color=#a54aff>By</color> <color=#ffff00>Gurge44</color>";

        if (Main.IsAprilFools) Main.CredentialsText = "<color=#00bfff>Endless Madness</color> v11.45.14 <color=#a54aff>by</color> <color=#ffff00>No one</color>";

        var credentials = Object.Instantiate(__instance.text);
        credentials.text = menuText;
        credentials.alignment = TextAlignmentOptions.Right;
        credentials.transform.position = new(1f, 2.79f, -2f);
        credentials.fontSize = credentials.fontSizeMax = credentials.fontSizeMin = 2f;

        ErrorText.Create(__instance.text);
        if (Main.HasArgumentException && ErrorText.Instance != null)
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

    // Winter BG
    // private static readonly Color ThemeColor1 = new(0.98f, 0.7f, 0.44f);
    // private static readonly Color ThemeColor2 = new(0.98f, 0.61f, 0.42f);
    // private static readonly Color ThemeColor3 = new(0.04f, 0.41f, 0.75f);
    // private static readonly Color ThemeColor4 = new(0.12f, 0.17f, 0.47f);
    // private static readonly Color ThemeColor5 = new(0.38f, 0.44f, 0.66f);
    // private static readonly Color ThemeColor6 = new(0.72f, 0.68f, 0.79f);
    // private static readonly Color ThemeColor7 = ThemeColor1.ShadeColor(0.1f);
    // private static readonly Color ThemeColor8 = new(0.19f, 0.24f, 0.37f);
    // private static readonly Color ThemeColor9 = ThemeColor5.ShadeColor(0.1f);
    // private static readonly Color ThemeColor10 = ThemeColor4.ShadeColor(0.1f);
    // private static readonly Color ThemeColor11 = ThemeColor6.ShadeColor(0.1f);

    // Summer BG
    private static readonly Color ThemeColor1 = new(0.706f, 0.588f, 0.455f);
    private static readonly Color ThemeColor2 = new(0.827f, 0.827f, 0.643f);
    private static readonly Color ThemeColor3 = new(0.196f, 0.196f, 0.067f);
    private static readonly Color ThemeColor4 = new(0.145f, 0.655f, 0.859f);
    private static readonly Color ThemeColor5 = new(0.580f, 0.780f, 0.820f);
    private static readonly Color ThemeColor6 = new(0.435f, 0.776f, 0.925f);
    private static readonly Color ThemeColor7 = new(0.161f, 0.439f, 0.588f);
    private static readonly Color ThemeColor8 = new(0.388f, 0.565f, 0.537f);
    private static readonly Color ThemeColor9 = new(0.463f, 0.471f, 0.188f);
    private static readonly Color ThemeColor10 = Color.black;
    private static readonly Color ThemeColor11 = ThemeColor4.ShadeColor(0.1f);

    private static void Postfix(MainMenuManager __instance)
    {
        if (!Options.IsLoaded)
        {
            LoadingHint = new("LoadingHint")
            {
                transform =
                {
                    position = Vector3.down
                }
            };
            var loadingHintText = LoadingHint.AddComponent<TextMeshPro>();
            loadingHintText.text = GetString("Loading");
            loadingHintText.alignment = TextAlignmentOptions.Center;
            loadingHintText.fontSize = 5f;
            __instance.playButton.transform.gameObject.SetActive(false);
        }

        if ((Ambience = GameObject.Find("Ambience")) != null)
        {
            try
            {
                if (Options.IsLoaded) __instance.playButton.transform.gameObject.SetActive(true);

                SpriteRenderer activeSpriteRender = __instance.playButton.activeSprites.GetComponent<SpriteRenderer>();
                activeSpriteRender.color = ThemeColor1;

                SpriteRenderer inactiveSpriteRender = __instance.playButton.inactiveSprites.GetComponent<SpriteRenderer>();
                inactiveSpriteRender.color = ThemeColor2;
                inactiveSpriteRender.sprite = activeSpriteRender.sprite;

                __instance.playLocalButton.activeSprites.GetComponent<SpriteRenderer>().color = Color.yellow;
                __instance.PlayOnlineButton.activeSprites.GetComponent<SpriteRenderer>().color = Color.yellow;

                __instance.howToPlayButton.activeSprites.GetComponent<SpriteRenderer>().color = Color.magenta;
                __instance.howToPlayButton.inactiveSprites.GetComponent<SpriteRenderer>().color = Color.blue;
                __instance.howToPlayButton.activeTextColor = Color.white;
                __instance.howToPlayButton.inactiveTextColor = Color.white;
                __instance.accountCTAButton.activeSprites.GetComponent<SpriteRenderer>().color = Color.yellow;

                __instance.playButton.activeTextColor = ThemeColor3;
                __instance.playButton.inactiveTextColor = ThemeColor3;

                __instance.inventoryButton.inactiveSprites.GetComponent<SpriteRenderer>().color = ThemeColor4;
                __instance.inventoryButton.activeSprites.GetComponent<SpriteRenderer>().color = ThemeColor5;
                __instance.inventoryButton.activeTextColor = Color.white;
                __instance.inventoryButton.inactiveTextColor = Color.white;

                __instance.shopButton.inactiveSprites.GetComponent<SpriteRenderer>().color = ThemeColor4;
                __instance.shopButton.activeSprites.GetComponent<SpriteRenderer>().color = ThemeColor5;
                __instance.shopButton.activeTextColor = Color.white;
                __instance.shopButton.inactiveTextColor = Color.white;

                __instance.newsButton.inactiveSprites.GetComponent<SpriteRenderer>().color = ThemeColor6;
                __instance.newsButton.activeSprites.GetComponent<SpriteRenderer>().color = ThemeColor7;
                __instance.newsButton.activeTextColor = Color.white;
                __instance.newsButton.inactiveTextColor = Color.white;

                __instance.myAccountButton.inactiveSprites.GetComponent<SpriteRenderer>().color = ThemeColor6;
                __instance.myAccountButton.activeSprites.GetComponent<SpriteRenderer>().color = ThemeColor7;
                __instance.myAccountButton.activeTextColor = Color.white;
                __instance.myAccountButton.inactiveTextColor = Color.white;

                __instance.settingsButton.inactiveSprites.GetComponent<SpriteRenderer>().color = ThemeColor6;
                __instance.settingsButton.activeSprites.GetComponent<SpriteRenderer>().color = ThemeColor7;
                __instance.settingsButton.activeTextColor = Color.white;
                __instance.settingsButton.inactiveTextColor = Color.white;

                __instance.quitButton.inactiveSprites.GetComponent<SpriteRenderer>().color = ThemeColor8;
                __instance.quitButton.activeSprites.GetComponent<SpriteRenderer>().color = ThemeColor9;
                __instance.quitButton.activeTextColor = Color.white;
                __instance.quitButton.inactiveTextColor = Color.white;

                __instance.creditsButton.inactiveSprites.GetComponent<SpriteRenderer>().color = ThemeColor10;
                __instance.creditsButton.activeSprites.GetComponent<SpriteRenderer>().color = ThemeColor11;
                __instance.creditsButton.activeTextColor = Color.white;
                __instance.creditsButton.inactiveTextColor = Color.white;

                GameObject.Find("WindowShine")?.transform.gameObject.SetActive(false);
                GameObject.Find("ScreenCover")?.transform.gameObject.SetActive(false);
                GameObject.Find("BackgroundTexture")?.transform.gameObject.SetActive(false);

                Ambience.SetActive(false);
                var customBg = new GameObject("CustomBG")
                {
                    transform =
                    {
                        position = new(0f, 0f, 520f)
                    }
                };
                var bgRenderer = customBg.AddComponent<SpriteRenderer>();
                bgRenderer.sprite = Utils.LoadSprite("EHR.Resources.Images.SummerBG.jpg", 180f);

                if (__instance.screenTint != null)
                {
                    __instance.screenTint.gameObject.transform.localPosition += new Vector3(1000f, 0f);
                    __instance.screenTint.enabled = false;
                }

                __instance.rightPanelMask?.SetActive(true);

                GameObject leftPanel = GameObject.Find("LeftPanel")?.transform.gameObject;
                GameObject rightPanel = GameObject.Find("RightPanel")?.transform.gameObject;
                if (rightPanel != null)
                {
                    rightPanel.gameObject.GetComponent<SpriteRenderer>().enabled = false;
                }

                GameObject maskedBlackScreen = GameObject.Find("MaskedBlackScreen")?.transform.gameObject;
                if (maskedBlackScreen != null)
                {
                    maskedBlackScreen.GetComponent<SpriteRenderer>().enabled = false;
                    maskedBlackScreen.transform.localPosition = new(-2.5f, 0.6f);
                    maskedBlackScreen.transform.localScale = new(7.35f, 4.5f, 4f);
                }

                GameObject.Find("Shine")?.transform.gameObject.SetActive(false);

                leftPanel?.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.name == "Shine").Do(r => r.color = new(0f, 0f, 1f, 0.1f));

                if (leftPanel != null) leftPanel.gameObject.GetComponent<SpriteRenderer>().enabled = false;
                GameObject.Find("LeftPanel")?.transform.Find("Divider")?.gameObject.SetActive(false);

                PlayerParticles particles = Object.FindObjectOfType<PlayerParticles>();
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
    public static bool Prefix(ModManager __instance)
    {
        __instance.ShowModStamp();

        LateTask.Update(Time.deltaTime);
        CheckMurderPatch.Update();

        return false;
    }

    public static void Postfix(ModManager __instance)
    {
        if (__instance.localCamera == null) __instance.localCamera = !DestroyableSingleton<HudManager>.InstanceExists ? Camera.main : DestroyableSingleton<HudManager>.Instance.GetComponentInChildren<Camera>();
        if (__instance.localCamera != null)
        {
            var offsetY = HudManager.InstanceExists ? 1.6f : 0.9f;
            __instance.ModStamp.transform.position = AspectPosition.ComputeWorldPosition(
                __instance.localCamera, AspectPosition.EdgeAlignments.RightTop,
                new(0.4f, offsetY, __instance.localCamera.nearClipPlane + 0.1f));
        }
    }
}