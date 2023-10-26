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

    private static void Postfix(PingTracker __instance)
    {
        __instance.text.alignment = TextAlignmentOptions.TopRight;

        sb.Clear();

        sb.Append(Main.credentialsText);

        var ping = AmongUsClient.Instance.Ping;
        string color = "#ff4500";
        if (ping < 30) color = "#44dfcc";
        else if (ping < 100) color = "#7bc690";
        else if (ping < 200) color = "#f3920e";
        else if (ping < 400) color = "#ff146e";
        sb.Append($"\r\n").Append($"<color={color}>Ping: {ping} ms</color>");

        if (Options.NoGameEnd.GetBool()) sb.Append($"\r\n").Append(Utils.ColorString(Color.red, GetString("NoGameEnd")));
        //if (Options.AllowConsole.GetBool()) sb.Append($"\r\n").Append(Utils.ColorString(Color.red, GetString("AllowConsole")));
        if (!GameStates.IsModHost) sb.Append($"\r\n").Append(Utils.ColorString(Color.red, GetString("Warning.NoModHost")));
        if (DebugModeManager.IsDebugMode) sb.Append("\r\n").Append(Utils.ColorString(Color.green, GetString("DebugMode")));
        //if (Options.LowLoadMode.GetBool()) sb.Append("\r\n").Append(Utils.ColorString(Color.green, GetString("LowLoadMode")));
        //if (Options.GuesserMode.GetBool()) sb.Append("\r\n").Append(Utils.ColorString(Color.yellow, GetString("GuesserMode")));

        var offset_x = 1.2f; //右端からのオフセット
        if (HudManager.InstanceExists && HudManager._instance.Chat.chatButton.active) offset_x += 0.8f; //チャットボタンがある場合の追加オフセット
        if (FriendsListManager.InstanceExists && FriendsListManager._instance.FriendsListButton.Button.active) offset_x += 0.8f; //フレンドリストボタンがある場合の追加オフセット
        __instance.GetComponent<AspectPosition>().DistanceFromEdge = new Vector3(offset_x, 0f, 0f);

        __instance.text.text = sb.ToString();
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
    //public static GameObject amongUsLogo;
    //public static GameObject PlayLocalButton;
    //public static GameObject PlayOnlineButton;
    //public static GameObject HowToPlayButton;
    //public static GameObject FreePlayButton;
    //public static GameObject BottomButtons;
    public static GameObject LoadingHint;


    private static void Postfix(MainMenuManager __instance)
    {
        /*if (Main.IsAprilFools)
          {
              if ((amongUsLogo = GameObject.Find("bannerLogo_AmongUs")) != null)
              {
                  amongUsLogo.transform.localScale *= 0.4f;
                  amongUsLogo.transform.position += Vector3.up * 0.25f;
              }

              var tohLogo = new GameObject("titleLogo_TOH");
              tohLogo.transform.position = Vector3.up;
              tohLogo.transform.localScale *= 1.2f;
              var renderer = tohLogo.AddComponent<SpriteRenderer>();
              renderer.sprite = Utils.LoadSprite("TOHE.Resources.Images.TownOfHost-Logo.png", 300f);

              return;
          }*/

        //if ((amongUsLogo = GameObject.Find("bannerLogo_AmongUs")) != null)
        //{
        //    amongUsLogo.transform.localScale *= 0.4f;
        //    amongUsLogo.transform.position += Vector3.up * 0.25f;
        //}

        /* if ((PlayLocalButton = GameObject.Find("PlayLocalButton")) != null)
        {
            PlayLocalButton.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            PlayLocalButton.transform.position = new Vector3(-0.76f, -2.1f, 0f);
        }

        if ((PlayOnlineButton = GameObject.Find("PlayOnlineButton")) != null)
        {
            PlayOnlineButton.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            PlayOnlineButton.transform.position = new Vector3(0.725f, -2.1f, 0f);
        }

        if ((HowToPlayButton = GameObject.Find("HowToPlayButton")) != null)
        {
            HowToPlayButton.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            HowToPlayButton.transform.position = new Vector3(-2.225f, -2.175f, 0f);
        }

        if ((FreePlayButton = GameObject.Find("FreePlayButton")) != null)
        {
            FreePlayButton.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            FreePlayButton.transform.position = new Vector3(2.1941f, -2.175f, 0f);
        }

        if ((BottomButtons = GameObject.Find("BottomButtons")) != null)
        {
            BottomButtons.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            BottomButtons.transform.position = new Vector3(0f, -2.71f, 0f);
        }*/

        /*   if ((Ambience = GameObject.Find("Ambience")) != null)
           {
               Ambience.SetActive(false);
               var CustomBG = new GameObject("CustomBG");
               CustomBG.transform.position = new Vector3(2.095f, -0.25f, 520f);
               var bgRenderer = CustomBG.AddComponent<SpriteRenderer>();
               bgRenderer.sprite = Utils.LoadSprite("TOHE.Resources.Images.TOHE-BG.jpg", 245f);
           } */

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
                activeSpriteRender.color = new Color(0.99f, 0.55f, 0.56f);

                SpriteRenderer inactiveSpriteRender = __instance.playButton.inactiveSprites.GetComponent<SpriteRenderer>();
                inactiveSpriteRender.color = new Color(1f, 0.31f, 0.09f);
                inactiveSpriteRender.sprite = activeSpriteRender.sprite;

                __instance.playLocalButton.activeSprites.GetComponent<SpriteRenderer>().color = Color.yellow;
                //__instance.playLocalButton.inactiveSprites.GetComponent<SpriteRenderer>().color = Color.blue;
                //__instance.playLocalButton.activeTextColor = Color.white;
                //__instance.playLocalButton.inactiveTextColor = Color.white;
                __instance.PlayOnlineButton.activeSprites.GetComponent<SpriteRenderer>().color = Color.yellow;
                //__instance.PlayOnlineButton.inactiveSprites.GetComponent<SpriteRenderer>().color = Color.blue;

                __instance.howToPlayButton.activeSprites.GetComponent<SpriteRenderer>().color = Color.magenta;
                __instance.howToPlayButton.inactiveSprites.GetComponent<SpriteRenderer>().color = Color.blue;
                __instance.howToPlayButton.activeTextColor = Color.white;
                __instance.howToPlayButton.inactiveTextColor = Color.white;
                __instance.accountCTAButton.activeSprites.GetComponent<SpriteRenderer>().color = Color.yellow;
                //__instance.accountCTAButton.inactiveSprites.GetComponent<SpriteRenderer>().color = Color.blue;
                //__instance.accountCTAButton.activeTextColor = Color.white;
                //__instance.accountCTAButton.inactiveTextColor = Color.white;

                __instance.playButton.activeTextColor = new Color(0.08f, 0.03f, 0.12f);
                __instance.playButton.inactiveTextColor = new Color(0.08f, 0.03f, 0.12f);

                __instance.inventoryButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new Color(0.11f, 0.13f, 0.59f);
                __instance.inventoryButton.activeSprites.GetComponent<SpriteRenderer>().color = new Color(0.63f, 0.53f, 0.89f);
                __instance.inventoryButton.activeTextColor = Color.white;
                __instance.inventoryButton.inactiveTextColor = Color.white;

                __instance.shopButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new Color(0.11f, 0.13f, 0.59f);
                __instance.shopButton.activeSprites.GetComponent<SpriteRenderer>().color = new Color(0.63f, 0.53f, 0.89f);
                __instance.shopButton.activeTextColor = Color.white;
                __instance.shopButton.inactiveTextColor = Color.white;

                __instance.newsButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new Color(0.69f, 0.2f, 0.65f);
                __instance.newsButton.activeSprites.GetComponent<SpriteRenderer>().color = new Color(0.97f, 0.41f, 0.61f);
                __instance.newsButton.activeTextColor = Color.white;
                __instance.newsButton.inactiveTextColor = Color.white;

                __instance.myAccountButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new Color(0.69f, 0.2f, 0.65f);
                __instance.myAccountButton.activeSprites.GetComponent<SpriteRenderer>().color = new Color(0.97f, 0.41f, 0.61f);
                __instance.myAccountButton.activeTextColor = Color.white;
                __instance.myAccountButton.inactiveTextColor = Color.white;

                __instance.settingsButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new Color(0.69f, 0.2f, 0.65f);
                __instance.settingsButton.activeSprites.GetComponent<SpriteRenderer>().color = new Color(0.97f, 0.41f, 0.61f);
                __instance.settingsButton.activeTextColor = Color.white;
                __instance.settingsButton.inactiveTextColor = Color.white;

                __instance.quitButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new Color(0.27f, 0.21f, 0.7f);
                __instance.quitButton.activeSprites.GetComponent<SpriteRenderer>().color = new Color(0.42f, 0.3f, 0.8f);
                __instance.quitButton.activeTextColor = Color.white;
                __instance.quitButton.inactiveTextColor = Color.white;

                __instance.creditsButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new Color(0.18f, 0.2f, 0.59f);
                __instance.creditsButton.activeSprites.GetComponent<SpriteRenderer>().color = new Color(0.96f, 0.88f, 0.86f);
                __instance.creditsButton.activeTextColor = Color.white;
                __instance.creditsButton.inactiveTextColor = Color.white;

                GameObject.Find("WindowShine")?.transform.gameObject.SetActive(false);
                GameObject.Find("ScreenCover")?.transform.gameObject.SetActive(false);
                GameObject.Find("BackgroundTexture")?.transform.gameObject.SetActive(false);

                Ambience.SetActive(false);
                var CustomBG = new GameObject("CustomBG");
                CustomBG.transform.position = new Vector3(0f, 0f, 520f);
                var bgRenderer = CustomBG.AddComponent<SpriteRenderer>();
                bgRenderer.sprite = Utils.LoadSprite("TOHE.Resources.Images.bg.png", 180f);

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
                //leftPanel.GetComponents<SpriteRenderer>().Where(r => r.name == "Shine").Do(r => r.color = new Color(1f, 0f, 0f));

                if (leftPanel != null) leftPanel.gameObject.GetComponent<SpriteRenderer>().enabled = false;
                GameObject.Find("LeftPanel")?.transform.Find("Divider")?.gameObject.SetActive(false);

                //__instance.playButton.OnClick = __instance.PlayOnlineButton.OnClick;
                //_ = new LateTask(() => __instance.playButton.buttonText.text = "Play Online", 0.001f);

                //__instance.playButton.transform.localPosition -= new Vector3(0f, 1.4f);

                //PassiveButton playLocalButton = UnityEngine.Object.Instantiate(__instance.playButton, __instance.transform);
                //playLocalButton.transform.localPosition -= new Vector3(3.4f, 1.5f);
                //playLocalButton.transform.localScale -= new Vector3(0.16f, 0.2f);
                //playLocalButton.activeSprites.GetComponent<SpriteRenderer>().color = activeSpriteRender.color;

                //playLocalButton.OnClick = __instance.playLocalButton.OnClick;
                //_ = new LateTask(() => playLocalButton.buttonText.text = "Play Local", 0.001f);

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