using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KEYCE_KODER.App.Search;
using KEYCE_KODER.App.Themes;

namespace KEYCE_KODER.App.Views;

public class Sidebar : Border
{
    private readonly TextBlock _title = new()
    {
        Foreground = ThemeColors.TextPrimary,
        FontWeight = FontWeights.SemiBold,
        FontSize = 11,
        Margin = new Thickness(16, 14, 10, 14)
    };

    private readonly ContentControl _content = new();
    private readonly Dictionary<string, UIElement> _views = new();

    public ExplorerView Explorer { get; } = new();

    public SearchPanel Search { get; }

    public Sidebar()
    {
        Width = 250;
        Background = ThemeColors.Sidebar;
        BorderBrush = ThemeColors.Border;
        BorderThickness = new Thickness(0, 0, 1, 0);

        _content.Background = ThemeColors.Sidebar;

        _views["explorer"] = Explorer;
        Search = new SearchPanel(() => Explorer.CurrentFolder);
        _views["search"] = Search;

        var dock = new DockPanel();

        DockPanel.SetDock(_title, Dock.Top);
        dock.Children.Add(_title);
        dock.Children.Add(_content);

        Child = dock;

        ShowView("explorer");
    }

    public void ShowView(string id)
    {
        _title.Text = id switch
        {
            "explorer" => "EXPLORATEUR",
            "search" => "RECHERCHE",
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