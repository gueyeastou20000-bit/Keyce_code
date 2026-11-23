using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace KEYCE_KODER.App.Views;

/// <summary>Barre du haut : bouton ☰ (menu sombre) + champ de recherche centré.</summary>
public class TopBar : Border
{
    private readonly IReadOnlyList<MenuItem> _source;
    private readonly Action _onSearch;
    private readonly Button _burger;
    private readonly Popup _menuPopup = new();
    private readonly Popup _subPopup = new();
    private readonly StackPanel _menuPanel = new();
    private readonly StackPanel _subPanel = new();
    private MenuItem? _openTop;
    private Window? _window;

    public TopBar(IReadOnlyList<MenuItem> source, Action onSearch)
    {
        _source = source;
        _onSearch = onSearch;

        Background = Theme.Window;
        Height = 42;

        _burger = new Button
        {
            Content = "☰",
            FontSize = 18,
            Width = 40,
            Height = 30,
            Margin = new Thickness(8, 0, 0, 0),
            Foreground = Theme.Text,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = "Menu"
        };
        _burger.Click += (_, _) => ToggleMenu();

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _burger.HorizontalAlignment = HorizontalAlignment.Left;
        _burger.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_burger, 0);
        grid.Children.Add(_burger);

        var search = BuildSearchBox();
        Grid.SetColumn(search, 1);
        grid.Children.Add(search);

        Child = grid;

        // Popups
        _menuPopup.StaysOpen = true;
        _menuPopup.AllowsTransparency = true;
        _menuPopup.PlacementTarget = _burger;
        _menuPopup.Placement = PlacementMode.Bottom;
        _menuPopup.Child = WrapPopup(_menuPanel, 230);

        _subPopup.StaysOpen = true;
        _subPopup.AllowsTransparency = true;
        _subPopup.Placement = PlacementMode.Right;
        _subPopup.Child = WrapPopup(_subPanel, 260);
    }

    private static Border WrapPopup(Panel content, double minWidth) => new()
    {
        Background = Theme.Surface,
        BorderBrush = Theme.Border,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(4),
        Margin = new Thickness(6),
        MinWidth = minWidth,
        Child = content,
        Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 12, ShadowDepth = 2, Opacity = 0.5
        }
    };

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
            Text = "Rechercher dans le projet…",
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

    // ---------- Menu principal ----------

    private void ToggleMenu()
    {
        if (_menuPopup.IsOpen) { CloseAll(); return; }

        _menuPanel.Children.Clear();
        foreach (MenuItem top in _source)
            _menuPanel.Children.Add(MakeTopRow(top));

        _window = Window.GetWindow(this);
        if (_window != null)
        {
            _window.PreviewMouseDown += OnWindowMouseDown;
            _window.Deactivated += OnWindowDeactivated;
        }
        _menuPopup.IsOpen = true;
    }

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_burger.IsMouseOver) CloseAll();
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) => CloseAll();

    private void CloseAll()
    {
        _subPopup.IsOpen = false;
        _menuPopup.IsOpen = false;
        _openTop = null;
        if (_window != null)
        {
            _window.PreviewMouseDown -= OnWindowMouseDown;
            _window.Deactivated -= OnWindowDeactivated;
            _window = null;
        }
    }

    private static string Clean(object? header) =>
        (header?.ToString() ?? string.Empty).Replace("_", string.Empty);

    private UIElement MakeTopRow(MenuItem top)
    {
        var row = MakeRow(Clean(top.Header), "›", true);
        row.MouseEnter += (_, _) => OpenSubMenu(top, row);
        return row;
    }

    private void OpenSubMenu(MenuItem top, FrameworkElement row)
    {
        if (ReferenceEquals(_openTop, top) && _subPopup.IsOpen) return;
        _openTop = top;

        // Laisse le code existant mettre à jour les éléments activés/désactivés
        top.RaiseEvent(new RoutedEventArgs(MenuItem.SubmenuOpenedEvent));

        _subPanel.Children.Clear();
        foreach (object child in top.Items)
        {
            if (child is Separator)
            {
                _subPanel.Children.Add(new Border
                {
                    Height = 1,
                    Background = Theme.Border,
                    Margin = new Thickness(6, 4, 6, 4)
                });
            }
            else if (child is MenuItem mi)
            {
                _subPanel.Children.Add(MakeSubRow(mi));
            }
        }

        _subPopup.IsOpen = false;
        _subPopup.PlacementTarget = row;
        _subPopup.IsOpen = _subPanel.Children.Count > 0;
    }

    private UIElement MakeSubRow(MenuItem mi)
    {
        bool enabled = mi.IsEnabled;
        var row = MakeRow(Clean(mi.Header), mi.InputGestureText ?? string.Empty, enabled);

        if (enabled)
        {
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

    private static Border MakeRow(string text, string right, bool enabled)
    {
        var dock = new DockPanel { LastChildFill = false };

        var rightText = new TextBlock
        {
            Text = right,
            Foreground = Theme.Muted,
            FontSize = 12,
            Margin = new Thickness(24, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(rightText, Dock.Right);
        dock.Children.Add(rightText);

        dock.Children.Add(new TextBlock
        {
            Text = text,
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
        }
        return row;
    }
}