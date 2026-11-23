
using KEYCE_KODER.Core.Models;

namespace KEYCE_KODER.Core.Services;

public class DocumentManager
{
    private readonly List<Document> _documents = new();

    public IReadOnlyList<Document> Documents => _documents.AsReadOnly();

    public Document CreateDocument()
    {
        var document = new Document();

        _documents.Add(document);

        return document;
    }

    public Document OpenDocument(string filePath, string content)
    {
        string fullPath = Path.GetFullPath(filePath);

        var existingDocument = _documents.FirstOrDefault(
            document =>
                document.FilePath != null &&
                string.Equals(
                    Path.GetFullPath(document.FilePath),
                    fullPath,
                    StringComparison.OrdinalIgnoreCase
                )
        );

        if (existingDocument != null)
            return existingDocument;

        var newDocument = new Document(fullPath, content);

        _documents.Add(newDocument);

        return newDocument;
    }

    public Document? GetDocument(Guid id)
    {
        return _documents.FirstOrDefault(document => document.Id == id);
    }

    public bool CloseDocument(Guid id)
    {
        var document = GetDocument(id);

        if (document == null)
            return false;

        return _documents.Remove(document);
    }
}
