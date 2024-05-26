using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using static EHR.Translator;
using Object = UnityEngine.Object;

namespace EHR;

[HarmonyPatch]
public class MainMenuManagerPatch
{
    public static GameObject template;

    //public static GameObject qqButton;
    //public static GameObject discordButton;
    public static GameObject updateButton;

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.LateUpdate)), HarmonyPostfix]
    public static void Postfix(MainMenuManager __instance)
    {
        if (__instance == null) return;
        try
        {
            __instance.playButton.transform.gameObject.SetActive(Options.IsLoaded);
            if (TitleLogoPatch.LoadingHint == null) return;
            TitleLogoPatch.LoadingHint.SetActive(!Options.IsLoaded);
            TitleLogoPatch.LoadingHint.GetComponent<TextMeshPro>().text = string.Format(GetString("LoadingWithPercentage"), Options.LoadingPercentage, Options.MainLoadingText, Options.RoleLoadingText);
        }
        catch
        {
        }
    }

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start)), HarmonyPrefix]
    public static void Start_Prefix(MainMenuManager __instance)
    {
        if (template == null) template = GameObject.Find("/MainUI/ExitGameButton");
        if (template == null) return;

        //if (CultureInfo.CurrentCulture.Name == "zh-CN")
        //{
        //    //生成QQ群按钮
        //    if (qqButton == null) qqButton = Object.Instantiate(template, template.transform.parent);
        //    qqButton.name = "qqButton";
        //    qqButton.transform.position = Vector3.Reflect(template.transform.position, Vector3.left);

        //    var qqText = qqButton.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
        //    Color qqColor = new Color32(0, 164, 255, byte.MaxValue);
        //    PassiveButton qqPassiveButton = qqButton.GetComponent<PassiveButton>();
        //    SpriteRenderer qqButtonSprite = qqButton.GetComponent<SpriteRenderer>();
        //    qqPassiveButton.OnClick = new();
        //    qqPassiveButton.OnClick.AddListener((Action)(() => Application.OpenURL(Main.QQInviteUrl)));
        //    qqPassiveButton.OnMouseOut.AddListener((Action)(() => qqButtonSprite.color = qqText.color = qqColor));
        //    __instance.StartCoroutine(Effects.Lerp(0.01f, new Action<float>((p) => qqText.SetText("QQ群"))));
        //    qqButtonSprite.color = qqText.color = qqColor;
        //    qqButton.gameObject.SetActive(Main.ShowQQButton && !Main.IsAprilFools);
        //}
        //else
        //{
        //    //Discordボタンを生成
        //    if (discordButton == null) discordButton = Object.Instantiate(template, template.transform.parent);
        //    discordButton.name = "DiscordButton";
        //    discordButton.transform.position = Vector3.Reflect(template.transform.position, Vector3.left);

        //    var discordText = discordButton.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
        //    Color discordColor = new Color32(86, 98, 246, byte.MaxValue);
        //    PassiveButton discordPassiveButton = discordButton.GetComponent<PassiveButton>();
        //    SpriteRenderer discordButtonSprite = discordButton.GetComponent<SpriteRenderer>();
        //    discordPassiveButton.OnClick = new();
        //    discordPassiveButton.OnClick.AddListener((Action)(() => Application.OpenURL(Main.DiscordInviteUrl)));
        //    discordPassiveButton.OnMouseOut.AddListener((Action)(() => discordButtonSprite.color = discordText.color = discordColor));
        //    __instance.StartCoroutine(Effects.Lerp(0.01f, new Action<float>((p) => discordText.SetText("Discord"))));
        //    discordButtonSprite.color = discordText.color = discordColor;
        //    discordButton.gameObject.SetActive(Main.ShowDiscordButton && !Main.IsAprilFools);
        //}

        if (updateButton == null) updateButton = Object.Instantiate(template, template.transform.parent);
        updateButton.name = "UpdateButton";
        updateButton.transform.position = template.transform.position + new Vector3(0.25f, 0.75f);
        updateButton.transform.GetChild(0).GetComponent<RectTransform>().localScale *= 1.5f;

        var updateText = updateButton.transform.GetChild(0).GetComponent<TMP_Text>();
        Color updateColor = new Color32(247, 56, 23, byte.MaxValue);
        PassiveButton updatePassiveButton = updateButton.GetComponent<PassiveButton>();
        SpriteRenderer updateButtonSprite = updateButton.GetComponent<SpriteRenderer>();
        updatePassiveButton.OnClick = new();
        updatePassiveButton.OnClick.AddListener((Action)(() =>
        {
            updateButton.SetActive(false);
            ModUpdater.StartUpdate(ModUpdater.downloadUrl, true);
        }));
        updatePassiveButton.OnMouseOut.AddListener((Action)(() => updateButtonSprite.color = updateText.color = updateColor));
        updateButtonSprite.color = updateText.color = updateColor;
        updateButtonSprite.size *= 1.5f;
        updateButton.gameObject.SetActive(ModUpdater.hasUpdate);

#if RELEASE
        var freeplayButton = GameObject.Find("/MainUI/FreePlayButton");
        if (freeplayButton != null)
        {
            freeplayButton.GetComponent<PassiveButton>().OnClick = new();
            freeplayButton.GetComponent<PassiveButton>().OnClick.AddListener((Action)(() => Application.OpenURL("https://tohe.cc")));
            __instance.StartCoroutine(Effects.Lerp(0.01f, new Action<float>(p => freeplayButton.transform.GetChild(0).GetComponent<TMP_Text>().SetText(GetString("Website")))));
        }
#endif

        Application.targetFrameRate = Main.UnlockFps.Value ? 165 : 60;
    }
}