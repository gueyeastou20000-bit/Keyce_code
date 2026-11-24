using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KEYCE_KODER.App.Services;

namespace KEYCE_KODER.App.Views;

public sealed class WelcomeView : Border
{
    public event Action? NewFileRequested;
    public event Action? OpenFileRequested;
    public event Action? OpenFolderRequested;
    public event Action? NewFolderRequested;
    public event Action<string>? OpenRecentFolderRequested;

    private readonly StackPanel _recentList = new();

    public WelcomeView()
    {
        Background = Theme.Editor;
        Padding = new Thickness(32, 24, 32, 24);

        // Réaction dynamique au thème
        Theme.ThemeChanged += _ =>
        {
            Background = Theme.Editor;
            RefreshRecentProjects();
        };

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var container = new StackPanel
        {
            MaxWidth = 720,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // --- En-tête : Logo et Titres ---
        var headerSp = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 32)
        };

        try
        {
            string logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
            if (!File.Exists(logoPath))
                logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Assets", "logo.png");

            if (File.Exists(logoPath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(Path.GetFullPath(logoPath), UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 100;
                bitmap.EndInit();
                bitmap.Freeze();

                headerSp.Children.Add(new Image
                {
                    Source = bitmap,
                    Width = 54,
                    Height = 54,
                    Margin = new Thickness(0, 0, 0, 12),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
            }
        }
        catch
        {
            // Pas de logo
        }

        headerSp.Children.Add(new TextBlock
        {
            Text = "KEYCE KODER",
            FontSize = 26,
            FontWeight = FontWeights.Bold,
            Foreground = Theme.Text,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        });

        headerSp.Children.Add(new TextBlock
        {
            Text = "Environnement de développement & Éditeur de code WPF / .NET 10",
            FontSize = 13,
            Foreground = Theme.Muted,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        container.Children.Add(headerSp);

        // --- Corps à deux colonnes : Commencer / Récents ---
        var bodyGrid = new Grid();
        bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) }); // Espace
        bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Colonne gauche : Démarrer
        var leftSp = new StackPanel();
        leftSp.Children.Add(new TextBlock
        {
            Text = "COMMENCER",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Accent,
            Margin = new Thickness(0, 0, 0, 12)
        });

        leftSp.Children.Add(MakeActionButton("Nouveau fichier", "Ctrl+N", "file", () => NewFileRequested?.Invoke()));
        leftSp.Children.Add(MakeActionButton("Ouvrir un fichier…", "Ctrl+O", "file", () => OpenFileRequested?.Invoke()));
        leftSp.Children.Add(MakeActionButton("Ouvrir un dossier / projet…", "", "folder", () => OpenFolderRequested?.Invoke()));
        leftSp.Children.Add(MakeActionButton("Nouveau dossier…", "", "folder", () => NewFolderRequested?.Invoke()));

        Grid.SetColumn(leftSp, 0);
        bodyGrid.Children.Add(leftSp);

        // Colonne droite : Projets récents
        var rightSp = new StackPanel();
        rightSp.Children.Add(new TextBlock
        {
            Text = "PROJETS RÉCENTS",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Accent,
            Margin = new Thickness(0, 0, 0, 12)
        });

        _recentList.Margin = new Thickness(0, 0, 0, 8);
        rightSp.Children.Add(_recentList);

        Grid.SetColumn(rightSp, 2);
        bodyGrid.Children.Add(rightSp);

        container.Children.Add(bodyGrid);

        // --- Raccourcis utiles en bas ---
        var shortcutBorder = new Border
        {
            Background = Theme.Surface,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 32, 0, 0)
        };

        var shGrid = new Grid();
        shGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        shGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var shLeft = new StackPanel();
        shLeft.Children.Add(MakeShortcutRow("Recherche globale", "Ctrl + K"));
        shLeft.Children.Add(MakeShortcutRow("Afficher / Masquer la barre latérale", "Ctrl + B"));

        var shRight = new StackPanel();
        shRight.Children.Add(MakeShortcutRow("Exécuter le fichier actif (Run)", "F5"));
        shRight.Children.Add(MakeShortcutRow("Assistant IA Copilot", "Ctrl + I"));

        Grid.SetColumn(shLeft, 0);
        Grid.SetColumn(shRight, 1);
        shGrid.Children.Add(shLeft);
        shGrid.Children.Add(shRight);

        shortcutBorder.Child = shGrid;
        container.Children.Add(shortcutBorder);

        scroll.Content = container;
        Child = scroll;

        RefreshRecentProjects();
    }

    private Border MakeActionButton(string title, string gesture, string icon, Action onClick)
    {
        var btn = new Border
        {
            Background = Theme.Surface,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Height = 38,
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(12, 0, 12, 0),
            Cursor = Cursors.Hand
        };

        var dock = new DockPanel();

        if (!string.IsNullOrEmpty(gesture))
        {
            var gestBlock = new TextBlock
            {
                Text = gesture,
                Foreground = Theme.Muted,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(gestBlock, Dock.Right);
            dock.Children.Add(gestBlock);
        }

        var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var iconElem = SvgIconFactory.Create(icon, 15);
        iconElem.Margin = new Thickness(0, 0, 8, 0);
        sp.Children.Add(iconElem);

        sp.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = Theme.Text,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        });

        dock.Children.Add(sp);
        btn.Child = dock;

        btn.MouseEnter += (_, _) => btn.Background = Theme.SurfaceHover;
        btn.MouseLeave += (_, _) => btn.Background = Theme.Surface;
        btn.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            onClick();
        };
        return btn;
    }

    public void RefreshRecentProjects()
    {
        _recentList.Children.Clear();
        var recents = AppSettings.Current.RecentProjects;

        if (recents == null || recents.Count == 0)
        {
            _recentList.Children.Add(new TextBlock
            {
                Text = "Aucun dossier ou projet récent.",
                Foreground = Theme.Muted,
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 4)
            });
            return;
        }

        foreach (var path in recents.Take(6))
        {
            string folderName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(folderName)) folderName = path;

            var btn = new Button
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Padding = new Thickness(6, 6, 6, 6),
                Margin = new Thickness(0, 0, 0, 4),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = folderName,
                Foreground = Theme.Text,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold
            });

            sp.Children.Add(new TextBlock
            {
                Text = path,
                Foreground = Theme.Muted,
                FontSize = 10,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            btn.Content = sp;
            btn.MouseEnter += (_, _) => btn.Background = Theme.SurfaceHover;
            btn.MouseLeave += (_, _) => btn.Background = Brushes.Transparent;
            btn.Click += (_, _) => OpenRecentFolderRequested?.Invoke(path);

            _recentList.Children.Add(btn);
        }
    }

    private static Grid MakeShortcutRow(string name, string key)
    {
        var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nameBlock = new TextBlock
        {
            Text = name,
            Foreground = Theme.Text,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(nameBlock, 0);
        g.Children.Add(nameBlock);

        var keyBadge = new Border
        {
            Background = Theme.Editor,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1)
        };
        keyBadge.Child = new TextBlock
        {
            Text = key,
            Foreground = Theme.Accent,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold
        };
        Grid.SetColumn(keyBadge, 1);
        g.Children.Add(keyBadge);

        return g;
    }
}
