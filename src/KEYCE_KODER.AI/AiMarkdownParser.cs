using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace KEYCE_KODER.AI;

public abstract record AiMarkdownBlock;

public sealed record AiMarkdownHeadingBlock(int Level, IReadOnlyList<AiMarkdownInline> Inlines) : AiMarkdownBlock;
public sealed record AiMarkdownParagraphBlock(IReadOnlyList<AiMarkdownInline> Inlines) : AiMarkdownBlock;
public sealed record AiMarkdownCodeBlock(string Language, string Code) : AiMarkdownBlock;
public sealed record AiMarkdownListItemBlock(bool IsOrdered, string Marker, IReadOnlyList<AiMarkdownInline> Inlines) : AiMarkdownBlock;
public sealed record AiMarkdownQuoteBlock(IReadOnlyList<AiMarkdownInline> Inlines) : AiMarkdownBlock;

public abstract record AiMarkdownInline;

public sealed record AiMarkdownTextInline(string Text) : AiMarkdownInline;
public sealed record AiMarkdownBoldInline(string Text) : AiMarkdownInline;
public sealed record AiMarkdownItalicInline(string Text) : AiMarkdownInline;
public sealed record AiMarkdownBoldItalicInline(string Text) : AiMarkdownInline;
public sealed record AiMarkdownCodeInline(string Code) : AiMarkdownInline;
public sealed record AiMarkdownLinkInline(string Text, string Url) : AiMarkdownInline;

/// <summary>
/// Parseur Markdown léger et tolérant aux flux incomplets pour les réponses de KEYCE IA.
/// </summary>
public static partial class AiMarkdownParser
{
    [GeneratedRegex(@"^(#{1,6})\s+(.+)$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^(\s*)[-*+]\s+(.+)$")]
    private static partial Regex BulletListRegex();

    [GeneratedRegex(@"^(\s*)(\d+\.)\s+(.+)$")]
    private static partial Regex OrderedListRegex();

    [GeneratedRegex(@"^>\s?(.*)$")]
    private static partial Regex QuoteRegex();

    public static IReadOnlyList<AiMarkdownBlock> Parse(string? markdown)
    {
        var blocks = new List<AiMarkdownBlock>();
        if (string.IsNullOrEmpty(markdown))
            return blocks;

        string normalized = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = normalized.Split('\n');

        var paragraphLines = new List<string>();
        var quoteLines = new List<string>();
        bool inCodeBlock = false;
        string codeLanguage = string.Empty;
        var codeLines = new List<string>();

        void FlushParagraph()
        {
            if (paragraphLines.Count == 0)
                return;

            string text = string.Join("\n", paragraphLines).TrimEnd();
            paragraphLines.Clear();
            if (!string.IsNullOrWhiteSpace(text))
                blocks.Add(new AiMarkdownParagraphBlock(ParseInlines(text)));
        }

        void FlushQuote()
        {
            if (quoteLines.Count == 0)
                return;

            string text = string.Join("\n", quoteLines).TrimEnd();
            quoteLines.Clear();
            blocks.Add(new AiMarkdownQuoteBlock(ParseInlines(text)));
        }

        foreach (string rawLine in lines)
        {
            string trimmedStart = rawLine.TrimStart();

            if (inCodeBlock)
            {
                if (trimmedStart.StartsWith("```", StringComparison.Ordinal))
                {
                    blocks.Add(new AiMarkdownCodeBlock(codeLanguage, string.Join("\n", codeLines)));
                    codeLines.Clear();
                    codeLanguage = string.Empty;
                    inCodeBlock = false;
                }
                else
                {
                    codeLines.Add(rawLine);
                }
                continue;
            }

            if (trimmedStart.StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph();
                FlushQuote();
                inCodeBlock = true;
                codeLanguage = trimmedStart[3..].Trim();
                codeLines.Clear();
                continue;
            }

            if (string.IsNullOrWhiteSpace(rawLine))
            {
                FlushParagraph();
                FlushQuote();
                continue;
            }

            var quoteMatch = QuoteRegex().Match(rawLine);
            if (quoteMatch.Success)
            {
                FlushParagraph();
                quoteLines.Add(quoteMatch.Groups[1].Value);
                continue;
            }
            FlushQuote();

            var headingMatch = HeadingRegex().Match(trimmedStart);
            if (headingMatch.Success)
            {
                FlushParagraph();
                int level = headingMatch.Groups[1].Value.Length;
                string content = headingMatch.Groups[2].Value.Trim();
                blocks.Add(new AiMarkdownHeadingBlock(level, ParseInlines(content)));
                continue;
            }

            var bulletMatch = BulletListRegex().Match(rawLine);
            if (bulletMatch.Success)
            {
                FlushParagraph();
                blocks.Add(new AiMarkdownListItemBlock(false, "•", ParseInlines(bulletMatch.Groups[2].Value.Trim())));
                continue;
            }

            var orderedMatch = OrderedListRegex().Match(rawLine);
            if (orderedMatch.Success)
            {
                FlushParagraph();
                blocks.Add(new AiMarkdownListItemBlock(true, orderedMatch.Groups[2].Value, ParseInlines(orderedMatch.Groups[3].Value.Trim())));
                continue;
            }

            paragraphLines.Add(rawLine);
        }

        if (inCodeBlock)
        {
            // Bloc de code non fermé (ex: réponse tronquée ou en cours de génération) : on le préserve.
            blocks.Add(new AiMarkdownCodeBlock(codeLanguage, string.Join("\n", codeLines)));
        }

        FlushQuote();
        FlushParagraph();

        return blocks;
    }

    public static IReadOnlyList<AiMarkdownInline> ParseInlines(string text)
    {
        var inlines = new List<AiMarkdownInline>();
        if (string.IsNullOrEmpty(text))
            return inlines;

        var plain = new StringBuilder();
        int i = 0;

        void FlushPlain()
        {
            if (plain.Length > 0)
            {
                inlines.Add(new AiMarkdownTextInline(plain.ToString()));
                plain.Clear();
            }
        }

        while (i < text.Length)
        {
            // 1. Code inline : `code`
            if (text[i] == '`')
            {
                int close = text.IndexOf('`', i + 1);
                if (close > i + 1)
                {
                    FlushPlain();
                    inlines.Add(new AiMarkdownCodeInline(text[(i + 1)..close]));
                    i = close + 1;
                    continue;
                }
            }

            // 2. Lien Markdown : [texte](url)
            if (text[i] == '[')
            {
                int closeBracket = text.IndexOf(']', i + 1);
                if (closeBracket > i + 1 && closeBracket + 1 < text.Length && text[closeBracket + 1] == '(')
                {
                    int closeParen = text.IndexOf(')', closeBracket + 2);
                    if (closeParen > closeBracket + 2)
                    {
                        string linkText = text[(i + 1)..closeBracket];
                        string url = text[(closeBracket + 2)..closeParen].Trim();
                        if (!string.IsNullOrEmpty(url))
                        {
                            FlushPlain();
                            inlines.Add(new AiMarkdownLinkInline(linkText, url));
                            i = closeParen + 1;
                            continue;
                        }
                    }
                }
            }

            // 3. Gras + Italique : ***texte***
            if (i + 2 < text.Length && text.AsSpan(i, 3).SequenceEqual("***"))
            {
                int close = text.IndexOf("***", i + 3, StringComparison.Ordinal);
                if (close > i + 3)
                {
                    FlushPlain();
                    inlines.Add(new AiMarkdownBoldItalicInline(text[(i + 3)..close]));
                    i = close + 3;
                    continue;
                }
            }

            // 4. Gras : **texte** ou __texte__
            if (i + 1 < text.Length &&
                ((text[i] == '*' && text[i + 1] == '*') || (text[i] == '_' && text[i + 1] == '_')))
            {
                string marker = text.Substring(i, 2);
                int close = text.IndexOf(marker, i + 2, StringComparison.Ordinal);
                if (close > i + 2)
                {
                    FlushPlain();
                    inlines.Add(new AiMarkdownBoldInline(text[(i + 2)..close]));
                    i = close + 2;
                    continue;
                }
            }

            // 5. Italique : *texte* ou _texte_
            if (text[i] == '*' || text[i] == '_')
            {
                char marker = text[i];
                bool prevIsWord = i > 0 && char.IsLetterOrDigit(text[i - 1]);
                if (!prevIsWord || marker == '*')
                {
                    int close = text.IndexOf(marker, i + 1);
                    if (close > i + 1 && text[close - 1] != ' ')
                    {
                        FlushPlain();
                        inlines.Add(new AiMarkdownItalicInline(text[(i + 1)..close]));
                        i = close + 1;
                        continue;
                    }
                }
            }

            plain.Append(text[i]);
            i++;
        }

        FlushPlain();
        return inlines;
    }
}
