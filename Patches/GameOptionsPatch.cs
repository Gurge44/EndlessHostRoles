using AmongUs.GameOptions;
using HarmonyLib;

namespace EHR.Patches;

[HarmonyPatch(typeof(GameOptionsManager), nameof(GameOptionsManager.SwitchGameMode))]
internal static class SwitchGameModePatch
{
    private static bool Warned;

    public static bool Prefix(GameModes gameMode)
    {
        if (!Options.IsLoaded || (AmongUsClient.Instance != null && !AmongUsClient.Instance.AmHost && PlayerControl.LocalPlayer != null) || gameMode != GameModes.HideNSeek || Warned || !HudManager.Instance) return true;

        HudManager.Instance.ShowPopUp(Translator.GetString("HnSUnloadWarning"));
        return false;
    }

    public static void Postfix(GameModes gameMode)
    {
        if (!Options.IsLoaded || (!AmongUsClient.Instance.AmHost && PlayerControl.LocalPlayer != null) || gameMode != GameModes.HideNSeek) return;

        if (!Warned)
        {
            Warned = true;
            return;
        }

        if (ErrorText.Instance != null)
        {
            ErrorText.Instance.HnSFlag = true;
            ErrorText.Instance.AddError(ErrorCode.HnsUnload);
        }

        Zoom.SetZoomSize(reset: true);
        Harmony.UnpatchAll();
        Main.Instance.Unload();
    }
}