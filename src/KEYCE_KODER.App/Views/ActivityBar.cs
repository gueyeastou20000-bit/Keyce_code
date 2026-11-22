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
        Width = 48;
        Background = ThemeColors.ActivityBar;
        BorderBrush = ThemeColors.Border;
        BorderThickness = new Thickness(0, 0, 1, 0);

        Child = _panel;

        AddButton("explorer", "explorer", "Explorateur");
        AddButton("search", "search", "Recherche");

        Select((Button)_panel.Children[0]);
    }

    private void AddButton(string id, string icon, string tooltip)
    {
        var indicator = new Border
        {
            Width = 2,
            Background = ThemeColors.Accent,
            Visibility = Visibility.Collapsed,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var iconView = SvgIconFactory.Create(icon, 22);

        var layout = new Grid();

        layout.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(3) });

        layout.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(indicator, 0);
        Grid.SetColumn(iconView, 1);

        layout.Children.Add(indicator);
        layout.Children.Add(iconView);

        var button = new Button
        {
            Content = layout,
            ToolTip = tooltip,
            Tag = id,
            Width = 48,
            Height = 48,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        button.Tag = new ActivityButtonData(id, indicator);

        button.MouseEnter += (_, _) =>
        {
            if (button != _active)
                button.Background = ThemeColors.Hover;
        };

        button.MouseLeave += (_, _) =>
        {
            if (button != _active)
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

            var oldData = (ActivityButtonData)_active.Tag;
            oldData.Indicator.Visibility = Visibility.Collapsed;
        }

        _active = button;

        button.Background = ThemeColors.Sidebar;

        var data = (ActivityButtonData)button.Tag;
        data.Indicator.Visibility = Visibility.Visible;

        ViewSelected?.Invoke(data.Id);
    }

    private sealed class ActivityButtonData
    {
        public string Id { get; }
        public Border Indicator { get; }

        public ActivityButtonData(string id, Border indicator)
        {
            Id = id;
            Indicator = indicator;
        }
    }
}