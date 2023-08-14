using HarmonyLib;
using System;
using TOHE.Modules;
using UnityEngine;
using static UnityEngine.UI.Button;
using Object = UnityEngine.Object;

namespace TOHE;

[HarmonyPatch]
public class MainMenuManagerPatch
{
    public static GameObject template;
    //public static GameObject qqButton;
    //public static GameObject discordButton;
    public static GameObject updateButton;

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

        ////Updateボタンを生成
        if (updateButton == null) updateButton = Object.Instantiate(template, template.transform.parent);
        updateButton.name = "UpdateButton";
        updateButton.transform.position = template.transform.position + new Vector3(0.25f, 0.75f);
        updateButton.transform.GetChild(0).GetComponent<RectTransform>().localScale *= 1.5f;

        var updateText = updateButton.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
        Color updateColor = new Color32(247, 56, 23, byte.MaxValue);
        PassiveButton updatePassiveButton = updateButton.GetComponent<PassiveButton>();
        SpriteRenderer updateButtonSprite = updateButton.GetComponent<SpriteRenderer>();
        updatePassiveButton.OnClick = new();
        updatePassiveButton.OnClick.AddListener((Action)(() =>
        {
            updateButton.SetActive(false);
            ModUpdater.StartUpdate(ModUpdater.downloadUrl);
        }));
        updatePassiveButton.OnMouseOut.AddListener((Action)(() => updateButtonSprite.color = updateText.color = updateColor));
        updateButtonSprite.color = updateText.color = updateColor;
        updateButtonSprite.size *= 1.5f;
        updateButton.SetActive(false);

#if RELEASE
        //フリープレイの無効化
        var freeplayButton = GameObject.Find("/MainUI/FreePlayButton");
            if (freeplayButton != null)
            {
                freeplayButton.GetComponent<PassiveButton>().OnClick = new();
                freeplayButton.GetComponent<PassiveButton>().OnClick.AddListener((Action)(() => Application.OpenURL("https://tohe.cc")));
                __instance.StartCoroutine(Effects.Lerp(0.01f, new Action<float>((p) => freeplayButton.transform.GetChild(0).GetComponent<TMPro.TMP_Text>().SetText(Translator.GetString("Website")))));
            }
#endif

        if (Main.IsAprilFools) return;

        var bottomTemplate = GameObject.Find("InventoryButton");
        if (bottomTemplate == null) return;

        var HorseButton = Object.Instantiate(bottomTemplate, bottomTemplate.transform.parent);
        var passiveHorseButton = HorseButton.GetComponent<PassiveButton>();
        var spriteHorseButton = HorseButton.GetComponent<SpriteRenderer>();
        if (HorseModePatch.isHorseMode) spriteHorseButton.transform.localScale *= -1;

        spriteHorseButton.sprite = Utils.LoadSprite($"TOHE.Resources.Images.HorseButton.png", 75f);
        passiveHorseButton.OnClick = new ButtonClickedEvent();
        passiveHorseButton.OnClick.AddListener((Action)(() =>
        {
            RunLoginPatch.ClickCount++;
            if (RunLoginPatch.ClickCount == 10) PlayerControl.LocalPlayer.RPCPlayCustomSound("Gunload", true);
            if (RunLoginPatch.ClickCount == 20) PlayerControl.LocalPlayer.RPCPlayCustomSound("AWP", true);

            spriteHorseButton.transform.localScale *= -1;
            HorseModePatch.isHorseMode = !HorseModePatch.isHorseMode;
            var particles = Object.FindObjectOfType<PlayerParticles>();
            if (particles != null)
            {
                particles.pool.ReclaimAll();
                particles.Start();
            }
        }));
        var DleksButton = Object.Instantiate(bottomTemplate, bottomTemplate.transform.parent);
        var passiveDleksButton = DleksButton.GetComponent<PassiveButton>();
        var spriteDleksButton = DleksButton.GetComponent<SpriteRenderer>();
        if (DleksPatch.isDleks) spriteDleksButton.transform.localScale *= -1;

        spriteDleksButton.sprite = Utils.LoadSprite($"TOHE.Resources.Images.DleksButton.png", 75f);
        passiveDleksButton.OnClick = new ButtonClickedEvent();
        passiveDleksButton.OnClick.AddListener((Action)(() =>
        {
            RunLoginPatch.ClickCount++;
            spriteDleksButton.transform.localScale *= -1;
            DleksPatch.isDleks = !DleksPatch.isDleks;
            var particles = Object.FindObjectOfType<PlayerParticles>();
            if (particles != null)
            {
                particles.pool.ReclaimAll();
                particles.Start();
            }
        }));

        var CreditsButton = Object.Instantiate(bottomTemplate, bottomTemplate.transform.parent);
        var passiveCreditsButton = CreditsButton.GetComponent<PassiveButton>();
        var spriteCreditsButton = CreditsButton.GetComponent<SpriteRenderer>();

        spriteCreditsButton.sprite = Utils.LoadSprite($"TOHE.Resources.Images.CreditsButton.png", 75f);
        passiveCreditsButton.OnClick = new ButtonClickedEvent();
        passiveCreditsButton.OnClick.AddListener((Action)(() =>
        {
            CredentialsPatch.LogoPatch.CreditsPopup?.SetActive(true);
        }));

        Application.targetFrameRate = Main.UnlockFPS.Value ? 165 : 60;
    }
}

// 来源：https://github.com/ykundesu/SuperNewRoles/blob/master/SuperNewRoles/Patches/HorseModePatch.cs
[HarmonyPatch(typeof(Constants), nameof(Constants.ShouldHorseAround))]
public static class HorseModePatch
{
    public static bool isHorseMode = false;
    public static bool Prefix(ref bool __result)
    {
        __result = isHorseMode;
        return false;
    }
}
[HarmonyPatch(typeof(Constants), nameof(Constants.ShouldFlipSkeld))]
public static class DleksPatch
{
    public static bool isDleks = false;
    public static bool Prefix(ref bool __result)
    {
        __result = isDleks;
        return false;
    }
}
