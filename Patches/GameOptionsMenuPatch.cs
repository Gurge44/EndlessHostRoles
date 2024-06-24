using AmongUs.GameOptions;
using UnityEngine;

namespace EHR;

/*
[HarmonyPatch(typeof(GameSettingMenu))]
public class GameSettingMenuPatch
{
    private static readonly Vector3 ButtonPositionLeft = new(-3.9f, -0.4f, 0f);
    private static readonly Vector3 ButtonPositionRight = new(-2.4f, -0.4f, 0f);
    private static readonly Vector3 ButtonSize = new(0.45f, 0.6f, 1f);

    private static GameOptionsMenu TemplateGameOptionsMenu;
    private static PassiveButton TemplateGameSettingsButton;

    static Dictionary<TabGroup, PassiveButton> ModSettingsButtons = new();
    static Dictionary<TabGroup, GameOptionsMenu> ModSettingsTabs = new();

    [HarmonyPatch(nameof(GameSettingMenu.Start)), HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void StartPostfix(GameSettingMenu __instance)
    {
        ModSettingsButtons = new();
        foreach (var tab in Enum.GetValues<TabGroup>())
        {
            var button = Object.Instantiate(TemplateGameSettingsButton, __instance.GameSettingsButton.transform.parent);
            button.gameObject.SetActive(true);
            button.name = "Button_" + tab;
            var label = button.GetComponentInChildren<TextMeshPro>();
            label.DestroyTranslator();
            label.text = "";
            button.activeTextColor = button.inactiveTextColor = Color.black;
            button.selectedTextColor = Color.blue;

            var activeButton = Utils.LoadSprite($"EHR.Resources.Tab_Active_{tab}.png", 100f);
            button.inactiveSprites.GetComponent<SpriteRenderer>().sprite = Utils.LoadSprite($"EHR.Resources.Tab_Small_{tab}.png", 100f);
            button.activeSprites.GetComponent<SpriteRenderer>().sprite = activeButton;
            button.selectedSprites.GetComponent<SpriteRenderer>().sprite = activeButton;

            Vector3 offset = new (0.0f, 0.5f * (((int)tab + 1) / 2), 0.0f);
            button.transform.localPosition = ((((int)tab + 1) % 2 == 0) ? ButtonPositionLeft : ButtonPositionRight) - offset;
            button.transform.localScale = ButtonSize;

            var buttonComponent = button.GetComponent<PassiveButton>();
            buttonComponent.OnClick = new();
            buttonComponent.OnClick.AddListener((Action)(() => __instance.ChangeTab((int)tab + 3, false)));

            ModSettingsButtons.Add(tab, button);
        }

        ModGameOptionsMenu.OptionList = new();
        ModGameOptionsMenu.BehaviourList = new();
        ModGameOptionsMenu.CategoryHeaderList = new();

        ModSettingsTabs = new();
        foreach (var tab in Enum.GetValues<TabGroup>())
        {
            var setTab = Object.Instantiate(TemplateGameOptionsMenu, __instance.GameSettingsTab.transform.parent);
            setTab.name = (tab + 3).ToString();
            setTab.gameObject.SetActive(false);

            ModSettingsTabs.Add(tab, setTab);
        }

        foreach (var tab in Enum.GetValues<TabGroup>())
        {
            if (ModSettingsButtons.TryGetValue(tab, out var button))
            {
                __instance.ControllerSelectable.Add(button);
            }
        }
    }
    private static void SetDefaultButton(GameSettingMenu __instance)
    {
        __instance.GamePresetsButton.gameObject.SetActive(false);

        var gameSettingButton = __instance.GameSettingsButton;
        gameSettingButton.transform.localPosition = new(-3f, -0.5f, 0f);
        var textLabel = gameSettingButton.GetComponentInChildren<TextMeshPro>();
        textLabel.DestroyTranslator();
        textLabel.text = "";
        gameSettingButton.activeTextColor = gameSettingButton.inactiveTextColor = Color.black;
        gameSettingButton.selectedTextColor = Color.blue;

        var vanillaActiveButton = Utils.LoadSprite("EHR.Resources.Tab_Active_VanillaGameSettings.png", 100f);
        gameSettingButton.inactiveSprites.GetComponent<SpriteRenderer>().sprite = Utils.LoadSprite("EHR.Resources.Tab_Small_VanillaGameSettings.png", 100f);
        gameSettingButton.activeSprites.GetComponent<SpriteRenderer>().sprite = vanillaActiveButton;
        gameSettingButton.selectedSprites.GetComponent<SpriteRenderer>().sprite = vanillaActiveButton;
        gameSettingButton.transform.localPosition = ButtonPositionLeft;
        gameSettingButton.transform.localScale = ButtonSize;

        __instance.RoleSettingsButton.gameObject.SetActive(false);

        __instance.DefaultButtonSelected = gameSettingButton;
        __instance.ControllerSelectable = new();
        __instance.ControllerSelectable.Add(gameSettingButton);
    }

    [HarmonyPatch(nameof(GameSettingMenu.ChangeTab)), HarmonyPrefix]
    public static bool ChangeTabPrefix(GameSettingMenu __instance, ref int tabNum, [HarmonyArgument(1)] bool previewOnly)
    {
        ModGameOptionsMenu.TabIndex = tabNum;

        GameOptionsMenu settingsTab;
        PassiveButton button;

        if ((previewOnly && Controller.currentTouchType == Controller.TouchType.Joystick) || !previewOnly)
        {
            foreach (var tab in Enum.GetValues<TabGroup>())
            {
                if (ModSettingsTabs.TryGetValue(tab, out settingsTab) && settingsTab != null)
                {
                    settingsTab.gameObject.SetActive(false);
                }
            }
            foreach (var tab in Enum.GetValues<TabGroup>())
            {
                if (ModSettingsButtons.TryGetValue(tab, out button) && button != null)
                {
                    button.SelectButton(false);
                }
            }
        }

        if (tabNum < 3) return true;

        if ((previewOnly && Controller.currentTouchType == Controller.TouchType.Joystick) || !previewOnly)
        {
            __instance.PresetsTab.gameObject.SetActive(false);
            __instance.GameSettingsTab.gameObject.SetActive(false);
            __instance.RoleSettingsTab.gameObject.SetActive(false);
            __instance.GamePresetsButton.SelectButton(false);
            __instance.GameSettingsButton.SelectButton(false);
            __instance.RoleSettingsButton.SelectButton(false);

            if (ModSettingsTabs.TryGetValue((TabGroup)(tabNum - 3), out settingsTab) &&
                settingsTab != null)
            {
                settingsTab.gameObject.SetActive(true);
                __instance.MenuDescriptionText.DestroyTranslator();
                __instance.MenuDescriptionText.text = GetString($"TabGroup.{(TabGroup)(tabNum - 3)}");
            }
        }
        if (previewOnly)
        {
            __instance.ToggleLeftSideDarkener(false);
            __instance.ToggleRightSideDarkener(true);
            return false;
        }
        __instance.ToggleLeftSideDarkener(true);
        __instance.ToggleRightSideDarkener(false);

        if (ModSettingsButtons.TryGetValue((TabGroup)(tabNum - 3), out button) && button != null)
        {
            button.SelectButton(true);
        }

        return false;
    }

    [HarmonyPatch(nameof(GameSettingMenu.OnEnable)), HarmonyPrefix]
    private static bool OnEnablePrefix(GameSettingMenu __instance)
    {
        if (TemplateGameOptionsMenu == null)
        {
            TemplateGameOptionsMenu = Object.Instantiate(__instance.GameSettingsTab, __instance.GameSettingsTab.transform.parent);
            TemplateGameOptionsMenu.gameObject.SetActive(false);
        }
        if (TemplateGameSettingsButton == null)
        {
            TemplateGameSettingsButton = Object.Instantiate(__instance.GameSettingsButton, __instance.GameSettingsButton.transform.parent);
            TemplateGameSettingsButton.gameObject.SetActive(false);
        }

        SetDefaultButton(__instance);

        ControllerManager.Instance.OpenOverlayMenu(__instance.name, __instance.BackButton, __instance.DefaultButtonSelected, __instance.ControllerSelectable);
        DestroyableSingleton<HudManager>.Instance.menuNavigationPrompts.SetActive(false);
        if (Controller.currentTouchType != Controller.TouchType.Joystick)
        {
            __instance.ChangeTab(1, Controller.currentTouchType == Controller.TouchType.Joystick);
        }
        __instance.StartCoroutine(__instance.CoSelectDefault());

        return false;
    }
    [HarmonyPatch(nameof(GameSettingMenu.Close)), HarmonyPostfix]
    private static void ClosePostfix(GameSettingMenu __instance)
    {
        foreach (var button in ModSettingsButtons.Values)
            Object.Destroy(button);
        foreach (var tab in ModSettingsTabs.Values)
            Object.Destroy(tab);
        ModSettingsButtons = new();
        ModSettingsTabs = new();
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSyncSettings))]
public class RpcSyncSettingsPatch
{
    public static void Postfix()
    {
        OptionItem.SyncAllOptions();
    }
}

public static class ModGameOptionsMenu
{
    public static int TabIndex;
    public static Dictionary<OptionBehaviour, int> OptionList = new();
    public static Dictionary<int, OptionBehaviour> BehaviourList = new();
    public static Dictionary<int, CategoryHeaderMasked> CategoryHeaderList = new();
}
[HarmonyPatch(typeof(GameOptionsMenu))]
public static class GameOptionsMenuPatch
{
    [HarmonyPatch(nameof(GameOptionsMenu.Initialize)), HarmonyPrefix]
    private static bool InitializePrefix(GameOptionsMenu __instance)
    {
        if (ModGameOptionsMenu.TabIndex < 3) return true;

        if (__instance.Children == null || __instance.Children.Count == 0)
        {
            __instance.MapPicker.gameObject.SetActive(false);
            __instance.Children = new();
            __instance.CreateSettings();
            __instance.cachedData = GameOptionsManager.Instance.CurrentGameOptions;
            for (int i = 0; i < __instance.Children.Count; i++)
            {
                OptionBehaviour optionBehaviour = __instance.Children[i];
                optionBehaviour.OnValueChanged = new Action<OptionBehaviour>(__instance.ValueChanged);
            }
            __instance.InitializeControllerNavigation();
        }

        return false;
    }
    [HarmonyPatch(nameof(GameOptionsMenu.CreateSettings)), HarmonyPrefix]
    private static bool CreateSettingsPrefix(GameOptionsMenu __instance)
    {
        if (ModGameOptionsMenu.TabIndex < 3) return true;
        var modTab = (TabGroup)(ModGameOptionsMenu.TabIndex - 3);

        float num = 2.0f;
        const float posX = 0.952f;
        const float posZ = -2.0f;
        for (int index = 0; index < OptionItem.AllOptions.Count; index++)
        {
            var option = OptionItem.AllOptions[index];
            if (option.Tab != modTab) continue;

            var enabled = !option.IsHiddenOn(Options.CurrentGameMode) && (option.Parent == null || (!option.Parent.IsHiddenOn(Options.CurrentGameMode) && option.Parent.GetBool()));

            if (option is TextOptionItem)
            {
                CategoryHeaderMasked categoryHeaderMasked = Object.Instantiate(__instance.categoryHeaderOrigin, Vector3.zero, Quaternion.identity, __instance.settingsContainer);
                categoryHeaderMasked.SetHeader(StringNames.RolesCategory, 20);
                categoryHeaderMasked.Title.text = option.GetName();
                categoryHeaderMasked.transform.localScale = Vector3.one * 0.63f;
                categoryHeaderMasked.transform.localPosition = new(-0.903f, num, posZ);
                var chmText = categoryHeaderMasked.transform.FindChild("HeaderText").GetComponent<TextMeshPro>();
                chmText.fontStyle = FontStyles.Bold;
                chmText.outlineWidth = 0.17f;
                categoryHeaderMasked.gameObject.SetActive(enabled);
                ModGameOptionsMenu.CategoryHeaderList.TryAdd(index, categoryHeaderMasked);

                if (enabled) num -= 0.63f;
            }
            if (option is TextOptionItem) continue;

            var baseGameSetting = GetSetting(option);
            if (baseGameSetting == null) continue;


            OptionBehaviour optionBehaviour;

            switch (baseGameSetting.Type)
            {
                case OptionTypes.Checkbox:
                    {
                        optionBehaviour = Object.Instantiate(__instance.checkboxOrigin, Vector3.zero, Quaternion.identity, __instance.settingsContainer);
                        optionBehaviour.transform.localPosition = new(posX, num, posZ);

                        OptionBehaviourSetSizeAndPosition(optionBehaviour, option, baseGameSetting.Type);

                        optionBehaviour.SetClickMask(__instance.ButtonClickMask);
                        optionBehaviour.SetUpFromData(baseGameSetting, 20);
                        optionBehaviour.transform.FindChild("Title Text").GetComponent<TextMeshPro>().text = option.GetName();
                        ModGameOptionsMenu.OptionList.TryAdd(optionBehaviour, index);
                        break;
                    }
                case OptionTypes.String:
                    {
                        optionBehaviour = Object.Instantiate(__instance.stringOptionOrigin, Vector3.zero, Quaternion.identity, __instance.settingsContainer);
                        optionBehaviour.transform.localPosition = new(posX, num, posZ);

                        OptionBehaviourSetSizeAndPosition(optionBehaviour, option, baseGameSetting.Type);

                        optionBehaviour.SetClickMask(__instance.ButtonClickMask);
                        optionBehaviour.SetUpFromData(baseGameSetting, 20);
                        optionBehaviour.transform.FindChild("Title Text").GetComponent<TextMeshPro>().text = option.GetName();
                        ModGameOptionsMenu.OptionList.TryAdd(optionBehaviour, index);
                        break;
                    }
                case OptionTypes.Float:
                case OptionTypes.Int:
                    {
                        optionBehaviour = Object.Instantiate(__instance.numberOptionOrigin, Vector3.zero, Quaternion.identity, __instance.settingsContainer);
                        optionBehaviour.transform.localPosition = new(posX, num, posZ);

                        OptionBehaviourSetSizeAndPosition(optionBehaviour, option, baseGameSetting.Type);

                        optionBehaviour.SetClickMask(__instance.ButtonClickMask);
                        optionBehaviour.SetUpFromData(baseGameSetting, 20);
                        optionBehaviour.transform.FindChild("Title Text").GetComponent<TextMeshPro>().text = option.GetName();
                        ModGameOptionsMenu.OptionList.TryAdd(optionBehaviour, index);
                        break;
                    }
                default:
                    continue;

            }
            optionBehaviour.transform.localPosition = new(0.952f, num, -2f);
            optionBehaviour.SetClickMask(__instance.ButtonClickMask);
            optionBehaviour.SetUpFromData(baseGameSetting, 20);
            optionBehaviour.transform.FindChild("Title Text").GetComponent<TextMeshPro>().text = option.GetName();
            ModGameOptionsMenu.OptionList.TryAdd(optionBehaviour, index);
            ModGameOptionsMenu.BehaviourList.TryAdd(index, optionBehaviour);
            optionBehaviour.gameObject.SetActive(enabled);
            __instance.Children.Add(optionBehaviour);

            if (enabled) num -= 0.45f;
        }

        __instance.ControllerSelectable.Clear();
        foreach (var x in __instance.scrollBar.GetComponentsInChildren<UiElement>())
            __instance.ControllerSelectable.Add(x);
        __instance.scrollBar.SetYBoundsMax(-num - 1.65f);

        return false;
    }
    private static void OptionBehaviourSetSizeAndPosition(OptionBehaviour optionBehaviour, OptionItem option, OptionTypes type)
    {
        var labelBackground = optionBehaviour.transform.FindChild("LabelBackground");
        labelBackground.GetComponent<SpriteRenderer>().sprite = Utils.LoadSprite("EHR.Resources.SettingMenu_LabelBackground.png", 100f);

        Vector3 positionOffset = new(0f, 0f, 0f);
        Vector3 scaleOffset = new(0f, 0f, 0f);
        Color color = new(0.7f, 0.7f, 0.7f);
        float sizeDelta_x = 5.7f;

        if (option.Parent?.Parent?.Parent != null)
        {
            scaleOffset = new(-0.18f, 0, 0);
            positionOffset = new(0.3f, 0f, 0f);
            color = new(0.7f, 0.5f, 0.5f);
            sizeDelta_x = 5.1f;
        }
        else if (option.Parent?.Parent != null)
        {
            scaleOffset = new(-0.12f, 0, 0);
            positionOffset = new(0.2f, 0f, 0f);
            color = new(0.5f, 0.5f, 0.7f);
            sizeDelta_x = 5.3f;
        }
        else if (option.Parent != null)
        {
            scaleOffset = new(-0.05f, 0, 0);
            positionOffset = new(0.1f, 0f, 0f);
            color = new(0.5f, 0.7f, 0.5f);
            sizeDelta_x = 5.5f;
        }

        labelBackground.GetComponent<SpriteRenderer>().color = color;
        labelBackground.localScale += new Vector3(0.9f, -0.2f, 0f) + scaleOffset;
        labelBackground.localPosition += new Vector3(-0.4f, 0f, 0f) + positionOffset;

        var titleText = optionBehaviour.transform.FindChild("Title Text");
        titleText.localPosition += new Vector3(-0.4f, 0f, 0f) + positionOffset;
        titleText.GetComponent<RectTransform>().sizeDelta = new(sizeDelta_x, 0.37f);
        var titleTextText = titleText.GetComponent<TextMeshPro>();
        titleTextText.alignment = TextAlignmentOptions.MidlineLeft;
        titleTextText.fontStyle = FontStyles.Bold;
        titleTextText.outlineWidth = 0.17f;

        switch (type)
        {
            case OptionTypes.Checkbox:
                optionBehaviour.transform.FindChild("Toggle").localPosition = new(1.46f, -0.042f);
                break;

            case OptionTypes.String:
                optionBehaviour.transform.FindChild("PlusButton (1)").localPosition += new Vector3(option.IsText ? 500f : 1.7f, option.IsText ? 500f : 0f, option.IsText ? 500f : 0f);
                optionBehaviour.transform.FindChild("MinusButton (1)").localPosition += new Vector3(option.IsText ? 500f : 0.9f, option.IsText ? 500f : 0f, option.IsText ? 500f : 0f);
                optionBehaviour.transform.FindChild("Value_TMP (1)").localPosition += new Vector3(1.3f, 0f, 0f);
                optionBehaviour.transform.FindChild("Value_TMP (1)").GetComponent<RectTransform>().sizeDelta = new(2.3f, 0.4f);
                goto default;

            case OptionTypes.Float:
            case OptionTypes.Int:
                optionBehaviour.transform.FindChild("PlusButton").localPosition += new Vector3(option.IsText ? 500f : 1.7f, option.IsText ? 500f : 0f, option.IsText ? 500f : 0f);
                optionBehaviour.transform.FindChild("MinusButton").localPosition += new Vector3(option.IsText ? 500f : 0.9f, option.IsText ? 500f : 0f, option.IsText ? 500f : 0f);
                optionBehaviour.transform.FindChild("Value_TMP").localPosition += new Vector3(1.3f, 0f, 0f);
                goto default;

            default:
                optionBehaviour.transform.FindChild("ValueBox").localScale += new Vector3(0.2f, 0f, 0f);
                optionBehaviour.transform.FindChild("ValueBox").localPosition += new Vector3(1.3f, 0f, 0f);
                break;
        }
    }

    [HarmonyPatch(nameof(GameOptionsMenu.ValueChanged)), HarmonyPrefix]
    private static bool ValueChangedPrefix(GameOptionsMenu __instance, OptionBehaviour option)
    {
        if (ModGameOptionsMenu.TabIndex < 3) return true;

        if (ModGameOptionsMenu.OptionList.TryGetValue(option, out var index))
        {
            var item = OptionItem.AllOptions[index];
            if (item != null && item.Children.Count > 0) ReCreateSettings(__instance);
        }
        return false;
    }
    private static void ReCreateSettings(GameOptionsMenu __instance)
    {
        if (ModGameOptionsMenu.TabIndex < 3) return;
        var modTab = (TabGroup)(ModGameOptionsMenu.TabIndex - 3);

        float num = 2.0f;
        for (int index = 0; index < OptionItem.AllOptions.Count; index++)
        {
            var option = OptionItem.AllOptions[index];
            if (option.Tab != modTab) continue;

            var enabled = !option.IsHiddenOn(Options.CurrentGameMode) && (option.Parent == null || (!option.Parent.IsHiddenOn(Options.CurrentGameMode) && option.Parent.GetBool()));

            if (ModGameOptionsMenu.CategoryHeaderList.TryGetValue(index, out var categoryHeaderMasked))
            {
                categoryHeaderMasked.transform.localPosition = new(-0.903f, num, -2f);
                categoryHeaderMasked.gameObject.SetActive(enabled);
                if (enabled) num -= 0.63f;
            }
            if (ModGameOptionsMenu.BehaviourList.TryGetValue(index, out var optionBehaviour))
            {
                optionBehaviour.transform.localPosition = new(0.952f, num, -2f);
                optionBehaviour.gameObject.SetActive(enabled);
                if (enabled) num -= 0.45f;
            }
        }

        __instance.ControllerSelectable.Clear();
        foreach (var x in __instance.scrollBar.GetComponentsInChildren<UiElement>())
            __instance.ControllerSelectable.Add(x);
        __instance.scrollBar.SetYBoundsMax(-num - 1.65f);
    }

    private static BaseGameSetting GetSetting(OptionItem item)
    {
        // BaseGameSetting baseGameSetting = item switch
        // {
        //     BooleanOptionItem => new CheckboxGameSetting { Type = OptionTypes.Checkbox, },
        //     IntegerOptionItem integerOptionItem => new IntGameSetting
        //     {
        //         Type = OptionTypes.Int,
        //         Value = integerOptionItem.GetInt(),
        //         Increment = integerOptionItem.Rule.Step,
        //         ValidRange = new(integerOptionItem.Rule.MinValue, integerOptionItem.Rule.MaxValue),
        //         ZeroIsInfinity = false,
        //         SuffixType = NumberSuffixes.Multiplier,
        //         FormatString = string.Empty,
        //     },
        //     FloatOptionItem floatOptionItem => new FloatGameSetting
        //     {
        //         Type = OptionTypes.Float,
        //         Value = floatOptionItem.GetFloat(),
        //         Increment = floatOptionItem.Rule.Step,
        //         ValidRange = new(floatOptionItem.Rule.MinValue, floatOptionItem.Rule.MaxValue),
        //         ZeroIsInfinity = false,
        //         SuffixType = NumberSuffixes.Multiplier,
        //         FormatString = string.Empty,
        //     },
        //     StringOptionItem stringOptionItem => new StringGameSetting { Type = OptionTypes.String, Values = new StringNames[stringOptionItem.Selections.Count], Index = stringOptionItem.GetInt(), },
        //     _ => null
        // };

        BaseGameSetting baseGameSetting = item switch
        {
            BooleanOptionItem => ScriptableObject.CreateInstance<CheckboxGameSetting>(),
            IntegerOptionItem => ScriptableObject.CreateInstance<IntGameSetting>(),
            FloatOptionItem => ScriptableObject.CreateInstance<FloatGameSetting>(),
            StringOptionItem => ScriptableObject.CreateInstance<StringGameSetting>(),
            _ => null
        };

        switch (baseGameSetting)
        {
            case CheckboxGameSetting checkboxGameSetting:
                checkboxGameSetting.Type = OptionTypes.Checkbox;
                break;
            case IntGameSetting intGameSetting:
                intGameSetting.Type = OptionTypes.Int;
                intGameSetting.Value = item.GetInt();
                intGameSetting.Increment = item switch
                {
                    IntegerOptionItem integerOptionItem => integerOptionItem.Rule.Step,
                    _ => throw new ArgumentOutOfRangeException(nameof(item), item, null)
                };
                intGameSetting.ValidRange = item switch
                {
                    IntegerOptionItem integerOptionItem => new(integerOptionItem.Rule.MinValue, integerOptionItem.Rule.MaxValue),
                    _ => throw new ArgumentOutOfRangeException(nameof(item), item, null)
                };
                intGameSetting.ZeroIsInfinity = false;
                intGameSetting.SuffixType = NumberSuffixes.Multiplier;
                intGameSetting.FormatString = string.Empty;
                break;
            case FloatGameSetting floatGameSetting:
                floatGameSetting.Type = OptionTypes.Float;
                floatGameSetting.Value = item.GetFloat();
                floatGameSetting.Increment = item switch
                {
                    FloatOptionItem floatOptionItem => floatOptionItem.Rule.Step,
                    _ => throw new ArgumentOutOfRangeException(nameof(item), item, null)
                };
                floatGameSetting.ValidRange = item switch
                {
                    FloatOptionItem floatOptionItem => new(floatOptionItem.Rule.MinValue, floatOptionItem.Rule.MaxValue),
                    _ => throw new ArgumentOutOfRangeException(nameof(item), item, null)
                };
                floatGameSetting.ZeroIsInfinity = false;
                floatGameSetting.SuffixType = NumberSuffixes.Multiplier;
                floatGameSetting.FormatString = string.Empty;
                break;
            case StringGameSetting stringGameSetting:
                stringGameSetting.Type = OptionTypes.String;
                stringGameSetting.Values = new StringNames[item switch
                {
                    StringOptionItem stringOptionItem => stringOptionItem.Selections.Count,
                    _ => throw new ArgumentOutOfRangeException(nameof(item), item, null)
                }];
                stringGameSetting.Index = item.GetInt();
                break;
        }

        if (baseGameSetting != null)
        {
            baseGameSetting.Title = StringNames.Accept;
        }

        return baseGameSetting;
    }
}

[HarmonyPatch(typeof(ToggleOption))]
public static class ToggleOptionPatch
{
    [HarmonyPatch(nameof(ToggleOption.Initialize)), HarmonyPrefix]
    private static bool InitializePrefix(ToggleOption __instance)
    {
        if (ModGameOptionsMenu.OptionList.TryGetValue(__instance, out var index))
        {
            var item = OptionItem.AllOptions[index];
            __instance.TitleText.text = item.GetName();
            __instance.CheckMark.enabled = item.GetBool();
            return false;
        }
        return true;
    }
    [HarmonyPatch(nameof(ToggleOption.UpdateValue)), HarmonyPrefix]
    private static bool UpdateValuePrefix(ToggleOption __instance)
    {
        if (ModGameOptionsMenu.OptionList.TryGetValue(__instance, out var index))
        {
            var item = OptionItem.AllOptions[index];
            item.SetValue(__instance.GetBool() ? 1 : 0);
            return false;
        }
        return true;
    }
}
[HarmonyPatch(typeof(NumberOption))]
public static class NumberOptionPatch
{
    [HarmonyPatch(nameof(NumberOption.Initialize)), HarmonyPrefix]
    private static bool InitializePrefix(NumberOption __instance)
    {
        switch (__instance.Title)
        {
            case StringNames.GameVotingTime:
                __instance.ValidRange = new(0, 600);
                __instance.Value = (float)Math.Round(__instance.Value, 2);
                break;
            case StringNames.GameShortTasks:
            case StringNames.GameLongTasks:
            case StringNames.GameCommonTasks:
                __instance.ValidRange = new(0, 90);
                __instance.Value = (float)Math.Round(__instance.Value, 2);
                break;
            case StringNames.GameKillCooldown:
                __instance.ValidRange = new(0, 180);
                __instance.Increment = 0.5f;
                __instance.Value = (float)Math.Round(__instance.Value, 2);
                break;
            case StringNames.GamePlayerSpeed:
            case StringNames.GameCrewLight:
            case StringNames.GameImpostorLight:
                __instance.Increment = 0.05f;
                __instance.Value = (float)Math.Round(__instance.Value, 2);
                break;
            case StringNames.GameNumImpostors:
                if (DebugModeManager.IsDebugMode)
                {
                    __instance.ValidRange.min = 0;
                }
                break;
        }

        if (ModGameOptionsMenu.OptionList.TryGetValue(__instance, out var index))
        {
            var item = OptionItem.AllOptions[index];
            __instance.TitleText.text = item.GetName();
            return false;
        }
        return true;
    }
    [HarmonyPatch(nameof(NumberOption.UpdateValue)), HarmonyPrefix]
    private static bool UpdateValuePrefix(NumberOption __instance)
    {
        if (ModGameOptionsMenu.OptionList.TryGetValue(__instance, out var index))
        {
            var item = OptionItem.AllOptions[index];

            switch (item)
            {
                case IntegerOptionItem integerOptionItem:
                    integerOptionItem.SetValue(integerOptionItem.Rule.GetNearestIndex(__instance.GetInt()));
                    break;
                case FloatOptionItem floatOptionItem:
                    floatOptionItem.SetValue(floatOptionItem.Rule.GetNearestIndex(__instance.GetFloat()));
                    break;
            }

            return false;
        }
        return true;
    }
    [HarmonyPatch(nameof(NumberOption.FixedUpdate)), HarmonyPrefix]
    private static bool FixedUpdatePrefix(NumberOption __instance)
    {
        if (ModGameOptionsMenu.OptionList.TryGetValue(__instance, out var index))
        {
            var item = OptionItem.AllOptions[index];

            if (!Mathf.Approximately(__instance.oldValue, __instance.Value))
            {
                __instance.oldValue = __instance.Value;
                __instance.ValueText.text = GetValueString(__instance, __instance.Value, item);
            }
            return false;
        }
        return true;
    }
    public static string GetValueString(NumberOption __instance, float value, OptionItem item)
    {
        if (__instance.ZeroIsInfinity && Mathf.Abs(value) < 0.0001f) return "<b>∞</b>";
        if (item == null) return value.ToString(__instance.FormatString);
        return item.ApplyFormat(value.ToString(CultureInfo.CurrentCulture));
    }
    [HarmonyPatch(nameof(NumberOption.Increase)), HarmonyPrefix]
    public static bool IncreasePrefix(NumberOption __instance)
    {
        if (Mathf.Approximately(__instance.Value, __instance.ValidRange.max))
        {
            __instance.Value = __instance.ValidRange.min;
            __instance.UpdateValue();
            __instance.OnValueChanged.Invoke(__instance);
            return false;
        }
        return true;
    }
    [HarmonyPatch(nameof(NumberOption.Decrease)), HarmonyPrefix]
    public static bool DecreasePrefix(NumberOption __instance)
    {
        if (Mathf.Approximately(__instance.Value, __instance.ValidRange.min))
        {
            __instance.Value = __instance.ValidRange.max;
            __instance.UpdateValue();
            __instance.OnValueChanged.Invoke(__instance);
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(StringOption))]
public static class StringOptionPatch
{
    [HarmonyPatch(nameof(StringOption.Initialize)), HarmonyPrefix]
    private static bool InitializePrefix(StringOption __instance)
    {
        if (ModGameOptionsMenu.OptionList.TryGetValue(__instance, out var index))
        {
            var item = OptionItem.AllOptions[index];
            __instance.TitleText.text = item.GetName();
            return false;
        }
        return true;
    }
    [HarmonyPatch(nameof(StringOption.UpdateValue)), HarmonyPrefix]
    private static bool UpdateValuePrefix(StringOption __instance)
    {
        if (ModGameOptionsMenu.OptionList.TryGetValue(__instance, out var index))
        {
            var item = OptionItem.AllOptions[index];
            Logger.Info($"{item.Name}, {index}", "StringOption.UpdateValue.TryAdd");

            item.SetValue(__instance.GetInt());
            return false;
        }
        return true;
    }
    [HarmonyPatch(nameof(StringOption.FixedUpdate)), HarmonyPrefix]
    private static bool FixedUpdatePrefix(StringOption __instance)
    {
        if (ModGameOptionsMenu.OptionList.TryGetValue(__instance, out var index))
        {
            var item = OptionItem.AllOptions[index];

            if (item is StringOptionItem stringOptionItem)
            {
                if (__instance.oldValue != __instance.Value)
                {
                    __instance.oldValue = __instance.Value;
                    __instance.ValueText.text = GetString(stringOptionItem.Selections[stringOptionItem.Rule.GetValueByIndex(__instance.Value)]);
                }
            }
            return false;
        }
        return true;
    }
    [HarmonyPatch(nameof(StringOption.Increase)), HarmonyPrefix]
    public static bool IncreasePrefix(StringOption __instance)
    {
        if (__instance.Value == __instance.Values.Length - 1)
        {
            __instance.Value = 0;
            __instance.UpdateValue();
            __instance.OnValueChanged.Invoke(__instance);
            return false;
        }
        return true;
    }
    [HarmonyPatch(nameof(StringOption.Decrease)), HarmonyPrefix]
    public static bool DecreasePrefix(StringOption __instance)
    {
        if (__instance.Value == 0)
        {
            __instance.Value = __instance.Values.Length - 1;
            __instance.UpdateValue();
            __instance.OnValueChanged.Invoke(__instance);
            return false;
        }
        return true;
    }
}*/

// ==================================================================================================================================
/*
[HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.Awake))]
[HarmonyPriority(Priority.First)]
public static class GameOptionsMenuPatch
{
    public static void Postfix(GameOptionsMenu __instance)
    {
        LateTask.New(() =>
        {
            foreach (OptionBehaviour ob in __instance.Children)
            {
                switch (ob.Title)
                {
                    case StringNames.GameVotingTime:
                        ob.Cast<NumberOption>().ValidRange = new(0, 600);
                        ob.Cast<NumberOption>().Value = (float)Math.Round(ob.Cast<NumberOption>().Value, 2);
                        break;
                    case StringNames.GameShortTasks:
                    case StringNames.GameLongTasks:
                    case StringNames.GameCommonTasks:
                        ob.Cast<NumberOption>().ValidRange = new(0, 90);
                        ob.Cast<NumberOption>().Value = (float)Math.Round(ob.Cast<NumberOption>().Value, 2);
                        break;
                    case StringNames.GameKillCooldown:
                        ob.Cast<NumberOption>().ValidRange = new(0, 180);
                        ob.Cast<NumberOption>().Increment = 0.5f;
                        ob.Cast<NumberOption>().Value = (float)Math.Round(ob.Cast<NumberOption>().Value, 2);
                        break;
                    case StringNames.GamePlayerSpeed:
                    case StringNames.GameCrewLight:
                    case StringNames.GameImpostorLight:
                        ob.Cast<NumberOption>().Increment = 0.05f;
                        ob.Cast<NumberOption>().Value = (float)Math.Round(ob.Cast<NumberOption>().Value, 2);
                        break;
                }
            }
        }, 2f, log: false);

        var template = Object.FindObjectsOfType<StringOption>().FirstOrDefault();
        if (template == null) return;

        var Tint = GameObject.Find("Tint");
        Tint?.SetActive(false);

        var gameSettings = GameObject.Find("Game Settings");
        if (gameSettings == null) return;
        if (Main.DarkTheme.Value) gameSettings.transform.FindChild("BackPanel").transform.FindChild("baseColor").GetComponent<SpriteRenderer>().color = new(0.25f, 0.25f, 0.25f, 1f);
        gameSettings.transform.FindChild("GameGroup").GetComponent<Scroller>().ScrollWheelSpeed = 1.1f;

        var gameSettingMenu = Object.FindObjectsOfType<GameSettingMenu>().FirstOrDefault();
        if (gameSettingMenu == null) return;
        List<GameObject> menus = [gameSettingMenu.GameSettingsTab.gameObject, gameSettingMenu.RoleSettingsTab.gameObject];
        // System.Collections.Generic.List<SpriteRenderer> highlights = [gameSettingMenu.GameSettingsHightlight, gameSettingMenu.RolesSettingsHightlight];

        var roleTab = GameObject.Find("RoleTab");
        var gameTab = GameObject.Find("GameTab");
        List<GameObject> tabs = [gameTab, roleTab];

        float delay = 0f;

        foreach ((TabGroup tab, OptionItem[] optionItems) in Options.GroupedOptions)
        {
            var obj = gameSettings.transform.parent.Find(tab + "Tab");
            if (obj != null)
            {
                obj.transform.FindChild("../../GameGroup/Text").GetComponent<TextMeshPro>().SetText(GetString("TabGroup." + tab));
                continue;
            }

            var ehrSettings = Object.Instantiate(gameSettings, gameSettings.transform.parent);
            ehrSettings.name = tab + "Tab";
            var backPanel = ehrSettings.transform.FindChild("BackPanel");
            backPanel.transform.localScale =
                ehrSettings.transform.FindChild("Bottom Gradient").transform.localScale = new(1.6f, 1f, 1f);
            backPanel.transform.localPosition += new Vector3(0.2f, 0f, 0f);
            ehrSettings.transform.FindChild("Bottom Gradient").transform.localPosition += new Vector3(0.2f, 0f, 0f);
            ehrSettings.transform.FindChild("Background").transform.localScale = new(1.8f, 1f, 1f);
            ehrSettings.transform.FindChild("UI_Scrollbar").transform.localPosition += new Vector3(1.4f, 0f, 0f);
            ehrSettings.transform.FindChild("UI_ScrollbarTrack").transform.localPosition += new Vector3(1.4f, 0f, 0f);
            ehrSettings.transform.FindChild("GameGroup/SliderInner").transform.localPosition += new Vector3(-0.3f, 0f, 0f);

            var ehrMenu = ehrSettings.transform.FindChild("GameGroup/SliderInner").GetComponent<GameOptionsMenu>();

            var scOptions = new Il2CppSystem.Collections.Generic.List<OptionBehaviour>();

            LateTask.New(() =>
            {
                ehrMenu.GetComponentsInChildren<OptionBehaviour>().Do(x => Object.Destroy(x.gameObject));

                foreach (OptionItem option in optionItems)
                {
                    if (option.OptionBehaviour == null)
                    {
                        float yoffset = option.IsText ? 100f : 0f;
                        var stringOption = Object.Instantiate(template, ehrMenu.transform);
                        scOptions.Add(stringOption);

                        stringOption.OnValueChanged = new Action<OptionBehaviour>(_ => { });
                        stringOption.TitleText.text = option.Name;
                        stringOption.Value = stringOption.oldValue = option.CurrentValue;
                        stringOption.ValueText.text = option.GetString();
                        stringOption.name = option.Name;

                        var bg = stringOption.transform.FindChild("Background");
                        bg.localScale = new(1.6f, 1f, 1f);
                        if (Main.DarkTheme.Value) bg.GetComponent<SpriteRenderer>().color = new(0f, 0f, 0f, 1f);

                        var plus = stringOption.transform.FindChild("Plus_TMP");
                        var minus = stringOption.transform.FindChild("Minus_TMP");
                        plus.localPosition += new Vector3(1.4f, yoffset, 0f);
                        minus.localPosition += new Vector3(1.0f, yoffset, 0f);
                        if (option.IsText)
                        {
                            plus.gameObject.SetActive(false);
                            minus.gameObject.SetActive(false);
                        }

                        var valueTMP = stringOption.transform.FindChild("Value_TMP");
                        valueTMP.localPosition += new Vector3(1.2f, yoffset, 0f);
                        valueTMP.GetComponent<RectTransform>().sizeDelta = new(1.6f, 0.26f);
                        if (option.IsText) valueTMP.gameObject.SetActive(false);

                        var titleTMP = stringOption.transform.FindChild("Title_TMP");
                        titleTMP.localPosition += new Vector3(option.IsText ? 0.25f : 0.1f, option.IsText ? -0.1f : 0f, 0f);
                        titleTMP.GetComponent<RectTransform>().sizeDelta = new(5.5f, 0.37f);

                        option.OptionBehaviour = stringOption;
                    }

                    option.OptionBehaviour.gameObject.SetActive(true);
                }
            }, delay, log: false);

            delay += 0.1f;

            ehrMenu.Children = scOptions;

            ehrSettings.gameObject.SetActive(false);
            menus.Add(ehrSettings.gameObject);

            var ehrTab = Object.Instantiate(roleTab, roleTab.transform.parent);
            var hatButton = ehrTab.transform.FindChild("Hat Button");
            hatButton.FindChild("Icon").GetComponent<SpriteRenderer>().sprite = Utils.LoadSprite($"EHR.Resources.Images.TabIcon_{tab}.png", 100f);
            tabs.Add(ehrTab);
            // var ehrTabHighlight = hatButton.FindChild("Tab Background").GetComponent<SpriteRenderer>();
            // highlights.Add(ehrTabHighlight);
        }

        for (var i = 0; i < tabs.Count; i++)
        {
            tabs[i].transform.localPosition = new(0.8f * (i - 1) - tabs.Count / 3f, tabs[i].transform.localPosition.y, tabs[i].transform.localPosition.z);
            var button = tabs[i].GetComponentInChildren<PassiveButton>();
            if (button == null) continue;
            var copiedIndex = i;
            button.OnClick = new();

            button.OnClick.AddListener((Action)Value);
            continue;

            void Value()
            {
                for (var j = 0; j < menus.Count; j++)
                {
                    menus[j].SetActive(j == copiedIndex);
                    //highlights[j].enabled = j == copiedIndex;
                }
            }
        }
    }
}*/
/*
[HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.Update))]
public static class GameOptionsMenuUpdatePatch
{
    private static float Timer = 1f;

    public static void Postfix(GameOptionsMenu __instance)
    {
        if (__instance.transform.parent.parent.name == "Game Settings") return;

        foreach ((TabGroup tab, OptionItem[] optionItems) in Options.GroupedOptions)
        {
            string tabcolor = tab switch
            {
                TabGroup.SystemSettings => Main.ModColor,
                TabGroup.GameSettings => "#59ef83",
                TabGroup.TaskSettings => "#EF59AF",
                TabGroup.ImpostorRoles => "#f74631",
                TabGroup.CrewmateRoles => "#8cffff",
                TabGroup.NeutralRoles => "#ffab1b",
                TabGroup.Addons => "#ff9ace",
                TabGroup.OtherRoles => "#76b8e0",
                _ => "#ffffff"
            };
            if (__instance.transform.parent.parent.name != tab + "Tab") continue;
            __instance.transform.FindChild("../../GameGroup/Text").GetComponent<TextMeshPro>().SetText($"<color={tabcolor}>" + GetString("TabGroup." + tab) + "</color>");

            Timer += Time.deltaTime;
            if (Timer < 0.2f) return;
            Timer = 0f;

            var offset = 2.7f;

            foreach (OptionItem option in optionItems)
            {
                if (option.OptionBehaviour == null || option.OptionBehaviour.gameObject == null) continue;

                var parent = option.Parent;

                bool enabled = AmongUsClient.Instance.AmHost && !option.IsHiddenOn(Options.CurrentGameMode);

                var opt = option.OptionBehaviour.transform.Find("Background").GetComponent<SpriteRenderer>();
                opt.size = new(5.0f, 0.45f);

                while (parent != null && enabled)
                {
                    enabled = parent.GetBool() && !parent.IsHiddenOn(Options.CurrentGameMode);
                    parent = parent.Parent;

                    opt.color = new(0f, 0f, 1f, 1f);
                    opt.size = new(4.8f, 0.45f);
                    opt.transform.localPosition = new(0.11f, 0f);

                    var titleTMP = option.OptionBehaviour.transform.Find("Title_TMP");
                    var rectTransform = option.OptionBehaviour.transform.FindChild("Title_TMP").GetComponent<RectTransform>();

                    titleTMP.transform.localPosition = new(-1.08f, 0f);
                    rectTransform.sizeDelta = new(5.1f, 0.28f);

                    if (option.Parent?.Parent != null)
                    {
                        opt.color = new(1f, 0f, 0f, 1f);
                        opt.size = new(4.6f, 0.45f);
                        opt.transform.localPosition = new(0.24f, 0f);

                        titleTMP.transform.localPosition = new(-0.88f, 0f);
                        rectTransform.sizeDelta = new(4.9f, 0.28f);

                        if (option.Parent?.Parent?.Parent != null)
                        {
                            opt.color = new(0f, 1f, 0f, 1f);
                            opt.size = new(4.4f, 0.45f);
                            opt.transform.localPosition = new(0.37f, 0f);

                            titleTMP.transform.localPosition = new(-0.68f, 0f);
                            rectTransform.sizeDelta = new(4.7f, 0.28f);
                        }
                    }
                }

                if (option.IsText)
                {
                    opt.color = new(0, 0, 0);
                    opt.transform.localPosition = new(100f, 100f, 100f);
                }

                option.OptionBehaviour.gameObject.SetActive(enabled);
                if (enabled)
                {
                    offset -= option.IsHeader ? 0.7f : 0.5f;
                    option.OptionBehaviour.transform.localPosition = new(
                        option.OptionBehaviour.transform.localPosition.x,
                        offset,
                        option.OptionBehaviour.transform.localPosition.z);
                }
            }

            __instance.GetComponentInParent<Scroller>().ContentYBounds.max = (-offset) - 1.5f;
        }
    }
}

[HarmonyPatch(typeof(StringOption), nameof(StringOption.Start))]
public class StringOptionEnablePatch
{
    public static bool Prefix(StringOption __instance)
    {
        var option = OptionItem.AllOptions.FirstOrDefault(opt => opt.OptionBehaviour == __instance);
        if (option == null) return true;

        __instance.OnValueChanged = new Action<OptionBehaviour>(_ => { });
        __instance.TitleText.text = option.GetName();
        if (option.Id == Options.UsePets.Id) LoadLangs();
        else if (!Options.UsePets.GetBool() && CustomRolesHelper.OnlySpawnsWithPetsRoleList.Any(role => GetString(role.ToString()).RemoveHtmlTags().Equals(option.GetName().RemoveHtmlTags())))
            __instance.TitleText.text += GetString("RequiresPetIndicator");
        else if (Options.UsePets.GetBool() && Enum.TryParse(option.Name, true, out CustomRoles petRole) && petRole.PetActivatedAbility())
            __instance.TitleText.text += GetString("SupportsPetIndicator");
        if (CustomRolesHelper.ExperimentalRoleList.Any(role => GetString(role.ToString()).RemoveHtmlTags().Equals(option.GetName().RemoveHtmlTags())))
            __instance.TitleText.text += GetString("ExperimentalRoleIndicator");
        __instance.Value = __instance.oldValue = option.CurrentValue;
        __instance.ValueText.text = option.GetString();

        return false;
    }
}

[HarmonyPatch(typeof(StringOption), nameof(StringOption.Increase))]
public class StringOptionIncreasePatch
{
    public static bool Prefix(StringOption __instance)
    {
        var option = OptionItem.AllOptions.FirstOrDefault(opt => opt.OptionBehaviour == __instance);
        if (option == null) return true;

        option.SetValue(option.CurrentValue + (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 5 : (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ? 10 : 1)));
        return false;
    }

    public static void Postfix() => OptionShower.GetText();
}

[HarmonyPatch(typeof(StringOption), nameof(StringOption.Decrease))]
public class StringOptionDecreasePatch
{
    public static bool Prefix(StringOption __instance)
    {
        var option = OptionItem.AllOptions.FirstOrDefault(opt => opt.OptionBehaviour == __instance);
        if (option == null) return true;

        option.SetValue(option.CurrentValue - (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 5 : (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ? 10 : 1)));
        return false;
    }

    public static void Postfix() => OptionShower.GetText();
}*/
/*
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSyncSettings))]
public class RpcSyncSettingsPatch
{
    public static void Postfix()
    {
        OptionItem.SyncAllOptions();
    }
}*/

// [HarmonyPatch(typeof(RolesSettingsMenu), nameof(RolesSettingsMenu.Start))]
// public static class RolesSettingsMenuPatch
// {
//     public static void Postfix(RolesSettingsMenu __instance)
//     {
//         foreach (OptionBehaviour ob in __instance.Children)
//         {
//             // ReSharper disable once ConvertSwitchStatementToSwitchExpression
//             switch (ob.Title)
//             {
//                 case StringNames.EngineerCooldown:
//                     ob.Cast<NumberOption>().ValidRange = new(0, 180);
//                     break;
//                 case StringNames.ShapeshifterCooldown:
//                     ob.Cast<NumberOption>().ValidRange = new(0, 180);
//                     break;
//             }
//         }
//     }
// }

// [HarmonyPatch(typeof(NormalGameOptionsV08), nameof(NormalGameOptionsV08.SetRecommendations))]
public static class SetRecommendationsPatch
{
    public static bool Prefix(NormalGameOptionsV08 __instance, int numPlayers, bool isOnline)
    {
        numPlayers = Mathf.Clamp(numPlayers, 4, 15);
        __instance.PlayerSpeedMod = __instance.MapId == 4 ? 1.5f : 1.25f;
        __instance.CrewLightMod = 0.5f;
        __instance.ImpostorLightMod = 1.25f;
        __instance.KillCooldown = 27.5f;
        __instance.NumCommonTasks = 1;
        __instance.NumLongTasks = 3;
        __instance.NumShortTasks = 4;
        __instance.NumEmergencyMeetings = 1;
        if (!isOnline)
            __instance.NumImpostors = NormalGameOptionsV08.RecommendedImpostors[numPlayers];
        __instance.KillDistance = 1;
        __instance.DiscussionTime = 0;
        __instance.VotingTime = 120;
        __instance.IsDefaults = true;
        __instance.ConfirmImpostor = false;
        __instance.VisualTasks = false;

        __instance.roleOptions.SetRoleRate(RoleTypes.Shapeshifter, 0, 0);
        __instance.roleOptions.SetRoleRate(RoleTypes.Scientist, 0, 0);
        __instance.roleOptions.SetRoleRate(RoleTypes.GuardianAngel, 0, 0);
        __instance.roleOptions.SetRoleRate(RoleTypes.Engineer, 0, 0);
        __instance.roleOptions.SetRoleRecommended(RoleTypes.Shapeshifter);
        __instance.roleOptions.SetRoleRecommended(RoleTypes.Scientist);
        __instance.roleOptions.SetRoleRecommended(RoleTypes.GuardianAngel);
        __instance.roleOptions.SetRoleRecommended(RoleTypes.Engineer);

        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.SoloKombat:
            case CustomGameMode.FFA:
            case CustomGameMode.HotPotato:
                __instance.CrewLightMod = __instance.ImpostorLightMod = 1.25f;
                __instance.NumImpostors = 3;
                __instance.NumCommonTasks = 0;
                __instance.NumLongTasks = 0;
                __instance.NumShortTasks = 0;
                __instance.KillCooldown = 0f;
                __instance.NumEmergencyMeetings = 0;
                break;
            case CustomGameMode.Speedrun:
            case CustomGameMode.MoveAndStop:
                __instance.CrewLightMod = 1.25f;
                __instance.ImpostorLightMod = 1.25f;
                __instance.KillCooldown = 60f;
                __instance.NumCommonTasks = 2;
                __instance.NumLongTasks = 3;
                __instance.NumShortTasks = 5;
                __instance.NumEmergencyMeetings = 0;
                __instance.VisualTasks = true;
                break;
            case CustomGameMode.HideAndSeek:
                __instance.CrewLightMod = 1.25f;
                __instance.ImpostorLightMod = 0.5f;
                __instance.KillCooldown = 10f;
                __instance.NumCommonTasks = 2;
                __instance.NumLongTasks = 3;
                __instance.NumShortTasks = 5;
                __instance.NumEmergencyMeetings = 0;
                __instance.VisualTasks = true;
                break;
        }

        return false;
    }
}