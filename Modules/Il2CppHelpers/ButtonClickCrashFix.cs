using System;
using HarmonyLib;

namespace EHR.Patches;

// By https://github.com/TouseefX
[HarmonyPatch]
internal static class ButtonClickCrashFix
{
    [HarmonyPatch(typeof(PassiveButton), nameof(PassiveButton.ReceiveClickDown)), HarmonyPrefix]
    public static bool ReceiveClickDownPrefix(PassiveButton __instance)
    {
        return __instance != null
#if IL2CPP
               && __instance.Pointer != IntPtr.Zero
#endif
            ;
    }

    [HarmonyPatch(typeof(PassiveButton), nameof(PassiveButton.ReceiveClickUp)), HarmonyPrefix]
    public static bool ReceiveClickUpPrefix(PassiveButton __instance)
    {
        return __instance != null
#if IL2CPP
               && __instance.Pointer != IntPtr.Zero
#endif
            ;
    }
}