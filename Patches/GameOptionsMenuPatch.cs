using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using EHR.Gamemodes;
using EHR.Modules;
using EHR.Patches;
using EHR.Roles;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

// ReSharper disable PossibleLossOfFraction

namespace EHR;
// Credit: https://github.com/Yumenopai/TownOfHost_Y

public static class ModGameOptionsMenu
{
    public static int TabIndex;
    private static Dictionary<int, int> _optionList;
    private static Dictionary<int, OptionBehaviour> _behaviourList;
    private static Dictionary<int, CategoryHeaderMasked> _categoryHeaderList;

    public static void ClearDictionaries()
    {
        OptionList.Clear();
        BehaviourList.Clear();
        CategoryHeaderList.Clear();
    }

    public static OptionItem GetFromBehaviour(OptionBehaviour behaviour)
    {
        if (OptionList.TryGetValue(behaviour.GetHashCode(), out int id))
        {
            return OptionItem.FastOptions[id];
        }

        return null;// behaviour.GetComponent<CustomOptionBehaviour>() is { item: var item } ? item : null;
    }

    public static Dictionary<int, int> OptionList => _optionList ??= new();
    public static Dictionary<int, OptionBehaviour> BehaviourList => _behaviourList ??= new();
    public static Dictionary<int, CategoryHeaderMasked> CategoryHeaderList => _categoryHeaderList ??= new();
}

[HarmonyPatch(typeof(GameOptionsMenu))]
public static class GameOptionsMenuPatch
{
    [HarmonyPatch(nameof(GameOptionsMenu.Initialize))]
    [HarmonyPrefix]
    private static bool InitializePrefix(GameOptionsMenu __instance)
    {
        if (ModGameOptionsMenu.TabIndex < 3) return true;

        if (__instance.Children == null || __instance.Children.Count == 0)
        {
            __instance.MapPicker.gameObject.SetActive(false);
            __instance.Children = new();
            __instance.CreateSettings();
            __instance.cachedData = GameOptionsManager.Instance.CurrentGameOptions;
            __instance.InitializeControllerNavigation();
        }

        return false;
    }

    [HarmonyPatch(nameof(GameOptionsMenu.Initialize))]
    [HarmonyPostfix]
    private static void InitializePostfix()
    {
        GameObject optionMenu = GameObject.Find("PlayerOptionsMenu(Clone)");
        if (optionMenu == null) return;

        optionMenu.transform.FindChild("Background")?.gameObject.SetActive(false);

        // By TommyXL
        LateTask.New(() =>
        {
            if (optionMenu == null) return;

            Transform menuDescription = optionMenu.transform.FindChild("What Is This?");
            if (menuDescription == null) return;

            Transform infoImage = menuDescription.transform.FindChild("InfoImage");

            if (infoImage != null)
            {
                infoImage.transform.localPosition = new(-4.65f, 0.16f, -1f);
                infoImage.transform.localScale = new(0.2202f, 0.2202f, 0.3202f);
            }

            Transform infoText = menuDescription.transform.FindChild("InfoText");

            if (infoText != null)
            {
                infoText.transform.localPosition = new(-3.5f, 0.83f, -2f);
                infoText.transform.localScale = new(1f, 1f, 1f);
            }

            Transform cubeObject = menuDescription.transform.FindChild("Cube");

            if (cubeObject != null)
            {
                cubeObject.transform.localPosition = new(-3.2f, 0.55f, -0.1f);
                cubeObject.transform.localScale = new(0.61f, 0.64f, 1f);
            }

            if (GameSettingMenu.Instance != null && GameSettingMenu.Instance.MenuDescriptionText != null)
                GameSettingMenu.Instance.MenuDescriptionText.m_marginWidth = 2.5f;
        }, 0.2f, log: false);
    }

    [HarmonyPatch(nameof(GameOptionsMenu.CreateSettings))]
    [HarmonyPrefix]
    private static bool CreateSettingsPrefix(GameOptionsMenu __instance)
    {
        if (ModGameOptionsMenu.TabIndex < 3) return true;

        var modTab = (TabGroup)(ModGameOptionsMenu.TabIndex - 3);

        __instance.scrollBar.SetYBoundsMax(CalculateScrollBarYBoundsMax());
        __instance.StartCoroutine(CoRoutine().WrapToIl2Cpp());
        return false;

        IEnumerator CoRoutine()
        {
            var num = 2.0f;
            const float posX = 0.952f;
            const float posZ = -2.0f;
            
            TextOptionItem header = null;
            
            const int frameBudget = 10; // milliseconds
            var stopwatch = new Stopwatch();
            stopwatch.Start();
    
            foreach (var option in OptionItem.AllOptions)
            {
                try
                {
                    if (option.Tab != modTab) continue;

                    bool enabled = !option.IsCurrentlyHidden() && AllParentsEnabledAndVisible(option.Parent);

                    if (option is TextOptionItem toi)
                    {
                        CategoryHeaderMasked categoryHeaderMasked = Object.Instantiate(__instance.categoryHeaderOrigin, Vector3.zero, Quaternion.identity, __instance.settingsContainer);
                        categoryHeaderMasked.SetHeader(StringNames.RolesCategory, 20);
                        categoryHeaderMasked.Title.text = option.GetName(disableColor: true).Trim('★', ' ');
                        categoryHeaderMasked.Background.color = categoryHeaderMasked.Divider.color = option.NameColor;
                        categoryHeaderMasked.transform.localScale = Vector3.one * 0.63f;
                        categoryHeaderMasked.transform.localPosition = new(-0.903f, num, posZ);
                        var chmText = categoryHeaderMasked.transform.FindChild("HeaderText").GetComponent<TextMeshPro>();
                        chmText.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
                        chmText.fontWeight = FontWeight.Black;
                        chmText.outlineWidth = 0.17f;
                        var chmCollider = categoryHeaderMasked.gameObject.AddComponent<BoxCollider2D>();
                        chmCollider.size = new Vector2(7, 0.7f);
                        chmCollider.offset = new Vector2(1.5f, -0.3f);
                        var chmButton = categoryHeaderMasked.gameObject.AddComponent<PassiveButton>();
                        chmButton.ClickSound = __instance.BackButton.GetComponent<PassiveButton>().ClickSound;
                        chmButton.OnMouseOver = new();
                        chmButton.OnMouseOut = new();
                        chmButton.OnClick.AddListener((UnityAction)(() =>
                        {
                            toi.CollapsesSection = !toi.CollapsesSection;
                            ReCreateSettings(__instance);
                        }));
                        chmButton.SetButtonEnableState(true);
                        categoryHeaderMasked.gameObject.SetActive(enabled);
                        ModGameOptionsMenu.CategoryHeaderList.TryAdd(option.Id, categoryHeaderMasked);

                        if (enabled) num -= 0.63f;
                        header = toi;
                        continue;
                    }

                    option.Header = header;

                    if (option.IsHeader && enabled)
                        num -= 0.18f;

                    BaseGameSetting baseGameSetting = GetSetting(option);
                    if (baseGameSetting == null) continue;


                    OptionBehaviour optionBehaviour;

                    try
                    {
                        optionBehaviour = baseGameSetting.Type switch
                        {
                            OptionTypes.Checkbox => Object.Instantiate(__instance.checkboxOrigin, Vector3.zero, Quaternion.identity, __instance.settingsContainer),
                            OptionTypes.String => Object.Instantiate(__instance.stringOptionOrigin, Vector3.zero, Quaternion.identity, __instance.settingsContainer),
                            OptionTypes.Float or OptionTypes.Int => Object.Instantiate(__instance.numberOptionOrigin, Vector3.zero, Quaternion.identity, __instance.settingsContainer),
                            _ => throw new Exception("Skipped option type")
                        };
                    }
                    catch { continue; }

                    optionBehaviour.transform.localPosition = new(posX, num, posZ);
                    OptionBehaviourSetSizeAndPosition(optionBehaviour, option, baseGameSetting.Type);

                    if (!ModGameOptionsMenu.OptionList.ContainsValue(option.Id) && option.Name == "Preset")
                        GameSettingMenuPatch.PresetBehaviour = (NumberOption)optionBehaviour;

                    optionBehaviour.transform.localPosition = new(0.952f, num, -2f);
                    optionBehaviour.SetClickMask(__instance.ButtonClickMask);
                    optionBehaviour.SetUpFromData(baseGameSetting, 20);
                    ModGameOptionsMenu.OptionList.TryAdd(optionBehaviour.GetHashCode(), option.Id);
                    ModGameOptionsMenu.BehaviourList.TryAdd(option.Id, optionBehaviour);
                    optionBehaviour.gameObject.SetActive(enabled);
                    optionBehaviour.OnValueChanged = new Action<OptionBehaviour>(__instance.ValueChanged);
                    __instance.Children.Add(optionBehaviour);

                    option.OptionBehaviour = optionBehaviour;

                    if (enabled) num -= 0.45f;
                }
                catch (Exception e) { Utils.ThrowException(e); }

                if (stopwatch.ElapsedMilliseconds >= frameBudget)
                {
                    stopwatch.Reset();
                    yield return null;
                    stopwatch.Start();
                }
            }

            __instance.ControllerSelectable.Clear();

            foreach (UiElement x in __instance.scrollBar.GetComponentsInChildren<UiElement>())
                __instance.ControllerSelectable.Add(x);
        }

        float CalculateScrollBarYBoundsMax()
        {
            var num = 2.0f;

            foreach (OptionItem option in OptionItem.AllOptions)
            {
                if (option.Tab != modTab) continue;

                bool enabled = !option.IsCurrentlyHidden() && AllParentsEnabledAndVisible(option.Parent);

                if (option is TextOptionItem)
                    num -= 0.63f;
                else if (enabled)
                {
                    if (option.IsHeader) num -= 0.18f;

                    num -= 0.45f;
                }
            }

            return -num - 1.65f;
        }
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

    private static void OptionBehaviourSetSizeAndPosition(OptionBehaviour optionBehaviour, OptionItem option, OptionTypes type)
    {
        Vector3 positionOffset = new(0f, 0f, 0f);
        Vector3 scaleOffset = new(0f, 0f, 0f);
        Color color = new(0.35f, 0.35f, 0.35f);
        var sizeDeltaX = 5.7f;

        if (option.Parent?.Parent?.Parent != null)
        {
            scaleOffset = new(-0.18f, 0, 0);
            positionOffset = new(0.3f, 0f, 0f);
            color = new(0.35f, 0f, 0f);
            sizeDeltaX = 5.1f;
        }
        else if (option.Parent?.Parent != null)
        {
            scaleOffset = new(-0.12f, 0, 0);
            positionOffset = new(0.2f, 0f, 0f);
            color = new(0.35f, 0.35f, 0f);
            sizeDeltaX = 5.3f;
        }
        else if (option.Parent != null)
        {
            scaleOffset = new(-0.05f, 0, 0);
            positionOffset = new(0.1f, 0f, 0f);
            color = new(0f, 0f, 0.35f);
            sizeDeltaX = 5.5f;
        }

        Transform labelBackground = optionBehaviour.transform.FindChild("LabelBackground");
        labelBackground.GetComponent<SpriteRenderer>().color = color;
        labelBackground.localScale += new Vector3(0.9f, -0.2f, 0f) + scaleOffset;
        labelBackground.localPosition += new Vector3(-0.4f, 0f, 0f) + positionOffset;

        Transform titleText = optionBehaviour.transform.FindChild("Title Text");
        titleText.localPosition += new Vector3(-0.4f, 0f, 0f) + positionOffset;
        titleText.GetComponent<RectTransform>().sizeDelta = new(sizeDeltaX, 0.37f);
        var textMeshPro = titleText.GetComponent<TextMeshPro>();
        textMeshPro.alignment = TextAlignmentOptions.MidlineLeft;
        textMeshPro.fontStyle = FontStyles.Bold;
        textMeshPro.outlineWidth = 0.17f;

        switch (type)
        {
            case OptionTypes.Checkbox:
            {
                optionBehaviour.transform.FindChild("Toggle").localPosition = new(1.46f, -0.042f);
                break;
            }
            case OptionTypes.String:
            {
                Transform plusButton = optionBehaviour.transform.FindChild("PlusButton");
                Transform minusButton = optionBehaviour.transform.FindChild("MinusButton");
                plusButton.localPosition += new Vector3(option.IsText ? 500f : 1.7f, option.IsText ? 500f : 0f, option.IsText ? 500f : 0f);
                minusButton.localPosition += new Vector3(option.IsText ? 500f : 0.9f, option.IsText ? 500f : 0f, option.IsText ? 500f : 0f);
                Transform valueTMP = optionBehaviour.transform.FindChild("Value_TMP (1)");
                valueTMP.localPosition += new Vector3(1.3f, 0f, 0f);
                valueTMP.GetComponent<RectTransform>().sizeDelta = new(2.3f, 0.4f);
                goto default;
            }
            case OptionTypes.Float:
            case OptionTypes.Int:
            {
                optionBehaviour.transform.FindChild("PlusButton").localPosition += new Vector3(option.IsText ? 500f : 1.7f, option.IsText ? 500f : 0f, option.IsText ? 500f : 0f);
                optionBehaviour.transform.FindChild("MinusButton").localPosition += new Vector3(option.IsText ? 500f : 0.9f, option.IsText ? 500f : 0f, option.IsText ? 500f : 0f);
                optionBehaviour.transform.FindChild("Value_TMP").localPosition += new Vector3(1.3f, 0f, 0f);
                goto default;
            }
            default:
            {
                Transform valueBox = optionBehaviour.transform.FindChild("ValueBox");
                valueBox.localScale += new Vector3(0.2f, 0f, 0f);
                valueBox.localPosition += new Vector3(1.3f, 0f, 0f);
                break;
            }
        }
    }

    [HarmonyPatch(nameof(GameOptionsMenu.ValueChanged))]
    [HarmonyPrefix]
    private static bool ValueChangedPrefix(GameOptionsMenu __instance, OptionBehaviour option)
    {
        if (ModGameOptionsMenu.TabIndex < 3) return true;

        if (ModGameOptionsMenu.GetFromBehaviour(option) is { } item && item.Children.Count > 0)
            ReCreateSettings(__instance);

        return false;
    }

    public static void ReCreateSettings(GameOptionsMenu __instance)
    {
        if (ModGameOptionsMenu.TabIndex < 3) return;

        var modTab = (TabGroup)(ModGameOptionsMenu.TabIndex - 3);

        var num = 2.0f;

        foreach (var option in OptionItem.AllOptions)
        {
            if (option.Tab != modTab) continue;

            bool enabled = !option.IsCurrentlyHidden() && AllParentsEnabledAndVisible(option.Parent);

            if (ModGameOptionsMenu.CategoryHeaderList.TryGetValue(option.Id, out CategoryHeaderMasked categoryHeaderMasked))
            {
                categoryHeaderMasked.transform.localPosition = new(-0.903f, num, -2f);
                categoryHeaderMasked.gameObject.SetActive(enabled);
                if (enabled) num -= 0.63f;
            }
            else if (option.IsHeader && enabled) num -= 0.18f;

            if (ModGameOptionsMenu.BehaviourList.TryGetValue(option.Id, out OptionBehaviour optionBehaviour))
            {
                optionBehaviour.transform.localPosition = new(0.952f, num, -2f);
                optionBehaviour.gameObject.SetActive(enabled);
                if (enabled) num -= 0.45f;
            }
        }

        __instance.ControllerSelectable.Clear();

        foreach (UiElement x in __instance.scrollBar.GetComponentsInChildren<UiElement>())
            __instance.ControllerSelectable.Add(x);

        __instance.scrollBar.SetYBoundsMax(-num - 1.65f);
    }

    private static BaseGameSetting GetSetting(OptionItem item)
    {
        BaseGameSetting baseGameSetting;

        switch (item)
        {
            case BooleanOptionItem:
                var checkboxGameSetting = ScriptableObject.CreateInstance<CheckboxGameSetting>();
                checkboxGameSetting.Type = OptionTypes.Checkbox;
                baseGameSetting = checkboxGameSetting;
                break;
            case IntegerOptionItem integerOptionItem:
                var intGameSetting = ScriptableObject.CreateInstance<IntGameSetting>();
                intGameSetting.Type = OptionTypes.Int;
                intGameSetting.Value = integerOptionItem.GetInt();
                intGameSetting.Increment = integerOptionItem.Rule.Step;
                intGameSetting.ValidRange = new(integerOptionItem.Rule.MinValue, integerOptionItem.Rule.MaxValue);
                intGameSetting.ZeroIsInfinity = false;
                intGameSetting.SuffixType = NumberSuffixes.Multiplier;
                intGameSetting.FormatString = string.Empty;
                baseGameSetting = intGameSetting;
                break;
            case FloatOptionItem floatOptionItem:
                var floatGameSetting = ScriptableObject.CreateInstance<FloatGameSetting>();
                floatGameSetting.Type = OptionTypes.Float;
                floatGameSetting.Value = floatOptionItem.GetFloat();
                floatGameSetting.Increment = floatOptionItem.Rule.Step;
                floatGameSetting.ValidRange = new(floatOptionItem.Rule.MinValue, floatOptionItem.Rule.MaxValue);
                floatGameSetting.ZeroIsInfinity = false;
                floatGameSetting.SuffixType = NumberSuffixes.Multiplier;
                floatGameSetting.FormatString = string.Empty;
                baseGameSetting = floatGameSetting;
                break;
            case StringOptionItem stringOptionItem:
                var stringGameSetting = ScriptableObject.CreateInstance<StringGameSetting>();
                stringGameSetting.Type = OptionTypes.String;
                stringGameSetting.Values = new StringNames[stringOptionItem.Selections.Count];
                stringGameSetting.Index = stringOptionItem.GetInt();
                baseGameSetting = stringGameSetting;
                break;
            case PresetOptionItem presetOptionItem:
                var presetIntGameSetting = ScriptableObject.CreateInstance<IntGameSetting>();
                presetIntGameSetting.Type = OptionTypes.Int;
                presetIntGameSetting.Value = presetOptionItem.GetInt();
                presetIntGameSetting.Increment = presetOptionItem.Rule.Step;
                presetIntGameSetting.ValidRange = new(presetOptionItem.Rule.MinValue, presetOptionItem.Rule.MaxValue);
                presetIntGameSetting.ZeroIsInfinity = false;
                presetIntGameSetting.SuffixType = NumberSuffixes.Multiplier;
                presetIntGameSetting.FormatString = string.Empty;
                baseGameSetting = presetIntGameSetting;
                break;
            default:
                baseGameSetting = null;
                break;
        }

        if (baseGameSetting != null) baseGameSetting.Title = StringNames.Accept;

        return baseGameSetting;
    }

    // From MoreGamemodes, by Rabek009
    public static void ReloadUI()
    {
        int tab = ModGameOptionsMenu.TabIndex;
        if (GameSettingMenu.Instance == null) return;
        GameSettingMenu.Instance.Close();
        OptionsConsole optionsConsole = null;

        foreach (OptionsConsole console in Object.FindObjectsOfType<OptionsConsole>())
        {
            if (console.HostOnly)
                optionsConsole = console;
        }

        if (optionsConsole == null) return;

        if (Camera.main != null)
        {
            GameObject gameObject = Object.Instantiate(optionsConsole.MenuPrefab, Camera.main.transform, false);
            gameObject.transform.localPosition = optionsConsole.CustomPosition;
        }

        LateTask.New(() =>
        {
            if (GameSettingMenu.Instance == null) return;
            GameSettingMenu.Instance.ChangeTab(tab, false);
        }, 0.01f);

        LateTask.New(() => GameObject.Find("PlayerOptionsMenu(Clone)")?.transform.FindChild("What Is This?")?.gameObject.SetActive(false), 0.02f);
    }
}

[HarmonyPatch(typeof(ToggleOption))]
public static class ToggleOptionPatch
{
    [HarmonyPatch(nameof(ToggleOption.Initialize))]
    [HarmonyPrefix]
    private static bool InitializePrefix(ToggleOption __instance)
    {
        OptionItem item = ModGameOptionsMenu.GetFromBehaviour(__instance);
        if (item == null) return true;

        CustomGameMode gm = Options.CurrentGameMode;
        Color32 color = gm == CustomGameMode.Standard ? (TabGroup)(ModGameOptionsMenu.TabIndex - 3) switch
        {
            TabGroup.ImpostorRoles => new Color32(255, 25, 25, 255),
            TabGroup.CrewmateRoles => new Color32(140, 255, 255, 255),
            TabGroup.NeutralRoles => new Color32(255, 171, 27, 255),
            TabGroup.CovenRoles => new Color32(123, 63, 187, 255),
            _ => new Color32(0, 165, 255, 255)
        } : Main.GameModeColors.TryGetValue(gm, out var c) ? c : new Color32(0, 165, 255, 255);
        __instance.CheckMark.sprite = Utils.LoadSprite("EHR.Resources.Images.Checkmark.png", 100f);
        __instance.CheckMark.color = color;
        var renderer = __instance.CheckMark.transform.parent.FindChild("ActiveSprite").GetComponent<SpriteRenderer>();
        renderer.sprite = Utils.LoadSprite("EHR.Resources.Images.CheckMarkBox.png", 100f);
        renderer.color = color;
        
        __instance.TitleText.text = item.GetName();
        __instance.CheckMark.enabled = item.GetBool();
        item.OptionBehaviour = __instance;
        return false;
    }

    // For some reason, ToggleOption.UpdateValue isn't called for Steam users
    [HarmonyPatch(nameof(ToggleOption.Toggle))]
    [HarmonyPrefix]
    private static bool TogglePrefix(ToggleOption __instance)
    {
        OptionItem item = ModGameOptionsMenu.GetFromBehaviour(__instance);
        if (item == null) return true;

        __instance.CheckMark.enabled = !__instance.CheckMark.enabled;
        item.SetValue(__instance.GetBool() ? 1 : 0);
        __instance.OnValueChanged.Invoke(__instance);
        NotificationPopperPatch.AddSettingsChangeMessage(item, true);
        return false;
    }
}

[HarmonyPatch(typeof(NumberOption))]
public static class NumberOptionPatch
{
    private static int IncrementMultiplier
    {
        get
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return 5;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return 10;
            return 1;
        }
    }

    [HarmonyPatch(nameof(NumberOption.Initialize))]
    [HarmonyPrefix]
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
                __instance.ValidRange = new(0, Crowded.MaxImpostors);
                __instance.Value = (float)Math.Round(__instance.Value, 2);
                if (DebugModeManager.AmDebugger) __instance.ValidRange.min = 0;
                break;
            case StringNames.CapacityLabel:
                __instance.ValidRange = new(4, 127);
                break;
        }

        OptionItem item = ModGameOptionsMenu.GetFromBehaviour(__instance);
        if (item == null) return true;
        
        __instance.TitleText.text = item.GetName();
        item.OptionBehaviour = __instance;
        return false;
    }

    [HarmonyPatch(nameof(NumberOption.UpdateValue))]
    [HarmonyPrefix]
    private static bool UpdateValuePrefix(NumberOption __instance)
    {
        OptionItem item = ModGameOptionsMenu.GetFromBehaviour(__instance);
        if (item == null) return true;

        switch (item)
        {
            case IntegerOptionItem integerOptionItem:
                integerOptionItem.SetValue(integerOptionItem.Rule.GetNearestIndex(__instance.GetInt()));
                break;
            case FloatOptionItem floatOptionItem:
                floatOptionItem.SetValue(floatOptionItem.Rule.GetNearestIndex(__instance.GetFloat()));
                break;
            case PresetOptionItem presetOptionItem:
                presetOptionItem.SetValue(presetOptionItem.Rule.GetNearestIndex(__instance.GetInt()));
                GameOptionsMenuPatch.ReloadUI();
                break;
        }

        NotificationPopperPatch.AddSettingsChangeMessage(item, true);
        return false;
    }

    [HarmonyPatch(nameof(NumberOption.FixedUpdate))]
    [HarmonyPrefix]
    private static bool FixedUpdatePrefix(NumberOption __instance)
    {
        OptionItem item = ModGameOptionsMenu.GetFromBehaviour(__instance);
        if (item == null) return true;
        
        __instance.MinusBtn.SetInteractable(true);
        __instance.PlusBtn.SetInteractable(true);

        if (!Mathf.Approximately(__instance.oldValue, __instance.Value))
        {
            __instance.oldValue = __instance.Value;
            __instance.ValueText.text = GetValueString(__instance, __instance.Value, item);
        }

        return false;
    }

    private static string GetValueString(NumberOption __instance, float value, OptionItem item)
    {
        if (__instance.ZeroIsInfinity && Mathf.Abs(value) < 0.0001f) return "<b>∞</b>";

        return item == null ? value.ToString(__instance.FormatString) : item.GetString();
    }

    [HarmonyPatch(nameof(NumberOption.Increase))]
    [HarmonyPrefix]
    public static bool IncreasePrefix(NumberOption __instance)
    {
        if (Mathf.Approximately(__instance.Value, __instance.ValidRange.max))
        {
            __instance.Value = __instance.ValidRange.min;
            __instance.UpdateValue();
            __instance.OnValueChanged.Invoke(__instance);
            return false;
        }

        float increment = IncrementMultiplier * __instance.Increment;

        if (__instance.Value + increment < __instance.ValidRange.max)
        {
            __instance.Value += increment;
            __instance.UpdateValue();
            __instance.OnValueChanged.Invoke(__instance);
            return false;
        }

        return true;
    }

    [HarmonyPatch(nameof(NumberOption.Decrease))]
    [HarmonyPrefix]
    public static bool DecreasePrefix(NumberOption __instance)
    {
        if (Mathf.Approximately(__instance.Value, __instance.ValidRange.min))
        {
            __instance.Value = __instance.ValidRange.max;
            __instance.UpdateValue();
            __instance.OnValueChanged.Invoke(__instance);
            return false;
        }

        float increment = IncrementMultiplier * __instance.Increment;

        if (__instance.Value - increment > __instance.ValidRange.min)
        {
            __instance.Value -= increment;
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
    private static long HelpShowEndTS;

    [HarmonyPatch(nameof(StringOption.Initialize))]
    [HarmonyPrefix]
    private static bool InitializePrefix(StringOption __instance)
    {
        OptionItem item = ModGameOptionsMenu.GetFromBehaviour(__instance);
        if (item == null) return true;

        string name = item.GetName();
        item.OptionBehaviour = __instance;
        string name1 = name;

        if (Main.CustomRoleValues.FindFirst(x => Translator.GetString($"{x}") == name1.RemoveHtmlTags(), out CustomRoles role))
        {
            if (role.ToString().Contains("GuardianAngel")) role = CustomRoles.GA;

            name = name.RemoveHtmlTags();

            switch (Options.UsePets.GetBool())
            {
                case false when role.OnlySpawnsWithPets():
                    name += Translator.GetString("RequiresPetIndicator");
                    break;
                case true when role.PetActivatedAbility():
                    name += Translator.GetString("SupportsPetIndicator");
                    break;
            }

            if (role.IsExperimental()) name += $"<size=2>{Translator.GetString("ExperimentalRoleIndicator")}</size>";
            if (role.IsGhostRole()) name += GetGhostRoleTeam(role);
            if (role.IsDevFavoriteRole()) name += "  <size=2><#00ffff>★</color></size>";

            __instance.TitleText.fontWeight = FontWeight.Black;
            __instance.TitleText.outlineColor = new(255, 255, 255, 255);
            __instance.TitleText.outlineWidth = 0.04f;
            __instance.LabelBackground.color = Utils.GetRoleColor(role);
            __instance.TitleText.color = Color.white;
            name = $"<size=3.5>{name}</size>";
            SetupHelpIcon(role, __instance);
        }

        __instance.TitleText.text = name;
        return false;
    }

    private static void SetupHelpIcon(CustomRoles role, StringOption option)
    {
        Transform template = option.transform.FindChild("MinusButton");
        Transform icon = Object.Instantiate(template, template.parent, true);
        icon.name = $"{role}HelpIcon";
        var text = icon.GetComponentInChildren<TextMeshPro>();
        text.text = "?";
        text.color = Color.white;
        icon.FindChild("ButtonSprite").GetComponent<SpriteRenderer>().color = Color.black;
        var gameOptionButton = icon.GetComponent<GameOptionButton>();
        gameOptionButton.OnClick = new();

        gameOptionButton.OnClick.AddListener((Action)(() =>
        {
            var item = ModGameOptionsMenu.GetFromBehaviour(option);
            if (item == null) return;
            
            string name = item.GetName();

            if (Main.CustomRoleValues.FindFirst(x => Translator.GetString($"{x}") == name.RemoveHtmlTags(), out CustomRoles value))
            {
                string roleName = value.IsVanilla() ? value + "EHR" : value.ToString();
                string str = Translator.GetString($"{roleName}InfoLong").FixRoleName(value);
                string infoLong;

                try { infoLong = CustomHnS.AllHnSRoles.Contains(value) ? str : str[(str.IndexOf('\n') + 1)..str.Split("\n\n")[0].Length]; }
                catch { infoLong = str; }

                GameObject.Find("PlayerOptionsMenu(Clone)").transform.FindChild("What Is This?").gameObject.SetActive(true);
                GameSettingMenuPatch.GMButtons.ForEach(x => x.gameObject.SetActive(false));

                var info = $"{value.ToColoredString()}: {infoLong}";
                GameSettingMenu.Instance.MenuDescriptionText.text = info;

                long now = Utils.TimeStamp;
                bool startCoRoutine = now > HelpShowEndTS;
                HelpShowEndTS = now + 15;
                if (startCoRoutine) Main.Instance.StartCoroutine(CoRoutine());

                IEnumerator CoRoutine()
                {
                    while (HelpShowEndTS > Utils.TimeStamp)
                        yield return new WaitForSecondsRealtime(1f);

                    GameObject gameObject = GameObject.Find("PlayerOptionsMenu(Clone)");

                    if (gameObject != null)
                    {
                        Transform findChild = gameObject.transform.FindChild("What Is This?");
                        if (findChild != null) findChild.gameObject.SetActive(false);
                    }

                    GameSettingMenuPatch.GMButtons.ForEach(x => x.gameObject.SetActive(true));
                }
            }
        }));

        gameOptionButton.interactableColor = Color.black;
        gameOptionButton.interactableHoveredColor = new Color32(0, 165, 255, 255);
        icon.localPosition += new Vector3(-0.8f, 0f, 0f);
        icon.SetAsLastSibling();
    }

    private static string GetGhostRoleTeam(CustomRoles role)
    {
        IGhostRole instance = GhostRolesManager.CreateGhostRoleInstance(role);
        if (instance == null) return string.Empty;

        Team team = instance.Team;
        if ((int)team is 1 or 2 or 4 or 8) return $"    <size=2>{GetColoredShortTeamName(team)}</size>";

        Team[] teams = (int)team switch
        {
            3 => [Team.Impostor, Team.Neutral],
            5 => [Team.Impostor, Team.Crewmate],
            6 => [Team.Neutral, Team.Crewmate],
            7 => [Team.Impostor, Team.Neutral, Team.Crewmate],
            9 => [Team.Impostor, Team.Coven],
            10 => [Team.Neutral, Team.Coven],
            11 => [Team.Impostor, Team.Neutral, Team.Coven],
            12 => [Team.Crewmate, Team.Coven],
            13 => [Team.Impostor, Team.Crewmate, Team.Coven],
            14 => [Team.Neutral, Team.Crewmate, Team.Coven],
            15 => [Team.Impostor, Team.Neutral, Team.Crewmate, Team.Coven],
            _ => []
        };

        return $"    <size=2>{string.Join('/', teams.Select(GetColoredShortTeamName))}</size>";

        string GetColoredShortTeamName(Team t) => Utils.ColorString(t.GetColor(), Translator.GetString($"ShortTeamName.{t}").ToUpper());
    }

    [HarmonyPatch(nameof(StringOption.UpdateValue))]
    [HarmonyPrefix]
    private static bool UpdateValuePrefix(StringOption __instance)
    {
        var item = ModGameOptionsMenu.GetFromBehaviour(__instance);
        if (item == null)
        {
            return true;
        }
        
        item.SetValue(__instance.GetInt());
        string name = item.GetName();

        string name1 = name;

        if (Main.CustomRoleValues.FindFirst(x => Translator.GetString($"{x}") == name1.RemoveHtmlTags(), out CustomRoles role))
        {
            if (role.ToString().Contains("GuardianAngel")) role = CustomRoles.GA;

            name = name.RemoveHtmlTags();

            switch (Options.UsePets.GetBool())
            {
                case true when role.PetActivatedAbility():
                    name += Translator.GetString("SupportsPetIndicator");
                    break;
                case false when role.OnlySpawnsWithPets():
                    name += Translator.GetString("RequiresPetIndicator");
                    Prompt.Show(Translator.GetString("Promt.RequiresPets"), () => Options.UsePets.SetValue(1), () => { });
                    break;
            }

            if (role.IsExperimental()) name += $"<size=2>{Translator.GetString("ExperimentalRoleIndicator")}</size>";
            if (role.IsGhostRole()) name += GetGhostRoleTeam(role);
            if (role.IsDevFavoriteRole()) name += "  <size=2><#00ffff>★</color></size>";

            __instance.TitleText.fontWeight = FontWeight.Black;
            __instance.TitleText.outlineColor = new(255, 255, 255, 255);
            __instance.TitleText.outlineWidth = 0.04f;
            __instance.LabelBackground.color = Utils.GetRoleColor(role);
            __instance.TitleText.color = Color.white;
            name = $"<size=3.5>{name}</size>";
            NotificationPopperPatch.AddRoleSettingsChangeMessage(item, role, true);
        }
        else
            NotificationPopperPatch.AddSettingsChangeMessage(item, true);

        __instance.TitleText.text = name;
        return false;
    }

    [HarmonyPatch(nameof(StringOption.FixedUpdate))]
    [HarmonyPrefix]
    private static bool FixedUpdatePrefix(StringOption __instance)
    {
        var item = ModGameOptionsMenu.GetFromBehaviour(__instance);
        if (item == null) return true;
        
        __instance.MinusBtn.SetInteractable(true);
        __instance.PlusBtn.SetInteractable(true);

        if (__instance.oldValue != __instance.Value && item is StringOptionItem stringOptionItem)
        {
            __instance.oldValue = __instance.Value;
            string selection = stringOptionItem.Selections[stringOptionItem.Rule.GetValueByIndex(__instance.Value)];
            if (!stringOptionItem.noTranslation) selection = Translator.GetString(selection);
            __instance.ValueText.text = selection;
        }

        return false;
    }

    [HarmonyPatch(nameof(StringOption.Increase))]
    [HarmonyPrefix]
    public static bool IncreasePrefix(StringOption __instance)
    {
        if (__instance.Value == __instance.Values.Length - 1)
        {
            __instance.Value = 0;
            __instance.UpdateValue();
            __instance.OnValueChanged?.Invoke(__instance);
            return false;
        }

        return true;
    }

    [HarmonyPatch(nameof(StringOption.Decrease))]
    [HarmonyPrefix]
    public static bool DecreasePrefix(StringOption __instance)
    {
        if (__instance.Value == 0)
        {
            __instance.Value = __instance.Values.Length - 1;
            __instance.UpdateValue();
            __instance.OnValueChanged?.Invoke(__instance);
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(GameSettingMenu))]
public static class GameSettingMenuPatch
{
    public static List<GameObject> GMButtons = [];

    private static readonly Vector3 ButtonPositionLeft = new(-3.9f, -0.55f, 0f);
    private static readonly Vector3 ButtonPositionRight = new(-2.4f, -0.55f, 0f);

    private static readonly Vector3 ButtonSize = new(0.45f, 0.35f, 1f);

    private static GameOptionsMenu TemplateGameOptionsMenu;
    private static PassiveButton TemplateGameSettingsButton;

    private static Dictionary<TabGroup, PassiveButton> ModSettingsButtons = [];
    private static Dictionary<TabGroup, GameOptionsMenu> ModSettingsTabs = [];

    public static NumberOption PresetBehaviour;

    public static long LastPresetChange;

    public static FreeChatInputField InputField;
    private static List<OptionItem> HiddenBySearch = [];
    public static Action SearchForOptionsAction;

    private static int NumImpsOnOpen = 1;
    private static int MinImpsOnOpen = 1;
    private static int MaxImpsOnOpen = 1;

    private static float VanillaKillCooldownOnOpen = 1;
    private static float ModdedKillCooldownOnOpen = 1;

    [HarmonyPatch(nameof(GameSettingMenu.Start))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void StartPostfix(GameSettingMenu __instance)
    {
        try
        {
            (OptionItem MinSetting, OptionItem MaxSetting) impLimits = Options.FactionMinMaxSettings[Team.Impostor];
            MinImpsOnOpen = impLimits.MinSetting.GetInt();
            MaxImpsOnOpen = impLimits.MaxSetting.GetInt();
            NumImpsOnOpen = Main.NormalOptions.NumImpostors;

            VanillaKillCooldownOnOpen = Main.NormalOptions.KillCooldown;
            ModdedKillCooldownOnOpen = Options.FallBackKillCooldownValue.GetFloat();
        }
        catch (Exception e) { Utils.ThrowException(e); }

        ModSettingsButtons = [];
        TabGroup[] tabGroups = Enum.GetValues<TabGroup>();

        tabGroups = Options.CurrentGameMode switch
        {
            CustomGameMode.Standard => tabGroups,
            CustomGameMode.HideAndSeek => tabGroups[..6],
            _ => tabGroups[..3]
        };

        foreach (TabGroup tab in tabGroups)
        {
            PassiveButton button = Object.Instantiate(TemplateGameSettingsButton, __instance.GameSettingsButton.transform.parent);
            button.gameObject.SetActive(true);
            button.name = "Button_" + tab;
            var label = button.GetComponentInChildren<TextMeshPro>();
            label.DestroyTranslator();
            label.text = Translator.GetString($"TabGroup.{tab}");
            label.color = Color.white;
            button.activeTextColor = button.inactiveTextColor = Color.white;
            button.selectedTextColor = new(0.7f, 0.7f, 0.7f);

            Color color = tab switch
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

            button.inactiveSprites.GetComponent<SpriteRenderer>().color = color;
            button.activeSprites.GetComponent<SpriteRenderer>().color = color;
            button.selectedSprites.GetComponent<SpriteRenderer>().color = color;

            // ReSharper disable once PossibleLossOfFraction
            Vector3 offset = new(0f, (0.3f * (((int)tab + 1) / 2)), 0f);
            button.transform.localPosition = (((int)tab + 1) % 2 == 0 ? ButtonPositionLeft : ButtonPositionRight) - offset;
            button.transform.localScale = ButtonSize;

            var buttonComponent = button.GetComponent<PassiveButton>();
            buttonComponent.OnClick = new();
            buttonComponent.OnClick.AddListener((Action)(() => __instance.ChangeTab((int)tab + 3, false)));

            ModSettingsButtons.Add(tab, button);
        }

        ModSettingsTabs = [];

        foreach (TabGroup tab in tabGroups)
        {
            GameOptionsMenu setTab = Object.Instantiate(TemplateGameOptionsMenu, __instance.GameSettingsTab.transform.parent);
            setTab.name = "tab_" + tab;
            setTab.gameObject.SetActive(false);

            ModSettingsTabs.Add(tab, setTab);
        }

        foreach (TabGroup tab in tabGroups)
        {
            if (ModSettingsButtons.TryGetValue(tab, out PassiveButton button))
                __instance.ControllerSelectable.Add(button);
        }

        HiddenBySearch.Do(x => x.SetHidden(false));
        HiddenBySearch.Clear();

        SetupExtendedUI(__instance);
    }

    // Thanks: Drakos for the preset button and search bar code (https://github.com/0xDrMoe/TownofHost-Enhanced/pull/1115)
    private static void SetupExtendedUI(GameSettingMenu __instance)
    {
        Transform parentLeftPanel = __instance.GamePresetsButton.transform.parent;
        GameObject preset = Object.Instantiate(GameObject.Find("ModeValue"), parentLeftPanel);

        preset.transform.localPosition = new(-2.55f, 0f, -2f);
        preset.transform.localScale = new(0.65f, 0.63f, 1f);
        var renderer = preset.GetComponentInChildren<SpriteRenderer>();
        renderer.color = Color.white;
        renderer.sprite = null;

        var presetTmp = preset.GetComponentInChildren<TextMeshPro>();
        presetTmp.DestroyTranslator();
        presetTmp.text = Translator.GetString($"Preset_{OptionItem.CurrentPreset + 1}");

        bool russian = TranslationController.Instance.currentLanguage.languageID == SupportedLangs.Russian;
        float size = !russian ? 2.45f : 1.45f;
        presetTmp.fontSizeMax = presetTmp.fontSizeMin = size;


        GameObject tempMinus = GameObject.Find("MinusButton").gameObject;
        GameObject gMinus = Object.Instantiate(__instance.GamePresetsButton.gameObject, preset.transform);
        gMinus.gameObject.SetActive(true);
        gMinus.transform.localScale = new(0.08f, 0.4f, 1f);

        var mLabel = gMinus.transform.Find("FontPlacer/Text_TMP").GetComponent<TextMeshPro>();
        mLabel.alignment = TextAlignmentOptions.Center;
        mLabel.DestroyTranslator();
        mLabel.text = "-";
        mLabel.transform.localPosition = new(mLabel.transform.localPosition.x, mLabel.transform.localPosition.y + 0.26f, mLabel.transform.localPosition.z);
        mLabel.color = Color.white;
        mLabel.SetFaceColor(new Color(255f, 255f, 255f));
        mLabel.transform.localScale = new(12f, 4f, 1f);

        var minus = gMinus.GetComponent<PassiveButton>();
        minus.OnClick.RemoveAllListeners();

        minus.OnClick.AddListener((Action)(() =>
        {
            if (PresetBehaviour == null)
                __instance.ChangeTab(3, false);

            LastPresetChange = Utils.TimeStamp;
            PresetBehaviour.Decrease();
        }));

        minus.activeTextColor = minus.inactiveTextColor = minus.disabledTextColor = minus.selectedTextColor = Color.white;
        minus.transform.localPosition = new(-2f, -3.37f, -4f);

        var inactiveSprites = minus.inactiveSprites.GetComponent<SpriteRenderer>();
        var activeSprites = minus.activeSprites.GetComponent<SpriteRenderer>();
        var selectedSprites = minus.selectedSprites.GetComponent<SpriteRenderer>();

        inactiveSprites.sprite = tempMinus.GetComponentInChildren<SpriteRenderer>().sprite;
        activeSprites.sprite = tempMinus.GetComponentInChildren<SpriteRenderer>().sprite;
        selectedSprites.sprite = tempMinus.GetComponentInChildren<SpriteRenderer>().sprite;

        inactiveSprites.color = new Color32(55, 59, 60, 255);
        activeSprites.color = new Color32(0, 255, 165, 255);
        selectedSprites.color = new Color32(0, 165, 255, 255);


        GameObject plusFab = Object.Instantiate(gMinus, preset.transform);
        var plusLabel = plusFab.transform.Find("FontPlacer/Text_TMP").GetComponent<TextMeshPro>();
        plusLabel.alignment = TextAlignmentOptions.Center;
        plusLabel.DestroyTranslator();
        plusLabel.text = "+";
        plusLabel.color = Color.white;
        plusLabel.transform.localPosition = new(plusLabel.transform.localPosition.x, plusLabel.transform.localPosition.y + 0.26f, plusLabel.transform.localPosition.z);
        plusLabel.transform.localScale = new(18f, 4f, 1f);

        var plus = plusFab.GetComponent<PassiveButton>();
        plus.OnClick.RemoveAllListeners();

        plus.OnClick.AddListener((Action)(() =>
        {
            if (PresetBehaviour == null)
                __instance.ChangeTab(3, false);

            LastPresetChange = Utils.TimeStamp;
            PresetBehaviour.Increase();
        }));

        plus.activeTextColor = plus.inactiveTextColor = plus.disabledTextColor = plus.selectedTextColor = Color.white;
        plus.transform.localPosition = new(-0.4f, -3.37f, -4f);


        GameObject.Find("PlayerOptionsMenu(Clone)").transform.FindChild("What Is This?").gameObject.SetActive(false);

        var gameSettingsLabel = __instance.GameSettingsButton.transform.parent.parent.FindChild("GameSettingsLabel").GetComponent<TextMeshPro>();
        gameSettingsLabel.DestroyTranslator();
        gameSettingsLabel.text = $"<size=50%>{Translator.GetString($"Mode{Options.CurrentGameMode}")}</size>\n";
        gameSettingsLabel.transform.localPosition += new Vector3(0f, 0.1f, 0f);

        if (russian)
        {
            gameSettingsLabel.transform.localScale = new(0.7f, 0.7f, 1f);
            gameSettingsLabel.transform.localPosition = new(-3.77f, 1.62f, -4);
        }

        Vector3 gameSettingsLabelPos = gameSettingsLabel.transform.localPosition;

        var gms = Enum.GetValues<CustomGameMode>()[..^1].ToList();
        gms.Remove(CustomGameMode.TheMindGame);
        
        if (SubmergedCompatibility.Loaded && Main.NormalOptions.MapId == 6)
            gms.RemoveAll(SubmergedCompatibility.IsNotSupported);

        if (Main.LIMap)
        {
            gms.Remove(CustomGameMode.CaptureTheFlag);
            gms.Remove(CustomGameMode.Quiz);
            gms.Remove(CustomGameMode.TheMindGame);
            gms.Remove(CustomGameMode.BedWars);
            gms.Remove(CustomGameMode.Deathrace);
        }
        
        const int totalCols = 3;

        GMButtons = [];

        for (var index = 0; index < gms.Count; index++)
        {
            CustomGameMode gm = gms[index];

            var gmButton = Object.Instantiate(gMinus, gameSettingsLabel.transform, true);
            gmButton.transform.localPosition = new Vector3((((index / 8) - ((totalCols - 1) / 2f)) * 1.4f) + 0.86f, gameSettingsLabelPos.y - 1.9f - (0.22f * (index % 8)), -1f);

            gmButton.transform.localScale = new(0.4f, 0.3f, 1f);
            var gmButtonTmp = gmButton.transform.Find("FontPlacer/Text_TMP").GetComponent<TextMeshPro>();
            gmButtonTmp.alignment = TextAlignmentOptions.Center;
            gmButtonTmp.DestroyTranslator();
            gmButtonTmp.text = Translator.GetString(gm.ToString()).ToUpper();
            gmButtonTmp.color = Main.GameModeColors[gm];
            gmButtonTmp.transform.localPosition = new(gameSettingsLabelPos.x + 3.35f, gameSettingsLabelPos.y - 1.62f, gameSettingsLabelPos.z);
            gmButtonTmp.transform.localScale = new(1f, 1f, 1f);

            var gmPassiveButton = gmButton.GetComponent<PassiveButton>();
            gmPassiveButton.OnClick.RemoveAllListeners();
            gmPassiveButton.OnClick.AddListener((Action)(() =>
            {
                Options.GameMode.SetValue((int)gm - 1);
                GameOptionsMenuPatch.ReloadUI();
                CustomHnS.AllHnSRoles = CustomHnS.GetAllHnsRoles(CustomHnS.GetAllHnsRoleTypes());
            }));
            gmPassiveButton.activeTextColor = gmPassiveButton.inactiveTextColor = gmPassiveButton.disabledTextColor = gmPassiveButton.selectedTextColor = Main.GameModeColors[gm];

            GMButtons.Add(gmButton);
        }


        if (!HudManager.InstanceExists) return;
        
        FreeChatInputField freeChatField = HudManager.Instance.Chat.freeChatField;
        FreeChatInputField field = Object.Instantiate(freeChatField, parentLeftPanel.parent);
        field.transform.localScale = new(0.3f, 0.59f, 1);
        field.transform.localPosition = new(-0.7f, -2.5f, -5f);
        field.textArea.outputText.transform.localScale = new(3.5f, 2f, 1f);
        field.textArea.outputText.font = plusLabel.font;

        InputField = field;

        Transform button = field.transform.FindChild("ChatSendButton");

        Transform buttonNormal = button.FindChild("Normal");
        Transform buttonHover = button.FindChild("Hover");
        Transform buttonDisabled = button.FindChild("Disabled");

        Object.Destroy(buttonNormal.FindChild("Icon").GetComponent<SpriteRenderer>());
        Object.Destroy(buttonHover.FindChild("Icon").GetComponent<SpriteRenderer>());
        Object.Destroy(buttonDisabled.FindChild("Icon").GetComponent<SpriteRenderer>());
        Object.Destroy(button.transform.FindChild("Text").GetComponent<TextMeshPro>());

        Transform buttonNormalBackground = buttonNormal.FindChild("Background");
        Transform buttonHoverBackground = buttonHover.FindChild("Background");
        Transform buttonDisabledBackground = buttonDisabled.FindChild("Background");

        buttonNormalBackground.GetComponent<SpriteRenderer>().sprite = Utils.LoadSprite("EHR.Resources.Images.SearchIconActive.png", 100f);
        buttonHoverBackground.GetComponent<SpriteRenderer>().sprite = Utils.LoadSprite("EHR.Resources.Images.SearchIconHover.png", 100f);
        buttonDisabledBackground.GetComponent<SpriteRenderer>().sprite = Utils.LoadSprite("EHR.Resources.Images.SearchIcon.png", 100f);

        if (russian)
        {
            Vector3 fixedScale = new(0.7f, 1f, 1f);
            buttonNormalBackground.transform.localScale = fixedScale;
            buttonHoverBackground.transform.localScale = fixedScale;
            buttonDisabledBackground.transform.localScale = fixedScale;
        }


        var passiveButton = button.GetComponent<PassiveButton>();

        passiveButton.OnClick = new();
        passiveButton.OnClick.AddListener((Action)(() => SearchForOptions(field)));

        SearchForOptionsAction = () =>
        {
            if (field.textArea.text != string.Empty) SearchForOptions(field);
        };

        return;

        static void SearchForOptions(FreeChatInputField textField)
        {
            if (ModGameOptionsMenu.TabIndex < 3) return;

            HiddenBySearch.Do(x => x.SetHidden(false));
            string text = textField.textArea.text.Trim().ToLower();
            var modTab = (TabGroup)(ModGameOptionsMenu.TabIndex - 3);
            OptionItem[] optionItems = Options.GroupedOptions[modTab];
            List<OptionItem> result = optionItems.Where(x => x.Parent == null && !x.IsCurrentlyHidden() && !Translator.GetString($"{x.Name}").Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
            HiddenBySearch = result;
            List<OptionItem> searchWinners = optionItems.Where(x => x.Parent == null && !x.IsCurrentlyHidden() && !result.Contains(x)).ToList();

            if (searchWinners.Count == 0 || !ModSettingsTabs.TryGetValue(modTab, out GameOptionsMenu gameSettings) || gameSettings == null)
            {
                HiddenBySearch.Clear();
                Logger.SendInGame(Translator.GetString("SearchNoResult"), Palette.Orange);
                return;
            }

            result.ForEach(x => x.SetHidden(true));

            GameOptionsMenuPatch.ReCreateSettings(gameSettings);
            textField.Clear();
        }
    }

    private static void SetDefaultButton(GameSettingMenu __instance)
    {
        __instance.GamePresetsButton.gameObject.SetActive(false);

        PassiveButton gameSettingButton = __instance.GameSettingsButton;
        gameSettingButton.transform.localPosition = new(-3f, -0.55f, 0f);

        var textLabel = gameSettingButton.GetComponentInChildren<TextMeshPro>();
        textLabel.DestroyTranslator();
        textLabel.text = Translator.GetString("TabGroup.VanillaSettings");

        gameSettingButton.activeTextColor = gameSettingButton.inactiveTextColor = Color.white;
        gameSettingButton.selectedTextColor = Color.gray;

        gameSettingButton.inactiveSprites.GetComponent<SpriteRenderer>().color = Color.black;
        gameSettingButton.activeSprites.GetComponent<SpriteRenderer>().color = Color.gray;
        gameSettingButton.selectedSprites.GetComponent<SpriteRenderer>().color = Color.black;

        gameSettingButton.transform.localPosition = ButtonPositionLeft;
        gameSettingButton.transform.localScale = ButtonSize;

        __instance.RoleSettingsButton.gameObject.SetActive(false);

        __instance.DefaultButtonSelected = gameSettingButton;
        __instance.ControllerSelectable = new();
        __instance.ControllerSelectable.Add(gameSettingButton);
    }

    [HarmonyPatch(nameof(GameSettingMenu.ChangeTab))]
    [HarmonyPrefix]
    public static bool ChangeTabPrefix(GameSettingMenu __instance, ref int tabNum, [HarmonyArgument(1)] bool previewOnly)
    {
        if (HiddenBySearch.Count > 0)
        {
            HiddenBySearch.Do(x => x.SetHidden(false));

            if (ModSettingsTabs.TryGetValue((TabGroup)(ModGameOptionsMenu.TabIndex - 3), out GameOptionsMenu gameSettings) && gameSettings != null)
                GameOptionsMenuPatch.ReCreateSettings(gameSettings);

            HiddenBySearch.Clear();
        }

        if (!previewOnly || tabNum != 1) ModGameOptionsMenu.TabIndex = tabNum;

        GameOptionsMenu settingsTab;
        PassiveButton button;

        if ((previewOnly && Controller.currentTouchType == Controller.TouchType.Joystick) || !previewOnly)
        {
            TabGroup[] tabGroups = Enum.GetValues<TabGroup>();

            foreach (TabGroup tab in tabGroups)
            {
                if (ModSettingsTabs.TryGetValue(tab, out settingsTab) && settingsTab != null)
                    settingsTab.gameObject.SetActive(false);
            }

            foreach (TabGroup tab in tabGroups)
            {
                if (ModSettingsButtons.TryGetValue(tab, out button) && button != null)
                    button.SelectButton(false);
            }
        }

        if (tabNum < 3) return true;

        var tabGroup = (TabGroup)(tabNum - 3);

        if ((previewOnly && Controller.currentTouchType == Controller.TouchType.Joystick) || !previewOnly)
        {
            __instance.PresetsTab.gameObject.SetActive(false);
            __instance.GameSettingsTab.gameObject.SetActive(false);
            __instance.RoleSettingsTab.gameObject.SetActive(false);
            __instance.GamePresetsButton.SelectButton(false);
            __instance.GameSettingsButton.SelectButton(false);
            __instance.RoleSettingsButton.SelectButton(false);

            if (ModSettingsTabs.TryGetValue(tabGroup, out settingsTab) && settingsTab != null)
            {
                settingsTab.gameObject.SetActive(true);
                __instance.MenuDescriptionText.DestroyTranslator();
                __instance.MenuDescriptionText.text = Translator.GetString("TabInfoTip");
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

        if (ModSettingsButtons.TryGetValue(tabGroup, out button) && button != null) button.SelectButton(true);

        return false;
    }

    [HarmonyPatch(nameof(GameSettingMenu.OnEnable))]
    [HarmonyPrefix]
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

        ModGameOptionsMenu.ClearDictionaries();

        ControllerManager.Instance.OpenOverlayMenu(__instance.name, __instance.BackButton, __instance.DefaultButtonSelected, __instance.ControllerSelectable);
        if (HudManager.InstanceExists) HudManager.Instance.menuNavigationPrompts.SetActive(false);
        if (Controller.currentTouchType != Controller.TouchType.Joystick) __instance.ChangeTab(1, false);

        __instance.StartCoroutine(__instance.CoSelectDefault());

        return false;
    }

    [HarmonyPatch(nameof(GameSettingMenu.Close))]
    [HarmonyPostfix]
    public static void ClosePostfix()
    {
        try
        {
            int numImpostors = Main.NormalOptions.NumImpostors;
            (OptionItem MinSetting, OptionItem MaxSetting) impLimits = Options.FactionMinMaxSettings[Team.Impostor];

            if (numImpostors != NumImpsOnOpen && MinImpsOnOpen == impLimits.MinSetting.GetInt() && MaxImpsOnOpen == impLimits.MaxSetting.GetInt())
            {
                impLimits.MinSetting.SetValue(numImpostors);
                impLimits.MaxSetting.SetValue(numImpostors);
                Logger.SendInGame(string.Format(Translator.GetString("MinMaxModdedImpCountsSettingsChangedAuto"), numImpostors));
            }


            float vanillaKillCooldown = Main.NormalOptions.KillCooldown;
            float moddedKillCooldown = Options.FallBackKillCooldownValue.GetFloat();

            bool vanillaChanged = !Mathf.Approximately(vanillaKillCooldown, VanillaKillCooldownOnOpen);
            bool moddedChanged = !Mathf.Approximately(moddedKillCooldown, ModdedKillCooldownOnOpen);

            if (!Mathf.Approximately(vanillaKillCooldown, moddedKillCooldown) && vanillaChanged && !moddedChanged)
                Options.FallBackKillCooldownValue.SetValue((int)Math.Round((Math.Round(vanillaKillCooldown, 1) / 0.5f) - 1));
        }
        catch (Exception e) { Utils.ThrowException(e); }

        try
        {
            if (Options.AutoGMRotationRecompileOnClose)
                Options.CompileAutoGMRotationSettings();
        }
        catch (Exception e) { Utils.ThrowException(e); }
        
        Options.AutoSetFactionMinMaxSettings();

        foreach (PassiveButton button in ModSettingsButtons.Values) Object.Destroy(button);
        foreach (GameOptionsMenu tab in ModSettingsTabs.Values) Object.Destroy(tab);
        foreach (GameObject button in GMButtons) Object.Destroy(button);

        ModGameOptionsMenu.ClearDictionaries();

        Main.Instance.StartCoroutine(OptionShower.GetText());
    }
}

[HarmonyPatch(typeof(FreeChatInputField), nameof(FreeChatInputField.UpdateCharCount))]
public static class FixInputChatField
{
    public static bool Prefix(FreeChatInputField __instance)
    {
        if (GameSettingMenuPatch.InputField != null && __instance == GameSettingMenuPatch.InputField)
        {
            Vector2 size = __instance.Background.size;
            size.y = Math.Max(0.62f, __instance.textArea.TextHeight + 0.2f);
            __instance.Background.size = size;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
public static class FixDarkThemeForSearchBar
{
    public static void Postfix()
    {
        if (!GameSettingMenu.Instance) return;

        FreeChatInputField field = GameSettingMenuPatch.InputField;

        if (field != null)
        {
            field.background.color = new Color32(40, 40, 40, byte.MaxValue);
            field.textArea.compoText.Color(Color.white);
            field.textArea.outputText.color = Color.white;
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSyncSettings))]
public static class RpcSyncSettingsPatch
{
    public static void Postfix()
    {
        OptionItem.SyncAllOptions();
    }

}
