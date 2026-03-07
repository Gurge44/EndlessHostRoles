using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace EHR.Patches;

[HarmonyPatch(typeof(LobbyViewSettingsPane))]
public static class LobbyViewSettingsPanePatch
{
    private static StringNames VanillaSettingsTabName => StringNames.OverviewCategory;
    private static StringNames VanillaRolesTabName => StringNames.RolesCategory;

    private static StringNames LastTabPressed = StringNames.OverviewCategory;
    public static CustomGameMode LastGameModeSelected = CustomGameMode.Standard;

    private static bool ForReloadTab = false;
    private static readonly Dictionary<StringNames, PassiveButton> TabButtons = [];
    private static readonly Dictionary<StringNames, TabGroup> TabNames = [];
    private static readonly Dictionary<TabGroup, PassiveButton> AllTabButtons = [];
    private static readonly HashSet<CustomRoles> RoleEnabledList = [];
    private static readonly List<CustomRoles> CahedRoleEnabledList = [];

    [HarmonyPatch(nameof(LobbyViewSettingsPane.Awake))]
    [HarmonyPostfix]
    public static void Awake_Postfix(LobbyViewSettingsPane __instance)
    {
        __instance.StartCoroutine(CoAwakeViwer().WrapToIl2Cpp());
        return;

        IEnumerator CoAwakeViwer()
        {
            var background = __instance.transform.FindChild("Background");
            var backgroundShine = __instance.transform.FindChild("Background Shine");
            var gameModeTexttransform = __instance.gameModeText.transform;
            var divider = __instance.transform.FindChild("Divider");
            var tabBackground = __instance.transform.FindChild("TabBackground");
            var headerImage = __instance.transform.FindChild("HeaderImage");
            var overviewTab = __instance.transform.FindChild("OverviewTab");
            var roleTab = __instance.transform.FindChild("RolesTabs");
            var mainAreaTransform = __instance.transform.FindChild("MainArea");
            var maskBg = __instance.backgroundMask.transform;

            yield return null;

            // #### Resize the lobby view window ####

            background.localPosition = new(-0.619f, -0.5462f, 12f);
            background.localScale = new(1.0141f, 1.6141f, 1.041f);

            backgroundShine.localPosition = new(-0.5876f, -1.58f, -0.1f);
            backgroundShine.localScale = new(1.36f, 2.21f, 1.0141f);

            gameModeTexttransform.localPosition = new(-4.4586f, 3.8241f, -2f);
            gameModeTexttransform.localScale = new(0.9f, 0.9f, 1f);

            divider.localPosition = new(-0.6377f, 3.382f, -0.1f);
            divider.localScale = new(1.07f, 1f, 1f);

            tabBackground.localPosition = new(-0.72f, 2.75f, 2f);
            tabBackground.localScale = new(12.42f, 1.2528f, 1f);

            headerImage.localPosition = new(2.88f, 3.86f, 11.7864f);
            headerImage.localScale = new(0.3653f, 0.3653f, 0.5053f);

            overviewTab.localPosition = new(-5.65f, 3.1f, 0f);
            overviewTab.localScale = new(0.7f, 0.7f, 1f);

            roleTab.localPosition = new(-3.2f, 3.1f, -0f);
            roleTab.localScale = new(0.7f, 0.7f, 1f);

            mainAreaTransform.localPosition = new(0.76f, 0.3f, -1f);
            mainAreaTransform.localScale = new(1.02f, 1f, 1f);

            maskBg.localPosition = new(-1.4794f, -2.0039f, -0.1f);
            maskBg.localScale = new(12.4155f, 7.2925f, 1f);

            yield return null;

            // #### Change value size ####

            var viewSettingsInfoPanel = __instance.infoPanelOrigin;
            
            var titleText = viewSettingsInfoPanel.titleText;
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.fontSizeMin = 1f;
            titleText.transform.localPosition = new(-0.9f, 0f, -2f);
            titleText.margin = new Vector4(-1f, 0.0008f, 0f, 0f);
            titleText.transform.localPosition = new Vector3(0.02f, 0f, -2f);

            var labelBackground = viewSettingsInfoPanel.labelBackground.transform;
            labelBackground.localPosition = new(-0.5325f, 0f, 0);
            labelBackground.localScale = new(1.22f, 1f, 1f);

            var spritePanel = viewSettingsInfoPanel.transform.FindChild("Value")?.transform.FindChild("Sprite");
            spritePanel.localPosition = new(2f, 0f, 0);
            spritePanel.localScale = new(0.35f, 0.6f, 1f);

            var posPanel = viewSettingsInfoPanel.settingText.transform;
            posPanel.localPosition = new(2f, 0f, -2f);
            posPanel.localScale = new(0.7f, 0.7f, 1f);

            var checkMarkOn = viewSettingsInfoPanel.checkMark.transform;
            checkMarkOn.localPosition = new(2f, 0f, -2f);
            checkMarkOn.localScale = new(0.8f, 0.8f, 1f);

            var checkMarkOff = viewSettingsInfoPanel.checkMarkOff.transform;
            checkMarkOff.localPosition = new(2f, 0f, -2f);
            checkMarkOff.localScale = new(0.8f, 0.8f, 1f);
            //viewSettingsInfoPanel.checkMarkOff.color = Color.red;

            yield return null;

            __instance.transform.FindChild("ClickToClose").transform.FindChild("IgnoreClose").localScale = new(1f, 2f, 1f);

            // Start vanilla button positions:
            // taskTabButton  - x: -5.65 - y: 3.1 - z: 0
            // rolesTabButton - x: -3.2  - y: 3.1 - z: 0

            // x: +2.45
            // y: -0.6

            // #### Set colors for vanilla setting tab ####
            __instance.taskTabButton.activeTextColor = __instance.taskTabButton.inactiveTextColor = Color.white;
            __instance.taskTabButton.selectedTextColor = Color.gray;
            __instance.taskTabButton.inactiveSprites.GetComponent<SpriteRenderer>().color = Color.black;
            __instance.taskTabButton.activeSprites.GetComponent<SpriteRenderer>().color = Color.black;
            __instance.taskTabButton.selectedSprites.GetComponent<SpriteRenderer>().color = Color.black;
            __instance.taskTabButton.inactiveSprites.GetComponent<SpriteRenderer>().sprite = Utils.LoadSprite("EHR.Resources.Images.DefaultPlate.png", 135f);
            __instance.taskTabButton.inactiveSprites.transform.FindChild("Shine").gameObject.SetActive(false);
            __instance.taskTabButton.activeSprites.transform.FindChild("Shine").gameObject.SetActive(false);
            __instance.taskTabButton.selectedSprites.transform.FindChild("Shine").gameObject.SetActive(false);
            yield return null;

            // #### Set colors for role tab ####
            __instance.rolesTabButton.activeTextColor = __instance.rolesTabButton.inactiveTextColor = Color.white;
            __instance.rolesTabButton.selectedTextColor = Color.gray;
            //__instance.rolesTabButton.inactiveSprites.GetComponent<SpriteRenderer>().color = Color.black;
            //__instance.rolesTabButton.activeSprites.GetComponent<SpriteRenderer>().color = Color.gray;
            //__instance.rolesTabButton.selectedSprites.GetComponent<SpriteRenderer>().color = Color.black;
            __instance.rolesTabButton.inactiveSprites.GetComponent<SpriteRenderer>().sprite = Utils.LoadSprite("EHR.Resources.Images.DefaultPlate.png", 135f);
            //__instance.rolesTabButton.activeSprites.GetComponent<SpriteRenderer>().sprite = CustomButton.Get("GuessPlate");
            //__instance.rolesTabButton.selectedSprites.GetComponent<SpriteRenderer>().sprite = CustomButton.Get("GuessPlate");
            __instance.rolesTabButton.inactiveSprites.transform.FindChild("Shine").gameObject.SetActive(false);
            __instance.rolesTabButton.activeSprites.transform.FindChild("Shine").gameObject.SetActive(false);
            __instance.rolesTabButton.selectedSprites.transform.FindChild("Shine").gameObject.SetActive(false);
            yield return null;

            // #### Patch info panel role origin ####
            var panelRole = __instance.infoPanelRoleOrigin;
            var panelRoleTransform = panelRole.transform;

            // Role name
            panelRole.titleText.transform.localScale = new(1.5f, 1.5f, 1f);
            panelRole.titleText.transform.localPosition = new(-2.85f, 0f, -2f);
            panelRole.titleText.alignment = TextAlignmentOptions.Left;
            panelRole.titleText.enableWordWrapping = false;
            panelRole.titleText.overflowMode = TextOverflowModes.Overflow;
            panelRole.titleText.fontWeight = FontWeight.Black;
            panelRole.titleText.outlineColor = Color.black;
            panelRole.titleText.outlineWidth = Translator.LangHasSensitiveOutlineText() ? 0.09f : 0.23f;
            panelRole.titleText.color = Color.white;

            // "% Chance" title text
            panelRole.chanceTitle.alignment = TextAlignmentOptions.Left;
            panelRole.chanceTitle.enableWordWrapping = false;
            panelRole.chanceTitle.overflowMode = TextOverflowModes.Overflow;
            panelRole.chanceTitle.fontWeight = FontWeight.Black;
            panelRole.chanceTitle.outlineColor = Color.black;
            panelRole.chanceTitle.outlineWidth = Translator.LangHasSensitiveOutlineText() ? 0.09f : 0.2f;
            panelRole.chanceTitle.color = Color.white;
            panelRole.chanceTitle.transform.localPosition = new(5.15f, -0.02f, -2f);
            panelRole.chanceTitle.transform.localScale = new(1.1f, 1.1f, 1f);

            // Chance value
            panelRole.chanceText.fontWeight = FontWeight.Black;
            panelRole.chanceText.outlineColor = Color.black;
            panelRole.chanceText.outlineWidth = 0.26f;
            panelRole.chanceText.color = Color.white;
            panelRole.transform.FindChild("Chance")?.localPosition = new(2.5f, -0.02f, -1f);

            // Max count sprite
            panelRole.transform.FindChild("Value")?.localPosition = new(0.35f, -0.02f, -1f);

            // Max count value
            panelRole.settingText.fontWeight = FontWeight.Black;
            panelRole.settingText.outlineColor = Color.black;
            panelRole.settingText.outlineWidth = 0.2f;
            panelRole.settingText.color = Color.white;

            yield return null;

            // Patch category header role origin
            var categoryHeaderRoleTitle = __instance.categoryHeaderRoleOrigin.Title;

            categoryHeaderRoleTitle.fontWeight = FontWeight.Black;
            categoryHeaderRoleTitle.outlineColor = Color.white;
            categoryHeaderRoleTitle.outlineWidth = Translator.LangHasSensitiveOutlineText() ? 0.09f : 0.2f;

            yield return null;

            // #### Patch advanced role panel origin ####
            var advancedRolePanelTitle = __instance.advancedRolePanelOrigin.header.Title;
            var advancedRolePanelTitleTransform = advancedRolePanelTitle.transform;

            advancedRolePanelTitle.fontWeight = FontWeight.Black;
            advancedRolePanelTitle.outlineColor = Color.black;
            advancedRolePanelTitle.outlineWidth = Translator.LangHasSensitiveOutlineText() ? 0.067f : 0.2f;
            advancedRolePanelTitleTransform.localScale = new(1.3f, 1.3f, 1f);
            advancedRolePanelTitleTransform.localPosition = new(-2.3f, -0.15f, -1f);
            yield return null;

            __instance.rolesTabButton.transform.localPosition = new(-5.65f, 2.5f, 0);

            TabNames.Clear();
            TabButtons.Clear();
            AllTabButtons.Clear();

            TabButtons[VanillaSettingsTabName] = __instance.taskTabButton;
            TabButtons[VanillaRolesTabName] = __instance.taskTabButton;

            __instance.taskTabButton.buttonText.text = Translator.GetString("TabGroup.VanillaSettings");
            yield return null;

            // Change game mode text & position
            __instance.gameModeText.DestroyTranslator();
            __instance.gameModeText.text = Translator.GetString(Options.CurrentGameMode.ToString());
            __instance.gameModeText.color = Main.GameModeColors[Options.CurrentGameMode];
            __instance.gameModeText.transform.localPosition = new Vector3(-2.75f, 3.88f, -2f);
            LastGameModeSelected = Options.CurrentGameMode;

            // Create right button
            var rightButton = Object.Instantiate(__instance.BackButton, __instance.BackButton.transform.parent).gameObject;
            var rightButtonBoxCollider2D = rightButton.GetComponent<BoxCollider2D>();
            rightButtonBoxCollider2D.size = new Vector2(0.3f, 0.3f);
            rightButtonBoxCollider2D.offset = new Vector2(0f, 0f);

            rightButton.transform.localPosition = new Vector3(-5.5f, 3.85f, -2f);
            rightButton.transform.localScale = new Vector3(3f, 3f, 2f);
            rightButton.name = "RightButtonArrow";
            
            var rightButtonInactiveSprite = rightButton.transform.FindChild("Normal").GetComponentInChildren<SpriteRenderer>();
            rightButtonInactiveSprite.transform.localPosition = new Vector3(0f, 0f, 0.3f);
            rightButtonInactiveSprite.sprite = Utils.LoadSprite("EHR.Resources.Images.InactiveNextButton.png", 100f);

            var rightButtonActiveSprite = rightButton.transform.FindChild("Hover").GetComponentInChildren<SpriteRenderer>();
            rightButtonActiveSprite.transform.localPosition = new Vector3(0f, 0f, 0.3f);
            rightButtonActiveSprite.sprite = Utils.LoadSprite("EHR.Resources.Images.ActiveNextButton.png", 100f);

            var rightPassiveButton = rightButton.gameObject.GetComponent<PassiveButton>();
            rightPassiveButton.OnClick = new();
            rightPassiveButton.OnClick.AddListener((UnityAction)(() =>
            {
                LastGameModeSelected++;
                var enumGameModes = Enum.GetValues<CustomGameMode>().Without(CustomGameMode.All).ToArray();
                if ((int)LastGameModeSelected > enumGameModes.Length)
                {
                    LastGameModeSelected = CustomGameMode.Standard;
                }
                __instance.ChangeTab(VanillaSettingsTabName);

            }));
            yield return null;

            // Create left button
            var leftButton = Object.Instantiate(rightButton, __instance.BackButton.transform.parent).gameObject;
            leftButton.transform.localPosition = new Vector3(-6.4f, 3.85f, -2f);
            leftButton.name = "LeftButtonArrow";
            // flip button
            leftButton.transform.FindChild("Normal").gameObject.GetComponentInChildren<SpriteRenderer>().flipX = true;
            leftButton.transform.FindChild("Hover").gameObject.GetComponentInChildren<SpriteRenderer>().flipX = true;
            
            var leftButtonPassiveButton = leftButton.gameObject.GetComponent<PassiveButton>();
            leftButtonPassiveButton.OnClick = new();
            leftButtonPassiveButton.OnClick.AddListener((UnityAction)(() =>
            {
                LastGameModeSelected--;
                var enumGameModes = Enum.GetValues<CustomGameMode>().Without(CustomGameMode.All).ToArray();
                if ((int)LastGameModeSelected < 0x01)
                {
                    LastGameModeSelected = (CustomGameMode)enumGameModes.Length;
                }
                __instance.ChangeTab(VanillaSettingsTabName);
            }));
            yield return null;

            // #### Add Tab Group ####

            int indexSettings = 1;
            int indexRoles = 0;
            foreach (var tabGroup in Enum.GetValues<TabGroup>())
            {
                Vector3 newXPos;
                var stringName = (StringNames)(5000 + tabGroup);
                TabNames[stringName] = tabGroup;
                Color color = tabGroup.GetTabColor();

                switch (tabGroup)
                {
                    case TabGroup.SystemSettings:
                    case TabGroup.GameSettings:
                    case TabGroup.TaskSettings:
                        var cloneSettingTabButton = Object.Instantiate(__instance.taskTabButton, __instance.taskTabButton.transform.parent);
                        cloneSettingTabButton.buttonText.DestroyTranslator();
                        cloneSettingTabButton.name = tabGroup.ToString();
                        cloneSettingTabButton.buttonText.text = Translator.GetString($"TabGroup.{tabGroup}");

                        newXPos = cloneSettingTabButton.transform.localPosition;
                        newXPos.x += 2.45f * indexSettings;
                        cloneSettingTabButton.transform.localPosition = newXPos;

                        cloneSettingTabButton.activeTextColor = cloneSettingTabButton.inactiveTextColor = Color.white;
                        cloneSettingTabButton.selectedTextColor = new(0.7f, 0.7f, 0.7f);

                        cloneSettingTabButton.inactiveSprites.GetComponent<SpriteRenderer>().color = color;
                        cloneSettingTabButton.activeSprites.GetComponent<SpriteRenderer>().color = color;
                        cloneSettingTabButton.selectedSprites.GetComponent<SpriteRenderer>().color = color;

                        cloneSettingTabButton.OnClick = new();
                        cloneSettingTabButton.OnClick.AddListener((UnityAction)(() =>
                        {
                            __instance.ChangeTab(stringName);
                        }));

                        TabButtons[stringName] = cloneSettingTabButton;
                        AllTabButtons[tabGroup] = cloneSettingTabButton;
                        indexSettings++;
                        break;
                    case TabGroup.ImpostorRoles:
                    case TabGroup.CrewmateRoles:
                    case TabGroup.NeutralRoles:
                    case TabGroup.CovenRoles:
                    case TabGroup.Addons:
                    case TabGroup.OtherRoles:
                        var cloneRoleTabButton = Object.Instantiate(__instance.rolesTabButton, __instance.rolesTabButton.transform.parent);
                        cloneRoleTabButton.buttonText.DestroyTranslator();
                        cloneRoleTabButton.name = tabGroup.ToString();
                        cloneRoleTabButton.buttonText.text = Translator.GetString($"TabGroup.{tabGroup}");

                        if (indexRoles != 0)
                        {
                            newXPos = cloneRoleTabButton.transform.localPosition;
                            newXPos.x += 2.45f * indexRoles;

                            if (tabGroup is TabGroup.Addons)
                                newXPos.y += 0.6f;
                            else
                                indexRoles++;

                            cloneRoleTabButton.transform.localPosition = newXPos;
                        }
                        else indexRoles++;

                        cloneRoleTabButton.activeTextColor = cloneRoleTabButton.inactiveTextColor = Color.white;
                        cloneRoleTabButton.selectedTextColor = new(0.7f, 0.7f, 0.7f);

                        cloneRoleTabButton.inactiveSprites.GetComponent<SpriteRenderer>().color = color;
                        cloneRoleTabButton.activeSprites.GetComponent<SpriteRenderer>().color = color;
                        cloneRoleTabButton.selectedSprites.GetComponent<SpriteRenderer>().color = color;

                        cloneRoleTabButton.OnClick = new();
                        cloneRoleTabButton.OnClick.AddListener((UnityAction)(() =>
                        {
                            __instance.ChangeTab(stringName);
                        }));

                        TabButtons[stringName] = cloneRoleTabButton;
                        AllTabButtons[tabGroup] = cloneRoleTabButton;
                        break;
                }
                yield return null;
            }
            __instance.rolesTabButton.gameObject.SetActive(false); // Hide vanilla role tab

            __instance.scrollBar.ContentXBounds.max = 1f;
            __instance.scrollBar.SetYBoundsMax(-2f);
        }
    }
    [HarmonyPatch(nameof(LobbyViewSettingsPane.OnEnable))]
    [HarmonyPostfix]
    public static void OnEnable_Postfix(LobbyViewSettingsPane __instance)
    {
        LateTask.New(() =>
        {
            __instance.taskTabButton.activeTextColor = __instance.taskTabButton.inactiveTextColor = Color.white;
            __instance.taskTabButton.selectedTextColor = Color.gray;
            __instance.taskTabButton.inactiveSprites.GetComponent<SpriteRenderer>().color = Color.black;
            __instance.taskTabButton.activeSprites.GetComponent<SpriteRenderer>().color = Color.gray;
            __instance.taskTabButton.selectedSprites.GetComponent<SpriteRenderer>().color = Color.black;

            __instance.ChangeTab(LastTabPressed);

            //if (LastTabPressed == VanillaSettingsTabName || LastTabPressed == VanillaRolesTabName)
            //{
            //    foreach (var tabButton in AllTabButtons)
            //    {
            //        var tabGroup = tabButton.Key;
            //        var passiveButton = tabButton.Value;

            //        HideTab(tabGroup, passiveButton);
            //    }
            //}
        }, 0.3f, "ChangeTab", log: false);
    }
    [HarmonyPatch(nameof(LobbyViewSettingsPane.SetTab))]
    [HarmonyPrefix]
    public static bool SetTab_Prefix(LobbyViewSettingsPane __instance)
    {
        if (__instance.currentTab == VanillaRolesTabName)
        {
            __instance.rolesTabButton.SelectButton(true);
            __instance.taskTabButton.SelectButton(false);
            __instance.DrawRolesTab();
            return false;
        }
        if (__instance.currentTab == VanillaSettingsTabName)
        {
            __instance.taskTabButton.SelectButton(true);
            __instance.rolesTabButton.SelectButton(false);
            __instance.DrawNormalTab();
            return false;
        }
        __instance.taskTabButton.SelectButton(false);
        __instance.rolesTabButton.SelectButton(false);
        return false;
    }

    [HarmonyPatch(nameof(LobbyViewSettingsPane.DrawNormalTab))]
    [HarmonyPrefix]
    public static bool DrawNormalTab_Prefix(LobbyViewSettingsPane __instance)
    {
        __instance.taskTabButton.inactiveTextColor = Color.white;
        __instance.taskTabButton.selectedTextColor = Color.gray;
        return __instance.currentTab == VanillaSettingsTabName;
    }

    [HarmonyPatch(nameof(LobbyViewSettingsPane.DrawRolesTab))]
    [HarmonyPrefix]
    public static bool DrawRolesTab_Prefix(LobbyViewSettingsPane __instance)
    {
        __instance.rolesTabButton.inactiveTextColor = Color.white;
        return __instance.currentTab == VanillaRolesTabName;
    }

    private static void ReDrawTab(this LobbyViewSettingsPane viewSettings, StringNames tabName)
    {
        ForReloadTab = true;
        viewSettings.currentTab = tabName;
        for (int i = 0; i < viewSettings.settingsInfo.Count; i++)
        {
            Object.Destroy(viewSettings.settingsInfo[i].gameObject);
        }
        viewSettings.settingsInfo.Clear();
        SetTabPatch_Postfix(viewSettings);
    }
    private static void HideTab(TabGroup tabName, PassiveButton buttonTab)
    {
        var currentGameMode = LastGameModeSelected;
        buttonTab.gameObject.SetActive(true);

        switch (currentGameMode)
        {
            case CustomGameMode.Standard:
                buttonTab.gameObject.SetActive(true);
                break;
            case CustomGameMode.HideAndSeek:
                if (tabName is TabGroup.CovenRoles or TabGroup.Addons or TabGroup.OtherRoles)
                    buttonTab.gameObject.SetActive(false);
                break;
            default:
                if (tabName is TabGroup.ImpostorRoles
                    or TabGroup.CrewmateRoles
                    or TabGroup.NeutralRoles
                    or TabGroup.CovenRoles
                    or TabGroup.Addons
                    or TabGroup.OtherRoles)
                {
                    buttonTab.gameObject.SetActive(false);
                }
                break;
        }
    }

    [HarmonyPatch(nameof(LobbyViewSettingsPane.ChangeTab))]
    [HarmonyPatch(nameof(LobbyViewSettingsPane.RefreshTab))]
    [HarmonyPostfix]
    public static void SetTabPatch_Postfix(LobbyViewSettingsPane __instance)
    {
        LastTabPressed = __instance.currentTab;
        __instance.gameModeText.text = Translator.GetString(LastGameModeSelected.ToString());
        __instance.gameModeText.color = Main.GameModeColors[LastGameModeSelected];

        foreach (var tabButton in AllTabButtons)
        {
            var tabGroup = tabButton.Key;
            var passiveButton = tabButton.Value;

            HideTab(tabGroup, passiveButton);
        }

        if (__instance.currentTab == VanillaSettingsTabName)
        {
            foreach (var buttons in TabButtons.Values)
                buttons.SelectButton(false);

            __instance.taskTabButton.SelectButton(true);
            __instance.scrollBar.SetYBoundsMax(4.2f);
        }
        else if (TabNames.TryGetValue(__instance.currentTab, out var tab)
            && TabButtons.TryGetValue(__instance.currentTab, out var button))
        {
            foreach (var buttons in TabButtons.Values)
                buttons.SelectButton(false);

            button.SelectButton(true);

            switch (tab)
            {
                case TabGroup.SystemSettings:
                case TabGroup.GameSettings:
                case TabGroup.TaskSettings:
                    DrawOptions(__instance, tab);
                    break;
                case TabGroup.ImpostorRoles:
                case TabGroup.CrewmateRoles:
                case TabGroup.NeutralRoles:
                case TabGroup.CovenRoles:
                case TabGroup.Addons:
                case TabGroup.OtherRoles:
                    DrawRoles(__instance, tab);
                    break;
            }
        }
    }
    private static void DrawOptions(LobbyViewSettingsPane viewSettings, TabGroup tabName)
    {
        // I tried using "StartCoroutine()" but it doesn't work normaly
        // Some tabs are empty for about 2 seconds and then start loading
        // But we don't need to use this, the settings are loaded quickly and without lag anyway
        float xPos;
        float yPos = 1.44f;
        bool firstTitle = true;
        int index = 0;
        TextOptionItem header = null;
        ForReloadTab = false;

        foreach (OptionItem option in OptionItem.AllOptions)
        {
            if (option.Tab != tabName) continue;
            BaseGameSetting data = GameOptionsMenuPatch.GetSetting(option);

            bool enabled = !option.IsCurrentlyHidden(forLobbyView: true) && GameOptionsMenuPatch.AllParentsEnabledAndVisible(option.Parent);
            // Title
            if (data == null && option is TextOptionItem toi)
            {
                if (!firstTitle && enabled)
                {
                    yPos -= 0.92f;
                }
                firstTitle = false;
                CategoryHeaderMasked categoryHeaderMasked = Object.Instantiate(viewSettings.categoryHeaderOrigin, Vector3.zero, Quaternion.identity, viewSettings.settingsContainer);
                categoryHeaderMasked.SetHeader(StringNames.Name, 61);
                categoryHeaderMasked.Title.text = option.GetName(disableColor: true).Trim('★', ' ').RemoveHtmlTags();
                categoryHeaderMasked.Title.name = option.Name;
                categoryHeaderMasked.transform.localScale = Vector3.one;
                categoryHeaderMasked.transform.localPosition = new Vector3(-9.77f, yPos, -2f);

                var chmCollider = categoryHeaderMasked.gameObject.AddComponent<BoxCollider2D>();
                chmCollider.size = new Vector2(7, 0.7f);
                chmCollider.offset = new Vector2(1.5f, -0.3f);
                var chmButton = categoryHeaderMasked.gameObject.AddComponent<PassiveButton>();
                chmButton.ClickSound = viewSettings.BackButton.GetComponent<PassiveButton>().ClickSound;
                chmButton.OnMouseOver = new();
                chmButton.OnMouseOut = new();
                chmButton.OnClick = new();
                chmButton.OnClick.AddListener((UnityAction)(() =>
                {
                    toi.CollapsesSection = !toi.CollapsesSection;
                    viewSettings.ReDrawTab(LastTabPressed);
                }));
                chmButton.SetButtonEnableState(true);
                categoryHeaderMasked.gameObject.SetActive(enabled);

                viewSettings.settingsInfo.Add(categoryHeaderMasked.gameObject);
                if (enabled)
                {
                    if (!toi.CollapsesSection) yPos -= 1.05f;
                    else yPos += 0.25f;
                }
                index = 0;
                header = toi;
                continue;
            }
            else if (enabled)
            {
                if (header != null) option.Header = header;

                ViewSettingsInfoPanel viewSettingsInfoPanel = Object.Instantiate(viewSettings.infoPanelOrigin, viewSettings.settingsContainer, true);
                viewSettingsInfoPanel.name = option.Name;
                viewSettingsInfoPanel.transform.localScale = Vector3.one;

                if (firstTitle && index == 0)
                {
                    firstTitle = false;
                    yPos -= 0.70f;
                }

                if (index % 2 == 0)
                {
                    xPos = -8.95f;
                    if (index > 0) yPos -= 0.95f;
                }
                else xPos = -3f;

                viewSettingsInfoPanel.transform.localPosition = new Vector3(xPos, yPos, -2f);
                switch (data.Type)
                {
                    case OptionTypes.Checkbox:
                        viewSettingsInfoPanel.SetInfoCheckbox(data.Title, 61, option.GetBool());
                        break;
                    case OptionTypes.String:
                        viewSettingsInfoPanel.SetInfo(data.Title, option.GetString(), 61);
                        break;
                    case OptionTypes.Float:
                        viewSettingsInfoPanel.SetInfo(data.Title, data.GetValueString(option.GetFloat()), 61);
                        break;
                    case OptionTypes.Int:
                        viewSettingsInfoPanel.SetInfo(data.Title, data.GetValueString(option.GetInt()), 61);
                        break;
                    default:
                        viewSettingsInfoPanel.SetInfo(data.Title, option.GetString(), 61);
                        break;
                }
                viewSettingsInfoPanel.titleText.text = option.GetName();
                viewSettings.settingsInfo.Add(viewSettingsInfoPanel.gameObject);
                index++;
            }
        }
        yPos -= 0.85f;
        viewSettings.scrollBar.SetYBoundsMax(-yPos - 6f);
    }

    private static void DrawRoles(LobbyViewSettingsPane viewSettings, TabGroup tabName)
    {
        float yPos = 1.3f;
        float xPos = -6.53f;
        if (!ForReloadTab) RoleEnabledList.Clear();
        Color roleColorHeaderOrigin = tabName switch
        {
            TabGroup.ImpostorRoles => new(1f, 0f, 0f),
            TabGroup.CrewmateRoles => new(0f, 0f, 1f),
            TabGroup.NeutralRoles => new(1f, 1f, 0f),
            TabGroup.CovenRoles => new(0.79f, 0.192f, 0.541f),
            TabGroup.Addons => new(1f, 0f, 1f),
            TabGroup.OtherRoles => new(0.4f, 0.4f, 0.4f),
            _ => new(0.3f, 0.3f, 0.3f)
        };
        Color roleColorHeaderRole = tabName.GetTabColor();
        TextOptionItem header = null;
        ForReloadTab = false;

        CategoryHeaderMasked categoryHeaderMasked = Object.Instantiate(viewSettings.categoryHeaderOrigin, viewSettings.settingsContainer);
        categoryHeaderMasked.SetHeader(StringNames.RoleQuotaLabel, 61);
        categoryHeaderMasked.Title.text = Translator.GetString($"TabGroup.{tabName}").Trim('★', ' ').RemoveHtmlTags();
        categoryHeaderMasked.Title.fontWeight = FontWeight.Light;
        categoryHeaderMasked.Title.outlineColor = Color.white;
        categoryHeaderMasked.Title.outlineWidth = Translator.LangHasSensitiveOutlineText() ? 0.09f : 0.4f;
        categoryHeaderMasked.Background.color = roleColorHeaderOrigin;
        categoryHeaderMasked.Title.color = Color.white;
        categoryHeaderMasked.transform.localScale = Vector3.one;
        categoryHeaderMasked.transform.localPosition = new Vector3(-9.77f, yPos, -2f);

        viewSettings.settingsInfo.Add(categoryHeaderMasked.gameObject);

        foreach (OptionItem option in OptionItem.AllOptions)
        {
            if (option.Tab != tabName) continue;
            bool enabled = !option.IsCurrentlyHidden(forLobbyView: true) && GameOptionsMenuPatch.AllParentsEnabledAndVisible(option.Parent);
            BaseGameSetting data = GameOptionsMenuPatch.GetSetting(option);
            string titleName = option.GetName(disableColor: true).Trim('★', ' ').RemoveHtmlTags();
            string realName = option.Name;

            // Title
            if (data == null && option is TextOptionItem toi)
            {
                CategoryHeaderRoleVariant categoryHeaderRoleVariant = Object.Instantiate(viewSettings.categoryHeaderRoleOrigin, viewSettings.settingsContainer);
                categoryHeaderRoleVariant.SetHeader((tabName is TabGroup.ImpostorRoles) ? StringNames.ImpostorRolesHeader : StringNames.CrewmateRolesHeader, 61);
                categoryHeaderRoleVariant.name = realName;

                categoryHeaderRoleVariant.Title.color = Color.white;
                categoryHeaderRoleVariant.Background.color = roleColorHeaderRole;
                categoryHeaderRoleVariant.Title.text = titleName;

                if (enabled || toi.CollapsesSection) yPos -= 0.4f;
                categoryHeaderRoleVariant.transform.localScale = Vector3.one;
                categoryHeaderRoleVariant.transform.localPosition = new Vector3(0.09f, yPos, -2f);

                var chmCollider = categoryHeaderRoleVariant.gameObject.AddComponent<BoxCollider2D>();
                chmCollider.size = new Vector2(4, 0.5f);
                chmCollider.offset = new Vector2(-2.1f, -0.2f);
                var chmButton = categoryHeaderRoleVariant.gameObject.AddComponent<PassiveButton>();
                chmButton.ClickSound = viewSettings.BackButton.GetComponent<PassiveButton>().ClickSound;
                chmButton.OnMouseOver = new();
                chmButton.OnMouseOut = new();
                chmButton.OnClick = new();
                chmButton.OnClick.AddListener((UnityAction)(() =>
                {
                    toi.CollapsesSection = !toi.CollapsesSection;
                    viewSettings.ReDrawTab(LastTabPressed);
                }));
                chmButton.SetButtonEnableState(true);
                categoryHeaderRoleVariant.gameObject.SetActive(enabled || toi.CollapsesSection);

                viewSettings.settingsInfo.Add(categoryHeaderRoleVariant.gameObject);
                if (enabled)
                {
                    if (!toi.CollapsesSection) yPos -= 0.65f;
                    else yPos -= 0.1f;
                }
                else if (toi.CollapsesSection) yPos -= 0.65f;
                header = toi;
            }
            // Roles
            if (Enum.GetValues<CustomRoles>().FindFirst(x => x.ToString() == realName, out CustomRoles role))
            {
                try
                {
                    int chancePerGame = Options.CustomRoleSpawnChances.TryGetValue(role, out var valueRoleOpt) ? valueRoleOpt.GetChance() : 0;
                    bool roleDisabled = chancePerGame == 0;

                    if (enabled)
                    {
                        if (role == default || role is CustomRoles.CovenLeader) continue;
                        if (header != null) option.Header = header;

                        ViewSettingsInfoPanelRoleVariant viewSettingsInfoPanelRoleVariant = Object.Instantiate(viewSettings.infoPanelRoleOrigin, viewSettings.settingsContainer);
                        viewSettingsInfoPanelRoleVariant.name = realName;

                        // Max count title
                        var settingTitle = Object.Instantiate(viewSettingsInfoPanelRoleVariant.chanceTitle, viewSettingsInfoPanelRoleVariant.transform);
                        settingTitle.name = "MaxCountTitle";
                        settingTitle.DestroyTranslator();
                        settingTitle.text = Translator.GetString("Maximum");
                        settingTitle.transform.localPosition = new(3.04f, -0.02f, -2f);
                        settingTitle.alignment = TextAlignmentOptions.Left;
                        settingTitle.enableWordWrapping = false;
                        settingTitle.overflowMode = TextOverflowModes.Overflow;

                        // if start pos not changed
                        if (yPos == 1.3f) yPos -= 0.8f;
                        viewSettingsInfoPanelRoleVariant.transform.localScale = Vector3.one;
                        viewSettingsInfoPanelRoleVariant.transform.localPosition = new Vector3(xPos, yPos, -2f);

                        if (role.ToString().Contains("GuardianAngel")) role = CustomRoles.GA;

                        titleName = titleName.RemoveHtmlTags();

                        switch (Options.UsePets.GetBool())
                        {
                            case true when role.PetActivatedAbility():
                                titleName += Translator.GetString("SupportsPetIndicator");
                                break;
                            case false when role.OnlySpawnsWithPets():
                                titleName += Translator.GetString("RequiresPetIndicator");
                                break;
                        }

                        if (role.IsExperimental()) titleName += $"<size=2>{Translator.GetString("ExperimentalRoleIndicator")}</size>";
                        if (role.IsGhostRole()) titleName += StringOptionPatch.GetGhostRoleTeam(role);
                        if (role.IsDevFavoriteRole()) titleName += "  <size=2><#00ffff>★</color></size>";

                        var chanceAddOnPerGame = Options.CustomAdtRoleSpawnRate.TryGetValue(role, out var valueAddOnOpt) ? valueAddOnOpt.GetInt() : 0;
                        int numPerGame = Options.CustomRoleCounts.TryGetValue(role, out var valueInt) ? valueInt.GetInt() : 0;

                        viewSettingsInfoPanelRoleVariant.SetInfo(titleName, numPerGame, chancePerGame, 61, option.NameColor, RoleManager.Instance.AllRoles[0].RoleIconSolid /*<- Role Icons sets here*/, tabName is not TabGroup.ImpostorRoles, roleDisabled);

                        if (roleDisabled)
                        {
                            if (role.IsAdditionRole())
                            {
                                //viewSettingsInfoPanelRoleVariant.chanceText.text = $"{Translator.GetString("RoleOff")}/{chanceAddOnPerGame}";
                                viewSettingsInfoPanelRoleVariant.chanceText.text = Translator.GetString("RoleOff");
                                viewSettingsInfoPanelRoleVariant.chanceText.outlineWidth = Translator.LangHasSensitiveOutlineText() ? 0.09f : 0.26f;
                            }

                            viewSettingsInfoPanelRoleVariant.chanceBackground.color = Palette.DisabledGrey;
                            viewSettingsInfoPanelRoleVariant.background.color = Palette.DisabledGrey;
                        }
                        else
                        {
                            if (role.IsAdditionRole())
                            {
                                viewSettingsInfoPanelRoleVariant.chanceText.text = $"{Translator.GetString("RoleRate")}/{chanceAddOnPerGame}";
                                viewSettingsInfoPanelRoleVariant.chanceText.outlineWidth = Translator.LangHasSensitiveOutlineText() ? 0.09f : 0.26f;
                            }

                            viewSettingsInfoPanelRoleVariant.chanceBackground.color = option.NameColor;
                            viewSettingsInfoPanelRoleVariant.background.color = option.NameColor;
                            RoleEnabledList.Add(role);
                        }
                        viewSettings.settingsInfo.Add(viewSettingsInfoPanelRoleVariant.gameObject);
                        yPos -= 0.65f;
                    }
                    else
                    {
                        if (!roleDisabled) RoleEnabledList.Add(role);
                    }
                }
                catch (Exception e) { Utils.ThrowException(e); }
            }
        }
        if (RoleEnabledList.Count > 0)
        {
            yPos -= 0.8f;
            viewSettings.StopCoroutine(CoShowRoleSettings().WrapToIl2Cpp());
            viewSettings.StartCoroutine(CoShowRoleSettings().WrapToIl2Cpp());
            return;

            IEnumerator CoShowRoleSettings()
            {
                CategoryHeaderMasked categoryHeaderMasked2 = Object.Instantiate(viewSettings.categoryHeaderOrigin, viewSettings.settingsContainer);
                categoryHeaderMasked2.SetHeader(StringNames.RoleSettingsLabel, 61);
                categoryHeaderMasked2.transform.localScale = Vector3.one;
                categoryHeaderMasked2.transform.localPosition = new Vector3(-9.77f, yPos, -2f);
                viewSettings.settingsInfo.Add(categoryHeaderMasked2.gameObject);
                yield return null;

                yPos -= 2.1f;
                float startY = yPos;

                float leftY = startY;
                float rightY = startY;

                float leftX = -5.8f;
                float rightX = 0.15f;

                foreach (var role in RoleEnabledList)
                {
                    // 1 child setting is "Max" or "Spawn chance" setting
                    if (!Options.CustomRoleSpawnChances.TryGetValue(role, out var optionRole) || optionRole.Children.Count <= 1) continue;
                    yield return null;

                    bool useLeftColumn = leftY >= rightY;
                    float columnX = useLeftColumn ? leftX : rightX;
                    float columnY = useLeftColumn ? leftY : rightY;

                    float endY = SetUpCustomRoleSettings(viewSettings, role, optionRole, tabName, 0.8f, 61, columnX, columnY);
                    yield return null;

                    if (useLeftColumn) leftY = endY - 1.2f;
                    else rightY = endY - 1.2f;

                    float currectLowestY = Mathf.Min(leftY, rightY);
                    viewSettings.scrollBar.SetYBoundsMax(-currectLowestY - 6f);
                    yield return null;
                }
                float endLowestY = Mathf.Min(leftY, rightY);
                viewSettings.scrollBar.SetYBoundsMax(-endLowestY - 6f);
            }
        }
        viewSettings.scrollBar.SetYBoundsMax(-yPos - 4f);
    }
    private static float SetUpCustomRoleSettings(this LobbyViewSettingsPane viewSettings, CustomRoles role, StringOptionItem optionRole, TabGroup tabName, float spacingY, int maskLayer, float xPosRoleHeader, float startY)
    {
        float yPos = startY;
        AdvancedRoleViewPanel advancedRoleViewPanel = Object.Instantiate(viewSettings.advancedRolePanelOrigin, viewSettings.settingsContainer);
        advancedRoleViewPanel.name = role + "AdvancedPanel";
        advancedRoleViewPanel.transform.localScale = Vector3.one;
        advancedRoleViewPanel.transform.localPosition = new Vector3(xPosRoleHeader, yPos, -2f);
        advancedRoleViewPanel.header.SetHeader((StringNames)(6000 + role), maskLayer, tabName is not TabGroup.ImpostorRoles, null /*<- Role Icons sets here*/);
        advancedRoleViewPanel.divider.material.SetInt(PlayerMaterial.MaskLayer, maskLayer);
        advancedRoleViewPanel.header.Title.text = Translator.GetString(role.ToString());
        advancedRoleViewPanel.header.Title.color = Color.white;
        advancedRoleViewPanel.header.Background.color = optionRole.NameColor;
        advancedRoleViewPanel.header.Divider.color = optionRole.NameColor;
        viewSettings.settingsInfo.Add(advancedRoleViewPanel.gameObject);

        for (int parentsIndex = 0; parentsIndex < optionRole.Children.Count; parentsIndex++)
        {
            OptionItem option = optionRole.Children[parentsIndex];
            bool show = option.Name != "Maximum";
            if (!show) continue;

            BaseGameSetting data = GameOptionsMenuPatch.GetSetting(option);
            if (data == null) continue;

            ViewSettingsInfoPanel viewSettingsInfoPanel = Object.Instantiate(advancedRoleViewPanel.infoPanelOrigin, advancedRoleViewPanel.transform.parent, true);
            viewSettingsInfoPanel.name = option.Name;
            viewSettingsInfoPanel.transform.localScale = Vector3.one;
            viewSettingsInfoPanel.transform.localPosition = new Vector3(xPosRoleHeader - 3f, yPos, -2f);

            switch (data.Type)
            {
                case OptionTypes.Checkbox:
                    viewSettingsInfoPanel.SetInfoCheckbox(data.Title, 61, option.GetBool());
                    break;
                case OptionTypes.String:
                    viewSettingsInfoPanel.SetInfo(data.Title, option.GetString(), 61);
                    break;
                case OptionTypes.Float:
                    viewSettingsInfoPanel.SetInfo(data.Title, data.GetValueString(option.GetFloat()), 61);
                    break;
                case OptionTypes.Int:
                    viewSettingsInfoPanel.SetInfo(data.Title, data.GetValueString(option.GetInt()), 61);
                    break;
                default:
                    viewSettingsInfoPanel.SetInfo(data.Title, option.GetString(), 61);
                    break;
            }
            viewSettingsInfoPanel.titleText.text = option.GetName();
            viewSettings.settingsInfo.Add(viewSettingsInfoPanel.gameObject);
            yPos -= spacingY;
        }
        return yPos;
    }
}
