using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using EHR;
using EHR.Modules;

namespace SettingsUI;

public partial class MainWindow
{
    
    private Dictionary<TabGroup, Page> Tabs = [];
    
    static int GetParentOffset(OptionItem item)
    {
        var offset = 0;
        while (item.Parent != null)
        {
            offset += 10;
            item = item.Parent;
        }

        return offset;
    }

    static StackPanel SetupOption(string name, string value, OptionItem tag) => new()
    {
        Orientation = Orientation.Horizontal,
        Background = Brushes.DimGray,
        Margin = new(GetParentOffset(tag), 5, 0, 5),
        Children =
        {
            new TextBlock
            {
                Text = name,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new(5, 0, 0, 5),
                Foreground = Brushes.LightGray,
                TextWrapping = TextWrapping.NoWrap,
                FontSize = name.Length > 30 ? 16 : 20
            },
            new Button
            {
                Content = "-",
                Width = 20,
                Height = 20,
                Margin = new(5, 0, 0, 5),
                Background = Brushes.Transparent,
                Foreground = Brushes.LightGray,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new(1),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = new AdjustButtonData(false, tag)
            },
            new TextBlock
            {
                Text = value,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new(5, 0, 0, 5),
                Foreground = Brushes.LightGray,
                TextWrapping = TextWrapping.NoWrap
            },
            new Button
            {
                Content = "+",
                Width = 20,
                Height = 20,
                Margin = new(5, 0, 0, 5),
                Background = Brushes.Transparent,
                Foreground = Brushes.LightGray,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new(1),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = new AdjustButtonData(true, tag)
            }
        },
        Tag = tag
    };

    static StackPanel SetupBooleanOption(string name, bool value, OptionItem tag) => new()
    {
        Orientation = Orientation.Horizontal,
        Background = Brushes.DimGray,
        Margin = new(GetParentOffset(tag), 5, 0, 5),
        Children =
        {
            new TextBlock
            {
                Text = name,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new(5, 0, 0, 5),
                Foreground = Brushes.LightGray,
                TextWrapping = TextWrapping.NoWrap,
                FontSize = name.Length > 30 ? 16 : 20
            },
            new Button
            {
                Content = value ? "✔" : "✘",
                Width = 20,
                Height = 20,
                Margin = new(5, 0, 0, 5),
                Background = Brushes.Transparent,
                Foreground = value ? Brushes.Turquoise : Brushes.Red,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new(1),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = new AdjustButtonData(false, tag)
            }
        },
        Tag = tag
    };
    
    public MainWindow()
    {
        InitializeComponent();
        InitTabs();
        //LoadOptions();

        //MainFrame.NavigationService.Navigate(Tabs[default]);
    }
    
    void InitTabs()
    {
        var tabGroups = Enum.GetValues<TabGroup>();
        
        Tabs = tabGroups.ToDictionary(x => x, _ => new Page());
        
        tabGroups.Do(x =>
        {
            var image = new Image
            {
                Source = new BitmapImage(new($"TabIcon_{x}.png", UriKind.Relative)),
                Stretch = Stretch.Fill,
                Width = 50,
                Height = 50,
                Margin = new(0, 0, 10, 0),
                Opacity = 0.5
            };
            TabsPanel.Children.Add(image);
            image.MouseDown += (_, _) =>
            {
                foreach (var child in TabsPanel.Children)
                {
                    if (child is Image img)
                    {
                        img.Opacity = img == image ? 1 : 0.5;
                    }
                }
                MainFrame.Content = Tabs[x];
            };
            image.MouseEnter += (_, _) =>
            {
                image.Cursor = Cursors.Hand;
                image.Effect = new DropShadowEffect
                {
                    Color = Colors.White,
                    ShadowDepth = 0,
                    BlurRadius = 10
                };
            };
            image.MouseLeave += (_, _) =>
            {
                image.Cursor = Cursors.Arrow;
                image.Effect = null;
            };
        });
    }

    void LoadOptions()
    {
        foreach ((TabGroup tabGroup, Page page) in Tabs)
        {
            var pageContent = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new(10)
            };

            foreach (var optionItem in Options.GroupedOptions[tabGroup])
            {
                var newItem = optionItem is BooleanOptionItem booleanOptionItem
                    ? SetupBooleanOption(optionItem.Name, booleanOptionItem.GetBool(), optionItem)
                    : SetupOption(optionItem.Name, optionItem.GetValue().ToString(), optionItem);
                
                newItem.Children.OfType<Button>().Do(x => x.Click += (_, _) =>
                {
                    if (x.Tag is AdjustButtonData data)
                    {
                        data.AdjustValue();
                        if (optionItem is BooleanOptionItem bo)
                        {
                            x.Content = bo.GetBool() ? "✔" : "✘";
                            x.Foreground = bo.GetBool() ? Brushes.Turquoise : Brushes.Red;
                        }
                        else
                        {
                            x.Content = data.IsIncrement ? "+" : "-";
                            newItem.Children.OfType<TextBlock>().Last().Text = optionItem.GetValue().ToString();
                        }
                    }
                });
                
                pageContent.Children.Add(newItem);
            }
            
            page.Content = pageContent;
        }
    }

    void Save(object sender, RoutedEventArgs e)
    {
        //OptionSaver.Save();
        Close();
    }
}

class AdjustButtonData(bool isIncrement, OptionItem optionItem)
{
    public bool IsIncrement { get; } = isIncrement;

    public void AdjustValue()
    {
        int change = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ? 5 : 1;
        if (IsIncrement) optionItem.SetValue(optionItem.CurrentValue + change);
        else optionItem.SetValue(optionItem.CurrentValue - change);
    }
}