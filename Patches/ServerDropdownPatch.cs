using System;
using System.Linq;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.SceneManagement;

// ReSharper disable PossibleLossOfFraction

namespace EHR.Patches;

[HarmonyPatch(typeof(ServerDropdown), nameof(ServerDropdown.FillServerOptions))]
public static class ServerDropdownPatch
{
    public static bool Prepare()
    {
        // On Android, Starlight handles this automatically
        return !OperatingSystem.IsAndroid();
    }
    
    public static bool Prefix(ServerDropdown __instance)
    {
        if (SceneManager.GetActiveScene().name == "FindAGame") return true;
        SpriteRenderer background = __instance.background;
        background.size = new Vector2(4, 1);
        ServerManager serverManager = ServerManager.Instance;
        TranslationController translationController = TranslationController.Instance;
        var regions = serverManager.AvailableRegions.ToList();
        IRegionInfo currentRegion = serverManager.CurrentRegion;
        var displayRegions = regions.Where(region => region.Name != currentRegion.Name).ToList();
        int totalColumns = Mathf.Max(1, Mathf.CeilToInt(displayRegions.Count / 5f));
        int rowLimit = Mathf.Min(displayRegions.Count, 5);
        __instance.defaultButtonSelected = __instance.firstOption;
        __instance.firstOption.ChangeButtonText(translationController.GetStringWithDefault(currentRegion.TranslateName, currentRegion.Name, new Il2CppReferenceArray<Il2CppSystem.Object>(0)));

        for (var index = 0; index < displayRegions.Count; index++)
        {
            IRegionInfo regionInfo = displayRegions[index];
            var buttonPool = __instance.ButtonPool.Get<ServerListButton>();
            buttonPool.transform.localPosition = new Vector3(((index / 5) - ((totalColumns - 1) / 2f)) * 3.15f, __instance.y_posButton - (0.5f * (index % 5)), -1f);
            buttonPool.Text.text = translationController.GetStringWithDefault(regionInfo.TranslateName, regionInfo.Name, new Il2CppReferenceArray<Il2CppSystem.Object>(0));
            buttonPool.Text.ForceMeshUpdate();
            buttonPool.Button.OnClick.RemoveAllListeners();
            buttonPool.Button.OnClick.AddListener((Action)(() => __instance.ChooseOption(regionInfo)));
            __instance.controllerSelectable.Add(buttonPool.Button);
        }

        float height = 1.2f + (0.5f * (rowLimit - 1));
        float width = totalColumns > 1 ? (3.15f * (totalColumns - 1)) + background.size.x : background.size.x;
        background.transform.localPosition = new Vector3(0f, __instance.initialYPos - ((height - 1.2f) / 2f), 0f);
        background.size = new Vector2(width, height);
        return false;
    }
}
