using AmongUs.GameOptions;
using HarmonyLib;

// https://github.com/CrowdedMods/CrowdedMod/blob/master/src/CrowdedMod
namespace EHR.Patches
{
    internal static class Crowded
    {
        public const int MaxImpostors = 127 / 2;

        [HarmonyPatch(typeof(GameOptionsData), nameof(GameOptionsData.AreInvalid))]
        public static class InvalidOptionsPatches
        {
            public static bool Prefix(GameOptionsData __instance, [HarmonyArgument(0)] int maxExpectedPlayers)
            {
                return __instance.MaxPlayers > maxExpectedPlayers ||
                       __instance.NumImpostors < 1 ||
                       __instance.NumImpostors + 1 > maxExpectedPlayers / 2 ||
                       __instance.KillDistance is < 0 or > 2 ||
                       __instance.PlayerSpeedMod is <= 0f or > 3f;
            }
        }

        [HarmonyPatch(typeof(SecurityLogger), nameof(SecurityLogger.Awake))]
        public static class SecurityLoggerPatch
        {
            public static void Postfix(ref SecurityLogger __instance)
            {
                __instance.Timers = new float[127];
            }
        }
    }
}