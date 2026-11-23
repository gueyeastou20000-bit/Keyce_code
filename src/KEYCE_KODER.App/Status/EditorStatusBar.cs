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
    private readonly TextBlock _branch = MakeText();
    private readonly TextBlock _file = MakeText();
    private readonly TextBlock _language = MakeText();
    private readonly TextBlock _position = MakeText();
    private readonly TextBlock _encoding = MakeText();
    private readonly TextBlock _bell = MakeText();

    public EditorStatusBar()
    {
        Background = Theme.Accent;
        Padding = new Thickness(10, 3, 10, 3);

        _branch.Margin = new Thickness(0, 0, 20, 0);
        _language.Margin = new Thickness(0, 0, 20, 0);
        _position.Margin = new Thickness(0, 0, 20, 0);
        _encoding.Margin = new Thickness(0, 0, 20, 0);
        _encoding.Text = "UTF-8";
        _bell.Text = "🔔";

        var right = new StackPanel { Orientation = Orientation.Horizontal };
        right.Children.Add(_language);
        right.Children.Add(_position);
        right.Children.Add(_encoding);
        right.Children.Add(_bell);

        var left = new StackPanel { Orientation = Orientation.Horizontal };
        left.Children.Add(_branch);
        left.Children.Add(_file);

        var dock = new DockPanel();
        DockPanel.SetDock(right, Dock.Right);
        dock.Children.Add(right);
        dock.Children.Add(left);

        Child = dock;
        _branch.Text = "⎇ " + FindGitBranch(null);
    }

    /// <summary>Met la barre à jour à partir de l'onglet actif (ou d'aucun onglet).</summary>
    public void Update(EditorTab? tab)
    {
        if (tab is null)
        {
            _file.Text = "Aucun fichier ouvert";
            _language.Text = string.Empty;
            _position.Text = string.Empty;
            return;
        }

        string path = tab.Document.FilePath ?? "Fichier non enregistré";
        _file.Text = tab.Document.IsDirty ? $"{path}  •  modifié" : path;

        _branch.Text = "⎇ " + FindGitBranch(tab.Document.FilePath);
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
            // lecture impossible : on affiche le texte par défaut
        }
        return "(pas de Git)";
    }

    private static TextBlock MakeText() => new()
    {
        Foreground = Brushes.White,
        FontSize = 12,
        VerticalAlignment = VerticalAlignment.Center
    };
}