using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using TMPro;
using Twitch;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

[HarmonyPatch]
public static class ModUpdater
{
    private const string URLGithub = "https://api.github.com/repos/Gurge44/EndlessHostRoles";
    public const bool ForceUpdate = false;
    public static bool HasUpdate;
    private static bool FirstNotify = true;
    private static bool HasOutdate;
    public static bool IsBroken;
    private static bool IsChecked;
    private static Version LatestVersion;
    private static string LatestTitleModName = null;
    private static string LatestTitle;
    public static string DownloadUrl;
    private static GenericPopup InfoPopup;
    private static GenericPopup InfoPopupV2;

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
    [HarmonyPrefix]
    [HarmonyPriority(2)]
    public static void Start_Prefix()
    {
#if !ANDROID
        NewVersionCheck();
        DeleteOldFiles();
#endif
        InfoPopup = Object.Instantiate(TwitchManager.Instance.TwitchPopup);
        InfoPopup.name = "InfoPopup";
        InfoPopup.TextAreaTMP.GetComponent<RectTransform>().sizeDelta = new(2.5f, 2f);

        InfoPopupV2 = Object.Instantiate(TwitchManager.Instance.TwitchPopup);
        InfoPopupV2.name = "InfoPopupV2";

#if !ANDROID
        if (!IsChecked)
        {
            bool done = CheckReleaseFromGithub(Main.BetaBuildUrl.Value != "").GetAwaiter().GetResult();
            Logger.Msg("done: " + done, "CheckRelease");
            Logger.Info("hasupdate: " + HasUpdate, "CheckRelease");
            Logger.Info("forceupdate: " + ForceUpdate, "CheckRelease");
            Logger.Info("downloadUrl: " + DownloadUrl, "CheckRelease");
            Logger.Info("latestVersionl: " + LatestVersion, "CheckRelease");
        }
#endif
    }

    public static void ShowAvailableUpdate()
    {
        if (FirstNotify && HasUpdate)
        {
            FirstNotify = false;
            
            if (!string.IsNullOrWhiteSpace(LatestTitleModName))
                ShowPopupWithTwoButtons(string.Format(GetString("NewUpdateAvailable"), LatestTitleModName), GetString("updateButton"), onClickOnFirstButton: () => StartUpdate(DownloadUrl, true));
        }
    }

    public static string Get(string url)
    {
        string result;
        HttpClient req = new();
        HttpResponseMessage res = req.GetAsync(url).Result;
        Stream stream = res.Content.ReadAsStreamAsync().Result;

        try
        {
            using StreamReader reader = new(stream);
            result = reader.ReadToEnd();
        }
        finally { stream.Close(); }

        return result;
    }

    public static async Task<bool> CheckReleaseFromGithub(bool beta = false)
    {
        Logger.Msg("Checking GitHub Release", "CheckRelease");
        const string url = URLGithub + "/releases/latest";

        try
        {
            string result;

            using (HttpClient client = new())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "EHR Updater");
                using HttpResponseMessage response = await client.GetAsync(new Uri(url), HttpCompletionOption.ResponseContentRead);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Error($"Response Status Code: {response.StatusCode}", "CheckRelease");
                    return false;
                }

                result = await response.Content.ReadAsStringAsync();
            }

            JObject data = JObject.Parse(result);

            LatestTitleModName = data["name"].ToString();

            if (beta)
            {
                LatestTitle = data["name"].ToString();
                DownloadUrl = data["url"].ToString();
                HasUpdate = LatestTitle != ThisAssembly.Git.Commit;
            }
            else
            {
                LatestVersion = new(data["tag_name"]?.ToString().TrimStart('v') ?? string.Empty);
                LatestTitle = $"Ver. {LatestVersion}";
                var assets = data["assets"].CastFast<JArray>();

                for (var i = 0; i < assets.Count; i++)
                {
                    if (assets[i]["name"].ToString() == $"EHR.v{LatestVersion}_Steam.zip")
                    {
                        DownloadUrl = assets[i]["browser_download_url"].ToString();
                        break;
                    }
                }

                HasUpdate = LatestVersion.CompareTo(Main.Version) > 0;
                HasOutdate = LatestVersion.CompareTo(Main.Version) < 0;
            }

            Logger.Info("hasupdate: " + HasUpdate, "GitHub");
            Logger.Info("hasoutdate: " + HasOutdate, "GitHub");
            Logger.Info("forceupdate: " + ForceUpdate, "GitHub");
            Logger.Info("downloadUrl: " + DownloadUrl, "GitHub");
            Logger.Info("latestVersionl: " + LatestVersion, "GitHub");
            Logger.Info("latestTitle: " + LatestTitle, "GitHub");

            if (string.IsNullOrWhiteSpace(DownloadUrl))
            {
                Logger.Error("No Download URL", "CheckRelease");
                return false;
            }

            IsChecked = true;
            IsBroken = false;
        }
        catch (Exception ex)
        {
            IsBroken = true;
            Logger.Error($"Error while checking release from GitHub:\n{ex}", "CheckRelease", false);
            return false;
        }

        return true;
    }

    public static void StartUpdate(string url, bool github)
    {
        ShowPopup(GetString("updatePleaseWait"), StringNames.Cancel, true, false);
        _ = !github ? DownloadDLL(url) : DownloadDLLGithub(url);
    }

    public static bool NewVersionCheck()
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

    public static void DeleteOldFiles()
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
                    if (Path.GetFileName(filePath).EndsWith(".bak") || Path.GetFileName(filePath).EndsWith(".temp"))
                    {
                        Logger.Info($"{filePath} will be deleted", "DeleteOldFiles");
                        File.Delete(filePath);
                    }
                }
            }
            catch (Exception e) { Logger.Error($"Failed to clear update residue\n{e}", "DeleteOldFiles"); }
        }
    }

    public static async Task<bool> DownloadDLL(string url)
    {
        try
        {
            const string savePath = "BepInEx/plugins/EHR.dll.temp";

            // Delete the temporary file if it exists
            if (File.Exists(savePath)) File.Delete(savePath);

            HttpResponseMessage response;

            using (HttpClient client = new()) response = await client.GetAsync(url);

            if (response is not { IsSuccessStatusCode: true }) throw new($"File retrieval failed with status code: {response.StatusCode}");

            await using (Stream stream = await response.Content.ReadAsStreamAsync())
            await using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            {
                var buffer = new byte[1024];
                int length;

                while ((length = await stream.ReadAsync(buffer)) != 0) await fileStream.WriteAsync(buffer.AsMemory(0, length));
            }

            string fileName = Assembly.GetExecutingAssembly().Location;
            File.Move(fileName, fileName + ".bak");
            File.Move(savePath, fileName);
            ShowPopup(GetString("updateRestart"), StringNames.Close, true);
        }
        catch (Exception ex)
        {
            Logger.Error($"Update failed\n{ex}", "DownloadDLL", false);
            ShowPopup(GetString("updateManually"), StringNames.Close, true);
            return false;
        }

        return true;
    }

    public static async Task<bool> DownloadDLLGithub(string url)
    {
        try
        {
            const string savePath = "BepInEx/plugins/EHR.dll.temp";

            // Delete the temporary file if it exists
            if (File.Exists(savePath)) File.Delete(savePath);

            HttpResponseMessage response;

            using (HttpClient client = new()) response = await client.GetAsync(url);

            if (response is not { IsSuccessStatusCode: true }) throw new($"File retrieval failed with status code: {response.StatusCode}");

            await using (Stream stream = await response.Content.ReadAsStreamAsync())
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                // Specify the relative path within the ZIP archive where "EHR.dll" is located
                const string entryPath = "BepInEx/plugins/EHR.dll";
                ZipArchiveEntry entry = archive.GetEntry(entryPath) ?? throw new($"'{entryPath}' not found in the ZIP archive");

                // Extract "EHR.dll" to the temporary file
                await using Stream entryStream = entry.Open();
                await using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                await entryStream.CopyToAsync(fileStream);
            }

            string fileName = Assembly.GetExecutingAssembly().Location;
            File.Move(fileName, fileName + ".bak");
            File.Move(savePath, fileName);
            ShowPopup(GetString("updateRestart"), StringNames.Close, true);
        }
        catch (Exception ex)
        {
            Logger.Error($"Update failed\n{ex}", "DownloadDLL", false);
            ShowPopup(GetString("updateManually"), StringNames.Close, true);
            return false;
        }

        return true;
    }

    public static void ShowPopup(string message, StringNames buttonText, bool showButton = false, bool buttonIsExit = true)
    {
        if (InfoPopup == null) return;

        InfoPopup.Show(message);
        Transform button = InfoPopup.transform.FindChild("ExitGame");

        if (button != null)
        {
            button.gameObject.SetActive(showButton);
            button.GetChild(0).GetComponent<TextTranslatorTMP>().TargetText = buttonText;
            button.GetChild(0).GetComponent<TextTranslatorTMP>().ResetText();
            button.GetComponent<PassiveButton>().OnClick = new();

            if (buttonIsExit)
                button.GetComponent<PassiveButton>().OnClick.AddListener((Action)Application.Quit);
            else
                button.GetComponent<PassiveButton>().OnClick.AddListener((Action)(() => InfoPopup.Close()));
        }
    }

    private static void ShowPopupWithTwoButtons(string message, string firstButtonText, string secondButtonText = "", Action onClickOnFirstButton = null, Action onClickOnSecondButton = null)
    {
        if (InfoPopupV2 != null)
        {
            var templateExitGame = InfoPopupV2.transform.FindChild("ExitGame");
            if (templateExitGame == null) return;

            var background = InfoPopupV2.transform.FindChild("Background");
            if (background == null) return;
            background.localScale *= 2f;

            InfoPopupV2.Show(message);
            templateExitGame.gameObject.SetActive(false);
            var firstButton = Object.Instantiate(templateExitGame, InfoPopupV2.transform);
            var secondButton = Object.Instantiate(templateExitGame, InfoPopupV2.transform);
            if (firstButton != null)
            {
                firstButton.gameObject.SetActive(true);
                firstButton.name = "FirstButton";
                var firstButtonTransform = firstButton.transform;
                firstButton.transform.localPosition = new Vector3(firstButtonTransform.localPosition.x - 1f, firstButtonTransform.localPosition.y - 0.7f, firstButtonTransform.localPosition.z);
                firstButton.transform.localScale *= 1.2f;
                var firstButtonGetChild = firstButton.GetChild(0);
                firstButtonGetChild.GetComponent<TextTranslatorTMP>().TargetText = StringNames.Cancel;
                firstButtonGetChild.GetComponent<TextTranslatorTMP>().ResetText();
                firstButtonGetChild.GetComponent<TextTranslatorTMP>().DestroyTranslator();
                firstButtonGetChild.GetComponent<TextMeshPro>().text = firstButtonText;
                firstButtonGetChild.GetComponent<TMP_Text>().text = firstButtonText;
                firstButton.GetComponent<PassiveButton>().OnClick = new();
                if (onClickOnFirstButton != null)
                    firstButton.GetComponent<PassiveButton>().OnClick.AddListener((UnityEngine.Events.UnityAction)(() => { onClickOnFirstButton(); InfoPopupV2.Close();}));
                else firstButton.GetComponent<PassiveButton>().OnClick.AddListener((UnityEngine.Events.UnityAction)(() => InfoPopupV2.Close()));
            }
            if (secondButton != null)
            {
                secondButton.gameObject.SetActive(true);
                secondButton.name = "SecondButton";
                var secondButtonTransform = secondButton.transform;
                secondButton.transform.localPosition = new Vector3(secondButtonTransform.localPosition.x + 1f, secondButtonTransform.localPosition.y - 0.7f, secondButtonTransform.localPosition.z);
                secondButton.transform.localScale *= 1.2f;
                var secondButtonGetChild = secondButton.GetChild(0);
                secondButtonGetChild.GetComponent<TextTranslatorTMP>().TargetText = StringNames.Cancel;
                secondButtonGetChild.GetComponent<TextTranslatorTMP>().ResetText();
                if (!string.IsNullOrWhiteSpace(secondButtonText))
                {
                    secondButtonGetChild.GetComponent<TextTranslatorTMP>().DestroyTranslator();
                    secondButtonGetChild.GetComponent<TextMeshPro>().text = secondButtonText;
                    secondButtonGetChild.GetComponent<TMP_Text>().text = secondButtonText;
                }
                secondButton.GetComponent<PassiveButton>().OnClick = new();
                if (onClickOnSecondButton != null)
                    secondButton.GetComponent<PassiveButton>().OnClick.AddListener((UnityEngine.Events.UnityAction)(() => { onClickOnSecondButton(); InfoPopupV2.Close(); }));
                else secondButton.GetComponent<PassiveButton>().OnClick.AddListener((UnityEngine.Events.UnityAction)(() => InfoPopupV2.Close()));
            }
        }
    }
}
