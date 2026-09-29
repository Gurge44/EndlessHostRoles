using HarmonyLib;
using InnerNet;
using TMPro;
using UnityEngine;

// Credits: https://github.com/D1GQ/BetterAmongUs/blob/main/src/Patches/Gameplay/Managers/FindAGameManagerClass/FindAGameManagerPatch.cs

namespace EHR.Patches;

[HarmonyPatch]
internal static class FindAGameManagerPatch
{
    private const string TmpObjectName = "LobbyInfo_TMP";

    [HarmonyPatch(typeof(FindAGameManager), nameof(FindAGameManager.HandleList))]
    [HarmonyPostfix]
    private static void HandleList_Postfix(FindAGameManager __instance, HttpMatchmakerManager.FindGamesListFilteredResponse response)
    {
        // Add platform and game code info to each visible game container
        foreach (var container in __instance.gameContainers)
        {
            if (container == null || !container.gameObject.activeSelf) continue;

            Transform child = container.transform.Find("Container");
            if (child == null) continue;

            // Reuse existing TMP if already created, otherwise create a new one
            Transform existing = child.Find(TmpObjectName);
            TMP_Text tmpro = existing != null
                ? existing.GetComponent<TMP_Text>()
                : CreateTMP(child);

            if (tmpro == null) continue;

            tmpro.font = container.capacity?.font;
            tmpro.fontSize = 3f;
            tmpro.text = BuildText(container.gameListing);
        }
    }

    private static TMP_Text CreateTMP(Transform parent)
    {
        // Create a new TextMeshPro object positioned below the host name
        var obj = new GameObject(TmpObjectName);
        obj.transform.SetParent(parent, false);

        var pos = obj.AddComponent<AspectPosition>();
        pos.Alignment = AspectPosition.EdgeAlignments.Center;
        pos.anchorPoint = new Vector2(0.2f, 0.5f);
        pos.DistanceFromEdge = new Vector3(10.9f, -2.17f, -2f);
        pos.AdjustPosition();

        return obj.AddComponent<TextMeshPro>();
    }

    private static string BuildText(GameListing listing)
    {
        // Show true host name if available, fall back to display name
        var host = !string.IsNullOrEmpty(listing.TrueHostName)
            ? listing.TrueHostName
            : listing.HostName;

        var platform = GetPlatformName(listing.Platform);
        var code = GameCode.IntToGameName(listing.GameId);

        return $"{host}\n<size=65%>{platform} ({code})";
    }

    private static string GetPlatformName(Platforms platform) => platform switch
    {
        (Platforms)112 => "Starlight",
        Platforms.StandaloneSteamPC => "Steam",
        Platforms.StandaloneEpicPC => "Epic Games",
        Platforms.StandaloneWin10 => "Microsoft Store",
        Platforms.StandaloneItch => "Itch.io",
        _ => string.Empty
    };
}
