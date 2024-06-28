using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace EHR;

[HarmonyPatch]
public static class MainMenuManagerPatch
{
    public static PassiveButton Template;
    public static PassiveButton UpdateButton;
    private static PassiveButton GitHubButton;
    private static PassiveButton DiscordButton;
    private static PassiveButton WebsiteButton;

    private static bool IsOnline;
    public static bool ShowedBak;
    private static bool ShowingPanel;
    private static SpriteRenderer MgLogo;
    private static MainMenuManager Instance { get; set; }

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.OpenGameModeMenu))]
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.OpenAccountMenu))]
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.OpenCredits))]
    [HarmonyPrefix, HarmonyPriority(Priority.Last)]
    public static void ShowRightPanel() => ShowingPanel = true;

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
    [HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Open))]
    [HarmonyPatch(typeof(AnnouncementPopUp), nameof(AnnouncementPopUp.Show))]
    [HarmonyPrefix, HarmonyPriority(Priority.Last)]
    public static void HideRightPanel()
    {
        ShowingPanel = false;
        AccountManager.Instance?.transform.FindChild("AccountTab/AccountWindow")?.gameObject.SetActive(false);
    }

    public static void ShowRightPanelImmediately()
    {
        ShowingPanel = true;
        TitleLogoPatch.RightPanel.transform.localPosition = TitleLogoPatch.RightPanelOp;
        Instance.OpenGameModeMenu();
        Instance.playButton.OnClick.AddListener((UnityEngine.Events.UnityAction)ShowRightPanelImmediately);
    }

    [HarmonyPatch(typeof(SignInStatusComponent), nameof(SignInStatusComponent.SetOnline)), HarmonyPostfix]
    public static void SetOnline_Postfix()
    {
        LateTask.New(() => { IsOnline = true; }, 0.1f, "Set Online Status");
    }

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start)), HarmonyPrefix]
    public static void Start_Prefix(MainMenuManager __instance)
    {
        if (Template == null) Template = __instance.quitButton;
        if (Template == null) return;

        if (UpdateButton == null)
        {
            UpdateButton = CreateButton(
                "updateButton",
                new(4.2f, -1.3f, 1f),
                new(255, 165, 0, byte.MaxValue),
                new(255, 200, 0, byte.MaxValue),
                () => ModUpdater.StartUpdate(ModUpdater.downloadUrl, true),
                Translator.GetString("updateButton"));
            UpdateButton.transform.localScale = Vector3.one;
        }

        UpdateButton.gameObject.SetActive(ModUpdater.hasUpdate);

        Application.targetFrameRate = Main.UnlockFps.Value ? 9999 : 60;
    }

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.LateUpdate)), HarmonyPostfix]
    public static void MainMenuManager_LateUpdate()
    {
        if (GameObject.Find("MainUI") == null) ShowingPanel = false;

        if (TitleLogoPatch.RightPanel != null)
        {
            var pos1 = TitleLogoPatch.RightPanel.transform.localPosition;
            Vector3 lerp1 = Vector3.Lerp(pos1, TitleLogoPatch.RightPanelOp + new Vector3(ShowingPanel ? 0f : 10f, 0f, 0f), Time.deltaTime * (ShowingPanel ? 3f : 2f));
            if (ShowingPanel
                    ? TitleLogoPatch.RightPanel.transform.localPosition.x > TitleLogoPatch.RightPanelOp.x + 0.03f
                    : TitleLogoPatch.RightPanel.transform.localPosition.x < TitleLogoPatch.RightPanelOp.x + 9f
               ) TitleLogoPatch.RightPanel.transform.localPosition = lerp1;
        }

        if (ShowedBak || !IsOnline) return;
        var bak = GameObject.Find("BackgroundTexture");
        if (bak == null || !bak.active) return;
        var pos2 = bak.transform.position;
        Vector3 lerp2 = Vector3.Lerp(pos2, new Vector3(pos2.x, 7.1f, pos2.z), Time.deltaTime * 1.4f);
        bak.transform.position = lerp2;
        if (pos2.y > 7f) ShowedBak = true;
    }

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start)), HarmonyPostfix, HarmonyPriority(Priority.VeryHigh)]
    public static void Start_Postfix(MainMenuManager __instance)
    {
        Instance = __instance;

        SimpleButton.SetBase(__instance.quitButton);
        var logoObject = new GameObject("titleLogo_MG");
        var logoTransform = logoObject.transform;
        MgLogo = logoObject.AddComponent<SpriteRenderer>();
        logoTransform.localPosition = new(2f, -0.5f, 1f);
        logoTransform.localScale *= 1.2f;
        MgLogo.sprite = Utils.LoadSprite("EHR.Resources.Images.EHR-Icon.png", 400f);

        // GitHub Button
        if (GitHubButton == null)
        {
            GitHubButton = CreateButton(
                "GitHubButton",
                new Vector3(-2.3f, -1.3f, 1f),
                new Color32(153, 153, 153, byte.MaxValue),
                new Color32(209, 209, 209, byte.MaxValue),
                () => Application.OpenURL("https://github.com/Gurge44/EndlessHostRoles"),
                Translator.GetString("GitHub")); //"GitHub"
        }

        GitHubButton.gameObject.SetActive(true);

        // Discord Button
        if (DiscordButton == null)
        {
            DiscordButton = CreateButton(
                "DiscordButton",
                new Vector3(-0.5f, -1.3f, 1f),
                new Color32(88, 101, 242, byte.MaxValue),
                new Color32(148, 161, byte.MaxValue, byte.MaxValue),
                () => Application.OpenURL("https://discord.com/invite/m3ayxfumC8"),
                Translator.GetString("Discord")); //"Discord"
        }

        DiscordButton.gameObject.SetActive(true);

        // Website Button
        if (WebsiteButton == null)
        {
            WebsiteButton = CreateButton(
                "WebsiteButton",
                new Vector3(1.3f, -1.3f, 1f),
                new Color32(251, 81, 44, byte.MaxValue),
                new Color32(211, 77, 48, byte.MaxValue),
                () => Application.OpenURL("https://sites.google.com/view/ehr-au"),
                Translator.GetString("Website")); //"Website"
        }

        WebsiteButton.gameObject.SetActive(true);

        Application.targetFrameRate = Main.UnlockFps.Value ? 9999 : 60;
    }

    private static PassiveButton CreateButton(string name, Vector3 localPosition, Color32 normalColor, Color32 hoverColor, Action action, string label, Vector2? scale = null)
    {
        var button = Object.Instantiate(Template, Template.transform.parent);
        button.name = name;
        Object.Destroy(button.GetComponent<AspectPosition>());
        button.transform.localPosition = localPosition;

        button.OnClick = new();
        button.OnClick.AddListener(action);

        var buttonText = button.transform.Find("FontPlacer/Text_TMP").GetComponent<TMP_Text>();
        buttonText.DestroyTranslator();
        buttonText.fontSize = buttonText.fontSizeMax = buttonText.fontSizeMin = 3.5f;
        buttonText.enableWordWrapping = false;
        buttonText.text = label;
        var normalSprite = button.inactiveSprites.GetComponent<SpriteRenderer>();
        var hoverSprite = button.activeSprites.GetComponent<SpriteRenderer>();
        normalSprite.color = normalColor;
        hoverSprite.color = hoverColor;

        var container = buttonText.transform.parent;
        Object.Destroy(container.GetComponent<AspectPosition>());
        Object.Destroy(buttonText.GetComponent<AspectPosition>());
        container.SetLocalX(0f);
        buttonText.transform.SetLocalX(0f);
        buttonText.horizontalAlignment = HorizontalAlignmentOptions.Center;

        var buttonCollider = button.GetComponent<BoxCollider2D>();
        if (scale.HasValue)
        {
            normalSprite.size = hoverSprite.size = buttonCollider.size = scale.Value;
        }

        buttonCollider.offset = new(0f, 0f);

        return button;
    }
}