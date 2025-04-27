using System;
using System.Linq;
using AmongUs.Data;
using EHR.Modules;
using EHR.Patches;
using HarmonyLib;
using Hazel;
using Il2CppSystem.Collections.Generic;
using InnerNet;
using TMPro;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.MakePublic))]
internal static class MakePublicPatch
{
    public static bool Prefix()
    {
        if (ModUpdater.IsBroken || (ModUpdater.HasUpdate && ModUpdater.ForceUpdate) || !VersionChecker.IsSupported)
        {
            var message = string.Empty;
            if (!VersionChecker.IsSupported) message = GetString("UnsupportedVersion");

            if (ModUpdater.IsBroken) message = GetString("ModBrokenMessage");

            if (ModUpdater.HasUpdate) message = GetString("CanNotJoinPublicRoomNoLatest");

            Logger.Info(message, "MakePublicPatch");
            Logger.SendInGame(message);
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(MMOnlineManager), nameof(MMOnlineManager.Start))]
// ReSharper disable once InconsistentNaming
internal static class MMOnlineManagerStartPatch
{
    public static void Postfix()
    {
        if (!((ModUpdater.HasUpdate && ModUpdater.ForceUpdate) || ModUpdater.IsBroken)) return;

        GameObject obj = GameObject.Find("FindGameButton");

        if (obj)
        {
            obj.SetActive(false);
            TextMeshPro textObj = Object.Instantiate(obj.transform.FindChild("Text_TMP").GetComponent<TextMeshPro>());
            textObj.transform.position = new(1f, -0.3f, 0);
            textObj.name = "CanNotJoinPublic";

            string message = ModUpdater.IsBroken
                ? $"<size=2>{Utils.ColorString(Color.red, GetString("ModBrokenMessage"))}</size>"
                : $"<size=2>{Utils.ColorString(Color.red, GetString("CanNotJoinPublicRoomNoLatest"))}</size>";

            LateTask.New(() => { textObj.text = message; }, 0.01f, "CanNotJoinPublic");
        }
    }
}

[HarmonyPatch(typeof(SplashManager), nameof(SplashManager.Update))]
internal static class SplashLogoAnimatorPatch
{
    public static void Prefix(SplashManager __instance)
    {
        __instance.sceneChanger.AllowFinishLoadingScene();
        __instance.startedSceneLoad = true;
    }
}

[HarmonyPatch(typeof(EOSManager), nameof(EOSManager.IsAllowedOnline))]
internal static class RunLoginPatch
{
    public const int ClickCount = 0;

    public static void Prefix(ref bool canOnline)
    {
        if (DebugModeManager.AmDebugger) canOnline = true;
    }
}

[HarmonyPatch(typeof(BanMenu), nameof(BanMenu.SetVisible))]
internal static class BanMenuSetVisiblePatch
{
    public static bool Prefix(BanMenu __instance, bool show)
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        show &= PlayerControl.LocalPlayer && PlayerControl.LocalPlayer.Data != null;
        __instance.BanButton.gameObject.SetActive(AmongUsClient.Instance.CanBan());
        __instance.KickButton.gameObject.SetActive(AmongUsClient.Instance.CanKick());
        __instance.MenuButton.gameObject.SetActive(show);
        return false;
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.CanBan))]
internal static class InnerNetClientCanBanPatch
{
    public static bool Prefix(InnerNetClient __instance, ref bool __result)
    {
        __result = __instance.AmHost;
        return false;
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.KickPlayer))]
internal static class KickPlayerPatch
{
    public static bool Prefix( /*InnerNetClient __instance,*/ int clientId, bool ban)
    {
        if (!AmongUsClient.Instance.AmHost && !OnGameJoinedPatch.JoiningGame) return true;

        if (AmongUsClient.Instance.ClientId == clientId)
        {
            Logger.SendInGame($"Game Attempting to {(ban ? "Ban" : "Kick")} Host, Blocked the attempt.");
            Logger.Info("Game attempted to kick/ban host....", "KickPlayerPatch");
            return false;
        }

        if (ban) BanManager.AddBanPlayer(AmongUsClient.Instance.GetRecentClient(clientId));

        return true;
    }
}

[HarmonyPatch(typeof(ResolutionManager), nameof(ResolutionManager.SetResolution))]
internal static class SetResolutionManager
{
    public static void Postfix()
    {
        if (MainMenuManagerPatch.UpdateButton != null) MainMenuManagerPatch.UpdateButton.transform.localPosition = MainMenuManagerPatch.Template.transform.localPosition + new Vector3(0.25f, 0.75f);
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendAllStreamedObjects))]
internal static class InnerNetObjectSerializePatch
{
    public static void Prefix()
    {
        if (AmongUsClient.Instance.AmHost) Main.Instance.StartCoroutine(GameOptionsSender.SendAllGameOptionsAsync());
    }
}

[HarmonyPatch(typeof(InnerNetClient))]
public static class InnerNetClientPatch
{
    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendInitialData))]
    [HarmonyPrefix]
    public static bool SendInitialDataPrefix(InnerNetClient __instance, int clientId)
    {
        if (!Constants.IsVersionModded() || __instance.NetworkMode != NetworkModes.OnlineGame) return true;
        MessageWriter messageWriter = MessageWriter.Get(SendOption.Reliable);
        messageWriter.StartMessage(6);
        messageWriter.Write(__instance.GameId);
        messageWriter.WritePacked(clientId);
        List<InnerNetObject> obj = __instance.allObjects;

        lock (obj)
        {
            System.Collections.Generic.HashSet<GameObject> hashSet = [];

            for (var i = 0; i < __instance.allObjects.Count; i++)
            {
                if (messageWriter.Length > 800)
                {
                    messageWriter.EndMessage();
                    __instance.SendOrDisconnect(messageWriter);
                    messageWriter.Recycle();
                    messageWriter = MessageWriter.Get(SendOption.Reliable);
                    messageWriter.StartMessage(6);
                    messageWriter.Write(__instance.GameId);
                    messageWriter.WritePacked(clientId);
                }

                InnerNetObject innerNetObject = __instance.allObjects[i]; // False error

                if (innerNetObject && (innerNetObject.OwnerId != -4 || __instance.AmModdedHost) && hashSet.Add(innerNetObject.gameObject))
                {
                    var gameManager = innerNetObject as GameManager;

                    if (gameManager != null)
                        __instance.SendGameManager(clientId, gameManager);
                    else
                        __instance.WriteSpawnMessage(innerNetObject, innerNetObject.OwnerId, innerNetObject.SpawnFlags, messageWriter);
                }
            }

            messageWriter.EndMessage();
            __instance.SendOrDisconnect(messageWriter);
            messageWriter.Recycle();
        }

        return false;
    }

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendAllStreamedObjects))]
    [HarmonyPrefix]
    public static bool SendAllStreamedObjectsPrefix(InnerNetClient __instance, ref bool __result)
    {
        if (!Constants.IsVersionModded() || __instance.NetworkMode != NetworkModes.OnlineGame) return true;
        __result = false;
        List<InnerNetObject> obj = __instance.allObjects;

        lock (obj)
        {
            for (var i = 0; i < __instance.allObjects.Count; i++)
            {
                InnerNetObject innerNetObject = __instance.allObjects[i]; // False error

                if (innerNetObject && innerNetObject.IsDirty && (innerNetObject.AmOwner || (innerNetObject.OwnerId == -2 && __instance.AmHost)))
                {
                    MessageWriter messageWriter = __instance.Streams[(int)innerNetObject.sendMode];
                    if (messageWriter.Length > 800)
                    {
                        messageWriter.EndMessage();
                        __instance.SendOrDisconnect(messageWriter);
                        messageWriter.Clear(innerNetObject.sendMode);
                        messageWriter.StartMessage(5);
                        messageWriter.Write(__instance.GameId);
                    }
                    messageWriter.StartMessage(1);
                    messageWriter.WritePacked(innerNetObject.NetId);

                    try
                    {
                        if (innerNetObject.Serialize(messageWriter, false))
                            messageWriter.EndMessage();
                        else
                            messageWriter.CancelMessage();

                        if (innerNetObject.Chunked && innerNetObject.IsDirty)
                            __result = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Exception(ex, "SendAllStreamedObjectsPrefix");
                        messageWriter.CancelMessage();
                    }
                }
            }
        }

        for (var j = 0; j < __instance.Streams.Length; j++)
        {
            MessageWriter messageWriter2 = __instance.Streams[j];

            if (messageWriter2.HasBytes(7))
            {
                messageWriter2.EndMessage();
                __instance.SendOrDisconnect(messageWriter2);
                messageWriter2.Clear((SendOption)j);
                messageWriter2.StartMessage(5);
                messageWriter2.Write(__instance.GameId);
            }
        }

        return false;
    }

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.Spawn))]
    [HarmonyPostfix]
    public static void Spawn_Postfix(InnerNetClient __instance, InnerNetObject netObjParent, int ownerId = -2, SpawnFlags flags = SpawnFlags.None)
    {
        if (!Constants.IsVersionModded() || __instance.NetworkMode != NetworkModes.OnlineGame) return;

        if (__instance.AmHost)
        {
            switch (netObjParent)
            {
                case NetworkedPlayerInfo playerinfo:
                    LateTask.New(() =>
                    {
                        if (playerinfo != null && AmongUsClient.Instance.AmConnected)
                        {
                            ClientData client = AmongUsClient.Instance.GetClient(playerinfo.ClientId);

                            if (client != null && !client.IsDisconnected())
                            {
                                if (playerinfo.IsIncomplete)
                                {
                                    Logger.Info($"Disconnecting Client [{client.Id}]{client.PlayerName} {client.FriendCode} for playerinfo timeout", "DelayedNetworkedData");
                                    AmongUsClient.Instance.SendLateRejection(client.Id, DisconnectReasons.ClientTimeout);
                                    __instance.OnPlayerLeft(client, DisconnectReasons.ClientTimeout);
                                }
                            }
                        }
                    }, 5f, "PlayerInfo Green Bean Kick", false);

                    break;
                case PlayerControl player:
                    LateTask.New(() =>
                    {
                        if (player != null && !player.notRealPlayer && !player.isDummy && AmongUsClient.Instance.AmConnected)
                        {
                            ClientData client = AmongUsClient.Instance.GetClient(player.OwnerId);

                            if (client != null && !client.IsDisconnected())
                            {
                                if (player.Data == null || player.Data.IsIncomplete)
                                {
                                    Logger.Info($"Disconnecting Client [{client.Id}]{client.PlayerName} {client.FriendCode} for playercontrol timeout", "DelayedNetworkedData");
                                    AmongUsClient.Instance.SendLateRejection(client.Id, DisconnectReasons.ClientTimeout);
                                    __instance.OnPlayerLeft(client, DisconnectReasons.ClientTimeout);
                                }
                            }
                        }
                    }, 5.5f, "PlayerControl Green Bean Kick", false);

                    break;
            }
        }

        if (!__instance.AmHost)
            Debug.LogError("Tried to spawn while not host: " + netObjParent?.ToString());
    }
}

[HarmonyPatch(typeof(GameData), nameof(GameData.DirtyAllData))]
internal static class DirtyAllDataPatch
{
    public static bool Prefix()
    {
        return false;
    }
}

[HarmonyPatch(typeof(NetworkedPlayerInfo), nameof(NetworkedPlayerInfo.Serialize))]
internal static class NetworkedPlayerInfoSerializePatch
{
    public static int IgnorePatchTimes;

    public static bool Prefix(NetworkedPlayerInfo __instance, MessageWriter writer, bool initialState, ref bool __result)
    {
        if (IgnorePatchTimes > 0)
        {
            IgnorePatchTimes--;
            return true;
        }

        writer.Write(__instance.PlayerId);
        writer.WritePacked(__instance.ClientId);
        writer.Write((byte)__instance.Outfits.Count);

        foreach (KeyValuePair<PlayerOutfitType, NetworkedPlayerInfo.PlayerOutfit> keyValuePair in __instance.Outfits)
        {
            writer.Write((byte)keyValuePair.Key);

            if (keyValuePair.Key is PlayerOutfitType.Default)
            {
                NetworkedPlayerInfo.PlayerOutfit oldOutfit = keyValuePair.Value;
                NetworkedPlayerInfo.PlayerOutfit playerOutfit = new();
                Main.AllClientRealNames.TryGetValue(__instance.ClientId, out string name);

                if (CheckForEndVotingPatch.TempExiledPlayer != null && CheckForEndVotingPatch.TempExiledPlayer.ClientId == __instance.ClientId)
                    name = CheckForEndVotingPatch.EjectionText;

                playerOutfit.Set(name ?? " ", oldOutfit.ColorId, oldOutfit.HatId, oldOutfit.SkinId, oldOutfit.VisorId, oldOutfit.PetId, oldOutfit.NamePlateId);
                playerOutfit.Serialize(writer);
            }
            else
                keyValuePair.Value.Serialize(writer);
        }

        writer.WritePacked(__instance.PlayerLevel);
        byte b = 0;
        if (__instance.Disconnected) b |= 1;
        if (__instance.IsDead) b |= 4;
        writer.Write(b);
        writer.Write((ushort)__instance.Role.Role);
        writer.Write(false);

        if (__instance.Tasks != null)
        {
            writer.Write((byte)__instance.Tasks.Count);

            for (var i = 0; i < __instance.Tasks.Count; i++)
                __instance.Tasks[i].Serialize(writer); // False error
        }
        else
            writer.Write(0);

        writer.Write(__instance.FriendCode ?? string.Empty);

        if (GameStates.CurrentServerType == GameStates.ServerType.Vanilla)
            writer.Write(__instance.Puid ?? string.Empty);
        else
            writer.Write(string.Empty);

        if (!initialState) __instance.ClearDirtyBits();
        __result = true;
        return false;
    }
}

// Next 4: https://github.com/Rabek009/MoreGamemodes/blob/master/Patches/ClientPatch.cs

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CheckOnlinePermissions))]
static class CheckOnlinePermissionsPatch
{
    public static void Prefix()
    {
        DataManager.Player.Ban.banPoints = 0f;
    }
}
 
[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.StartRpc))]
static class StartRpcPatch
{
    public static void Prefix(InnerNetClient __instance, [HarmonyArgument(0)] uint targetNetId, [HarmonyArgument(1)] byte callId, [HarmonyArgument(2)] SendOption option)
    {
        MessageWriter writer = __instance.Streams[(int)option];
        if (writer.Length > 800)
        {
            writer.EndMessage();
            __instance.SendOrDisconnect(writer);
            writer.Clear(option);
            writer.StartMessage(5);
            writer.Write(__instance.GameId);
        }
    }
}
 
[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.Spawn))]
static class SpawnPatch
{
    public static void Prefix(InnerNetClient __instance)
    {
        MessageWriter writer = __instance.Streams[1];
        if (writer.Length > 800)
        {
            writer.EndMessage();
            __instance.SendOrDisconnect(writer);
            writer.Clear(SendOption.Reliable);
            writer.StartMessage(5);
            writer.Write(__instance.GameId);
        }
    }
}
 
[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.Despawn))]
static class DespawnPatch
{
    public static void Prefix(InnerNetClient __instance)
    {
        MessageWriter writer = __instance.Streams[1];
        if (writer.Length > 800)
        {
            writer.EndMessage();
            __instance.SendOrDisconnect(writer);
            writer.Clear(SendOption.Reliable);
            writer.StartMessage(5);
            writer.Write(__instance.GameId);
        }
    }
}

[HarmonyPatch]
internal static class AuthTimeoutPatch
{
    [HarmonyPatch(typeof(AuthManager._CoConnect_d__4), nameof(AuthManager._CoConnect_d__4.MoveNext))]
    [HarmonyPatch(typeof(AuthManager._CoWaitForNonce_d__6), nameof(AuthManager._CoWaitForNonce_d__6.MoveNext))]
    [HarmonyPrefix]
    // From Reactor.gg
    // https://github.com/NuclearPowered/Reactor/blob/master/Reactor/Patches/Miscellaneous/CustomServersPatch.cs
    public static bool CoWaitforNoncePrefix(ref bool __result)
    {
        if (GameStates.CurrentServerType == GameStates.ServerType.Vanilla) return true;

        __result = false;
        return false;
    }

    // If you don't patch this, you still need to wait for 5 s.
    // I have no idea why this is happening
    [HarmonyPatch(typeof(AmongUsClient._CoJoinOnlinePublicGame_d__1), nameof(AmongUsClient._CoJoinOnlinePublicGame_d__1.MoveNext))]
    [HarmonyPrefix]
    public static void EnableUdpMatchmakingPrefix(AmongUsClient._CoJoinOnlinePublicGame_d__1 __instance)
    {
        // Skip to state 1, which just calls CoJoinOnlineGameDirect
        if (__instance.__1__state == 0 && !ServerManager.Instance.IsHttp)
        {
            __instance.__1__state = 1;

            __instance.__8__1 = new()
            {
                matchmakerToken = string.Empty
            };
        }
    }
}