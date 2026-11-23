using KEYCE_KODER.Core.Models;

namespace KEYCE_KODER.Core.Services;

public class DocumentService
{
    private readonly DocumentManager _documentManager;
    private readonly FileService _fileService;

    public DocumentService(
        DocumentManager documentManager,
        FileService fileService)
    {
        _documentManager = documentManager;
        _fileService = fileService;
    }

    public Document CreateDocument()
    {
        return _documentManager.CreateDocument();
    }

    public async Task<Document> OpenDocumentAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        string content = await _fileService.ReadFileAsync(
            filePath,
            cancellationToken
        );

        return _documentManager.OpenDocument(filePath, content);
    }

    public async Task SaveDocumentAsync(
        Document document,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(document.FilePath))
        {
            throw new InvalidOperationException(
                "Ce document n'a pas encore de chemin de fichier."
            );
        }

        await _fileService.SaveFileAsync(
            document.FilePath,
            document.Content,
            cancellationToken
        );

        document.MarkSaved();
    }

    public async Task SaveDocumentAsAsync(
        Document document,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await _fileService.SaveFileAsync(
            filePath,
            document.Content,
            cancellationToken
        );

        document.MarkSaved(filePath);
    }
}