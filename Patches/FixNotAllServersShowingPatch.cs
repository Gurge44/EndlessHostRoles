using System;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.SceneManagement;

// ReSharper disable PossibleLossOfFraction

namespace EHR.Patches;

[HarmonyPatch(typeof(ServerDropdown), nameof(ServerDropdown.FillServerOptions))]
public static class FixNotAllServersShowingPatch
{
    public static bool Prefix(ServerDropdown __instance)
    {
        if (SceneManager.GetActiveScene().name == "FindAGame") return true;
        SpriteRenderer bg = __instance.background;
        bg.size = new Vector2(4, 1);
        ServerManager sm = FastDestroyableSingleton<ServerManager>.Instance;
        TranslationController tc = FastDestroyableSingleton<TranslationController>.Instance;
        int totalCols = Mathf.Max(1, Mathf.CeilToInt(sm.AvailableRegions.Length / (float)5));
        int rowLimit = Mathf.Min(sm.AvailableRegions.Length, 5);

        for (var index = 0; index < sm.AvailableRegions.Length; index++)
        {
            IRegionInfo ri = sm.AvailableRegions[index];
            var b = __instance.ButtonPool.Get<ServerListButton>();
            b.transform.localPosition = new Vector3(((index / 5) - ((totalCols - 1) / 2f)) * 3.15f, __instance.y_posButton - (0.5f * (index % 5)), -1f);
            b.Text.text = tc.GetStringWithDefault(ri.TranslateName, ri.Name, new Il2CppReferenceArray<Il2CppSystem.Object>(0));
            b.Text.ForceMeshUpdate();
            b.Button.OnClick.RemoveAllListeners();
            b.Button.OnClick.AddListener((Action)(() => __instance.ChooseOption(ri)));
            __instance.controllerSelectable.Add(b.Button);
        }

        float h = 1.2f + (0.5f * (rowLimit - 1));
        float w = totalCols > 1 ? (3.15f * (totalCols - 1)) + bg.size.x : bg.size.x;
        bg.transform.localPosition = new Vector3(0f, __instance.initialYPos - ((h - 1.2f) / 2f), 0f);
        bg.size = new Vector2(w, h);
        return false;
    }
}