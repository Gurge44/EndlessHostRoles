using HarmonyLib;

namespace EHR;

// From: https://github.com/yukieiji/ExtremeRoles/blob/master/ExtremeRoles/Patches/AirshipStatusPatch.cs
[HarmonyPatch(typeof(AirshipStatus), nameof(AirshipStatus.PrespawnStep))]
public static class AirshipStatusPrespawnStepPatch
{
    public static bool Prefix()
    {
        return !PlayerControl.LocalPlayer.Is(CustomRoles.GM);
    }
}