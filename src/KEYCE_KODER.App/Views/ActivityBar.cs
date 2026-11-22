using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KEYCE_KODER.App.Themes;

namespace KEYCE_KODER.App.Views;

public class ActivityBar : Border
{
    public event Action<string>? ViewSelected;

    private readonly StackPanel _panel = new();
    private Button? _active;

    public ActivityBar()
    {
        Width = 52;
        Background = ThemeColors.Brush(ThemeColors.ActivityBar);
        BorderBrush = ThemeColors.Brush(ThemeColors.Border);
        BorderThickness = new Thickness(0, 0, 1, 0);

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
            Height = 50,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        button.MouseEnter += (_, _) =>
        {
            if (_active != button)
                button.Background = ThemeColors.Brush(ThemeColors.Panel);
        };

        button.MouseLeave += (_, _) =>
        {
            if (_active != button)
                button.Background = Brushes.Transparent;
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
        button.Background = ThemeColors.Brush(ThemeColors.Panel);

        ViewSelected?.Invoke((string)button.Tag);
    }
}