using System;
using System.Collections.Generic;

namespace KEYCE_KODER.Editor.Folding;

public static class HtmlFoldingDetector
{
    private static readonly HashSet<string> VoidElements =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "area", "base", "br", "col", "embed", "hr",
            "img", "input", "link", "meta", "param",
            "source", "track", "wbr"
        };

    public static IReadOnlyList<FoldingRegion> Detect(string text)
    {
        var regions = new List<FoldingRegion>();

        if (string.IsNullOrWhiteSpace(text))
            return regions;

        var openings = new List<(string Name, int ContentStart)>();
        int i = 0;

        while (i < text.Length)
        {
            if (text[i] != '<')
            {
                i++;
                continue;
            }

            if (StartsWith(text, i, "<!--"))
            {
                int commentEnd = text.IndexOf("-->", i + 4, StringComparison.Ordinal);

                if (commentEnd < 0)
                    break;

                AddRegion(regions, i + 4, commentEnd, text);
                i = commentEnd + 3;
                continue;
            }

            if (i + 1 >= text.Length ||
                text[i + 1] == '!' ||
                text[i + 1] == '?')
            {
                int declarationEnd = FindTagEnd(text, i + 1);

                if (declarationEnd < 0)
                    break;

                i = declarationEnd + 1;
                continue;
            }

            bool closing = text[i + 1] == '/';
            int nameStart = i + (closing ? 2 : 1);

            if (nameStart >= text.Length || !IsNameStart(text[nameStart]))
            {
                i++;
                continue;
            }

            int nameEnd = nameStart;

            while (nameEnd < text.Length && IsNameCharacter(text[nameEnd]))
                nameEnd++;

            string name = text.Substring(nameStart, nameEnd - nameStart);
            int tagEnd = FindTagEnd(text, nameEnd);

            if (tagEnd < 0)
                break;

            bool selfClosing = IsSelfClosing(text, nameEnd, tagEnd);

            if (closing)
            {
                int matchingIndex = -1;

                for (int s = openings.Count - 1; s >= 0; s--)
                {
                    if (string.Equals(
                        openings[s].Name,
                        name,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        matchingIndex = s;
                        break;
                    }
                }

                if (matchingIndex >= 0)
                {
                    AddRegion(
                        regions,
                        openings[matchingIndex].ContentStart,
                        i,
                        text);

                    openings.RemoveRange(
                        matchingIndex,
                        openings.Count - matchingIndex);
                }

                i = tagEnd + 1;
                continue;
            }

            if (selfClosing || VoidElements.Contains(name))
            {
                i = tagEnd + 1;
                continue;
            }

            openings.Add((name, tagEnd + 1));

            if (name.Equals("script", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("style", StringComparison.OrdinalIgnoreCase))
            {
                int closeStart = text.IndexOf(
                    "</" + name,
                    tagEnd + 1,
                    StringComparison.OrdinalIgnoreCase);

                i = closeStart >= 0 ? closeStart : text.Length;
            }
            else
            {
                i = tagEnd + 1;
            }
        }

        return regions;
    }

    private static void AddRegion(
        List<FoldingRegion> regions,
        int contentStart,
        int closingStart,
        string text)
    {
        int start = FindLineEnd(text, contentStart);
        int end = FindLineStart(text, closingStart);

        if (end > start)
            regions.Add(new FoldingRegion(start, end, "..."));
    }

    private static int FindTagEnd(string text, int start)
    {
        char quote = '\0';

        for (int i = start; i < text.Length; i++)
        {
            char current = text[i];

            if (quote != '\0')
            {
                if (current == quote)
                    quote = '\0';

                continue;
            }

            if (current == '"' || current == '\'')
            {
                quote = current;
                continue;
            }

            if (current == '>')
                return i;
        }

        return -1;
    }

    private static bool IsSelfClosing(string text, int start, int end)
    {
        int i = end - 1;

        while (i >= start && char.IsWhiteSpace(text[i]))
            i--;

        return i >= start && text[i] == '/';
    }

    private static int FindLineEnd(string text, int offset)
    {
        while (offset < text.Length && text[offset] != '\n')
            offset++;

        return offset < text.Length ? offset + 1 : offset;
    }

    private static int FindLineStart(string text, int offset)
    {
        while (offset > 0 && text[offset - 1] != '\n')
            offset--;

        return offset;
    }

    private static bool StartsWith(string text, int offset, string value)
    {
        return offset + value.Length <= text.Length &&
               string.Compare(
                   text, offset, value, 0, value.Length,
                   StringComparison.Ordinal) == 0;
    }

    private static bool IsNameStart(char c)
    {
        return char.IsLetter(c) || c == '_';
    }

    private static bool IsNameCharacter(char c)
    {
        return char.IsLetterOrDigit(c) ||
               c == '-' || c == '_' || c == ':';
    }
}
