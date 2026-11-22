using System.IO;
using System.Windows.Controls;
using System.Windows.Threading;
using KEYCE_KODER.Core.Models;

namespace KEYCE_KODER.App.Tabs;

public sealed class TabManager
{
    private readonly TabControl _tabControl;
    private readonly List<EditorTab> _tabs = new();

    public event EventHandler? ActiveTabChanged;

    public event EventHandler? ActiveEditorStateChanged;

    public event EventHandler<EditorTab>? CloseRequested;

    public IReadOnlyList<EditorTab> Tabs => _tabs;

    public EditorTab? ActiveTab => (_tabControl.SelectedItem as TabItem)?.Tag as EditorTab;

    public TabManager(TabControl tabControl)
    {
        _tabControl = tabControl;

        _tabControl.SelectionChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.OriginalSource, _tabControl))
                return;

            ActiveTabChanged?.Invoke(this, EventArgs.Empty);
            FocusActiveEditorLater();
        };
    }
    public EditorTab Open(Document document)
    {
        EditorTab? existing = _tabs.FirstOrDefault(t => t.Document.Id == document.Id);

        if (existing != null)
        {
            Activate(existing);
            return existing;
        }

        var tab = new EditorTab(document);

        tab.CloseRequested += (_, _) => CloseRequested?.Invoke(this, tab);
        tab.StateChanged += (_, _) => RaiseIfActive(tab);
        tab.Actions.CaretPositionChanged += (_, _) => RaiseIfActive(tab);

        _tabs.Add(tab);
        _tabControl.Items.Add(tab.TabItem);

        Activate(tab);

        return tab;
    }

    public EditorTab? FindByPath(string filePath)
    {
        string fullPath = Path.GetFullPath(filePath);

        return _tabs.FirstOrDefault(t =>
            t.Document.FilePath != null &&
            string.Equals(
                Path.GetFullPath(t.Document.FilePath),
                fullPath,
                StringComparison.OrdinalIgnoreCase));
    }

    public void Activate(EditorTab tab)
    {
        _tabControl.SelectedItem = tab.TabItem;
        FocusActiveEditorLater();
    }

    public void Remove(EditorTab tab)
    {
        _tabs.Remove(tab);
        _tabControl.Items.Remove(tab.TabItem);

        if (_tabs.Count == 0)
            ActiveTabChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ActivateNext() => ActivateRelative(+1);

    public void ActivatePrevious() => ActivateRelative(-1);

    private void ActivateRelative(int step)
    {
        if (_tabs.Count < 2 || ActiveTab is null)
            return;

        int index = _tabs.IndexOf(ActiveTab);
        int next = (index + step + _tabs.Count) % _tabs.Count;

        Activate(_tabs[next]);
    }

    private void RaiseIfActive(EditorTab tab)
    {
        if (ReferenceEquals(tab, ActiveTab))
            ActiveEditorStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void FocusActiveEditorLater()
    {
        _tabControl.Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() => ActiveTab?.Actions.Focus()));
    }
}