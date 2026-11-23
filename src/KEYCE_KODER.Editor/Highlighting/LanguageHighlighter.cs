using System.IO;
using ICSharpCode.AvalonEdit.Highlighting;

namespace KEYCE_KODER.Editor.Highlighting;

public static class LanguageHighlighter
{
    private static readonly HighlightingManager Manager =
        HighlightingManager.Instance;

    public static IHighlightingDefinition? GetDefinition(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        string extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (string.IsNullOrEmpty(extension))
            return null;

        return Manager.GetDefinitionByExtension(extension);
    }
}
