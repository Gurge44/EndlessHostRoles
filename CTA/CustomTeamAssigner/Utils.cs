using EHR;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

/*
 * Copyright (c) 2024, Gurge44
 * All rights reserved.
 *
 * This source code is licensed under the BSD-style license found in the
 * README file in the root directory of this source tree.
 */

namespace CustomTeamAssigner
{
    internal static partial class Utils
    {
        public static readonly HashSet<Team> Teams = [];

        public const string OutputFileName = "CTA_Data.txt";

        private static readonly Dictionary<CustomRoles, string> RoleNames = new()
        {
            { CustomRoles.LovingCrewmate, "Lover" },
            { CustomRoles.LovingImpostor, "Loving Impostor" },
            { CustomRoles.FortuneTeller, "Fortune Teller" },
            { CustomRoles.LazyGuy, "Lazy Guy" },
            { CustomRoles.ToiletMaster, "Toilet Master" }
        };

        public static void SetMainWindowContents(Visibility visibility)
        {
            MainWindow.Instance.Title.Visibility = visibility;
            MainWindow.Instance.MainGrid.Children.OfType<Button>().Do(x => x.Visibility = visibility);
            MainWindow.Instance.ButtonGroup.Visibility = visibility;
        }

        public static Color ToColor(this string htmlColor)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(htmlColor);
            }
            catch
            {
                return Color.FromRgb(0, 0, 0);
            }
        }
        
        public static string RemoveHtmlTags(this string str) => MyRegex().Replace(str, string.Empty);

        public static void Do<T>(this IEnumerable<T> enumerable, Action<T> action)
        {
            foreach (var item in enumerable)
            {
                action(item);
            }
        }
        
        public static List<T> Shuffle<T>(this IEnumerable<T> collection)
        {
            var list = collection.ToList();
            int n = list.Count;
            var r = new Random();
            while (n > 1)
            {
                n--;
                int k = r.Next(n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }

            return list;
        }

        public static IEnumerable<CustomRoles> GetAllValidRoles() => Enum.GetValues<CustomRoles>().Where(x => !Teams.Any(t => t.TeamMembers.Contains(x)) && !x.ToString().Contains("EHR") && x < CustomRoles.NotAssigned && x is not (CustomRoles.SoloPVP_Player or CustomRoles.Killer or CustomRoles.Tasker or CustomRoles.Potato or CustomRoles.Runner or CustomRoles.CTFPlayer or CustomRoles.NDPlayer or CustomRoles.Hider or CustomRoles.Seeker or CustomRoles.Fox or CustomRoles.Troll or CustomRoles.Jet or CustomRoles.Detector or CustomRoles.Jumper or CustomRoles.Venter or CustomRoles.Locator or CustomRoles.Agent or CustomRoles.Dasher or CustomRoles.GM or CustomRoles.Convict or CustomRoles.Impostor or CustomRoles.Shapeshifter or CustomRoles.Crewmate or CustomRoles.Engineer or CustomRoles.Scientist or CustomRoles.GuardianAngel or CustomRoles.Phantom or CustomRoles.Tracker or CustomRoles.Noisemaker));

        public static string GetActualRoleName(CustomRoles role)
        {
            if (RoleNames.TryGetValue(role, out var roleName))
            {
                return roleName;
            }

            var sb = new StringBuilder().Append($"{role}");
            for (int i = 1; i < sb.Length; i++)
            {
                if (char.IsUpper(sb[i]))
                {
                    sb.Insert(i, ' ');
                    break;
                }
            }

            if (sb.ToString().EndsWith("Role"))
            {
                sb.Remove(sb.Length - 4, 4);
            }

            return sb.ToString();
        }

        public static CustomRoles GetCustomRole(this string roleName)
        {
            var role = RoleNames.FirstOrDefault(x => x.Value == roleName).Key;
            return role != default ? role : Enum.Parse<CustomRoles>(roleName.Replace(" ", string.Empty), true);
        }

        [GeneratedRegex("<[^>]*?>")]
        private static partial Regex MyRegex();
    }
}
