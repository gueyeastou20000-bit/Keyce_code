using System.Linq;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Folding;

namespace KEYCE_KODER.Editor.Folding;

public sealed class CodeFoldingController
{
    private readonly TextEditor _editor;
    private FoldingManager? _manager;
    private string? _filePath;

    public CodeFoldingController(TextEditor editor)
    {
        _editor = editor;

        _editor.TextChanged += (_, _) => UpdateFoldings();
    }

    public void SetLanguage(string? filePath)
    {
        _filePath = filePath;
        UpdateFoldings();
    }

    public void UpdateFoldings()
    {
        if (_manager == null)
            _manager = FoldingManager.Install(_editor.TextArea);

        var regions = FoldingDetectionService.Detect(
            _editor.Text,
            _filePath);

        var foldings = regions
            .Where(region =>
                region.StartOffset >= 0 &&
                region.EndOffset <= _editor.Document.TextLength &&
                region.EndOffset > region.StartOffset)
            .OrderBy(region => region.StartOffset)
            .Select(region => new NewFolding(
                region.StartOffset,
                region.EndOffset)
            {
                Name = region.Title
            })
            .ToList();

        _manager.UpdateFoldings(foldings, -1);
    }

    public void FoldAll()
    {
        if (_manager == null)
            return;

        foreach (var folding in _manager.AllFoldings)
            folding.IsFolded = true;
    }

    public void UnfoldAll()
    {
        if (_manager == null)
            return;

        foreach (var folding in _manager.AllFoldings)
            folding.IsFolded = false;
    }

    public void Dispose()
    {
        if (_manager == null)
            return;

        FoldingManager.Uninstall(_manager);
        _manager = null;
    }
}
