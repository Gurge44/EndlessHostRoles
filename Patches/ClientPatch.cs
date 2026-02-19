using System;
using System.Reflection;
using AmongUs.Data;
using EHR.Modules;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
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
    public static void Postfix(uint targetNetId, byte callId, Hazel.SendOption option, int targetClientId = -1)
    {
        Logger.Info($"Starting RPC: {callId} ({RPC.GetRpcName(callId)}) as {targetNetId} with SendOption {option} to {targetClientId}", "StartRpcImmediately");
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
        if (GameStates.CurrentServerType is GameStates.ServerType.Vanilla or GameStates.ServerType.Local) return true;

        __result = false;
        return false;
    }

    // If you don't patch this, you still need to wait for 5 s.
    // I have no idea why this is happening
    [HarmonyPatch]
    public static class EnableUdpPatch
    {
        public static MethodBase TargetMethod()
        {
            return Utils.GetStateMachineMoveNext<AmongUsClient>(nameof(AmongUsClient.CoJoinOnlinePublicGame))!;
        }

        public static void Prefix(Il2CppObjectBase __instance)
        {
            var stateMachine = new StateMachineWrapper<AmongUsClient>(__instance);

            // Skip to state 1 which just calls CoJoinOnlineGameDirect
            if (stateMachine.State == 0 && !ServerManager.Instance.IsHttp)
            {
                stateMachine.State = 1;
                var lambdaType = stateMachine.GetParameter<Il2CppObjectBase>("__8__1").GetType();
                var newDisplayClass = Activator.CreateInstance(lambdaType);
                if (newDisplayClass == null)
                {
                    throw new InvalidOperationException($"Could not create display class of type '{lambdaType}'.");
                }

                var displayClass = new CompilerGeneratedObjectWrapper(newDisplayClass);
                displayClass.SetField("matchmakerToken", string.Empty);

                stateMachine.SetParameter("__8__1", newDisplayClass);
            }
        }
    }
}