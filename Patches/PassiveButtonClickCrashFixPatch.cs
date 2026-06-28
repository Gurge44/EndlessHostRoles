using System;
using HarmonyLib;

namespace EHR.Patches;

// By https://github.com/TouseefX
[HarmonyPatch(typeof(PassiveButton))]
internal static class PassiveButtonClickCrashFixPatch
{
    [HarmonyPatch(nameof(PassiveButton.ReceiveClickDown)), HarmonyPrefix]
    public static bool ReceiveClickDownPrefix(PassiveButton __instance)
    {
        return __instance != null && __instance.Pointer != IntPtr.Zero;
    }

    [HarmonyPatch(nameof(PassiveButton.ReceiveClickUp)), HarmonyPrefix]
    public static bool ReceiveClickUpPrefix(PassiveButton __instance)
    {
        return __instance != null && __instance.Pointer != IntPtr.Zero;
    }
}