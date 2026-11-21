using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Keyce_code.App.Views;

public class ActivityBar : Border
{
    // Événement envoyé quand on clique sur une icône
    public event Action<string>? ViewSelected;

    private readonly StackPanel _panel = new();
    private Button? _active;

    public ActivityBar()
    {
        Width = 48;
        Background = new SolidColorBrush(Color.FromRgb(51, 51, 51));
        Child = _panel;

        AddButton("explorer", "📁", "Explorateur");
        AddButton("search", "🔍", "Recherche");
    }

    private void AddButton(string id, string icon, string tooltip)
    {
        var button = new Button
        {
            Content = icon,
            ToolTip = tooltip,
            Tag = id,
            Height = 48,
            FontSize = 20,
            Foreground = Brushes.White,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };

        button.Click += (s, e) => Select(button);
        _panel.Children.Add(button);
    }

    private void Select(Button button)
    {
        if (_active != null)
            _active.Background = Brushes.Transparent;

        _active = button;
        button.Background = new SolidColorBrush(Color.FromRgb(80, 80, 80));

        ViewSelected?.Invoke((string)button.Tag);
    }
}