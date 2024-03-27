using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using EHR;

namespace CustomTeamAssigner
{
    internal class Team(string teamName)
    {
        public string RoleRevealScreenTitle { get; set; } = "*";

        public string RoleRevealScreenSubtitle { get; set; } = "*";

        public string RoleRevealScreenBackgroundColor { get; set; } = "*";

        public string TeamName
        {
            get => teamName;
            set => teamName = value;
        }

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
                        MessageBox.Show("The file is empty.", "Error while importing", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    case InvalidOperationException:
                    case IndexOutOfRangeException:
                        MessageBox.Show("The file is not formatted correctly.", "Error while importing", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    case ArgumentException:
                        MessageBox.Show("A team member role could not be recognized from the given string.", "Error while importing", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    default:
                        MessageBox.Show(e.Message, "Error while importing", MessageBoxButton.OK, MessageBoxImage.Error);
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
    }
}
