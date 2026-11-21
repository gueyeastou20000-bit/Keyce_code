using KEYCE_KODER.Core.Models;

namespace KEYCE_KODER.Core.Interfaces;

public interface IEditorService
{
    string Text { get; set; }

    string? FilePath { get; }

    bool IsModified { get; }

    void SetText(string text);

    void SetLanguage(string? filePath);

    void SetDocument(Document document);

    void MarkSaved(string? filePath = null);
}