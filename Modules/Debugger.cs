using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using EHR.Modules;
using UnityEngine;
using LogLevel = BepInEx.Logging.LogLevel;

namespace EHR
{
    internal class Webhook
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

    internal static class Logger
    {
        private static bool IsEnable;
        private static readonly List<string> DisableList = [];
        private static readonly List<string> SendToGameList = [];
        public static bool IsAlsoInGame;

        private static readonly HashSet<string> NowDetailedErrorLog = [];

        public static void Enable()
        {
            IsEnable = true;
        }

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
            if (!IsEnable || DisableList.Contains(tag) || (level == LogLevel.Debug && !DebugModeManager.AmDebugger)) return;

            if (SendToGameList.Contains(tag) || IsAlsoInGame) SendInGame($"[{tag}]{text}");

            string log_text;

            if (level is LogLevel.Error or LogLevel.Fatal && !multiLine && !NowDetailedErrorLog.Contains(tag))
            {
                var t = DateTime.Now.ToString("HH:mm:ss");
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

                var t = DateTime.Now.ToString("HH:mm:ss");
                log_text = $"[{t}][{tag}]{text}";
            }

            if (multiLine)
            {
                Main.Instance.StartCoroutine(LogMultilineAsync());
                return;
            }

            CustomLogger.Instance.Log(level.ToString(), log_text);

            return;

            IEnumerator LogMultilineAsync()
            {
                foreach (string s in log_text.Split("\\n"))
                {
                    CustomLogger.Instance.Log(level.ToString(), s);
                    yield return null;
                }
            }
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
        private const string LOGFilePath = "./BepInEx/log.html";

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
        private float timer = 3f;

        private CustomLogger()
        {
            if (!File.Exists(LOGFilePath)) File.WriteAllText(LOGFilePath, HtmlHeader);
            Main.Instance.StartCoroutine(InactivityCheck());
        }

        // Singleton to ensure a single logger instance
        public static CustomLogger Instance
        {
            get { return PrivateInstance ??= new(); }
        }

        public static void ClearLog()
        {
            File.WriteAllText(LOGFilePath, HtmlHeader);
        }

        public void Log(string level, string message)
        {
            string logEntry = $"""
                               <div class='log-entry {level.ToLower()}'>
                                   {message}
                               </div>
                               """;

            File.AppendAllText(LOGFilePath, logEntry);

            timer = 3f;
        }

        private IEnumerator InactivityCheck()
        {
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                yield return null;
            }

            File.AppendAllText(LOGFilePath, HtmlFooter);
            PrivateInstance = null;
        }
    }
}