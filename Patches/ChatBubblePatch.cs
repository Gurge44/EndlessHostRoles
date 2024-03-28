using HarmonyLib;
using UnityEngine;

namespace EHR.Patches;

[HarmonyPatch(typeof(ChatBubble), nameof(ChatBubble.SetRight))]
class ChatBubbleSetRightPatch
{
    public static void Postfix(ChatBubble __instance)
    {
        if (Main.IsChatCommand) __instance.SetLeft();
    }
}
[HarmonyPatch(typeof(ChatBubble), nameof(ChatBubble.SetName))]
class ChatBubbleSetNamePatch
{
    public static void Postfix(ChatBubble __instance)
    {
        if (GameStates.IsInGame && __instance.playerInfo.PlayerId == PlayerControl.LocalPlayer.PlayerId)
            __instance.NameText.color = PlayerControl.LocalPlayer.GetRoleColor();
        if (Main.DarkTheme.Value)
        {
            __instance.Background.color = Color.black;
            __instance.TextArea.color = Color.white;
        }
    }
}