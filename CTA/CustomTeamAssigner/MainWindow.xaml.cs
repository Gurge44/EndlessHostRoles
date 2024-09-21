using System.Diagnostics.CodeAnalysis;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

/*
 * Copyright (c) 2024, Gurge44
 * All rights reserved.
 *
 * This source code is licensed under the BSD-style license found in the
 * README file in the root directory of this source tree.
 */

namespace CustomTeamAssigner
{
    public partial class MainWindow : Window
    {
        public static MainWindow Instance { get; private set; } = null!;

        private static readonly List<(string FileName, DrawingImage Image)> SVGs = [];

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;
            LoadSvgImages();
        }

        void LoadSvgImages()
        {
            int doneIndex = 0;
            var fileReader = new FileSvgReader(new());
            var files = Directory.GetFiles("Resources", "*.svg");
            files.Do(LoadImage);
            LoadingLabel.Visibility = Visibility.Collapsed;
            return;

            void LoadImage(string file)
            {
                var drawing = fileReader.Read(file);
                var image = new DrawingImage(drawing);
                var fileName = Path.GetFileNameWithoutExtension(file);
                SVGs.Add((fileName, image));

                ApplyImage(fileName, image);
                
                doneIndex++;
                LoadingLabel.Content = $"Loading images.... ({doneIndex}/{files.Length})";
            }
        }

        public static void ApplyAllImages()
        {
            foreach (var tuple in SVGs)
            {
                var fileName = tuple.FileName;
                var image = tuple.Image;
                
                ApplyImage(fileName, image);
            }
        }

        [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract")]
        private static void ApplyImage(string fileName, DrawingImage image)
        {
            switch (fileName)
            {
                case "add":
                    Instance.AddButtonImage.Source = image;
                    if (PlaySetListerPage.Instance != null) PlaySetListerPage.Instance.AddButtonImage.Source = image;
                    break;
                case "back":
                    if (PlaySetListerPage.Instance != null) PlaySetListerPage.Instance.BackButtonImage.Source = image;
                    if (QMQuestions.Instance != null) QMQuestions.Instance.BackButtonImage.Source = image;
                    if (RichTextEditor.Instance != null) RichTextEditor.Instance.BackButtonImage.Source = image;
                    if (RoleDescFinder.Instance != null) RoleDescFinder.Instance.BackButtonImage.Source = image;
                    break;
                case "copy":
                    if (QMQuestions.Instance != null) QMQuestions.Instance.CopyButtonImage.Source = image;
                    break;
                case "delete":
                    if (PlaySetListerPage.Instance != null) PlaySetListerPage.Instance.DeleteButtonImage.Source = image;
                    if (TeamEditorPage.Instance != null) TeamEditorPage.Instance.DeleteButtonImage.Source = image;
                    break;
                case "discard":
                    if (TeamEditorPage.Instance != null) TeamEditorPage.Instance.DiscardButtonImage.Source = image;
                    break;
                case "edit":
                    if (PlaySetListerPage.Instance != null) PlaySetListerPage.Instance.EditButtonImage.Source = image;
                    break;
                case "import":
                    Instance.ImportButtonImage.Source = image;
                    break;
                case "new-file":
                    if (QMQuestions.Instance != null) QMQuestions.Instance.NewFileButtonImage.Source = image;
                    break;
                case "question":
                    Instance.QuestionButtonImage.Source = image;
                    break;
                case "save":
                    if (TeamEditorPage.Instance != null) TeamEditorPage.Instance.SaveButtonImage.Source = image;
                    break;
                case "save-as":
                    if (PlaySetListerPage.Instance != null) PlaySetListerPage.Instance.SaveAsButtonImage.Source = image;
                    if (QMQuestions.Instance != null) QMQuestions.Instance.SaveAsButtonImage.Source = image;
                    break;
                case "search":
                    Instance.SearchButtonImage.Source = image;
                    if (RoleDescFinder.Instance != null) RoleDescFinder.Instance.SearchButtonImage.Source = image;
                    break;
                case "template":
                    Instance.TemplateButtonImage.Source = image;
                    break;
            }
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
                ApplyAllImages();
            }
        }

        void CreateNewPlaySet(object sender, RoutedEventArgs e)
        {
            Utils.Teams.Clear();
            Navigator.NavigationService.Navigate(new PlaySetListerPage());
            Utils.SetMainWindowContents(Visibility.Collapsed);
            ApplyAllImages();
        }

        private void QMQuestionsClick(object sender, RoutedEventArgs e)
        {
            Utils.SetMainWindowContents(Visibility.Collapsed);
            Navigator.NavigationService.Navigate(new QMQuestions());
            ApplyAllImages();
        }

        private void OpenRoleDescFinder(object sender, RoutedEventArgs e)
        {
            Utils.SetMainWindowContents(Visibility.Collapsed);
            Navigator.NavigationService.Navigate(new RoleDescFinder());
            ApplyAllImages();
        }

        private void OpenTemplateCreator(object sender, RoutedEventArgs e)
        {
            Utils.SetMainWindowContents(Visibility.Collapsed);
            Navigator.NavigationService.Navigate(new RichTextEditor());
            ApplyAllImages();
        }
    }
}