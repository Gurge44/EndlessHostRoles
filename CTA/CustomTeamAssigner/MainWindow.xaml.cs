using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;

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
                File.ReadAllLines(ofd.FileName).Do(line => new Team(line.Split(';')[0]).Import(line));

                Navigator.NavigationService.Navigate(new PlaySetListerPage());
                MainGrid.Visibility = Visibility.Collapsed;
            }
        }

        void CreateNewPlaySet(object sender, RoutedEventArgs e)
        {
        }

        void Exit(object sender, RoutedEventArgs e) => Close();
    }
}