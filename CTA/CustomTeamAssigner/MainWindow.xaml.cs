using Microsoft.Win32;
using System.IO;
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
    public partial class MainWindow
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

        private void QMQuestionsClick(object sender, RoutedEventArgs e)
        {
            Utils.SetMainWindowContents(Visibility.Collapsed);
            Navigator.NavigationService.Navigate(new QMQuestions());
        }

        private void OpenRoleDescFinder(object sender, RoutedEventArgs e)
        {
            Utils.SetMainWindowContents(Visibility.Collapsed);
            Navigator.NavigationService.Navigate(new RoleDescFinder());
        }

        private void OpenTemplateCreator(object sender, RoutedEventArgs e)
        {
            Utils.SetMainWindowContents(Visibility.Collapsed);
            Navigator.NavigationService.Navigate(new RichTextEditor());
        }
    }
}