using System;
using HarmonyLib;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;

namespace EHR.Patches;

// This is taken from Reactor modded handshake.
// We do not want to pull in Reactor as a dependency though.

/// <summary>
/// Represents flags of the mod.
/// </summary>
[Flags]
public enum ModFlags : ushort
{
    /// <summary>
    /// No flags.
    /// </summary>
    None = 0,

    /// <summary>
    /// Requires all clients in a lobby to have the mod.
    /// </summary>
    RequireOnAllClients = 1 << 0,

    /// <summary>
    /// Requires the server to have a plugin that handles the mod.
    /// </summary>
    RequireOnServer = 1 << 1,

    /// <summary>
    /// Requires the host of the lobby to have the mod.
    /// </summary>
    RequireOnHost = 1 << 2,

    /// <summary>
    /// Notifies the game server that the host has authority over game logic.
    /// </summary>
    DisableServerAuthority = 1 << 3,
}


/// <summary>
///     Version of the Reactor.Networking protocol format.
/// </summary>
public enum ReactorProtocolVersion : byte
{
    /// <summary>
    ///     First public Reactor Protocol version.
    /// </summary>
    V2 = 1,

    /// <summary>
    ///     Version introducing vanilla server support, syncer concept and registries.
    /// </summary>
    V3 = 2,

    /// <summary>
    ///     Latest version.
    /// </summary>
    Latest = V3,
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.GetConnectionData))]
public static class HandshakePatch
{
    private const ulong MAGIC = 0x72656163746f72; // "reactor" in ascii, 7 bytes

    public static void Prefix(ref bool useDtlsLayout)
    {
        // Due to reasons currently unknown, the useDtlsLayout parameter sometimes doesn't reflect whether DTLS
        // is actually supposed to be enabled. This causes a bad handshake message and a quick disconnect.
        // The field on AmongUsClient appears to be more reliable, so override this parameter with what it is supposed to be.
        useDtlsLayout = AmongUsClient.Instance.useDtls;
    }

    public static void Postfix(ref Il2CppStructArray<byte> __result)
    {
        var handshake = new MessageWriter(1000);

        // Original data
        handshake.Write(__result);

        // Reactor Header
        var version = (byte) ReactorProtocolVersion.Latest;
        var value = (MAGIC << 8) | version;
        handshake.Write(value);

        // ModdedHandshakeC2S
        handshake.WritePacked(1);
        handshake.Write(Main.PluginGuid);
        handshake.Write(Main.PluginVersion);
        handshake.Write((ushort) (ModFlags.RequireOnHost | ModFlags.RequireOnAllClients));

        __result = handshake.ToByteArray(true);
        handshake.Recycle();
    }
}