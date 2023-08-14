using HarmonyLib;

namespace TOHE.Patches;

[HarmonyPatch(typeof(Constants), nameof(Constants.GetBroadcastVersion))]
class ServerUpdatePatch
{
    public static bool Prefix(ref int __result)
    {
        if (GameStates.IsLocalGame)
        {
            Logger.Info($"IsLocalGame: {__result}", "VersionServer");
            return true;
        }

        // Changing server version for AU mods
        __result = Constants.GetVersion(2222, 0, 0, 0);

        Logger.Info($"IsOnlineGame: {__result}", "VersionServer");
        return false;
    }
}
