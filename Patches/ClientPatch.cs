using HarmonyLib;
using InnerNet;
using TMPro;
using TOHE.Modules;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE;

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.MakePublic))]
internal class MakePublicPatch
{
    public static bool Prefix(/*GameStartManager __instance*/)
    {
        // 定数設定による公開ルームブロック
        if (!Main.AllowPublicRoom)
        {
            var message = GetString("DisabledByProgram");
            Logger.Info(message, "MakePublicPatch");
            Logger.SendInGame(message);
            return false;
        }
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
internal class MMOnlineManagerStartPatch
{
    public static void Postfix()
    {
        if (!((ModUpdater.hasUpdate && ModUpdater.forceUpdate) || ModUpdater.isBroken)) return;
        var obj = GameObject.Find("FindGameButton");
        if (obj)
        {
            obj?.SetActive(false);
            var parentObj = obj.transform.parent.gameObject;
            var textObj = Object.Instantiate(obj.transform.FindChild("Text_TMP").GetComponent<TextMeshPro>());
            textObj.transform.position = new(1f, -0.3f, 0);
            textObj.name = "CanNotJoinPublic";
            var message = ModUpdater.isBroken ? $"<size=2>{Utils.ColorString(Color.red, GetString("ModBrokenMessage"))}</size>"
                : $"<size=2>{Utils.ColorString(Color.red, GetString("CanNotJoinPublicRoomNoLatest"))}</size>";
            _ = new LateTask(() => { textObj.text = message; }, 0.01f, "CanNotJoinPublic");
        }
    }
}
[HarmonyPatch(typeof(SplashManager), nameof(SplashManager.Update))]
internal class SplashLogoAnimatorPatch
{
    public static void Prefix(SplashManager __instance)
    {
        //if (DebugModeManager.AmDebugger)
        //{
        __instance.sceneChanger.AllowFinishLoadingScene();
        __instance.startedSceneLoad = true;
        //}
    }
}
[HarmonyPatch(typeof(EOSManager), nameof(EOSManager.IsAllowedOnline))]
internal class RunLoginPatch
{
    public static int ClickCount;
    public static void Prefix(ref bool canOnline)
    {
#if DEBUG
        switch (ClickCount)
        {
            case < 10:
                canOnline = true;
                break;
            case >= 10:
                ModUpdater.forceUpdate = false;
                break;
        }

#endif
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
    public static bool Prefix(/*InnerNetClient __instance,*/ int clientId, bool ban)
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        if (AmongUsClient.Instance.ClientId == clientId)
        {
            Logger.SendInGame(string.Format("Game Attempting to {0} Host, Blocked the attempt.", ban ? "Ban" : "Kick"));
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
        if (MainMenuManagerPatch.updateButton != null)
            MainMenuManagerPatch.updateButton.transform.localPosition = MainMenuManagerPatch.template.transform.localPosition + new Vector3(0.25f, 0.75f);
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendAllStreamedObjects))]
internal class InnerNetObjectSerializePatch
{
    public static void Prefix()
    {
        if (AmongUsClient.Instance.AmHost)
            GameOptionsSender.SendAllGameOptions();
    }
}