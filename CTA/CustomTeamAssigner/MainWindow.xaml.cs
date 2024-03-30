using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace CustomTeamAssigner
{
    public partial class MainWindow : Window
    {
        public static MainWindow Instance { get; private set; } = null!;

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;
        }

        void ImportPlaySet(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new()
            {
                Filter = "Text files (*.txt)|*.txt",
                RestoreDirectory = true
            };

            if (ofd.ShowDialog() == true)
            {
                Utils.Teams.Clear();
                File.ReadAllLines(ofd.FileName).Do(line => new Team(line.Split(';')[0]).Import(line));

                Navigator.NavigationService.Navigate(new PlaySetListerPage());
                Utils.SetMainWindowContents(Visibility.Collapsed);
            }
        }

        void CreateNewPlaySet(object sender, RoutedEventArgs e)
        {
            Utils.Teams.Clear();
            Navigator.NavigationService.Navigate(new PlaySetListerPage());
            Utils.SetMainWindowContents(Visibility.Collapsed);
        }

        void Exit(object sender, RoutedEventArgs e) => Close();
    }
}