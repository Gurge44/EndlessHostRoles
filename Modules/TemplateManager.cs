using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using AmongUs.Data;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

public static class TemplateManager
{
    private static readonly string TemplateFilePath = $"{Main.DataPath}/EHR_DATA/template.txt";

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
                if (!Directory.Exists($"{Main.DataPath}/EHR_DATA")) Directory.CreateDirectory($"{Main.DataPath}/EHR_DATA");

                if (File.Exists($"{Main.DataPath}/template.txt"))
                    File.Move($"{Main.DataPath}/template.txt", TemplateFilePath);
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

                    Logger.Warn($"Creating new template file: {fileName}", "TemplateManager");
                    File.WriteAllText(TemplateFilePath, GetResourcesTxt($"EHR.Resources.Config.template.{fileName}.txt"));
                }
            }
            catch (Exception ex) { Logger.Exception(ex, "TemplateManager"); }
        }
        else
        {
            const string oldWelcomeTemplate = "welcome:<b><size=2.5>Welcome!</size></b>\\nThis is a <color=#ff0000>Modded</color> Lobby.\\n<size=90%>The mod is <b><color=#00ffff>Endless Host Roles</color> <color=#00ffa5>(EHR)</color></b> <b><color=#902efd>v{{ModVersion}}</b></color>.\\nThe mod was made by <color=#ffff00>Gurge44</color>.</size>\\n\\n<size=70%><b>/r</b> → Show all active roles\\n<b>/r <color=#ff0000>[role name]</color></b> → Show description & settings for that role</size>";
            const string newWelcomeTemplate = "welcome:<b>Welcome!</b>\\n<size=80%>This is a <color=red>Modded</color> Lobby.\\nYou're playing <b><color=blue>Endless Host Roles</color> <color=purple>(EHR)</color> <color=orange>v{{ModVersion}}</b></color>.\\nThe mod was made by <color=yellow>Gurge44</color>.\\n\\n<b>/r</b> → Show all active roles\\n<b>/r <color=red>[role name]</color></b> → Show description & settings for that role";
            string text = File.ReadAllText(TemplateFilePath, Encoding.GetEncoding("UTF-8"));
            File.WriteAllText(TemplateFilePath, text.Replace("5PNwUaN5", "hkk2p9ggv4").Replace(oldWelcomeTemplate, newWelcomeTemplate));
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

    public static void SendTemplate(string str = "", byte playerId = 0xff, bool noErr = false, MessageImportance importance = MessageImportance.Medium)
    {
        CreateIfNotExists();
        using StreamReader sr = new(TemplateFilePath, Encoding.GetEncoding("UTF-8"));
        List<string> sendList = [];
        HashSet<string> tags = [];

        while (sr.ReadLine() is { } text)
        {
            string[] tmp = text.Split(':');

            if (tmp.Length > 1 && tmp[1] != "")
            {
                tags.Add(tmp[0]);
                if (string.Equals(tmp[0], str, StringComparison.CurrentCultureIgnoreCase))
                    sendList.Add(string.Join(':', tmp[1..]).Replace("\\n", "\n"));
            }
        }

        if (sendList.Count == 0 && !noErr)
        {
            if (playerId == 0xff)
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, string.Format(GetString("Message.TemplateNotFoundHost"), str, tags.Join(delimiter: ", ")));
            else
                Utils.SendMessage(string.Format(GetString("Message.TemplateNotFoundClient"), str), playerId, importance: MessageImportance.Low);
        }
        else
        {
            List<Message> messages = [];

            foreach (string x in sendList)
                messages.Add(new Message(ApplyReplaceDictionary(x), playerId));

            messages.SendMultipleMessages(importance);
        }
    }

    private static string ApplyReplaceDictionary(string text)
    {
        foreach (KeyValuePair<string, Func<string>> kvp in ReplaceDictionary)
            text = Regex.Replace(text, "{{" + kvp.Key + "}}", kvp.Value.Invoke() ?? "", RegexOptions.IgnoreCase);

        return text;
    }
}