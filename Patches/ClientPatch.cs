using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using HarmonyLib;
using Hazel;
using InnerNet;
using TMPro;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.MakePublic))]
internal class MakePublicPatch
{
    public static bool Prefix()
    {
        if (ModUpdater.isBroken || (ModUpdater.hasUpdate && ModUpdater.forceUpdate) || !VersionChecker.IsSupported)
        {
            var message = string.Empty;
            if (!VersionChecker.IsSupported) message = GetString("UnsupportedVersion");
            if (ModUpdater.isBroken) message = GetString("ModBrokenMessage");
            if (ModUpdater.hasUpdate) message = GetString("CanNotJoinPublicRoomNoLatest");
            Logger.Info(message, "MakePublicPatch");
            Logger.SendInGame(message);
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(MMOnlineManager), nameof(MMOnlineManager.Start))]
// ReSharper disable once InconsistentNaming
internal class MMOnlineManagerStartPatch
{
    public static void Postfix()
    {
        if (!((ModUpdater.hasUpdate && ModUpdater.forceUpdate) || ModUpdater.isBroken)) return;
        var obj = GameObject.Find("FindGameButton");
        if (obj)
        {
            obj.SetActive(false);
            var textObj = Object.Instantiate(obj.transform.FindChild("Text_TMP").GetComponent<TextMeshPro>());
            textObj.transform.position = new(1f, -0.3f, 0);
            textObj.name = "CanNotJoinPublic";
            var message = ModUpdater.isBroken
                ? $"<size=2>{Utils.ColorString(Color.red, GetString("ModBrokenMessage"))}</size>"
                : $"<size=2>{Utils.ColorString(Color.red, GetString("CanNotJoinPublicRoomNoLatest"))}</size>";
            LateTask.New(() => { textObj.text = message; }, 0.01f, "CanNotJoinPublic");
        }
    }
}

[HarmonyPatch(typeof(SplashManager), nameof(SplashManager.Update))]
internal class SplashLogoAnimatorPatch
{
    public static void Prefix(SplashManager __instance)
    {
        __instance.sceneChanger.AllowFinishLoadingScene();
        __instance.startedSceneLoad = true;
    }
}

[HarmonyPatch(typeof(EOSManager), nameof(EOSManager.IsAllowedOnline))]
internal class RunLoginPatch
{
    public const int ClickCount = 0;

    public static void Prefix(ref bool canOnline)
    {
        if (DebugModeManager.AmDebugger)
            canOnline = true;
    }
}

[HarmonyPatch(typeof(BanMenu), nameof(BanMenu.SetVisible))]
internal class BanMenuSetVisiblePatch
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
internal class InnerNetClientCanBanPatch
{
    public static bool Prefix(InnerNetClient __instance, ref bool __result)
    {
        __result = __instance.AmHost;
        return false;
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.KickPlayer))]
internal class KickPlayerPatch
{
    public static bool Prefix( /*InnerNetClient __instance,*/ int clientId, bool ban)
    {
        if (!AmongUsClient.Instance.AmHost) return true;

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
internal class SetResolutionManager
{
    public static void Postfix()
    {
        //if (MainMenuManagerPatch.qqButton != null)
        //    MainMenuManagerPatch.qqButton.transform.localPosition = Vector3.Reflect(MainMenuManagerPatch.template.transform.localPosition, Vector3.left);
        //if (MainMenuManagerPatch.discordButton != null)
        //    MainMenuManagerPatch.discordButton.transform.localPosition = Vector3.Reflect(MainMenuManagerPatch.template.transform.localPosition, Vector3.left);
        if (MainMenuManagerPatch.UpdateButton != null)
            MainMenuManagerPatch.UpdateButton.transform.localPosition = MainMenuManagerPatch.Template.transform.localPosition + new Vector3(0.25f, 0.75f);
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendAllStreamedObjects))]
internal class InnerNetObjectSerializePatch
{
    public static void Prefix()
    {
        if (AmongUsClient.Instance.AmHost)
            Main.Instance.StartCoroutine(GameOptionsSender.SendAllGameOptions());
    }
}

public class InnerNetClientPatch
{
    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendInitialData))]
    [HarmonyPrefix]
    public static bool SendInitialDataPrefix(InnerNetClient __instance, int clientId)
    {
        if (!Constants.IsVersionModded()) return true;
        // We make sure other stuff like PlayerControl and LobbyBehavior is spawned properly
        // Then we spawn the networked data for new clients
        MessageWriter messageWriter = MessageWriter.Get(SendOption.Reliable);
        messageWriter.StartMessage(6);
        messageWriter.Write(__instance.GameId);
        messageWriter.WritePacked(clientId);
        Il2CppSystem.Collections.Generic.List<InnerNetObject> obj = __instance.allObjects;
        lock (obj)
        {
            HashSet<GameObject> hashSet = [];
            for (int i = 0; i < __instance.allObjects.Count; i++)
            {
                InnerNetObject innerNetObject = __instance.allObjects[i];
                if (innerNetObject && (innerNetObject.OwnerId != -4 || __instance.AmModdedHost) && hashSet.Add(innerNetObject.gameObject))
                {
                    GameManager gameManager = innerNetObject as GameManager;
                    if (gameManager != null)
                    {
                        __instance.SendGameManager(clientId, gameManager);
                    }
                    else
                    {
                        if (innerNetObject is not NetworkedPlayerInfo)
                            __instance.WriteSpawnMessage(innerNetObject, innerNetObject.OwnerId, innerNetObject.SpawnFlags, messageWriter);
                    }
                }
            }

            messageWriter.EndMessage();
            __instance.SendOrDisconnect(messageWriter);
            messageWriter.Recycle();
        }

        DelaySpawnPlayerInfo(__instance, clientId);
        return false;
    }

    private static void DelaySpawnPlayerInfo(InnerNetClient __instance, int clientId)
    {
        var players = GameData.Instance.AllPlayers.ToArray();

        // We send 5 players at a time to prevent too large packets
        foreach (var batch in players.Chunk(5))
        {
            MessageWriter messageWriter = MessageWriter.Get(SendOption.Reliable);
            messageWriter.StartMessage(6);
            messageWriter.Write(__instance.GameId);
            messageWriter.WritePacked(clientId);

            batch.DoIf(p => p != null && p.ClientId != clientId && !p.Disconnected, p => __instance.WriteSpawnMessage(p, p.OwnerId, p.SpawnFlags, messageWriter));

            messageWriter.EndMessage();
            __instance.SendOrDisconnect(messageWriter);
            messageWriter.Recycle();
        }
    }
}

[HarmonyPatch(typeof(GameData), nameof(GameData.DirtyAllData))]
internal class DirtyAllDataPatch
{
    // Currently this function only occurs in CreatePlayer.
    // It's believed to lag the host, delay the PlayerControl spawn mesasge, blackout new clients
    // and send huge packets to all clients while there's completely no need to run this.
    // Temporarily disable it until Innersloth gets a better fix.
    public static bool Prefix() => false;
}