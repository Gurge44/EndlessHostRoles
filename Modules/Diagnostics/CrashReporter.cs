using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EHR;

public static class CrashReporter
{
    private static readonly string PendingCrashPath = GetPendingCrashPath();

    private const string ApiEndpoint = "https://app.gurge44.eu/api/reports/crashes";
    private const int MaxLogChars = 5 * 1024 * 1024;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    // lines collected by CrashErrorListener during this session
    private static readonly List<string> ErrorBuffer = [];
    private static readonly object ErrorBufferLock = new();

    public static void Init()
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        CheckPendingCrash();
    }

    // called by CrashErrorListener for every Error/Fatal log event
    internal static void BufferError(string line)
    {
        lock (ErrorBufferLock)
            ErrorBuffer.Add(line);
    }

    // BepInEx/ is unreachable on Starlight Android
    private static string GetPendingCrashPath()
    {
        string dir = OperatingSystem.IsAndroid()
            ? Path.Combine(Main.DataPath, "EHR_Logs")
            : Path.Combine(Main.DataPath, "BepInEx");

        return Path.Combine(dir, "pending_crash.json");
    }

    // upload anything left over from a previous session that failed to send
    private static void CheckPendingCrash()
    {
        try
        {
            if (!File.Exists(PendingCrashPath)) return;

            string json = File.ReadAllText(PendingCrashPath);
            if (string.IsNullOrWhiteSpace(json)) return;

            _ = Task.Run(async () =>
            {
                bool sent = await SendReport(json);
                if (sent)
                    try { File.Delete(PendingCrashPath); } catch { }
            });
        }
        catch (Exception e)
        {
            BepInEx.Logging.Logger.CreateLogSource("EHR.CrashReporter")
                .LogWarning($"CheckPendingCrash failed: {e.Message}");
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        string logContent = ReadLogFile(CustomLogger.LOGFilePath);

        // flush buffered HTML to disk before we read it
        try { CustomLogger.Instance.Finish(); } catch { }

        string errorMessage = e.ExceptionObject?.ToString() ?? "Unknown managed exception";
        string json = BuildPayload(errorMessage, "UnhandledException", logContent);

        // write to disk first in case the POST doesn't finish before the process dies
        WritePending(json);
        try { SendReport(json).Wait(TimeSpan.FromSeconds(5)); } catch { }
    }

    private static string BuildPayload(string errorMessage, string source, string logContent)
    {
        if (logContent.Length > MaxLogChars)
            logContent = logContent[^MaxLogChars..];

        string bufferedErrors;
        lock (ErrorBufferLock)
        {
            bufferedErrors = ErrorBuffer.Count > 0 ? string.Join("\n", ErrorBuffer) : "";
            ErrorBuffer.Clear();
        }

        string combinedError = string.IsNullOrEmpty(bufferedErrors)
            ? errorMessage
            : bufferedErrors + (string.IsNullOrEmpty(errorMessage) ? "" : "\n" + errorMessage);

        var payload = new Dictionary<string, string>
        {
            ["source"] = source,
            ["mod_version"] = Main.PluginVersion,
            ["os"] = GetOSName(),
            ["crashed_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            ["puid"] = TryGet(() => EOSManager.Instance.ProductUserId),
            ["friend_code"] = TryGet(() => EOSManager.Instance.FriendCode),
            ["error"] = combinedError,
            ["log"] = logContent
        };

        return JsonSerializer.Serialize(payload);
    }

    internal static void WritePending(string json)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PendingCrashPath)!);
            File.WriteAllText(PendingCrashPath, json);
        }
        catch { }
    }

    internal static async Task<bool> SendReport(string json)
    {
        try
        {
            var body = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await Http.PostAsync(ApiEndpoint, body);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // uses FileShare.ReadWrite so we can read while BepInEx still holds the file open
    private static string ReadLogFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return "(log file not found)";
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs, Encoding.UTF8);
            return sr.ReadToEnd();
        }
        catch (Exception ex)
        {
            return $"(failed to read log: {ex.Message})";
        }
    }

    private static string GetOSName()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsLinux()) return "Linux";
        if (OperatingSystem.IsMacOS()) return "macOS";
        if (OperatingSystem.IsAndroid()) return "Android";
        return Environment.OSVersion.ToString();
    }

    private static string TryGet(Func<string> getter)
    {
        try { return getter() ?? "unknown"; }
        catch (Exception ex) { return $"unavailable ({ex.GetType().Name}: {ex.Message})"; }
    }
}
