
using System.IO;

namespace KEYCE_KODER.Core.Models;

public class Document
{
    private string _content;

    public Guid Id { get; } = Guid.NewGuid();

    public string? FilePath { get; private set; }

    public string Name =>
        string.IsNullOrWhiteSpace(FilePath)
            ? "Sans titre"
            : Path.GetFileName(FilePath);

    public string Content
    {
        get => _content;
        set
        {
            if (_content == value)
                return;

            _content = value;
            IsDirty = true;
        }
    }

    public bool IsDirty { get; private set; }

    public Document(string? filePath = null, string content = "")
    {
        FilePath = filePath;
        _content = content;
        IsDirty = false;
    }

    public void MarkSaved(string? filePath = null)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
            FilePath = filePath;

        IsDirty = false;
    }
}
