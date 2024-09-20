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
        }

        private void SaveToNewFile(object sender, RoutedEventArgs e)
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true
            };

            if (sfd.ShowDialog() == true)
            {
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
    }
}