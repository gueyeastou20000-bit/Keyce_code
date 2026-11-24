using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace KEYCE_KODER.App.Views;

public class ExplorerView : DockPanel
{
    private const string Placeholder = "__placeholder__";

    private static readonly HashSet<string> Ignored =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", ".git", ".vs", "node_modules"
        };

    // Extension -> (texte de la pastille, couleur)
    private static readonly Dictionary<string, (string Label, Color Color)> FileBadges =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".cs"] = ("C#", Color.FromRgb(104, 217, 120)),
            [".js"] = ("JS", Color.FromRgb(247, 223, 30)),
            [".ts"] = ("TS", Color.FromRgb(49, 120, 198)),
            [".json"] = ("{}", Color.FromRgb(250, 204, 21)),
            [".html"] = ("<>", Color.FromRgb(251, 146, 60)),
            [".htm"] = ("<>", Color.FromRgb(251, 146, 60)),
            [".xml"] = ("<>", Color.FromRgb(251, 146, 60)),
            [".xaml"] = ("<>", Color.FromRgb(96, 170, 255)),
            [".css"] = ("#", Color.FromRgb(96, 170, 255)),
            [".py"] = ("PY", Color.FromRgb(96, 170, 255)),
            [".md"] = ("i", Color.FromRgb(96, 170, 255)),
            [".txt"] = ("≡", Color.FromRgb(145, 151, 167)),
            [".pdf"] = ("PDF", Color.FromRgb(242, 117, 119)),
            [".csproj"] = ("◆", Color.FromRgb(167, 139, 250)),
            [".slnx"] = ("◆", Color.FromRgb(167, 139, 250)),
            [".sln"] = ("◆", Color.FromRgb(167, 139, 250)),
            [".props"] = ("◆", Color.FromRgb(167, 139, 250)),
            [".gitignore"] = ("◆", Color.FromRgb(145, 151, 167)),
            [".png"] = ("▣", Color.FromRgb(80, 200, 151)),
            [".jpg"] = ("▣", Color.FromRgb(80, 200, 151)),
            [".jpeg"] = ("▣", Color.FromRgb(80, 200, 151)),
            [".svg"] = ("▣", Color.FromRgb(80, 200, 151)),
            [".ico"] = ("▣", Color.FromRgb(80, 200, 151)),
        };

    public event Action<string>? FileOpenRequested;
    public event Action<string>? CurrentPathChanged;

    private readonly TreeView _tree = new()
    {
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Foreground = Theme.Text,
        Padding = new Thickness(2, 2, 2, 4)
    };

    private string? _currentPath;

    public string? CurrentPath => _currentPath;

    /// <summary>Dossier actuellement ouvert dans l'explorateur (null si aucun).</summary>
    public string? CurrentFolder => _currentPath;

    public ExplorerView()
    {
        var openButton = new Button
        {
            Content = "Ouvrir un dossier",
            Padding = new Thickness(8, 5, 8, 5),
            Margin = new Thickness(0, 0, 4, 0),
            Foreground = Theme.Text,
            Background = Theme.Surface,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            FontSize = 11
        };

        openButton.Click += (_, _) => ChooseFolder();

        var refreshButton = new Button
        {
            Content = SvgIconFactory.Create("refresh", 15),
            ToolTip = "Actualiser",
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = Theme.Muted,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand
        };

        refreshButton.Click += (_, _) => Refresh();

        var header = new DockPanel
        {
            Margin = new Thickness(6, 0, 6, 6)
        };

        DockPanel.SetDock(refreshButton, Dock.Right);
        header.Children.Add(refreshButton);
        header.Children.Add(openButton);

        DockPanel.SetDock(header, Dock.Top);
        Children.Add(header);
        Children.Add(_tree);

        _tree.SelectedItemChanged += (_, _) =>
        {
            if (_tree.SelectedItem is TreeViewItem item &&
                item.Tag is string path &&
                File.Exists(path))
            {
                FileOpenRequested?.Invoke(path);
            }
        };

        _tree.AddHandler(
            TreeViewItem.ExpandedEvent,
            new RoutedEventHandler(OnItemExpanded));
    }

    private static StackPanel CreateHeader(string text, string icon)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        panel.Children.Add(SvgIconFactory.Create(icon, 14));
        panel.Children.Add(CreateLabel(text));

        return panel;
    }

    /// <summary>En-tête d'un fichier : pastille colorée selon l'extension + nom.</summary>
    private static StackPanel CreateFileHeader(string path)
    {
        string name = Path.GetFileName(path);
        string ext = Path.GetExtension(path);

        // ".gitignore" n'a pas d'extension pour .NET : on utilise le nom complet
        string key = string.IsNullOrEmpty(ext) ? name : ext;

        if (!FileBadges.TryGetValue(key, out var badge))
            return CreateHeader(name, "file");

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        panel.Children.Add(new TextBlock
        {
            Text = badge.Label,
            Foreground = new SolidColorBrush(badge.Color),
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Width = 18,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });

        panel.Children.Add(CreateLabel(name));
        return panel;
    }

    private static TextBlock CreateLabel(string text) => new()
    {
        Text = text,
        Foreground = Theme.Text,
        FontSize = 11,
        Margin = new Thickness(6, 1, 0, 1),
        VerticalAlignment = VerticalAlignment.Center
    };

    public void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !Directory.Exists(path))
        {
            return;
        }

        _currentPath = Path.GetFullPath(path);
        _tree.Items.Clear();

        var root = CreateNode(_currentPath, isDirectory: true);
        root.IsExpanded = true;
        _tree.Items.Add(root);

        CurrentPathChanged?.Invoke(_currentPath);
    }

    public void Refresh()
    {
        if (_currentPath is not null)
            OpenFolder(_currentPath);
    }

    public void ChooseFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Sélectionner un dossier"
        };

        if (dialog.ShowDialog() == true)
            OpenFolder(dialog.FolderName);
    }

    private static TreeViewItem CreateNode(string path, bool isDirectory)
    {
        var node = new TreeViewItem
        {
            Header = isDirectory
                ? CreateHeader(Path.GetFileName(path), "folder")
                : CreateFileHeader(path),
            Tag = path,
            Foreground = Theme.Text,
            Padding = new Thickness(2, 2, 4, 2)
        };

        if (isDirectory)
            node.Items.Add(new TreeViewItem { Tag = Placeholder });

        return node;
    }

    private static void OnItemExpanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not TreeViewItem item ||
            item.Tag is not string path ||
            !Directory.Exists(path))
        {
            return;
        }

        if (item.Items.Count != 1 ||
            item.Items[0] is not TreeViewItem child ||
            child.Tag as string != Placeholder)
        {
            return;
        }

        item.Items.Clear();

        try
        {
            foreach (var dir in Directory.GetDirectories(path)
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                if (!Ignored.Contains(Path.GetFileName(dir)))
                    item.Items.Add(CreateNode(dir, isDirectory: true));
            }

            foreach (var file in Directory.GetFiles(path)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                item.Items.Add(CreateNode(file, isDirectory: false));
            }
        }
        catch (Exception ex) when (
            ex is UnauthorizedAccessException or
            SecurityException or
            IOException)
        {
            item.Items.Add(new TreeViewItem
            {
                Header = CreateLabel("(Accès refusé)"),
                IsEnabled = false
            });
        }
    }
}
