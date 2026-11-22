
using KEYCE_KODER.Core.Interfaces;
using KEYCE_KODER.Core.Models;
using KEYCE_KODER.Editor.Controls;

namespace KEYCE_KODER.Editor.Services;

public class EditorService : IEditorService
{
    private readonly CodeEditor _editor;
    private Document? _currentDocument;
    private bool _isLoadingDocument;

    public EditorService(CodeEditor editor)
    {
        _editor = editor;

        _editor.TextChanged += (_, _) =>
        {
            if (!_isLoadingDocument && _currentDocument != null)
                _currentDocument.Content = _editor.Text;
        };
    }

    public string Text
    {
        get => _editor.Text;
        set
        {
            _editor.Text = value;

            if (_currentDocument != null)
                _currentDocument.Content = value;
        }
    }

    public string? FilePath => _currentDocument?.FilePath;

    public bool IsModified => _currentDocument?.IsDirty ?? false;

    public void SetText(string text)
    {
        Text = text;
    }

    public void SetLanguage(string? filePath)
    {
        _editor.SetLanguage(filePath);
    }

    public void SetDocument(Document document)
    {
        _isLoadingDocument = true;

        try
        {
            _editor.Text = document.Content;
        }
        finally
        {
            _isLoadingDocument = false;
        }

        _currentDocument = document;
        _editor.SetLanguage(document.FilePath);
    }

    public void MarkSaved(string? filePath = null)
    {
        _currentDocument?.MarkSaved(filePath);
    }
}
