using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace KEYCE_KODER.Editor.Folding;

public static class XmlFoldingDetector
{
    public static IReadOnlyList<FoldingRegion> Detect(string text)
    {
        var regions = new List<FoldingRegion>();

        if (string.IsNullOrWhiteSpace(text))
            return regions;

        try
        {
            using var reader = XmlReader.Create(
                new StringReader(text),
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Ignore,
                    XmlResolver = null,
                    IgnoreWhitespace = false
                });

            var document = XDocument.Load(
                reader,
                LoadOptions.SetLineInfo);

            foreach (var element in document.Descendants())
            {
                if (element.IsEmpty)
                    continue;

                if (element.FirstNode == null)
                    continue;

                if (element.LastNode == null)
                    continue;

                if (element.FirstNode == element.LastNode &&
                    element.FirstNode is XText)
                {
                    continue;
                }

                if (element is not IXmlLineInfo startInfo ||
                    !startInfo.HasLineInfo())
                {
                    continue;
                }

                int startLine = startInfo.LineNumber;
                int endLine = startLine;

                if (element.LastNode is IXmlLineInfo endInfo &&
                    endInfo.HasLineInfo())
                {
                    endLine = endInfo.LineNumber;
                }

                if (endLine <= startLine)
                    continue;

                int startOffset = GetLineStart(text, startLine);
                int endOffset = GetLineStart(text, endLine);

                if (startOffset < 0 || endOffset <= startOffset)
                    continue;

                regions.Add(new FoldingRegion(
                    startOffset,
                    endOffset,
                    "..."
                ));
            }
        }
        catch (XmlException)
        {
            // Le document est incomplet ou temporairement invalide.
            // Aucun pliage n'est proposé jusqu'à ce qu'il redevienne valide.
        }

        return regions;
    }

    private static int GetLineStart(string text, int lineNumber)
    {
        if (lineNumber < 1)
            return -1;

        int currentLine = 1;

        for (int i = 0; i < text.Length; i++)
        {
            if (currentLine == lineNumber)
                return i;

            if (text[i] == '\n')
                currentLine++;
        }

        return currentLine == lineNumber
            ? text.Length
            : -1;
    }
}
