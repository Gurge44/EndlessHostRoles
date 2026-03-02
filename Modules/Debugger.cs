using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using EHR.Modules;
using EHR.Patches;
using UnityEngine;
using LogLevel = BepInEx.Logging.LogLevel;

namespace EHR;

internal static class Logger
{
    private static bool IsEnable;
    private static readonly List<string> DisableList = [];
#if DEBUG
    public static bool IsAlsoInGame;
#endif

    private static readonly Dictionary<string, DateTime> NowDetailedErrorLog = [];

    public static void Enable()
    {
        IsEnable = true;
    }

    /*
        public static void Disable()
        {
            IsEnable = false;
        }

        public static void Enable(string tag, bool toGame = false)
        {
            DisableList.Remove(tag);

            if (toGame && !SendToGameList.Contains(tag))
                SendToGameList.Add(tag);
            else
                SendToGameList.Remove(tag);
        }
    */

    public static void Disable(string tag)
    {
        if (!DisableList.Contains(tag)) DisableList.Add(tag);
    }

    public static void SendInGame(string text, Color? textColor = null)
    {
        if (!IsEnable) return;

        NotificationPopper np = NotificationPopperPatch.Instance;

        if (np)
        {
            Warn(text, "SendInGame");

            LobbyNotificationMessage newMessage = Object.Instantiate(np.notificationMessageOrigin, Vector3.zero, Quaternion.identity, np.transform);
            newMessage.transform.localPosition = new(0f, 0f, -2f);
            text = "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">" + text + "</font>";
            newMessage.SetUp(text, np.settingsChangeSprite, textColor ?? np.settingsChangeColor, (Action)(() => np.OnMessageDestroy(newMessage)));
            np.ShiftMessages();
            np.AddMessageToQueue(newMessage);

            SoundManager.Instance.PlaySoundImmediate(np.settingsChangeSound, false);
        }
    }

    private static void SendToFile(string text, LogLevel level = LogLevel.Info, string tag = "", bool escapeCRLF = true, int lineNumber = 0, string fileName = "", bool multiLine = false)
    {
        if (!IsEnable || DisableList.Contains(tag) || (level == LogLevel.Debug && !DebugModeManager.AmDebugger)) return;

#if DEBUG
        if (IsAlsoInGame) SendInGame($"[{tag}]{text}");
#endif

        string logText;

        DateTime now = DateTime.Now;

        if (level is LogLevel.Error or LogLevel.Fatal && !multiLine && (!NowDetailedErrorLog.TryGetValue(tag, out DateTime dt) || dt.AddSeconds(3) < now))
        {
            var t = now.ToString("HH:mm:ss");
            StackFrame stack = new(2);
            string className = stack.GetMethod()?.ReflectedType?.Name;
            string memberName = stack.GetMethod()?.Name;
            logText = $"[{t}][{className}.{memberName}({Path.GetFileName(fileName)}:{lineNumber})][{tag}]{text}";
            NowDetailedErrorLog[tag] = now;
        }
        else
        {
            if (escapeCRLF) text = text.Replace("\r", "\\r").Replace("\n", "\\n");

            var t = now.ToString("HH:mm:ss");
            logText = $"[{t}][{tag}]{text}";

            if (level == LogLevel.Message) NowDetailedErrorLog.Clear();
        }

        CustomLogger.Instance.Log(level.ToString(), logText, multiLine);
    }

    public static void Test(object content, string tag = "======= Test =======", bool escapeCRLF = true, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "", bool multiLine = false)
    {
        SendToFile(content.ToString(), LogLevel.Debug, tag, escapeCRLF, lineNumber, fileName, multiLine);
    }

    public static void Info(string text, string tag, bool escapeCRLF = true, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "", bool multiLine = false)
    {
        SendToFile(text, LogLevel.Info, tag, escapeCRLF, lineNumber, fileName, multiLine);
    }

    public static void Warn(string text, string tag, bool escapeCRLF = true, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "", bool multiLine = false)
    {
        SendToFile(text, LogLevel.Warning, tag, escapeCRLF, lineNumber, fileName, multiLine);
    }

    public static void Error(string text, string tag, bool escapeCRLF = true, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "", bool multiLine = false)
    {
        SendToFile(text, LogLevel.Error, tag, escapeCRLF, lineNumber, fileName, multiLine);
    }

    public static void Fatal(string text, string tag, bool escapeCRLF = true, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "", bool multiLine = false)
    {
        SendToFile(text, LogLevel.Fatal, tag, escapeCRLF, lineNumber, fileName, multiLine);
    }

    public static void Msg(string text, string tag, bool escapeCRLF = true, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "", bool multiLine = false)
    {
        SendToFile(text, LogLevel.Message, tag, escapeCRLF, lineNumber, fileName, multiLine);
    }

    public static void Exception(Exception ex, string tag, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "", bool multiLine = false)
    {
        SendToFile(ex.ToString(), LogLevel.Error, tag, false, lineNumber, fileName);
    }

    public static void CurrentMethod([CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "")
    {
        StackFrame stack = new(1);
        MethodBase method = stack.GetMethod();
        Msg($"\"{method?.ReflectedType?.Name}.{method?.Name}\" Called in \"{Path.GetFileName(fileName)}({lineNumber})\"", "Method");
    }

    public static LogHandler Handler(string tag)
    {
        return new(tag);
    }
}

public class CustomLogger
{
    public static readonly string LOGFilePath = Path.Combine(Main.DataPath, OperatingSystem.IsAndroid() ? "EHR" : "BepInEx", "log.html");

    private const string HtmlHeader =
        """
        <!DOCTYPE html>
        <html lang='en'>
        <head>
          <meta charset='UTF-8'>
          <meta name='viewport' content='width=device-width, initial-scale=1.0'>
          <title>EHR Log File</title>
          <style>
              body { font-family: Arial, sans-serif; background-color: #1e1e1e; color: #aaaaaa; margin: 0; padding: 1rem; font-family: "Roboto Mono", "Consolas", "Courier New", monospace; }
              .log-entry { margin: 0; padding: 0; border-radius: 5px; letter-spacing: 0.1rem; }
              .info { background-color: transparent; }
              .warning { background-color: #ffff44; color: black; }
              .error { background-color: red; color: black; border-radius: 10px; margin: 1rem; }
              .fatal { background: linear-gradient(to bottom, #ff9999, #cc0000); color: black; border: 3px solid yellow; border-radius: 15px; padding: 1rem; }
              .debug { background-color: gray; color: white; }
              .message { color: aqua; }
          </style>
        </head>
        <body>
          <h1>EHR Log File</h1>
          <div id='log-container'>

        """;

    private const string HtmlFooter =
        """
             </div>
            </body>
            </html>
        """;

    private static CustomLogger PrivateInstance;
    private float timer = 1f;

    private readonly StringBuilder Builder;

    private CustomLogger()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LOGFilePath) ?? throw new InvalidOperationException());
        if (!File.Exists(LOGFilePath)) File.WriteAllText(LOGFilePath, HtmlHeader);
        else if (Options.IsLoaded && new FileInfo(LOGFilePath).Length > 4 * 1024 * 1024) // 4 MB
        {
            ClearLog(false);
            PrivateInstance ??= new();
            LateTask.New(() => Logger.SendInGame("The size of the log file exceeded 4 MB and was dumped."), 0.1f, log: false);
        }

        Builder = new();
        Main.Instance.StartCoroutine(InactivityCheck());
    }

    public static CustomLogger Instance => PrivateInstance ??= new();

    public static void ClearLog(bool check = true)
    {
        if (!check || (File.Exists(LOGFilePath) && new FileInfo(LOGFilePath).Length > 0))
        {
            PrivateInstance?.Finish();
            Utils.DumpLog(false, false);
        }

        File.WriteAllText(LOGFilePath, HtmlHeader);
    }

    public void Log(string level, string message, bool multiLine = false)
    {
        if (multiLine) message = message.Replace("\\n", "<br>");

        if (message.Contains("<b")) message += "</b>";
        if (message.Contains("<u")) message += "</u>";
        if (message.Contains("<i")) message += "</i>";
        if (message.Contains("<s")) message += "</s>";

        var logEntry = $"""
                        <div class='log-entry {level.ToLower()}'>
                            {message}
                        </div>
                        """;

        Builder.Append(logEntry);
#if DEBUG
        Finish(false);
#endif
    }

    private IEnumerator InactivityCheck()
    {
        while (timer > 0)
        {
            timer -= Time.deltaTime;
            yield return null;
        }

        Finish(false);
    }

    public void Finish(bool dump = true)
    {
        var append = Builder.ToString();
        if (string.IsNullOrWhiteSpace(append)) return;
        if (dump) append += HtmlFooter;
        File.AppendAllText(LOGFilePath, append);
        PrivateInstance = null;
#if DEBUG
        Main.Instance.StopCoroutine(InactivityCheck());
#endif
    }
}