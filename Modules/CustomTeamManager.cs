using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        public static void InitializeCustomTeamPlayers()
        {
            if (CustomTeams.Count == 0) return;

            CustomTeamPlayerIds = Main.PlayerStates
                .IntersectBy(Main.AllAlivePlayerControls.Select(x => x.PlayerId), x => x.Key)
                .GroupBy(x => CustomTeams.FirstOrDefault(t => t.TeamMembers.Contains(x.Value.MainRole)), x => x.Key)
                .ToDictionary(x => x.Key, x => x.ToHashSet());
        }

        public static bool CheckCustomTeamGameEnd()
        {
            if (CustomTeams.Count == 0 || CustomTeamPlayerIds.Count == 0) return false;

            CustomTeamPlayerIds.Values.Do(x => x.RemoveWhere(p =>
            {
                var pc = Utils.GetPlayerById(p);
                return pc == null || !pc.IsAlive() || pc.Data.Disconnected;
            }));

            if (CustomTeamPlayerIds.Count == 1)
            {
                WinnerTeam = CustomTeamPlayerIds.Keys.First();
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.CustomTeam);
                CustomWinnerHolder.WinnerIds = CustomTeamPlayerIds.Values.First();
                return true;
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
