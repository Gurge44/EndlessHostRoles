using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using AmongUs.Data;
using HarmonyLib;
using InnerNet;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

public static class TemplateManager
{
    private const string TemplateFilePath = "./EHR_DATA/template.txt";

    private static readonly Dictionary<string, Func<string>> ReplaceDictionary = new()
    {
        ["RoomCode"] = () => GameCode.IntToGameName(AmongUsClient.Instance.GameId),
        ["PlayerName"] = () => DataManager.Player.Customization.Name,
        ["AmongUsVersion"] = () => Application.version,
        ["InternalVersion"] = () => Main.PluginVersion,
        ["ModVersion"] = () => Main.PluginDisplayVersion,
        ["Map"] = () => Constants.MapNames[Main.NormalOptions.MapId],
        ["NumEmergencyMeetings"] = () => Main.NormalOptions.NumEmergencyMeetings.ToString(),
        ["EmergencyCooldown"] = () => Main.NormalOptions.EmergencyCooldown.ToString(),
        ["DiscussionTime"] = () => Main.NormalOptions.DiscussionTime.ToString(),
        ["VotingTime"] = () => Main.NormalOptions.VotingTime.ToString(),
        ["PlayerSpeedMod"] = () => Main.NormalOptions.PlayerSpeedMod.ToString(),
        ["CrewLightMod"] = () => Main.NormalOptions.CrewLightMod.ToString(),
        ["ImpostorLightMod"] = () => Main.NormalOptions.ImpostorLightMod.ToString(),
        ["KillCooldown"] = () => Main.NormalOptions.KillCooldown.ToString(),
        ["NumCommonTasks"] = () => Main.NormalOptions.NumCommonTasks.ToString(),
        ["NumLongTasks"] = () => Main.NormalOptions.NumLongTasks.ToString(),
        ["NumShortTasks"] = () => Main.NormalOptions.NumShortTasks.ToString(),
        ["Date"] = () => DateTime.Now.ToShortDateString(),
        ["Time"] = () => DateTime.Now.ToShortTimeString()
    };

    public static void Init()
    {
        CreateIfNotExists();
    }

    public static void CreateIfNotExists()
    {
        if (!File.Exists(TemplateFilePath))
        {
            try
            {
                if (!Directory.Exists("EHR_DATA")) Directory.CreateDirectory("EHR_DATA");

                if (File.Exists("./template.txt"))
                    File.Move("./template.txt", TemplateFilePath);
                else
                {
                    string fileName;
                    string[] name = CultureInfo.CurrentCulture.Name.Split("-");

                    if (name.Length >= 2)
                    {
                        fileName = name[0] switch
                        {
                            "zh" => "SChinese",
                            "ru" => "Russian",
                            _ => "English"
                        };
                    }
                    else
                        fileName = "English";

                    Logger.Warn($"创建新的 Template 文件：{fileName}", "TemplateManager");
                    File.WriteAllText(TemplateFilePath, GetResourcesTxt($"EHR.Resources.Config.template.{fileName}.txt"));
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "TemplateManager");
            }
        }
        else
        {
            string text = File.ReadAllText(TemplateFilePath, Encoding.GetEncoding("UTF-8"));
            File.WriteAllText(TemplateFilePath, text.Replace("5PNwUaN5", "hkk2p9ggv4"));
        }
    }

    private static string GetResourcesTxt(string path)
    {
        Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
        if (stream == null) return string.Empty;

        stream.Position = 0;
        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static void SendTemplate(string str = "", byte playerId = 0xff, bool noErr = false)
    {
        CreateIfNotExists();
        using StreamReader sr = new(TemplateFilePath, Encoding.GetEncoding("UTF-8"));
        List<string> sendList = [];
        HashSet<string> tags = [];

        while (sr.ReadLine() is { } text)
        {
            string[] tmp = text.Split(":");

            if (tmp.Length > 1 && tmp[1] != "")
            {
                tags.Add(tmp[0]);
                if (string.Equals(tmp[0], str, StringComparison.CurrentCultureIgnoreCase))
                    sendList.Add(tmp.Skip(1).Join(delimiter: ":").Replace("\\n", "\n"));
            }
        }

        if (sendList.Count == 0 && !noErr)
        {
            if (playerId == 0xff)
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, string.Format(GetString("Message.TemplateNotFoundHost"), str, tags.Join(delimiter: ", ")));
            else
                Utils.SendMessage(string.Format(GetString("Message.TemplateNotFoundClient"), str), playerId);
        }
        else
            foreach (string x in sendList)
                Utils.SendMessage(ApplyReplaceDictionary(x), playerId);
    }

    private static string ApplyReplaceDictionary(string text)
    {
        foreach (KeyValuePair<string, Func<string>> kvp in ReplaceDictionary)
            text = Regex.Replace(text, "{{" + kvp.Key + "}}", kvp.Value.Invoke() ?? "", RegexOptions.IgnoreCase);

        return text;
    }
}