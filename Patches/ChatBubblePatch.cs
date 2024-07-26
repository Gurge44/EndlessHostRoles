using AmongUs.GameOptions;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace EHR.Patches;

[HarmonyPatch(typeof(ChatBubble), nameof(ChatBubble.SetRight))]
static class ChatBubbleSetRightPatch
{
    public static void Postfix(ChatBubble __instance)
    {
        if (Main.IsChatCommand) __instance.SetLeft();
    }
}

[HarmonyPatch(typeof(ChatBubble), nameof(ChatBubble.SetName))]
static class ChatBubbleSetNamePatch
{
    public static void Postfix(ChatBubble __instance, [HarmonyArgument(2)] bool voted)
    {
        var seer = PlayerControl.LocalPlayer;
        var target = __instance.playerInfo.Object;

        if (GameStates.IsInGame && !voted && seer.PlayerId == target.PlayerId)
            __instance.NameText.color = seer.GetRoleColor();

        if (seer.GetCustomRole().GetDYRole() is RoleTypes.Shapeshifter or RoleTypes.Phantom)
            __instance.NameText.color = Color.white;

        if (Main.DarkTheme.Value)
        {
            __instance.Background.color = Color.black;
            __instance.TextArea.color = Color.white;
            if (!__instance.playerInfo.Object.IsAlive())
                __instance.Background.color = new(0f, 0f, 0f, 0.7f);
        }
    }
}

//Thanks https://github.com/NuclearPowered/Reactor/blob/master/Reactor/Patches/Fixes/CursorPosPatch.cs

/// <summary>
/// "Fixes" an issue where empty TextBoxes have wrong cursor positions.
/// </summary>
[HarmonyPatch(typeof(TextMeshProExtensions), nameof(TextMeshProExtensions.CursorPos))]
internal static class CursorPosPatch
{
    public static bool Prefix(TextMeshPro self, ref Vector2 __result)
    {
        if (self.textInfo == null || self.textInfo.lineCount == 0 || self.textInfo.lineInfo[0].characterCount <= 0)
        {
            __result = self.GetTextInfo(" ").lineInfo[0].lineExtents.max;
            return false;
        }

        return true;
    }
}