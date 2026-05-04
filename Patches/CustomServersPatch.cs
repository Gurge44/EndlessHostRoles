using System;
using System.Linq;
using HarmonyLib;

namespace EHR.Patches;

// From: https://github.com/NuclearPowered/Reactor/blob/master/Reactor/Patches/Miscellaneous/CustomServersPatch.cs
[HarmonyPatch]
internal static class CustomServersPatch
{
    private static bool IsCurrentServerOfficial()
    {
        const string domain = "among.us";

        return ServerManager.Instance.CurrentRegion?.TryCast<StaticHttpRegionInfo>() is { } regionInfo &&
               regionInfo.PingServer.EndsWith(domain, StringComparison.Ordinal) &&
               regionInfo.Servers.All(serverInfo => serverInfo.Ip.EndsWith(domain, StringComparison.Ordinal));
    }

    [HarmonyPatch(typeof(AuthManager._CoConnect_d__4), "MoveNext")]
    [HarmonyPatch(typeof(AuthManager._CoWaitForNonce_d__6), "MoveNext")]
    static class DisableAuthServerPatch
    {
        public static bool Prepare()
        {
            return !OperatingSystem.IsAndroid();
        }
        
        public static bool Prefix(ref bool __result)
        {
            if (IsCurrentServerOfficial())
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(AmongUsClient._CoJoinOnlinePublicGame_d__49), "MoveNext")]
    static class EnableUdpMatchmakingPatch
    {
        public static bool Prepare()
        {
            return !OperatingSystem.IsAndroid();
        }

        public static void Prefix(AmongUsClient._CoJoinOnlinePublicGame_d__49 __instance)
        {
            // Skip to state 1 which just calls CoJoinOnlineGameDirect
            if (__instance.__1__state == 0 && !ServerManager.Instance.IsHttp)
            {
                __instance.__1__state = 1;
                __instance.__8__1 = new AmongUsClient.__c__DisplayClass49_0
                {
                    matchmakerToken = string.Empty,
                };
            }
        }
    }
}
