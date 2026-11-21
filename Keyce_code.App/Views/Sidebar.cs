using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Keyce_code.App.Views;

public class Sidebar : Border
{
    private readonly TextBlock _title = new()
    {
        Foreground = Brushes.White,
        FontWeight = FontWeights.Bold,
        Margin = new Thickness(10)
    };

    private readonly ContentControl _content = new();
    private readonly Dictionary<string, UIElement> _views = new();

    public ExplorerView Explorer { get; } = new();

    public Sidebar()
    {
        Width = 250;
        Background = new SolidColorBrush(Color.FromRgb(37, 37, 38));

        _views["explorer"] = Explorer;
        _views["search"] = new TextBlock
        {
            Text = "La recherche arrive bientôt...",
            Foreground = Brushes.Gray,
            Margin = new Thickness(10)
        };

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
            _ => id.ToUpper()
        };

        _content.Content = _views.TryGetValue(id, out var view) ? view : null;
    }

    // Afficher / cacher la Sidebar (Ctrl+B)
    public void Toggle()
    {
        Visibility = Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}