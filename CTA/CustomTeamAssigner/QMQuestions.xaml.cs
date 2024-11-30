using System.Windows;
using System.Windows.Controls;

namespace CustomTeamAssigner
{
    public partial class QMQuestions : Page
    {
        public static QMQuestions Instance { get; private set; } = null!;
        
        public QMQuestions()
        {
            InitializeComponent();
            Instance = this;
            MainGrid.Visibility = Visibility.Visible;
        }

        private void SaveToNewFile(object sender, RoutedEventArgs e)
        {
            if (OutputTextBox.Text.Contains(";;") || CorrectAnswerComboBox.SelectedIndex == -1)
            {
                MessageBox.Show("Please fill in all fields before saving.", "Missing information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            MessageBox.Show("Please select where your file will be saved. To make it easier for yourself, I recommend your Among Us / EHR_DATA folder. Please note that the file name must be \"QuizMasterQuestions.txt\".", "Save to new file", MessageBoxButton.OK, MessageBoxImage.Information);
            
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true,
                DefaultDirectory = @"C:\Program Files (x86)\Steam\steamapps\common\Among Us\EHR_DATA",
                FileName = "QuizMasterQuestions.txt"
            };

            if (sfd.ShowDialog() == true)
            {
                if (!sfd.FileName.EndsWith("QuizMasterQuestions.txt"))
                    sfd.FileName = string.Join('\\', sfd.FileName.Split('\\').SkipLast(1)) + "\\QuizMasterQuestions.txt";
                System.IO.File.WriteAllText(sfd.FileName, OutputTextBox.Text);
            }
        }

        private void TextBoxTextChanged(object sender, TextChangedEventArgs e) => UpdateOutput();
        private void ComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateOutput();

        void UpdateOutput()
        {
            if (QuestionTextBox == null || Answer1TextBox == null || Answer2TextBox == null || Answer3TextBox == null || CorrectAnswerComboBox == null) return;
            OutputTextBox.Text = string.Join(';', QuestionTextBox.Text, Answer1TextBox.Text, Answer2TextBox.Text, Answer3TextBox.Text, CorrectAnswerComboBox.SelectedIndex.ToString());
        }

        private void AddToExistingFile(object sender, RoutedEventArgs e)
        {
            if (OutputTextBox.Text.Contains(";;") || CorrectAnswerComboBox.SelectedIndex == -1)
            {
                MessageBox.Show("Please fill in all fields before saving.", "Missing information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true
            };

            if (ofd.ShowDialog() == true)
            {
                System.IO.File.AppendAllText(ofd.FileName, '\n' + OutputTextBox.Text);
            }
        }

        private void CopyOutput(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(OutputTextBox.Text); 
        }

        private void GoBack(object sender, RoutedEventArgs e)
        {
            MainGrid.Visibility = Visibility.Collapsed;
            Utils.SetMainWindowContents(Visibility.Visible);
        }
    }
}