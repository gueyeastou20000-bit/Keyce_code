using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KEYCE_KODER.App.Views;

public sealed class WelcomeView : StackPanel
{
    public event Action? OpenFolderRequested;
    public event Action<string>? OpenRecentFolderRequested;

    private readonly StackPanel _recentList = new();

    public WelcomeView()
    {
        Margin = new Thickness(16, 8, 16, 16);

        Children.Add(new TextBlock
        {
            Text = "BIENVENUE",
            Foreground = Theme.Text,
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 10)
        });

        Children.Add(new TextBlock
        {
            Text = "Commencez un nouveau projet ou ouvrez un dossier existant.",
            Foreground = Theme.Muted,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var open = new Button
        {
            Content = "Ouvrir un dossier",
            Height = 34,
            Background = Theme.Accent,
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        open.Click += (_, _) => OpenFolderRequested?.Invoke();
        Children.Add(open);

        Children.Add(new TextBlock
        {
            Text = "PROJETS RÉCENTS",
            Foreground = Theme.Muted,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 18, 0, 6)
        });
        _recentList.Margin = new Thickness(0, 0, 0, 8);
        Children.Add(_recentList);

        Children.Add(new TextBlock
        {
            Text = "RACCOURCIS",
            Foreground = Theme.Muted,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 24, 0, 8)
        });

        Children.Add(Shortcut("Palette de commandes", "Ctrl + Maj + P"));
        Children.Add(Shortcut("Afficher l'explorateur", "Ctrl + B"));
        Children.Add(Shortcut("Exécuter", "F5"));
        Children.Add(Shortcut("Enregistrer", "Ctrl + S"));
    }

    private static Grid Shortcut(string name, string keys)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nameBlock = new TextBlock
        {
            Text = name,
            Foreground = Theme.Text,
            FontSize = 12
        };
        var keyBlock = new TextBlock
        {
            Text = keys,
            Foreground = Theme.Muted,
            FontSize = 11
        };

        Grid.SetColumn(nameBlock, 0);
        Grid.SetColumn(keyBlock, 1);
        grid.Children.Add(nameBlock);
        grid.Children.Add(keyBlock);
        return grid;
    }

    public void AddRecentFolder(string folderPath)
    {
        var item = new Button
        {
            Content = folderPath,
            Style = (Style)Application.Current.Resources["RecentFolderButton"],
            Margin = new Thickness(0, 4, 0, 4),
            Cursor = Cursors.Hand
        };
        item.Click += (_, _) => OpenRecentFolderRequested?.Invoke(folderPath);

        _recentList.Children.Insert(0, item);

        if (_recentList.Children.Count > 5)
            _recentList.Children.RemoveAt(_recentList.Children.Count - 1);
    }

    public void SetRecentFolders(IEnumerable<string> folders)
    {
        _recentList.Children.Clear();
        foreach (var folder in folders)
        {
            var button = new Button
            {
                Content = folder,
                Height = 30,
                Margin = new Thickness(0, 2, 0, 2),
                Padding = new Thickness(8, 0, 8, 0),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Brushes.Transparent,
                Foreground = Theme.Text,
                BorderBrush = Theme.Border,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Cursor = Cursors.Hand
            };
            button.Click += (_, _) => OpenRecentFolderRequested?.Invoke(folder);
            _recentList.Children.Add(button);
        }
    }
}
