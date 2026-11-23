using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using KEYCE_KODER.Core.Interfaces;
using KEYCE_KODER.Editor.Controls;

namespace KEYCE_KODER.Editor.Services;

public sealed class EditorActions : IEditorActions
{
    private readonly TextEditor _editor;

    public event EventHandler? CaretPositionChanged;

    public EditorActions(CodeEditor codeEditor)
    {
        _editor = codeEditor.InnerEditor;

        _editor.TextArea.Caret.PositionChanged += (_, _) =>
            CaretPositionChanged?.Invoke(this, EventArgs.Empty);
    }

    // ----- État -----
    public bool CanUndo => _editor.CanUndo;

    public bool CanRedo => _editor.CanRedo;

    public bool HasSelection => _editor.SelectionLength > 0;

    public string Text => _editor.Text;

    public int SelectionStart => _editor.SelectionStart;

    public int SelectionLength => _editor.SelectionLength;

    public string SelectedText => _editor.SelectedText;

    public int CaretLine => _editor.TextArea.Caret.Line;

    public int CaretColumn => _editor.TextArea.Caret.Column;
    // ----- Menu Édition -----
    public void Undo() => _editor.Undo();

    public void Redo() => _editor.Redo();

    public void Cut() => _editor.Cut();

    public void Copy() => _editor.Copy();

    public void Paste() => _editor.Paste();

    public void Delete() => _editor.Delete();

    public void SelectAll() => _editor.SelectAll();

    // ----- Recherche / remplacement -----
    public void Select(int start, int length)
    {
        int textLength = _editor.Document.TextLength;
        start = Math.Clamp(start, 0, textLength);
        length = Math.Clamp(length, 0, textLength - start);

        _editor.Select(start, length);

        TextLocation location = _editor.Document.GetLocation(start);
        _editor.ScrollTo(location.Line, location.Column);
    }

    public void ReplaceSelection(string text)
    {
        _editor.Document.Replace(_editor.SelectionStart, _editor.SelectionLength, text);
    }

    public void ReplaceAllText(string text)
    {
        _editor.Document.Replace(0, _editor.Document.TextLength, text);
    }

    public void Focus()
    {
        _editor.TextArea.Focus();
    }
}