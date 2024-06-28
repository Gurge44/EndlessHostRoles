﻿using EHR;
using System.Text;
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
    internal static class Utils
    {
        public static HashSet<Team> Teams = [];

        public const string OutputFileName = "CTA_Data.txt";

        public static Dictionary<CustomRoles, string> RoleNames = new()
        {
            { CustomRoles.Hacker, "Anonymous" },
            { CustomRoles.Sans, "Arrogance" },
            { CustomRoles.OverKiller, "Butcher" },
            { CustomRoles.EvilDiviner, "Consigliere" },
            { CustomRoles.Minimalism, "Killing Machine" },
            { CustomRoles.BallLightning, "Lightning" },
            { CustomRoles.LovingCrewmate, "Lover" },
            { CustomRoles.LovingImpostor, "Loving Impostor" },
            { CustomRoles.Mafia, "Nemesis" },
            { CustomRoles.SerialKiller, "Mercenary" },
            { CustomRoles.Assassin, "Ninja" },
            { CustomRoles.ImperiusCurse, "Soul Catcher" },
            { CustomRoles.BoobyTrap, "Trapster" },
            { CustomRoles.CyberStar, "Celebrity" },
            { CustomRoles.Bloodhound, "Coroner" },
            { CustomRoles.Divinator, "Fortune Teller" },
            { CustomRoles.ParityCop, "Inspector" },
            { CustomRoles.Needy, "Lazy Guy" },
            { CustomRoles.SabotageMaster, "Mechanic" },
            { CustomRoles.SwordsMan, "Vigilante" },
            { CustomRoles.Gamer, "Demon" },
            { CustomRoles.Totocalcio, "Follower" },
            { CustomRoles.FFF, "Hater" },
            { CustomRoles.NSerialKiller, "Serial Killer" },
            { CustomRoles.DarkHide, "Stalker" },
            { CustomRoles.ToiletMaster, "Toilet Master" }
        };

        public static void SetMainWindowContents(Visibility visibility)
        {
            MainWindow.Instance.Title.Visibility = visibility;
            MainWindow.Instance.MainGrid.Children.OfType<Button>().Do(x => x.Visibility = visibility);
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

        public static void Do<T>(this IEnumerable<T> enumerable, Action<T> action)
        {
            foreach (var item in enumerable)
            {
                action(item);
            }
        }

        public static IEnumerable<CustomRoles> GetAllValidRoles() => Enum.GetValues<CustomRoles>().Where(x => !Teams.Any(t => t.TeamMembers.Contains(x)) && !x.ToString().Contains("EHR") && x < CustomRoles.NotAssigned && x is not (CustomRoles.KB_Normal or CustomRoles.Killer or CustomRoles.Tasker or CustomRoles.Potato or CustomRoles.Hider or CustomRoles.Seeker or CustomRoles.Fox or CustomRoles.Troll or CustomRoles.GM or CustomRoles.Convict or CustomRoles.Impostor or CustomRoles.Shapeshifter or CustomRoles.Crewmate or CustomRoles.Engineer or CustomRoles.Scientist or CustomRoles.GuardianAngel));

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

        public static CustomRoles GetInternalRoleName(this string roleName)
        {
            if (roleName == "Guess Manager") return CustomRoles.GuessManagerRole;
            var role = RoleNames.FirstOrDefault(x => x.Value == roleName).Key;
            return role != default ? role : Enum.Parse<CustomRoles>(roleName.Replace(" ", string.Empty));
        }
    }
}
