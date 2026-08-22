using System;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using InnerNet;

namespace EHR.Modules;

/// <summary>
/// Handles AU MCI (Among Us Modded Client Identification) registration for EHR.
/// See: https://github.com/Innersloth-LLC/AmongUsModdingInformation
///
/// On startup, EHR's GUID is written into the game's mod registration slot.
/// The matchmaker picks this up automatically and injects a mod filter into
/// every lobby search, so Find Game exclusively returns EHR lobbies to EHR clients.
///
/// The hosting side is handled by <see cref="HostGamePatch"/>, which appends
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
    /// Call once from <see cref="Main.Load"/>
    /// </summary>
    public static void Apply()
    {
        CurrentModRegistration.ModRegistrationGuidString = ModGuidString;
        Logger.Info($"AMCI mod GUID registered: {ModGuidString}", nameof(AmciRegistration));
    }

    /// <summary>
    /// Replaces the standard HostGame packet with the modded variant and appends
    /// EHR's GUID, so Innersloth's servers register this lobby as a modded EHR
    /// lobby and remove it from vanilla matchmaking.
    /// </summary>
    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.HostGame))]
    public static class HostGamePatch
    {
        public static bool Prefix(InnerNetClient __instance, IGameOptions settings, GameFilterOptions filterOpts)
        {
            if (!Guid.TryParse(ModGuidString, out Guid guid))
            {
                Logger.Warn("Failed to parse AMCI mod GUID, falling back to standard HostGame", nameof(AmciRegistration));
                return true;
            }

            MessageWriter msg = MessageWriter.Get(SendOption.Reliable);
            msg.StartMessage(Tags.HostModdedGame);
            msg.WriteBytesAndSize(__instance.gameOptionsFactory.ToBytes(settings, AprilFoolsMode.IsAprilFoolsModeToggledOn));
            msg.Write(CrossplayMode.GetCrossplayFlags());
            filterOpts.Serialize(msg);
            msg.Write(guid.ToByteArray());
            msg.EndMessage();
            __instance.SendOrDisconnect(msg);
            msg.Recycle();

            return false;
        }
    }
}
