using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KEYCE_KODER.Core.Models;
using KEYCE_KODER.Core.Services;

namespace KEYCE_KODER.App.Search;

public sealed class SearchPanel : DockPanel
{
    private static readonly Brush TextBrush = Frozen(220, 220, 220);
    private static readonly Brush MutedBrush = Frozen(150, 150, 150);
    private static readonly Brush InputBrush = Frozen(60, 60, 60);
    private static readonly Brush BorderBrushColor = Frozen(85, 85, 85);

    private readonly Func<string?> _getRootFolder;
    private readonly FileSearchService _fileSearch = new();

    private readonly TextBox _queryBox = new()
    {
        Background = InputBrush,
        Foreground = TextBrush,
        CaretBrush = TextBrush,
        BorderBrush = BorderBrushColor,
        Padding = new Thickness(4, 3, 4, 3)
    };

    private readonly CheckBox _matchCase = MakeCheckBox("Casse");
    private readonly CheckBox _wholeWord = MakeCheckBox("Mot entier");

    private readonly TextBlock _status = new()
    {
        Foreground = MutedBrush,
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 6),
        Text = "Tapez un texte puis appuyez sur Entrée."
    };

    private readonly TreeView _results = new()
    {
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Foreground = TextBrush
    };

    private CancellationTokenSource? _cancellation;

    /// <summary>Double-clic sur un résultat : (chemin du fichier, position, longueur).</summary>
    public event Action<string, int, int>? MatchOpenRequested;

    public SearchPanel(Func<string?> getRootFolder)
    {
        _getRootFolder = getRootFolder;
        Margin = new Thickness(10, 0, 10, 0);

        var options = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 6) };
        options.Children.Add(_matchCase);
        options.Children.Add(_wholeWord);

        var top = new StackPanel();
        top.Children.Add(_queryBox);
        top.Children.Add(options);
        top.Children.Add(_status);

        SetDock(top, Dock.Top);
        Children.Add(top);
        Children.Add(_results); // le dernier élément prend toute la place restante

        // Entrée = lancer la recherche ; changer une option relance la recherche
        _queryBox.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await RunSearchAsync();
            }
        };

        _matchCase.Click += async (_, _) => await RunSearchAsync();
        _wholeWord.Click += async (_, _) => await RunSearchAsync();
    }

    private async Task RunSearchAsync()
    {
        string query = _queryBox.Text;

        // Annule la recherche précédente si elle n'est pas finie
        _cancellation?.Cancel();
        _results.Items.Clear();

        if (query.Length == 0)
        {
            _status.Text = "Tapez un texte puis appuyez sur Entrée.";
            return;
        }

        string? rootFolder = _getRootFolder();
        if (rootFolder is null)
        {
            _status.Text = "Ouvrez d'abord un dossier dans l'explorateur.";
            return;
        }

        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        _status.Text = "Recherche en cours…";

        try
        {
            var options = new SearchOptions(
                MatchCase: _matchCase.IsChecked == true,
                WholeWord: _wholeWord.IsChecked == true);

            FileSearchReport report = await _fileSearch.SearchAsync(rootFolder, query, options, cancellation.Token);

            // Une recherche plus récente a été lancée entre-temps : on ignore celle-ci
            if (cancellation != _cancellation)
                return;

            ShowReport(report, rootFolder);
        }
        catch (OperationCanceledException)
        {
            // Recherche annulée par une nouvelle recherche : rien à faire
        }
    }

    private void ShowReport(FileSearchReport report, string rootFolder)
    {
        _results.Items.Clear();

        if (report.TotalMatches == 0)
        {
            _status.Text = "Aucun résultat.";
            return;
        }

        _status.Text = $"{report.TotalMatches} résultat(s) dans {report.Files.Count} fichier(s)"
                       + (report.Truncated ? " — limite atteinte, affinez la recherche." : ".");

        foreach (FileSearchResult file in report.Files)
        {
            var fileNode = new TreeViewItem
            {
                Header = MakeFileHeader(file, rootFolder),
                IsExpanded = true,
                Foreground = TextBrush,
                ToolTip = file.FilePath
            };

            foreach (LineMatch line in file.Matches)
            {
                var lineNode = new TreeViewItem
                {
                    Header = MakeLineHeader(line),
                    Foreground = TextBrush,
                    ToolTip = $"{Path.GetFileName(file.FilePath)} — ligne {line.LineNumber}"
                };

                // Double-clic ou Entrée = ouvrir le fichier sur cette occurrence
                lineNode.MouseDoubleClick += (_, e) =>
                {
                    e.Handled = true;
                    MatchOpenRequested?.Invoke(file.FilePath, line.Index, line.Length);
                };

                lineNode.KeyDown += (_, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        e.Handled = true;
                        MatchOpenRequested?.Invoke(file.FilePath, line.Index, line.Length);
                    }
                };

                fileNode.Items.Add(lineNode);
            }

            _results.Items.Add(fileNode);
        }
    }

    private static UIElement MakeFileHeader(FileSearchResult file, string rootFolder)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        panel.Children.Add(new TextBlock
        {
            Text = Path.GetFileName(file.FilePath),
            Foreground = TextBrush,
            FontWeight = FontWeights.SemiBold
        });

        // Dossier relatif (ex. « src\utils »), sauf si le fichier est à la racine
        string folder = Path.GetRelativePath(rootFolder, Path.GetDirectoryName(file.FilePath) ?? rootFolder);
        if (folder != ".")
            panel.Children.Add(new TextBlock { Text = "  " + folder, Foreground = MutedBrush });

        panel.Children.Add(new TextBlock { Text = $"  ({file.Matches.Count})", Foreground = MutedBrush });

        return panel;
    }

    private static UIElement MakeLineHeader(LineMatch line)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        panel.Children.Add(new TextBlock
        {
            Text = line.LineNumber.ToString(),
            Foreground = MutedBrush,
            MinWidth = 32
        });

        panel.Children.Add(new TextBlock { Text = line.LineText, Foreground = TextBrush });

        return panel;
    }

    private static CheckBox MakeCheckBox(string text) => new()
    {
        Content = text,
        Foreground = TextBrush,
        Margin = new Thickness(0, 0, 12, 0)
    };

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}