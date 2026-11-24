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

/// <summary>
/// Page d'accueil moderne de KEYCE KODER :
/// Présentation obligatoire en deux colonnes :
/// - Colonne gauche : Logo KEYCE KODER de grande taille, visuellement dominant.
/// - Colonne droite : Titre de bienvenue, actions réelles (Projet, Fichier, Dossier) et projets récents.
/// </summary>
public sealed class WelcomeView : Border
{
    public event Action? NewFileRequested;
    public event Action? OpenFileRequested;
    public event Action? OpenFolderRequested;
    public event Action? NewFolderRequested;
    public event Action<string>? OpenRecentFolderRequested;

    private readonly StackPanel _recentList = new();
    private readonly TextBlock _welcomeTitle = new();
    private readonly TextBlock _welcomeSubtitle = new();
    private readonly TextBlock _actionsSectionTitle = new();
    private readonly TextBlock _recentsSectionTitle = new();

    public WelcomeView()
    {
        Background = Theme.Editor;
        Padding = new Thickness(40, 24, 40, 24);

        // Réaction dynamique au thème
        Theme.ThemeChanged += _ =>
        {
            Background = Theme.Editor;
            _welcomeTitle.Foreground = Theme.Text;
            _welcomeSubtitle.Foreground = Theme.Muted;
            _actionsSectionTitle.Foreground = Theme.Accent;
            _recentsSectionTitle.Foreground = Theme.Accent;
            RefreshRecentProjects();
        };

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        // Conteneur principal centré horizontalement et verticalement
        var mainGrid = new Grid
        {
            MaxWidth = 960,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 24, 16, 24)
        };

        // Deux colonnes : Gauche = Logo dominant | Droite = Titre + Actions + Récents
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) }); // Séparation visuelle
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // =========================================================================
        // COLONNE GAUCHE : UNIQUEMENT LE LOGO KEYCE KODER (GRAND ET DOMINANT)
        // =========================================================================
        var logoHost = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(16)
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
                bitmap.DecodePixelWidth = 512;
                bitmap.EndInit();
                bitmap.Freeze();

                var logoImage = new Image
                {
                    Source = bitmap,
                    Width = 240,
                    Height = 240,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                logoHost.Child = logoImage;
            }
        }
        catch
        {
            // Repli gracieux en cas d'absence du logo
        }

        Grid.SetColumn(logoHost, 0);
        mainGrid.Children.Add(logoHost);

        // =========================================================================
        // COLONNE DROITE : TITRE + ACTIONS + PROJETS RÉCENTS
        // =========================================================================
        var rightColumn = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // 1. En-tête : Titre de bienvenue & Sous-titre
        _welcomeTitle.Text = "Bienvenue dans KEYCE KODER";
        _welcomeTitle.FontSize = 26;
        _welcomeTitle.FontWeight = FontWeights.Bold;
        _welcomeTitle.Foreground = Theme.Text;
        _welcomeTitle.Margin = new Thickness(0, 0, 0, 4);

        _welcomeSubtitle.Text = "Environnement de développement & Éditeur de code C# / .NET 10";
        _welcomeSubtitle.FontSize = 13;
        _welcomeSubtitle.Foreground = Theme.Muted;
        _welcomeSubtitle.Margin = new Thickness(0, 0, 0, 24);

        rightColumn.Children.Add(_welcomeTitle);
        rightColumn.Children.Add(_welcomeSubtitle);

        // 2. Section COMMENCER (Actions réelles avec icônes SVG)
        _actionsSectionTitle.Text = "COMMENCER";
        _actionsSectionTitle.FontSize = 11;
        _actionsSectionTitle.FontWeight = FontWeights.SemiBold;
        _actionsSectionTitle.Foreground = Theme.Accent;
        _actionsSectionTitle.Margin = new Thickness(0, 0, 0, 10);
        rightColumn.Children.Add(_actionsSectionTitle);

        rightColumn.Children.Add(MakeActionButton("Ouvrir un projet…", "", "folder", () => OpenFolderRequested?.Invoke()));
        rightColumn.Children.Add(MakeActionButton("Ouvrir un fichier…", "Ctrl+O", "file", () => OpenFileRequested?.Invoke()));
        rightColumn.Children.Add(MakeActionButton("Créer un dossier…", "", "folder", () => NewFolderRequested?.Invoke()));
        rightColumn.Children.Add(MakeActionButton("Créer un fichier", "Ctrl+N", "file", () => NewFileRequested?.Invoke()));

        // 3. Section PROJETS RÉCENTS
        _recentsSectionTitle.Text = "PROJETS RÉCENTS";
        _recentsSectionTitle.FontSize = 11;
        _recentsSectionTitle.FontWeight = FontWeights.SemiBold;
        _recentsSectionTitle.Foreground = Theme.Accent;
        _recentsSectionTitle.Margin = new Thickness(0, 20, 0, 10);
        rightColumn.Children.Add(_recentsSectionTitle);

        _recentList.Margin = new Thickness(0, 0, 0, 16);
        rightColumn.Children.Add(_recentList);

        // 4. Barre de raccourcis essentiels
        var shortcutRow = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        shortcutRow.Children.Add(MakeShortcutBadge("Recherche", "Ctrl+K"));
        shortcutRow.Children.Add(MakeShortcutBadge("Barre latérale", "Ctrl+B"));
        shortcutRow.Children.Add(MakeShortcutBadge("Run / Debug", "F5"));
        shortcutRow.Children.Add(MakeShortcutBadge("Assistant IA", "Ctrl+I"));
        rightColumn.Children.Add(shortcutRow);

        Grid.SetColumn(rightColumn, 2);
        mainGrid.Children.Add(rightColumn);

        scroll.Content = mainGrid;
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
            Height = 36,
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
        iconElem.Margin = new Thickness(0, 0, 10, 0);
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
                Text = "Aucun projet récent pour le moment.",
                Foreground = Theme.Muted,
                FontStyle = FontStyles.Italic,
                FontSize = 12,
                Margin = new Thickness(4, 2, 0, 4)
            });
            return;
        }

        foreach (var path in recents.Take(5))
        {
            string folderName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(folderName)) folderName = path;

            var itemBorder = new Border
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                Cursor = Cursors.Hand,
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 4)
            };

            var dock = new DockPanel();
            var iconElem = SvgIconFactory.Create("folder", 14);
            iconElem.Margin = new Thickness(0, 0, 8, 0);
            iconElem.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(iconElem, Dock.Left);
            dock.Children.Add(iconElem);

            var textSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textSp.Children.Add(new TextBlock
            {
                Text = folderName,
                Foreground = Theme.Text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            });

            textSp.Children.Add(new TextBlock
            {
                Text = path,
                Foreground = Theme.Muted,
                FontSize = 10,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            dock.Children.Add(textSp);
            itemBorder.Child = dock;

            itemBorder.MouseEnter += (_, _) => itemBorder.Background = Theme.SurfaceHover;
            itemBorder.MouseLeave += (_, _) => itemBorder.Background = Brushes.Transparent;
            itemBorder.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                OpenRecentFolderRequested?.Invoke(path);
            };

            _recentList.Children.Add(itemBorder);
        }
    }

    private static Border MakeShortcutBadge(string label, string keys)
    {
        var border = new Border
        {
            Background = Theme.Surface,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 8, 6)
        };

        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(new TextBlock { Text = label + " : ", Foreground = Theme.Muted, FontSize = 11 });
        sp.Children.Add(new TextBlock { Text = keys, Foreground = Theme.Accent, FontWeight = FontWeights.SemiBold, FontSize = 11 });
        border.Child = sp;

        return border;
    }
}
