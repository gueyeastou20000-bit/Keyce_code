using System.IO;
using System.Reflection;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace KEYCE_KODER.Editor.Highlighting;

public static class LanguageHighlighter
{
    private static readonly HighlightingManager Manager =
        HighlightingManager.Instance;

    private static IHighlightingDefinition? _pythonDefinition;

    public static IHighlightingDefinition? GetDefinition(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        string extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (extension == ".py" || extension == ".pyw")
            return LoadPythonDefinition();

        return Manager.GetDefinitionByExtension(extension);
    }

    private static IHighlightingDefinition? LoadPythonDefinition()
    {
        if (_pythonDefinition != null)
            return _pythonDefinition;

        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(
            "KEYCE_KODER.Editor.Highlighting.Python-Mode.xshd");

        if (stream == null)
            throw new FileNotFoundException(
                "Le fichier Python-Mode.xshd est introuvable.");

        using var reader = XmlReader.Create(stream);

        _pythonDefinition = HighlightingLoader.Load(reader, Manager);

        return _pythonDefinition;
    }
}