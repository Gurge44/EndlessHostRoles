using System.Globalization;
using AmongUs.GameOptions;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace EHR.Patches;

[HarmonyPatch]
public static class MatchInfoGuidePatch
{
    [HarmonyPatch(typeof(MatchInfoGuide), nameof(MatchInfoGuide.CreateNormalModeSettings)), HarmonyPrefix]
    public static bool CreateModdedSettings(MatchInfoGuide __instance)
    {
        int num = 6;
        CreateTeamSizeSettingEntries(__instance);
        __instance.CreateSettingsEntry(StringNames.GameKillCooldown, GameManager.Instance.AllGameSettingData[StringNames.GameKillCooldown].GetValueString(GameManager.Instance.LogicOptions.GetKillCooldown()));
        __instance.CreateSettingsEntry(StringNames.GameEmergencyCooldown, GameManager.Instance.AllGameSettingData[StringNames.GameEmergencyCooldown].GetValueString(GameManager.Instance.LogicOptions.GetEmergencyCooldown()));
        __instance.CreateSettingsEntry(StringNames.GameVisualTasks, __instance.GetBoolString(GameManager.Instance.LogicOptions.GetVisualTasks()));
        __instance.CreateSettingsEntry(StringNames.GameAnonymousVotes, __instance.GetBoolString(GameManager.Instance.LogicOptions.GetAnonymousVotes()));
        __instance.CreateSettingsEntry(StringNames.GameTaskBarMode, GameManager.Instance.LogicOptions.GetTaskBarMode().ToString());
        CreateModdedSettingEntries(Options.GroupedOptions[TabGroup.GameSettings], __instance, ref num);
        CreateModdedSettingEntries(Options.GroupedOptions[TabGroup.TaskSettings], __instance, ref num);
        __instance.transform.FindChild("MatchInfoParent").FindChild("SettingsPanel").GetComponentInChildren<Scroller>().SetYBoundsMax(Mathf.Clamp(Mathf.Ceil(num / 4f), 0.0f, 999f));
        num = CreateModdedRoleEntries(__instance);
        if (num == 0) __instance.rolesEnabledMessage.SetActive(true);
        __instance.MatchInfoRoleScroller.SetYBoundsMax(Mathf.Clamp(Mathf.Ceil(num / 2f) + __instance.RoleEntryBoundsModifier, 0.0f, 999f));
        __instance.MatchInfoRoleMaskArea.material.SetInt(PlayerMaterial.MaskLayer, 50);
        __instance.matchInfoSettingsMaskArea.material.SetInt(PlayerMaterial.MaskLayer, 50);
        __instance.CreatePlayerEntries();
        ReColorTabButtons(__instance);
        return false;
    }

    private static void CreateTeamSizeSettingEntries(MatchInfoGuide __instance)
    {
        foreach ((Team team, (OptionItem minSetting, OptionItem maxSetting)) in Options.FactionMinMaxSettings)
        {
            int min = minSetting.GetInt(), max = maxSetting.GetInt();
            string value = min == max ? min.ToString() : $"{min}-{max}";
            CreateModdedSettingEntry(__instance, Utils.ColorString(team.GetColor(), $"# {Translator.GetString($"Type{team}")}"), value);
        }
    }

    private static int CreateModdedRoleEntries(MatchInfoGuide __instance)
    {
        int num = 0;

        foreach (CustomRoles role in Main.CustomRoleValues)
        {
            if (!role.IsVanilla() && !role.IsForOtherGameMode() && role.IsEnable())
            {
                CreateModdedRoleEntry(__instance, role);
                ++num;
            }
        }

        return num;
    }

    private static void CreateModdedSettingEntries(OptionItem[] settings, MatchInfoGuide __instance, ref int num)
    {
        foreach (OptionItem optionItem in settings)
        {
            if (optionItem.IsCurrentlyHidden(checkCollapsedSection: false) || optionItem.Parent != null || optionItem is PresetOptionItem or TextOptionItem) continue;

            string value = optionItem switch
            {
                BooleanOptionItem b => __instance.GetBoolString(b.GetBool()),
                FloatOptionItem f => f.GetFloat().ToString(CultureInfo.CurrentCulture),
                IntegerOptionItem i => i.GetInt().ToString(),
                StringOptionItem s => s.noTranslation ? s.Selections[s.GetValue()] : Translator.GetString(s.Selections[s.GetValue()]),
                _ => string.Empty
            };

            CreateModdedSettingEntry(__instance, optionItem.GetName(), value);
            ++num;
        }
    }

    private static void CreateModdedSettingEntry(MatchInfoGuide __instance, string settingName, string value)
    {
        GameObject gameObject = Object.Instantiate(__instance.MatchInfoSettingPrefab, __instance.settingsScrollArea);
        MatchInfoGuideSettingLabel component = gameObject.GetComponent<MatchInfoGuideSettingLabel>();
        if (component != null) component.SetInfo(settingName, value);
        __instance.NormalModeSettings.Add(gameObject);
    }

    private static void CreateModdedRoleEntry(MatchInfoGuide __instance, CustomRoles role)
    {
        MatchInfoRolePanel matchInfoRolePanel = Object.Instantiate(__instance.MatchInfoRolePanelPrefab, __instance.settingsTabs[2].GetComponent<Scroller>().Inner);
        matchInfoRolePanel.roleName.text = role.ToColoredString();
        matchInfoRolePanel.roleDescription.text = Translator.GetString($"{role}Info");
        matchInfoRolePanel.roleIcon.sprite = GetRoleBehaviourFromBasis(role).RoleIconColor;
        int roleCount = role.GetCount();
        string roleChance = Translator.GetString(Options.Rates[role.GetMode() / 5]);
        matchInfoRolePanel.roleCount.text = roleCount > 1 ? $"{roleCount}x {roleChance}" : roleChance;
        matchInfoRolePanel.roleIcon.material.SetInt(PlayerMaterial.MaskLayer, 50);
        matchInfoRolePanel.roleName.fontMaterial.SetFloat(matchInfoRolePanel.STENCIL_NAME, 50f);
        matchInfoRolePanel.roleDescription.fontMaterial.SetFloat(matchInfoRolePanel.STENCIL_NAME, 50f);
        matchInfoRolePanel.roleCount.fontMaterial.SetFloat(matchInfoRolePanel.STENCIL_NAME, 50f);
    }

    private static RoleBehaviour GetRoleBehaviourFromBasis(CustomRoles role)
    {
        var roleBehaviours = RoleManager.Instance.AllRoles;
        RoleTypes roleTypes = role.IsGhostRole() ? RoleTypes.GuardianAngel : role switch
        {
            CustomRoles.Aid or CustomRoles.Doctor or CustomRoles.Medic => RoleTypes.Scientist,
            CustomRoles.Captain or CustomRoles.Catcher or CustomRoles.Coroner or CustomRoles.Druid or CustomRoles.EvilTracker or CustomRoles.Hacker or CustomRoles.Scout or CustomRoles.Lookout => RoleTypes.Tracker,
            CustomRoles.Beehive or CustomRoles.Demon or CustomRoles.Pelican or CustomRoles.Scavenger or CustomRoles.Spider or CustomRoles.Vampire or CustomRoles.Vulture or CustomRoles.Wasp => RoleTypes.Viper,
            CustomRoles.Markseeker or CustomRoles.Soothsayer or CustomRoles.Specter or CustomRoles.SuperStar or CustomRoles.Sunnyboy or CustomRoles.Vacuum => RoleTypes.Noisemaker,
            CustomRoles.Prosecutor or CustomRoles.Mayor or CustomRoles.Dictator or CustomRoles.Swapper or CustomRoles.President => RoleTypes.Judge,
            _ when role.IsCoven() => RoleTypes.Shapeshifter,
            _ when role.IsCrewmate() && role.IsDesyncRole() => RoleTypes.Engineer,
            _ when role.GetCrewmateRoleCategory() == RoleOptionType.Crewmate_Investigate => RoleTypes.Detective,
            _ => role.GetRoleTypes()
        };
        roleTypes = roleTypes switch
        {
            RoleTypes.Crewmate => RoleTypes.Engineer,
            RoleTypes.CrewmateGhost => RoleTypes.GuardianAngel,
            RoleTypes.Impostor => RoleTypes.Viper,
            RoleTypes.ImpostorGhost => RoleTypes.GuardianAngel,
            _ => roleTypes
        };

        foreach (RoleBehaviour roleBehaviour in roleBehaviours)
            if (roleBehaviour.Role == roleTypes)
                return roleBehaviour;

        return roleBehaviours[0];
    }

    private static void ReColorTabButtons(MatchInfoGuide __instance)
    {
        Color32 color = new Color32(0, 165, 255, 255);
        string[] stateNames = ["Highlight", "Inactive", "Selected"];

        foreach (MatchInfoGuideTabButton matchInfoGuideTabButton in __instance.TabButtons)
        {
            matchInfoGuideTabButton.GetComponentInChildren<TextMeshPro>().color = Color.white;
            
            foreach (string stateName in stateNames)
            {
                var child = matchInfoGuideTabButton.transform.FindChild(stateName);
                child.GetComponent<SpriteRenderer>().color = color;
                if (child.childCount > 0) child.DestroyChildren();
            }
        }
    }
}