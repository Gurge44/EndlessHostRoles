using System;
using System.Collections.Generic;
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
    public partial class TeamEditorPage : Page
    {
        public TeamEditorPage()
        {
            InitializeComponent();
        }

        void OverrideColorCheck(object sender, RoutedEventArgs e) => TeamColorTextBox.IsEnabled = OverrideColorCheckBox.IsChecked == true;
        void OverrideTitleCheck(object sender, RoutedEventArgs e) => TeamTitleTextBox.IsEnabled = OverrideTitleCheckBox.IsChecked == true;
        void OverrideSubTitleCheck(object sender, RoutedEventArgs e) => TeamSubTitleTextBox.IsEnabled = OverrideSubTitleCheckBox.IsChecked == true;
    }
}
