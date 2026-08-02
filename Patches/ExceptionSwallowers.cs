using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using InnerNet;

namespace EHR.Patches;

// These methods sometimes throw random exceptions in the base game code and stop the code after them from executing
// Simply swallow the exception and continue as if nothing happened

[HarmonyPatch]
static class ExceptionSwallowers
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(AbilityButton), nameof(AbilityButton.SetFromSettings));
        yield return AccessTools.Method(typeof(ActionButton), nameof(ActionButton.SetEnabled));
        yield return AccessTools.Method(typeof(PlayerControl), nameof(PlayerControl.RawSetName));
        yield return AccessTools.Method(typeof(PlayerControl), nameof(PlayerControl.SetName));
        yield return AccessTools.Method(typeof(CosmeticsLayer), nameof(CosmeticsLayer.UpdateBodyMaterial));
        yield return AccessTools.Method(typeof(CosmeticsCache), nameof(CosmeticsCache.ClearUnusedCosmetics));
        yield return AccessTools.Method(typeof(InnerNetClient), nameof(InnerNetClient.SendOrDisconnect));
        yield return AccessTools.Method(typeof(DisconnectPopup), nameof(DisconnectPopup.DoShow));
        yield return AccessTools.Method(typeof(IGameOptionsExtensions), nameof(IGameOptionsExtensions.GetValue));
    }
    
    public static Exception Finalizer()
    {
        return null;
    }
}