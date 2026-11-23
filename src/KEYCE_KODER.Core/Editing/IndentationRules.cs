using KEYCE_KODER.Core.Languages;

namespace KEYCE_KODER.Core.Editing;

/// <summary>Règles d'indentation automatique (indépendantes de l'éditeur, testables).</summary>
public static class IndentationRules
{
    /// <summary>Les espaces et tabulations au début d'une ligne.</summary>
    public static string LeadingWhitespace(string line)
    {
        int i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
            i++;

        return line[..i];
    }

    /// <summary>Indentation de la nouvelle ligne créée par Entrée après « previousLine ».</summary>
    public static string IndentAfter(string previousLine, LanguageDefinition language, string indentUnit)
    {
        string indent = LeadingWhitespace(previousLine);
        string code = StripComment(previousLine, language).TrimEnd();

        if (code.Length == 0)
            return indent;

        char last = code[^1];
        bool opensBlock = last is '{' or '[' or '('
                          || (language.IndentAfterColon && last == ':');

        return opensBlock ? indent + indentUnit : indent;
    }

    /// <summary>Retire un niveau d'indentation.</summary>
    public static string Dedent(string indent, string indentUnit)
    {
        if (indent.EndsWith(indentUnit, StringComparison.Ordinal))
            return indent[..^indentUnit.Length];

        if (indent.EndsWith('\t'))
            return indent[..^1];

        int remove = Math.Min(indent.Length, indentUnit.Length);
        return indent[..^remove];
    }

    /// <summary>Le curseur est-il entre une paire vide : {|}, [|] ou (|) ?</summary>
    public static bool IsEmptyPair(char previous, char next) =>
        (previous, next) is ('{', '}') or ('[', ']') or ('(', ')');

    private static string StripComment(string line, LanguageDefinition language)
    {
        if (language.LineComment is null)
            return line;

        int index = line.IndexOf(language.LineComment, StringComparison.Ordinal);
        return index >= 0 ? line[..index] : line;
    }
}