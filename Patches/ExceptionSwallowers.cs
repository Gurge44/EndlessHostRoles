using System;
using HarmonyLib;
using InnerNet;

namespace EHR.Patches;

// These methods sometimes throw random exceptions in the base game code and stop the code after them from executing
// Simply swallow the exception and continue as if nothing happened

[HarmonyPatch(typeof(AbilityButton), nameof(AbilityButton.SetFromSettings))]
[HarmonyPatch(typeof(ActionButton), nameof(ActionButton.SetEnabled))]
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RawSetName))]
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetName))]
[HarmonyPatch(typeof(CosmeticsLayer), nameof(CosmeticsLayer.UpdateBodyMaterial))]
[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendOrDisconnect))]
static class ExceptionSwallowers
{
    public static Exception Finalizer()
    {
        return null;
    }
}