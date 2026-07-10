using System;
using AmongUs.Data;
using HarmonyLib;
using TMPro;
using UnityEngine;
using static EHR.Translator;

namespace EHR.Modules;

public static class ModConsentPopUp
{
    public static bool IsShowingModPolicy { get; private set; }
    private static int OriginalVersion { get; set; }

    public static void SetupCustomPolicy(PrivacyPolicyScreen screen)
    {
        if (screen == null) return;

        IsShowingModPolicy = true;
        OriginalVersion = DataManager.Player.Onboarding.LastAcceptedPrivacyPolicyVersion;

        DestroyTranslator(screen.DefaultHyperlinkText);
        DestroyTranslator(screen.PlayStationHyperlinkText);

        screen.DefaultHyperlinkText.Text = GetString("EHRConsentPopUp");
        screen.PlayStationHyperlinkText.Text = screen.DefaultHyperlinkText.Text;

        try
        {
            screen.OnTextUpdated();
        }
        catch (Exception ex)
        {
            Logger.Warn($"OnTextUpdated() failed: {ex.StackTrace}", "ModPrivacyPolicy");
        }

        DataManager.Player.Onboarding.LastAcceptedPrivacyPolicyVersion =
            Math.Max(0, OriginalVersion) == 0 ? -1 : OriginalVersion - 1;

        ConfigureButtons(screen);

        Logger.Info("Custom mod consent popup configured", "ModPrivacyPolicy");
    }

    private static void DestroyTranslator(OpenHyperlinks hyperlink)
    {
        if (hyperlink == null) return;
        var translator = hyperlink.GetComponent<TextTranslatorTMP>();
        if (translator != null)
        {
            UnityEngine.Object.Destroy(translator);
        }
    }

    private static void ConfigureButtons(PrivacyPolicyScreen screen)
    {
        var acceptGO = screen.AcceptButton?.gameObject;
        if (acceptGO == null) return;

        var acceptPassive = acceptGO.GetComponentInChildren<PassiveButton>();
        if (acceptPassive == null) return;

        acceptPassive.OnClick.RemoveAllListeners();
        acceptPassive.OnClick.AddListener((Action)(() =>
        {
            Main.AckdConsentPopup.Value = true;
            screen.Close();
        }));

        var acceptTMP = acceptGO.GetComponentInChildren<TMP_Text>();
        if (acceptTMP != null)
        {
            acceptTMP.DestroyTranslator();
            acceptTMP.text = GetString("Accept");
        }

        var disagreeGO = UnityEngine.Object.Instantiate(acceptGO, acceptGO.transform.parent);
        disagreeGO.name = "DisagreeButton";

        float z = acceptGO.transform.localPosition.z;
        acceptGO.transform.localPosition = new Vector3(1.35f, -1.74f, z);
        disagreeGO.transform.localPosition = new Vector3(-1.25f, -1.74f, z);

        var disagreePassive = disagreeGO.GetComponentInChildren<PassiveButton>();
        if (disagreePassive != null)
        {
            disagreePassive.OnClick.RemoveAllListeners();
            disagreePassive.OnClick.AddListener((Action)(() =>
                SplashLogoAnimatorPatch.SceneChanger.ExitGame()));
        }

        var disagreeTMP = disagreeGO.GetComponentInChildren<TMP_Text>();
        if (disagreeTMP != null)
        {
            disagreeTMP.DestroyTranslator();
            disagreeTMP.text = GetString("Reject");
        }

        if (screen.ManageDataButton != null)
            screen.ManageDataButton.gameObject.SetActive(false);
    }

    public static void OnPolicyAccepted()
    {
        if (!IsShowingModPolicy) return;
        IsShowingModPolicy = false;

        DataManager.Player.Onboarding.LastAcceptedPrivacyPolicyVersion = OriginalVersion;
        DataManager.Player.Save();

        Logger.Info("Mod consent popup accepted by user", "ModPrivacyPolicy");
    }

}

[HarmonyPatch(typeof(PrivacyPolicyScreen), nameof(PrivacyPolicyScreen.Show))]
internal static class PrivacyPolicyScreenShowPatch
{
    public static void Prefix(PrivacyPolicyScreen __instance)
    {
        if (!Main.AckdConsentPopup.Value)
            ModConsentPopUp.SetupCustomPolicy(__instance);
    }
}

[HarmonyPatch(typeof(PrivacyPolicyScreen), nameof(PrivacyPolicyScreen.Close))]
internal static class PrivacyPolicyScreenClosePatch
{
    public static void Postfix()
    {
        ModConsentPopUp.OnPolicyAccepted();
    }
}
