using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Indentation;
using KEYCE_KODER.Core.Editing;
using KEYCE_KODER.Core.Languages;

namespace KEYCE_KODER.Editor.Editing;

/// <summary>
/// Indentation intelligente : Entrée, blocs {|}, désindentation de « } », Tab selon le langage.
/// Remplace la stratégie d'indentation par défaut d'AvalonEdit.
/// </summary>
public sealed class IndentationController : IIndentationStrategy
{
    private readonly TextEditor _editor;
    private LanguageDefinition _language = LanguageCatalog.PlainText;

    public bool Enabled { get; set; } = true;

    public IndentationController(TextEditor editor)
    {
        _editor = editor;

        _editor.Options.IndentationSize = 4;
        _editor.Options.ConvertTabsToSpaces = true;

        _editor.TextArea.IndentationStrategy = this;   // AvalonEdit nous appellera après chaque Entrée
        _editor.TextArea.PreviewKeyDown += OnPreviewKeyDown;
        _editor.TextArea.TextEntered += OnTextEntered;
    }

    private TextDocument Document => _editor.Document;

    private string IndentUnit =>
        _language.UseTabs ? "\t" : new string(' ', _editor.Options.IndentationSize);

    public void SetLanguage(string? filePath)
    {
        _language = LanguageCatalog.FromPath(filePath);

        // Tab insère des espaces, sauf pour les langages à tabulations (Go)
        _editor.Options.ConvertTabsToSpaces = !_language.UseTabs;
    }

    // ----- Contrat IIndentationStrategy d'AvalonEdit -----

    /// <summary>Appelée par AvalonEdit sur la nouvelle ligne créée par Entrée.</summary>
    public void IndentLine(TextDocument document, DocumentLine line)
    {
        DocumentLine? previous = line.PreviousLine;
        if (previous is null)
            return;

        string previousText = document.GetText(previous);
        string indent = Enabled
            ? IndentationRules.IndentAfter(previousText, _language, IndentUnit)
            : IndentationRules.LeadingWhitespace(previousText);

        ISegment current = TextUtilities.GetWhitespaceAfter(document, line.Offset);
        document.Replace(current.Offset, current.Length, indent, OffsetChangeMappingType.RemoveAndInsert);
    }

    public void IndentLines(TextDocument document, int beginLine, int endLine)
    {
        // Pas de réindentation de tout un bloc : on laisse le texte tel quel
    }

    // ----- Entrée entre {|} : ouvrir un bloc -----

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!Enabled || e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None)
            return;

        if (_editor.SelectionLength > 0)
            return;

        int caret = _editor.CaretOffset;
        char previous = caret > 0 ? Document.GetCharAt(caret - 1) : '\0';
        char next = caret < Document.TextLength ? Document.GetCharAt(caret) : '\0';

        if (!IndentationRules.IsEmptyPair(previous, next))
            return;

        e.Handled = true;

        DocumentLine line = Document.GetLineByOffset(caret);
        string lineIndent = IndentationRules.LeadingWhitespace(Document.GetText(line));
        string inner = lineIndent + IndentUnit;
        string newLine = TextUtilities.GetNewLineFromDocument(Document, line.LineNumber);

        // {|}  devient  {
        //                   |
        //               }
        Document.Insert(caret, newLine + inner + newLine + lineIndent);
        _editor.CaretOffset = caret + newLine.Length + inner.Length;
    }

    // ----- « } » seul sur sa ligne : reculer d'un niveau -----

    private void OnTextEntered(object sender, TextCompositionEventArgs e)
    {
        if (!Enabled || e.Text is not ("}" or "]" or ")"))
            return;

        DocumentLine line = Document.GetLineByOffset(_editor.CaretOffset);
        string text = Document.GetText(line);

        if (text.Trim() != e.Text)
            return; // le fermant n'est pas seul sur la ligne : on ne touche à rien

        string indent = IndentationRules.LeadingWhitespace(text);
        Document.Replace(line.Offset, indent.Length, IndentationRules.Dedent(indent, IndentUnit));
    }
}