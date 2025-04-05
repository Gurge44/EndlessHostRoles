using AmongUs.Data.Player;
using HarmonyLib;

namespace EHR.Patches;

[HarmonyPatch(typeof(PlayerBanData), nameof(PlayerBanData.BanPoints), MethodType.Getter)]
public static class DisconnectPenaltyPatch
{
    public static bool Prefix(ref int __result)
    {
        __result = 0;
        return false;
    }
}