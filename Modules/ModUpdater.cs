using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Twitch;
using UnityEngine;
using static EHR.Translator;
using Object = UnityEngine.Object;

namespace EHR;

[HarmonyPatch]
public class ModUpdater
{
    private const string URLGithub = "https://api.github.com/repos/Gurge44/EndlessHostRoles";
    public static bool hasUpdate;
    public static bool hasOutdate;
    public static bool forceUpdate = false;
    public static bool isBroken;
    public static bool isChecked;
    public static Version latestVersion;
    public static string latestTitle;
    public static string downloadUrl;
    public static string notice = null;
    public static GenericPopup InfoPopup;

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start)), HarmonyPrefix]
    [HarmonyPriority(2)]
    public static void Start_Prefix()
    {
        NewVersionCheck();
        DeleteOldFiles();
        InfoPopup = Object.Instantiate(TwitchManager.Instance.TwitchPopup);
        InfoPopup.name = "InfoPopup";
        InfoPopup.TextAreaTMP.GetComponent<RectTransform>().sizeDelta = new(2.5f, 2f);
        if (!isChecked)
        {
            bool done = CheckReleaseFromGithub(Main.BetaBuildUrl.Value != "").GetAwaiter().GetResult();
            Logger.Warn("done: " + done, "CheckRelease");
            Logger.Info("hasupdate: " + hasUpdate, "CheckRelease");
            Logger.Info("forceupdate: " + forceUpdate, "CheckRelease");
            Logger.Info("downloadUrl: " + downloadUrl, "CheckRelease");
            Logger.Info("latestVersionl: " + latestVersion, "CheckRelease");
        }
    }

    public static string Get(string url)
    {
        string result;
        HttpClient req = new();
        var res = req.GetAsync(url).Result;
        Stream stream = res.Content.ReadAsStreamAsync().Result;
        try
        {
            using StreamReader reader = new(stream);
            result = reader.ReadToEnd();
        }
        finally
        {
            stream.Close();
        }

        return result;
    }

    public static async Task<bool> CheckReleaseFromGithub(bool beta = false)
    {
        Logger.Warn("Checking GitHub Release", "CheckRelease");
        const string url = URLGithub + "/releases/latest";
        try
        {
            string result;
            using (HttpClient client = new())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "EHR Updater");
                using var response = await client.GetAsync(new Uri(url), HttpCompletionOption.ResponseContentRead);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Error($"Response Status Code: {response.StatusCode}", "CheckRelease");
                    return false;
                }

                result = await response.Content.ReadAsStringAsync();
            }

            JObject data = JObject.Parse(result);
            if (beta)
            {
                latestTitle = data["name"].ToString();
                downloadUrl = data["url"].ToString();
                hasUpdate = latestTitle != ThisAssembly.Git.Commit;
            }
            else
            {
                latestVersion = new(data["tag_name"]?.ToString().TrimStart('v') ?? string.Empty);
                latestTitle = $"Ver. {latestVersion}";
                JArray assets = data["assets"].Cast<JArray>();
                for (int i = 0; i < assets.Count; i++)
                {
                    if (assets[i]["name"].ToString() == $"EHR.v{latestVersion}.zip")
                    {
                        downloadUrl = assets[i]["browser_download_url"].ToString();
                        break;
                    }
                }

                hasUpdate = latestVersion.CompareTo(Main.Version) > 0;
                hasOutdate = latestVersion.CompareTo(Main.Version) < 0;
            }

            Logger.Info("hasupdate: " + hasUpdate, "GitHub");
            Logger.Info("hasoutdate: " + hasOutdate, "GitHub");
            Logger.Info("forceupdate: " + forceUpdate, "GitHub");
            Logger.Info("downloadUrl: " + downloadUrl, "GitHub");
            Logger.Info("latestVersionl: " + latestVersion, "GitHub");
            Logger.Info("latestTitle: " + latestTitle, "GitHub");

            if (string.IsNullOrEmpty(downloadUrl))
            {
                Logger.Error("No Download URL", "CheckRelease");
                return false;
            }

            isChecked = true;
            isBroken = false;
        }
        catch (Exception ex)
        {
            isBroken = true;
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
            if (Directory.Exists("TOH_DATA") && File.Exists("./EHR_DATA/BanWords.txt"))
            {
                DirectoryInfo di = new("TOH_DATA");
                di.Delete(true);
                Logger.Warn("Directory deleted：TOH_DATA", "NewVersionCheck");
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
            catch (Exception e)
            {
                Logger.Error($"Failed to clear update residue\n{e}", "DeleteOldFiles");
            }
        }
    }

    public static async Task<bool> DownloadDLL(string url)
    {
        try
        {
            const string savePath = "BepInEx/plugins/EHR.dll.temp";

            // Delete the temporary file if it exists
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }

            HttpResponseMessage response;

            using (HttpClient client = new())
            {
                response = await client.GetAsync(url);
            }

            if (response is not { IsSuccessStatusCode: true })
            {
                throw new($"File retrieval failed with status code: {response.StatusCode}");
            }

            await using (var stream = await response.Content.ReadAsStreamAsync())
            await using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
            {
                byte[] buffer = new byte[1024];
                int length;

                while ((length = await stream.ReadAsync(buffer)) != 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, length));
                }
            }

            var fileName = Assembly.GetExecutingAssembly().Location;
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
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }

            HttpResponseMessage response;

            using (HttpClient client = new())
            {
                response = await client.GetAsync(url);
            }

            if (response is not { IsSuccessStatusCode: true })
            {
                throw new($"File retrieval failed with status code: {response.StatusCode}");
            }

            await using (var stream = await response.Content.ReadAsStreamAsync())
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                // Specify the relative path within the ZIP archive where "EHR.dll" is located
                const string entryPath = "BepInEx/plugins/EHR.dll";
                var entry = archive.GetEntry(entryPath) ?? throw new($"'{entryPath}' not found in the ZIP archive");

                // Extract "EHR.dll" to the temporary file
                await using var entryStream = entry.Open();
                await using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
                await entryStream.CopyToAsync(fileStream);
            }

            var fileName = Assembly.GetExecutingAssembly().Location;
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
        var button = InfoPopup.transform.FindChild("ExitGame");
        if (button != null)
        {
            button.gameObject.SetActive(showButton);
            button.GetChild(0).GetComponent<TextTranslatorTMP>().TargetText = buttonText;
            button.GetChild(0).GetComponent<TextTranslatorTMP>().ResetText();
            button.GetComponent<PassiveButton>().OnClick = new();
            if (buttonIsExit) button.GetComponent<PassiveButton>().OnClick.AddListener((Action)Application.Quit);
            else button.GetComponent<PassiveButton>().OnClick.AddListener((Action)(() => InfoPopup.Close()));
        }
    }
}
