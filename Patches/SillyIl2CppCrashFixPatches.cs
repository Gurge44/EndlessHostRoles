using System;
using HarmonyLib;

namespace EHR.Patches;

// By https://github.com/TouseefX
[HarmonyPatch]
internal static class SillyIl2CppCrashFixPatches
{
    [HarmonyPatch(typeof(PassiveButton), nameof(PassiveButton.ReceiveClickDown)), HarmonyPrefix]
    public static bool ReceiveClickDownPrefix(PassiveButton __instance)
    {
        return __instance != null && __instance.Pointer != IntPtr.Zero;
    }

    [HarmonyPatch(typeof(PassiveButton), nameof(PassiveButton.ReceiveClickUp)), HarmonyPrefix]
    public static bool ReceiveClickUpPrefix(PassiveButton __instance)
    {
        return __instance != null && __instance.Pointer != IntPtr.Zero;
    }
}