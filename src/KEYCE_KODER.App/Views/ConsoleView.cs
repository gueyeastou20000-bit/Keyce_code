using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using KEYCE_KODER.Core.Execution;

namespace KEYCE_KODER.App.Views;

/// <summary>Zone de sortie en lecture seule avec conservation limitée des lignes.</summary>
public sealed class ConsoleView : RichTextBox
{
    private const int ChunkLines = 200;

    private static readonly Regex AnsiCodes = new(@"\x1B\[[0-9;?]*[ -/]*[@-~]", RegexOptions.Compiled);

    private static readonly Brush StdOutBrush = Frozen(218, 222, 232);
    private static readonly Brush StdErrBrush = Theme.Red;
    private static readonly Brush WarningBrush = Frozen(226, 192, 141);
    private static readonly Brush InfoBrush = Theme.Muted;

    // Beaucoup d'outils (git, pip, dotnet...) écrivent des messages NORMAUX sur la sortie d'erreur
    // (« Switched to branch », « To https://github.com/... », progression...).
    // On ne met en rouge que ce qui ressemble vraiment à une erreur.
    private static readonly Regex ErrorPattern = new(
        @"\b(error|erreur|fatal|exception|traceback|failed|échec|echec|denied|refusé|rejected|not found|introuvable|" +
        @"cannot|impossible|invalid|invalide|n'est pas reconnu|is not recognized|unable|syntaxerror|abort)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex WarningPattern = new(
        @"^\s*(warning|avertissement|attention|hint|astuce|deprecat)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private bool _lastErrorWasRed; // une erreur sur plusieurs blocs (ex. Traceback Python) reste rouge

    private readonly Paragraph _paragraph = new() { Margin = new Thickness(0) };
    private readonly Queue<(Run Run, int Lines)> _chunks = new();
    private int _totalLines;

    public int MaxLines { get; set; } = 5000;

    public ConsoleView()
    {
        IsReadOnly = true;
        Background = Theme.Panel;
        Foreground = StdOutBrush;
        BorderThickness = new Thickness(0);
        FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New");
        FontSize = 13;
        Padding = new Thickness(12, 8, 12, 8);
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        Document = new FlowDocument(_paragraph)
        {
            PagePadding = new Thickness(0),
            Background = Theme.Panel
        };
    }

    /// <summary>Ajoute un lot de sorties (à appeler depuis le thread UI).</summary>
    public void Append(IReadOnlyList<OutputLine> lines)
    {
        var text = new StringBuilder();
        var kind = OutputKind.StdOut;
        var count = 0;

        void Flush()
        {
            if (text.Length == 0) return;
            AddRun(kind, text.ToString(), Math.Max(1, count));
            text.Clear();
            count = 0;
        }

        foreach (var line in lines)
        {
            if (text.Length > 0 && (line.Kind != kind || count >= ChunkLines)) Flush();

            kind = line.Kind;
            var clean = Clean(line.Text);
            text.Append(clean);
            count += CountNewLines(clean);
        }
        Flush();

        ScrollToEnd();
    }

    public void Clear()
    {
        _paragraph.Inlines.Clear();
        _chunks.Clear();
        _totalLines = 0;
    }

    private void AddRun(OutputKind kind, string text, int lines)
    {
        var run = new Run(text)
        {
            Foreground = kind switch
            {
                OutputKind.StdErr => ErrorOutputBrush(text),
                OutputKind.Info => InfoBrush,
                _ => StdOutBrush,
            },
        };

        if (kind != OutputKind.StdErr)
            _lastErrorWasRed = false;
        _paragraph.Inlines.Add(run);
        _chunks.Enqueue((run, lines));
        _totalLines += lines;

        while (_totalLines > MaxLines && _chunks.Count > 1)
        {
            var old = _chunks.Dequeue();
            _paragraph.Inlines.Remove(old.Run);
            _totalLines -= old.Lines;
        }
    }

    /// <summary>Couleur d'un bloc écrit sur la sortie d'erreur : rouge, orange (avertissement) ou normal.</summary>
    private Brush ErrorOutputBrush(string text)
    {
        if (ErrorPattern.IsMatch(text) || _lastErrorWasRed)
        {
            _lastErrorWasRed = true;
            return StdErrBrush;
        }

        return WarningPattern.IsMatch(text) ? WarningBrush : StdOutBrush;
    }

    private static string Clean(string s)
        => AnsiCodes.Replace(s, "").Replace("\r\n", "\n").Replace("\r", "");

    private static int CountNewLines(string s)
    {
        var n = 0;
        foreach (var c in s) if (c == '\n') n++;
        return n;
    }

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
