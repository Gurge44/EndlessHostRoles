using HarmonyLib;

namespace TOHE;

[HarmonyPatch(typeof(OneWayShadows), nameof(OneWayShadows.IsIgnored))]
public static class OneWayShadowsIsIgnoredPatch
{
    public static bool Prefix(OneWayShadows __instance, ref bool __result)
    {
        if (__instance.IgnoreImpostor && Main.ResetCamPlayerList.Contains(PlayerControl.LocalPlayer.PlayerId))
        {
            __result = true;
            return false;
        }
        return true;
    }
}
