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

        _views["explorer"] = Explorer;

        Search = new SearchPanel(() => Explorer.CurrentFolder);
        _views["search"] = Search;

        // Contrôle de source (Git) réel et complet
        SourceControl = new GitPanel(() => Explorer.CurrentFolder);
        _views["git"] = SourceControl;

        Explorer.CurrentPathChanged += _ => SourceControl.OnFolderChanged();
        SourceControl.ChooseFolderRequested += Explorer.ChooseFolder;
        SourceControl.FolderOpenRequested += Explorer.OpenFolder;

        _views["settings"] = Settings;

        // TODO: Implémenter le gestionnaire d'extensions et thèmes personnalisés.
        _views["extensions"] = Placeholder("Extensions : fonctionnalité non disponible pour le moment.");

        var dock = new DockPanel();
        DockPanel.SetDock(_title, Dock.Top);
        dock.Children.Add(_title);
        dock.Children.Add(_content);

        Child = dock;

        ShowView("explorer");
    }

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
