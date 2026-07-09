using System;
using HarmonyLib;

namespace EHR.Patches.ReversePatches;

[HarmonyPatch]
public static class ReversePatches
{
    [HarmonyReversePatch]
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.AssertWithTimeout))]
    internal static Il2CppSystem.Collections.IEnumerator AssertWithTimeout_Unpatched(this PlayerControl __instance, Func<bool> assertion, Action onTimeout, float timeoutInSeconds)
    {
        throw new Exception("Reverse Patch Stub");
    }
}