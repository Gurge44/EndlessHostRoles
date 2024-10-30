using HarmonyLib;

namespace EHR.Patches
{
    [HarmonyPatch(typeof(Constants), nameof(Constants.GetBroadcastVersion))]
    internal static class ServerUpdatePatch
    {
        public static void Postfix(ref int __result)
        {
            if (GameStates.IsLocalGame)
            {
                Logger.Info($"IsLocalGame: {__result}", "VersionServer");
            }

            if (GameStates.IsOnlineGame)
            {
                // Changing server version for AU mods
                __result += 25;
                Logger.Info($"IsOnlineGame: {__result}", "VersionServer");
            }
        }
    }

    [HarmonyPatch(typeof(Constants), nameof(Constants.IsVersionModded))]
    public static class IsVersionModdedPatch
    {
        public static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }
}