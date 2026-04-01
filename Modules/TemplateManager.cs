using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using AmongUs.Data;
using InnerNet;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

public static class TemplateManager
{
    private static readonly string TemplateFilePath = $"{Main.DataPath}/EHR_DATA/template.txt";

    private static readonly Regex HeaderRegex = new(
        @"^([a-zA-Z0-9_]+)(?:\[([^\]]*)\])?:\s*(.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PropertyRegex = new(
        @"^\s*(\w+)\s*([><=!]{1,2})?\s*(.*?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PlaceholderRegex = new(
        @"\{\{(\w+)\}\}|@(\w+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex UserVariableRegex = new(
        @"^@(\w+)\s*=\s*(.+)$",
        RegexOptions.Compiled);

    private const int MaxWeight = 10;
    private const int MinDelay = 1;
    private const int MaxDelay = 30;

    private static readonly Dictionary<string, Func<string>> Placeholders = new()
    {
        ["RoomCode"]             = () => GameCode.IntToGameName(AmongUsClient.Instance.GameId),
        ["AmongUsVersion"]       = () => Application.version,
        ["InternalVersion"]      = () => Main.PluginVersion,
        ["ModVersion"]           = () => Main.PluginDisplayVersion,
        ["PlayerName"]           = () => DataManager.Player.Customization.Name,
        ["HostName"]             = () => PlayerControl.LocalPlayer.GetRealName(),
        ["Players"]              = () => string.Join(", ", Main.AllPlayerNames.Values),
        ["AlivePlayers"]         = () => string.Join(", ", Main.EnumerateAlivePlayerControls().Select(x => x.GetRealName())),
        ["DeadPlayers"]          = () => string.Join(", ", Main.EnumeratePlayerControls().Where(x => !x.IsAlive()).Select(x => x.GetRealName())),
        ["PlayerCount"]          = () => (GameData.Instance ? GameData.Instance.PlayerCount : 0).ToString(),
        ["AlivePlayerCount"]     = () => Main.EnumerateAlivePlayerControls().Count().ToString(),
        ["DeadPlayerCount"]      = () => Main.EnumeratePlayerControls().Count(x => !x.IsAlive()).ToString(),
        ["Map"]                  = () => Constants.MapNames[Main.NormalOptions.MapId],
        ["KillCooldown"]         = () => Main.NormalOptions.KillCooldown.ToString(CultureInfo.CurrentCulture),
        ["DiscussionTime"]       = () => Main.NormalOptions.DiscussionTime.ToString(),
        ["VotingTime"]           = () => Main.NormalOptions.VotingTime.ToString(),
        ["EmergencyCooldown"]    = () => Main.NormalOptions.EmergencyCooldown.ToString(),
        ["MeetingCount"]         = () => MeetingStates.MeetingNum.ToString(),
        ["NumEmergencyMeetings"] = () => Main.NormalOptions.NumEmergencyMeetings.ToString(),
        ["PlayerSpeedMod"]       = () => Main.NormalOptions.PlayerSpeedMod.ToString(CultureInfo.CurrentCulture),
        ["CrewLightMod"]         = () => Main.NormalOptions.CrewLightMod.ToString(CultureInfo.CurrentCulture),
        ["ImpostorLightMod"]     = () => Main.NormalOptions.ImpostorLightMod.ToString(CultureInfo.CurrentCulture),
        ["NumCommonTasks"]       = () => Main.NormalOptions.NumCommonTasks.ToString(),
        ["NumLongTasks"]         = () => Main.NormalOptions.NumLongTasks.ToString(),
        ["NumShortTasks"]        = () => Main.NormalOptions.NumShortTasks.ToString(),
        ["GameDuration"]         = () => { if (!GameStates.IsInGame) return "00:00"; int e = (int)(Utils.TimeStamp - IntroCutsceneDestroyPatch.IntroDestroyTS); return $"{e / 60:00}:{e % 60:00}"; },
        ["Date"]                 = () => DateTime.Now.ToShortDateString(),
        ["Time"]                 = () => DateTime.Now.ToShortTimeString(),
        ["Preset"]               = () => GetString($"Preset_{OptionItem.CurrentPreset + 1}"),
    };

    private class TemplateEntry
    {
        public string Tag { get; init; }
        public string Content { get; init; }
        public int Weight { get; init; } = 1;
        public int Delay { get; init; }
        public bool Hidden { get; init; }
        public HashSet<MapNames> AllowedMaps { get; init; }
        public HashSet<string> AllowedRoles { get; init; }
        public HashSet<string> AllowedRanks { get; init; }
        public HashSet<int> AllowedPresets { get; init; }
        public string PlayerCountOp { get; init; }
        public int PlayerCountVal { get; init; }

        public bool MatchesContext() => MatchesMap() && MatchesPlayerCount() && MatchesPreset();

        public bool MatchesRole(PlayerControl player)
        {
            if (AllowedRoles == null || !player) return AllowedRoles == null;

            return AllowedRoles.Any(entry =>
            {
                bool negate = entry.StartsWith("!");
                string name = negate ? entry[1..] : entry;

                bool has = Main.CustomRoleValues.FindFirst(r => string.Equals(GetString(r.ToString()), name, StringComparison.OrdinalIgnoreCase), out CustomRoles role)
                    ? player.Is(role)
                    : Enum.GetValues<CustomRoleTypes>().FindFirst(t => string.Equals(GetString(t.ToString()), name, StringComparison.OrdinalIgnoreCase), out CustomRoleTypes roleType) && player.Is(roleType);

                return negate ? !has : has;
            });
        }

        public bool MatchesRank(PlayerControl player)
        {
            if (AllowedRanks == null || !player) return AllowedRanks == null;

            string fc = player.FriendCode;
            return AllowedRanks.Any(rank => rank.ToLower() switch
            {
                "host"               => player.IsHost(),
                "admin"              => ChatCommands.IsPlayerAdmin(fc),
                "mod" or "moderator" => ChatCommands.IsPlayerModerator(fc),
                "vip"                => ChatCommands.IsPlayerVIP(fc),
                _                    => false
            });
        }

        private bool MatchesMap() => AllowedMaps == null || AllowedMaps.Contains(Main.CurrentMap);

        private bool MatchesPlayerCount()
        {
            if (PlayerCountOp == null) return true;
            int count = GameData.Instance ? GameData.Instance.PlayerCount : 0;
            return PlayerCountOp switch
            {
                ">"         => count > PlayerCountVal,
                ">="        => count >= PlayerCountVal,
                "<"         => count < PlayerCountVal,
                "<="        => count <= PlayerCountVal,
                "=" or "==" => count == PlayerCountVal,
                "!="        => count != PlayerCountVal,
                _           => true
            };
        }
        
        private bool MatchesPreset() =>  AllowedPresets == null || AllowedPresets.Contains(OptionItem.CurrentPreset + 1);    
    }

    public static void Init() => CreateIfNotExists();

    public static void CreateIfNotExists()
    {
        if (File.Exists(TemplateFilePath)) return;

        try
        {
            if (!Directory.Exists($"{Main.DataPath}/EHR_DATA"))
                Directory.CreateDirectory($"{Main.DataPath}/EHR_DATA");

            if (File.Exists($"{Main.DataPath}/template.txt"))
            {
                File.Move($"{Main.DataPath}/template.txt", TemplateFilePath);
            }
            else
            {
                string[] name = CultureInfo.CurrentCulture.Name.Split("-");
                string fileName = name.Length >= 2
                    ? name[0] switch { "zh" => "SChinese", "ru" => "Russian", _ => "English" }
                    : "English";

                Logger.Warn($"Creating new template file: {fileName}", "TemplateManager");
                File.WriteAllText(TemplateFilePath, GetResourcesTxt($"EHR.Resources.Config.template.{fileName}.txt"));
            }
        }
        catch (Exception ex) { Logger.Exception(ex, "TemplateManager"); }
    }

    private static string GetResourcesTxt(string path)
    {
        Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
        if (stream == null) return string.Empty;
        stream.Position = 0;
        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static (List<TemplateEntry> Matched, HashSet<string> Tags, Dictionary<string, string> UserVars) ParseTemplateFile(string filterTag)
    {
        CreateIfNotExists();

        List<TemplateEntry> matched = [];
        HashSet<string> tags = [];
        Dictionary<string, string> userVars = [];
        string currentTag = null;
        Dictionary<string, string> currentProps = null;
        StringBuilder buffer = new();

        using StreamReader sr = new(TemplateFilePath, Encoding.GetEncoding("UTF-8"));
        while (sr.ReadLine() is { } line)
        {
            if (line.TrimStart().StartsWith("#")) continue; // For Comments in Template File

            Match v = UserVariableRegex.Match(line);
            if (v.Success)
            {
                userVars[v.Groups[1].Value] = v.Groups[2].Value.Trim();
                continue;
            }

            Match m = HeaderRegex.Match(line);
            if (m.Success)
            {
                Commit();
                currentTag = m.Groups[1].Value;
                currentProps = ParseProperties(m.Groups[2].Success ? m.Groups[2].Value : null);
                buffer.Clear();
                string inline = m.Groups[3].Value;
                if (!string.IsNullOrEmpty(inline)) buffer.Append(inline);
            }
            else if (currentTag != null)
            {
                if (buffer.Length > 0) buffer.Append('\n');
                buffer.Append(line);
            }
        }

        Commit();
        return (matched, tags, userVars);

        void Commit()
        {
            if (currentTag == null) return;
            TemplateEntry entry = BuildEntry(currentTag, buffer.ToString().TrimEnd(), currentProps);
            if (!entry.Hidden) tags.Add(currentTag);
            if (!string.IsNullOrEmpty(filterTag) && string.Equals(currentTag, filterTag, StringComparison.OrdinalIgnoreCase))
                matched.Add(entry);
        }
    }

    private static Dictionary<string, string> ParseProperties(string propString)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(propString)) return props;

        foreach (string token in propString.Split(','))
        {
            Match m = PropertyRegex.Match(token);
            if (m.Success) props[m.Groups[1].Value] = m.Groups[2].Value + m.Groups[3].Value;
        }

        return props;
    }

    private static TemplateEntry BuildEntry(string tag, string content, Dictionary<string, string> props)
    {
        content = content.Replace("\\n", "\n");

        if (props == null || props.Count == 0)
            return new TemplateEntry { Tag = tag, Content = content };

        int weight = 1;
        int delay = 0;
        bool hidden = props.ContainsKey("hidden");
        HashSet<MapNames> allowedMaps = null;
        HashSet<string> allowedRoles = null;
        HashSet<string> allowedRanks = null;
        HashSet<int> allowedPresets = null;
        string playerCountOp = null;
        int playerCountVal = 0;

        if (props.TryGetValue("weight", out string wStr) && int.TryParse(wStr.TrimStart('='), out int w))
            weight = Math.Clamp(w, 1, MaxWeight);

        if (props.TryGetValue("delay", out string dStr) && float.TryParse(dStr.TrimStart('=').Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float d))
            delay = Math.Clamp((int)d, MinDelay, MaxDelay);

        if (props.TryGetValue("map", out string mapStr))
        {
            allowedMaps = [];
            foreach (string part in mapStr.TrimStart('=').Split('|'))
                if (Enum.TryParse(part.Trim(), ignoreCase: true, out MapNames map))
                    allowedMaps.Add(map);
        }

        if (props.TryGetValue("role", out string roleStr))
            allowedRoles = roleStr.TrimStart('=').Split('|').Select(r => r.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (props.TryGetValue("rank", out string rankStr))
            allowedRanks = rankStr.TrimStart('=').Split('|').Select(r => r.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (props.TryGetValue("players", out string playersStr))
        {
            Match pm = Regex.Match(playersStr, @"^([><=!]{1,2})(\d+)$");
            if (pm.Success)
            {
                playerCountOp = pm.Groups[1].Value;
                int.TryParse(pm.Groups[2].Value, out playerCountVal);
            }
        }

        if (props.TryGetValue("preset", out string presetStr))
        {
            allowedPresets = [];
            foreach (string part in presetStr.TrimStart('=').Split('|'))
                if (int.TryParse(part.Trim(), out int preset))
                    allowedPresets.Add(preset);
        }

        return new TemplateEntry
        {
            Tag = tag,
            Content = content,
            Weight = weight,
            Delay = delay,
            Hidden = hidden,
            AllowedMaps = allowedMaps,
            AllowedRoles = allowedRoles,
            AllowedRanks = allowedRanks,
            AllowedPresets = allowedPresets,
            PlayerCountOp = playerCountOp,
            PlayerCountVal = playerCountVal
        };
    }

    public static HashSet<string> GetAllTags() => ParseTemplateFile("").Tags;

    public static void SendTemplate(string str = "", byte playerId = 0xff, bool noErr = false, MessageImportance importance = MessageImportance.Medium)
    {
        (List<TemplateEntry> allMatched, HashSet<string> tags, Dictionary<string, string> userVars) = ParseTemplateFile(str);

        PlayerControl player = playerId == 0xff
            ? PlayerControl.LocalPlayer
            : Utils.GetPlayerById(playerId);

        if (allMatched.Count == 0)
        {
            if (noErr) return;
            string errMsg = string.Format(GetString(playerId == 0xff ? "Message.TemplateNotFoundHost" : "Message.TemplateNotFoundClient"), str, string.Join(", ", tags));
            if (playerId == 0xff)
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, errMsg);
            else
                Utils.SendMessage(errMsg, playerId, importance: MessageImportance.Low);
            return;
        }

        List<TemplateEntry> eligible = allMatched.FindAll(e => e.MatchesContext());
        if (eligible.Count == 0)
        {
            if (!noErr) Utils.SendMessage(GetString("Message.TemplateConditionsNotMet"), playerId, importance: MessageImportance.Low);
            return;
        }

        eligible = ApplyFilter(eligible, e => e.AllowedRoles != null, e => e.MatchesRole(player),
            out bool roleBlocked, out TemplateEntry roleBlockSource);

        eligible = ApplyFilter(eligible, e => e.AllowedRanks != null, e => e.MatchesRank(player),
            out bool rankBlocked, out TemplateEntry rankBlockSource);

        if (eligible.Count == 0)
        {
            if (noErr) return;

            string msg = (roleBlocked, rankBlocked) switch
            {
                (true, _) => string.Format(GetString("Message.TemplateRoleRequired"), string.Join('/', roleBlockSource.AllowedRoles
                    .Select(r =>
                    {
                        string name = r.TrimStart('!');
                        return Enum.TryParse(name, ignoreCase: true, out CustomRoles role)
                            ? GetString(role.ToString())
                            : name;
                    }))),
                (_, true) => string.Format(GetString("Message.TemplateRankRequired"), string.Join('/', rankBlockSource.AllowedRanks)),
                _ => string.Format(GetString(playerId == 0xff ? "Message.TemplateNotFoundHost" : "Message.TemplateNotFoundClient"), str, string.Join(", ", tags))
            };

            if (playerId == 0xff)
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, msg);
            else
                Utils.SendMessage(msg, playerId, importance: MessageImportance.Low);

            return;
        }

        List<TemplateEntry> immediate = eligible.FindAll(e => e.Delay == 0);
        List<TemplateEntry> delayed = eligible.FindAll(e => e.Delay > 0);

        if (immediate.Count > 0)
        {
            bool hasWeights = immediate.Exists(e => e.Weight != 1);

            if (hasWeights)
            {
                string content = ApplyPlaceholders(WeightedPick(immediate).Content, userVars);
                Dispatch(content, playerId, importance);
            }
            else
            {
                foreach (TemplateEntry entry in immediate)
                    Dispatch(ApplyPlaceholders(entry.Content, userVars), playerId, importance);
            }
        }

        foreach (TemplateEntry entry in delayed)
        {
            string content = ApplyPlaceholders(entry.Content, userVars);
            LateTask.New(() => Dispatch(content, playerId, importance), entry.Delay, log: false);
        }
    }

    private static List<TemplateEntry> ApplyFilter(
        List<TemplateEntry> eligible,
        Predicate<TemplateEntry> hasCondition,
        Predicate<TemplateEntry> meetsCondition,
        out bool blocked,
        out TemplateEntry blockSource)
    {
        blocked = false;
        blockSource = null;

        List<TemplateEntry> restricted = [.. eligible.FindAll(hasCondition)];
        if (restricted.Count == 0) return eligible;

        List<TemplateEntry> unrestricted = [.. eligible.FindAll(e => !hasCondition(e))];
        List<TemplateEntry> passing = [.. restricted.FindAll(meetsCondition)];

        if (passing.Count > 0) return passing;
        if (unrestricted.Count > 0) return unrestricted;

        blocked = true;
        blockSource = restricted[0];
        return [];
    }

    private static void Dispatch(string content, byte playerId, MessageImportance importance) =>
        new List<Message> { new(content, playerId) }.SendMultipleMessages(importance);

    private static TemplateEntry WeightedPick(List<TemplateEntry> entries)
    {
        int total = entries.Sum(e => e.Weight);
        int roll = IRandom.Instance.Next(total);
        int running = 0;

        foreach (TemplateEntry entry in entries)
        {
            running += entry.Weight;
            if (roll < running) return entry;
        }

        return entries[^1];
    }

    private static string ApplyPlaceholders(string text, Dictionary<string, string> userVars) =>
        PlaceholderRegex.Replace(text, match =>
        {
            if (match.Groups[1].Success)
                return Placeholders.TryGetValue(match.Groups[1].Value, out Func<string> getValue)
                    ? getValue() ?? ""
                    : match.Value;

            if (!userVars.TryGetValue(match.Groups[2].Value, out string val))
                return match.Value;

            return PlaceholderRegex.Replace(val, inner =>
                inner.Groups[1].Success && Placeholders.TryGetValue(inner.Groups[1].Value, out Func<string> getValue)
                    ? getValue() ?? ""
                    : inner.Value);
        });
}
