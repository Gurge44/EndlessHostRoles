using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;

namespace EHR.Modules
{
    internal static class CustomTeamManager
    {
        internal class CustomTeam
        {
            public readonly string RoleRevealScreenTitle;
            public readonly string RoleRevealScreenSubtitle;
            public readonly string RoleRevealScreenBackgroundColor;
            public readonly string TeamName;

            public List<CustomRoles> TeamMembers { get; set; } = [];

            public override bool Equals(object obj)
            {
                if (obj is not CustomTeam team) return false;
                return TeamName == team.TeamName;
            }

            public override int GetHashCode() => TeamName.GetHashCode();

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
        }

        public static HashSet<CustomTeam> CustomTeams = [];
        public static CustomTeam WinnerTeam;
        public static Dictionary<CustomTeam, HashSet<byte>> CustomTeamPlayerIds = [];
        public static List<CustomTeamOptionGroup> CustomTeamOptions = [];

        internal class CustomTeamOptionGroup(CustomTeam team, BooleanOptionItem enabled, BooleanOptionItem knowRoles, BooleanOptionItem winWithOriginalTeam, BooleanOptionItem killEachOther, BooleanOptionItem guessEachOther, BooleanOptionItem arrows)
        {
            public CustomTeam Team {get; set;} = team;
            
            public BooleanOptionItem Enabled { get; set; } = enabled;
            public BooleanOptionItem KnowRoles { get; set; } = knowRoles;
            public BooleanOptionItem WinWithOriginalTeam { get; set; } = winWithOriginalTeam;
            public BooleanOptionItem KillEachOther { get; set; } = killEachOther;
            public BooleanOptionItem GuessEachOther { get; set; } = guessEachOther;
            public BooleanOptionItem Arrows { get; set; } = arrows;

            public List<BooleanOptionItem> AllOptions = [enabled, knowRoles, winWithOriginalTeam, killEachOther, guessEachOther, arrows];
        }

        public static void LoadCustomTeams()
        {
            try
            {
                if (!File.Exists("./EHR_DATA/CTA_Data.txt")) return;

                CustomTeams.Clear();
                // ReSharper disable once ObjectCreationAsStatement
#pragma warning disable CA1806
                File.ReadAllLines("./EHR_DATA/CTA_Data.txt").Do(x => new CustomTeam(x));
#pragma warning restore CA1806
            }
            catch (Exception e)
            {
                Utils.ThrowException(e);
            }
        }

        public static void RefreshCustomOptions()
        {
            CustomTeamOptions.ForEach(x => x.AllOptions.ForEach(o => OptionItem.Remove(o.Id)));
            CustomTeamOptions.Clear();

            const int startId = 649000;
            const TabGroup tab = TabGroup.GameSettings;
            CustomTeamOptions = CustomTeams.Select((x, i) => CreateSetting(x, startId + (6 * i))).ToList();
            return;

            static CustomTeamOptionGroup CreateSetting(CustomTeam team, int id)
            {
                var enabled = BooleanOptionItem.Create(id++, "CTA.TeamEnabled", true, tab);
                var knowRoles = BooleanOptionItem.Create(id++, "CTA.KnowRoles", true, tab);
                var winWithOriginalTeam = BooleanOptionItem.Create(id++, "CTA.WinWithOriginalTeam", false, tab);
                var killEachOther = BooleanOptionItem.Create(id++, "CTA.KillEachOther", false, tab);
                var guessEachOther = BooleanOptionItem.Create(id++, "CTA.GuessEachOther", false, tab);
                var arrows = BooleanOptionItem.Create(id, "CTA.Arrows", true, tab);

                CustomTeamOptionGroup group = new(team, enabled, knowRoles, winWithOriginalTeam, killEachOther, guessEachOther, arrows);
                group.AllOptions.Skip(1).Do(x => x.SetParent(group.Enabled));
                return group;
            }
        }

        public static void InitializeCustomTeamPlayers()
        {
            if (CustomTeams.Count == 0) return;

            CustomTeamPlayerIds = Main.PlayerStates
                .IntersectBy(Main.AllAlivePlayerControls.Select(x => x.PlayerId), x => x.Key)
                .GroupBy(x => CustomTeams.FirstOrDefault(t => t.TeamMembers.Contains(x.Value.MainRole)), x => x.Key)
                .Where(x => x.Key != null)
                .ToDictionary(x => x.Key, x => x.ToHashSet());
        }

        public static bool CheckCustomTeamGameEnd()
        {
            if (CustomTeams.Count == 0 || CustomTeamPlayerIds.Count == 0) return false;

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

        public static CustomTeam GetCustomTeam(byte id) => CustomTeams.FirstOrDefault(x => x.TeamMembers.Contains(Main.PlayerStates[id].MainRole));

        public static bool AreInSameCustomTeam(byte id1, byte id2)
        {
            if (CustomTeams.Count == 0) return false;

            var team1 = GetCustomTeam(id1);
            var team2 = GetCustomTeam(id2);

            return team1 != null && team2 != null && team1.Equals(team2);
        }
    }
}
