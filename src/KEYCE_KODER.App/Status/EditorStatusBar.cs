using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KEYCE_KODER.App.Tabs;

namespace KEYCE_KODER.App.Status;

public sealed class EditorStatusBar : Border
{
    private readonly TextBlock _file = MakeText();
    private readonly TextBlock _language = MakeText();
    private readonly TextBlock _position = MakeText();

    public EditorStatusBar()
    {
        Background = new SolidColorBrush(Color.FromRgb(0, 122, 204));
        Padding = new Thickness(10, 3, 10, 3);

        var right = new StackPanel { Orientation = Orientation.Horizontal };
        _language.Margin = new Thickness(0, 0, 20, 0);
        right.Children.Add(_language);
        right.Children.Add(_position);

        var dock = new DockPanel();
        DockPanel.SetDock(right, Dock.Right);
        dock.Children.Add(right);
        dock.Children.Add(_file);

        Child = dock;
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

        _language.Text = LanguageName(tab.Document.FilePath);
        _position.Text = $"Ln {tab.Actions.CaretLine}, Col {tab.Actions.CaretColumn}";
    }

    private static string LanguageName(string? filePath)
    {
        string extension = Path.GetExtension(filePath ?? string.Empty).ToLowerInvariant();

        return extension switch
        {
            ".py" => "Python",
            ".cs" => "C#",
            ".js" => "JavaScript",
            ".html" or ".htm" => "HTML",
            ".css" => "CSS",
            ".json" => "JSON",
            ".xml" or ".xaml" => "XML",
            ".md" => "Markdown",
            ".txt" => "Texte",
            _ => "Texte brut"
        };
    }

    private static TextBlock MakeText() => new()
    {
        Foreground = Brushes.White,
        FontSize = 12,
        VerticalAlignment = VerticalAlignment.Center
    };
}