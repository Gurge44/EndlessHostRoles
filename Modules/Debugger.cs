using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using EHR.Modules;
using LogLevel = BepInEx.Logging.LogLevel;

namespace EHR;

class Webhook
{
    public static void Send(string text)
    {
        if (Main.WebhookUrl.Value == "none") return;
        HttpClient httpClient = new();
        Dictionary<string, string> strs = new()
        {
            { "content", text },
            { "username", "EHR-Debugger" },
            { "avatar_url", "https://npm.elemecdn.com/hexo-static@1.0.1/img/avatar.webp" }
        };
        TaskAwaiter<HttpResponseMessage> awaiter = httpClient.PostAsync(
            Main.WebhookUrl.Value, new FormUrlEncodedContent(strs)).GetAwaiter();
        awaiter.GetResult();
    }
}

class Logger
{
    private static bool IsEnable;
    private static readonly List<string> DisableList = [];
    private static readonly List<string> SendToGameList = [];
    public static bool IsAlsoInGame;

    private static readonly HashSet<string> NowDetailedErrorLog = [];
    public static void Enable() => IsEnable = true;
    public static void Disable() => IsEnable = false;

    public static void Enable(string tag, bool toGame = false)
    {
        DisableList.Remove(tag);
        if (toGame && !SendToGameList.Contains(tag)) SendToGameList.Add(tag);
        else SendToGameList.Remove(tag);
    }

    public static void Disable(string tag)
    {
        if (!DisableList.Contains(tag)) DisableList.Add(tag);
    }

    public static void SendInGame(string text /*, bool isAlways = false*/)
    {
        if (!IsEnable) return;
        if (DestroyableSingleton<HudManager>._instance)
        {
            DestroyableSingleton<HudManager>.Instance.Notifier.AddDisconnectMessage(text);
            Warn(text, "SendInGame");
        }
    }

    private static void SendToFile(string text, LogLevel level = LogLevel.Info, string tag = "", bool escapeCRLF = true, int lineNumber = 0, string fileName = "", bool multiLine = false)
    {
        if (!IsEnable || DisableList.Contains(tag)) return;
        var logger = Main.Logger;

        if (SendToGameList.Contains(tag) || IsAlsoInGame)
        {
            SendInGame($"[{tag}]{text}");
        }

        string log_text;
        if (level is LogLevel.Error or LogLevel.Fatal && !multiLine && !NowDetailedErrorLog.Contains(tag))
        {
            string t = DateTime.Now.ToString("HH:mm:ss");
            StackFrame stack = new(2);
            string className = stack.GetMethod()?.ReflectedType?.Name;
            string memberName = stack.GetMethod()?.Name;
            log_text = $"[{t}][{className}.{memberName}({Path.GetFileName(fileName)}:{lineNumber})][{tag}]{text}";
            NowDetailedErrorLog.Add(tag);
            LateTask.New(() => NowDetailedErrorLog.Remove(tag), 3f, log: false);
        }
        else
        {
            if (escapeCRLF) text = text.Replace("\r", "\\r").Replace("\n", "\\n");
            string t = DateTime.Now.ToString("HH:mm:ss");
            log_text = $"[{t}][{tag}]{text}";
        }

        switch (level)
        {
            case LogLevel.Info when !multiLine:
                logger.LogInfo(log_text);
                break;
            case LogLevel.Info:
                log_text.Split("\\n").Do(x => logger.LogInfo(x));
                break;
            case LogLevel.Warning when !multiLine:
                logger.LogWarning(log_text);
                break;
            case LogLevel.Warning:
                log_text.Split("\\n").Do(x => logger.LogWarning(x));
                break;
            case LogLevel.Error when !multiLine:
                logger.LogError(log_text);
                break;
            case LogLevel.Error:
                log_text.Split("\\n").Do(x => logger.LogError(x));
                break;
            case LogLevel.Fatal when !multiLine:
                logger.LogFatal(log_text);
                break;
            case LogLevel.Fatal:
                log_text.Split("\\n").Do(x => logger.LogFatal(x));
                break;
            case LogLevel.Message when !multiLine:
                logger.LogMessage(log_text);
                break;
            case LogLevel.Message:
                log_text.Split("\\n").Do(x => logger.LogMessage(x));
                break;
            case LogLevel.Debug when !multiLine:
                logger.LogFatal(log_text);
                break;
            case LogLevel.Debug:
                log_text.Split("\\n").Do(x => logger.LogFatal(x));
                break;
            default:
                logger.LogWarning("Error: Invalid LogLevel");
                logger.LogInfo(log_text);
                break;
        }
    }

    public static void Test(object content, string tag = "======= Test =======", bool escapeCRLF = true, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "", bool multiLine = false) =>
        SendToFile(content.ToString(), LogLevel.Debug, tag, escapeCRLF, lineNumber, fileName, multiLine);

    public static void Info(string text, string tag, bool escapeCRLF = true, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "", bool multiLine = false) =>
        SendToFile(text, LogLevel.Info, tag, escapeCRLF, lineNumber, fileName, multiLine);

    public static void Warn(string text, string tag, bool escapeCRLF = true, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "", bool multiLine = false) =>
        SendToFile(text, LogLevel.Warning, tag, escapeCRLF, lineNumber, fileName, multiLine);

    public static void Error(string text, string tag, bool escapeCRLF = true, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "", bool multiLine = false) =>
        SendToFile(text, LogLevel.Error, tag, escapeCRLF, lineNumber, fileName, multiLine);

    public static void Fatal(string text, string tag, bool escapeCRLF = true, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "", bool multiLine = false) =>
        SendToFile(text, LogLevel.Fatal, tag, escapeCRLF, lineNumber, fileName, multiLine);

    public static void Msg(string text, string tag, bool escapeCRLF = true, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "", bool multiLine = false) =>
        SendToFile(text, LogLevel.Message, tag, escapeCRLF, lineNumber, fileName, multiLine);

    public static void Exception(Exception ex, string tag, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "", bool multiLine = false) =>
        SendToFile(ex.ToString(), LogLevel.Error, tag, false, lineNumber, fileName);

    public static void CurrentMethod([CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "")
    {
        StackFrame stack = new(1);
        Msg($"\"{stack.GetMethod()?.ReflectedType?.Name}.{stack.GetMethod()?.Name}\" Called in \"{Path.GetFileName(fileName)}({lineNumber})\"", "Method");
    }

    public static LogHandler Handler(string tag) => new(tag);
}