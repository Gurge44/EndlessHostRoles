using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using static EHR.Translator;

namespace EHR;

public static class SpamManager
{
    private static readonly string BannedWordsFilePath = $"{Main.DataPath}/EHR_DATA/BanWords.txt";
    private static List<string> BanWords = [];

    public static void Init()
    {
        CreateIfNotExists();
        BanWords = ReturnAllNewLinesInFile(BannedWordsFilePath);
    }

    private static void CreateIfNotExists()
    {
        if (!File.Exists(BannedWordsFilePath))
        {
            try
            {
                if (!Directory.Exists($"{Main.DataPath}/EHR_DATA")) Directory.CreateDirectory($"{Main.DataPath}/EHR_DATA");

                if (File.Exists($"{Main.DataPath}/BanWords.txt"))
                    File.Move($"{Main.DataPath}/BanWords.txt", BannedWordsFilePath);
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

                    Logger.Warn($"Creating new BanWords file: {fileName}", "SpamManager");
                    File.WriteAllText(BannedWordsFilePath, GetResourcesTxt($"EHR.Resources.Config.BanWords.{fileName}.txt"));
                }
            }
            catch (Exception ex) { Logger.Exception(ex, "SpamManager"); }
        }
    }

    private static string GetResourcesTxt(string path)
    {
        Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
        stream!.Position = 0;
        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static List<string> ReturnAllNewLinesInFile(string filename)
    {
        if (!File.Exists(filename)) return [];

        using StreamReader sr = new(filename, Encoding.GetEncoding("UTF-8"));
        List<string> sendList = [];

        while (sr.ReadLine() is { } text)
        {
            if (text.Length > 1 && text != "")
                sendList.Add(text.Replace("\\n", "\n").ToLower());
        }

        return sendList;
    }

    public static bool CheckSpam(PlayerControl player, string text)
    {
        if (player.AmOwner) return false;

        string name = player.GetRealName();
        var kick = false;
        var msg = string.Empty;

        if (Options.AutoKickStart.GetBool())
        {
            if (ContainsStart(text) && GameStates.IsLobby && !ChatCommands.IsPlayerVIP(player.FriendCode))
            {
                Main.SayStartTimes.TryAdd(player.OwnerId, 0);

                Main.SayStartTimes[player.OwnerId]++;
                msg = string.Format(GetString("Message.WarnWhoSayStart"), name, Main.SayStartTimes[player.OwnerId]);

                if (Main.SayStartTimes[player.OwnerId] > Options.AutoKickStartTimes.GetInt())
                {
                    msg = string.Format(GetString("Message.KickStartAfterWarn"), name, Main.SayStartTimes[player.OwnerId]);
                    kick = true;
                }

                if (msg != string.Empty && msg != "") Utils.SendMessage(msg, importance: MessageImportance.Low);

                if (kick) AmongUsClient.Instance.KickPlayer(player.OwnerId, Options.AutoKickStartAsBan.GetBool());

                return true;
            }
        }

        bool banned = BanWords.Any(x => text.Contains(x, StringComparison.OrdinalIgnoreCase));

        if (!banned) return false;

        if (Options.AutoWarnStopWords.GetBool()) msg = string.Format(GetString("Message.WarnWhoSayBanWord"), name);

        if (Options.AutoKickStopWords.GetBool())
        {
            Main.SayBanwordsTimes.TryAdd(player.OwnerId, 0);

            Main.SayBanwordsTimes[player.OwnerId]++;
            msg = string.Format(GetString("Message.WarnWhoSayBanWordTimes"), name, Main.SayBanwordsTimes[player.OwnerId]);

            if (Main.SayBanwordsTimes[player.OwnerId] > Options.AutoKickStopWordsTimes.GetInt())
            {
                msg = string.Format(GetString("Message.KickWhoSayBanWordAfterWarn"), name, Main.SayBanwordsTimes[player.OwnerId]);
                kick = true;
            }
        }

        if (msg != string.Empty && msg != "")
        {
            if (kick || !GameStates.IsInGame)
                Utils.SendMessage(msg, importance: MessageImportance.Low);
            else
            {
                foreach (PlayerControl pc in Main.CachedAllPlayerControls())
                    if (pc.IsAlive() == player.IsAlive())
                        Utils.SendMessage(msg, pc.PlayerId, importance: MessageImportance.Low);
            }
        }

        if (kick) AmongUsClient.Instance.KickPlayer(player.OwnerId, Options.AutoKickStopWordsAsBan.GetBool());

        return true;
    }
    
    private static readonly string[] StartWords =
    [
        "start",
        "started",
        "begin",
        "commence",
        "proceed",

        // Russian
        "старт",
        "начни",
        "начинай",
        "го",
        "гоу",

        // Chinese
        "开",
        "开始",
        "快开",

        // Pinyin
        "kai",
        "kaishi"
    ];

    private static bool ContainsStart(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (text.Length > 35)
            return false;

        string normalized = Normalize(text);

        foreach (string word in StartWords)
        {
            string target = Normalize(word);

            if (normalized.Contains(target))
                return true;

            // Fuzzy matching
            if (LevenshteinDistance(normalized, target) <= 1)
                return true;

            // Check every substring of similar length
            for (int i = 0; i < normalized.Length; i++)
            {
                int len = Math.Min(target.Length + 2, normalized.Length - i);

                for (int j = Math.Max(1, target.Length - 2); j <= len; j++)
                {
                    string part = normalized.Substring(i, j);

                    if (LevenshteinDistance(part, target) <= 1)
                        return true;
                }
            }
        }

        // Chinese "开开开"
        return normalized.Count(c => c == '开') >= 3;
    }
    
    private static string Normalize(string text)
    {
        text = text.ToLowerInvariant();

        text = text.Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder();

        foreach (char c in text)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);

            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            sb.Append(c);
        }

        text = sb.ToString();

        // Common leetspeak replacements
        text = text
            .Replace('0', 'o')
            .Replace('1', 'i')
            .Replace('3', 'e')
            .Replace('4', 'a')
            .Replace('5', 's')
            .Replace('7', 't');

        // Remove everything except letters/numbers/CJK
        text = Regex.Replace(text, @"[\W_]+", "");

        return text;
    }
    
    private static int LevenshteinDistance(string s, string t)
    {
        int[,] d = new int[s.Length + 1, t.Length + 1];

        for (int i = 0; i <= s.Length; i++)
            d[i, 0] = i;

        for (int j = 0; j <= t.Length; j++)
            d[0, j] = j;

        for (int i = 1; i <= s.Length; i++)
        {
            for (int j = 1; j <= t.Length; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;

                d[i, j] = Math.Min(
                    Math.Min(
                        d[i - 1, j] + 1,
                        d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[s.Length, t.Length];
    }
}