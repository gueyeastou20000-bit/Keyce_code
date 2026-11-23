using System.Windows;
using KEYCE_KODER.App.Tabs;
using KEYCE_KODER.Core.Interfaces;

namespace KEYCE_KODER.App.Actions;

public sealed class EditActions
{
    private readonly TabManager _tabs;

    public EditActions(TabManager tabs)
    {
        _tabs = tabs;
    }

    private IEditorActions? Active => _tabs.ActiveTab?.Actions;

    // ----- Disponibilité (sert à griser les éléments du menu) -----
    public bool HasEditor => Active != null;

    public bool CanUndo => Active?.CanUndo ?? false;

    public bool CanRedo => Active?.CanRedo ?? false;

    public bool HasSelection => Active?.HasSelection ?? false;

    public bool CanPaste => Active != null && ClipboardHasText();

    // ----- Actions -----
    public void Undo() => Run(e => e.Undo());

    public void Redo() => Run(e => e.Redo());

    public void Cut() => Run(e => e.Cut());

    public void Copy() => Run(e => e.Copy());

    public void Paste() => Run(e => e.Paste());

    public void Delete() => Run(e => e.Delete());

    public void SelectAll() => Run(e => e.SelectAll());

    public void FoldAll()
    {
        EditorTab? tab = _tabs.ActiveTab;
        if (tab is null) return;

        tab.Editor.FoldAll();
        tab.Actions.Focus();
    }

    public void UnfoldAll()
    {
        EditorTab? tab = _tabs.ActiveTab;
        if (tab is null) return;

        tab.Editor.UnfoldAll();
        tab.Actions.Focus();
    }

    private void Run(Action<IEditorActions> action)
    {
        IEditorActions? editor = Active;

        if (editor is null)
            return;

        action(editor);
        editor.Focus(); // après un clic dans le menu, on revient dans l'éditeur
    }

    private static bool ClipboardHasText()
    {
        try
        {
            return Clipboard.ContainsText();
        }
        catch
        {
            // Le presse-papiers peut être bloqué un instant par un autre programme
            return true;
        }
    }
}