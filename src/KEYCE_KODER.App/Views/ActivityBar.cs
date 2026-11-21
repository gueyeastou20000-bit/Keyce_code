using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KEYCE_KODER.App.Views;

public class ActivityBar : Border
{
    public event Action<string>? ViewSelected;

    private readonly StackPanel _panel = new();
    private Button? _active;

    public ActivityBar()
    {
        Width = 48;
        Background = new SolidColorBrush(Color.FromRgb(51, 51, 51));
        Child = _panel;

        AddButton("explorer", "explorer", "Explorateur");
        AddButton("search", "search", "Recherche");

        Select((Button)_panel.Children[0]);
    }

    private void AddButton(string id, string icon, string tooltip)
    {
        var button = new Button
        {
            Content = SvgIconFactory.Create(icon, 22),
            ToolTip = tooltip,
            Tag = id,
            Height = 48,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        button.Click += (_, _) => Select(button);
        _panel.Children.Add(button);
    }

    private void Select(Button button)
    {
        if (_active != null)
        {
            _active.Background = Brushes.Transparent;
        }

        _active = button;
        button.Background = new SolidColorBrush(Color.FromRgb(80, 80, 80));

        ViewSelected?.Invoke((string)button.Tag);
    }
}
