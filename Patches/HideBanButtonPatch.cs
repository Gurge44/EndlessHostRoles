using HarmonyLib;

namespace EHR;

[HarmonyPatch(typeof(ChatController), nameof(ChatController.Toggle))]
class CancelBanMenuStuckPatch
{
    public static void Prefix(ChatController __instance)
    {
        if (__instance.IsOpenOrOpening && !__instance.IsAnimating)
        {
            __instance.banButton.SetVisible(false);
        }
    }
}