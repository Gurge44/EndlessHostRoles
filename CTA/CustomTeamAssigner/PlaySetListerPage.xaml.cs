using System.IO;
using System.Windows;
using System.Windows.Controls;

/*
 * Copyright (c) 2024, Gurge44
 * All rights reserved.
 *
 * This source code is licensed under the BSD-style license found in the
 * README file in the root directory of this source tree.
 */

namespace CustomTeamAssigner
{
    public partial class PlaySetListerPage : Page
    {
        public static PlaySetListerPage Instance { get; private set; } = null!;
        
        public PlaySetListerPage()
        {
            InitializeComponent();
            Instance = this;
            TeamListBox.ItemsSource = Utils.Teams.Select(x => x.TeamName);
            MainGrid.Visibility = Visibility.Visible;
        }

        void EditTeam(object sender, RoutedEventArgs e)
        {
            if (TeamListBox.SelectedIndex == -1)
            {
                MessageBox.Show("Please select a team to edit.", "No team selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MainWindow.Instance.Navigator.NavigationService.Navigate(new TeamEditorPage(Utils.Teams.ElementAt(TeamListBox.SelectedIndex)));
            Utils.SetMainWindowContents(Visibility.Collapsed);
            MainWindow.ApplyAllImages();
        }

        void DeleteTeam(object sender, RoutedEventArgs e)
        {
            if (TeamListBox.SelectedIndex == -1)
            {
                MessageBox.Show("Please select a team to delete.", "No team selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Utils.Teams.Remove(Utils.Teams.ElementAt(TeamListBox.SelectedIndex));
            TeamListBox.ItemsSource = Utils.Teams.Select(x => x.TeamName);
        }

        void AddNewTeam(object sender, RoutedEventArgs e)
        {
            Team team = new(string.Empty);
            team.SetAllValuesToPreset();
            Utils.Teams.Add(team);
            MainWindow.Instance.Navigator.NavigationService.Navigate(new TeamEditorPage(team));
            Utils.SetMainWindowContents(Visibility.Collapsed);
            MainWindow.ApplyAllImages();
        }

        void SavePlaySet(object sender, RoutedEventArgs e)
        {
            File.WriteAllText(Utils.OutputFileName, string.Join('\n', Utils.Teams.Select(x => x.Export)));
            MessageBox.Show("The play-set has been saved successfully.", "Play-Set saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        void ChangeSelection(object sender, SelectionChangedEventArgs e)
        {
            bool enabled = TeamListBox.SelectedIndex != -1;
            EditTeamButton.IsEnabled = enabled;
            DeleteTeamButton.IsEnabled = enabled;
        }

        void Back(object sender, RoutedEventArgs e)
        {
            MainGrid.Visibility = Visibility.Collapsed;
            Utils.SetMainWindowContents(Visibility.Visible);
        }
    }
}
