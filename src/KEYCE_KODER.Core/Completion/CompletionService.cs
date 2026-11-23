using System.Text.RegularExpressions;
using KEYCE_KODER.Core.Languages;

namespace KEYCE_KODER.Core.Completion;

/// <summary>
/// Calcule les suggestions d'autocomplétion à une position du texte :
/// mots-clés et fonctions du langage + mots déjà présents dans le fichier.
/// Ne dépend ni de WPF ni d'AvalonEdit : testable unitairement.
/// </summary>
public sealed class CompletionService
{
    public const int MinWordLength = 3;
    public const int MaxItems = 300;

    /// <summary>Caractère de mot « générique » (lettre, chiffre, « _ »).</summary>
    public static bool IsIdentifierChar(char c) => LanguageCatalog.PlainText.IsWordChar(c);

    /// <summary>Le mot en cours de frappe juste avant le curseur, et sa position de début.</summary>
    public static (int Start, string Prefix) GetPrefix(
        string text, int caretOffset, LanguageDefinition? language = null)
    {
        language ??= LanguageCatalog.PlainText;
        caretOffset = Math.Clamp(caretOffset, 0, text.Length);

        int start = caretOffset;
        while (start > 0 && language.IsWordChar(text[start - 1]))
            start--;

        return (start, text[start..caretOffset]);
    }

    public IReadOnlyList<CompletionItem> GetCompletions(string text, int caretOffset, string? filePath)
    {
        LanguageDefinition language = LanguageCatalog.FromPath(filePath);
        (int start, string prefix) = GetPrefix(text, caretOffset, language);

        // Clé = texte de la suggestion : un même mot n'apparaît qu'une fois
        var results = new Dictionary<string, CompletionItem>(StringComparer.Ordinal);

        // 0. Structures de code (snippets)
        foreach (Snippet snippet in SnippetCatalog.For(language.Id))
        {
            if (StartsWith(snippet.Prefix, prefix))
                results.TryAdd(
                    "snippet:" + snippet.Prefix,
                    new CompletionItem(snippet.Prefix, CompletionKind.Snippet, snippet.Description, snippet.Body));
        }

        // 1. Mots-clés et fonctions du langage (prioritaires)
        foreach (string keyword in language.Keywords)
        {
            if (StartsWith(keyword, prefix))
                results.TryAdd(keyword, new CompletionItem(keyword, CompletionKind.Keyword, $"Mot-clé {language.DisplayName}"));
        }

        foreach (string builtin in language.Builtins)
        {
            if (StartsWith(builtin, prefix))
                results.TryAdd(builtin, new CompletionItem(builtin, CompletionKind.Builtin, $"{language.DisplayName} : fonction / objet courant"));
        }

        // 2. Mots déjà présents dans le fichier
        foreach (Match match in language.WordRegex.Matches(text))
        {
            if (match.Length < MinWordLength)
                continue;

            // Le mot en train d'être tapé n'est pas une suggestion
            if (match.Index == start)
                continue;

            if (StartsWith(match.Value, prefix))
                results.TryAdd(match.Value, new CompletionItem(match.Value, CompletionKind.Word, "Mot présent dans le fichier"));
        }

        return results.Values
            // Inutile de proposer exactement ce qui est déjà tapé... sauf un snippet :
            // taper « for » doit justement proposer la boucle complète.
            .Where(item => item.Kind == CompletionKind.Snippet || item.Text != prefix)
            .OrderBy(item => item.Text.StartsWith(prefix, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Kind == CompletionKind.Snippet ? 0 : 1) // à texte égal, le snippet d'abord
            .Take(MaxItems)
            .ToList();
    }

    private static bool StartsWith(string candidate, string prefix) =>
        candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}