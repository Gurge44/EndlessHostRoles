using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EHR.Patches;

// Thanks: https://github.com/SubmergedAmongUs/Submerged/blob/4a5a6b47cbed526670ae4b7eae76acd7c42e35de/Submerged/UI/Patches/MapSelectButtonPatches.cs#L49
// And thanks: https://github.com/AU-Avengers/TOU-Mira/blob/main/TownOfUs/Patches/AprilFools/DleksMapOptionPickerPatches.cs
[HarmonyPatch]
public static class GameOptionsMapPickerPatch
{
    public static bool SetSubmerged;
    public static bool SetDleks;

    [HarmonyPatch(typeof(GameOptionsMapPicker), nameof(GameOptionsMapPicker.SelectMap), typeof(int))]
    [HarmonyPrefix]
    public static void Prefix_SelectMap([HarmonyArgument(0)] ref int mapId)
    {
        if (!SetDleks && mapId == 3)
            mapId = 0;
    }
    [HarmonyPatch(typeof(GameOptionsMapPicker), nameof(GameOptionsMapPicker.SetupMapButtons))]
    [HarmonyPrefix]
    public static void Postfix_Prefix(GameOptionsMapPicker __instance)
    {
        if (__instance.AllMapIcons.ToArray().Any(x => x.Name == MapNames.Dleks)) return;

        __instance.AllMapIcons.Insert((int)MapNames.Dleks, new MapIconByName
        {
            Name = MapNames.Dleks,
            MapImage = Utils.LoadSprite("EHR.Resources.Images.DleksBanner.png", 100f),
            MapIcon = Utils.LoadSprite("EHR.Resources.Images.DleksBanner-Icon.png", 95f),
            NameImage = Utils.LoadSprite("EHR.Resources.Images.DleksBanner-Wordart.png", 160f),
        });
    }
    [HarmonyPatch(typeof(GameOptionsMapPicker), nameof(GameOptionsMapPicker.SetupMapButtons))]
    [HarmonyPostfix]
    public static void Postfix_Initialize(CreateGameMapPicker __instance)
    {
        if (SceneManager.GetActiveScene().name == "FindAGame") return;

        const int dleksPos = 3;
        const int submergedPos = 6;

        MapSelectButton[] AllMapButton = __instance.transform.GetComponentsInChildren<MapSelectButton>();

        if (AllMapButton != null)
        {
            // Dleks Button
            {
                var dleksButton_MapButton = __instance.mapButtons[dleksPos];
                dleksButton_MapButton.Button.OnClick.RemoveAllListeners();
                dleksButton_MapButton.Button.OnClick.AddListener((Action)(() =>
                {
                    __instance.SelectMap(__instance.AllMapIcons[0]);

                    if (__instance.selectedButton)
                        __instance.selectedButton.Button.SelectButton(false);

                    __instance.selectedButton = dleksButton_MapButton;
                    __instance.selectedButton.Button.SelectButton(true);
                    __instance.selectedMapId = dleksPos;

                    SetDleks = true;

                    Main.NormalOptions.MapId = 0;

                    __instance.MapImage.sprite = Utils.LoadSprite("EHR.Resources.Images.DleksBanner.png", 100f);
                    __instance.MapName.sprite = Utils.LoadSprite("EHR.Resources.Images.DleksBanner-Wordart.png", 100f);
                }));

                if (dleksButton_MapButton != null)
                {
                    if (SetDleks)
                    {
                        if (__instance.selectedButton)
                            __instance.selectedButton.Button.SelectButton(false);

                        __instance.selectedButton = dleksButton_MapButton;
                        __instance.selectedButton.Button.SelectButton(true);
                        __instance.selectedMapId = dleksPos;

                        __instance.MapImage.sprite = Utils.LoadSprite("EHR.Resources.Images.DleksBanner.png", 100f);
                        __instance.MapName.sprite = Utils.LoadSprite("EHR.Resources.Images.DleksBanner-Wordart.png", 100f);
                    }
                    else
                        __instance.mapButtons[dleksPos].Button.SelectButton(false);
                }
            }

            // Submerged Button
            if (SubmergedCompatibility.Loaded)
            {
                var submergedButton_MapButton = __instance.mapButtons[submergedPos];
                submergedButton_MapButton.Button.OnClick.RemoveAllListeners();
                submergedButton_MapButton.Button.OnClick.AddListener((Action)(() =>
                {
                    __instance.SelectMap(__instance.AllMapIcons[submergedPos]);

                    if (__instance.selectedButton) __instance.selectedButton.Button.SelectButton(false);

                    __instance.selectedButton = submergedButton_MapButton;
                    __instance.selectedMapId = 6;

                    Main.NormalOptions.MapId = 6;

                    __instance.MapImage.sprite = Utils.LoadSprite("EHR.Resources.Images.SubmergedBanner.png", 100f);
                    __instance.MapName.sprite = Utils.LoadSprite("EHR.Resources.Images.SubmergedBanner-Wordart.png", 100f);
                }));

                if (submergedButton_MapButton != null)
                {
                    if (SetSubmerged)
                    {
                        if (__instance.selectedButton) __instance.selectedButton.Button.SelectButton(false);

                        submergedButton_MapButton.Button.SelectButton(true);
                        __instance.selectedButton = submergedButton_MapButton;
                        __instance.selectedMapId = 6;

                        __instance.MapImage.sprite = Utils.LoadSprite("EHR.Resources.Images.SubmergedBanner.png", 100f);
                        __instance.MapName.sprite = Utils.LoadSprite("EHR.Resources.Images.SubmergedBanner-Wordart.png", 100f);
                    }
                    else
                        submergedButton_MapButton.Button.SelectButton(false);
                }
            }
        }
    }

    [HarmonyPatch(typeof(GameOptionsMapPicker), nameof(GameOptionsMapPicker.FixedUpdate))]
    [HarmonyPrefix]
    public static bool Prefix_FixedUpdate(GameOptionsMapPicker __instance)
    {
        if (__instance == null) return true;
        if (__instance.MapName == null) return false;

        SetDleks = __instance.selectedMapId == 3;
        SetSubmerged = __instance.selectedMapId == 6;

        if (__instance.selectedMapId == 3)
        {
            if (SceneManager.GetActiveScene().name == "FindAGame")
            {
                __instance.SelectMap(0);
                SetDleks = false;
            }

            return false;
        }

        return true;
    }
}