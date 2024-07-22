using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using InnerNet;
using static EHR.Translator;

namespace EHR;

public static class BanManager
{
    private const string DenyNameListPath = "./EHR_DATA/DenyName.txt";
    private const string BanListPath = "./EHR_DATA/BanList.txt";
    private const string ModeratorListPath = "./EHR_DATA/Moderators.txt";
    private const string WhiteListListPath = "./EHR_DATA/WhiteList.txt";
#pragma warning disable IDE0044 // Add readonly modifier
    private static List<string> EACList = []; // Don't make it read-only
#pragma warning restore IDE0044 // Add readonly modifier
    public static List<string> TempBanWhiteList = []; //To prevent writing to ban list

    public static void Init()
    {
        try
        {
            Directory.CreateDirectory("EHR_DATA");

            if (!File.Exists(BanListPath))
            {
                Logger.Warn("Create a new BanList.txt file", "BanManager");
                File.Create(BanListPath).Close();
            }

            if (!File.Exists(DenyNameListPath))
            {
                Logger.Warn("Create a new DenyName.txt file", "BanManager");
                File.Create(DenyNameListPath).Close();
                File.WriteAllText(DenyNameListPath, GetResourcesTxt("EHR.Resources.Config.DenyName.txt"));
            }

            if (!File.Exists(ModeratorListPath))
            {
                Logger.Warn("Creating a new Moderators.txt file", "BanManager");
                File.Create(ModeratorListPath).Close();
            }

            if (!File.Exists(WhiteListListPath))
            {
                Logger.Warn("Creating a new WhiteList.txt file", "BanManager");
                File.Create(WhiteListListPath).Close();
            }

            // Read EAC List
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EHR.Resources.Config.EACList.txt");
            stream.Position = 0;
            using StreamReader sr = new(stream, Encoding.UTF8);
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                if (line == "" || line.StartsWith("#")) continue;
                EACList.Add(line);
            }
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "BanManager");
        }
    }

    private static string GetResourcesTxt(string path)
    {
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
        stream.Position = 0;
        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static string GetHashedPuid(this ClientData player)
    {
        if (player == null) return string.Empty;
        string puid = player.ProductUserId;
        using SHA256 sha256 = SHA256.Create();
        // get sha-256 hash
        byte[] sha256Bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(puid));
        string sha256Hash = BitConverter.ToString(sha256Bytes).Replace("-", "").ToLower();

        // pick front 5 and last 4
        return string.Concat(sha256Hash.AsSpan(0, 5), sha256Hash.AsSpan(sha256Hash.Length - 4));
    }

    public static void AddBanPlayer(ClientData player)
    {
        if (!AmongUsClient.Instance.AmHost || player == null) return;
        if (!CheckBanList(player.FriendCode, player.GetHashedPuid()) && !TempBanWhiteList.Contains(player.GetHashedPuid()))
        {
            if (player.GetHashedPuid() != "" && player.GetHashedPuid() != null && player.GetHashedPuid() != "e3b0cb855")
            {
                File.AppendAllText(BanListPath, $"{player.FriendCode},{player.GetHashedPuid()},{player.PlayerName.RemoveHtmlTags()}\n");
                Logger.SendInGame(string.Format(GetString("Message.AddedPlayerToBanList"), player.PlayerName));
            }
            else Logger.Info($"Failed to add player {player.PlayerName.RemoveHtmlTags()}/{player.FriendCode}/{player.GetHashedPuid()} to ban list!", "AddBanPlayer");
        }
    }

    public static bool CheckDenyNamePlayer(PlayerControl player, string name)
    {
        if (!AmongUsClient.Instance.AmHost || !Options.ApplyDenyNameList.GetBool()) return false;

        try
        {
            Directory.CreateDirectory("TOHE-DATA");
            if (!File.Exists(DenyNameListPath)) File.Create(DenyNameListPath).Close();
            using StreamReader sr = new(DenyNameListPath);
            while (sr.ReadLine() is { } line)
            {
                if (line == "") continue;
                if (line.Contains("Amogus") || line.Contains("Amogus V") || Regex.IsMatch(name, line))
                {
                    AmongUsClient.Instance.KickPlayer(player.OwnerId, false);
                    Logger.SendInGame(string.Format(GetString("Message.KickedByDenyName"), name, line));
                    Logger.Info($"{name} was kicked because their name matched \"{line}\".", "Kick");
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "CheckDenyNamePlayer");
            return true;
        }
    }

    public static void CheckBanPlayer(ClientData player)
    {
        if (!AmongUsClient.Instance.AmHost || !Options.ApplyBanList.GetBool()) return;

        string friendcode = player?.FriendCode;
        if (friendcode?.Length < 7) // #1234 is 5 chars, and it's impossible for a friend code to only have 3
        {
            AmongUsClient.Instance.KickPlayer(player.Id, true);
            Logger.SendInGame(string.Format(GetString("Message.BannedByEACList"), player.PlayerName));
            Logger.Info($"{player.PlayerName} banned by EAC because their friend code is too short.", "EAC");
            return;
        }

        if (friendcode?.Count(c => c == '#') != 1)
        {
            // This is part of eac, so that's why it will say banned by EAC list.
            AmongUsClient.Instance.KickPlayer(player.Id, true);
            Logger.SendInGame(string.Format(GetString("Message.BannedByEACList"), player.PlayerName));
            Logger.Info($"{player.PlayerName} EAC Banned bc friendcode contains more than 1 #", "EAC");
            return;
        }

        // Contains any non-word character or digits
        const string pattern = @"[\W\d]";
        if (Regex.IsMatch(friendcode[..friendcode.IndexOf("#", StringComparison.Ordinal)], pattern))
        {
            AmongUsClient.Instance.KickPlayer(player.Id, true);
            Logger.SendInGame(string.Format(GetString("Message.BannedByEACList"), player.PlayerName));
            Logger.Info($"{player.PlayerName} was banned because of a spoofed friend code", "EAC");
            return;
        }

        if (CheckBanList(player?.FriendCode, player?.GetHashedPuid()))
        {
            AmongUsClient.Instance.KickPlayer(player.Id, true);
            Logger.SendInGame(string.Format(GetString("Message.BanedByBanList"), player.PlayerName));
            Logger.Info($"{player.PlayerName} is banned because he has been banned in the past.", "BAN");
            return;
        }

        if (CheckEACList(player?.FriendCode, player?.GetHashedPuid()))
        {
            AmongUsClient.Instance.KickPlayer(player.Id, true);
            Logger.SendInGame(string.Format(GetString("Message.BanedByEACList"), player.PlayerName));
            Logger.Info($"{player.PlayerName} is on the EAC ban list", "BAN");
            return;
        }

        if (TempBanWhiteList.Contains(player?.GetHashedPuid()))
        {
            AmongUsClient.Instance.KickPlayer(player.Id, true);
            Logger.Info($"{player.PlayerName} was in temp ban list", "BAN");
        }
    }

    public static bool CheckBanList(string code, string hashedpuid = "")
    {
        bool OnlyCheckPuid = false;
        if (code == "" && hashedpuid != "") OnlyCheckPuid = true;
        else if (code == "") return false;
        try
        {
            Directory.CreateDirectory("EHR_DATA");
            if (!File.Exists(BanListPath)) File.Create(BanListPath).Close();
            using StreamReader sr = new(BanListPath);
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                if (line == "") continue;
                if (!OnlyCheckPuid)
                    if (line.Contains(code))
                        return true;
                if (line.Contains(hashedpuid)) return true;
            }
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "CheckBanList");
        }

        return false;
    }

    public static bool CheckEACList(string code, string hashedPuid)
    {
        bool OnlyCheckPuid = false;
        switch (code)
        {
            case "" when hashedPuid == "":
                OnlyCheckPuid = true;
                break;
            case "":
                return false;
        }

        return EACList.Any(x => x.Contains(code) && !OnlyCheckPuid) || EACList.Any(x => x.Contains(hashedPuid) && hashedPuid != "");
    }
}

[HarmonyPatch(typeof(BanMenu), nameof(BanMenu.Select))]
class BanMenuSelectPatch
{
    public static void Postfix(BanMenu __instance, int clientId)
    {
        ClientData recentClient = AmongUsClient.Instance.GetRecentClient(clientId);
        if (recentClient == null) return;
        if (!BanManager.CheckBanList(recentClient.FriendCode, recentClient.GetHashedPuid()))
            __instance.BanButton.GetComponent<ButtonRolloverHandler>().SetEnabledColors();
    }
}