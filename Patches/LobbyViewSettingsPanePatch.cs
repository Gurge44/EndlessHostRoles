using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace EHR.Patches;

[HarmonyPatch(typeof(LobbyViewSettingsPane))]
public static class LobbyViewPanePatches
{
    private static StringNames VanillaSettingsTabName => StringNames.OverviewCategory;
    private static StringNames RolesTabName => StringNames.RolesCategory;
    //private static StringNames ModSettingsTabName { get; } = StringNames.AgeVerificationMoreInfo; // Random StringName lol

    private static readonly Dictionary<StringNames, PassiveButton> ModTabButtons = [];
    private static readonly Dictionary<StringNames, TabGroup> TabNames = [];

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

            var viewSettingsInfoPanel = __instance.infoPanelOrigin;
            
            var titleText = viewSettingsInfoPanel.titleText;
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.fontSizeMin = 1f;
            titleText.transform.localPosition = new(-0.9f, 0f, -2f);

            var spritePanel = viewSettingsInfoPanel.transform.FindChild("Value")?.transform.FindChild("Sprite");
            spritePanel.localPosition = new(2f, 0f, 0);
            spritePanel.localScale = new(0.35f, 0.6f, 1f);

            var posPanel = viewSettingsInfoPanel.settingText.transform;
            posPanel.localPosition = new(2f, 0f, -2f);
            posPanel.localScale = new(0.8f, 0.8f, 1f);

            var checkMarkOn = viewSettingsInfoPanel.checkMark.transform;
            checkMarkOn.localPosition = new(2f, 0f, -2f);
            checkMarkOn.localScale = new(0.8f, 0.8f, 1f);

            var checkMarkOff = viewSettingsInfoPanel.checkMarkOff.transform;
            checkMarkOff.localPosition = new(2f, 0f, -2f);
            checkMarkOff.localScale = new(0.8f, 0.8f, 1f);

            yield return null;

            // Disabled closing a window when clicking outside the window
            //__instance.transform.FindChild("ClickToClose").gameObject.SetActive(false);

            yield return null;

            // Start positions:
            // taskTabButton  - x: -5.65 - y: 3.1 - z: 0
            // rolesTabButton - x: -3.2  - y: 3.1 - z: 0

            // x: +2.45
            // y: -0.6

            __instance.taskTabButton.activeTextColor = __instance.taskTabButton.inactiveTextColor = Color.white;
            __instance.taskTabButton.selectedTextColor = Color.gray;
            __instance.taskTabButton.inactiveSprites.GetComponent<SpriteRenderer>().color = Color.black;
            __instance.taskTabButton.activeSprites.GetComponent<SpriteRenderer>().color = Color.gray;
            __instance.taskTabButton.selectedSprites.GetComponent<SpriteRenderer>().color = Color.black;
            __instance.taskTabButton.inactiveSprites.transform.FindChild("Shine").gameObject.SetActive(false);
            __instance.taskTabButton.activeSprites.transform.FindChild("Shine").gameObject.SetActive(false);
            __instance.taskTabButton.selectedSprites.transform.FindChild("Shine").gameObject.SetActive(false);
            yield return null;

            //__instance.rolesTabButton.activeTextColor = __instance.rolesTabButton.inactiveTextColor = Color.white;
            //__instance.rolesTabButton.selectedTextColor = Color.gray;
            //__instance.rolesTabButton.inactiveSprites.GetComponent<SpriteRenderer>().color = Color.black;
            //__instance.rolesTabButton.activeSprites.GetComponent<SpriteRenderer>().color = Color.gray;
            //__instance.rolesTabButton.selectedSprites.GetComponent<SpriteRenderer>().color = Color.black;
            //__instance.rolesTabButton.inactiveSprites.transform.FindChild("Shine").gameObject.SetActive(false);
            //__instance.rolesTabButton.activeSprites.transform.FindChild("Shine").gameObject.SetActive(false);
            //__instance.rolesTabButton.selectedSprites.transform.FindChild("Shine").gameObject.SetActive(false);

            __instance.rolesTabButton.transform.localPosition = new(-5.65f, 2.5f, 0);
            //yield return null;

            ModTabButtons.Add(VanillaSettingsTabName, __instance.taskTabButton);
            ModTabButtons.Add(RolesTabName, __instance.rolesTabButton);

            __instance.gameModeText.DestroyTranslator();
            __instance.gameModeText.text = Translator.GetString(Options.CurrentGameMode.ToString());
            __instance.taskTabButton.buttonText.text = Translator.GetString("TabGroup.VanillaSettings");

            int index = 1;
            foreach (var tabGroup in Enum.GetValues<TabGroup>())
            {
                Color color = tabGroup switch
                {
                    TabGroup.SystemSettings => new(0.2f, 0.2f, 0.2f),
                    TabGroup.GameSettings => new(0.2f, 0.4f, 0.3f),
                    TabGroup.TaskSettings => new(0.4f, 0.2f, 0.5f),
                    TabGroup.ImpostorRoles => new(0.5f, 0.2f, 0.2f),
                    TabGroup.CrewmateRoles => new(0.2f, 0.4f, 0.5f),
                    TabGroup.NeutralRoles => new(0.5f, 0.4f, 0.2f),
                    TabGroup.CovenRoles => new(0.5f, 0.2f, 0.4f),
                    TabGroup.Addons => new(0.4f, 0.2f, 0.3f),
                    TabGroup.OtherRoles => new(0.4f, 0.4f, 0.4f),
                    _ => new(0.3f, 0.3f, 0.3f)
                };
                switch (tabGroup)
                {
                    case TabGroup.SystemSettings:
                    case TabGroup.GameSettings:
                    case TabGroup.TaskSettings:
                        var cloneTabButton = Object.Instantiate(__instance.taskTabButton, __instance.taskTabButton.transform.parent);
                        cloneTabButton.buttonText.DestroyTranslator();
                        cloneTabButton.name = tabGroup.ToString();
                        cloneTabButton.buttonText.text = Translator.GetString($"TabGroup.{tabGroup}");

                        Vector3 newXTabPos = cloneTabButton.transform.localPosition;
                        newXTabPos.x += 2.45f * index;
                        cloneTabButton.transform.localPosition = newXTabPos;

                        cloneTabButton.activeTextColor = /*cloneTabButton.inactiveTextColor = */ Color.white;
                        //LateTask.New(() => { cloneTabButton?.inactiveTextColor = Color.white; }, 2f, "SetInactiveTextColor", log: false);
                        cloneTabButton.selectedTextColor = new(0.7f, 0.7f, 0.7f);

                        cloneTabButton.inactiveSprites.GetComponent<SpriteRenderer>().color = color;
                        cloneTabButton.activeSprites.GetComponent<SpriteRenderer>().color = color;
                        cloneTabButton.selectedSprites.GetComponent<SpriteRenderer>().color = color;

                        cloneTabButton.inactiveSprites.transform.FindChild("Shine").gameObject.SetActive(false);
                        cloneTabButton.activeSprites.transform.FindChild("Shine").gameObject.SetActive(false);
                        cloneTabButton.selectedSprites.transform.FindChild("Shine").gameObject.SetActive(false);

                        var stringName = (StringNames)(5000 + tabGroup);
                        cloneTabButton.OnClick = new();
                        cloneTabButton.OnClick.AddListener((UnityAction)(() =>
                        {
                            __instance.ChangeTab(stringName);
                        }));

                        ModTabButtons[stringName] = cloneTabButton;
                        TabNames[stringName] = tabGroup;
                        break;
                    case TabGroup.ImpostorRoles:
                    case TabGroup.CrewmateRoles:
                    case TabGroup.NeutralRoles:
                    case TabGroup.CovenRoles:
                    case TabGroup.Addons:
                    case TabGroup.OtherRoles:
                        break;
                }

                index++;
                yield return null;
            }
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

            __instance.ChangeTab(StringNames.OverviewCategory);
        }, 0.01f, "ChangeTab", log: false);
    }
    [HarmonyPatch(nameof(LobbyViewSettingsPane.SetTab))]
    [HarmonyPrefix]
    public static bool SetTab_Prefix(LobbyViewSettingsPane __instance)
    {
        if (__instance.currentTab == StringNames.RolesCategory)
        {
            __instance.rolesTabButton.SelectButton(true);
            __instance.taskTabButton.SelectButton(false);
            __instance.DrawRolesTab();
            return false;
        }
        if (__instance.currentTab == StringNames.OverviewCategory)
        {
            __instance.taskTabButton.SelectButton(true);
            __instance.rolesTabButton.SelectButton(false);
            __instance.DrawNormalTab();
            __instance.scrollBar.SetYBoundsMax(-2f);
            return false;
        }
        __instance.taskTabButton.SelectButton(false);
        __instance.rolesTabButton.SelectButton(false);
        return false;
    }

    [HarmonyPatch(nameof(LobbyViewSettingsPane.DrawNormalTab))]
    [HarmonyPrefix]
    public static bool DrawNormalTabPatch(LobbyViewSettingsPane __instance)
    {
        __instance.taskTabButton.inactiveTextColor = Color.white;
        return __instance.currentTab == VanillaSettingsTabName;
    }

    //[HarmonyPatch(nameof(LobbyViewSettingsPane.DrawRolesTab))]
    //[HarmonyPrefix]
    public static bool DrawRolesTabPatch(LobbyViewSettingsPane __instance)
    {
        if (__instance.currentTab != RolesTabName) return false;


        return true;
    }

    private static bool IsLoading = false;
    [HarmonyPatch(nameof(LobbyViewSettingsPane.ChangeTab))]
    [HarmonyPatch(nameof(LobbyViewSettingsPane.RefreshTab))]
    [HarmonyPostfix]
    public static void SetTabPatch_Postfix(LobbyViewSettingsPane __instance)
    {
        // Prevent double loading
        if (IsLoading) return;
        if (TabNames.ContainsKey(__instance.currentTab) && ModTabButtons.ContainsKey(__instance.currentTab))
        {
            IsLoading = true;
            var tab = TabNames[__instance.currentTab];
            var button = ModTabButtons[__instance.currentTab];

            foreach (var buttons in ModTabButtons.Values)
            {
                //buttons.inactiveTextColor = Color.white;
                buttons.SelectButton(false);
            }

            button.SelectButton(true);
            DrawOptions(__instance, tab);
        }
        else
            foreach (var tab in ModTabButtons.Values)
            {
                //tab.inactiveTextColor = Color.white;
                tab.SelectButton(false);
            }
    }

    private static void DrawOptions(LobbyViewSettingsPane menu, TabGroup tabName)
    {
        // I tried using "StartCoroutine" but it doesn't work normaly
        // Some tabs are blank for about 2 seconds and then start loading

        float xPos;
        float yPos = 1.44f;
        bool firstTitle = true;
        int index = 0;
        foreach (OptionItem option in OptionItem.AllOptions)
        {
            if (option.Tab != tabName) continue;
            BaseGameSetting data = GameOptionsMenuPatch.GetSetting(option);

            bool enable = !option.IsCurrentlyHidden() && AllParentsEnabledAndVisible(option.Parent);
            // Title
            if (enable && data == null && option is TextOptionItem)
            {
                if (!firstTitle) yPos -= 1.44f;
                firstTitle = false;
                CategoryHeaderMasked categoryHeaderMasked = Object.Instantiate(menu.categoryHeaderOrigin, menu.settingsContainer, true);
                categoryHeaderMasked.SetHeader(StringNames.Name, 61);
                categoryHeaderMasked.Title.text = option.GetName(disableColor: true).Trim('★', ' ');
                categoryHeaderMasked.Title.name = option.Name;
                categoryHeaderMasked.transform.localScale = Vector3.one;
                categoryHeaderMasked.transform.localPosition = new Vector3(-9.77f, yPos, -2f);
                menu.settingsInfo.Add(categoryHeaderMasked.gameObject);
                yPos -= 1.05f;
                index = 0;
                continue;
            }
            else if (enable)
            {
                ViewSettingsInfoPanel viewSettingsInfoPanel = Object.Instantiate(menu.infoPanelOrigin, menu.settingsContainer, true);
                viewSettingsInfoPanel.name = option.Name;
                viewSettingsInfoPanel.transform.localScale = Vector3.one;

                var labelBackground = viewSettingsInfoPanel.labelBackground.transform;
                labelBackground.localPosition = new(-0.5325f, 0f, 0);
                labelBackground.localScale = new(1.22f, 1f, 1f);

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
                menu.settingsInfo.Add(viewSettingsInfoPanel.gameObject);
                index++;
            }
        }
        yPos -= 0.85f;
        menu.scrollBar.SetYBoundsMax(-yPos - 6f);
        IsLoading = false;
    }
    private static bool AllParentsEnabledAndVisible(OptionItem o)
    {
        while (true)
        {
            if (o == null) return true;
            if (o.IsCurrentlyHidden() || !o.GetBool()) return false;
            o = o.Parent;
        }
    }
}
