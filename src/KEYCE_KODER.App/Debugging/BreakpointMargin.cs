using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using KEYCE_KODER.App.Git;

namespace KEYCE_KODER.App.Debugging;

/// <summary>
/// Ligne où le débogueur est arrêté (partagée par tous les éditeurs).
/// IsTopFrame = false : l'utilisateur regarde une autre ligne de la pile d'appels.
/// </summary>
public sealed class DebugLocation
{
    public string? FilePath { get; private set; }

    public int Line { get; private set; }

    public bool IsTopFrame { get; private set; }

    public event Action? Changed;

    public void Set(string filePath, int line, bool isTopFrame)
    {
        FilePath = BreakpointStore.Normalize(filePath);
        Line = line;
        IsTopFrame = isTopFrame;
        Changed?.Invoke();
    }

    public void Clear()
    {
        FilePath = null;
        Line = 0;
        Changed?.Invoke();
    }

    public bool Matches(string? filePath) =>
        FilePath is not null && filePath is not null
        && string.Equals(FilePath, BreakpointStore.Normalize(filePath), StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Marge à gauche des numéros de ligne (comme VS Code) :
/// clic = poser / retirer un point d'arrêt (rond rouge), clic droit = condition, désactiver...
/// Une flèche jaune montre la ligne où le programme est arrêté.
/// </summary>
public sealed class BreakpointMargin : AbstractMargin
{
    private const double MarginWidth = 18;

    private static readonly Brush BreakpointBrush = Frozen(Color.FromRgb(229, 20, 0));
    private static readonly Brush DisabledBrush = Frozen(Color.FromRgb(130, 130, 130));
    private static readonly Brush HoverBrush = Frozen(Color.FromArgb(110, 229, 20, 0));
    private static readonly Brush ArrowBrush = Frozen(Color.FromRgb(255, 204, 0));
    private static readonly Brush FrameArrowBrush = Frozen(Color.FromRgb(80, 200, 151));
    private static readonly Pen ConditionPen = FrozenPen(Brushes.White, 1.6);
    private static readonly Pen UnverifiedPen = FrozenPen(Frozen(Color.FromRgb(229, 20, 0)), 1.5);

    private readonly BreakpointStore _store;
    private readonly DebugLocation _location;
    private readonly Func<string?> _getFilePath;
    private readonly Dictionary<Breakpoint, TextAnchor> _anchors = new();
    private int _hoverLine;

    public BreakpointMargin(BreakpointStore store, DebugLocation location, Func<string?> getFilePath)
    {
        _store = store;
        _location = location;
        _getFilePath = getFilePath;

        Cursor = Cursors.Hand;
        ToolTip = "Clic : point d'arrêt (F9)  ·  Clic droit : condition, désactiver";

        _store.Changed += file =>
        {
            if (IsMyFile(file))
            {
                SyncAnchors();
                InvalidateVisual();
            }
        };

        _location.Changed += InvalidateVisual;
    }

    private string? FilePath => _getFilePath();

    private bool IsMyFile(string file) =>
        FilePath is { } mine && string.Equals(BreakpointStore.Normalize(mine), file, StringComparison.OrdinalIgnoreCase);

    // ---------------- Dessin ----------------

    protected override Size MeasureOverride(Size availableSize) => new(MarginWidth, 0);

    protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
    {
        if (oldTextView is not null)
        {
            oldTextView.VisualLinesChanged -= OnVisualLinesChanged;
            oldTextView.ScrollOffsetChanged -= OnVisualLinesChanged;
        }

        base.OnTextViewChanged(oldTextView, newTextView);

        if (newTextView is not null)
        {
            newTextView.VisualLinesChanged += OnVisualLinesChanged;
            newTextView.ScrollOffsetChanged += OnVisualLinesChanged;
        }

        InvalidateVisual();
    }

    protected override void OnDocumentChanged(TextDocument oldDocument, TextDocument newDocument)
    {
        if (oldDocument is not null)
            oldDocument.Changed -= OnDocumentTextChanged;

        base.OnDocumentChanged(oldDocument, newDocument);

        _anchors.Clear();

        if (newDocument is not null)
        {
            newDocument.Changed += OnDocumentTextChanged;
            SyncAnchors();
        }

        InvalidateVisual();
    }

    private void OnVisualLinesChanged(object? sender, EventArgs e) => InvalidateVisual();

    protected override void OnRender(DrawingContext drawingContext)
    {
        // Fond transparent : toute la marge reçoit les clics
        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));

        TextView? textView = TextView;
        if (textView is null || !textView.VisualLinesValid)
            return;

        string? file = FilePath;
        IReadOnlyList<Breakpoint> breakpoints = _store.For(file);

        foreach (VisualLine visualLine in textView.VisualLines)
        {
            int line = visualLine.FirstDocumentLine.LineNumber;
            double lineHeight = visualLine.TextLines.Count > 0 ? visualLine.TextLines[0].Height : visualLine.Height;
            double top = visualLine.VisualTop - textView.VerticalOffset;
            var center = new Point(MarginWidth / 2, top + lineHeight / 2);
            double radius = Math.Min(5.5, lineHeight / 2 - 1);

            Breakpoint? breakpoint = breakpoints.FirstOrDefault(b => b.Line == line);

            if (breakpoint is not null)
            {
                if (!breakpoint.Verified)
                    drawingContext.DrawEllipse(null, UnverifiedPen, center, radius - 0.5, radius - 0.5);
                else
                    drawingContext.DrawEllipse(breakpoint.Enabled ? BreakpointBrush : DisabledBrush, null, center, radius, radius);

                // Point d'arrêt conditionnel : un trait blanc au centre
                if (!string.IsNullOrWhiteSpace(breakpoint.Condition))
                    drawingContext.DrawLine(ConditionPen, center with { X = center.X - 2.5 }, center with { X = center.X + 2.5 });
            }
            else if (line == _hoverLine)
            {
                drawingContext.DrawEllipse(HoverBrush, null, center, radius, radius);
            }

            // Flèche : ligne où le programme est arrêté
            if (_location.Line == line && _location.Matches(file))
                DrawArrow(drawingContext, center, lineHeight, _location.IsTopFrame ? ArrowBrush : FrameArrowBrush);
        }
    }

    private static void DrawArrow(DrawingContext drawingContext, Point center, double height, Brush brush)
    {
        double half = Math.Min(6, height / 2 - 1);
        var geometry = new StreamGeometry();

        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(new Point(2, center.Y - half / 2), isFilled: true, isClosed: true);
            context.LineTo(new Point(9, center.Y - half / 2), true, false);
            context.LineTo(new Point(9, center.Y - half), true, false);
            context.LineTo(new Point(16, center.Y), true, false);
            context.LineTo(new Point(9, center.Y + half), true, false);
            context.LineTo(new Point(9, center.Y + half / 2), true, false);
            context.LineTo(new Point(2, center.Y + half / 2), true, false);
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(brush, null, geometry);
    }

    // ---------------- Souris ----------------

    private int LineAt(Point position)
    {
        TextView? textView = TextView;
        if (textView is null || !textView.VisualLinesValid)
            return 0;

        VisualLine? visualLine = textView.GetVisualLineFromVisualTop(position.Y + textView.VerticalOffset);
        return visualLine?.FirstDocumentLine.LineNumber ?? 0;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        int line = LineAt(e.GetPosition(this));
        if (line != _hoverLine)
        {
            _hoverLine = line;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverLine = 0;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        e.Handled = true;

        int line = LineAt(e.GetPosition(this));
        if (line > 0)
            ToggleAt(line);
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        e.Handled = true;

        int line = LineAt(e.GetPosition(this));
        if (line <= 0 || FilePath is not { } file)
            return;

        var menu = new ContextMenu { PlacementTarget = this };
        Breakpoint? breakpoint = _store.Find(file, line);

        if (breakpoint is null)
        {
            menu.Items.Add(MenuEntry("Ajouter un point d'arrêt", () => ToggleAt(line)));
            menu.Items.Add(MenuEntry("Ajouter un point d'arrêt conditionnel…", () =>
            {
                ToggleAt(line);
                if (_store.Find(file, line) is { } created)
                    EditCondition(created);
            }));
        }
        else
        {
            menu.Items.Add(MenuEntry("Modifier la condition…", () => EditCondition(breakpoint)));
            menu.Items.Add(MenuEntry(breakpoint.Enabled ? "Désactiver le point d'arrêt" : "Activer le point d'arrêt",
                () => _store.SetEnabled(breakpoint, !breakpoint.Enabled)));
            menu.Items.Add(MenuEntry("Supprimer le point d'arrêt", () => _store.Remove(breakpoint)));
        }

        menu.IsOpen = true;
    }

    private void ToggleAt(int line)
    {
        if (FilePath is not { } file)
        {
            MessageBox.Show(Window.GetWindow(this)!,
                "Enregistrez d'abord le fichier (Ctrl+S) pour pouvoir y poser des points d'arrêt.",
                "Débogage", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _store.Toggle(file, line);
    }

    /// <summary>Point d'arrêt sur la ligne du curseur (F9).</summary>
    public void ToggleAtCaret(int line) => ToggleAt(line);

    private void EditCondition(Breakpoint breakpoint)
    {
        string[]? values = PromptWindow.Ask(
            Window.GetWindow(this),
            "Condition du point d'arrêt",
            $"Le programme s'arrêtera à la ligne {breakpoint.Line} seulement si cette expression Python est vraie "
            + "(ex. i == 5 ou len(liste) > 3). Laissez vide pour toujours s'arrêter.",
            ("Condition", breakpoint.Condition ?? ""));

        if (values is not null)
            _store.SetCondition(breakpoint, values[0]);
    }

    private static MenuItem MenuEntry(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    // ---------------- Les points d'arrêt suivent le texte ----------------

    /// <summary>Crée une « ancre » par point d'arrêt : elle se déplace quand on ajoute ou supprime des lignes.</summary>
    private void SyncAnchors()
    {
        TextDocument? document = Document;
        if (document is null)
            return;

        IReadOnlyList<Breakpoint> breakpoints = _store.For(FilePath);

        foreach (Breakpoint removed in _anchors.Keys.Where(b => !breakpoints.Contains(b)).ToList())
            _anchors.Remove(removed);

        foreach (Breakpoint breakpoint in breakpoints)
        {
            if (_anchors.ContainsKey(breakpoint))
                continue;

            int line = Math.Clamp(breakpoint.Line, 1, document.LineCount);
            TextAnchor anchor = document.CreateAnchor(document.GetLineByNumber(line).Offset);
            anchor.MovementType = AnchorMovementType.AfterInsertion;
            anchor.SurviveDeletion = true;
            _anchors[breakpoint] = anchor;
        }
    }

    private void OnDocumentTextChanged(object? sender, DocumentChangeEventArgs e)
    {
        if (FilePath is not { } file || Document is not { } document)
            return;

        // Tout le texte a été remplacé (fichier rechargé après un pull...) : on garde les numéros de ligne
        int previousLength = document.TextLength - e.InsertionLength + e.RemovalLength;
        if (e.Offset == 0 && e.RemovalLength > 0 && e.RemovalLength == previousLength)
        {
            _anchors.Clear();
            SyncAnchors();
            InvalidateVisual();
            return;
        }

        bool moved = false;

        foreach ((Breakpoint breakpoint, TextAnchor anchor) in _anchors)
        {
            if (anchor.Line != breakpoint.Line)
            {
                breakpoint.Line = anchor.Line;
                moved = true;
            }
        }

        if (moved)
            _store.NotifyMoved(file);
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen FrozenPen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }
}

/// <summary>Surligne en jaune la ligne où le programme est arrêté.</summary>
public sealed class DebugLineMarker : IBackgroundRenderer
{
    private static readonly Brush TopFrameBrush = FrozenBrush(Color.FromArgb(70, 255, 204, 0));
    private static readonly Brush OtherFrameBrush = FrozenBrush(Color.FromArgb(55, 80, 200, 151));

    private readonly DebugLocation _location;
    private readonly Func<string?> _getFilePath;

    public DebugLineMarker(DebugLocation location, Func<string?> getFilePath)
    {
        _location = location;
        _getFilePath = getFilePath;
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_location.Line <= 0 || !_location.Matches(_getFilePath()) || !textView.VisualLinesValid)
            return;

        VisualLine? visualLine = textView.GetVisualLine(_location.Line);
        if (visualLine is null)
            return;

        var rect = new Rect(
            0,
            visualLine.VisualTop - textView.VerticalOffset,
            Math.Max(textView.ActualWidth, 1),
            visualLine.Height);

        drawingContext.DrawRectangle(_location.IsTopFrame ? TopFrameBrush : OtherFrameBrush, null, rect);
    }

    private static Brush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
