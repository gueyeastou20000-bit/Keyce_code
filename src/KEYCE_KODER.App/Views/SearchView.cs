using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KEYCE_KODER.App.Views;

public sealed class SearchView : DockPanel
{
    private readonly TextBox _query = new();
    private readonly TextBox _folder = new();
    private readonly ListBox _results = new();
    private readonly TextBlock _status = new();

    public event Action<string>? FileOpenRequested;

    public SearchView()
    {
        Margin = new Thickness(10, 0, 10, 10);

        _query.Height = 32;
        _query.Padding = new Thickness(8, 5, 8, 5);
        _query.Background = Theme.Surface;
        _query.Foreground = Theme.Text;
        _query.CaretBrush = Theme.Text;
        _query.BorderBrush = Theme.Border;
        _query.BorderThickness = new Thickness(1);
        _query.TextChanged += (_, _) => _ = SearchAsync();

        _folder.Height = 30;
        _folder.Padding = new Thickness(8, 4, 8, 4);
        _folder.Background = Theme.Panel;
        _folder.Foreground = Theme.Muted;
        _folder.CaretBrush = Theme.Text;
        _folder.BorderBrush = Theme.Border;
        _folder.BorderThickness = new Thickness(1);

        _status.Foreground = Theme.Muted;
        _status.FontSize = 11;
        _status.Margin = new Thickness(0, 8, 0, 8);

        _results.Background = Brushes.Transparent;
        _results.BorderThickness = new Thickness(0);
        _results.Foreground = Theme.Text;
        _results.MouseDoubleClick += (_, _) =>
        {
            if (_results.SelectedItem is SearchResult result)
                FileOpenRequested?.Invoke(result.Path);
        };

        var fields = new StackPanel();
        fields.Children.Add(_query);
        fields.Children.Add(new TextBlock
        {
            Text = "DOSSIER",
            Foreground = Theme.Muted,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 10, 0, 4)
        });
        fields.Children.Add(_folder);
        fields.Children.Add(_status);

        DockPanel.SetDock(fields, Dock.Top);
        Children.Add(fields);
        Children.Add(_results);
    }

    public void SetFolder(string? folder)
    {
        _folder.Text = folder ?? "";
    }

    private async Task SearchAsync()
    {
        var query = _query.Text.Trim();
        var folder = _folder.Text.Trim();

        _results.Items.Clear();
        if (query.Length < 2 || !Directory.Exists(folder))
        {
            _status.Text = query.Length < 2 ? "Tapez au moins 2 caractères" : "Sélectionnez un dossier valide";
            return;
        }

        _status.Text = "Recherche…";
        var matches = await Task.Run(() => FindMatches(folder, query));

        foreach (var match in matches.Take(200))
            _results.Items.Add(match);

        _status.Text = matches.Count == 0
            ? "Aucun résultat"
            : $"{matches.Count} résultat(s)";
    }

    private static List<SearchResult> FindMatches(string folder, string query)
    {
        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "bin", "obj", ".git", ".vs", "node_modules" };
        var results = new List<SearchResult>();

        try
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                var parts = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (parts.Any(part => ignored.Contains(part))) continue;

                try
                {
                    foreach (var (line, number) in File.ReadLines(file)
                                 .Select((text, index) => (text, index + 1)))
                    {
                        var position = line.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                        if (position >= 0)
                        {
                            results.Add(new SearchResult
                            {
                                Path = file,
                                Line = number,
                                Preview = line.Trim()
                            });
                            break;
                        }
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (Exception) { }

        return results;
    }

    private sealed class SearchResult
    {
        public required string Path { get; init; }
        public int Line { get; init; }
        public required string Preview { get; init; }

        public override string ToString()
            => $"{System.IO.Path.GetFileName(Path)}:{Line}  —  {Preview}";
    }
}
