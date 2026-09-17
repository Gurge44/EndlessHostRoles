using System;
using System.Collections;
using System.IO;
using System.Net.Http;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using TMPro;
using Twitch;
using UnityEngine;
using UnityEngine.Networking;
using static EHR.Translator;

namespace EHR;

[HarmonyPatch]
public static class ModUpdater
{
    const string SavePath = "BepInEx/plugins/EHR.dll.temp";
    private const string URLGithub = "https://api.github.com/repos/Gurge44/EndlessHostRoles";
    public const bool ForceUpdate = false;
    public static bool HasUpdate;
    private static bool FirstNotify = true;
    private static bool HasOutdate;
    public static bool IsBroken;
    private static Version LatestVersion;
    private static string LatestTitleModName;
    private static string LatestTitle;
    public static string DownloadUrl;
    private static GenericPopup InfoPopup;
    private static GenericPopup InfoPopupV2;
    public static readonly HttpClient HttpClient = new();

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
    [HarmonyPrefix]
    [HarmonyPriority(2)]
    public static void Start_Prefix()
    {
        if (!OperatingSystem.IsAndroid())
        {
            // Version checks are not handled on Android
            NewVersionCheck();
            DeleteOldFiles();
            
            Main.Instance.StartCoroutine(CheckReleaseFromGithub(Main.BetaBuildUrl.Value != ""));
        }
        
        InfoPopup = Object.Instantiate(TwitchManager.Instance.TwitchPopup);
        InfoPopup.name = "InfoPopup";
        InfoPopup.TextAreaTMP.GetComponent<RectTransform>().sizeDelta = new(2.5f, 2f);

        InfoPopupV2 = Object.Instantiate(TwitchManager.Instance.TwitchPopup);
        InfoPopupV2.name = "InfoPopupV2";
    }

    public static void ShowAvailableUpdate()
    {
        if (FirstNotify && HasUpdate)
        {
            FirstNotify = false;
            
            if (!string.IsNullOrWhiteSpace(LatestTitleModName))
                ShowPopupWithTwoButtons(string.Format(GetString("NewUpdateAvailable"), LatestTitleModName), GetString("updateButton"), onClickOnFirstButton: () => StartUpdate(DownloadUrl));
        }
    }

    private static IEnumerator CheckReleaseFromGithub(bool beta = false)
    {
        Logger.Msg("Checking GitHub Release", "CheckRelease");

        const string url = URLGithub + "/releases/latest";

        using UnityWebRequest request = UnityWebRequest.Get(url);

        request.SetRequestHeader("User-Agent", "EHR Updater");
        request.downloadHandler = new DownloadHandlerBuffer();

        yield return request.SendWebRequest();

        try
        {
            if (request.result != UnityWebRequest.Result.Success)
            {
                Logger.Error($"GitHub request failed: {request.responseCode} {request.error}", "CheckRelease");
                IsBroken = true;
                yield break;
            }

            JObject data = JObject.Parse(request.downloadHandler.text);

            LatestTitleModName = data["name"]?.ToString();

            if (beta)
            {
                LatestTitle = data["name"]?.ToString();

                // Beta builds still use the same EHR.dll release asset.
                DownloadUrl = FindDllAssetUrl(data);

                HasUpdate = LatestTitle != ThisAssembly.Git.Commit;
            }
            else
            {
                string versionString = data["tag_name"]?
                    .ToString()
                    .TrimStart('v');

                LatestVersion = new Version(versionString!);
                LatestTitle = $"Ver. {LatestVersion}";
                DownloadUrl = FindDllAssetUrl(data);
                HasUpdate = LatestVersion.CompareTo(Main.Version) > 0;
                HasOutdate = LatestVersion.CompareTo(Main.Version) < 0;
            }

            Logger.Info("hasupdate: " + HasUpdate, "GitHub");
            Logger.Info("hasoutdate: " + HasOutdate, "GitHub");
            Logger.Info("forceupdate: " + ForceUpdate, "GitHub");
            Logger.Info("downloadUrl: " + DownloadUrl, "GitHub");
            Logger.Info("latestVersion: " + LatestVersion, "GitHub");
            Logger.Info("latestTitle: " + LatestTitle, "GitHub");

            if (string.IsNullOrWhiteSpace(DownloadUrl))
            {
                Logger.Error("No EHR.dll download URL found in release assets", "CheckRelease");
                IsBroken = true;
                yield break;
            }

            IsBroken = false;

            Logger.Msg("GitHub release check completed", "CheckRelease");
        }
        catch (Exception ex)
        {
            IsBroken = true;

            Logger.Error(
                $"Error while checking release from GitHub:\n{ex}",
                "CheckRelease",
                false
            );
        }
    }
    
    private static string FindDllAssetUrl(JObject release)
    {
        if (release["assets"] is not JArray assets)
            return null;

        for (int i = 0; i < assets.Count; i++)
        {
            JObject asset = assets[i] as JObject;

            if (asset?["name"]?.ToString() == "EHR.dll")
                return asset["browser_download_url"]?.ToString();
        }

        return null;
    }

    public static void StartUpdate(string url)
    {
        if (OperatingSystem.IsAndroid()) return;
        ShowPopup(GetString("updatePleaseWait"), StringNames.Cancel, true, false);
        Main.Instance.StartCoroutine(DownloadDLL(url));
    }

    private static bool NewVersionCheck()
    {
        try
        {
            if (Directory.Exists($"{Main.DataPath}/TOH_DATA") && File.Exists($"{Main.DataPath}/EHR_DATA/BanWords.txt"))
            {
                DirectoryInfo di = new($"{Main.DataPath}/TOH_DATA");
                di.Delete(true);
                Logger.Warn("Directory deleted: TOH_DATA", "NewVersionCheck");
            }
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "NewVersionCheck");
            return false;
        }

        return true;
    }

    private static void DeleteOldFiles()
    {
        string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        const string searchPattern = "EHR.dll*";

        if (path != null)
        {
            string[] files = Directory.GetFiles(path, searchPattern);

            try
            {
                foreach (string filePath in files)
                {
                    string fileName = Path.GetFileName(filePath);

                    if (fileName.EndsWith(".bak") || fileName.EndsWith(".temp"))
                    {
                        Logger.Info($"{filePath} will be deleted", "DeleteOldFiles");
                        File.Delete(filePath);
                    }
                }
            }
            catch (Exception e) { Logger.Error($"Failed to clear update residue\n{e}", "DeleteOldFiles"); }
        }
    }

    private static IEnumerator DownloadDLL(string url)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                Logger.Error("Cannot download update: URL is empty", "DownloadDLL");
                UpdateFailed();
                yield break;
            }

            if (File.Exists(SavePath))
                File.Delete(SavePath);
        }
        catch (Exception e) { Utils.ThrowException(e); }

        using UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("User-Agent", "EHR Updater");
        request.downloadHandler = new DownloadHandlerFile(SavePath);
        yield return request.SendWebRequest();

        try
        {
            if (request.result != UnityWebRequest.Result.Success)
            {
                Logger.Error($"File retrieval failed with status code {request.responseCode}: {request.error}", "DownloadDLL");
                UpdateFailed();
                yield break;
            }

            if (!File.Exists(SavePath))
            {
                Logger.Error("Downloaded EHR.dll was not found", "DownloadDLL");
                UpdateFailed();
                yield break;
            }

            string fileName = Assembly.GetExecutingAssembly().Location;

            File.Move(fileName, fileName + ".bak");
            File.Move(SavePath, fileName);

            Logger.Msg($"Successfully downloaded update to {fileName}", "DownloadDLL");
            ShowPopup(GetString("updateRestart"), StringNames.Close, true);
        }
        catch (Exception ex)
        {
            Logger.Error($"Update failed\n{ex}", "DownloadDLL", false);
            UpdateFailed();
        }
    }

    public static void UpdateFailed()
    {
        try
        {
            if (File.Exists(SavePath))
                File.Delete(SavePath);
        }
        catch { }

        ShowPopup(GetString("updateManually"), StringNames.Close, true);
    }

    public static void ShowPopup(string message, StringNames buttonText, bool showButton = false, bool buttonIsExit = true)
    {
        if (!InfoPopup) return;

        InfoPopup.Show(message);
        Transform button = InfoPopup.transform.Find("ExitGame");

        if (button)
        {
            button.gameObject.SetActive(showButton);
            var textTranslatorTMP = button.GetChild(0).GetComponent<TextTranslatorTMP>();
            textTranslatorTMP.TargetText = buttonText;
            textTranslatorTMP.ResetText();
            var passiveButton = button.GetComponent<PassiveButton>();
            passiveButton.OnClick = new();

            if (buttonIsExit)
                passiveButton.OnClick.AddListener(SplashLogoAnimatorPatch.SceneChanger.ExitGame);
            else
                passiveButton.OnClick.AddListener(() => InfoPopup.Close());
        }
    }

    public static void ShowPopupWithTwoButtons(string message, string firstButtonText, string secondButtonText = "", Action onClickOnFirstButton = null, Action onClickOnSecondButton = null)
    {
        if (InfoPopupV2)
        {
            var templateExitGame = InfoPopupV2.transform.Find("ExitGame");
            if (!templateExitGame) return;

            var background = InfoPopupV2.transform.Find("Background");
            if (!background) return;
            background.localScale *= 2f;

            InfoPopupV2.Show(message);
            templateExitGame.gameObject.SetActive(false);
            var firstButton = Object.Instantiate(templateExitGame, InfoPopupV2.transform);
            var secondButton = Object.Instantiate(templateExitGame, InfoPopupV2.transform);
            
            if (firstButton)
            {
                firstButton.gameObject.SetActive(true);
                firstButton.name = "FirstButton";
                var firstButtonTransform = firstButton.transform;
                firstButton.transform.localPosition = new Vector3(firstButtonTransform.localPosition.x - 1f, firstButtonTransform.localPosition.y - 0.7f, firstButtonTransform.localPosition.z);
                firstButton.transform.localScale *= 1.2f;
                var firstButtonGetChild = firstButton.GetChild(0);
                var textTranslatorTMP = firstButtonGetChild.GetComponent<TextTranslatorTMP>();
                textTranslatorTMP.TargetText = StringNames.Cancel;
                textTranslatorTMP.ResetText();
                textTranslatorTMP.DestroyTranslator();
                firstButtonGetChild.GetComponent<TextMeshPro>().text = firstButtonText;
                firstButtonGetChild.GetComponent<TMP_Text>().text = firstButtonText;
                var passiveButton = firstButton.GetComponent<PassiveButton>();
                passiveButton.OnClick = new();
                if (onClickOnFirstButton != null)
                    passiveButton.OnClick.AddListener(() => { onClickOnFirstButton(); InfoPopupV2.Close();});
                else passiveButton.OnClick.AddListener(() => InfoPopupV2.Close());
            }
            
            if (secondButton)
            {
                secondButton.gameObject.SetActive(true);
                secondButton.name = "SecondButton";
                var secondButtonTransform = secondButton.transform;
                secondButton.transform.localPosition = new Vector3(secondButtonTransform.localPosition.x + 1f, secondButtonTransform.localPosition.y - 0.7f, secondButtonTransform.localPosition.z);
                secondButton.transform.localScale *= 1.2f;
                var secondButtonGetChild = secondButton.GetChild(0);
                var textTranslatorTMP = secondButtonGetChild.GetComponent<TextTranslatorTMP>();
                textTranslatorTMP.TargetText = StringNames.Cancel;
                textTranslatorTMP.ResetText();
                if (!string.IsNullOrWhiteSpace(secondButtonText))
                {
                    textTranslatorTMP.DestroyTranslator();
                    secondButtonGetChild.GetComponent<TextMeshPro>().text = secondButtonText;
                    secondButtonGetChild.GetComponent<TMP_Text>().text = secondButtonText;
                }

                var passiveButton = secondButton.GetComponent<PassiveButton>();
                passiveButton.OnClick = new();
                if (onClickOnSecondButton != null)
                    passiveButton.OnClick.AddListener(() => { onClickOnSecondButton(); InfoPopupV2.Close(); });
                else passiveButton.OnClick.AddListener(() => InfoPopupV2.Close());
            }
        }
    }
}
