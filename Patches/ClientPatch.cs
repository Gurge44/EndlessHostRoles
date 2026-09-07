using System;
using AmongUs.Data;
using EHR.Modules;
using HarmonyLib;
using Hazel;
using InnerNet;
using TMPro;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

/*[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.MakePublic))]
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
            Logger.SendInGame(message, Color.red);
            return false;
        }

        return true;
    }
}*/

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.StartRpcImmediately))]
static class StartRpcImmediatelyPatch
{
    public static void Postfix(uint targetNetId, byte callId, SendOption option, int targetClientId = -1)
    {
        if (callId is 21 or 44 or 45 or 104) return;
        Logger.Info($"Starting RPC: {callId} ({RPC.GetRpcName(callId)}) as {Main.CachedAllPlayerControls().Find(x => x.NetId == targetNetId)?.GetRealName() ?? targetNetId.ToString()} with SendOption {option} to {Utils.GetClientById(targetClientId)?.Character?.GetRealName() ?? targetClientId.ToString()}", "StartRpcImmediately");
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

[HarmonyPatch(typeof(SplashManager), nameof(SplashManager.Start))]
internal static class SplashLogoAnimatorPatch
{
    public static SceneChanger SceneChanger;
    
    public static void Postfix(SplashManager __instance)
    {
        SceneChanger = __instance.sceneChanger;
        Main.Instance.StartCoroutine(Coroutine());
        return;

        System.Collections.IEnumerator Coroutine()
        {
            while (__instance && SceneChanger && SceneChanger.loadOp == null) yield return null;
                
            if (SceneChanger) SceneChanger.AllowFinishLoadingScene();
            if (__instance) __instance.startedSceneLoad = true;

            while (!ModManager.InstanceExists) yield return null;
            
            ModManager.Instance.ShowModStamp();
        }
    }
}

[HarmonyPatch(typeof(EOSManager), nameof(EOSManager.IsAllowedOnline))]
internal static class RunLoginPatch
{
    public const int ClickCount = 0;

    public static void Prefix(ref bool canOnline)
    {
        if (DebugModeManager.AmDebugger) canOnline = true;
        
        if (!Main.AckdConsentPopup.Value)
        {
            canOnline = true;
        }

        try { ModUpdater.ShowAvailableUpdate(); }
        catch (Exception error) { Logger.Error(error.ToString(), "ModUpdater.ShowAvailableUpdate"); }
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
            Logger.SendInGame($"Game Attempting to {(ban ? "Ban" : "Kick")} Host, Blocked the attempt.", Color.red);
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
        if (MainMenuManagerPatch.UpdateButton)
            MainMenuManagerPatch.UpdateButton.transform.localPosition = MainMenuManagerPatch.Template.transform.localPosition + new Vector3(0.25f, 0.75f);
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendAllStreamedObjects))]
internal static class InnerNetObjectSerializePatch
{
    public static void Prefix()
    {
        if (!AmongUsClient.Instance.AmHost || GameOptionsSender.ActiveCoroutine != null) return;
        GameOptionsSender.ActiveCoroutine = Main.Instance.StartCoroutine(GameOptionsSender.SendDirtyGameOptionsContinuously());
    }
}

// https://github.com/Rabek009/MoreGamemodes/blob/master/Patches/ClientPatch.cs
[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CheckOnlinePermissions))]
static class CheckOnlinePermissionsPatch
{
    public static void Prefix()
    {
        DataManager.Player.Ban.banPoints = 0f;
    }
}