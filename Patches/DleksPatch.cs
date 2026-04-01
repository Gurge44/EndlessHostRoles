using System;
using EHR.Patches;
using HarmonyLib;
using UnityEngine;

namespace EHR;

[HarmonyPatch(typeof(GameStartManager))]
internal static class AllMapIconsPatch
{
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

                    if (!Options.RandomMapsMode.GetBool()) CreateOptionsPickerPatch.SetDleks = true;
                }
            }, AmongUsClient.Instance.AmHost ? 1f : 4f, "Set Skeld Icon For Dleks Map");

            MapIconByName dleksIcon = Object.Instantiate(__instance, __instance.gameObject.transform).AllMapIcons[0];
            dleksIcon.Name = MapNames.Dleks;
            dleksIcon.MapImage = Utils.LoadSprite("EHR.Resources.Images.DleksBanner.png", 100f);
            dleksIcon.NameImage = Utils.LoadSprite("EHR.Resources.Images.DleksBanner-Wordart.png", 100f);
            __instance.AllMapIcons.Add(dleksIcon);

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