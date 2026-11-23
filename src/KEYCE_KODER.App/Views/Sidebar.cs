using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KEYCE_KODER.App.Search;

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
    public SearchPanel Search { get; }
    public WelcomeView Welcome { get; } = new();
    public SettingsView Settings { get; } = new();

    public Sidebar()
    {
        Width = 264;
        Background = Theme.Sidebar;
        BorderBrush = Theme.Border;
        BorderThickness = new Thickness(0, 0, 1, 0);

        _views["explorer"] = BuildExplorerHost();

        Search = new SearchPanel(() => Explorer.CurrentFolder);
        _views["search"] = Search;

        _views["home"] = Welcome;
        _views["settings"] = Settings;

        var dock = new DockPanel();

        DockPanel.SetDock(_title, Dock.Top);
        dock.Children.Add(_title);
        dock.Children.Add(_content);

        Child = dock;

        ShowView("explorer");
    }

    /// <summary>Explorateur en haut, sections OUTLINE et TIMELINE repliables en bas.</summary>
    private UIElement BuildExplorerHost()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Grid.SetRow(Explorer, 0);
        grid.Children.Add(Explorer);

        var sections = new StackPanel();
        sections.Children.Add(MakeSection("OUTLINE"));
        sections.Children.Add(MakeSection("TIMELINE"));
        Grid.SetRow(sections, 1);
        grid.Children.Add(sections);

        return grid;
    }

    private static Expander MakeSection(string title) => new()
    {
        Header = new TextBlock
        {
            Text = title,
            Foreground = Theme.Text,
            FontWeight = FontWeights.SemiBold,
            FontSize = 11
        },
        Foreground = Theme.Muted,
        Padding = new Thickness(8, 6, 8, 6),
        BorderBrush = Theme.Border,
        BorderThickness = new Thickness(0, 1, 0, 0),
        IsExpanded = false
    };

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
    }

    public void Toggle()
    {
        Visibility = Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}