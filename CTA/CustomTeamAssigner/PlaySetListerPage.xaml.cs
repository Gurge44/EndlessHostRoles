using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CustomTeamAssigner
{
    public partial class PlaySetListerPage : Page
    {
        public PlaySetListerPage()
        {
            InitializeComponent();
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
