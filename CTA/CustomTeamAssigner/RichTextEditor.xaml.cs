using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CustomTeamAssigner
{
    public partial class RichTextEditor : Page
    {
        public RichTextEditor()
        {
            InitializeComponent();
            richTextBox.TextChanged += RichTextBox_TextChanged;
            richTextBox.Focus();
            MainGrid.Visibility = Visibility.Visible;
        }
        
        private void RichTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateHtmlPreview();
        }

        private void UpdateHtmlPreview()
        {
            string xamlText = GetXamlFromRichTextBox();
            string htmlText = ConvertXamlToHtml(xamlText);
            htmlPreview.Text = htmlText;
        }

        private string GetXamlFromRichTextBox()
        {
            TextRange textRange = new(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
            using MemoryStream stream = new();
            textRange.Save(stream, DataFormats.Xaml);
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private string ConvertXamlToHtml(string xamlText)
        {
            Section doc = (Section)XamlReader.Parse(xamlText);
            StringBuilder htmlBuilder = new();

            foreach (Block block in doc.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    ConvertParagraphToHtml(paragraph, ref htmlBuilder);
                    htmlBuilder.Append('\n');
                }
            }

            return htmlBuilder.ToString();
        }

        private void ConvertParagraphToHtml(Paragraph paragraph, ref StringBuilder htmlBuilder)
        {
            foreach (Inline inline in paragraph.Inlines)
            {
                switch (inline)
                {
                    case Run run:
                        ConvertRunToHtml(run, htmlBuilder);
                        break;
                    case Bold bold:
                        ConvertBoldToHtml(bold, htmlBuilder);
                        break;
                    case Italic italic:
                        ConvertItalicToHtml(italic, htmlBuilder);
                        break;
                    case Underline underline:
                        ConvertUnderlineToHtml(underline, htmlBuilder);
                        break;
                }
            }
        }

        private void ConvertRunToHtml(Run run, StringBuilder htmlBuilder)
        {
            string text = run.Text;

            string style = string.Empty;
            if (run.Foreground != null) style += $"<color={((SolidColorBrush)run.Foreground).Color.ToString()}>";
            if (!double.IsNaN(run.FontSize)) style += $"<size={run.FontSize / 10f}>".Replace(',', '.');
            if (run.FontWeight == FontWeights.Bold) style += "<b>";
            if (run.FontStyle == FontStyles.Italic) style += "<i>";
            if (run.TextDecorations.Contains(TextDecorations.Underline[0])) style += "<u>";
            
            if (!string.IsNullOrEmpty(style))
            {
                htmlBuilder.Append(style);
                htmlBuilder.Append(text);
                if (style.Contains("<color")) htmlBuilder.Append("</color>");
                if (style.Contains("<size")) htmlBuilder.Append("</size>");
                if (style.Contains("<b>")) htmlBuilder.Append("</b>");
                if (style.Contains("<i>")) htmlBuilder.Append("</i>");
                if (style.Contains("<u>")) htmlBuilder.Append("</u>");
            }
            else
            {
                htmlBuilder.Append(text);
            }
        }

        private void ConvertBoldToHtml(Bold bold, StringBuilder htmlBuilder)
        {
            foreach (Inline inline in bold.Inlines)
            {
                if (inline is Run run)
                {
                    ConvertRunToHtml(run, htmlBuilder);
                }
            }
        }

        private void ConvertItalicToHtml(Italic italic, StringBuilder htmlBuilder)
        {
            foreach (Inline inline in italic.Inlines)
            {
                if (inline is Run run)
                {
                    ConvertRunToHtml(run, htmlBuilder);
                }
            }
        }

        private void ConvertUnderlineToHtml(Underline underline, StringBuilder htmlBuilder)
        {
            foreach (Inline inline in underline.Inlines)
            {
                if (inline is Run run)
                {
                    ConvertRunToHtml(run, htmlBuilder);
                }
            }
        }

        private void Bold_Click(object sender, RoutedEventArgs e)
        {
            if (!richTextBox.Selection.IsEmpty)
                richTextBox.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
        }

        private void Italic_Click(object sender, RoutedEventArgs e)
        {
            if (!richTextBox.Selection.IsEmpty)
                richTextBox.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Italic);
        }

        private void Underline_Click(object sender, RoutedEventArgs e)
        {
            TextSelection selection = richTextBox.Selection;
            if (!selection.IsEmpty)
            {
                selection.ApplyPropertyValue(
                    Inline.TextDecorationsProperty, 
                    (Equals(selection.GetPropertyValue(Inline.TextDecorationsProperty), TextDecorations.Underline)
                        ? null
                        : TextDecorations.Underline)!);
            }
        }
        
        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox { SelectedItem: ComboBoxItem selectedItem })
            {
                double selectedFontSize = double.Parse(selectedItem.Content.ToString() ?? "20");
                richTextBox.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, selectedFontSize);
            }
        }

        private void FontColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox { SelectedItem: ComboBoxItem selectedItem })
            {
                string selectedColor = selectedItem.Content.ToString() ?? string.Empty;
                SolidColorBrush colorBrush = new BrushConverter().ConvertFromString(selectedColor) as SolidColorBrush ?? new(Colors.White);
                richTextBox.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, colorBrush);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            MainGrid.Visibility = Visibility.Collapsed;
            Utils.SetMainWindowContents(Visibility.Visible);
        }
    }
}