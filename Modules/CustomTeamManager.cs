using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace EHR.Modules
{
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
                if (!File.Exists("./EHR_DATA/CTA_Data.txt")) return;
                CustomTeams = File.ReadAllLines("./EHR_DATA/CTA_Data.txt").Select(x => new CustomTeam(x)).ToHashSet();
                RefreshCustomOptions();
            }
            catch (Exception e)
            {
                Utils.ThrowException(e);
            }
        }

        private static void RefreshCustomOptions()
        {
            CustomTeamOptions.Clear();
            EnabledCustomTeams.Clear();

            const int startId = 647000;
            const TabGroup tab = TabGroup.GameSettings;
            CustomTeamOptions = CustomTeams.Select((x, i) => CreateSetting(x, startId + (6 * i))).ToList();
            UpdateEnabledTeams();

            return;

            static CustomTeamOptionGroup CreateSetting(CustomTeam team, int id)
            {
                var enabled = new BooleanOptionItem(id++, "CTA.FLAG" + team.TeamName, true, tab);
                var knowRoles = new BooleanOptionItem(id++, "CTA.KnowRoles", true, tab);
                var winWithOriginalTeam = new BooleanOptionItem(id++, "CTA.WinWithOriginalTeam", false, tab);
                var killEachOther = new BooleanOptionItem(id++, "CTA.KillEachOther", false, tab);
                var guessEachOther = new BooleanOptionItem(id++, "CTA.GuessEachOther", false, tab);
                var arrows = new BooleanOptionItem(id, "CTA.Arrows", true, tab);

                CustomTeamOptionGroup group = new(team, enabled, knowRoles, winWithOriginalTeam, killEachOther, guessEachOther, arrows);
                group.AllOptions.Skip(1).Do(x => x.SetParent(enabled));
                group.AllOptions.ForEach(x => x.SetColor(new Color32(215, 227, 84, byte.MaxValue)));
                group.AllOptions.ForEach(x => x.SetGameMode(CustomGameMode.Standard));
                if (ColorUtility.TryParseHtmlString(team.RoleRevealScreenBackgroundColor, out var color)) enabled.SetColor(color);
                enabled.RegisterUpdateValueEvent((_, _) => UpdateEnabledTeams());
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

            CustomTeamPlayerIds = Main.PlayerStates
                .IntersectBy(Main.AllAlivePlayerControls.Select(x => x.PlayerId), x => x.Key)
                .GroupBy(x => EnabledCustomTeams.FirstOrDefault(t => t.TeamMembers.Contains(x.Value.MainRole)), x => x.Key)
                .Where(x => x.Key != null)
                .ToDictionary(x => x.Key, x => x.ToHashSet());

            foreach ((CustomTeam team, HashSet<byte> players) in CustomTeamPlayerIds)
            {
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
                CustomTeamPlayerIds.Do(x => x.Value.RemoveWhere(p =>
                {
                    var pc = Utils.GetPlayerById(p);
                    return pc == null || pc.Data.Disconnected;
                }));

                var aliveTeamPlayers = CustomTeamPlayerIds.ToDictionary(x => x.Key, x => x.Value);
                aliveTeamPlayers.Do(x => x.Value.RemoveWhere(p => !Utils.GetPlayerById(p).IsAlive()));

                var team = aliveTeamPlayers.Keys.First();
                if (aliveTeamPlayers.Count == 1 && Main.AllAlivePlayerControls.All(x =>
                    {
                        var customTeam = GetCustomTeam(x.PlayerId);
                        return customTeam != null && customTeam.Equals(team);
                    }))
                {
                    WinnerTeam = team;
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.CustomTeam);
                    CustomWinnerHolder.WinnerIds = aliveTeamPlayers.Values.First();
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        public static CustomTeam GetCustomTeam(byte id)
        {
            foreach (var team in EnabledCustomTeams)
            {
                if (team.TeamMembers.Contains(Main.PlayerStates[id].MainRole))
                    return team;
            }

            return null;
        }

        public static bool AreInSameCustomTeam(byte id1, byte id2)
        {
            if (EnabledCustomTeams.Count == 0) return false;

            var team1 = GetCustomTeam(id1);
            var team2 = GetCustomTeam(id2);

            return team1 != null && team2 != null && team1.Equals(team2);
        }

        public static bool IsSettingEnabledForPlayerTeam(byte id, CTAOption setting)
        {
            var team = GetCustomTeam(id);
            return team != null && IsSettingEnabledForTeam(team, setting);
        }

        public static bool IsSettingEnabledForTeam(CustomTeam team, CTAOption setting)
        {
            var optionsGroup = CustomTeamOptions.First(x => x.Team.Equals(team));
            var values = optionsGroup.AllOptions.ConvertAll(x => x.GetBool());
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
                catch (Exception e)
                {
                    Utils.ThrowException(e);
                }
                finally
                {
                    CustomTeams.Add(this);
                }
            }

            public List<CustomRoles> TeamMembers { get; } = [];

            public override bool Equals(object obj)
            {
                if (obj is not CustomTeam team) return false;
                return TeamName == team.TeamName;
            }

            public override int GetHashCode() => TeamName.GetHashCode();
        }

        internal class CustomTeamOptionGroup(CustomTeam team, BooleanOptionItem enabled, BooleanOptionItem knowRoles, BooleanOptionItem winWithOriginalTeam, BooleanOptionItem killEachOther, BooleanOptionItem guessEachOther, BooleanOptionItem arrows)
        {
            public readonly List<BooleanOptionItem> AllOptions = [enabled, knowRoles, winWithOriginalTeam, killEachOther, guessEachOther, arrows];
            public CustomTeam Team { get; set; } = team;

            public BooleanOptionItem Enabled { get; set; } = enabled;
            public BooleanOptionItem KnowRoles { get; set; } = knowRoles;
            public BooleanOptionItem WinWithOriginalTeam { get; set; } = winWithOriginalTeam;
            public BooleanOptionItem KillEachOther { get; set; } = killEachOther;
            public BooleanOptionItem GuessEachOther { get; set; } = guessEachOther;
            public BooleanOptionItem Arrows { get; set; } = arrows;
        }
    }

    public enum CTAOption
    {
        Enabled,
        KnowRoles,
        WinWithOriginalTeam,
        KillEachOther,
        GuessEachOther,
        Arrows
    }
}