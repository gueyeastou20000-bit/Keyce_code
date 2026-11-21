namespace KEYCE_KODER.Core.Interfaces;

public interface IEditorActions
{
    
    bool CanUndo { get; }
    bool CanRedo { get; }
    bool HasSelection { get; }

    string Text { get; }
    int SelectionStart { get; }
    int SelectionLength { get; }
    string SelectedText { get; }

    int CaretLine { get; }
    int CaretColumn { get; }

    event EventHandler? CaretPositionChanged;

   
    void Undo();
    void Redo();
    void Cut();
    void Copy();
    void Paste();
    void Delete();
    void SelectAll();

    void Select(int start, int length);
    void ReplaceSelection(string text);
    void ReplaceAllText(string text);

  
    void Focus();
}