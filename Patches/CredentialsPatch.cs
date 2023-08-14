using HarmonyLib;
using Il2CppMono.Security;
using System;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

using static TOHE.Translator;

namespace TOHE;

[HarmonyPatch(typeof(PingTracker), nameof(PingTracker.Update))]
internal class PingTrackerUpdatePatch
{
    private static readonly StringBuilder sb = new();

    private static void Postfix(PingTracker __instance)
    {
        __instance.text.alignment = TMPro.TextAlignmentOptions.TopRight;

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
        Main.credentialsText = $"\r<size=2><color={Main.ModColor}>{Main.ModName}</color> v{Main.PluginDisplayVersion} by <color=#ffff00>Gurge44</color>";
    //    Main.credentialsText = $"\r\n<color=#de56fd>TOHE SolarLoonieEdit</color> v{Main.PluginDisplayVersion}";
        if (Main.IsAprilFools) Main.credentialsText = $"\r\n<color=#00bfff>Town Of Host</color> v11.45.14";
#if DEBUG
      //  Main.credentialsText += $"\r\n<color=#a54aff>Modified by </color><color=#ff3b6f>Loonie</color>";
        Main.credentialsText += $"\r\n<color=#a54aff>By <color=#ffc0cb>KARPED1EM</color> & </color><color=#f34c50>Loonie</color>";
#endif

#if RELEASE
        string additionalCredentials = GetString("TextBelowVersionText");
        if (additionalCredentials != null && additionalCredentials != "*TextBelowVersionText")
        {
            Main.credentialsText += $"\n{additionalCredentials}";
        }
#endif
        //var credentials = Object.Instantiate(__instance.text);
        //credentials.text = Main.credentialsText;
        //credentials.alignment = TextAlignmentOptions.TopRight;
        //credentials.transform.position = new Vector3(3.2f, 2.5f, 0);

        //ErrorText.Create(__instance.text);
        //if (Main.hasArgumentException && ErrorText.Instance != null)
        //    ErrorText.Instance.AddError(ErrorCode.Main_DictionaryError);

        //if (SpecialEventText == null)
        //{
        //    SpecialEventText = Object.Instantiate(__instance.text);
        //    SpecialEventText.text = "";
        //    SpecialEventText.color = Color.white;
        //    SpecialEventText.fontSize += 2.5f;
        //    SpecialEventText.alignment = TextAlignmentOptions.Top;
        //    SpecialEventText.transform.position = new Vector3(0, 0.5f, 0);
        //}
        //SpecialEventText.enabled = TitleLogoPatch.amongUsLogo != null;
        //if (Main.IsInitialRelease)
        //{
        //    SpecialEventText.text = $"Happy Birthday to {Main.ModName}!";
        //    ColorUtility.TryParseHtmlString(Main.ModColor, out var col);
        //    SpecialEventText.color = col;
        //}
      /*else if (!Main.IsAprilFools)
        {
            SpecialEventText.text = $"{Main.MainMenuText}";
            SpecialEventText.fontSize = 0.9f;
            SpecialEventText.color = Color.white;
            SpecialEventText.alignment = TextAlignmentOptions.TopRight;
            SpecialEventText.transform.position = new Vector3(4.6f, 2.725f, 0);
        }

        if ((OVersionShower = GameObject.Find("VersionShower")) != null && !Main.IsAprilFools)
        {
            OVersionShower.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
            OVersionShower.transform.position = new Vector3(-7.3f, 3.9f, 0f);
            if (TitleLogoPatch.amongUsLogo != null)
            {
                if (VisitText == null && ModUpdater.visit > 0)
                {
                    VisitText = Object.Instantiate(__instance.text);
                    VisitText.text = string.Format(GetString("TOHEVisitorCount"), Main.ModColor, ModUpdater.visit);
                    VisitText.color = Color.white;
                    VisitText.fontSize = 1.2f;
                    //VisitText.alignment = TMPro.TextAlignmentOptions.Top;
                    OVersionShower.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
                    VisitText.transform.position = new Vector3(-5.3f, 2.75f, 0f);
                }
            }
        }*/
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
        if ((Ambience = GameObject.Find("Ambience")) != null)
        {
            try
            {
                SpriteRenderer activeSpriteRender = __instance.playButton.activeSprites.GetComponent<SpriteRenderer>();
                activeSpriteRender.color = new Color(1f, 0f, 0.62f);

                SpriteRenderer inactiveSpriteRender = __instance.playButton.inactiveSprites.GetComponent<SpriteRenderer>();
                inactiveSpriteRender.color = new Color(1f, 0f, 0.35f);
                inactiveSpriteRender.sprite = activeSpriteRender.sprite;

                __instance.playButton.activeTextColor = Color.white;
                __instance.playButton.inactiveTextColor = Color.white;

                __instance.inventoryButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new Color(0.95f, 0f, 1f);
                __instance.inventoryButton.activeSprites.GetComponent<SpriteRenderer>().color = new Color(1f, 0f, 0.85f);
                __instance.inventoryButton.activeTextColor = Color.white;
                __instance.inventoryButton.inactiveTextColor = Color.white;

                __instance.shopButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new Color(1f, 0f, 0.85f);
                __instance.shopButton.activeSprites.GetComponent<SpriteRenderer>().color = new Color(1f, 0f, 0.85f);
                __instance.shopButton.activeTextColor = Color.white;
                __instance.shopButton.inactiveTextColor = Color.white;

                __instance.newsButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new Color(0.82f, 0f, 1f);
                __instance.newsButton.activeSprites.GetComponent<SpriteRenderer>().color = new Color(1f, 0f, 0.85f);
                __instance.newsButton.activeTextColor = Color.white;
                __instance.newsButton.inactiveTextColor = Color.white;

                __instance.myAccountButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new Color(0.95f, 0f, 1f);
                __instance.myAccountButton.activeSprites.GetComponent<SpriteRenderer>().color = new Color(1f, 0f, 0.85f);
                __instance.myAccountButton.activeTextColor = Color.white;
                __instance.myAccountButton.inactiveTextColor = Color.white;

                __instance.settingsButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new Color(1f, 0f, 0.85f);
                __instance.settingsButton.activeSprites.GetComponent<SpriteRenderer>().color = new Color(1f, 0f, 0.85f);
                __instance.settingsButton.activeTextColor = Color.white;
                __instance.settingsButton.inactiveTextColor = Color.white;

                __instance.quitButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new Color(1f, 0f, 0.35f);
                __instance.quitButton.activeSprites.GetComponent<SpriteRenderer>().color = new Color(1f, 0f, 0.62f);
                __instance.quitButton.activeTextColor = Color.white;
                __instance.quitButton.inactiveTextColor = Color.white;

                __instance.creditsButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new Color(0.5f, 0f, 0.85f);
                __instance.creditsButton.activeSprites.GetComponent<SpriteRenderer>().color = new Color(1f, 0f, 0.85f);
                __instance.creditsButton.activeTextColor = Color.white;
                __instance.creditsButton.inactiveTextColor = Color.white;

                GameObject.Find("WindowShine").transform.gameObject.SetActive(false);
                GameObject.Find("ScreenCover").transform.gameObject.SetActive(false);
                GameObject.Find("BackgroundTexture").transform.gameObject.SetActive(false);

                Ambience.SetActive(false);
                var CustomBG = new GameObject("CustomBG");
                CustomBG.transform.position = new Vector3(0f, 0f, 520f);
                var bgRenderer = CustomBG.AddComponent<SpriteRenderer>();
                bgRenderer.sprite = Utils.LoadSprite("TOHE.Resources.Images.PL.png", 245f);

                __instance.screenTint.gameObject.transform.localPosition += new Vector3(1000f, 0f);
                __instance.screenTint.enabled = false;
                __instance.rightPanelMask.SetActive(true);

                GameObject leftPanel = GameObject.Find("LeftPanel").transform.gameObject;
                GameObject rightPanel = GameObject.Find("RightPanel").transform.gameObject;
                rightPanel.gameObject.GetComponent<SpriteRenderer>().enabled = false;
                GameObject maskedBlackScreen = GameObject.Find("MaskedBlackScreen").transform.gameObject;
                maskedBlackScreen.GetComponent<SpriteRenderer>().enabled = false;
                maskedBlackScreen.transform.localPosition = new Vector3(-2.5f, 0.6f);
                maskedBlackScreen.transform.localScale = new Vector3(7.35f, 4.5f, 4f);

                GameObject.Find("Shine").transform.gameObject.SetActive(false);

                leftPanel.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.name == "Shine").Do(r => r.color = new Color(1f, 0f, 0.35f, 0.2f));
                //leftPanel.GetComponents<SpriteRenderer>().Where(r => r.name == "Shine").Do(r => r.color = new Color(1f, 0f, 0f));

                leftPanel.gameObject.GetComponent<SpriteRenderer>().enabled = false;
                GameObject.Find("LeftPanel").transform.Find("Divider").gameObject.SetActive(false);

                //__instance.playButton.OnClick = __instance.PlayOnlineButton.OnClick;
                //new LateTask(() => __instance.playButton.buttonText.text = "Play Online", 0.001f);

                __instance.playButton.transform.localPosition -= new Vector3(0f, 1.4f);

                //PassiveButton playLocalButton = UnityEngine.Object.Instantiate(__instance.playButton, __instance.transform);
                //playLocalButton.transform.localPosition -= new Vector3(3.4f, 1.5f);
                //playLocalButton.transform.localScale -= new Vector3(0.16f, 0.2f);
                //playLocalButton.activeSprites.GetComponent<SpriteRenderer>().color = activeSpriteRender.color;

                //playLocalButton.OnClick = __instance.playLocalButton.OnClick;
                //new LateTask(() => playLocalButton.buttonText.text = "Play Local", 0.001f);

                PlayerParticles particles = UnityEngine.Object.FindObjectOfType<PlayerParticles>();
                particles.gameObject.SetActive(false);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "Platform");
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