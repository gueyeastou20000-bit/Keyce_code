using System;
using System.Collections.Generic;
using System.IO;

namespace KEYCE_KODER.Editor.Folding;

public static class FoldingDetectionService
{
    public static IReadOnlyList<FoldingRegion> Detect(
        string text,
        string? filePath)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            string.IsNullOrWhiteSpace(filePath))
        {
            return Array.Empty<FoldingRegion>();
        }

        string extension = Path.GetExtension(filePath)
            .ToLowerInvariant();

        return extension switch
        {
            ".c" or ".h" or ".cs" or ".js" or ".css" =>
                BraceFoldingDetector.Detect(text),

            ".json" =>
                BraceFoldingDetector.Detect(
                    text,
                    includeSquareBrackets: true),

            ".py" =>
                PythonFoldingDetector.Detect(text),

            ".html" or ".htm" or ".xml" or ".xaml" =>
                XmlFoldingDetector.Detect(text),

            _ =>
                Array.Empty<FoldingRegion>()
        };
    }
}
