using HarmonyLib;

namespace EHR.Modules;

/// <summary>
/// Handles AU MCI (Among Us Modded Client Identification) registration for EHR.
/// See: https://github.com/Innersloth-LLC/AmongUsModdingInformation
///
/// On startup, EHR's GUID is written into the game's mod registration slot.
/// The matchmaker picks this up automatically and injects a mod filter into
/// every lobby search, so Find Game exclusively returns EHR lobbies to EHR clients.
///
/// The hosting side is handled by <see cref="TryGetModRegistrationGuidPatch"/>, which appends
/// EHR's GUID to the host packet so Innersloth's servers register the lobby
/// as modded and remove it from the vanilla matchmaking pool.
/// </summary>
public static class AmciRegistration
{
    /// <summary>
    /// EHR's AMCI mod GUID. Do not change this after it ships to players or they won't be able
    /// to find EHR lobbies until they update.
    /// </summary>
    private const string ModGuidString = "446420cc-ce35-4c2f-8f5d-b14f84354421";

    /// <summary>
    /// Writes EHR's GUID into the game's mod registration slot so the matchmaker
    /// can filter lobby searches to EHR lobbies only. 
    /// Call once from <see cref="Main.Load"/>.
    /// The base game code does the rest.
    /// </summary>
    public static void Apply()
    {
        CurrentModRegistration.ModRegistrationGuidString = ModGuidString;
        Logger.Info($"AMCI mod GUID registered: {ModGuidString}", nameof(AmciRegistration));
    }

    // Fixes not being able to host local games when the mod GUID is registered
    [HarmonyPatch(typeof(CurrentModRegistration), nameof(CurrentModRegistration.TryGetModRegistrationGuid))]
    public static class TryGetModRegistrationGuidPatch
    {
        public static bool Prefix(ref bool __result)
        {
            if (GameStates.IsLocalGame)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }
}
