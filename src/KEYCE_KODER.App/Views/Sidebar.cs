using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KEYCE_KODER.App.Git;
using KEYCE_KODER.App.Search;

namespace KEYCE_KODER.App.Views;

public class Sidebar : Border
{
    private readonly TextBlock _title = new()
    {
        Foreground = Theme.Muted,
        FontWeight = FontWeights.SemiBold,
        FontSize = 11,
        Margin = new Thickness(10, 10, 10, 6)
    };

    private readonly ContentControl _content = new();
    private readonly Dictionary<string, UIElement> _views = new();
    private readonly Dictionary<string, string> _titles = new()
    {
        ["explorer"] = "EXPLORATEUR",
        ["search"] = "RECHERCHE",
        ["git"] = "CONTRÔLE DE SOURCE",
        ["debug"] = "EXÉCUTER ET DÉBOGUER",
        ["settings"] = "PARAMÈTRES",
        ["tools"] = "LANGAGES ET OUTILS",
        ["account"] = "COMPTE",
        ["extensions"] = "EXTENSIONS"
    };

    private string _currentView = "explorer";

    public ExplorerView Explorer { get; } = new();
    public SearchPanel Search { get; }
    public GitPanel SourceControl { get; }
    public SettingsView Settings { get; } = new();

    public event Action? OpenToolsRequested;

    public Sidebar()
    {
        Width = 220; // Largeur compacte pour maximiser l'espace de l'éditeur
        Background = Theme.Sidebar;
        BorderBrush = Theme.Border;
        BorderThickness = new Thickness(0, 0, 1, 0);

        _views["explorer"] = BuildExplorerHost();

        Search = new SearchPanel(() => Explorer.CurrentFolder);
        _views["search"] = Search;

        // Contrôle de source (Git) réel et complet
        SourceControl = new GitPanel(() => Explorer.CurrentFolder);
        _views["git"] = SourceControl;

        Explorer.CurrentPathChanged += _ => SourceControl.OnFolderChanged();
        SourceControl.ChooseFolderRequested += Explorer.ChooseFolder;
        SourceControl.FolderOpenRequested += Explorer.OpenFolder;

        _views["settings"] = Settings;

        // Vues complémentaires
        _views["account"] = Placeholder("Gestion de profil et synchronisation des paramètres.");
        _views["extensions"] = Placeholder("Gestionnaire d'extensions et thèmes personnalisés.");

        var dock = new DockPanel();
        DockPanel.SetDock(_title, Dock.Top);
        dock.Children.Add(_title);
        dock.Children.Add(_content);

        Child = dock;

        ShowView("explorer");
    }

    /// <summary>Explorateur en haut avec sections OUTLINE et TIMELINE repliables en bas (design Astou).</summary>
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
        Padding = new Thickness(8, 4, 8, 4),
        BorderBrush = Theme.Border,
        BorderThickness = new Thickness(0, 1, 0, 0),
        IsExpanded = false
    };

    private static UIElement Placeholder(string message) => new TextBlock
    {
        Text = message,
        Foreground = Theme.Muted,
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(12, 8, 12, 0)
    };

    /// <summary>Ajoute ou remplace une vue personnalisée (ex. « debug » : Exécuter et déboguer).</summary>
    public void AddView(string id, string title, UIElement view)
    {
        _views[id] = view;
        _titles[id] = title;

        if (_currentView == id)
            ShowView(id);
    }

    public void ShowView(string id)
    {
        if (id == "tools")
        {
            OpenToolsRequested?.Invoke();
            return;
        }

        _currentView = id;
        Visibility = Visibility.Visible;

        _title.Text = _titles.TryGetValue(id, out var title)
            ? title
            : id.ToUpperInvariant();

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
