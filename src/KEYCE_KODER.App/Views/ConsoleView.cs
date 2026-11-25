using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using KEYCE_KODER.Core.Execution;
using KEYCE_KODER.App.Services;

namespace KEYCE_KODER.App.Views;

/// <summary>
/// Zone de sortie en lecture seule avec conservation limitée des lignes.
/// Comprend les couleurs ANSI (git status, git diff, git log, invite du terminal...).
/// </summary>
public sealed class ConsoleView : RichTextBox
{
    private const int ChunkLines = 200;

    // Séquences ANSI : couleurs « ESC [ ... m » et autres commandes « ESC [ ... lettre » (ignorées)
    private static readonly Regex AnsiSequence = new(@"\x1B\[(?<args>[0-9;?]*)(?<cmd>[ -/]*[@-~])|\x1B\][^\x07\x1B]*(\x07|\x1B\\)|\x1B[@-Z\\-_]", RegexOptions.Compiled);
    private static readonly Regex IncompleteAnsi = new(@"\x1B(\[[0-9;?]*[ -/]*|\][^\x07]*)?$", RegexOptions.Compiled);

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

    // Palette ANSI (16 couleurs) adaptée au fond sombre
    private static readonly Color[] AnsiPalette =
    [
        Color.FromRgb(110, 118, 129), Color.FromRgb(241, 76, 76), Color.FromRgb(35, 209, 139), Color.FromRgb(229, 229, 16),
        Color.FromRgb(59, 142, 234), Color.FromRgb(214, 112, 214), Color.FromRgb(41, 184, 219), Color.FromRgb(204, 204, 204),
        Color.FromRgb(128, 128, 128), Color.FromRgb(241, 76, 76), Color.FromRgb(35, 209, 139), Color.FromRgb(245, 245, 67),
        Color.FromRgb(59, 142, 234), Color.FromRgb(214, 112, 214), Color.FromRgb(41, 184, 219), Color.FromRgb(229, 229, 229),
    ];

    private static readonly Dictionary<Color, Brush> BrushCache = new();

    private readonly Paragraph _paragraph = new() { Margin = new Thickness(0) };
    private readonly Queue<(Run Run, int Lines)> _chunks = new();
    private int _totalLines;

    private bool _lastErrorWasRed;   // une erreur sur plusieurs blocs (ex. Traceback Python) reste rouge
    private Brush? _ansiForeground;  // couleur ANSI en cours (null = couleur normale)
    private bool _ansiBold;
    private string _carry = "";      // séquence ANSI coupée entre deux blocs

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
            AddText(kind, text.ToString(), Math.Max(1, count));
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
        _ansiForeground = null;
        _ansiBold = false;
        _carry = "";
        _lastErrorWasRed = false;
    }

    /// <summary>Ajoute un bloc de texte d'un même type, découpé selon ses couleurs ANSI.</summary>
    private void AddText(OutputKind kind, string text, int lines)
    {
        text = _carry + text;
        _carry = "";

        // Séquence ANSI pas encore complète : on la garde pour le bloc suivant
        Match incomplete = IncompleteAnsi.Match(text);
        if (incomplete.Success && incomplete.Length > 0)
        {
            _carry = incomplete.Value;
            text = text[..incomplete.Index];
        }

        if (text.Length == 0)
            return;

        string plain = AnsiSequence.Replace(text, "");
        Brush baseBrush = AppSettings.Current.EnableColoredExecutionOutput
            ? (kind switch
            {
                OutputKind.StdErr => ErrorOutputBrush(plain),
                OutputKind.Info => InfoBrush,
                _ => StdOutBrush,
            })
            : StdOutBrush;

        if (kind != OutputKind.StdErr)
            _lastErrorWasRed = false;

        // Découpage : texte, séquence, texte, séquence...
        var runs = new List<Run>();
        int position = 0;

        foreach (Match match in AnsiSequence.Matches(text))
        {
            if (match.Index > position)
                runs.Add(MakeRun(text[position..match.Index], baseBrush));

            if (match.Groups["cmd"].Value == "m")
                ApplySgr(match.Groups["args"].Value);

            position = match.Index + match.Length;
        }

        if (position < text.Length)
            runs.Add(MakeRun(text[position..], baseBrush));

        for (int i = 0; i < runs.Count; i++)
        {
            int runLines = i == runs.Count - 1 ? lines : 0; // le compte de lignes va sur le dernier morceau
            _paragraph.Inlines.Add(runs[i]);
            _chunks.Enqueue((runs[i], runLines));
            _totalLines += runLines;
        }

        while (_totalLines > MaxLines && _chunks.Count > 1)
        {
            var old = _chunks.Dequeue();
            _paragraph.Inlines.Remove(old.Run);
            _totalLines -= old.Lines;
        }
    }

    private Run MakeRun(string text, Brush baseBrush)
    {
        Brush foreground = AppSettings.Current.EnableColoredExecutionOutput
            ? (_ansiForeground ?? baseBrush)
            : StdOutBrush;

        var run = new Run(text) { Foreground = foreground };
        if (_ansiBold && AppSettings.Current.EnableColoredExecutionOutput)
            run.FontWeight = FontWeights.SemiBold;
        return run;
    }

    /// <summary>Applique une séquence de couleur ANSI (SGR) : 0 = normal, 1 = gras, 31 = rouge, 38;5;n, 38;2;r;g;b...</summary>
    private void ApplySgr(string arguments)
    {
        string[] parts = arguments.Length == 0 ? ["0"] : arguments.Split(';');

        for (int i = 0; i < parts.Length; i++)
        {
            int code = int.TryParse(parts[i], out int value) ? value : 0;

            switch (code)
            {
                case 0:
                    _ansiForeground = null;
                    _ansiBold = false;
                    break;
                case 1:
                    _ansiBold = true;
                    break;
                case 22:
                    _ansiBold = false;
                    break;
                case 39:
                    _ansiForeground = null;
                    break;
                case >= 30 and <= 37:
                    _ansiForeground = BrushOf(AnsiPalette[code - 30 + (_ansiBold ? 8 : 0)]);
                    break;
                case >= 90 and <= 97:
                    _ansiForeground = BrushOf(AnsiPalette[code - 90 + 8]);
                    break;
                case 38 when i + 2 < parts.Length && parts[i + 1] == "5":
                    _ansiForeground = BrushOf(Color256(Parse(parts[i + 2])));
                    i += 2;
                    break;
                case 38 when i + 4 < parts.Length && parts[i + 1] == "2":
                    _ansiForeground = BrushOf(Color.FromRgb(Byte(parts[i + 2]), Byte(parts[i + 3]), Byte(parts[i + 4])));
                    i += 4;
                    break;
                case 48 when i + 1 < parts.Length:
                    i += parts[i + 1] == "5" ? 2 : parts[i + 1] == "2" ? 4 : 0; // fond : ignoré
                    break;
            }
        }
    }

    private static Color Color256(int index)
    {
        if (index < 16)
            return AnsiPalette[Math.Max(0, index)];

        if (index < 232)
        {
            int i = index - 16;
            static byte Level(int v) => (byte)(v == 0 ? 0 : 55 + v * 40);
            return Color.FromRgb(Level(i / 36), Level(i / 6 % 6), Level(i % 6));
        }

        byte gray = (byte)Math.Min(255, 8 + (index - 232) * 10);
        return Color.FromRgb(gray, gray, gray);
    }

    private static int Parse(string text) => int.TryParse(text, out int value) ? Math.Clamp(value, 0, 255) : 0;

    private static byte Byte(string text) => (byte)Parse(text);

    private static Brush BrushOf(Color color)
    {
        if (!BrushCache.TryGetValue(color, out Brush? brush))
        {
            var solid = new SolidColorBrush(color);
            solid.Freeze();
            BrushCache[color] = brush = solid;
        }

        return brush;
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
        => s.Replace("\r\n", "\n").Replace("\r", "");

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
