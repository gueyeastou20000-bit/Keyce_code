using System;
using System.Collections.Generic;

namespace KEYCE_KODER.Editor.Folding;

public static class PythonFoldingDetector
{
    private sealed record LineInfo(
        int Start,
        int End,
        int Indent,
        bool HasCode,
        bool IsBlockHeader
    );

    public static IReadOnlyList<FoldingRegion> Detect(string text)
    {
        var lines = new List<LineInfo>();
        var regions = new List<FoldingRegion>();

        string? tripleQuote = null;
        int offset = 0;

        while (offset < text.Length)
        {
            int lineEnd = text.IndexOf('\n', offset);

            if (lineEnd < 0)
                lineEnd = text.Length;

            int contentEnd = lineEnd;

            if (contentEnd > offset &&
                text[contentEnd - 1] == '\r')
            {
                contentEnd--;
            }

            string line = text.Substring(offset, contentEnd - offset);

            int indent = 0;
            int position = 0;

            while (position < line.Length)
            {
                if (line[position] == ' ')
                {
                    indent++;
                    position++;
                }
                else if (line[position] == '\t')
                {
                    indent += 4;
                    position++;
                }
                else
                {
                    break;
                }
            }

            string code = ExtractCode(line, ref tripleQuote);
            string trimmed = code.TrimEnd();

            bool hasCode = !string.IsNullOrWhiteSpace(trimmed);

            bool isHeader =
                hasCode &&
                trimmed.EndsWith(":", StringComparison.Ordinal);

            lines.Add(new LineInfo(
                offset,
                lineEnd < text.Length ? lineEnd + 1 : lineEnd,
                indent,
                hasCode,
                isHeader
            ));

            offset = lineEnd < text.Length
                ? lineEnd + 1
                : text.Length;
        }

        for (int i = 0; i < lines.Count; i++)
        {
            LineInfo header = lines[i];

            if (!header.IsBlockHeader)
                continue;

            int firstBodyLine = -1;

            for (int j = i + 1; j < lines.Count; j++)
            {
                if (!lines[j].HasCode)
                    continue;

                if (lines[j].Indent <= header.Indent)
                    break;

                firstBodyLine = j;
                break;
            }

            if (firstBodyLine < 0)
                continue;

            int lastBodyLine = firstBodyLine;

            for (int j = firstBodyLine + 1; j < lines.Count; j++)
            {
                if (!lines[j].HasCode)
                    continue;

                if (lines[j].Indent <= header.Indent)
                    break;

                lastBodyLine = j;
            }

            int start = header.End;
            int end = lines[lastBodyLine].End;

            if (start < end)
            {
                regions.Add(new FoldingRegion(
                    start,
                    end,
                    "..."
                ));
            }
        }

        return regions;
    }

    private static string ExtractCode(
        string line,
        ref string? tripleQuote)
    {
        var code = new System.Text.StringBuilder();

        char quote = '\0';
        bool escaped = false;

        for (int i = 0; i < line.Length; i++)
        {
            char current = line[i];

            if (tripleQuote != null)
            {
                if (i + 2 < line.Length &&
                    line.Substring(i, 3) == tripleQuote)
                {
                    tripleQuote = null;
                    i += 2;
                }

                continue;
            }

            if (quote != '\0')
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (current == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (current == quote)
                    quote = '\0';

                continue;
            }

            if (current == '#')
                break;

            if ((current == '"' || current == '\'') &&
                i + 2 < line.Length &&
                line[i + 1] == current &&
                line[i + 2] == current)
            {
                tripleQuote = new string(current, 3);
                i += 2;
                continue;
            }

            if (current == '"' || current == '\'')
            {
                quote = current;
                continue;
            }

            code.Append(current);
        }

        return code.ToString();
    }
}
