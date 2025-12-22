using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;

namespace EHR.Modules;

internal static class CustomTeamManager
{
    public static HashSet<CustomTeam> CustomTeams = [];
    public static HashSet<CustomTeam> EnabledCustomTeams = [];
    public static CustomTeam WinnerTeam;
    public static Dictionary<CustomTeam, HashSet<byte>> CustomTeamPlayerIds = [];
    public static List<CustomTeamOptionGroup> CustomTeamOptions = [];

    public static void LoadCustomTeams()
    {
        try
        {
            if (!File.Exists($"{Main.DataPath}/EHR_DATA/CTA_Data.txt")) return;

            CustomTeams = File.ReadAllLines($"{Main.DataPath}/EHR_DATA/CTA_Data.txt").Select(x => new CustomTeam(x)).ToHashSet();
            RefreshCustomOptions();
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    private static void RefreshCustomOptions()
    {
        CustomTeamOptions.Clear();
        EnabledCustomTeams.Clear();

        const int startId = 660000;
        const TabGroup tab = TabGroup.GameSettings;
        CustomTeamOptions = CustomTeams.Select((x, i) => CreateSetting(x, startId + (20 * i))).ToList();
        UpdateEnabledTeams();

        return;

        static CustomTeamOptionGroup CreateSetting(CustomTeam team, int id)
        {
            var enabled = new BooleanOptionItem(id++, "CTA.FLAG" + team.TeamName, true, tab);
            var knowRoles = new BooleanOptionItem(id++, "CTA.KnowRoles", true, tab);
            var winWithOriginalTeam = new BooleanOptionItem(id++, "CTA.WinWithOriginalTeam", false, tab);
            var originalWinCondition = new BooleanOptionItem(id++, "CTA.OriginalWinCondition", true, tab);
            var killEachOther = new BooleanOptionItem(id++, "CTA.KillEachOther", false, tab);
            var guessEachOther = new BooleanOptionItem(id++, "CTA.GuessEachOther", false, tab);
            var arrows = new BooleanOptionItem(id++, "CTA.Arrows", true, tab);
            var maxPlayers = new IntegerOptionItem(id++, "CTA.MaxPlayersAssignedToTeam", new(0, 15, 1), 15, tab);

            var teamPlayerCounts = new Dictionary<Team, IntegerOptionItem[]>();

            foreach (Team teamType in Enum.GetValues<Team>()[1..])
            {
                var min = new IntegerOptionItem(id++, "CTA.MinPlayers." + teamType, new(0, 15, 1), 1, tab);
                var max = new IntegerOptionItem(id++, "CTA.MaxPlayers." + teamType, new(0, 15, 1), 15, tab);
                teamPlayerCounts[teamType] = [min, max];
            }

            CustomTeamOptionGroup group = new(team, enabled, knowRoles, winWithOriginalTeam, originalWinCondition, killEachOther, guessEachOther, arrows, maxPlayers, teamPlayerCounts);
            group.AllOptions.Skip(1).Do(x => x.SetParent(enabled));
            group.AllOptions.ForEach(x => x.SetColor(new Color32(215, 227, 84, byte.MaxValue)));
            group.AllOptions.ForEach(x => x.SetGameMode(CustomGameMode.Standard));
            if (ColorUtility.TryParseHtmlString(team.RoleRevealScreenBackgroundColor, out Color color)) enabled.SetColor(color);

            enabled.RegisterUpdateValueEvent((_, _, _) => UpdateEnabledTeams());
            return group;
        }
    }

    private static void UpdateEnabledTeams()
    {
        EnabledCustomTeams = CustomTeamOptions.Where(x => x.Enabled.GetBool()).Select(x => x.Team).ToHashSet();
    }

    public static void InitializeCustomTeamPlayers()
    {
        UpdateEnabledTeams();
        if (EnabledCustomTeams.Count == 0) return;

        PlayerControl[] aapc = Main.AllAlivePlayerControls;
        
        CustomTeamPlayerIds = Main.PlayerStates
            .IntersectBy(aapc.Select(x => x.PlayerId), x => x.Key)
            .GroupBy(x => EnabledCustomTeams.FirstOrDefault(t => t.TeamMembers.Contains(x.Value.MainRole)), x => x.Key)
            .Where(x => x.Key != null)
            .ToDictionary(x => x.Key, x => TakeAsManyAsSet(x, x.Key));

        foreach ((CustomTeam team, HashSet<byte> players) in CustomTeamPlayerIds)
        {
            if (!IsSettingEnabledForTeam(team, CTAOption.WinWithOriginalTeam))
            {
                foreach (byte id in players)
                {
                    if (Main.PlayerStates.TryGetValue(id, out PlayerState ps))
                        ps.countTypes = CountTypes.CustomTeam;
                }
            }

            if (!IsSettingEnabledForTeam(team, CTAOption.Arrows)) continue;

            foreach (byte player in players)
            {
                foreach (byte target in players)
                {
                    if (player == target) continue;
                    TargetArrow.Add(player, target);
                }
            }
        }

        var imps = aapc.Where(x => x.IsImpostor() && !CustomTeamPlayerIds.Values.Any(l => l.Contains(x.PlayerId))).ToList();

        if (imps.Count > 0)
        {
            CustomTeamPlayerIds.Values.Flatten().ToValidPlayers().DoIf(x => x != null && x.IsImpostor() && !IsSettingEnabledForPlayerTeam(x.PlayerId, CTAOption.WinWithOriginalTeam), ctp =>
            {
                imps.ForEach(imp => ctp.RpcSetRoleDesync(RoleTypes.Crewmate, imp.OwnerId, setRoleMap: true));
                var sender = CustomRpcSender.Create("CustomTeamManager_SetImpostorDesync", SendOption.Reliable);
                imps.ForEach(imp => sender.RpcSetRole(imp, RoleTypes.Crewmate, ctp.OwnerId, changeRoleMap: true));
                sender.SendMessage();
            });
        }

        return;

        static HashSet<byte> TakeAsManyAsSet(IEnumerable<byte> playerIds, CustomTeam customTeam)
        {
            try
            {
                CustomTeamOptionGroup options = CustomTeamOptions.First(x => x.Team.Equals(customTeam));
                Dictionary<Team, List<PlayerControl>> grouped = playerIds.ToValidPlayers().GroupBy(x => x.GetTeam()).ToDictionary(x => x.Key, x => x.Shuffle());

                foreach ((Team team, List<PlayerControl> players) in grouped)
                {
                    try
                    {
                        if (!options.TeamPlayerCounts.TryGetValue(team, out IntegerOptionItem[] counts) || counts.Length < 2)
                            continue;

                        int num = IRandom.Instance.Next(counts[0].GetInt(), counts[1].GetInt() + 1);
                        if (players.Count <= num) continue;

                        List<PlayerControl> selected = players.Take(num).Shuffle();
                        players.Clear();
                        players.AddRange(selected);
                    }
                    catch (Exception e) { Utils.ThrowException(e); }
                }

                return grouped.Values.Flatten().Select(x => x.PlayerId).Take(options.MaxPlayers.GetInt()).ToHashSet();
            }
            catch (Exception e)
            {
                Utils.ThrowException(e);
                return [];
            }
        }
    }

    public static string GetSuffix(PlayerControl seer)
    {
        if (seer == null || EnabledCustomTeams.Count == 0 || Main.HasJustStarted || !IsSettingEnabledForPlayerTeam(seer.PlayerId, CTAOption.Arrows)) return string.Empty;
        return CustomTeamPlayerIds[GetCustomTeam(seer.PlayerId)].Aggregate(string.Empty, (s, id) => s + Utils.ColorString(Main.PlayerColors.GetValueOrDefault(id, Color.white), TargetArrow.GetArrows(seer, id)));
    }

    public static bool CheckCustomTeamGameEnd()
    {
        if (EnabledCustomTeams.Count == 0 || CustomTeamPlayerIds.Count == 0) return false;

        try
        {
            PlayerControl[] aapc = Main.AllAlivePlayerControls;

            if (aapc.Length == 1)
            {
                PlayerControl lastPlayer = aapc[0];
                CustomTeam lastTeam = GetCustomTeam(lastPlayer.PlayerId);
                WinnerTeam = lastTeam;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.CustomTeam);
                CustomWinnerHolder.WinnerIds = [lastPlayer.PlayerId];
                return true;
            }
            
            CustomTeamPlayerIds.Do(x => x.Value.RemoveWhere(p =>
            {
                PlayerControl pc = Utils.GetPlayerById(p);
                return pc == null || pc.Data == null || pc.Data.Disconnected;
            }));

            Dictionary<CustomTeam, HashSet<byte>> aliveTeamPlayers = CustomTeamPlayerIds.ToDictionary(x => x.Key, x => x.Value);
            aliveTeamPlayers.Do(x => x.Value.RemoveWhere(p => !Utils.GetPlayerById(p).IsAlive()));

            List<CustomTeam> toRemove = aliveTeamPlayers.Where(x => x.Value.Count == 0).Select(x => x.Key).ToList();
            toRemove.ForEach(x => aliveTeamPlayers.Remove(x));

            CustomTeam team = aliveTeamPlayers.Keys.FirstOrDefault();

            if (aliveTeamPlayers.Count == 1 && aapc.All(x =>
            {
                CustomTeam customTeam = GetCustomTeam(x.PlayerId);
                return customTeam != null && customTeam.Equals(team);
            }))
            {
                WinnerTeam = team;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.CustomTeam);
                CustomWinnerHolder.WinnerIds = aliveTeamPlayers.Values.First();
                return true;
            }
        }
        catch (Exception e) { Logger.Error($"Error in CheckCustomTeamGameEnd: {e}", "CustomTeamManager"); }

        return false;
    }

    public static CustomTeam GetCustomTeam(byte id)
    {
        foreach ((CustomTeam team, HashSet<byte> ids) in CustomTeamPlayerIds)
        {
            if (ids.Contains(id))
                return team;
        }

        return null;
    }

    public static bool ArentInCustomTeam(params List<byte> ids)
    {
        if (EnabledCustomTeams.Count == 0 || CustomTeamPlayerIds.Count == 0) return true;
        return ids.TrueForAll(x => GetCustomTeam(x) == null);
    }

    public static bool AreInSameCustomTeam(byte id1, byte id2)
    {
        if (EnabledCustomTeams.Count == 0) return false;

        CustomTeam team1 = GetCustomTeam(id1);
        CustomTeam team2 = GetCustomTeam(id2);

        return team1 != null && team2 != null && team1.Equals(team2);
    }

    public static bool IsSettingEnabledForPlayerTeam(byte id, CTAOption setting)
    {
        CustomTeam team = GetCustomTeam(id);
        return team != null && IsSettingEnabledForTeam(team, setting);
    }

    public static bool IsSettingEnabledForTeam(CustomTeam team, CTAOption setting)
    {
        CustomTeamOptionGroup optionsGroup = CustomTeamOptions.First(x => x.Team.Equals(team));
        List<bool> values = optionsGroup.AllOptions.ConvertAll(x => x.GetBool());
        return values[(int)setting];
    }

    internal class CustomTeam
    {
        public readonly string RoleRevealScreenBackgroundColor;
        public readonly string RoleRevealScreenSubtitle;
        public readonly string RoleRevealScreenTitle;
        public readonly string TeamName;

        public CustomTeam(string line)
        {
            try
            {
                string[] parts = line.Split(';');

                TeamName = parts[0];
                RoleRevealScreenTitle = parts[1];
                RoleRevealScreenSubtitle = parts[2];
                RoleRevealScreenBackgroundColor = parts[3];

                TeamMembers = parts[4].Split(',').Select(x => Enum.Parse<CustomRoles>(x, true)).ToList();
            }
            catch (Exception e) { Utils.ThrowException(e); }
            finally { CustomTeams.Add(this); }
        }

        public List<CustomRoles> TeamMembers { get; } = [];

        public override bool Equals(object obj)
        {
            if (obj is not CustomTeam team) return false;

            return TeamName == team.TeamName;
        }

        public override int GetHashCode()
        {
            return TeamName.GetHashCode();
        }
    }

    internal class CustomTeamOptionGroup
    {
        public readonly List<OptionItem> AllOptions;

        public CustomTeamOptionGroup(CustomTeam team, BooleanOptionItem enabled, BooleanOptionItem knowRoles, BooleanOptionItem winWithOriginalTeam, BooleanOptionItem originalWinCondition, BooleanOptionItem killEachOther, BooleanOptionItem guessEachOther, BooleanOptionItem arrows, IntegerOptionItem maxPlayers, Dictionary<Team, IntegerOptionItem[]> teamPlayerCounts)
        {
            AllOptions = [enabled, knowRoles, winWithOriginalTeam, originalWinCondition, killEachOther, guessEachOther, arrows, maxPlayers];
            AllOptions.AddRange(teamPlayerCounts.Values.Flatten());
            Team = team;
            Enabled = enabled;
            MaxPlayers = maxPlayers;
            TeamPlayerCounts = teamPlayerCounts;
        }

        public CustomTeam Team { get; }

        public BooleanOptionItem Enabled { get; }
        public IntegerOptionItem MaxPlayers { get; }
        public Dictionary<Team, IntegerOptionItem[]> TeamPlayerCounts { get; }
    }
}

public enum CTAOption
{
    KnowRoles = 1,
    WinWithOriginalTeam,
    OriginalWinCondition,
    KillEachOther,
    GuessEachOther,
    Arrows
}