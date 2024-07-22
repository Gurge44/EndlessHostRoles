using System;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using EHR.Patches;
using HarmonyLib;
using Il2CppSystem.Collections.Generic;
using TMPro;
using UnityEngine;

namespace EHR;

// Credit: https://github.com/Yumenopai/TownOfHost_Y

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
            __instance.InitializeControllerNavigation();
        }

        return false;
    }

    [HarmonyPatch(nameof(GameOptionsMenu.Initialize)), HarmonyPostfix]
    private static void InitializePostfix()
    {
        GameObject.Find("PlayerOptionsMenu(Clone)")?.transform.FindChild("Background")?.gameObject.SetActive(false);
    }

    [HarmonyPatch(nameof(GameOptionsMenu.CreateSettings)), HarmonyPrefix]
    private static bool CreateSettingsPrefix(GameOptionsMenu __instance)
    {
        if (ModGameOptionsMenu.TabIndex < 3) return true;
        __instance.scrollBar.SetYBoundsMax(CalculateScrollBarYBoundsMax());
        __instance.StartCoroutine(CoRoutine().WrapToIl2Cpp());
        return false;

        System.Collections.IEnumerator CoRoutine()
        {
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
                else if (option.IsHeader && enabled) num -= 0.25f;

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
                        ModGameOptionsMenu.OptionList.TryAdd(optionBehaviour, index);
                        break;
                    }
                    default:
                        continue;
                }

                optionBehaviour.transform.localPosition = new(0.952f, num, -2f);
                optionBehaviour.SetClickMask(__instance.ButtonClickMask);
                optionBehaviour.SetUpFromData(baseGameSetting, 20);
                ModGameOptionsMenu.OptionList.TryAdd(optionBehaviour, index);
                ModGameOptionsMenu.BehaviourList.TryAdd(index, optionBehaviour);
                optionBehaviour.gameObject.SetActive(enabled);
                optionBehaviour.OnValueChanged = new Action<OptionBehaviour>(__instance.ValueChanged);
                __instance.Children.Add(optionBehaviour);

                if (enabled) num -= 0.45f;

                if (index % 50 == 0) yield return null;
            }

            yield return null;

            __instance.ControllerSelectable.Clear();
            foreach (var x in __instance.scrollBar.GetComponentsInChildren<UiElement>())
            {
                __instance.ControllerSelectable.Add(x);
            }
        }

        float CalculateScrollBarYBoundsMax()
        {
            float num = 2.0f;
            foreach (var option in OptionItem.AllOptions)
            {
                if (option.Tab != (TabGroup)(ModGameOptionsMenu.TabIndex - 3)) continue;

                var enabled = !option.IsHiddenOn(Options.CurrentGameMode) && (option.Parent == null || (!option.Parent.IsHiddenOn(Options.CurrentGameMode) && option.Parent.GetBool()));

                if (option is TextOptionItem) num -= 0.63f;
                else if (enabled)
                {
                    if (option.IsHeader) num -= 0.25f;
                    num -= 0.45f;
                }
            }

            return -num - 1.65f;
        }
    }

    private static void OptionBehaviourSetSizeAndPosition(OptionBehaviour optionBehaviour, OptionItem option, OptionTypes type)
    {
        Vector3 positionOffset = new(0f, 0f, 0f);
        Vector3 scaleOffset = new(0f, 0f, 0f);
        Color color = new(0.8f, 0.8f, 0.8f);
        float sizeDelta_x = 5.7f;

        if (option.Parent?.Parent?.Parent != null)
        {
            scaleOffset = new(-0.18f, 0, 0);
            positionOffset = new(0.3f, 0f, 0f);
            color = new(0.8f, 0.8f, 0.2f);
            sizeDelta_x = 5.1f;
        }
        else if (option.Parent?.Parent != null)
        {
            scaleOffset = new(-0.12f, 0, 0);
            positionOffset = new(0.2f, 0f, 0f);
            color = new(0.5f, 0.2f, 0.8f);
            sizeDelta_x = 5.3f;
        }
        else if (option.Parent != null)
        {
            scaleOffset = new(-0.05f, 0, 0);
            positionOffset = new(0.1f, 0f, 0f);
            color = new(0.2f, 0.8f, 0.8f);
            sizeDelta_x = 5.5f;
        }

        var labelBackground = optionBehaviour.transform.FindChild("LabelBackground");
        labelBackground.GetComponent<SpriteRenderer>().color = color;
        labelBackground.localScale += new Vector3(0.9f, -0.2f, 0f) + scaleOffset;
        labelBackground.localPosition += new Vector3(-0.4f, 0f, 0f) + positionOffset;

        var titleText = optionBehaviour.transform.FindChild("Title Text");
        titleText.localPosition += new Vector3(-0.4f, 0f, 0f) + positionOffset;
        titleText.GetComponent<RectTransform>().sizeDelta = new(sizeDelta_x, 0.37f);
        var textMeshPro = titleText.GetComponent<TextMeshPro>();
        textMeshPro.alignment = TextAlignmentOptions.MidlineLeft;
        textMeshPro.fontStyle = FontStyles.Bold;
        textMeshPro.outlineWidth = 0.17f;

        switch (type)
        {
            case OptionTypes.Checkbox:
                optionBehaviour.transform.FindChild("Toggle").localPosition = new(1.46f, -0.042f);
                break;

            case OptionTypes.String:
                optionBehaviour.transform.FindChild("PlusButton (1)").localPosition += new Vector3(option.IsText ? 500f : 1.7f, option.IsText ? 500f : 0f, option.IsText ? 500f : 0f);
                optionBehaviour.transform.FindChild("MinusButton (1)").localPosition += new Vector3(option.IsText ? 500f : 0.9f, option.IsText ? 500f : 0f, option.IsText ? 500f : 0f);
                var valueTMP = optionBehaviour.transform.FindChild("Value_TMP (1)");
                valueTMP.localPosition += new Vector3(1.3f, 0f, 0f);
                valueTMP.GetComponent<RectTransform>().sizeDelta = new(2.3f, 0.4f);
                goto default;

            case OptionTypes.Float:
            case OptionTypes.Int:
                optionBehaviour.transform.FindChild("PlusButton").localPosition += new Vector3(option.IsText ? 500f : 1.7f, option.IsText ? 500f : 0f, option.IsText ? 500f : 0f);
                optionBehaviour.transform.FindChild("MinusButton").localPosition += new Vector3(option.IsText ? 500f : 0.9f, option.IsText ? 500f : 0f, option.IsText ? 500f : 0f);
                optionBehaviour.transform.FindChild("Value_TMP").localPosition += new Vector3(1.3f, 0f, 0f);
                goto default;

            default:
                var valueBox = optionBehaviour.transform.FindChild("ValueBox");
                valueBox.localScale += new Vector3(0.2f, 0f, 0f);
                valueBox.localPosition += new Vector3(1.3f, 0f, 0f);
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
            else if (option.IsHeader && enabled) num -= 0.25f;

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

        if (baseGameSetting != null)
        {
            baseGameSetting.Title = StringNames.Accept;
        }

        return baseGameSetting;
    }

    public static void ReloadUI(bool preset = false)
    {
        GameSettingMenu.Instance?.Close();
        LateTask.New(() =>
        {
            if (GameStates.IsLobby) GameObject.Find("Host Buttons")?.transform.FindChild("Edit")?.GetComponent<PassiveButton>()?.ReceiveClickDown();
        }, 0.1f, log: false);
        LateTask.New(() =>
        {
            if (GameStates.IsLobby) GameSettingMenu.Instance?.ChangeTab(preset ? 3 : 4, Controller.currentTouchType == Controller.TouchType.Joystick);
        }, 0.28f, log: false);
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
    private static int IncrementMultiplier
    {
        get
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return 5;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return 10;
            return 1;
        }
    }

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
                __instance.ValidRange = new(1, Crowded.MaxImpostors);
                __instance.Value = (float)Math.Round(__instance.Value, 2);
                if (DebugModeManager.AmDebugger) __instance.ValidRange.min = 0;
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
                case PresetOptionItem presetOptionItem:
                    presetOptionItem.SetValue(presetOptionItem.Rule.GetNearestIndex(__instance.GetInt()));
                    GameOptionsMenuPatch.ReloadUI(preset: true);
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

    private static string GetValueString(NumberOption __instance, float value, OptionItem item)
    {
        if (__instance.ZeroIsInfinity && Mathf.Abs(value) < 0.0001f) return "<b>∞</b>";
        return item == null ? value.ToString(__instance.FormatString) : item.GetString();
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

        var increment = IncrementMultiplier * __instance.Increment;
        if (__instance.Value + increment < __instance.ValidRange.max)
        {
            __instance.Value += increment;
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

        var increment = IncrementMultiplier * __instance.Increment;
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
    [HarmonyPatch(nameof(StringOption.Initialize)), HarmonyPrefix]
    private static bool InitializePrefix(StringOption __instance)
    {
        if (ModGameOptionsMenu.OptionList.TryGetValue(__instance, out var index))
        {
            var item = OptionItem.AllOptions[index];
            var name = item.GetName();
            if (Enum.GetValues<CustomRoles>().Find(x => Translator.GetString($"{x}") == name.RemoveHtmlTags(), out var role))
            {
                if (Options.UsePets.GetBool() && role.PetActivatedAbility()) name += Translator.GetString("SupportsPetIndicator");
                if (!Options.UsePets.GetBool() && role.OnlySpawnsWithPets()) name += Translator.GetString("RequiresPetIndicator");
                __instance.TitleText.fontWeight = FontWeight.Black;
                __instance.TitleText.outlineColor = new(255, 255, 255, 255);
                __instance.TitleText.outlineWidth = 0.04f;
                __instance.LabelBackground.color = Utils.GetRoleColor(role);
                __instance.TitleText.color = Color.white;
                name = name.RemoveHtmlTags();
                name = $"<size=3.5>{name}</size>";
            }

            __instance.TitleText.text = name;
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
            item.SetValue(__instance.GetInt());
            if (item.Name == "GameMode") GameOptionsMenuPatch.ReloadUI();
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
                    __instance.ValueText.text = stringOptionItem.noTranslation
                        ? stringOptionItem.Selections[stringOptionItem.Rule.GetValueByIndex(__instance.Value)]
                        : Translator.GetString(stringOptionItem.Selections[stringOptionItem.Rule.GetValueByIndex(__instance.Value)]);
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
            __instance.OnValueChanged?.Invoke(__instance);
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
            __instance.OnValueChanged?.Invoke(__instance);
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(GameSettingMenu))]
public class GameSettingMenuPatch
{
    private static readonly Vector3 ButtonPositionLeft = new(-3.9f, -0.4f, 0f);
    private static readonly Vector3 ButtonPositionRight = new(-2.4f, -0.4f, 0f);

    private static readonly Vector3 ButtonSize = new(0.45f, 0.4f, 1f);
    // private static readonly Vector3 ButtonSize = new(0.45f, 0.6f, 1f);

    private static GameOptionsMenu TemplateGameOptionsMenu;
    private static PassiveButton TemplateGameSettingsButton;

    static System.Collections.Generic.Dictionary<TabGroup, PassiveButton> ModSettingsButtons = [];
    static System.Collections.Generic.Dictionary<TabGroup, GameOptionsMenu> ModSettingsTabs = [];

    [HarmonyPatch(nameof(GameSettingMenu.Start)), HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void StartPostfix(GameSettingMenu __instance)
    {
        ModSettingsButtons = [];
        foreach (var tab in Enum.GetValues<TabGroup>())
        {
            var button = Object.Instantiate(TemplateGameSettingsButton, __instance.GameSettingsButton.transform.parent);
            button.gameObject.SetActive(true);
            button.name = "Button_" + tab;
            var label = button.GetComponentInChildren<TextMeshPro>();
            label.DestroyTranslator();
            label.text = Translator.GetString($"TabGroup.{tab}");
            label.color = Color.white;
            button.activeTextColor = button.inactiveTextColor = Color.white;
            button.selectedTextColor = new(0.7f, 0.7f, 0.7f);

            // var activeButton = Utils.LoadSprite($"EHR.Resources.Images.TabIcon_{tab}.png", 100f);
            // button.inactiveSprites.GetComponent<SpriteRenderer>().sprite = activeButton /*Utils.LoadSprite($"EHR.Resources.Tab_Small_{tab}.png", 100f)*/;
            // button.activeSprites.GetComponent<SpriteRenderer>().sprite = activeButton;
            // button.selectedSprites.GetComponent<SpriteRenderer>().sprite = activeButton;
            Color color = tab switch
            {
                TabGroup.SystemSettings => new(0.2f, 0.2f, 0.2f),
                TabGroup.GameSettings => new(0.2f, 0.4f, 0.3f),
                TabGroup.TaskSettings => new(0.4f, 0.2f, 0.5f),
                TabGroup.ImpostorRoles => new(0.5f, 0.2f, 0.2f),
                TabGroup.CrewmateRoles => new(0.2f, 0.4f, 0.5f),
                TabGroup.NeutralRoles => new(0.5f, 0.4f, 0.2f),
                TabGroup.Addons => new(0.5f, 0.2f, 0.4f),
                TabGroup.OtherRoles => new(0.4f, 0.4f, 0.4f),
                _ => new(0.3f, 0.3f, 0.3f)
            };
            button.inactiveSprites.GetComponent<SpriteRenderer>().color = color;
            button.activeSprites.GetComponent<SpriteRenderer>().color = color;
            button.selectedSprites.GetComponent<SpriteRenderer>().color = color;

            Vector3 offset = new(0.0f, 0.4f * (((int)tab + 1) / 2), 0.0f);
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

        ModSettingsTabs = [];
        foreach (var tab in Enum.GetValues<TabGroup>())
        {
            var setTab = Object.Instantiate(TemplateGameOptionsMenu, __instance.GameSettingsTab.transform.parent);
            setTab.name = "tab_" + tab;
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
        gameSettingButton.transform.localPosition = new(-3f, -0.4f, 0f);
        var textLabel = gameSettingButton.GetComponentInChildren<TextMeshPro>();
        // textLabel.DestroyTranslator();
        // textLabel.text = "";
        textLabel.text = Translator.GetString("TabGroup.VanillaSettings");
        gameSettingButton.activeTextColor = gameSettingButton.inactiveTextColor = Color.black;
        gameSettingButton.selectedTextColor = Color.blue;

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

        TabGroup tabGroup = (TabGroup)(tabNum - 3);
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

        if (ModSettingsButtons.TryGetValue(tabGroup, out button) && button != null)
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
    public static void ClosePostfix()
    {
        foreach (var button in ModSettingsButtons.Values)
            Object.Destroy(button);
        foreach (var tab in ModSettingsTabs.Values)
            Object.Destroy(tab);
        ModSettingsButtons = [];
        ModSettingsTabs = [];
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