using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace Keyce_code.App.Views;

public class ExplorerView : DockPanel
{
    private const string Placeholder = "__placeholder__";
    private static readonly string[] Ignored = { "bin", "obj", ".git", ".vs", "node_modules" };

    // Envoyé quand on double-clique sur un fichier
    public event Action<string>? FileOpenRequested;

    private readonly TreeView _tree = new()
    {
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Foreground = Brushes.White
    };

    private string? _currentPath;

    public ExplorerView()
    {
        var openButton = new Button
        {
            Content = "📂 Ouvrir un dossier",
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 6, 0)
        };
        openButton.Click += (s, e) => ChooseFolder();

        var refreshButton = new Button
        {
            Content = "⟳",
            ToolTip = "Actualiser",
            Width = 32,
            Padding = new Thickness(0, 4, 0, 4)
        };
        refreshButton.Click += (s, e) =>
        {
            if (_currentPath != null)
                OpenFolder(_currentPath);
        };

        // Les deux boutons sur une seule ligne
        var toolbar = new DockPanel { Margin = new Thickness(10, 0, 10, 8) };
        DockPanel.SetDock(refreshButton, Dock.Right);
        toolbar.Children.Add(refreshButton);
        toolbar.Children.Add(openButton);

        DockPanel.SetDock(toolbar, Dock.Top);
        Children.Add(toolbar);
        Children.Add(_tree);
    }

    private void ChooseFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Choisir un dossier de projet" };
        if (dialog.ShowDialog() == true)
            OpenFolder(dialog.FolderName);
    }

    public void OpenFolder(string path)
    {
        _currentPath = path;
        _tree.Items.Clear();

        var root = CreateNode(path, true);
        root.Header = "📂 " + Path.GetFileName(path.TrimEnd('\\', '/'));
        _tree.Items.Add(root);
        root.IsExpanded = true;
    }

    private TreeViewItem CreateNode(string path, bool isDirectory)
    {
        var node = new TreeViewItem
        {
            Header = (isDirectory ? "📁 " : "📄 ") + Path.GetFileName(path),
            Tag = path,
            Foreground = Brushes.White
        };

        if (isDirectory)
        {
            // Faux enfant : fait apparaître la flèche de dépliage
            node.Items.Add(new TreeViewItem { Tag = Placeholder });
            node.Expanded += OnExpanded;
        }
        else
        {
            node.MouseDoubleClick += (s, e) =>
            {
                FileOpenRequested?.Invoke(path);
                e.Handled = true;
            };
        }

        return node;
    }

    private void OnExpanded(object sender, RoutedEventArgs e)
    {
        var node = (TreeViewItem)sender;

        // On charge le contenu une seule fois, à la première ouverture
        bool notLoaded = node.Items.Count == 1
            && node.Items[0] is TreeViewItem child
            && child.Tag is string tag
            && tag == Placeholder;

        if (!notLoaded)
            return;

        node.Items.Clear();
        var path = (string)node.Tag;

        try
        {
            var dirs = Directory.EnumerateDirectories(path)
                .Where(d => !Ignored.Contains(Path.GetFileName(d)))
                .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase);

            foreach (var d in dirs)
                node.Items.Add(CreateNode(d, true));

            var files = Directory.EnumerateFiles(path)
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase);

            foreach (var f in files)
                node.Items.Add(CreateNode(f, false));
        }
        catch (UnauthorizedAccessException)
        {
            node.Items.Add(new TreeViewItem { Header = "(accès refusé)", Foreground = Brushes.Gray });
        }
        catch (IOException)
        {
            node.Items.Add(new TreeViewItem { Header = "(erreur de lecture)", Foreground = Brushes.Gray });
        }
    }
}