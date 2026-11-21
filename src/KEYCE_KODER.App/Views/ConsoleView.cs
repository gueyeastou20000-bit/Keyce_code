using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using KEYCE_KODER.Core.Execution;

namespace KEYCE_KODER.App.Views;

/// <summary>Zone de texte en lecture seule, colorée selon le type de sortie (stdout, stderr, info).
/// Ne conserve que les <see cref="MaxLines"/> dernières lignes pour rester fluide.</summary>
public sealed class ConsoleView : RichTextBox
{
    private const int ChunkLines = 200;

    private static readonly Regex AnsiCodes = new(@"\x1B\[[0-9;?]*[ -/]*[@-~]", RegexOptions.Compiled);

    private static readonly Brush StdOutBrush = Frozen(212, 212, 212);
    private static readonly Brush StdErrBrush = Frozen(244, 135, 113);
    private static readonly Brush InfoBrush = Frozen(133, 133, 133);

    private readonly Paragraph _paragraph = new() { Margin = new Thickness(0) };
    private readonly Queue<(Run Run, int Lines)> _chunks = new();
    private int _totalLines;

    public int MaxLines { get; set; } = 5000;

    public ConsoleView()
    {
        IsReadOnly = true;
        Background = Frozen(30, 30, 30);
        Foreground = StdOutBrush;
        BorderThickness = new Thickness(0);
        FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New");
        FontSize = 13;
        Padding = new Thickness(8, 4, 8, 4);
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        Document = new FlowDocument(_paragraph) { PagePadding = new Thickness(0) };
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
                OutputKind.StdErr => StdErrBrush,
                OutputKind.Info => InfoBrush,
                _ => StdOutBrush,
            },
        };
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
