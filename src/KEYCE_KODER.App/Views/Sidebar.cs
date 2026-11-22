using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KEYCE_KODER.App.Views;

public class Sidebar : Border
{
    private readonly TextBlock _title = new()
    {
        Foreground = Theme.Muted,
        FontWeight = FontWeights.SemiBold,
        FontSize = 11,
        Margin = new Thickness(16, 16, 16, 10)
    };

    private readonly ContentControl _content = new();
    private readonly Dictionary<string, UIElement> _views = new();

    public ExplorerView Explorer { get; } = new();
    public SearchView Search { get; } = new();
    public WelcomeView Welcome { get; } = new();
    public SettingsView Settings { get; } = new();

    public Sidebar()
    {
        Width = 264;
        Background = Theme.Sidebar;
        BorderBrush = Theme.Border;
        BorderThickness = new Thickness(0, 0, 1, 0);

        _views["search"] = Search;
        _views["home"] = Welcome;
        _views["settings"] = Settings;

        var dock = new DockPanel();

        DockPanel.SetDock(_title, Dock.Top);
        dock.Children.Add(_title);
        dock.Children.Add(_content);

        Child = dock;
        ShowView("search");
    }

    public void ShowView(string id)
    {
        _title.Text = id switch
        {
            "explorer" => "EXPLORATEUR",
            "search" => "RECHERCHE",
            "home" => "ACCUEIL",
            "settings" => "PARAMÈTRES",
            _ => id.ToUpperInvariant()
        };

        _content.Content = _views.TryGetValue(id, out var view)
            ? view
            : null;

        if (id == "search")
            Search.SetFolder(Explorer.CurrentPath);
    }

    public void Toggle()
    {
        Visibility = Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}
