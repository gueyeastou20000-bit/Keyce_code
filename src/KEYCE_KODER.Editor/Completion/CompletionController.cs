using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using KEYCE_KODER.Core.Completion;
using KEYCE_KODER.Core.Languages;

namespace KEYCE_KODER.Editor.Completion;

/// <summary>
/// Gère l'autocomplétion d'un éditeur : ouverture automatique pendant la frappe,
/// Ctrl+Espace, fermeture, remplissage de la liste avec CompletionService.
/// </summary>
public sealed class CompletionController
{
    private const int MinPrefixLength = 2;

    private static readonly Brush BackgroundBrush = Frozen(30, 33, 42);
    private static readonly Brush BorderBrushColor = Frozen(70, 76, 92);
    private static readonly Brush TextBrush = Frozen(220, 220, 220);

    private readonly TextEditor _editor;
    private readonly CompletionService _service = new();
    private CompletionWindow? _window;
    private string? _filePath;
    private LanguageDefinition _language = LanguageCatalog.PlainText;

    /// <summary>Permet de désactiver l'ouverture automatique (Ctrl+Espace reste disponible).</summary>
    public bool AutoOpen { get; set; } = true;

    public CompletionController(TextEditor editor)
    {
        _editor = editor;

        _editor.TextArea.TextEntering += OnTextEntering;
        _editor.TextArea.TextEntered += OnTextEntered;
        _editor.TextArea.PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>Le langage dépend du fichier (même principe que la coloration et le pliage).</summary>
    public void SetLanguage(string? filePath)
    {
        _filePath = filePath;
        _language = LanguageCatalog.FromPath(filePath);
    }

    private void OnTextEntered(object sender, TextCompositionEventArgs e)
    {
        if (!AutoOpen || _window != null || e.Text.Length == 0)
            return;

        // On n'ouvre la liste que pendant la frappe d'un mot (lettre, chiffre, « _ »)
        if (!CompletionService.IsIdentifierChar(e.Text[0]))
            return;

        (_, string prefix) = CompletionService.GetPrefix(_editor.Text, _editor.CaretOffset);

        // Pas avant 2 caractères, et jamais au milieu d'un nombre (ex. 3.14)
        if (prefix.Length < MinPrefixLength || char.IsDigit(prefix[0]))
            return;

        ShowCompletion();
    }

    private void OnTextEntering(object sender, TextCompositionEventArgs e)
    {
        // Un caractère qui ne fait pas partie d'un mot (espace, parenthèse, point...)
        // ferme la liste SANS insérer de suggestion, comme dans VS Code.
        if (_window != null && e.Text.Length > 0 && !CompletionService.IsIdentifierChar(e.Text[0]))
            _window.Close();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+Espace : ouvrir la liste à la demande
        if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true; // empêche l'insertion d'un espace
            ShowCompletion();
        }
    }

    /// <summary>Calcule les suggestions à la position du curseur et ouvre la liste.</summary>
    public void ShowCompletion()
    {
        _window?.Close();

        string text = _editor.Text;
        int caret = _editor.CaretOffset;

        IReadOnlyList<CompletionItem> items = _service.GetCompletions(text, caret, _filePath);
        if (items.Count == 0)
            return; // rien à proposer : on n'ouvre pas une liste vide

        (int start, string prefix) = CompletionService.GetPrefix(text, caret);

        var window = new CompletionWindow(_editor.TextArea)
        {
            StartOffset = start,                     // le mot remplacé commence ici
            CloseWhenCaretAtBeginning = prefix.Length > 0,
            Width = 280,
            Background = BackgroundBrush,
            BorderBrush = BorderBrushColor,
            BorderThickness = new Thickness(1)
        };

        window.CompletionList.ListBox.Background = BackgroundBrush;
        window.CompletionList.ListBox.Foreground = TextBrush;
        window.CompletionList.ListBox.BorderThickness = new Thickness(0);

        foreach (CompletionItem item in items)
            window.CompletionList.CompletionData.Add(new CompletionData(item));

        window.Closed += (_, _) =>
        {
            if (_window == window)
                _window = null;
        };

        _window = window;
        window.Show();

        // Sélectionne la meilleure suggestion pour ce qui est déjà tapé
        window.CompletionList.SelectItem(prefix);
    }

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}