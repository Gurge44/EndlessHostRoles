using EHR;
using System.Text;
using System.Windows;

/*
 * Copyright (c) 2024, Gurge44
 * All rights reserved.
 *
 * This source code is licensed under the BSD-style license found in the
 * README file in the root directory of this source tree.
 */

namespace CustomTeamAssigner
{
    public class Team(string teamName)
    {
        public string RoleRevealScreenTitle { get; set; } = "*";

        public string RoleRevealScreenSubtitle { get; set; } = "*";

        public string RoleRevealScreenBackgroundColor { get; set; } = "*";

        public string TeamName { get; set; } = teamName;

        public List<CustomRoles> TeamMembers { get; set; } = [];

        public void Import(string line)
        {
            try
            {
                string[] parts = line.Split(';')[1..];

                string title = parts[0];
                string subtitle = parts[1];
                string bgColor = parts[2];

                TeamMembers = parts[3].Split(',').Select(member => Enum.Parse<CustomRoles>(member, true)).ToList();

                RoleRevealScreenTitle = title;
                RoleRevealScreenSubtitle = subtitle;
                RoleRevealScreenBackgroundColor = bgColor;
            }
            catch (Exception e)
            {
                switch (e)
                {
                    case NullReferenceException:
                        MessageBox.Show($"The file is empty.\nAt line: {line}", "Error while importing", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    case InvalidOperationException:
                    case IndexOutOfRangeException:
                        MessageBox.Show($"The file is not formatted correctly.\nAt line: {line}", "Error while importing", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    case ArgumentException:
                        MessageBox.Show($"A team member role could not be recognized from the given string.\nAt line: {line}", "Error while importing", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    default:
                        MessageBox.Show($"{e.Message}\nAt line: {line}", "Error while importing", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                }
            }
            finally
            {
                Utils.Teams.Add(this);
            }
        }

        public string Export
        {
            get
            {
                StringBuilder sb = new();
                sb.Append(TeamName);
                sb.Append(';');
                sb.Append(RoleRevealScreenTitle);
                sb.Append(';');
                sb.Append(RoleRevealScreenSubtitle);
                sb.Append(';');
                sb.Append(RoleRevealScreenBackgroundColor);
                sb.Append(';');
                sb.Append(string.Join(",", TeamMembers));
                return sb.ToString();
            }
        }

        public void SetAllValuesToPreset()
        {
            TeamName = "Really Cool Team";
            RoleRevealScreenTitle = "Teamed";
            RoleRevealScreenSubtitle = "You're in a Custom Team!";
            RoleRevealScreenBackgroundColor = "#00ffa5";
            TeamMembers = [];
        }

        public override bool Equals(object? obj)
        {
            if (obj is not Team team) return false;
            return TeamName == team.TeamName;
        }

        // ReSharper disable once NonReadonlyMemberInGetHashCode
        public override int GetHashCode() => TeamName.GetHashCode();
    }
}
