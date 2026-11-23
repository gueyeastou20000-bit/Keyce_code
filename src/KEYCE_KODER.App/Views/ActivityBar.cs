using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;

namespace KEYCE_KODER.App.Views;

public class ActivityBar : Border
{
    public event Action<string>? ViewSelected;

    private readonly StackPanel _panel = new();
    private Button? _active;

    public ActivityBar()
    {
        Width = 52;
        Background = Theme.ActivityBar;
        BorderBrush = Theme.Border;
        BorderThickness = new Thickness(0, 0, 1, 0);
        Child = _panel;

        AddButton("explorer", "explorer", "Explorateur");
        AddButton("search", "search", "Recherche");
        AddTextButton("home", "⌂", "Accueil");
        AddTextButton("settings", "⚙", "Paramètres");

        Select((Button)_panel.Children[0]);
    }

    private void AddButton(string id, string icon, string tooltip)
    {
        var button = new Button
        {
            Content = SvgIconFactory.Create(icon, 21),
            ToolTip = tooltip,
            Tag = id,
            Height = 52,
            Width = 52,
            Margin = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand
        };

        button.MouseEnter += (_, _) =>
        {
            if (button != _active) button.Background = Theme.Surface;
        };
        button.MouseLeave += (_, _) =>
        {
            if (button != _active) button.Background = Brushes.Transparent;
        };
        button.Click += (_, _) => Select(button);
        _panel.Children.Add(button);
    }

    private void AddTextButton(string id, string text, string tooltip)
    {
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = text,
                Foreground = Theme.Muted,
                FontSize = 20,
                TextAlignment = TextAlignment.Center
            },
            ToolTip = tooltip,
            Tag = id,
            Height = 52,
            Width = 52,
            Margin = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand
        };

        button.MouseEnter += (_, _) =>
        {
            if (button != _active) button.Background = Theme.Surface;
        };
        button.MouseLeave += (_, _) =>
        {
            if (button != _active) button.Background = Brushes.Transparent;
        };
        button.Click += (_, _) => Select(button);
        _panel.Children.Add(button);
    }

    private void Select(Button button)
    {
        if (_active != null)
        {
            _active.Background = Brushes.Transparent;
            _active.BorderThickness = new Thickness(0);
        }

        _active = button;
        button.Background = Theme.AccentSoft;
        button.BorderBrush = Theme.Accent;
        button.BorderThickness = new Thickness(3, 0, 0, 0);

        ViewSelected?.Invoke((string)button.Tag);
    }
}
