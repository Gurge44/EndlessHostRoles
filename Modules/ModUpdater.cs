using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE;

[HarmonyPatch]
public class ModUpdater
{
    private static readonly string URL_Github = "https://api.github.com/repos/Gurge44/TOHE_PLUS";
    public static bool hasUpdate = false;
    public static bool hasOutdate = false;
    public static bool forceUpdate = false;
    public static bool isBroken = false;
    public static bool isChecked = false;
    public static Version latestVersion = null;
    public static string latestTitle = null;
    public static string downloadUrl = null;
    public static string notice = null;
    public static GenericPopup InfoPopup;

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start)), HarmonyPrefix]
    [HarmonyPriority(2)]
    public static void Start_Prefix()
    {
        NewVersionCheck();
        DeleteOldFiles();
        InfoPopup = UnityEngine.Object.Instantiate(Twitch.TwitchManager.Instance.TwitchPopup);
        InfoPopup.name = "InfoPopup";
        InfoPopup.TextAreaTMP.GetComponent<RectTransform>().sizeDelta = new(2.5f, 2f);
        if (!isChecked)
        {
            bool done = CheckReleaseFromGithub(Main.BetaBuildURL.Value != "").GetAwaiter().GetResult();
            Logger.Warn("done: " + done, "CheckRelease");
            Logger.Info("hasupdate: " + hasUpdate, "CheckRelease");
            Logger.Info("forceupdate: " + forceUpdate, "CheckRelease");
            Logger.Info("downloadUrl: " + downloadUrl, "CheckRelease");
            Logger.Info("latestVersionl: " + latestVersion, "CheckRelease");
        }
    }

    public static string Get(string url)
    {
        string result = "";
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
        string url = URL_Github + "/releases/latest";
        try
        {
            string result;
            using (HttpClient client = new())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "TOHE+ Updater");
                using var response = await client.GetAsync(new Uri(url), HttpCompletionOption.ResponseContentRead);
                if (!response.IsSuccessStatusCode || response.Content == null)
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
                latestVersion = new(data["tag_name"]?.ToString().TrimStart('v'));
                latestTitle = $"Ver. {latestVersion}";
                JArray assets = data["assets"].Cast<JArray>();
                //Logger.Info(assets.ToString(), "ModUpdater");
                for (int i = 0; i < assets.Count; i++)
                {
                    if (assets[i]["name"].ToString() == $"TOHE-PLUS.v{latestVersion}.zip")
                    {
                        downloadUrl = assets[i]["browser_download_url"].ToString();
                        break;
                    }
                }
                hasUpdate = latestVersion.CompareTo(Main.version) > 0;
                hasOutdate = latestVersion.CompareTo(Main.version) < 0;
            }

            Logger.Info("hasupdate: " + hasUpdate, "GitHub");
            Logger.Info("hasoutdate: " + hasOutdate, "GitHub");
            Logger.Info("forceupdate: " + forceUpdate, "GitHub");
            Logger.Info("downloadUrl: " + downloadUrl, "GitHub");
            Logger.Info("latestVersionl: " + latestVersion, "GitHub");
            Logger.Info("latestTitle: " + latestTitle, "GitHub");

            if (downloadUrl == null || downloadUrl == "")
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
        if (!github)
        {
            _ = DownloadDLL(url);
        }
        else
        {
            _ = DownloadDLLGithub(url);
        }
        return;
    }
    public static bool NewVersionCheck()
    {
        try
        {
            var fileName = Assembly.GetExecutingAssembly().Location;
            if (Directory.Exists("TOH_DATA") && File.Exists(@"./TOHE_DATA/BanWords.txt"))
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
        string searchPattern = "TOHE+.dll*";
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
    private static readonly object downloadLock = new();
    public static async Task<bool> DownloadDLL(string url)
    {
        try
        {
            var savePath = "BepInEx/plugins/TOHE+.dll.temp";

            // Delete the temporary file if it exists
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }

            HttpResponseMessage response;
            var downloadCallBack = DownloadCallBack;

            using (HttpClient client = new())
            {
                response = await client.GetAsync(url);
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                throw new Exception($"File retrieval failed with status code: {response?.StatusCode}");
            }

            var total = response.Content.Headers.ContentLength ?? 0;
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
            {
                if (downloadCallBack == null)
                {
                    await stream.CopyToAsync(fileStream);
                }
                else
                {
                    byte[] buffer = new byte[1024];
                    long readLength = 0;
                    int length;

                    while ((length = await stream.ReadAsync(buffer)) != 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, length));

                        readLength += length;
                        double? progress = Math.Round((double)readLength / total * 100, 2, MidpointRounding.ToZero);
                    }
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
            var savePath = "BepInEx/plugins/TOHE+.dll.temp";

            // Delete the temporary file if it exists
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }

            HttpResponseMessage response;
            var downloadCallBack = DownloadCallBack;

            using (HttpClient client = new())
            {
                response = await client.GetAsync(url);
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                throw new Exception($"File retrieval failed with status code: {response?.StatusCode}");
            }

            var total = response.Content.Headers.ContentLength ?? 0;
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                // Specify the relative path within the ZIP archive where "TOHE+.dll" is located
                var entryPath = "BepInEx/plugins/TOHE+.dll";
                var entry = archive.GetEntry(entryPath) ?? throw new Exception($"'{entryPath}' not found in the ZIP archive");

                // Extract "TOHE+.dll" to the temporary file
                using var entryStream = entry.Open();
                using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
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
    private static void DownloadCallBack(long total, long downloaded, double progress) { }
    private static void ShowPopup(string message, StringNames buttonText, bool showButton = false, bool buttonIsExit = true)
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
            if (buttonIsExit) button.GetComponent<PassiveButton>().OnClick.AddListener((Action)(() => Application.Quit()));
            else button.GetComponent<PassiveButton>().OnClick.AddListener((Action)(() => InfoPopup.Close()));
        }
    }
}