using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
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
        Padding = new Thickness(4, 2, 4, 4)
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
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 0, 6, 0),
            Foreground = Theme.Text,
            Background = Theme.Surface,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand
        };

        openButton.Click += (_, _) => ChooseFolder();

        var refreshButton = new Button
        {
            Content = SvgIconFactory.Create("refresh", 17),
            ToolTip = "Actualiser",
            Width = 32,
            Height = 32,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = Theme.Muted,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand
        };

        refreshButton.Click += (_, _) =>
        {
            if (_currentPath != null)
                OpenFolder(_currentPath);
        };

        var toolbar = new DockPanel
        {
            Margin = new Thickness(10, 2, 10, 10)
        };

        DockPanel.SetDock(refreshButton, Dock.Right);
        toolbar.Children.Add(refreshButton);
        toolbar.Children.Add(openButton);

        DockPanel.SetDock(toolbar, Dock.Top);
        Children.Add(toolbar);
        Children.Add(_tree);
    }

    public void ChooseFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choisir un dossier de projet"
        };

        if (dialog.ShowDialog() == true)
            OpenFolder(dialog.FolderName);
    }

    private static StackPanel CreateHeader(string text, string icon)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        panel.Children.Add(SvgIconFactory.Create(icon, 15));
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
            FontSize = 10,
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
        FontSize = 12,
        Margin = new Thickness(7, 1, 0, 1),
        VerticalAlignment = VerticalAlignment.Center
    };

    public void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !Directory.Exists(path))
        {
            MessageBox.Show(
                "Le dossier sélectionné n'existe pas ou n'est pas accessible.",
                "Explorateur",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _currentPath = Path.GetFullPath(path);
        CurrentPathChanged?.Invoke(_currentPath);
        _tree.Items.Clear();

        string folderName = Path.GetFileName(
            Path.TrimEndingDirectorySeparator(_currentPath));

        if (string.IsNullOrWhiteSpace(folderName))
            folderName = _currentPath;

        var root = CreateNode(_currentPath, true);
        root.Header = CreateHeader(folderName, "folder");

        _tree.Items.Add(root);
        root.IsExpanded = true;

        CurrentPathChanged?.Invoke(_currentPath);
    }

    private TreeViewItem CreateNode(string path, bool isDirectory)
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
        {
            node.Items.Add(new TreeViewItem { Tag = Placeholder });
            node.Expanded += OnExpanded;
        }
        else
        {
            node.MouseDoubleClick += (_, e) =>
            {
                FileOpenRequested?.Invoke(path);
                e.Handled = true;
            };
        }

        return node;
    }

    private void OnExpanded(object sender, RoutedEventArgs e)
    {
        if (sender is not TreeViewItem node)
            return;

        bool notLoaded =
            node.Items.Count == 1 &&
            node.Items[0] is TreeViewItem child &&
            child.Tag is string tag &&
            tag == Placeholder;

        if (!notLoaded)
            return;

        node.Items.Clear();

        if (node.Tag is not string path || !Directory.Exists(path))
        {
            AddErrorNode(node, "(dossier introuvable)");
            return;
        }

        try
        {
            var directories = Directory.EnumerateDirectories(path)
                .Where(d => !Ignored.Contains(Path.GetFileName(d)))
                .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase);

            foreach (var directory in directories)
                node.Items.Add(CreateNode(directory, true));

            var files = Directory.EnumerateFiles(path)
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
                node.Items.Add(CreateNode(file, false));
        }
        catch (UnauthorizedAccessException) { AddErrorNode(node, "(accès refusé)"); }
        catch (SecurityException) { AddErrorNode(node, "(accès interdit)"); }
        catch (IOException) { AddErrorNode(node, "(erreur de lecture)"); }
    }

    private static void AddErrorNode(TreeViewItem parent, string message)
    {
        parent.Items.Add(new TreeViewItem
        {
            Header = message,
            Foreground = Theme.Muted,
            IsEnabled = false
        });
    }
}