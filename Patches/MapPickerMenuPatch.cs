﻿using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace EHR.Patches;

// Thanks: https://github.com/SubmergedAmongUs/Submerged/blob/4a5a6b47cbed526670ae4b7eae76acd7c42e35de/Submerged/UI/Patches/MapSelectButtonPatches.cs#L49
static class CreateOptionsPickerPatch
{
    public static bool SetDleks;
    private static MapSelectButton DleksButton;

    [HarmonyPatch(typeof(GameOptionsMapPicker))]
    public static class GameOptionsMapPickerPatch
    {
        [HarmonyPatch(nameof(GameOptionsMapPicker.Initialize))]
        [HarmonyPostfix]
        public static void Postfix_Initialize(GameOptionsMapPicker __instance)
        {
            const int dleksPos = 3;

            MapSelectButton[] AllMapButton = __instance.transform.GetComponentsInChildren<MapSelectButton>();

            if (AllMapButton != null)
            {
                GameObject dlekS_ehT = Object.Instantiate(AllMapButton[0].gameObject, __instance.transform);
                dlekS_ehT.transform.position = AllMapButton[dleksPos].transform.position;
                dlekS_ehT.transform.SetSiblingIndex(dleksPos + 2);
                MapSelectButton dlekS_ehT_MapButton = dlekS_ehT.GetComponent<MapSelectButton>();
                DleksButton = dlekS_ehT_MapButton;
                dlekS_ehT_MapButton.MapIcon.transform.localScale = new(-1f, 1f, 1f);
                dlekS_ehT_MapButton.Button.OnClick.RemoveAllListeners();
                dlekS_ehT_MapButton.Button.OnClick.AddListener((Action)(() =>
                {
                    __instance.SelectMap(__instance.AllMapIcons[0]);

                    if (__instance.selectedButton)
                    {
                        __instance.selectedButton.Button.SelectButton(false);
                    }

                    __instance.selectedButton = dlekS_ehT_MapButton;
                    __instance.selectedButton.Button.SelectButton(true);
                    __instance.selectedMapId = 3;

                    Main.NormalOptions.MapId = 0;

                    __instance.MapImage.sprite = Utils.LoadSprite("EHR.Resources.Images.DleksBanner.png", 100f);
                    __instance.MapName.sprite = Utils.LoadSprite("EHR.Resources.Images.DleksBanner-Wordart.png", 100f);
                }));

                for (int i = dleksPos; i < AllMapButton.Length; i++)
                {
                    AllMapButton[i].transform.localPosition += new Vector3(0.625f, 0f, 0f);
                }

                if (DleksButton != null)
                {
                    if (SetDleks)
                    {
                        if (__instance.selectedButton)
                        {
                            __instance.selectedButton.Button.SelectButton(false);
                        }

                        DleksButton.Button.SelectButton(true);
                        __instance.selectedButton = DleksButton;
                        __instance.selectedMapId = 3;

                        __instance.MapImage.sprite = Utils.LoadSprite("EHR.Resources.Images.DleksBanner.png", 100f);
                        __instance.MapName.sprite = Utils.LoadSprite("EHR.Resources.Images.DleksBanner-Wordart.png", 100f);
                    }
                    else
                    {
                        DleksButton.Button.SelectButton(false);
                    }
                }
            }
        }

        [HarmonyPatch(nameof(GameOptionsMapPicker.FixedUpdate))]
        [HarmonyPrefix]
        public static bool Prefix_FixedUpdate(GameOptionsMapPicker __instance)
        {
            if (DleksButton != null)
            {
                SetDleks = __instance.selectedMapId == 3;
            }

            return __instance.selectedMapId != 3;
        }
    }

    [HarmonyPatch(typeof(CreateOptionsPicker), nameof(CreateOptionsPicker.Awake))]
    class MenuMapPickerPatch
    {
        public static void Postfix(CreateOptionsPicker __instance)
        {
            Transform mapPickerTransform = __instance.transform.Find("MapPicker");
            MapPickerMenu mapPickerMenu = mapPickerTransform.Find("Map Picker Menu").GetComponent<MapPickerMenu>();

            MapFilterButton airhipIconInMenu = __instance.MapMenu.MapButtons[3];
            MapFilterButton fungleIconInMenu = __instance.MapMenu.MapButtons[4];
            MapFilterButton skeldIconInMenu = __instance.MapMenu.MapButtons[0];
            MapFilterButton dleksIconInMenuCopy = Object.Instantiate(airhipIconInMenu, airhipIconInMenu.transform.parent);

            Transform skeldMenuButton = mapPickerMenu.transform.Find("Skeld");
            Transform polusMenuButton = mapPickerMenu.transform.Find("Polus");
            Transform airshipMenuButton = mapPickerMenu.transform.Find("Airship");
            Transform fungleMenuButton = mapPickerMenu.transform.Find("Fungle");
            Transform dleksMenuButtonCopy = Object.Instantiate(airshipMenuButton, airshipMenuButton.parent);

            // Set mapid for Dleks button
            PassiveButton dleksButton = dleksMenuButtonCopy.GetComponent<PassiveButton>();
            dleksButton.OnClick.m_PersistentCalls.m_Calls._items[0].arguments.intArgument = (int)MapNames.Dleks;

            SpriteRenderer dleksImage = dleksMenuButtonCopy.Find("Image").GetComponent<SpriteRenderer>();
            dleksImage.sprite = skeldMenuButton.Find("Image").GetComponent<SpriteRenderer>().sprite;

            dleksIconInMenuCopy.name = "Dleks";
            dleksIconInMenuCopy.transform.localPosition = new(0.8f, airhipIconInMenu.transform.localPosition.y, airhipIconInMenu.transform.localPosition.z);
            dleksIconInMenuCopy.MapId = MapNames.Dleks;
            dleksIconInMenuCopy.Button = dleksButton;
            dleksIconInMenuCopy.ButtonCheck = dleksMenuButtonCopy.Find("selectedCheck").GetComponent<SpriteRenderer>();
            dleksIconInMenuCopy.ButtonImage = dleksImage;
            dleksIconInMenuCopy.ButtonOutline = dleksImage.transform.parent.GetComponent<SpriteRenderer>();
            dleksIconInMenuCopy.Icon.sprite = skeldIconInMenu.Icon.sprite;

            dleksMenuButtonCopy.name = "Dleks";
            dleksMenuButtonCopy.position = new(dleksMenuButtonCopy.position.x, 2f * dleksMenuButtonCopy.position.y - polusMenuButton.transform.position.y, dleksMenuButtonCopy.position.z);
            fungleMenuButton.position = new(fungleMenuButton.position.x, dleksMenuButtonCopy.transform.position.y - 0.6f, fungleMenuButton.position.z);

            __instance.MapMenu.MapButtons = __instance.MapMenu.MapButtons.AddItem(dleksIconInMenuCopy).ToArray();

            float xPos = -1f;
            for (int index = 0; index < 6; ++index)
            {
                __instance.MapMenu.MapButtons[index].transform.SetLocalX(xPos);
                xPos += 0.34f;
            }

            if (__instance.mode == SettingsMode.Host)
            {
                mapPickerMenu.transform.localPosition = new(mapPickerMenu.transform.localPosition.x, 0.85f, mapPickerMenu.transform.localPosition.z);

                mapPickerTransform.localScale = new(0.86f, 0.85f, 1f);
                mapPickerTransform.transform.localPosition = new(mapPickerTransform.transform.localPosition.x + 0.05f, mapPickerTransform.transform.localPosition.y + 0.03f, mapPickerTransform.transform.localPosition.z);
            }

            SwapIconOrButtonPositions(airhipIconInMenu, dleksIconInMenuCopy);
            SwapIconOrButtonPositions(fungleIconInMenu, airhipIconInMenu);

            SwapIconOrButtonPositions(airshipMenuButton, dleksMenuButtonCopy);

            // set flipped dleks map Icon/button
            __instance.MapMenu.MapButtons[5].SetFlipped(true);

            mapPickerMenu.transform.Find("Backdrop").localScale *= 5;
        }

        private static void SwapIconOrButtonPositions(Component one, Component two)
        {
            Transform transform1 = one.transform;
            Transform transform2 = two.transform;
            Vector3 position1 = two.transform.position;
            Vector3 position2 = one.transform.position;
            transform1.position = position1;
            transform2.position = position2;
        }
    }
}