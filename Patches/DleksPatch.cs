using EHR.Patches;
using HarmonyLib;
using System;
using System.Linq;

namespace EHR;

// Thanks: https://github.com/AU-Avengers/TOU-Mira/blob/main/TownOfUs/Patches/AprilFools/DleksMapOptionPickerPatches.cs
[HarmonyPatch(typeof(GameStartManager))]
internal static class AllMapIconsPatch
{
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
    [HarmonyPriority(Priority.First)]
    [HarmonyPrefix]
    public static void GameStartManagerStart_Prefix(GameStartManager __instance)
    {
        if (__instance.AllMapIcons.ToArray().Any(x => x.Name == MapNames.Dleks)) return;

        __instance.AllMapIcons.Insert((int)MapNames.Dleks, new MapIconByName
        {
            Name = MapNames.Dleks,
            MapIcon = Utils.LoadSprite("EHR.Resources.Images.DleksBanner-Wordart.png", 160f),
        });
    }
    [HarmonyPatch(nameof(GameStartManager.Start))]
    [HarmonyPostfix]
    public static void Postfix_AllMapIcons(GameStartManager __instance)
    {
        try
        {
            if (__instance == null) return;

            LateTask.New(() =>
            {
                if (Main.NormalOptions.MapId == 3)
                {
                    Main.NormalOptions.MapId = 0;
                    __instance.UpdateMapImage(MapNames.Skeld);

                    if (!Options.RandomMapsMode.GetBool()) GameOptionsMapPickerPatch.SetDleks = true;
                }
            }, AmongUsClient.Instance.AmHost ? 1f : 4f, "Set Skeld Icon For Dleks Map");

            if (SubmergedCompatibility.Loaded)
            {
                MapIconByName submergedIcon = Object.Instantiate(__instance, __instance.gameObject.transform).AllMapIcons[0];
                submergedIcon.Name = (MapNames)6;
                submergedIcon.MapImage = Utils.LoadSprite("EHR.Resources.Images.SubmergedBanner.png", 100f);
                submergedIcon.NameImage = Utils.LoadSprite("EHR.Resources.Images.Submerged-Wordart.png", 100f);
                __instance.AllMapIcons.Add(submergedIcon);
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.UpdateMapImage))]
    [HarmonyPrefix]
    public static bool Prefix_UpdateMapImage(GameStartManager __instance)
    {
        if (GameOptionsMapPickerPatch.SetDleks)
        {
            __instance.MapImage.sprite = Utils.LoadSprite("EHR.Resources.Images.DleksBanner-Wordart.png", 160f);
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(StringOption), nameof(StringOption.Start))]
public static class AutoselectDleksPatch
{
    public static void Postfix(StringOption __instance)
    {
        if (__instance.Title == StringNames.GameMapName)
        {
            // vanilla clamps this to not autoselect dleks
            __instance.Value = GameOptionsManager.Instance.CurrentGameOptions.MapId;
        }
    }
}

[HarmonyPatch]
public static class CreateGameOptionsPatch
{
    [HarmonyPatch(typeof(CreateGameOptions), nameof(CreateGameOptions.MapChanged))]
    [HarmonyPrefix]
    public static bool MapChangedPrefix(CreateGameOptions __instance)
    {
        if (__instance.mapPicker.GetSelectedID() is (int)MapNames.Dleks)
        {
            __instance.mapBanner.flipX = false;
            __instance.rendererBGCrewmates.sprite = __instance.bgCrewmates[0];
            __instance.mapBanner.sprite = Utils.LoadSprite("EHR.Resources.Images.DleksBanner-Wordart.png", 100f);
            __instance.TurnOffCrewmates();
            __instance.currentCrewSprites = __instance.skeldCrewSprites;
            __instance.SetCrewmateGraphic(__instance.capacityOption.Value - 1f);
            return false;
        }

        return true;
    }
    [HarmonyPatch(typeof(CreateGameOptions), nameof(CreateGameOptions.Start))]
    [HarmonyPrefix]
    public static void SetupMapBackground(CreateGameOptions __instance)
    {
        if (__instance.currentCrewSprites == null)
        {
            __instance.mapBanner.sprite = Utils.LoadSprite("EHR.Resources.Images.DleksBanner-Wordart.png", 100f);
        }
        __instance.currentCrewSprites ??= __instance.skeldCrewSprites;
        __instance.mapTooltips[3] = StringNames.ToolTipSkeld;
    }
}

[HarmonyPatch]
public static class MapSelectionGameSettingPatch
{
    [HarmonyPriority(Priority.VeryLow)]
    [HarmonyPatch(typeof(MapSelectionGameSetting), nameof(MapSelectionGameSetting.GetValueString))]
    [HarmonyPrefix]
    public static void AddToActualOptions(MapSelectionGameSetting __instance)
    {
        if (__instance.Values.All(x => (int)x != (int)StringNames.MapNameSkeld))
        {
            var list = __instance.Values.ToList();
            list.Insert((int)MapNames.Dleks, StringNames.MapNameSkeld);
            __instance.Values = list.ToArray();
        }
    }
}