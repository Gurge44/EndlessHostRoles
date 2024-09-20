using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using EHR;

namespace CustomTeamAssigner
{
    public partial class RoleDescFinder : Page
    {
        public static RoleDescFinder Instance { get; private set; } = null!;
        
        public RoleDescFinder()
        {
            InitializeComponent();
            MainGrid.Visibility = Visibility.Visible;
        }
        
        void FindDescription(object sender, RoutedEventArgs e)
        {
            FindDescriptionAsync();
        }
        
        async void FindDescriptionAsync()
        {
            try
            {
                FindDescriptionButton.IsEnabled = false;
                var role = RoleNameTextBox.Text.GetCustomRole();
                const string url = "https://raw.githubusercontent.com/Gurge44/EndlessHostRoles/main/Resources/Lang/en_US.json";
                using HttpClient httpClient = new();
                HttpResponseMessage response = await httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var descriptions = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
                    RoleDescriptionTextBlock.Text = descriptions.GetValueOrDefault(role + "InfoLong", "No description found for this role. Are you sure you typed it correctly?\n\nHere's a list of a few roles:\n" + string.Join(", ", descriptions.Keys.Shuffle().Take(10).Where(x => x.EndsWith("InfoLong")).Select(x => new string(x.SkipLast(8).ToArray())).Select(x => Utils.GetActualRoleName(x.GetCustomRole())))).Replace("\\n", "\n").RemoveHtmlTags();
                }
                else
                {
                    MessageBox.Show("Failed to get role descriptions. Ensure you have an active internet connection and try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                MessageBox.Show("An error occurred while trying to get role descriptions. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                FindDescriptionButton.IsEnabled = true;
            }
        }

        private void BackToMain(object sender, RoutedEventArgs e)
        {
            MainGrid.Visibility = Visibility.Collapsed;
            Utils.SetMainWindowContents(Visibility.Visible);
        }
    }
}