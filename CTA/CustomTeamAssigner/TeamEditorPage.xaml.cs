using EHR;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CustomTeamAssigner
{
    public partial class TeamEditorPage : Page
    {
        private readonly Team EditingTeam;
        private readonly List<CustomRoles> EditingTeamMembers;

        public TeamEditorPage(Team team)
        {
            InitializeComponent();
            EditingTeam = team;
            EditingTeamMembers = team.TeamMembers;
            InitializeMembersGrid();
            InitializeComboBox();
            OverrideColorCheckBox.IsChecked = EditingTeam.RoleRevealScreenBackgroundColor != "*";
            OverrideTitleCheckBox.IsChecked = EditingTeam.RoleRevealScreenTitle != "*";
            OverrideSubTitleCheckBox.IsChecked = EditingTeam.RoleRevealScreenSubtitle != "*";
        }

        void InitializeMembersGrid()
        {
            TeamMembersGrid.RowDefinitions.Clear();
            TeamMembersGrid.Children.Clear();

            for (int i = 0; i < Utils.GetAllValidRoles().Count() / 3 + 1; i++)
            {
                TeamMembersGrid.RowDefinitions.Add(new());
            }

            if (EditingTeamMembers.Count == 0) return;

            EditingTeamMembers.Do(AddMemberToGrid);
        }

        void InitializeComboBox()
        {
            MemberComboBox.Items.Clear();
            Utils.GetAllValidRoles().Do(role => MemberComboBox.Items.Add(Utils.GetActualRoleName(role)));
        }

        void UpdateComboBox()
        {
            MemberComboBox.Items.Clear();
            Utils.GetAllValidRoles().Except(EditingTeamMembers).Do(role => MemberComboBox.Items.Add(Utils.GetActualRoleName(role)));
        }

        void Save(object sender, RoutedEventArgs e)
        {
            if (Utils.Teams.Any(x => x.TeamName == TeamNameTextBox.Text) || string.IsNullOrWhiteSpace(TeamNameTextBox.Text) || EditingTeamMembers.Count == 0)
            {
                MessageBox.Show("The team name is already taken, or the team name is empty, or there are no team members.", "Invalid Team", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            EditingTeam.TeamName = TeamNameTextBox.Text;

            EditingTeam.RoleRevealScreenTitle = OverrideTitleCheckBox.IsChecked == true ? TeamTitleTextBox.Text : "*";
            EditingTeam.RoleRevealScreenSubtitle = OverrideSubTitleCheckBox.IsChecked == true ? TeamSubTitleTextBox.Text : "*";
            EditingTeam.RoleRevealScreenBackgroundColor = OverrideColorCheckBox.IsChecked == true ? TeamColorTextBox.Text : "*";

            EditingTeam.TeamMembers = EditingTeamMembers;

            MainWindow.Instance.Navigator.NavigationService.Navigate(new PlaySetListerPage());
            Utils.SetMainWindowContents(Visibility.Collapsed);
        }

        void Cancel(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.Navigator.NavigationService.Navigate(new PlaySetListerPage());
            Utils.SetMainWindowContents(Visibility.Collapsed);
        }

        void Delete(object sender, RoutedEventArgs e)
        {
            Utils.Teams.Remove(EditingTeam);
            MainWindow.Instance.Navigator.NavigationService.Navigate(new PlaySetListerPage());
            Utils.SetMainWindowContents(Visibility.Collapsed);
        }

        void AddMember(object sender, RoutedEventArgs e)
        {
            if (MemberComboBox.SelectedIndex == -1)
            {
                MessageBox.Show("Please select a role to add.", "No role selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var role = Enum.Parse<CustomRoles>(((string)MemberComboBox.SelectedItem).GetInternalRoleName().ToString());
            EditingTeamMembers.Add(role);
            MemberComboBox.Items.RemoveAt(MemberComboBox.SelectedIndex);
            AddMemberToGrid(role);
        }

        void AddMemberToGrid(CustomRoles role)
        {
            var count = TeamMembersGrid.Children.Count;
            var row = count / 3;
            var column = count % 3;

            var button = new Button
            {
                Content = Utils.GetActualRoleName(role),
                Background = Brushes.Black,
                Foreground = new SolidColorBrush(Colors.LightGray),
                FontSize = 20,
                Margin = new(2),
                Padding = new(2),
                Tag = role
            };

            button.Click += (sender, _) =>
            {
                Button element = (Button)sender;
                EditingTeamMembers.Remove((CustomRoles)element.Tag);
                UpdateComboBox();
                int index = TeamMembersGrid.Children.IndexOf(element);
                TeamMembersGrid.Children.Remove(element);
                for (int i = index; i < TeamMembersGrid.Children.Count; i++)
                {
                    var b = (Button)TeamMembersGrid.Children[i];
                    int bRow = Grid.GetRow(b);
                    int bColumn = Grid.GetColumn(b);
                    if (bColumn == 0)
                    {
                        Grid.SetRow(b, bRow - 1);
                        Grid.SetColumn(b, 2);
                    }
                    else Grid.SetColumn(b, bColumn - 1);
                }
            };

            button.MouseEnter += (sender, _) =>
            {
                var b = (Button)sender;
                b.Background = Brushes.LightGray;
                b.Foreground = new SolidColorBrush(Colors.Black);
            };
            button.MouseLeave += (sender, _) =>
            {
                var b = (Button)sender;
                b.Background = Brushes.Black;
                b.Foreground = new SolidColorBrush(Colors.LightGray);
            };

            Grid.SetRow(button, row);
            Grid.SetColumn(button, column);

            TeamMembersGrid.Children.Add(button);
        }

        void OverrideColorCheck(object sender, RoutedEventArgs e) => TeamColorTextBox.IsEnabled = OverrideColorCheckBox.IsChecked == true;
        void OverrideTitleCheck(object sender, RoutedEventArgs e) => TeamTitleTextBox.IsEnabled = OverrideTitleCheckBox.IsChecked == true;
        void OverrideSubTitleCheck(object sender, RoutedEventArgs e) => TeamSubTitleTextBox.IsEnabled = OverrideSubTitleCheckBox.IsChecked == true;

        void ColorTextChanged(object sender, TextChangedEventArgs e)
        {
            TeamColorTextBox.Foreground = new SolidColorBrush(TeamColorTextBox.Text.ToColor());
        }
    }
}
