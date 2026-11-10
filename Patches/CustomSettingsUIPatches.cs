using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR;
using EHR.Gamemodes;
using EHR.Modules;
using EHR.Patches;
using EHR.Roles;
using HarmonyLib;
using Rewired;
using TMPro;
using UnityEngine;

// ReSharper disable PossibleLossOfFraction

[HarmonyPatch]
internal static class CustomSettingsUIPatches
{
    [HarmonyPatch(typeof(GamePresetsTab))]
    internal static class GamePresetsTabPatches
    {
        public static PassiveButton TemplatePresetButton;
        public static TransitionOpen TemplateConfirmPopUp;
        public static readonly Dictionary<CustomGameMode, GameObject> Buttons = [];
        public static GameObject NDIntegrationButton;

        private static readonly Dictionary<CustomGameMode, string> SpriteKey = new()
        {
            [CustomGameMode.Standard] = "ExamineBody",
            [CustomGameMode.SoloPVP] = "Attack",
            [CustomGameMode.FFA] = "Assassinate",
            [CustomGameMode.StopAndGo] = "TimeMaster",
            [CustomGameMode.HotPotato] = "Bomb",
            [CustomGameMode.HideAndSeek] = "Lighter",
            [CustomGameMode.Speedrun] = "Timer",
            [CustomGameMode.CaptureTheFlag] = "Tag",
            [CustomGameMode.NaturalDisasters] = "Tornado",
            [CustomGameMode.RoomRush] = "TPToLoca",
            [CustomGameMode.KingOfTheZones] = "Scanner",
            [CustomGameMode.Quiz] = "Camo",
            [CustomGameMode.TheMindGame] = "Puttpuer",
            [CustomGameMode.BedWars] = "Drink",
            [CustomGameMode.Deathrace] = "Gaulois",
            [CustomGameMode.Mingle] = "InspectorIcon",
            [CustomGameMode.Snowdown] = "Snowdown",
            [CustomGameMode.DoomTag] = "Hangman"
        };

        [HarmonyPatch(nameof(GamePresetsTab.OnEnable))]
        [HarmonyPrefix]
        private static bool OnEnable(GamePresetsTab __instance)
        {
            try
            {
                CustomGameMode currentGameMode = Options.CurrentGameMode;

                if (Buttons.Count > 0)
                {
                    foreach ((CustomGameMode customGameMode, GameObject button) in Buttons)
                        button.GetComponent<PassiveButton>().SelectButton(customGameMode == currentGameMode);
                
                    NDIntegrationButton.GetComponent<PassiveButton>().SelectButton(Options.IntegrateNaturalDisasters.GetBool());
                    Object.Destroy(__instance.StandardPresetButton.gameObject);
                    Object.Destroy(__instance.SecondPresetButton.gameObject);
                    __instance.StandardPresetButton = null;
                    __instance.SecondPresetButton = null;
                    __instance.PresetDescriptionText.DestroyTranslator();
                    __instance.PresetDescriptionText.text = Translator.GetString($"{(CustomRoleSelector.GameModeRoles.TryGetValue(currentGameMode, out CustomRoles role) ? role.ToString() : currentGameMode.ToString())}Info").ToUpper();
                    return false;
                }

                PassiveButton original = __instance.StandardPresetButton;
                string[] childrenNames = ["Active", "Inactive", "Selected", "InactiveSelected"];

                foreach (CustomGameMode customGameMode in Main.CustomGameModeValues[..^1])
                {
                    if (SubmergedCompatibility.Loaded && Main.NormalOptions.MapId == 6 && customGameMode == CustomGameMode.NaturalDisasters) continue;
                    if (Main.LIMap && customGameMode is CustomGameMode.CaptureTheFlag or CustomGameMode.Quiz or CustomGameMode.BedWars or CustomGameMode.Deathrace) continue;
                    int num = (int)customGameMode - 1;
                    GameObject button = Object.Instantiate(original.gameObject, original.transform.parent);
                    button.name = $"{customGameMode}PresetButton";
                    button.transform.localScale *= 0.3f;
                    button.transform.localPosition = new Vector3(-3.05f + (num % 8 * 0.9f), 1.35f - (1.25f * (num / 8)), original.transform.localPosition.z);
                    var passiveButton = button.GetComponent<PassiveButton>();
                    passiveButton.SelectButton(currentGameMode == customGameMode);
                    passiveButton.OnClick.RemoveAllListeners();
                    passiveButton.OnClick.AddListener(() =>
                    {
                        Options.GameMode.SetValue(num);
                        foreach ((CustomGameMode gameMode, GameObject gameObject) in Buttons)
                            gameObject.GetComponent<PassiveButton>().SelectButton(gameMode == customGameMode);
                        __instance.PresetDescriptionText.text = Translator.GetString($"{(CustomRoleSelector.GameModeRoles.TryGetValue(customGameMode, out CustomRoles customRoles) ? customRoles.ToString() : customGameMode.ToString())}Info").ToUpper();
                        GameSettingMenu.Instance.RoleSettingsButton.gameObject.SetActive(customGameMode is CustomGameMode.Standard or CustomGameMode.HideAndSeek);
                        GameOptionsMenuPatches.SetAllDirty();
                    
                        if (customGameMode == CustomGameMode.NaturalDisasters && Options.IntegrateNaturalDisasters.GetBool())
                        {
                            Options.IntegrateNaturalDisasters.SetValue(0);
                            NDIntegrationButton.GetComponent<PassiveButton>().SelectButton(false);
                        }
                    });
                    passiveButton.OnMouseOver.RemoveAllListeners();
                    passiveButton.OnMouseOver.AddListener(() => __instance.PresetDescriptionText.text = Translator.GetString($"{(CustomRoleSelector.GameModeRoles.TryGetValue(customGameMode, out CustomRoles customRoles) ? customRoles.ToString() : customGameMode.ToString())}Info").ToUpper());
                    passiveButton.OnMouseOut.RemoveAllListeners();
                    passiveButton.OnMouseOut.AddListener(() => __instance.PresetDescriptionText.text = Translator.GetString($"{(CustomRoleSelector.GameModeRoles.TryGetValue(currentGameMode, out CustomRoles customRoles) ? customRoles.ToString() : currentGameMode.ToString())}Info").ToUpper());
                
                    Color color = Main.GameModeColors.GetValueOrDefault(customGameMode, Color.white);

                    foreach (string name in childrenNames)
                    {
                        Transform transform = button.transform.Find(name);
                        transform.Find("Label").GetComponent<SpriteRenderer>().color = color;
                        transform.Find("Frame").GetComponent<SpriteRenderer>().color = color;
                        transform.Find("Background").GetComponent<SpriteRenderer>().sprite = Utils.LoadSprite($"EHR.Resources.Images.Skills.{SpriteKey[customGameMode]}.png", 35f);
                    }
                
                    var textMeshPro = button.transform.Find("ModeText").GetComponent<TextMeshPro>();
                    textMeshPro.DestroyTranslator();
                    textMeshPro.text = $"<font=\"DIN_Pro_Bold_700 SDF\">{Translator.GetString(customGameMode.ToString()).ToUpper()}</font>";
                    textMeshPro.fontWeight = FontWeight.Bold;
                    textMeshPro.color = Color.white;
                
                    Buttons[customGameMode] = button;
                }

                // ND integration button
                {
                    GameObject button = Object.Instantiate(original.gameObject, original.transform.parent);
                    button.name = "NDIntegrationPresetButton";
                    button.transform.localScale *= 0.3f;
                    button.transform.localPosition = new Vector3(3.25f, -1.15f, original.transform.localPosition.z);
                    var passiveButton = button.GetComponent<PassiveButton>();
                    passiveButton.SelectButton(Options.IntegrateNaturalDisasters.GetBool());
                    passiveButton.OnClick.RemoveAllListeners();
                    passiveButton.OnClick.AddListener(() =>
                    {
                        if (Options.CurrentGameMode == CustomGameMode.NaturalDisasters) return;
                        Options.IntegrateNaturalDisasters.SetValue(Options.IntegrateNaturalDisasters.GetBool() ? 0 : 1);
                        passiveButton.SelectButton(Options.IntegrateNaturalDisasters.GetBool());
                        GameOptionsMenuPatches.SetAllDirty();
                    });
                    passiveButton.OnMouseOver.RemoveAllListeners();
                    passiveButton.OnMouseOver.AddListener(() => __instance.PresetDescriptionText.text = Translator.GetString("IntegrateNaturalDisasters").ToUpper());
                    passiveButton.OnMouseOut.RemoveAllListeners();
                    passiveButton.OnMouseOut.AddListener(() => __instance.PresetDescriptionText.text = Translator.GetString($"{(CustomRoleSelector.GameModeRoles.TryGetValue(Options.CurrentGameMode, out CustomRoles customRoles) ? customRoles.ToString() : Options.CurrentGameMode.ToString())}Info").ToUpper());
                
                    var textMeshPro = button.transform.Find("ModeText").GetComponent<TextMeshPro>();
                    textMeshPro.DestroyTranslator();
                    textMeshPro.text = string.Empty;
                
                    var text = Object.Instantiate(textMeshPro, textMeshPro.transform.parent);
                    text.transform.localPosition += new Vector3(0f, 1.6f, 0f);
                    text.GetComponent<RectTransform>().sizeDelta = new(2.5f, 2.5f);
                    text.text = $"<font=\"DIN_Pro_Bold_700 SDF\"><size=150%>{Translator.GetString("NDIntegrationShort").ToUpper()}</size></font>";

                    Color color = Main.GameModeColors[CustomGameMode.NaturalDisasters];

                    foreach (string name in childrenNames)
                    {
                        Transform transform = button.transform.Find(name);
                        transform.Find("Frame").GetComponent<SpriteRenderer>().color = color;
                        Object.Destroy(transform.Find("Label").GetComponent<SpriteRenderer>().gameObject);
                        Object.Destroy(transform.Find("Background").GetComponent<SpriteRenderer>().gameObject);
                    }
                
                    NDIntegrationButton = button;
                }

                if (!TemplatePresetButton)
                {
                    TemplatePresetButton = Object.Instantiate(original, original.transform.parent);
                    TemplatePresetButton.gameObject.SetActive(false);
                }

                if (!TemplateConfirmPopUp)
                {
                    TemplateConfirmPopUp = Object.Instantiate(__instance.ConfirmPresetPopUp, __instance.ConfirmPresetPopUp.transform.parent);
                    TemplateConfirmPopUp.gameObject.SetActive(false);
                }

                Object.Destroy(original.gameObject);
                Object.Destroy(__instance.SecondPresetButton.gameObject);
                __instance.StandardPresetButton = null;
                __instance.SecondPresetButton = null;
                __instance.PresetDescriptionText.DestroyTranslator();
                __instance.PresetDescriptionText.text = Translator.GetString($"{(CustomRoleSelector.GameModeRoles.TryGetValue(currentGameMode, out CustomRoles roles) ? roles.ToString() : currentGameMode.ToString())}Info").ToUpper();
            }
            catch { }
            
            return false;
        }

        [HarmonyPatch(nameof(GamePresetsTab.Start))]
        [HarmonyPrefix]
        private static bool Start(GamePresetsTab __instance)
        {
            return false;
        }

        [HarmonyPatch(nameof(GamePresetsTab.SetSelectedText))]
        [HarmonyPrefix]
        private static bool SetSelectedText(GamePresetsTab __instance)
        {
            return false;
        }
    }

    [HarmonyPatch(typeof(GameOptionsMenu))]
    internal static class GameOptionsMenuPatches
    {
        private static readonly Dictionary<string, List<OptionItem>> Sections = [];
        private static readonly HashSet<string> DirtySections = [];
        private static string BasicSettingsSection;

        public static void InitializeSections()
        {
            List<int> irrelevantOptionIds =
            [
                .. Options.CustomRoleSpawnChances.Values
                    .Concat(Options.CustomRoleSpawnChances.Values.SelectMany(GetAllChildren))
                    .Concat(Options.CustomRoleCounts.Values)
                    .Concat(Options.AddonGuessSettings.Values)
                    .Concat(Options.CustomAdtRoleSpawnRate.Values)
                    .Concat(Options.AddonCanBeSettings.Values.SelectMany(x => new[]
                    {
                        x.Imp,
                        x.Neutral,
                        x.Crew,
                        x.Coven
                    }))
                    .Select(x => x.Id),

                0,
                1
            ];

            BasicSettingsSection = $"MODDED_{Translator.GetString("BasicSettings").ToUpper()}";
            Sections[BasicSettingsSection] = [];

            foreach ((EHR.TabGroup tab, OptionItem[] options) in Options.GroupedOptions)
            {
                if (tab == EHR.TabGroup.OtherRoles) continue;
                bool gameSettings = tab == EHR.TabGroup.GameSettings;
                string section = null;
                bool any = false;

                foreach (OptionItem optionItem in options)
                {
                    if (irrelevantOptionIds.Contains(optionItem.Id) || optionItem.GameMode is not (CustomGameMode.Standard or CustomGameMode.All) && !gameSettings) continue;

                    if (optionItem is TextOptionItem textOptionItem && !textOptionItem.Name.StartsWith("ROT."))
                    {
                        if (!any && !string.IsNullOrEmpty(section)) Sections.Remove(section);
                        section = $"MODDED_{textOptionItem.GetName(true).Trim('★', ' ').ToUpper()}";
                        any = false;
                        continue;
                    }

                    if (string.IsNullOrEmpty(section))
                        section = $"MODDED_{Translator.GetString(gameSettings ? "GameModeSettings" : $"TabGroup.{tab}").ToUpper()}";

                    Sections.TryAdd(section, []);
                    Sections[section].Add(optionItem);
                    any = true;
                }
            }
        }
        
        public static void SetAllDirty() 
        {
            DirtySections.UnionWith(Sections.Keys);
        }

        [HarmonyPatch(nameof(GameOptionsMenu.OpenMenu))]
        [HarmonyPrefix]
        static bool OpenMenu(GameOptionsMenu __instance)
        {
            ControllerManager.Instance.OpenOverlayMenu(__instance.name, __instance.BackButton, __instance.ControllerSelectable[0], __instance.ControllerSelectable);
            return false;
        }

        [HarmonyPatch(nameof(GameOptionsMenu.Initialize))]
        [HarmonyPrefix]
        private static bool Initialize(GameOptionsMenu __instance)
        {
            if (__instance.name.StartsWith("IDX_"))
            {
                int idx = int.Parse(__instance.name[4..]);
                if (idx == 1000) return false;
                __instance.name = Sections.Keys.ElementAt(idx - 10);
            }

            if (__instance.name == BasicSettingsSection) return true;

            if (__instance.Children != null && __instance.Children.Count != 0)
            {
                if (DirtySections.Remove(__instance.name))
                    ValueChanged(__instance, null);

                return false;
            }

            Object.Destroy(__instance.MapPicker.gameObject);
            __instance.Children = new();
            __instance.CreateSettings();
            __instance.cachedData = GameOptionsManager.Instance.CurrentGameOptions;

            for (var index = 0; index < __instance.Children.Count; ++index)
            {
                OptionBehaviour child = __instance.Children[index];
#if IL2CPP
                child.OnValueChanged = (Action<OptionBehaviour>)__instance.ValueChanged;
#else
                child.OnValueChanged = __instance.ValueChanged;
#endif
                if (AmongUsClient.Instance && !AmongUsClient.Instance.AmHost)
                    child.SetAsPlayer();
            }

            __instance.InitializeControllerNavigation();
            return false;
        }

        [HarmonyPatch(nameof(GameOptionsMenu.CreateSettings))]
        [HarmonyPrefix]
        private static bool CreateSettings(GameOptionsMenu __instance)
        {
            if (__instance.name == BasicSettingsSection)
                return true;

            var y = 2f;

            if (Sections.TryGetValue(__instance.name, out List<OptionItem> options))
            {
                StringOption back = Object.Instantiate(__instance.stringOptionOrigin, Vector3.zero, Quaternion.identity, __instance.settingsContainer);
                back.name = $"BACK_BUTTON_{__instance.name}";
                back.transform.localPosition = new Vector3(0.952f, y, -2f);
                back.SetClickMask(__instance.ButtonClickMask);
                back.SetUpFromData(null, 20);

                Object.Destroy(back.ValueText.gameObject);
                Object.Destroy(back.transform.Find("ValueBox").gameObject);
                Object.Destroy(back.PlusBtn.gameObject);
                back.ValueText = null;
                back.PlusBtn = null;

                back.MinusBtn.OnClick = new();
                back.MinusBtn.OnClick.AddListener(() => GameSettingMenu.Instance.ChangeTab(1, false));
                back.MinusBtn.transform.localPosition -= new Vector3(3f, 0f, 0f);
                Vector3 scale = back.MinusBtn.transform.localScale;
                back.MinusBtn.transform.localScale = new Vector3(scale.x * 7f, scale.y, scale.z);
                var text = back.MinusBtn.GetComponentInChildren<TextMeshPro>();
                Vector3 textScale = text.transform.localScale;
                text.transform.localScale = new Vector3(textScale.x / 7f, textScale.y, textScale.z);
                text.autoSizeTextContainer = false;
                text.GetComponent<RectTransform>().sizeDelta = new(3f, 0.37f);
                text.DestroyTranslator();
                text.text = $"⬅  {Translator.GetString("BackToList")}";
                back.TitleText.enabled = false;
                back.LabelBackground.enabled = false;
                back.gameObject.SetActive(true);

                __instance.Children.Add(back);

                for (var i = 0; i < options.Count; i++)
                {
                    OptionItem optionItem = options[i];

                    if (!InstantiateOptionBehaviour(optionItem, __instance.settingsContainer, out OptionBehaviour optionBehaviour, out BaseGameSetting baseGameSetting, __instance.checkboxOrigin, __instance.numberOptionOrigin, __instance.stringOptionOrigin))
                        continue;

                    Vector3 positionOffset = new(0f, 0f, 0f);
                    Vector3 scaleOffset = new(0f, 0f, 0f);
                    Color color = new(0.35f, 0.35f, 0.35f);
                    var sizeDeltaX = 5.7f;

                    var depth = 0;
                    OptionItem parent = optionItem.Parent;

                    while (parent != null && depth < 3)
                    {
                        depth++;
                        parent = parent.Parent;
                    }

                    if (depth > 0)
                    {
                        scaleOffset.x = -0.06f * depth;
                        positionOffset.x = 0.1f * depth;
                        sizeDeltaX = 5.7f - (0.2f * depth);

                        color = depth switch
                        {
                            1 => new(0f, 0f, 0.35f),
                            2 => new(0.35f, 0.35f, 0f),
                            _ => new(0.35f, 0f, 0f)
                        };
                    }

                    SpriteRenderer labelBackground = optionBehaviour.LabelBackground;
                    labelBackground.color = color;
                    Transform labelBackgroundTransform = labelBackground.transform;
                    labelBackgroundTransform.localScale += new Vector3(0.9f, -0.2f, 0f) + scaleOffset;
                    labelBackgroundTransform.localPosition += new Vector3(-0.4f, 0f, 0f) + positionOffset;

#if IL2CPP
                    TextMeshPro titleText = GetTitleTextAndSetPositions(optionBehaviour, optionItem);
#else
                    TextMeshPro titleText = GetTitleTextAndSetPositions(optionBehaviour);
#endif
                    titleText.transform.localPosition += new Vector3(-0.4f, 0f, 0f) + positionOffset;
                    titleText.GetComponent<RectTransform>().sizeDelta = new(sizeDeltaX, 0.37f);
                    titleText.alignment = TextAlignmentOptions.MidlineLeft;
                    titleText.fontStyle = FontStyles.Bold;
                    titleText.outlineWidth = 0.17f;

                    bool hidden = optionItem.IsCurrentlyHidden(checkParentEnabled: true);
                    
                    if (!hidden)
                    {
                        y -= 0.45f;
                        if (optionItem.IsHeader) y -= 0.18f;
                    }
                    
                    optionBehaviour.transform.localPosition = new Vector3(0.952f, y, -2f);
                    optionBehaviour.SetClickMask(__instance.ButtonClickMask);
                    optionBehaviour.SetUpFromData(baseGameSetting, 20);
                    optionBehaviour.gameObject.SetActive(!hidden);
                    __instance.Children.Add(optionBehaviour);
                }
                
                y -= 0.45f;
            }
            else
            {
                var index = 0;

                foreach (string sectionTitle in Sections.Keys)
                {
                    StringOption section = Object.Instantiate(__instance.stringOptionOrigin, Vector3.zero, Quaternion.identity, __instance.settingsContainer);
                    section.name = sectionTitle;
                    section.transform.localPosition = new Vector3(0.952f, y, -2f);
                    section.SetClickMask(__instance.ButtonClickMask);
                    section.SetUpFromData(null, 20);

                    Object.Destroy(section.ValueText.gameObject);
                    Object.Destroy(section.transform.Find("ValueBox").gameObject);
                    Object.Destroy(section.PlusBtn.gameObject);
                    section.ValueText = null;
                    section.PlusBtn = null;

                    int currentIndex = index;
                    section.MinusBtn.OnClick = new();
                    section.MinusBtn.OnClick.AddListener(() => GameSettingMenu.Instance.ChangeTab(10 + currentIndex, false));
                    section.MinusBtn.transform.localPosition += new Vector3(1.3f, 0f, 0f);
                    Vector3 scale = section.MinusBtn.transform.localScale;
                    section.MinusBtn.transform.localScale = new Vector3(scale.x * 2f, scale.y, scale.z);
                    var text = section.MinusBtn.GetComponentInChildren<TextMeshPro>();
                    text.DestroyTranslator();
                    text.text = "➡";
                    section.TitleText.DestroyTranslator();
                    section.TitleText.GetComponent<RectTransform>().sizeDelta = new(5.7f, 0.37f);
                    section.TitleText.alignment = TextAlignmentOptions.MidlineLeft;
                    section.TitleText.fontStyle = FontStyles.Bold;
                    section.TitleText.outlineWidth = 0.17f;
                    section.LabelBackground.enabled = false;
                    section.gameObject.SetActive(true);

                    __instance.Children.Add(section);

                    y -= 0.45f;
                    index++;
                }
            }

#if IL2CPP
            foreach (var uiElement in __instance.scrollBar.GetComponentsInChildren<UiElement>())
                __instance.ControllerSelectable.Add(uiElement);
#else
            __instance.ControllerSelectable.AddRange(__instance.scrollBar.GetComponentsInChildren<UiElement>());
#endif
            __instance.scrollBar.SetYBoundsMax(-y - 1.65f);
            return false;
        }

        [HarmonyPatch(nameof(GameOptionsMenu.Update))]
        [HarmonyPrefix]
        private static void PreUpdate(GameOptionsMenu __instance)
        {
            if (!HudManager.InstanceExists || !__instance.gameObject.activeSelf || !GameSettingMenu.Instance) return;
            __instance.scrollBar.enabled = !HudManager.Instance.Chat.IsOpenOrOpening;
        }

        [HarmonyPatch(nameof(GameOptionsMenu.ValueChanged))]
        [HarmonyPrefix]
        private static bool ValueChanged(GameOptionsMenu __instance, OptionBehaviour option)
        {
            if (option)
            {
                if (!OptionItem.BehaviourNameToItem.TryGetValue(option.name, out OptionItem item)) return true;
                if (item.Children.Count == 0) return false;
            }
            
            var y = 2f;

            foreach (OptionBehaviour optionBehaviour in __instance.Children)
            {
                if (!OptionItem.BehaviourNameToItem.TryGetValue(optionBehaviour.name, out OptionItem optionItem)) continue;

#if IL2CPP
                switch (optionItem)
                {
                    case BooleanOptionItem when optionBehaviour.TryCastFast(out ToggleOption toggleOption):
                        toggleOption.CheckMark.enabled = optionItem.GetBool();
                        break;
                    case StringOptionItem when optionBehaviour.TryCastFast(out StringOption stringOption):
                        stringOption.Value = optionItem.GetInt();
                        stringOption.oldValue = stringOption.Value;
                        stringOption.ValueText.SetText(optionItem.GetString());
                        stringOption.TitleText.SetText(optionItem.GetName());
                        break;
                    case FloatOptionItem:
                    case IntegerOptionItem:
                        NumberOption numberOption = optionBehaviour.CastFast<NumberOption>();
                        numberOption.Value = optionItem.GetFloat();
                        break;
                }
#else
                switch (optionBehaviour)
                {
                    case ToggleOption toggleOption:
                        toggleOption.CheckMark.enabled = optionItem.GetBool();
                        break;
                    case StringOption stringOption:
                        stringOption.Value = optionItem.GetInt();
                        stringOption.oldValue = stringOption.Value;
                        stringOption.ValueText.SetText(optionItem.GetString());
                        stringOption.TitleText.SetText(optionItem.GetName());
                        break;
                    case NumberOption numberOption:
                        numberOption.Value = optionItem.GetFloat();
                        break;
                }
#endif

                bool hidden = optionItem.IsCurrentlyHidden(checkParentEnabled: true);
                optionBehaviour.gameObject.SetActive(!hidden);
                if (hidden) continue;
                y -= 0.45f;
                if (optionItem.IsHeader) y -= 0.18f;

                optionBehaviour.transform.localPosition = new Vector3(0.952f, y, -2f);
            }

            y -= 0.45f;
            __instance.scrollBar.SetYBoundsMax(-y - 1.65f);
            return false;
        }
    }

    [HarmonyPatch(typeof(RolesSettingsMenu))]
    private static class RolesSettingsMenuPatches
    {
        private static StringOption BackButton;
        private static readonly int StencilComp = Shader.PropertyToID("_StencilComp");
        private static readonly int Stencil = Shader.PropertyToID("_Stencil");
        private static readonly Dictionary<EHR.TabGroup, GameObject> Tabs = [];
        private static readonly Dictionary<EHR.TabGroup, int> TabChildrenCount = [];
        private static readonly Dictionary<Vector3, PassiveButton> TabButtons = [];
        private static Vector3 TabPosition;
        private static Transform TempParent;

        [HarmonyPatch(nameof(RolesSettingsMenu.OnEnable))]
        [HarmonyPrefix]
        public static bool OnEnable(RolesSettingsMenu __instance)
        {
            if (Tabs.Count > 0)
            {
                __instance.roleChances = new();
                __instance.QuotaTabSelectables = new();
                
                foreach (GameObject gameObject in Tabs.Values)
                {
                    gameObject.transform.SetParent(__instance.scrollBar.Inner);
                    gameObject.transform.localPosition = TabPosition;

                    foreach (RoleOptionSetting roleOptionSetting in gameObject.GetComponentsInChildren<RoleOptionSetting>(true))
                    {
                        __instance.roleChances.Add(roleOptionSetting);
#if IL2CPP
                        roleOptionSetting.OnValueChanged = (Action<OptionBehaviour>)(obj => ValueChanged(obj, true));
                        
                        foreach (var button in roleOptionSetting.ControllerSelectable)
                            __instance.QuotaTabSelectables.Add(button);
#else
                        roleOptionSetting.OnValueChanged = obj => ValueChanged(obj, true);
                        __instance.QuotaTabSelectables.AddRange(roleOptionSetting.ControllerSelectable);
#endif
                    }
                }

                CustomGameMode currentGameMode = Options.CurrentGameMode;
                __instance.roleTabs = new();
                    
                foreach ((Vector3 position, PassiveButton button) in TabButtons)
                {
                    button.transform.SetParent(__instance.roleSettingsTabParent);
                    button.transform.localPosition = position;
                    button.gameObject.SetActive(currentGameMode == CustomGameMode.Standard);
                    __instance.roleTabs.Add(button);
                }
                    
                __instance.roleTabs[^1].gameObject.SetActive(currentGameMode == CustomGameMode.HideAndSeek);
                    
                if (__instance.AllButton)
                    TidyStuff(__instance);

                Tabs[currentGameMode == CustomGameMode.HideAndSeek ? (EHR.TabGroup)100 : EHR.TabGroup.ImpostorRoles].SetActive(true);
            }
            else
                __instance.RoleChancesSettings.SetActive(true);
            __instance.AdvancedRolesSettings.SetActive(false);
            RefreshChildren();
            foreach (SpriteRenderer componentsInChild in __instance.AdvancedRolesSettings.GetComponentsInChildren<SpriteRenderer>(true))
                componentsInChild.material.SetInt(PlayerMaterial.MaskLayer, 20);
            foreach (TextMeshPro componentsInChild in __instance.AdvancedRolesSettings.GetComponentsInChildren<TextMeshPro>(true))
            {
                componentsInChild.fontMaterial.SetFloat(StencilComp, 3f);
                componentsInChild.fontMaterial.SetFloat(Stencil, 20f);
            }

            return false;
        }

        [HarmonyPatch(nameof(RolesSettingsMenu.OpenMenu))]
        [HarmonyPrefix]
        static bool OpenMenu(RolesSettingsMenu __instance, bool controllerSelected = true)
        {
            if (controllerSelected)
            {
                ControllerManager.Instance.OpenOverlayMenu(__instance.name, __instance.BackButton, __instance.DefaultButtonSelected, __instance.ControllerSelectable);
                __instance.EnableTabControllerGlyphs(true);
            }
            EnsureSetupCompleted(controllerSelected);
            OpenChancesTab(Options.CurrentGameMode == CustomGameMode.HideAndSeek ? (EHR.TabGroup)100 : EHR.TabGroup.ImpostorRoles, __instance.roleTabs[0], controllerSelected);
            return false;
        }

        [HarmonyPatch(nameof(RolesSettingsMenu.CloseMenu))]
        [HarmonyPrefix]
        public static void PreCloseMenu(RolesSettingsMenu __instance)
        {
            if (BackButton)
            {
                Object.Destroy(BackButton.gameObject);
                BackButton = null;
            }

            if (!TempParent)
            {
                var gameObject = new GameObject("Forever-living parent");
                Object.DontDestroyOnLoad(gameObject);
                gameObject.SetActive(true);
                TempParent = gameObject.transform;
            }

            foreach (PassiveButton passiveButton in TabButtons.Values)
            {
                passiveButton.transform.SetParent(TempParent);
                passiveButton.gameObject.SetActive(false);
            }

            foreach (GameObject tab in Tabs.Values)
            {
                tab.transform.SetParent(TempParent);
                tab.SetActive(false);
            }
        }
        
        [HarmonyPatch(nameof(RolesSettingsMenu.Update))]
        [HarmonyPrefix]
        public static bool Update(RolesSettingsMenu __instance)
        {
            if (__instance.cachedData != GameOptionsManager.Instance.CurrentGameOptions.RoleOptions)
            {
                __instance.cachedData = GameOptionsManager.Instance.CurrentGameOptions.RoleOptions;
                RefreshChildren();
            }
            if (ControllerManager.Instance.CurrentUiState.MenuName == __instance.name)
            {
                Player player = ReInput.players.GetPlayer(0);
                bool flag = false;
                if (__instance.selectedRoleTab > 0 && player.GetButtonDown(35))
                {
                    --__instance.selectedRoleTab;
                    flag = true;
                    if (__instance.roleTabs[__instance.selectedRoleTab].transform.position.x < (double) __instance.roleTabsMinPos.position.x)
                        __instance.roleTabsScroller.ScrollRelative(new Vector2(0.762f, 0.0f));
                }
                if (__instance.selectedRoleTab < __instance.roleTabs.Count - 1 && player.GetButtonDown(34))
                {
                    ++__instance.selectedRoleTab;
                    flag = true;
                    if (__instance.roleTabs[__instance.selectedRoleTab].transform.position.x > (double) __instance.roleTabsMaxPos.position.x)
                        __instance.roleTabsScroller.ScrollRelative(new Vector2(-0.762f, 0.0f));
                }
                if (flag)
                {
                    __instance.roleTabs[__instance.selectedRoleTab].OnClick.Invoke();
                }
                __instance.glyphL.color = __instance.selectedRoleTab <= 0 ? __instance.glyphUnavailableColor : Color.white;
                __instance.glyphR.color = __instance.selectedRoleTab >= __instance.roleTabs.Count - 1 ? __instance.glyphUnavailableColor : Color.white;
            }

            __instance.roleTabsGradient.SetActive(!__instance.roleTabsScroller.AtLeft);
            return false;
        }

        private static void AddRoleTab(EHR.TabGroup tabGroup, ref float tabXPos)
        {
            RolesSettingsMenu instance = GameSettingMenu.Instance.RoleSettingsTab;
            PassiveButton tabButton = Object.Instantiate(instance.AllButton, Vector3.zero, Quaternion.identity, instance.roleSettingsTabParent);
            tabButton.name = $"MODDED_{tabGroup}";
            tabButton.transform.localPosition = new Vector3(tabXPos, 2.275f, -2f);
            Color color = tabGroup switch
            {
                EHR.TabGroup.ImpostorRoles => new Color32(255, 25, 25, 255),
                EHR.TabGroup.CrewmateRoles => new Color32(140, 255, 255, 255),
                EHR.TabGroup.NeutralRoles => new Color32(255, 171, 27, 255),
                EHR.TabGroup.CovenRoles => new Color32(123, 63, 187, 255),
                EHR.TabGroup.Addons => new Color32(0, 255, 107, 255),
                EHR.TabGroup.OtherRoles => new Color32(255, 255, 68, 255),
                _ => Color.white
            };
            tabButton.transform.Find("Inactive").GetComponent<SpriteRenderer>().color = color;
            tabButton.transform.Find("Highlight").GetComponent<SpriteRenderer>().color = color;
            tabButton.transform.Find("Selected").GetComponent<SpriteRenderer>().color = color;

            TextMeshPro textMeshPro = tabButton.transform.Find("Text").GetComponent<TextMeshPro>();
            textMeshPro.DestroyTranslator();
            textMeshPro.color = Color.black;
            textMeshPro.text = tabGroup switch
            {
                EHR.TabGroup.ImpostorRoles => "★",
                EHR.TabGroup.CrewmateRoles => "⚽",
                EHR.TabGroup.NeutralRoles => "◈",
                EHR.TabGroup.CovenRoles => "†",
                EHR.TabGroup.Addons => "✚",
                EHR.TabGroup.OtherRoles => "☁",
                _ => ""
            } + $"\n<font=\"DIN_Pro_Bold_700 SDF\"><size=25%>{Translator.GetString($"ShortTabName.{tabGroup}")}  </size></font>";
            
            tabButton.OnClick.RemoveAllListeners();
            tabButton.OnClick.AddListener(() => OpenChancesTab(tabGroup, tabButton, ActiveInputManager.currentControlType == ActiveInputManager.InputType.Joystick));
            tabButton.gameObject.SetActive(Options.CurrentGameMode == CustomGameMode.Standard);
            tabXPos += 0.762f;
            instance.roleTabs.Add(tabButton);
            TabButtons[tabButton.transform.localPosition] = tabButton;
        }

        [HarmonyPatch(nameof(RolesSettingsMenu.SetQuotaTab))]
        [HarmonyPrefix]
        public static bool SetQuotaTab(RolesSettingsMenu __instance)
        {
            Main.Instance.StartCoroutine(CreateRoleSettingsTabs());
            return false;

            System.Collections.IEnumerator CreateRoleSettingsTabs()
            {
                float tabXPos = -1.928f;
                __instance.roleTabs = new();
                
                for (EHR.TabGroup tabGroup = EHR.TabGroup.ImpostorRoles; tabGroup <= EHR.TabGroup.OtherRoles; tabGroup++)
                {
                    AddRoleTab(tabGroup, ref tabXPos);

                    GameObject tab = Object.Instantiate(__instance.RoleChancesSettings, __instance.RoleChancesSettings.transform.parent, false);
                    var localPosition = tab.transform.localPosition;
                    localPosition.z = -3f;
                    TabPosition = tab.transform.localPosition = localPosition;
                    
                    float y = 1f;
                    int index = 0;
                    int num = 0;

                    foreach (var group in Options.CustomRoleSpawnChances.Where(x => x.Value.Tab == tabGroup && x.Value.GameMode == CustomGameMode.Standard).GroupBy(x => tabGroup switch
                    {
                        EHR.TabGroup.Addons => (int)Options.GroupedAddons.First(k => k.Value.Contains(x.Key)).Key,
                        EHR.TabGroup.OtherRoles => (int)RoleOptionType.Crewmate_Miscellaneous,
                        _ => (int)x.Key.GetRoleOptionType()
                    }))
                    {
                        bool addons = tabGroup == EHR.TabGroup.Addons;
                        Color color = addons ? ((AddonTypes)group.Key).GetAddonTypeColor() : ((RoleOptionType)group.Key).GetRoleOptionTypeColor();
                        
                        var categoryHeaderEditRole = Object.Instantiate(__instance.categoryHeaderEditRoleOrigin, Vector3.zero, Quaternion.identity, tab.transform);
                        categoryHeaderEditRole.SetHeader(StringNames.None, 20);
                        categoryHeaderEditRole.Title.text = Translator.GetString(addons ? $"ROT.AddonType.{((AddonTypes)group.Key)}" : $"ROT.{((RoleOptionType)group.Key)}").Trim('★', ' ').ToUpper();
                        categoryHeaderEditRole.Title.color = Utils.GetTextColor(color);
                        categoryHeaderEditRole.Background.color = color;
                        categoryHeaderEditRole.blankLabel.color = color.ShadeColor(0.5f);
                        categoryHeaderEditRole.chanceLabel.color = color.ShadeColor(0.25f);
                        categoryHeaderEditRole.countLabel.color = color.ShadeColor(0.25f);
                        categoryHeaderEditRole.transform.localPosition = new Vector3(4.986f, y, -2f);
                        categoryHeaderEditRole.gameObject.SetActive(true);
                        num++;
                        y -= 0.522f;
                        bool any = false;

                        foreach ((CustomRoles role, StringOptionItem stringOptionItem) in group)
                        {
                            CreateQuotaOption(role, ref y, index, tab, stringOptionItem);
                            any = true;
                            index++;
                        }

                        if (!any)
                        {
                            Object.Destroy(categoryHeaderEditRole.gameObject);
                            y += 0.742f;
                        }
                        
                        y -= 0.22f;
                    }

                    tab.SetActive(false);
                    Tabs[tabGroup] = tab;
                    TabChildrenCount[tabGroup] = index + num * 2;

                    yield return null;
                }

                // HNS Roles tab
                {
                    const EHR.TabGroup tabGroup = (EHR.TabGroup)100;
                    
                    // AddRoleTab reimplementation
                    {
                        PassiveButton tabButton = Object.Instantiate(__instance.AllButton, Vector3.zero, Quaternion.identity, __instance.roleSettingsTabParent);
                        tabButton.name = "MODDED_HNSRoles";
                        tabButton.transform.localPosition = new Vector3(-2.69f, 2.275f, -2f);
                        Color color = Main.GameModeColors[CustomGameMode.HideAndSeek];
                        tabButton.transform.Find("Inactive").GetComponent<SpriteRenderer>().color = color;
                        tabButton.transform.Find("Highlight").GetComponent<SpriteRenderer>().color = color;
                        tabButton.transform.Find("Selected").GetComponent<SpriteRenderer>().color = color;

                        TextMeshPro textMeshPro = tabButton.transform.Find("Text").GetComponent<TextMeshPro>();
                        textMeshPro.DestroyTranslator();
                        textMeshPro.color = Color.black;
                        textMeshPro.text = $"☯\n<font=\"DIN_Pro_Bold_700 SDF\"><size=25%>{Translator.GetString("ShortTabName.HNSRoles")}  </size></font>";
                        
                        tabButton.OnClick.RemoveAllListeners();
                        tabButton.OnClick.AddListener(() => OpenChancesTab(tabGroup, tabButton, ActiveInputManager.currentControlType == ActiveInputManager.InputType.Joystick));
                        tabButton.gameObject.SetActive(Options.CurrentGameMode == CustomGameMode.HideAndSeek);
                        __instance.roleTabs.Add(tabButton);
                        TabButtons[tabButton.transform.localPosition] = tabButton;
                    }

                    GameObject tab = Object.Instantiate(__instance.RoleChancesSettings, __instance.RoleChancesSettings.transform.parent, false);
                    var localPosition = tab.transform.localPosition;
                    localPosition.z = -3f;
                    tab.transform.localPosition = localPosition;
                
                    float y = 1f;
                    int index = 0;
                    int num = 0;

                    foreach (var group in CustomHnS.GetAllHnsRoleTypes().Select(x => (Role: Enum.Parse<CustomRoles>(ignoreCase: true, value: x.Name), Interface: (IHideAndSeekRole)Activator.CreateInstance(x))).Where(x => x.Role is not (CustomRoles.Hider or CustomRoles.Seeker)).GroupBy(x => x.Interface.Team))
                    {
                        Team team = group.Key;
                        Color color = team.GetColor();
                    
                        var categoryHeaderEditRole = Object.Instantiate(__instance.categoryHeaderEditRoleOrigin, Vector3.zero, Quaternion.identity, tab.transform);
                        categoryHeaderEditRole.SetHeader(StringNames.None, 20);
                        categoryHeaderEditRole.Title.text = Translator.GetString($"HNS.{team}Roles").ToUpper();
                        categoryHeaderEditRole.Title.color = Utils.GetTextColor(color);
                        categoryHeaderEditRole.Background.color = color;
                        categoryHeaderEditRole.blankLabel.color = color.ShadeColor(0.5f);
                        categoryHeaderEditRole.chanceLabel.color = color.ShadeColor(0.25f);
                        categoryHeaderEditRole.countLabel.color = color.ShadeColor(0.25f);
                        categoryHeaderEditRole.transform.localPosition = new Vector3(4.986f, y, -2f);
                        categoryHeaderEditRole.gameObject.SetActive(true);
                        num++;
                        y -= 0.522f;
                        bool any = false;

                        foreach ((CustomRoles role, _) in group)
                        {
                            CreateQuotaOption(role, ref y, index, tab, Options.CustomRoleSpawnChances[role]);
                            any = true;
                            index++;
                        }

                        if (!any)
                        {
                            Object.Destroy(categoryHeaderEditRole.gameObject);
                            y += 0.742f;
                        }
                    
                        y -= 0.22f;
                    }

                    tab.SetActive(false);
                    Tabs[tabGroup] = tab;
                    TabChildrenCount[tabGroup] = index + num * 2;

                    yield return null;
                }

                TidyStuff(__instance);
            }
        }

        private static void TidyStuff(RolesSettingsMenu __instance)
        {
            Object.Destroy(__instance.roleScreenshot.gameObject);
            Object.Destroy(__instance.AdvancedRolesSettings.transform.Find("Background").gameObject);
            Object.Destroy(__instance.AdvancedRolesSettings.transform.Find("Imagebackground").gameObject);
            Object.Destroy(__instance.AdvancedRolesSettings.transform.Find("InfoLabelBackground").gameObject);
            Object.Destroy(__instance.AdvancedRolesSettings.GetComponentInChildren<CategoryHeaderMasked>().gameObject);
            Object.Destroy(__instance.RoleChancesSettings);
            Object.Destroy(__instance.AllButton.gameObject);
            __instance.roleScreenshot = null;
            __instance.RoleChancesSettings = null;
            __instance.AllButton = null;
                
            Transform roleDescriptionBox = __instance.roleDescriptionText.transform.parent;
            roleDescriptionBox.localPosition = new Vector3(1.4f, - 0.2531f, -1f);
            roleDescriptionBox.localScale = new Vector3(0.114f, 0.3f, roleDescriptionBox.localScale.z);
            Vector3 scale = __instance.roleDescriptionText.transform.localScale;
            __instance.roleDescriptionText.transform.localScale = new Vector3(scale.x / 1.6f, scale.y / 2f, scale.z);
            __instance.roleDescriptionText.GetComponent<RectTransform>().sizeDelta = new Vector2(5.55f, 1.15f);
        }

        private static void CreateQuotaOption(CustomRoles role, ref float yPos, int index, GameObject tab, StringOptionItem optionItem)
        {
            RolesSettingsMenu instance = GameSettingMenu.Instance.RoleSettingsTab;
            RoleOptionSetting roleOptionSetting = Object.Instantiate(instance.roleOptionSettingOrigin, Vector3.zero, Quaternion.identity, tab.transform);
            roleOptionSetting.transform.localPosition = new Vector3(-0.15f, yPos, -2f);
            RoleOptionSettingPatches.SetRole(roleOptionSetting, role, 20);
#if IL2CPP
            roleOptionSetting.OnValueChanged = (Action<OptionBehaviour>)(obj => ValueChanged(obj, true));
#else
            roleOptionSetting.OnValueChanged = obj => ValueChanged(obj, true);
#endif
            roleOptionSetting.SetClickMask(instance.ButtonClickMask);
            roleOptionSetting.name = $"MODDED_{role}";
            roleOptionSetting.gameObject.SetActive(true);
            OptionItem.SettingObjectNameToRole[roleOptionSetting.name] = role;
            instance.roleChances.Add(roleOptionSetting);

            if (optionItem.Children.Count > 1)
            {
                GameOptionButton settingsButton = Object.Instantiate(roleOptionSetting.CountMinusBtn, roleOptionSetting.CountMinusBtn.transform.parent, true);
                settingsButton.name = $"MODDED_{role}_AdvancedSettingsButton";
                var text = settingsButton.GetComponentInChildren<TextMeshPro>();
                text.SetText("✽");
                text.color = Color.white;
                settingsButton.buttonSprite.color = Color.black;
                settingsButton.interactableColor = Color.black;
                settingsButton.interactableHoveredColor = Utils.GetRoleColor(role);
                settingsButton.transform.localPosition += new Vector3(-2.4f, 0f, 0f);
                settingsButton.OnClick = new();
                settingsButton.OnClick.AddListener(() => ChangeTab(role));
                settingsButton.gameObject.SetActive(role.IsEnable());
            }

            if (index != 0)
            {
                for (var i = 0; i < roleOptionSetting.ControllerSelectable.Count; ++i)
                {
                    roleOptionSetting.ControllerSelectable[i].ControllerNav.selectOnUp = instance.roleChances[index - 1].ControllerSelectable[i];
                    instance.roleChances[index - 1].ControllerSelectable[i].ControllerNav.selectOnDown = roleOptionSetting.ControllerSelectable[i];
                }
            }

#if IL2CPP
            foreach (var button in roleOptionSetting.ControllerSelectable)
                instance.QuotaTabSelectables.Add(button);
#else
            instance.QuotaTabSelectables.AddRange(roleOptionSetting.ControllerSelectable);
#endif
            yPos -= 0.43f;
        }

        private static void ChangeTab(CustomRoles role)
        {
            RolesSettingsMenu instance = GameSettingMenu.Instance.RoleSettingsTab;
            CreateAdvancedSettings(role);
            string info = Translator.GetString($"{role}InfoLong");
            if (info.Length > 500) info = info.Split("\n\n")[0];
            if (!role.IsGhostRole() && info.Contains("):\n")) info = info.Split("):\n")[1];
            instance.roleDescriptionText.text = info;
            instance.roleTitleText.text = Translator.GetString(role.ToString()).ToUpper();
            Color roleColor = Utils.GetRoleColor(role);
            instance.roleHeaderSprite.color = roleColor;
            instance.roleHeaderText.color = Utils.GetTextColor(roleColor);

            for (var index = 0; index < instance.advancedSettingChildren.Count; ++index)
            {
                OptionBehaviour advancedSettingChild = instance.advancedSettingChildren[index];
#if IL2CPP
                advancedSettingChild.OnValueChanged = (Action<OptionBehaviour>)(obj => ValueChanged(obj, false));
#else
                advancedSettingChild.OnValueChanged = obj => ValueChanged(obj, false);
#endif
                if (AmongUsClient.Instance && !AmongUsClient.Instance.AmHost)
                    advancedSettingChild.SetAsPlayer();
            }

            foreach (GameObject gameObject in Tabs.Values)
                gameObject.SetActive(false);
            instance.AdvancedRolesSettings.SetActive(true);
            RefreshChildren();
            ControllerManager.Instance.CurrentUiState.SelectableUiElements = instance.ControllerSelectable;
            ControllerManager.Instance.SetDefaultSelection(instance.ControllerSelectable[0]);
        }

        [HarmonyPatch(nameof(RolesSettingsMenu.OpenChancesTab))]
        [HarmonyPrefix]
        static bool OpenChancesTabRedirect(RolesSettingsMenu __instance, bool controllerSelected = true)
        {
            EnsureSetupCompleted(controllerSelected);
            OpenChancesTab(Options.CurrentGameMode == CustomGameMode.HideAndSeek ? (EHR.TabGroup)100 : EHR.TabGroup.ImpostorRoles, __instance.roleTabs[0], controllerSelected);
            return false;
        }

        private static void OpenChancesTab(EHR.TabGroup tabGroup, PassiveButton button, bool controllerSelected = true, bool goneBack = false)
        {
            RolesSettingsMenu instance = GameSettingMenu.Instance.RoleSettingsTab;
            instance.selectedRoleTab = (int)tabGroup == 100 ? 6 : tabGroup - EHR.TabGroup.ImpostorRoles;
            EnsureSetupCompleted(controllerSelected);
            foreach (GameObject gameObject in Tabs.Values)
                gameObject.SetActive(false);
            Tabs[tabGroup].SetActive(true);
            instance.AdvancedRolesSettings.SetActive(false);
            instance.scrollBar.CalculateAndSetYBounds(TabChildrenCount[tabGroup] + 3, 1f, 6f, 0.43f);
            if (!goneBack) instance.scrollBar.ScrollToTop();

            if (controllerSelected)
            {
                ControllerManager.Instance.CurrentUiState.SelectableUiElements = instance.ControllerSelectable;
                ControllerManager.Instance.SetDefaultSelection(instance.roleChances[0].ControllerSelectable[0]);
            }
            
            instance.currentTabButton?.SelectButton(false);
            button.SelectButton(true);
            instance.currentTabButton = button;
        }

        private static void EnsureSetupCompleted(bool controllerSelected)
        {
            RolesSettingsMenu instance = GameSettingMenu.Instance.RoleSettingsTab;
            if (instance.QuotaTabSelectables == null)
                instance.InitialSetup();
            instance.ControllerSelectable.Clear();
            if (controllerSelected)
#if IL2CPP
                foreach (var uiElement in instance.QuotaTabSelectables!)
                    instance.ControllerSelectable.Add(uiElement);
#else
                instance.ControllerSelectable.AddRange(instance.QuotaTabSelectables!);
#endif
        }

        private static void RefreshChildren()
        {
            RolesSettingsMenu instance = GameSettingMenu.Instance.RoleSettingsTab;
            if (instance.advancedSettingChildren is { Count: > 0 })
            {
                for (var index = 0; index < instance.advancedSettingChildren.Count; ++index)
                {
                    OptionBehaviour advancedSettingChild = instance.advancedSettingChildren[index];
                    advancedSettingChild.enabled = false;
                    advancedSettingChild.enabled = true;
                }
            }

            if (instance.roleChances is not { Count: > 0 })
                return;
            for (var index = 0; index < instance.roleChances.Count; ++index)
            {
                RoleOptionSetting roleOptionSetting = instance.roleChances[index];
                if (OptionItem.SettingObjectNameToRole.TryGetValue(roleOptionSetting.name, out CustomRoles role))
                    RoleOptionSettingPatches.UpdateValuesAndText(roleOptionSetting, role);
            }
        }

        private static void ValueChanged(OptionBehaviour obj, bool roleSetting)
        {
            RolesSettingsMenu instance = GameSettingMenu.Instance.RoleSettingsTab;

            if (roleSetting)
            {
#if IL2CPP
                RoleOptionSetting roleOptionSetting = obj.CastFast<RoleOptionSetting>();
#else
                RoleOptionSetting roleOptionSetting = (RoleOptionSetting)obj;
#endif
                CustomRoles role = OptionItem.SettingObjectNameToRole[roleOptionSetting.name];
                Options.CustomRoleSpawnChances[role].SetValue(roleOptionSetting.RoleChance / 5);
                if (!Options.SingleRoles.Contains(role))
                    Options.CustomRoleCounts[role].SetValue(Math.Max(roleOptionSetting.RoleMaxCount - 1, 0));
                else
                    roleOptionSetting.roleMaxCount = Math.Clamp(roleOptionSetting.roleMaxCount, 0, 1);
                Transform transform = roleOptionSetting.CountMinusBtn.transform.parent.Find($"MODDED_{role}_AdvancedSettingsButton");
                if (transform) transform.gameObject.SetActive(role.IsEnable());
            }
            else
            {
                OptionItem optionItem = OptionItem.BehaviourNameToItem[obj.name];

                if (optionItem.Children.Count > 0)
                {
                    float y = -1.2f;
                    int num = 1;

                    foreach (OptionBehaviour optionBehaviour in instance.advancedSettingChildren)
                    {
                        if (OptionItem.BehaviourNameToItem.TryGetValue(optionBehaviour.name, out OptionItem item))
                        {
                            bool hidden = item.IsCurrentlyHidden(checkParentEnabled: true);
                            optionBehaviour.gameObject.SetActive(!hidden);
                            if (hidden) continue;
                            optionBehaviour.transform.localPosition = new Vector3(0.952f, y, -2f);
                            y -= 0.45f;
                        }

                        num++;
                    }
                    
                    instance.scrollBar.CalculateAndSetYBounds(num + 3, 1f, 6f, 0.45f);
                }
            }

            GameOptionsManager.Instance.CurrentGameOptions.SetInt(Int32OptionNames.RulePreset, 100);
            GameOptionsManager.Instance.CurrentGameOptions.SetBool(BoolOptionNames.IsDefaults, false);
            RefreshChildren();
            GameOptionsManager.Instance.GameHostOptions = GameOptionsManager.Instance.CurrentGameOptions;
        }

        private static void CreateAdvancedSettings(CustomRoles role)
        {
            RolesSettingsMenu instance = GameSettingMenu.Instance.RoleSettingsTab;
            instance.advancedSettingChildren ??= new();
            for (var i = 0; i < instance.advancedSettingChildren.Count; ++i)
                Object.Destroy(instance.advancedSettingChildren[i].gameObject);
            instance.ControllerSelectable.Clear();
            instance.advancedSettingChildren.Clear();
            float scrollPercY = instance.scrollBar.GetScrollPercY();
            float y = -1.2f;
            int skip = role.IsAdditionRole() ? 2 : 1;
            int index = 0;
            int num = 1;
            StringOptionItem stringOptionItem = Options.CustomRoleSpawnChances[role];
            EHR.TabGroup tab = Options.CurrentGameMode == CustomGameMode.HideAndSeek ? (EHR.TabGroup)100 : stringOptionItem.Tab;

            if (!BackButton)
            {
                StringOption back = Object.Instantiate(instance.stringOptionOrigin, Vector3.zero, Quaternion.identity, instance.AdvancedRolesSettings.transform);
                back.name = $"BACK_BUTTON_{role}";
                back.transform.localPosition = new Vector3(-1.25f, 0.9f, -2f);
                back.SetClickMask(instance.ButtonClickMask);
                back.SetUpFromData(null, 20);

                Object.Destroy(back.ValueText.gameObject);
                Object.Destroy(back.transform.Find("ValueBox").gameObject);
                Object.Destroy(back.PlusBtn.gameObject);
                back.ValueText = null;
                back.PlusBtn = null;

                back.MinusBtn.OnClick = new();
                back.MinusBtn.OnClick.AddListener(() =>
                {
                    OpenChancesTab(tab, instance.currentTabButton, ActiveInputManager.currentControlType == ActiveInputManager.InputType.Joystick, true);
                    instance.scrollBar.ScrollPercentY(scrollPercY);
                });
                Vector3 scale = back.MinusBtn.transform.localScale;
                back.MinusBtn.transform.localScale = new Vector3(scale.x * 7f, scale.y, scale.z);
                var text = back.MinusBtn.GetComponentInChildren<TextMeshPro>();
                Vector3 textScale = text.transform.localScale;
                text.transform.localScale = new Vector3(textScale.x / 7f, textScale.y, textScale.z);
                text.autoSizeTextContainer = false;
                text.GetComponent<RectTransform>().sizeDelta = new(3f, 0.37f);
                text.DestroyTranslator();
                text.text = $"⬅  {Translator.GetString("BackToList")}";
                back.TitleText.enabled = false;
                back.LabelBackground.enabled = false;
                back.gameObject.SetActive(true);

#if IL2CPP
                foreach (var uiElement in back.GetComponentsInChildren<UiElement>(false))
                    instance.ControllerSelectable.Add(uiElement);
#else
                instance.ControllerSelectable.AddRange(back.GetComponentsInChildren<UiElement>(false));
#endif
                BackButton = back;
            }
            else
            {
                BackButton.MinusBtn.OnClick = new();
                BackButton.MinusBtn.OnClick.AddListener(() =>
                {
                    OpenChancesTab(tab, instance.currentTabButton, ActiveInputManager.currentControlType == ActiveInputManager.InputType.Joystick);
                    instance.scrollBar.ScrollPercentY(scrollPercY);
                });
            }

            foreach (OptionItem optionItem in GetAllChildren(stringOptionItem))
            {
                if (index++ < skip) continue;
                
                if (!InstantiateOptionBehaviour(optionItem, instance.AdvancedRolesSettings.transform, out OptionBehaviour optionBehaviour, out BaseGameSetting baseGameSetting, instance.checkboxOrigin, instance.numberOptionOrigin, instance.stringOptionOrigin))
                    continue;
                
                optionBehaviour.transform.localPosition = new Vector3(0.952f, y, -2f);
                optionBehaviour.SetUpFromData(baseGameSetting, 20);
                optionBehaviour.SetClickMask(instance.ButtonClickMask);
                optionBehaviour.LabelBackground.enabled = false;
                optionBehaviour.AssociatedRole = RoleTypes.Engineer;
                
#if IL2CPP
                TextMeshPro titleText = GetTitleTextAndSetPositions(optionBehaviour, optionItem);
#else
                TextMeshPro titleText = GetTitleTextAndSetPositions(optionBehaviour);
#endif
                titleText.transform.localPosition += new Vector3(-0.4f, 0f, 0f);
                RectTransform rectTransform = titleText.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new(5.7f, rectTransform.sizeDelta.y);
                titleText.alignment = TextAlignmentOptions.MidlineRight;
                instance.advancedSettingChildren.Add(optionBehaviour);
#if IL2CPP
                foreach (var uiElement in optionBehaviour.GetComponentsInChildren<UiElement>(false))
                    instance.ControllerSelectable.Add(uiElement);
#else
                instance.ControllerSelectable.AddRange(optionBehaviour.GetComponentsInChildren<UiElement>(false));
#endif
                
                bool hidden = optionItem.IsCurrentlyHidden(checkParentEnabled: true);
                optionBehaviour.gameObject.SetActive(!hidden);
                if (hidden) continue;
                y -= 0.45f;
                num++;
            }

            instance.scrollBar.CalculateAndSetYBounds(num + 3, 1f, 6f, 0.45f);
            instance.InitializeControllerNavigation();
            instance.scrollBar.ScrollToTop();
        }
    }

    [HarmonyPatch(typeof(RoleOptionSetting))]
    static class RoleOptionSettingPatches
    {
        private static readonly int StencilComp = Shader.PropertyToID("_StencilComp");
        private static readonly int Stencil = Shader.PropertyToID("_Stencil");

        public static void SetRole(RoleOptionSetting __instance, CustomRoles role, int maskLayer)
        {
            foreach (SpriteRenderer componentsInChild in __instance.GetComponentsInChildren<SpriteRenderer>(true))
                componentsInChild.material.SetInt(PlayerMaterial.MaskLayer, maskLayer);
            foreach (TextMeshPro componentsInChild in __instance.GetComponentsInChildren<TextMeshPro>(true))
            {
                componentsInChild.fontMaterial.SetFloat(StencilComp, 3f);
                componentsInChild.fontMaterial.SetFloat(Stencil, maskLayer);
            }
#if IL2CPP
            __instance.role = RoleManager.Instance.AllRoles.Find((Il2CppSystem.Predicate<RoleBehaviour>)(x => x.Role == RoleTypes.Engineer));
#else
            __instance.role = RoleManager.Instance.AllRoles.Find(x => x.Role == RoleTypes.Engineer);
#endif
            __instance.titleText.text = Translator.GetString(role.ToString());
            Color roleColor = Utils.GetRoleColor(role);
            __instance.titleText.color = Utils.GetTextColor(roleColor);
            __instance.labelSprite.color = roleColor;
            UpdateValuesAndText(__instance, role);
        }
        
        public static void UpdateValuesAndText(RoleOptionSetting __instance, CustomRoles role)
        {
            __instance.roleMaxCount = role.GetCount();
            __instance.roleChance = role.GetMode();
            __instance.countText.text = __instance.roleMaxCount.ToString();
            __instance.chanceText.text = __instance.roleChance.ToString();
            __instance.AdjustCountButtonsActiveState();
            __instance.AdjustChanceButtonsActiveState();
        }
        
        [HarmonyPatch(nameof(RoleOptionSetting.IncreaseChance))]
        [HarmonyPrefix]
        public static bool IncreaseChance(RoleOptionSetting __instance)
        {
            switch (__instance.roleChance)
            {
                case 100:
                    return false;
                case 0:
                    __instance.roleMaxCount = 1;
                    __instance.AdjustCountButtonsActiveState();
                    break;
            }

            __instance.roleChance = Mathf.Clamp(__instance.roleChance + 5, 10, 100);
#if IL2CPP
            __instance.OnValueChanged.Invoke(__instance);
#else
            __instance.OnValueChanged(__instance);
#endif
            __instance.AdjustChanceButtonsActiveState();
            return false;
        }

        [HarmonyPatch(nameof(RoleOptionSetting.DecreaseChance))]
        [HarmonyPrefix]
        public static bool DecreaseChance(RoleOptionSetting __instance)
        {
            if (__instance.roleChance == 0)
                return false;
            __instance.roleChance = Mathf.Clamp(__instance.roleChance - 5, 0, 100);
            if (__instance.roleChance == 0)
            {
                __instance.roleMaxCount = 0;
                __instance.AdjustCountButtonsActiveState();
            }
#if IL2CPP
            __instance.OnValueChanged.Invoke(__instance);
#else
            __instance.OnValueChanged(__instance);
#endif
            __instance.AdjustChanceButtonsActiveState();
            return false;
        }
    }

    [HarmonyPatch(typeof(GameSettingMenu))]
    internal static class GameSettingMenuPatches
    {
        private static GameOptionsMenu TemplateGameOptionsMenu;
        private static readonly Dictionary<int, GameOptionsMenu> CachedGameOptionsMenus = [];
        public static bool IsDirty;

        private static int NumImpsOnOpen = 1;
        private static int MinImpsOnOpen = 1;
        private static int MaxImpsOnOpen = 1;
        private static float VanillaKillCooldownOnOpen = 1;
        private static float ModdedKillCooldownOnOpen = 1;

        [HarmonyPatch(nameof(GameSettingMenu.ChangeTab))]
        [HarmonyPrefix]
        private static bool ChangeTab(GameSettingMenu __instance, int tabNum, bool previewOnly)
        {
            if (previewOnly || !IsDirty || tabNum is 0 or 1) return true;
            
            Main.Instance.StartCoroutine(ReOpen());
            return false;

            System.Collections.IEnumerator ReOpen()
            {
                __instance.Close();
                    
                yield return new WaitForEndOfFrame();
                    
                if (!GameStartManager.Instance) yield break;
                GameStartManager.Instance.ClickEdit();

                yield return new WaitForSecondsRealtime(0.2f);

                if (!GameStates.IsLobby || !GameSettingMenu.Instance) yield break;
                GameSettingMenu.Instance.ChangeTab(tabNum, false);
            }
        }

        [HarmonyPatch(nameof(GameSettingMenu.ChangeTab))]
        [HarmonyPostfix]
        private static void PostChangeTab(GameSettingMenu __instance, int tabNum, bool previewOnly)
        {
            if (previewOnly) return;
            
            foreach (GameOptionsMenu irrelevantMenu in CachedGameOptionsMenus.Values)
                irrelevantMenu.gameObject.SetActive(false);
            
            if (tabNum == 0)
                __instance.MenuDescriptionText.text = Translator.GetString("MenuDescription.GameModes");

            if (tabNum < 10) return;

            if (!CachedGameOptionsMenus.TryGetValue(tabNum, out GameOptionsMenu gameOptionsMenu))
            {
                gameOptionsMenu = Object.Instantiate(TemplateGameOptionsMenu, __instance.GameSettingsTab.transform.parent);
                gameOptionsMenu.name = $"IDX_{tabNum}";
                CachedGameOptionsMenus[tabNum] = gameOptionsMenu;
            }

            gameOptionsMenu.transform.SetParent(__instance.GameSettingsTab.transform.parent, false);
            gameOptionsMenu.gameObject.SetActive(true);
        }

        [HarmonyPatch(nameof(GameSettingMenu.OnEnable))]
        [HarmonyPrefix]
        private static void PreOnEnable(GameSettingMenu __instance)
        {
            if (!TemplateGameOptionsMenu)
            {
                TemplateGameOptionsMenu = Object.Instantiate(__instance.GameSettingsTab, __instance.GameSettingsTab.transform.parent);
                TemplateGameOptionsMenu.gameObject.SetActive(false);
            }
        }

        [HarmonyPatch(nameof(GameSettingMenu.Start))]
        [HarmonyPostfix]
        private static void PostStart(GameSettingMenu __instance)
        {
            PassiveButton[] buttons = [__instance.RoleSettingsButton, __instance.GameSettingsButton, __instance.GamePresetsButton];

            for (var index = 0; index < buttons.Length; index++)
            {
                PassiveButton passiveButton = buttons[index];
                
                passiveButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new(0f, 0.647f, 1f, 1f);
                passiveButton.activeSprites.GetComponent<SpriteRenderer>().color = new(0f, 0.847f, 1f, 1f);
                passiveButton.selectedSprites.GetComponent<SpriteRenderer>().color = new(0f, 0.847f, 1f, 1f);
                
                Vector3 scale = passiveButton.transform.localScale;
                passiveButton.transform.localScale = new Vector3(scale.x, scale.y * 0.75f, scale.z);
                passiveButton.transform.localPosition -= new Vector3(0f, (index + 1) * 0.16f, 0f);
            }

            __instance.RoleSettingsButton.gameObject.SetActive(Options.CurrentGameMode is CustomGameMode.Standard or CustomGameMode.HideAndSeek);
            
            PassiveButton presetsButton = Object.Instantiate(__instance.GamePresetsButton, __instance.GamePresetsButton.transform.parent);
            presetsButton.gameObject.SetActive(false);
            presetsButton.transform.localPosition += new Vector3(0f, buttons.Length * 0.16f, 0f);
            var buttonComponent = presetsButton.GetComponent<PassiveButton>();
            buttonComponent.OnClick = new();
            buttonComponent.OnClick.AddListener(() => GameSettingMenu.Instance.ChangeTab(1000, false));

            if (!OnlinePresetsManager.PresetsLoaded)
            {
                Main.Instance.StartCoroutine(OnlinePresetsManager.FetchPresetList(list =>
                {
                    OnlinePresetsManager.CachedPresets = list;
                    OnlinePresetsManager.PresetsLoaded = true;
                    CreatePresetExplorerUI();
                    presetsButton.gameObject.SetActive(true);
                }));
            }
            else
            {
                CreatePresetExplorerUI();
                presetsButton.gameObject.SetActive(true);
            }
            
            var label = __instance.GamePresetsButton.GetComponentInChildren<TextMeshPro>();
            label.DestroyTranslator();
            label.SetText(Translator.GetString("GameModeListTitle"));
            
            __instance.transform.Find("Background").gameObject.SetActive(false);
            __instance.MenuDescriptionText.text = Translator.GetString("MenuDescription.GameModes");
            
            (OptionItem minSetting, OptionItem maxSetting) = Options.FactionMinMaxSettings[Team.Impostor];
            MinImpsOnOpen = minSetting.GetInt();
            MaxImpsOnOpen = maxSetting.GetInt();
            NumImpsOnOpen = Main.NormalOptions.NumImpostors;
            VanillaKillCooldownOnOpen = Main.NormalOptions.KillCooldown;
            ModdedKillCooldownOnOpen = Options.FallBackKillCooldownValue.GetFloat();
        }

        private static void CreatePresetExplorerUI()
        {
            GameOptionsMenu gameOptionsMenu = Object.Instantiate(TemplateGameOptionsMenu, GameSettingMenu.Instance.GameSettingsTab.transform.parent);
            gameOptionsMenu.name = "IDX_1000";
            Object.Destroy(gameOptionsMenu.MapPicker.gameObject);
            gameOptionsMenu.Children = new();
            gameOptionsMenu.cachedData = GameOptionsManager.Instance.CurrentGameOptions;
            CachedGameOptionsMenus[1000] = gameOptionsMenu;
            OnlinePresetsManager.CreatePresetExplorerUI(gameOptionsMenu);
            gameOptionsMenu.InitializeControllerNavigation();
        }

        [HarmonyPatch(nameof(GameSettingMenu.Close))]
        [HarmonyPrefix]
        public static void PreClose(GameSettingMenu __instance)
        {
            IsDirty = false;
            
            try
            {
                foreach (GameObject gameObject in GamePresetsTabPatches.Buttons.Values)
                {
                    try
                    {
                        var button = gameObject.GetComponent<PassiveButton>();
                        button.OnClick.RemoveAllListeners();
                        button.OnMouseOver.RemoveAllListeners();
                        button.OnMouseOut.RemoveAllListeners();
                        Object.Destroy(gameObject);
                    }
                    catch { }
                }
            }
            catch { }
        
            GamePresetsTabPatches.Buttons.Clear();

            try
            {
                GameObject gameObject = GamePresetsTabPatches.NDIntegrationButton;
                var button = gameObject.GetComponent<PassiveButton>();
                button.OnClick.RemoveAllListeners();
                button.OnMouseOver.RemoveAllListeners();
                button.OnMouseOut.RemoveAllListeners();
                Object.Destroy(gameObject);
            }
            catch { }

            GamePresetsTabPatches.NDIntegrationButton = null;
            
            try
            {
                foreach (GameObject gameObject in OnlinePresetsManager.Buttons.Values)
                {
                    try
                    {
                        var button = gameObject.GetComponent<PassiveButton>();
                        button.OnClick.RemoveAllListeners();
                        Object.Destroy(gameObject);
                    }
                    catch { }
                }
            }
            catch { }
        
            OnlinePresetsManager.Buttons.Clear();
            
            foreach (GameOptionsMenu gameOptionsMenu in CachedGameOptionsMenus.Values)
            {
                try
                {
                    for (var index = 0; index < gameOptionsMenu.Children.Count; index++)
                    {
                        try
                        {
                            OptionBehaviour optionBehaviour = gameOptionsMenu.Children[index];
                    
                            foreach (PassiveButton button in optionBehaviour.GetComponentsInChildren<PassiveButton>(true))
                                button.OnClick.RemoveAllListeners();

                            optionBehaviour.OnValueChanged = null;
                            optionBehaviour.SetClickMask(null);
                        
                            Object.Destroy(optionBehaviour.Data);
                            Object.Destroy(optionBehaviour.gameObject);
                        }
                        catch { }
                    }

                    gameOptionsMenu.Children.Clear();
                    Object.Destroy(gameOptionsMenu.gameObject);
                }
                catch { }
            }
            
            CachedGameOptionsMenus.Clear();

            try
            {
                foreach (OptionBehaviour optionBehaviour in __instance.GameSettingsTab.GetComponentsInChildren<OptionBehaviour>(true))
                {
                    try
                    {
                        foreach (PassiveButton button in optionBehaviour.GetComponentsInChildren<PassiveButton>(true))
                            button.OnClick.RemoveAllListeners();
                    
                        optionBehaviour.OnValueChanged = null;
                        optionBehaviour.SetClickMask(null);
                    
                        Object.Destroy(optionBehaviour.Data);
                        Object.Destroy(optionBehaviour.gameObject);
                    }
                    catch { }
                }
            }
            catch { }
            
            int numImpostors = Main.NormalOptions.NumImpostors;
            (OptionItem minSetting, OptionItem maxSetting) = Options.FactionMinMaxSettings[Team.Impostor];

            if (numImpostors != NumImpsOnOpen && MinImpsOnOpen == minSetting.GetInt() && MaxImpsOnOpen == maxSetting.GetInt())
            {
                minSetting.SetValue(numImpostors);
                maxSetting.SetValue(numImpostors);
                EHR.Logger.SendInGame(string.Format(Translator.GetString("MinMaxModdedImpCountsSettingsChangedAuto"), numImpostors));
            }

            float vanillaKillCooldown = Main.NormalOptions.KillCooldown;
            float moddedKillCooldown = Options.FallBackKillCooldownValue.GetFloat();
            bool vanillaChanged = !Mathf.Approximately(vanillaKillCooldown, VanillaKillCooldownOnOpen);
            bool moddedChanged = !Mathf.Approximately(moddedKillCooldown, ModdedKillCooldownOnOpen);

            if (!Mathf.Approximately(vanillaKillCooldown, moddedKillCooldown) && vanillaChanged && !moddedChanged)
                Options.FallBackKillCooldownValue.SetValue((int)Math.Round(Math.Round(vanillaKillCooldown, 1) / 0.5f - 1));

            if (Options.AutoGMRotationRecompileOnClose)
                Options.CompileAutoGMRotationSettings();

            Options.AutoSetFactionMinMaxSettings();
            
            GameManager.Instance.LogicOptions.SyncOptions();

            NotificationPopperPatch.ReleaseRpcs();
        }
    }

    [HarmonyPatch(typeof(ToggleOption))]
    internal static class ToggleOptionPatches
    {
        private static readonly Color DefaultCheckMarkColor = new Color32(0, 165, 255, 255);
        
        [HarmonyPatch(nameof(ToggleOption.Initialize))]
        [HarmonyPrefix]
        private static bool Initialize(ToggleOption __instance)
        {
            if (!OptionItem.BehaviourNameToItem.TryGetValue(__instance.name, out OptionItem item)) return true;
            
            CustomGameMode currentGameMode = Options.CurrentGameMode;
            var color = currentGameMode != CustomGameMode.Standard ? Main.GameModeColors.GetValueOrDefault(currentGameMode, DefaultCheckMarkColor) : DefaultCheckMarkColor;
            __instance.CheckMark.sprite = Utils.LoadSprite("EHR.Resources.Images.Checkmark.png", 100f);
            __instance.CheckMark.color = color;
            var spriteRenderer = __instance.CheckMark.transform.parent.Find("ActiveSprite").GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = Utils.LoadSprite("EHR.Resources.Images.CheckMarkBox.png", 100f);
            spriteRenderer.color = color;

            __instance.TitleText.SetText(item.GetName());
            __instance.CheckMark.enabled = item.GetBool();
            return false;
        }

        [HarmonyPatch(nameof(ToggleOption.UpdateValue))]
        [HarmonyPrefix]
        private static bool UpdateValue(ToggleOption __instance)
        {
            if (!OptionItem.BehaviourNameToItem.TryGetValue(__instance.name, out OptionItem item)) return true;
            
            item.SetValue(__instance.GetBool() ? 1 : 0, doSave: true, doSync: false);
            NotificationPopperPatch.AddSettingsChangeMessage(item);
            return false;
        }
    }

    [HarmonyPatch(typeof(NumberOption))]
    internal static class NumberOptionPatches
    {
        [HarmonyPatch(nameof(NumberOption.Initialize))]
        [HarmonyPrefix]
        private static bool Initialize(NumberOption __instance)
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
                    __instance.ValidRange = new(0, 85);
                    __instance.Value = (float)Math.Round(__instance.Value, 2);
                    break;
                case StringNames.GameKillCooldown:
                    __instance.ValidRange = new(0, 180);
                    __instance.Increment = 0.5f;
                    __instance.Value = (float)Math.Round(__instance.Value, 2);
                    break;
                case StringNames.GamePlayerSpeed:
                    __instance.Increment = 0.05f;
                    __instance.Value = Mathf.Clamp((float)Math.Round(__instance.Value, 2), __instance.ValidRange.min, __instance.ValidRange.max);
                    break;
                case StringNames.GameCrewLight:
                case StringNames.GameImpostorLight:
                    __instance.Increment = 0.05f;
                    __instance.Value = (float)Math.Round(__instance.Value, 2);
                    break;
                case StringNames.GameNumImpostors:
                    __instance.ValidRange = new(0, Crowded.MaxImpostors);
                    __instance.Value = Mathf.Clamp((float)Math.Round(__instance.Value, 2), __instance.ValidRange.min, __instance.ValidRange.max);
                    if (DebugModeManager.AmDebugger) __instance.ValidRange.min = 0;
                    break;
                case StringNames.CapacityLabel:
                    __instance.ValidRange = new(4, 127);
                    __instance.Value = Mathf.Clamp(__instance.Value, __instance.ValidRange.min, __instance.ValidRange.max);
                    break;
            }

            if (!OptionItem.BehaviourNameToItem.TryGetValue(__instance.name, out OptionItem item)) return true;
            
            __instance.TitleText.SetText(item.GetName());
            __instance.ValueText.SetText(GetValueString(__instance, __instance.Value, item));
            __instance.AdjustButtonsActiveState();
            return false;
        }

        [HarmonyPatch(nameof(NumberOption.UpdateValue))]
        [HarmonyPrefix]
        private static bool UpdateValue(NumberOption __instance)
        {
            if (!OptionItem.BehaviourNameToItem.TryGetValue(__instance.name, out OptionItem item)) return true;

            if (item.Name.Contains("KillCooldown", StringComparison.OrdinalIgnoreCase) && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
                __instance.Value = Main.NormalOptions?.KillCooldown ?? Options.FallBackKillCooldownValue.GetFloat();

            switch (item)
            {
                case IntegerOptionItem integerOptionItem:
                    integerOptionItem.SetValue(integerOptionItem.Rule.GetNearestIndex(__instance.GetInt()), doSave: true, doSync: false);
                    break;
                case FloatOptionItem floatOptionItem:
                    floatOptionItem.SetValue(floatOptionItem.Rule.GetNearestIndex(__instance.GetFloat()), doSave: true, doSync: false);
                    break;
            }

            NotificationPopperPatch.AddSettingsChangeMessage(item);
            __instance.oldValue = __instance.Value;
            __instance.ValueText.SetText(GetValueString(__instance, __instance.Value, item));
            return false;
        }

        private static string GetValueString(NumberOption __instance, float value, OptionItem item)
        {
            if (__instance.ZeroIsInfinity && Mathf.Abs(value) < 0.0001f) return "<b>∞</b>";
            return item == null ? value.ToString(__instance.FormatString) : item.GetString();
        }

        static float GetIncrement(float increment)
        {
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) increment *= 10;
            else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) increment *= 5;
            return increment;
        }

        [HarmonyPatch(nameof(NumberOption.Increase))]
        [HarmonyPrefix]
        public static bool Increase(NumberOption __instance)
        {
            if (Mathf.Approximately(__instance.Value, __instance.ValidRange.max))
            {
                __instance.Value = __instance.ValidRange.min;
                __instance.UpdateValue();
                __instance.OnValueChanged?.Invoke(__instance);
                return false;
            }

            float increment = GetIncrement(__instance.Increment);

            if (__instance.Value + increment < __instance.ValidRange.max)
            {
                __instance.Value += increment;
                __instance.UpdateValue();
                __instance.OnValueChanged?.Invoke(__instance);
                return false;
            }

            return true;
        }

        [HarmonyPatch(nameof(NumberOption.Decrease))]
        [HarmonyPrefix]
        public static bool Decrease(NumberOption __instance)
        {
            if (Mathf.Approximately(__instance.Value, __instance.ValidRange.min))
            {
                __instance.Value = __instance.ValidRange.max;
                __instance.UpdateValue();
                __instance.OnValueChanged?.Invoke(__instance);
                return false;
            }

            float increment = GetIncrement(__instance.Increment);

            if (__instance.Value - increment > __instance.ValidRange.min)
            {
                __instance.Value -= increment;
                __instance.UpdateValue();
                __instance.OnValueChanged?.Invoke(__instance);
                return false;
            }

            return true;
        }

        [HarmonyPatch(nameof(NumberOption.AdjustButtonsActiveState))]
        [HarmonyPrefix]
        static bool AdjustButtonsActiveState(NumberOption __instance)
        {
            __instance.MinusBtn.SetInteractable(true);
            __instance.PlusBtn.SetInteractable(true);
            return false;
        }
    }

    [HarmonyPatch(typeof(StringOption))]
    internal static class StringOptionPatches
    {
        [HarmonyPatch(nameof(StringOption.Initialize))]
        [HarmonyPrefix]
        private static bool Initialize(StringOption __instance)
        {
            if (__instance.name.StartsWith(nameof(OnlinePresetsManager)) || __instance.name.StartsWith("BACK_BUTTON_") || __instance.name.StartsWith("MODDED_")) return false;

            if (!OptionItem.BehaviourNameToItem.TryGetValue(__instance.name, out OptionItem item)) return true;

            __instance.TitleText.SetText(item.GetName());
            return false;
        }

        [HarmonyPatch(nameof(StringOption.UpdateValue))]
        [HarmonyPrefix]
        private static bool UpdateValue(StringOption __instance)
        {
            if (__instance.name.StartsWith(nameof(OnlinePresetsManager)) || __instance.name.StartsWith("BACK_BUTTON_") || __instance.name.StartsWith("MODDED_")) return false;

            if (!OptionItem.BehaviourNameToItem.TryGetValue(__instance.name, out OptionItem item)) return true;
            
            item.SetValue(__instance.GetInt(), doSave: true, doSync: false);
            NotificationPopperPatch.AddSettingsChangeMessage(item);
            __instance.TitleText.SetText(item.GetName());
            return false;
        }

        [HarmonyPatch(nameof(StringOption.FixedUpdate))]
        [HarmonyPrefix]
        private static bool FixedUpdate(StringOption __instance)
        {
            if (__instance.name.StartsWith(nameof(OnlinePresetsManager)))
            {
                __instance.TitleText.SetText(__instance.name[(nameof(OnlinePresetsManager).Length + 1)..]);
                return false;
            }
            
            if (__instance.name.StartsWith("BACK_BUTTON_"))
            {
                __instance.TitleText.SetText("⬅");
                return false;
            }
            
            if (__instance.name.StartsWith("MODDED_")) 
            {
                __instance.TitleText.SetText(__instance.name[7..]);
                return false;
            }

            if (!OptionItem.BehaviourNameToItem.TryGetValue(__instance.name, out OptionItem item)) return true;

            if (__instance.oldValue != __instance.Value)
            {
                __instance.oldValue = __instance.Value;
                __instance.ValueText.SetText(item.GetString());
                __instance.TitleText.SetText(item.GetName());
                __instance.AdjustButtonsActiveState();
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

        [HarmonyPatch(nameof(StringOption.AdjustButtonsActiveState))]
        [HarmonyPrefix]
        static bool AdjustButtonsActiveState(StringOption __instance)
        {
            __instance.MinusBtn.SetInteractable(true);
            __instance.PlusBtn.SetInteractable(true);
            return false;
        }
    }

    private static bool InstantiateOptionBehaviour(OptionItem optionItem, Transform parent, out OptionBehaviour optionBehaviour, out BaseGameSetting baseGameSetting, params OptionBehaviour[] templates)
    {
        int templateIndex;

        switch (optionItem)
        {
            case BooleanOptionItem:
                var checkboxGameSetting = ScriptableObject.CreateInstance<CheckboxGameSetting>();
                checkboxGameSetting.Type = OptionTypes.Checkbox;
                checkboxGameSetting.Title = StringNames.Accept;
                baseGameSetting = checkboxGameSetting;
                templateIndex = 0;
                break;
            case IntegerOptionItem integerOptionItem:
                var intGameSetting = ScriptableObject.CreateInstance<IntGameSetting>();
                intGameSetting.Type = OptionTypes.Int;
                intGameSetting.Value = integerOptionItem.GetInt();
                intGameSetting.Increment = integerOptionItem.Rule.Step;
                intGameSetting.ValidRange = new(integerOptionItem.Rule.MinValue, integerOptionItem.Rule.MaxValue);
                intGameSetting.ZeroIsInfinity = false;
                intGameSetting.SuffixType = GetNumberSuffix();
                intGameSetting.FormatString = string.Empty;
                intGameSetting.Title = StringNames.Accept;
                baseGameSetting = intGameSetting;
                templateIndex = 1;
                break;
            case FloatOptionItem floatOptionItem:
                var floatGameSetting = ScriptableObject.CreateInstance<FloatGameSetting>();
                floatGameSetting.Type = OptionTypes.Float;
                floatGameSetting.Value = floatOptionItem.GetFloat();
                floatGameSetting.Increment = floatOptionItem.Rule.Step;
                floatGameSetting.ValidRange = new(floatOptionItem.Rule.MinValue, floatOptionItem.Rule.MaxValue);
                floatGameSetting.ZeroIsInfinity = false;
                floatGameSetting.SuffixType = GetNumberSuffix();
                floatGameSetting.FormatString = string.Empty;
                floatGameSetting.Title = StringNames.Accept;
                baseGameSetting = floatGameSetting;
                templateIndex = 1;
                break;
            case StringOptionItem stringOptionItem:
                var stringGameSetting = ScriptableObject.CreateInstance<StringGameSetting>();
                stringGameSetting.Type = OptionTypes.String;
                stringGameSetting.Values = new StringNames[stringOptionItem.Selections.Count];
                stringGameSetting.Index = stringOptionItem.GetInt();
                stringGameSetting.Title = StringNames.Accept;
                baseGameSetting = stringGameSetting;
                templateIndex = 2;
                break;
            default:
                optionBehaviour = null;
                baseGameSetting = null;
                return false;
        }

        optionBehaviour = Object.Instantiate(templates[templateIndex], Vector3.zero, Quaternion.identity, parent);
        optionBehaviour.name = $"IDX_{optionItem.Id}";
        OptionItem.BehaviourNameToItem[optionBehaviour.name] = optionItem;
        return true;

        NumberSuffixes GetNumberSuffix() => optionItem.ValueFormat switch
        {
            OptionFormat.Multiplier => NumberSuffixes.Multiplier,
            OptionFormat.Seconds => NumberSuffixes.Seconds,
            _ => NumberSuffixes.None
        };
    }

#if IL2CPP
    private static TextMeshPro GetTitleTextAndSetPositions(OptionBehaviour optionBehaviour, OptionItem optionItem)
    {
        TextMeshPro titleText = null;

        switch (optionItem)
        {
            case BooleanOptionItem when optionBehaviour.TryCastFast(out ToggleOption toggleOption):
#else
    private static TextMeshPro GetTitleTextAndSetPositions(OptionBehaviour optionBehaviour)
    {
        TextMeshPro titleText = null;

        switch (optionBehaviour)
        {
            case ToggleOption toggleOption:
#endif
            {
                toggleOption.transform.Find("Toggle").localPosition = new(1.46f, -0.042f);
                titleText = toggleOption.TitleText;
                break;
            }
#if IL2CPP
            case StringOptionItem when optionBehaviour.TryCastFast(out StringOption stringOption):
#else
            case StringOption stringOption:
#endif
            {
                stringOption.PlusBtn.transform.localPosition += new Vector3(1.7f, 0f, 0f);
                stringOption.MinusBtn.transform.localPosition += new Vector3(0.9f, 0f, 0f);
                stringOption.ValueText.transform.localPosition += new Vector3(1.3f, 0f, 0f);
                stringOption.ValueText.GetComponent<RectTransform>().sizeDelta = new(2.3f, 0.4f);
                titleText = stringOption.TitleText;
                goto default;
            }
#if IL2CPP
            case FloatOptionItem:
            case IntegerOptionItem:
            {
                NumberOption numberOption = optionBehaviour.CastFast<NumberOption>();
#else
            case NumberOption numberOption:
            {
#endif
                numberOption.PlusBtn.transform.localPosition += new Vector3(1.7f, 0f, 0f);
                numberOption.MinusBtn.transform.localPosition += new Vector3(0.9f, 0f, 0f);
                numberOption.ValueText.transform.localPosition += new Vector3(1.3f, 0f, 0f);
                titleText = numberOption.TitleText;
                goto default;
            }
            default:
            {
                Transform valueBox = optionBehaviour.transform.Find("ValueBox");
                valueBox.localScale += new Vector3(0.2f, 0f, 0f);
                valueBox.localPosition += new Vector3(1.3f, 0f, 0f);
                break;
            }
        }

        if (!titleText) titleText = optionBehaviour.transform.Find("Title Text").GetComponent<TextMeshPro>();
        return titleText;
    }

    private static IEnumerable<OptionItem> GetAllChildren(OptionItem optionItem)
    {
        for (var index = 0; index < optionItem.Children.Count; index++)
        {
            OptionItem child = optionItem.Children[index];
            yield return child;

            foreach (OptionItem descendant in GetAllChildren(child))
                yield return descendant;
        }
    }
}