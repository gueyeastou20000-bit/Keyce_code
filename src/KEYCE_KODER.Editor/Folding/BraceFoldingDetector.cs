using System.Linq;
using System.Collections.Generic;

namespace KEYCE_KODER.Editor.Folding;

public static class BraceFoldingDetector
{
    public static IReadOnlyList<FoldingRegion> Detect(
        string text,
        bool includeSquareBrackets = false)
    {
        var regions = new List<FoldingRegion>();
        var stack = new Stack<(char Character, int Offset)>();

        bool inLineComment = false;
        bool inBlockComment = false;
        char stringDelimiter = '\0';
        bool escaped = false;

        for (int i = 0; i < text.Length; i++)
        {
            char current = text[i];
            char next = i + 1 < text.Length ? text[i + 1] : '\0';

            if (inLineComment)
            {
                if (current == '\n')
                    inLineComment = false;

                continue;
            }

            if (inBlockComment)
            {
                if (current == '*' && next == '/')
                {
                    inBlockComment = false;
                    i++;
                }

                continue;
            }

            if (stringDelimiter != '\0')
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

                if (current == stringDelimiter)
                    stringDelimiter = '\0';

                continue;
            }

            if (current == '/' && next == '/')
            {
                inLineComment = true;
                i++;
                continue;
            }

            if (current == '/' && next == '*')
            {
                inBlockComment = true;
                i++;
                continue;
            }

            if (current == '"' || current == '\'' || current == '`')
            {
                stringDelimiter = current;
                continue;
            }

            if (current == '{' ||
                (includeSquareBrackets && current == '['))
            {
                stack.Push((current, i));
                continue;
            }

            if (current == '}' ||
                (includeSquareBrackets && current == ']'))
            {
                if (stack.Count == 0)
                    continue;

                var opening = stack.Peek();

                bool matching =
                    (opening.Character == '{' && current == '}') ||
                    (opening.Character == '[' && current == ']');

                if (!matching)
                    continue;

                stack.Pop();

                int start = FindLineEnd(text, opening.Offset);
                int end = FindLineStart(text, i);

                if (start < end)
                {
                    regions.Add(new FoldingRegion(
                        start,
                        end,
                        "..."
                    ));
                }
            }
        }

        return regions
            .OrderBy(region => region.StartOffset)
            .ToList();
    }

    private static int FindLineEnd(string text, int offset)
    {
        int position = offset;

        while (position < text.Length &&
               text[position] != '\n')
        {
            position++;
        }

        return position < text.Length
            ? position + 1
            : position;
    }

    private static int FindLineStart(string text, int offset)
    {
        int position = offset;

        while (position > 0 &&
               text[position - 1] != '\n')
        {
            position--;
        }

        return position;
    }
}

