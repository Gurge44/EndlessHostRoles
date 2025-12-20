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
using UnityEngine;
using UnityEngine.Networking;
using static EHR.Translator;

namespace EHR;

public static class BanManager
{
    private static readonly string DenyNameListPath = $"{Main.DataPath}/EHR_DATA/DenyName.txt";
    private static readonly string BanListPath = $"{Main.DataPath}/EHR_DATA/BanList.txt";
    private static readonly string ModeratorListPath = $"{Main.DataPath}/EHR_DATA/Moderators.txt";
    private static readonly string WhiteListListPath = $"{Main.DataPath}/EHR_DATA/WhiteList.txt";
    private static readonly List<string> EACList = [];
    public static readonly List<string> TempBanWhiteList = []; // To prevent writing to the banlist

    public static void Init()
    {
        try
        {
            if (!Directory.Exists($"{Main.DataPath}/EHR_DATA")) Directory.CreateDirectory($"{Main.DataPath}/EHR_DATA");

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
            
            Main.Instance.StartCoroutine(LoadEACList());

            static System.Collections.IEnumerator LoadEACList()
            {
                EACList.Clear();

                UnityWebRequest request = UnityWebRequest.Get("https://raw.githubusercontent.com/Gurge44/EndlessHostRoles/main/Resources/Config/EACList.txt");
                request.timeout = 5;
                request.SetRequestHeader("User-Agent", $"{Main.ModName} v{Main.PluginVersion}");
                
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success && !string.IsNullOrWhiteSpace(request.downloadHandler.text))
                {
                    using StringReader reader = new(request.downloadHandler.text);

                    while (reader.ReadLine() is { } line)
                    {
                        line = line.Trim();
                        if (line.Length == 0 || line.StartsWith("#")) continue;

                        EACList.Add(line);
                    }

                    Logger.Info("EAC list loaded from GitHub", "BanManager");
                    yield break;
                }

                Logger.Warn($"Failed to load EAC list from GitHub, falling back to local copy: {request.error}", "BanManager");

                // Fallback: embedded resource
                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EHR.Resources.Config.EACList.txt")!;
                stream.Position = 0;
                using StreamReader sr = new(stream, Encoding.UTF8);

                while (sr.ReadLine() is { } line)
                {
                    line = line.Trim();
                    if (line == "" || line.StartsWith("#")) continue;

                    EACList.Add(line);
                }

                Logger.Info("EAC list loaded from embedded resource", "BanManager");
            }
        }
        catch (Exception ex) { Logger.Exception(ex, "BanManager"); }
    }

    private static string GetResourcesTxt(string path)
    {
        Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path)!;
        stream.Position = 0;
        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static string GetHashedPuid(this ClientData player)
    {
        if (player == null) return string.Empty;

        string puid = player.ProductUserId;
        using var sha256 = SHA256.Create();
        // get sha-256 hash
        byte[] sha256Bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(puid));
        string sha256Hash = BitConverter.ToString(sha256Bytes).Replace("-", "").ToLower();

        // pick front 5 and last 4
        return string.Concat(sha256Hash.AsSpan(0, 5), sha256Hash.AsSpan(sha256Hash.Length - 4));
    }

    public static void AddBanPlayer(ClientData player)
    {
        if (!AmongUsClient.Instance.AmHost || player == null) return;

        string friendCode = player.FriendCode.Replace(':', '#');
        string hashedPuid = player.GetHashedPuid();

        if (!CheckBanList(friendCode, hashedPuid) && !TempBanWhiteList.Contains(hashedPuid))
        {
            if (!string.IsNullOrWhiteSpace(hashedPuid))
            {
                File.AppendAllText(BanListPath, $"{friendCode},{hashedPuid},{player.PlayerName.RemoveHtmlTags()}\n");
                Logger.SendInGame(string.Format(GetString("Message.AddedPlayerToBanList"), player.PlayerName), Color.yellow);
            }
            else
                Logger.Info($"Failed to add player {player.PlayerName.RemoveHtmlTags()}/{friendCode}/{hashedPuid} to ban list!", "AddBanPlayer");
        }
    }

    public static bool CheckDenyNamePlayer(PlayerControl player, string name)
    {
        if (!AmongUsClient.Instance.AmHost || !Options.ApplyDenyNameList.GetBool()) return false;

        try
        {
            if (!Directory.Exists($"{Main.DataPath}/EHR_DATA")) Directory.CreateDirectory($"{Main.DataPath}/EHR_DATA");
            if (!File.Exists(DenyNameListPath)) File.Create(DenyNameListPath).Close();

            using StreamReader sr = new(DenyNameListPath);

            while (sr.ReadLine() is { } line)
            {
                if (line == "") continue;

                if (Regex.IsMatch(name, line))
                {
                    AmongUsClient.Instance.KickPlayer(player.OwnerId, false);
                    Logger.SendInGame(string.Format(GetString("Message.KickedByDenyName"), name, line), Color.yellow);
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
        if (!AmongUsClient.Instance.AmHost || !Options.ApplyBanList.GetBool() || player == null) return;

        if (TempBanWhiteList.Contains(player.GetHashedPuid()))
        {
            AmongUsClient.Instance.KickPlayer(player.Id, false);
            Logger.Info($"{player.PlayerName} was in temp ban list", "BAN");
        }

        if (GameStates.CurrentServerType is GameStates.ServerType.Modded or GameStates.ServerType.Niko or GameStates.ServerType.Local) return;

        string friendcode = player.FriendCode.Replace(':', '#');

        if (friendcode.Length < 7) // #1234 is 5 chars, and it's impossible for a friend code to only have 3
        {
            AmongUsClient.Instance.KickPlayer(player.Id, false);
            Logger.SendInGame(string.Format(GetString("Message.InvalidFriendCode"), player.PlayerName), Color.yellow);
            Logger.Info($"{player.PlayerName} banned by EAC because their friend code is too short.", "EAC");
            return;
        }

        if (friendcode.Count(c => c == '#') != 1)
        {
            AmongUsClient.Instance.KickPlayer(player.Id, false);
            Logger.SendInGame(string.Format(GetString("Message.InvalidFriendCode"), player.PlayerName), Color.yellow);
            Logger.Info($"{player.PlayerName} EAC Banned because friendcode contains more than 1 #", "EAC");
            return;
        }

        // Contains any non-word character or digits
        const string pattern = @"[\W\d]";

        if (Regex.IsMatch(friendcode[..friendcode.IndexOf("#", StringComparison.Ordinal)], pattern))
        {
            AmongUsClient.Instance.KickPlayer(player.Id, true);
            Logger.SendInGame(string.Format(GetString("Message.InvalidFriendCode"), player.PlayerName), Color.yellow);
            Logger.Info($"{player.PlayerName} was banned because of a spoofed friend code", "EAC");
            return;
        }

        if (CheckBanList(friendcode, player.GetHashedPuid()))
        {
            AmongUsClient.Instance.KickPlayer(player.Id, true);
            Logger.SendInGame(string.Format(GetString("Message.BanedByBanList"), player.PlayerName), Color.yellow);
            Logger.Info($"{player.PlayerName} is banned because he has been banned in the past.", "BAN");
            return;
        }

        if (CheckEACList(friendcode, player.GetHashedPuid()))
        {
            AmongUsClient.Instance.KickPlayer(player.Id, true);
            Logger.SendInGame(string.Format(GetString("Message.BanedByEACList"), player.PlayerName), Color.yellow);
            Logger.Info($"{player.PlayerName} is on the EAC ban list", "BAN");
        }
    }

    public static bool CheckBanList(string code, string hashedpuid = "")
    {
        code = code.Replace(':', '#');

        var onlyCheckPuid = false;

        switch (code)
        {
            case "" when hashedpuid != "":
                onlyCheckPuid = true;
                break;
            case "":
                return false;
        }

        try
        {
            if (!Directory.Exists($"{Main.DataPath}/EHR_DATA")) Directory.CreateDirectory($"{Main.DataPath}/EHR_DATA");
            if (!File.Exists(BanListPath)) File.Create(BanListPath).Close();

            using StreamReader sr = new(BanListPath);

            while (sr.ReadLine() is { } line)
            {
                if (line == "") continue;

                if (!onlyCheckPuid)
                {
                    if (line.Contains(code))
                        return true;
                }

                if (line.Contains(hashedpuid)) return true;
            }
        }
        catch (Exception ex) { Logger.Exception(ex, "CheckBanList"); }

        return false;
    }

    public static bool CheckEACList(string code, string hashedPuid)
    {
        code = code.Replace(':', '#');

        var onlyCheckPuid = false;

        switch (code)
        {
            case "" when hashedPuid == "":
                onlyCheckPuid = true;
                break;
            case "":
                return false;
        }

        return EACList.Any(x => x.Contains(code) && !onlyCheckPuid) || EACList.Any(x => x.Contains(hashedPuid) && hashedPuid != "");
    }
}

[HarmonyPatch(typeof(BanMenu), nameof(BanMenu.Select))]
internal class BanMenuSelectPatch
{
    public static void Postfix(BanMenu __instance, int clientId)
    {
        ClientData recentClient = AmongUsClient.Instance.GetRecentClient(clientId);
        if (recentClient == null) return;

        if (!BanManager.CheckBanList(recentClient.FriendCode, recentClient.GetHashedPuid()))
            __instance.BanButton.GetComponent<ButtonRolloverHandler>().SetEnabledColors();
    }
}