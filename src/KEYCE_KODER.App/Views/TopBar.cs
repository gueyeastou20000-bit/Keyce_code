using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace KEYCE_KODER.App.Views;

/// <summary>Barre du haut : boutons de menu, recherche au centre, icônes à droite.</summary>
public class TopBar : Border
{
    private readonly Action _onSearch;
    private readonly Popup _popup = new();
    private readonly StackPanel _panel = new();
    private readonly List<(Border Button, MenuItem Menu)> _buttons = new();
    private MenuItem? _openMenu;
    private Window? _window;

    public TopBar(IReadOnlyList<MenuItem> source, Action onSearch)
    {
        _onSearch = onSearch;
        Background = Theme.Window;
        Height = 42;

        var menuStrip = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        foreach (MenuItem top in source)
        {
            Border button = MakeMenuButton(top);
            _buttons.Add((button, top));
            menuStrip.Children.Add(button);
        }

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(menuStrip, 0);
        grid.Children.Add(menuStrip);

        UIElement search = BuildSearchBox();
        Grid.SetColumn(search, 1);
        grid.Children.Add(search);

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        right.Children.Add(IconButton("⚙", "Paramètres"));
        right.Children.Add(IconButton("🔔", "Notifications"));
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        Child = grid;

        _popup.StaysOpen = true;
        _popup.AllowsTransparency = true;
        _popup.Placement = PlacementMode.Bottom;
        _popup.Child = new Border
        {
            Background = Theme.Surface,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4),
            Margin = new Thickness(6),
            MinWidth = 260,
            Child = _panel,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 12, ShadowDepth = 2, Opacity = 0.5
            }
        };
    }

    private static TextBlock IconButton(string glyph, string tip) => new()
    {
        Text = glyph,
        FontSize = 15,
        Foreground = Theme.Muted,
        Margin = new Thickness(8, 0, 0, 0),
        ToolTip = tip,
        Cursor = Cursors.Hand
    };

    // ---------- Boutons de menu ----------

    private Border MakeMenuButton(MenuItem top)
    {
        var label = new TextBlock
        {
            Text = Clean(top.Header),
            Foreground = Theme.Text,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        };

        var button = new Border
        {
            Padding = new Thickness(11, 6, 11, 6),
            CornerRadius = new CornerRadius(6),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = label
        };

        button.MouseEnter += (_, _) =>
        {
            if (_popup.IsOpen) OpenMenu(top, button); // passe d'un menu à l'autre
            else button.Background = Theme.SurfaceHover;
        };
        button.MouseLeave += (_, _) =>
        {
            if (!ReferenceEquals(_openMenu, top)) button.Background = Brushes.Transparent;
        };
        button.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            if (_popup.IsOpen && ReferenceEquals(_openMenu, top)) CloseAll();
            else OpenMenu(top, button);
        };
        return button;
    }

    private void OpenMenu(MenuItem top, Border button)
    {
        if (ReferenceEquals(_openMenu, top) && _popup.IsOpen) return;

        // Laisse le code existant mettre à jour les éléments activés / désactivés
        top.RaiseEvent(new RoutedEventArgs(MenuItem.SubmenuOpenedEvent));

        foreach (var (b, _) in _buttons) b.Background = Brushes.Transparent;
        button.Background = Theme.AccentSoft;
        _openMenu = top;

        _panel.Children.Clear();
        foreach (object child in top.Items)
        {
            if (child is Separator)
            {
                _panel.Children.Add(new Border
                {
                    Height = 1,
                    Background = Theme.Border,
                    Margin = new Thickness(6, 4, 6, 4)
                });
            }
            else if (child is MenuItem mi)
            {
                _panel.Children.Add(MakeItemRow(mi));
            }
        }

        _popup.IsOpen = false;
        _popup.PlacementTarget = button;
        _popup.IsOpen = _panel.Children.Count > 0;

        if (_popup.IsOpen && _window is null)
        {
            _window = Window.GetWindow(this);
            if (_window != null)
            {
                _window.PreviewMouseDown += OnWindowMouseDown;
                _window.Deactivated += OnWindowDeactivated;
            }
        }
    }

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_popup.Child is FrameworkElement c && c.IsMouseOver) return;
        foreach (var (b, _) in _buttons) if (b.IsMouseOver) return;
        CloseAll();
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) => CloseAll();

    private void CloseAll()
    {
        _popup.IsOpen = false;
        _openMenu = null;
        foreach (var (b, _) in _buttons) b.Background = Brushes.Transparent;
        if (_window != null)
        {
            _window.PreviewMouseDown -= OnWindowMouseDown;
            _window.Deactivated -= OnWindowDeactivated;
            _window = null;
        }
    }

    private static string Clean(object? header) =>
        (header?.ToString() ?? string.Empty).Replace("_", string.Empty);

    private UIElement MakeItemRow(MenuItem mi)
    {
        bool enabled = mi.IsEnabled;

        var dock = new DockPanel { LastChildFill = false };
        var gesture = new TextBlock
        {
            Text = mi.InputGestureText ?? string.Empty,
            Foreground = Theme.Muted,
            FontSize = 12,
            Margin = new Thickness(24, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(gesture, Dock.Right);
        dock.Children.Add(gesture);
        dock.Children.Add(new TextBlock
        {
            Text = Clean(mi.Header),
            Foreground = enabled ? Theme.Text : Theme.Muted,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        });

        var row = new Border
        {
            Padding = new Thickness(12, 7, 12, 7),
            CornerRadius = new CornerRadius(5),
            Background = Brushes.Transparent,
            Cursor = enabled ? Cursors.Hand : Cursors.Arrow,
            Child = dock
        };

        if (enabled)
        {
            row.MouseEnter += (_, _) => row.Background = Theme.SurfaceHover;
            row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
            row.MouseLeftButtonUp += (_, _) =>
            {
                CloseAll();
                if (mi.Command != null && mi.Command.CanExecute(mi.CommandParameter))
                    mi.Command.Execute(mi.CommandParameter);
                else
                    mi.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            };
        }
        return row;
    }

    // ---------- Recherche ----------

    private UIElement BuildSearchBox()
    {
        var box = new TextBox
        {
            Background = Brushes.Transparent,
            Foreground = Theme.Text,
            CaretBrush = Theme.Text,
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(30, 0, 8, 0),
            FontSize = 13
        };

        var hint = new TextBlock
        {
            Text = "Rechercher (Ctrl+K)",
            Foreground = Theme.Muted,
            FontSize = 13,
            Margin = new Thickness(30, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };

        var icon = new TextBlock
        {
            Text = "⌕",
            Foreground = Theme.Muted,
            FontSize = 16,
            Margin = new Thickness(10, 0, 0, 2),
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };

        box.TextChanged += (_, _) =>
            hint.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        box.GotKeyboardFocus += (_, _) => _onSearch();
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) _onSearch(); };

        var host = new Grid();
        host.Children.Add(box);
        host.Children.Add(hint);
        host.Children.Add(icon);

        return new Border
        {
            Background = Theme.Sidebar,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Height = 28,
            VerticalAlignment = VerticalAlignment.Center,
            Child = host
        };
    }
}