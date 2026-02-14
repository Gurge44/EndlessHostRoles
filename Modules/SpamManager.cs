using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Hazel;
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
            if (ContainsStart(text) && GameStates.IsLobby && !ChatCommands.IsPlayerModerator(player.FriendCode) && !ChatCommands.IsPlayerVIP(player.FriendCode))
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

        bool banned = BanWords.Any(text.Contains);

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
                foreach (PlayerControl pc in Main.EnumeratePlayerControls())
                    if (pc.IsAlive() == player.IsAlive())
                        Utils.SendMessage(msg, pc.PlayerId, importance: MessageImportance.Low);
            }
        }

        if (kick) AmongUsClient.Instance.KickPlayer(player.OwnerId, Options.AutoKickStopWordsAsBan.GetBool());

        return true;
    }

    private static bool ContainsStart(string text)
    {
        text = text.Trim().ToLower();

        var stNum = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i..].Equals("k")) stNum++;
            if (text[i..].Equals("开")) stNum++;
        }

        if (stNum >= 3) return true;

        switch (text)
        {
            case "Start":
            case "start":
            case "/Start":
            case "/Start/":
            case "Start/":
            case "/start":
            case "/start/":
            case "start/":
            case "plsstart":
            case "pls start":
            case "please start":
            case "pleasestart":
            case "Plsstart":
            case "Pls start":
            case "Please start":
            case "Pleasestart":
            case "plsStart":
            case "pls Start":
            case "please Start":
            case "pleaseStart":
            case "PlsStart":
            case "Pls Start":
            case "Please Start":
            case "PleaseStart":
            case "sTart":
            case "stArt":
            case "staRt":
            case "starT":
            case "s t a r t":
            case "S t a r t":
            case "started":
            case "Started":
            case "s t a r t e d":
            case "S t a r t e d":
            case "Го":
            case "гО":
            case "го":
            case "Гоу":
            case "гоу":
            case "Старт":
            case "старт":
            case "/Старт":
            case "/Старт/":
            case "Старт/":
            case "/старт":
            case "/старт/":
            case "старт/":
            case "пжстарт":
            case "пж старт":
            case "пжСтарт":
            case "пж Старт":
            case "Пжстарт":
            case "Пж старт":
            case "ПжСтарт":
            case "Пж Старт":
            case "сТарт":
            case "стАрт":
            case "стаРт":
            case "старТ":
            case "с т а р т":
            case "С т а р т":
            case "начни":
            case "Начни":
            case "начинай":
            case "начинай уже":
            case "Начинай":
            case "Начинай уже":
            case "Начинай Уже":
            case "н а ч и н а й":
            case "Н а ч и н а й":
            case "пж го":
            case "пжго":
            case "Пж Го":
            case "Пж го":
            case "пж Го":
            case "ПжГо":
            case "Пжго":
            case "пжГо":
            case "ГоПж":
            case "гоПж":
            case "Гопж":
            case "开":
            case "快开":
            case "开始":
            case "开啊":
            case "开阿":
            case "kai":
            case "kaishi":
                return true;
        }

        if (text.Length > 30) return false;

        if (text.Contains("start")) return true;
        if (text.Contains("Start")) return true;
        if (text.Contains("STart")) return true;
        if (text.Contains("s t a r t")) return true;
        if (text.Contains("begin")) return true;
        if (text.Contains("commence")) return true;
        if (text.Contains('了')) return false;
        if (text.Contains('没')) return false;
        if (text.Contains('吗')) return false;
        if (text.Contains('哈')) return false;
        if (text.Contains('还')) return false;
        if (text.Contains('现')) return false;
        if (text.Contains('不')) return false;
        if (text.Contains('可')) return false;
        if (text.Contains('刚')) return false;
        if (text.Contains('的')) return false;
        if (text.Contains('打')) return false;
        if (text.Contains('门')) return false;
        if (text.Contains('关')) return false;
        if (text.Contains('怎')) return false;
        if (text.Contains('要')) return false;
        if (text.Contains('摆')) return false;
        if (text.Contains('啦')) return false;
        if (text.Contains('咯')) return false;
        if (text.Contains('嘞')) return false;
        if (text.Contains('勒')) return false;
        if (text.Contains('心')) return false;
        if (text.Contains('呢')) return false;
        if (text.Contains('门')) return false;
        if (text.Contains('总')) return false;
        if (text.Contains('哥')) return false;
        if (text.Contains('姐')) return false;
        if (text.Contains('《')) return false;
        if (text.Contains('?')) return false;
        if (text.Contains('？')) return false;
        return text.Contains('开') || text.Contains("kai");
    }
}