using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KEYCE_KODER.App.Views;

namespace KEYCE_KODER.App.Git;

/// <summary>
/// Affiche les modifications d'un fichier (format diff) :
/// lignes ajoutées en vert, lignes supprimées en rouge.
/// </summary>
public sealed class DiffWindow : Window
{
    private const int MaxLines = 5000;

    private static readonly Brush AddedBackground = Frozen(23, 58, 40);
    private static readonly Brush RemovedBackground = Frozen(72, 28, 33);

    public DiffWindow(Window? owner, string fileName, string diff)
    {
        if (owner is not null)
            Owner = owner;

        Title = $"Modifications — {fileName}";
        Width = 900;
        Height = 600;
        MinWidth = 400;
        MinHeight = 250;
        ShowInTaskbar = false;
        WindowStartupLocation = owner is null
            ? WindowStartupLocation.CenterScreen
            : WindowStartupLocation.CenterOwner;
        Background = Theme.Editor;

        var list = new ListBox
        {
            Background = Theme.Editor,
            Foreground = Theme.Text,
            BorderThickness = new Thickness(0),
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 12.5,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };

        string[] lines = diff.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

        if (diff.Trim().Length == 0)
            lines = ["(Aucune différence à afficher.)"];

        foreach (string line in lines.Take(MaxLines))
            list.Items.Add(MakeLine(line));

        if (lines.Length > MaxLines)
            list.Items.Add(MakeLine($"… ({lines.Length - MaxLines} lignes de plus non affichées)"));

        Content = list;

        // Échap = fermer
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
    }

    private static TextBlock MakeLine(string line)
    {
        Brush foreground = Theme.Text;
        Brush? background = null;

        if (IsHeader(line))
        {
            foreground = Theme.Muted;
        }
        else if (line.StartsWith("@@", StringComparison.Ordinal))
        {
            foreground = Theme.Accent; // début d'un bloc : « @@ -10,4 +10,6 @@ »
        }
        else if (line.StartsWith('+'))
        {
            foreground = Theme.Green;
            background = AddedBackground;
        }
        else if (line.StartsWith('-'))
        {
            foreground = Theme.Red;
            background = RemovedBackground;
        }

        return new TextBlock
        {
            Text = line.Length == 0 ? " " : line.Replace("\t", "    "),
            Foreground = foreground,
            Background = background,
            Padding = new Thickness(6, 0, 6, 0)
        };
    }

    private static bool IsHeader(string line) =>
        line.StartsWith("diff ", StringComparison.Ordinal)
        || line.StartsWith("index ", StringComparison.Ordinal)
        || line.StartsWith("+++", StringComparison.Ordinal)
        || line.StartsWith("---", StringComparison.Ordinal)
        || line.StartsWith("new file", StringComparison.Ordinal)
        || line.StartsWith("deleted file", StringComparison.Ordinal)
        || line.StartsWith("similarity", StringComparison.Ordinal)
        || line.StartsWith("rename ", StringComparison.Ordinal)
        || line.StartsWith("old mode", StringComparison.Ordinal)
        || line.StartsWith("new mode", StringComparison.Ordinal)
        || line.StartsWith("Binary files", StringComparison.Ordinal);

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
