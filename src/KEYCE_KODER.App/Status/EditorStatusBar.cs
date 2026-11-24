using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KEYCE_KODER.App.Tabs;
using KEYCE_KODER.App.Views;
using KEYCE_KODER.Core.Languages;

namespace KEYCE_KODER.App.Status;

public sealed class EditorStatusBar : Border
{
    private readonly StackPanel _branchPanel = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 16, 0) };
    private readonly TextBlock _branch = MakeText();
    private readonly TextBlock _file = MakeText();
    private readonly TextBlock _language = MakeText();
    private readonly TextBlock _position = MakeText();
    private readonly TextBlock _encoding = MakeText();
    private readonly Border _bellHost = new() { Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };

    public EditorStatusBar()
    {
        Background = Theme.Accent;
        Padding = new Thickness(10, 3, 10, 3);

        var gitIcon = SvgIconFactory.Create("git", 13);
        gitIcon.Margin = new Thickness(0, 0, 5, 0);
        gitIcon.VerticalAlignment = VerticalAlignment.Center;
        _branchPanel.Children.Add(gitIcon);
        _branchPanel.Children.Add(_branch);

        _language.Margin = new Thickness(0, 0, 16, 0);
        _position.Margin = new Thickness(0, 0, 16, 0);
        _encoding.Margin = new Thickness(0, 0, 16, 0);
        _encoding.Text = "UTF-8";

        var bellIcon = SvgIconFactory.Create("bell", 13);
        _bellHost.Child = bellIcon;

        var right = new StackPanel { Orientation = Orientation.Horizontal };
        right.Children.Add(_language);
        right.Children.Add(_position);
        right.Children.Add(_encoding);
        right.Children.Add(_bellHost);

        var left = new StackPanel { Orientation = Orientation.Horizontal };
        left.Children.Add(_branchPanel);
        left.Children.Add(_file);

        var dock = new DockPanel();
        DockPanel.SetDock(right, Dock.Right);
        dock.Children.Add(right);
        dock.Children.Add(left);

        Child = dock;
        _branch.Text = FindGitBranch(null);
    }

    /// <summary>Met la barre à jour à partir de l'onglet actif (ou d'aucun onglet).</summary>
    public void Update(EditorTab? tab)
    {
        if (tab is null)
        {
            _file.Text = "Prêt";
            _language.Text = string.Empty;
            _position.Text = string.Empty;
            _branch.Text = FindGitBranch(null);
            return;
        }

        string path = tab.Document.FilePath ?? "Fichier non enregistré";
        _file.Text = tab.Document.IsDirty ? $"{path}  •  modifié" : path;

        _branch.Text = FindGitBranch(tab.Document.FilePath);
        _language.Text = LanguageName(tab.Document.FilePath);
        _position.Text = $"Ln {tab.Actions.CaretLine}, Col {tab.Actions.CaretColumn}";
    }

    private static string LanguageName(string? filePath) =>
        LanguageCatalog.FromPath(filePath).DisplayName;

    /// <summary>Remonte les dossiers jusqu'à .git et lit la branche dans HEAD.</summary>
    private static string FindGitBranch(string? filePath)
    {
        try
        {
            string? dir = filePath is null
                ? Environment.CurrentDirectory
                : Path.GetDirectoryName(filePath);

            while (!string.IsNullOrEmpty(dir))
            {
                string head = Path.Combine(dir, ".git", "HEAD");
                if (File.Exists(head))
                {
                    string content = File.ReadAllText(head).Trim();
                    const string prefix = "ref: refs/heads/";
                    return content.StartsWith(prefix)
                        ? content[prefix.Length..]
                        : content[..Math.Min(7, content.Length)];
                }
                dir = Path.GetDirectoryName(dir);
            }
        }
        catch
        {
            // lecture impossible
        }
        return "main";
    }

    private static TextBlock MakeText() => new()
    {
        Foreground = Brushes.White,
        FontSize = 11,
        VerticalAlignment = VerticalAlignment.Center
    };
}
