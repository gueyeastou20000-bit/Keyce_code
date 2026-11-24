using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace KEYCE_KODER.App.Views;

/// <summary>Barre du haut : boutons de menu, recherche au centre, icônes SVG à droite.</summary>
public class TopBar : Border
{
    private readonly Action _onSearch;
    public event Action? SettingsRequested;

    private readonly Popup _popup = new();
    private readonly Popup _subPopup = new();
    private readonly StackPanel _panel = new();
    private readonly StackPanel _subPanel = new();
    private readonly List<(Border Button, MenuItem Menu)> _buttons = new();
    private MenuItem? _openMenu;
    private MenuItem? _openSub;
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

        // Menus (taille du contenu) | recherche (le reste) | icônes
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(menuStrip, 0);
        grid.Children.Add(menuStrip);

        UIElement search = BuildSearchBox();
        if (search is FrameworkElement sf)
        {
            sf.MaxWidth = 520;
            sf.Margin = new Thickness(12, 0, 12, 0);
        }
        Grid.SetColumn(search, 1);
        grid.Children.Add(search);

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };

        var settingsBtn = MakeSvgIconButton("settings", "Paramètres");
        settingsBtn.MouseLeftButtonUp += (_, _) => SettingsRequested?.Invoke();
        right.Children.Add(settingsBtn);

        var bellBtn = MakeSvgIconButton("bell", "Notifications");
        right.Children.Add(bellBtn);

        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        Child = grid;

        SetupPopup(_popup, _panel, PlacementMode.Bottom, 240);
        SetupPopup(_subPopup, _subPanel, PlacementMode.Right, 240);
    }

    private static void SetupPopup(Popup popup, Panel content, PlacementMode placement, double minWidth)
    {
        popup.StaysOpen = true;
        popup.AllowsTransparency = true;
        popup.Placement = placement;
        popup.Child = new Border
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
                BlurRadius = 12,
                ShadowDepth = 2,
                Opacity = 0.5
            }
        };
    }

    private static Border MakeSvgIconButton(string iconName, string tip)
    {
        var icon = SvgIconFactory.Create(iconName, 16);
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        icon.VerticalAlignment = VerticalAlignment.Center;

        var b = new Border
        {
            Width = 32,
            Height = 32,
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(4, 0, 0, 0),
            ToolTip = tip,
            Cursor = Cursors.Hand,
            Child = icon
        };

        b.MouseEnter += (_, _) => b.Background = Theme.SurfaceHover;
        b.MouseLeave += (_, _) => b.Background = Brushes.Transparent;
        return b;
    }

    // ---------- Boutons de menu ----------

    private Border MakeMenuButton(MenuItem top)
    {
        string title = Clean(top.Header);
        bool isMore = title == "⋯";

        var label = new TextBlock
        {
            Text = title,
            Foreground = Theme.Text,
            FontSize = isMore ? 16 : 12,
            VerticalAlignment = VerticalAlignment.Center
        };

        var button = new Border
        {
            Padding = new Thickness(8, 6, 8, 6),
            CornerRadius = new CornerRadius(6),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = label
        };

        button.MouseEnter += (_, _) =>
        {
            if (_popup.IsOpen) OpenMenu(top, button);
            else button.Background = Theme.SurfaceHover;
        };
        button.MouseLeave += (_, _) =>
        {
            if (!ReferenceEquals(_openMenu, top)) button.Background = Brushes.Transparent;
        };
        button.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            if (_popup.IsOpen && ReferenceEquals(_openMenu, top))
            {
                CloseAll();
            }
            else
            {
                OpenMenu(top, button);
            }
        };

        return button;
    }

    private void OpenMenu(MenuItem top, Border button)
    {
        _window ??= Window.GetWindow(this);
        if (_window != null)
        {
            _window.PreviewMouseDown -= OnWindowMouseDown;
            _window.PreviewMouseDown += OnWindowMouseDown;
            _window.Deactivated -= OnWindowDeactivated;
            _window.Deactivated += OnWindowDeactivated;
        }

        CloseSub();

        foreach (var (b, _) in _buttons) b.Background = Brushes.Transparent;
        button.Background = Theme.SurfaceHover;

        _openMenu = top;
        _panel.Children.Clear();

        foreach (var item in top.Items)
        {
            if (item is Separator)
            {
                _panel.Children.Add(new Border
                {
                    Height = 1,
                    Background = Theme.Border,
                    Margin = new Thickness(8, 4, 8, 4)
                });
            }
            else if (item is MenuItem mi)
            {
                _panel.Children.Add(MakeItemRow(mi, isMainLevel: true));
            }
        }

        _popup.PlacementTarget = button;
        _popup.IsOpen = _panel.Children.Count > 0;
    }

    private void OpenSub(MenuItem parent, FrameworkElement row)
    {
        _openSub = parent;
        _subPanel.Children.Clear();

        foreach (var item in parent.Items)
        {
            if (item is Separator)
            {
                _subPanel.Children.Add(new Border
                {
                    Height = 1,
                    Background = Theme.Border,
                    Margin = new Thickness(8, 4, 8, 4)
                });
            }
            else if (item is MenuItem mi)
            {
                _subPanel.Children.Add(MakeItemRow(mi, isMainLevel: false));
            }
        }

        _subPopup.IsOpen = false;
        _subPopup.PlacementTarget = row;
        _subPopup.IsOpen = _subPanel.Children.Count > 0;
    }

    private void CloseSub()
    {
        _subPopup.IsOpen = false;
        _openSub = null;
    }

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_popup.Child is FrameworkElement c && c.IsMouseOver) return;
        if (_subPopup.Child is FrameworkElement s && s.IsMouseOver) return;
        foreach (var (b, _) in _buttons) if (b.IsMouseOver) return;
        CloseAll();
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) => CloseAll();

    public void CloseAll()
    {
        CloseSub();
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

    private UIElement MakeItemRow(MenuItem mi, bool isMainLevel)
    {
        bool enabled = mi.IsEnabled;
        bool hasChildren = mi.Items.Count > 0;

        var dock = new DockPanel { LastChildFill = false };
        var right = new TextBlock
        {
            Text = hasChildren ? "›" : (mi.InputGestureText ?? string.Empty),
            Foreground = Theme.Muted,
            FontSize = hasChildren ? 16 : 12,
            Margin = new Thickness(24, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(right, Dock.Right);
        dock.Children.Add(right);
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

        if (!enabled) return row;

        row.MouseEnter += (_, _) =>
        {
            row.Background = Theme.SurfaceHover;
            if (isMainLevel)
            {
                if (hasChildren) OpenSub(mi, row);
                else CloseSub();
            }
        };
        row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;

        if (hasChildren)
        {
            row.MouseLeftButtonUp += (_, _) => OpenSub(mi, row);
        }
        else
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

        var icon = SvgIconFactory.Create("search", 14);
        icon.HorizontalAlignment = HorizontalAlignment.Left;
        icon.VerticalAlignment = VerticalAlignment.Center;
        icon.Margin = new Thickness(8, 0, 0, 0);
        icon.IsHitTestVisible = false;

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
