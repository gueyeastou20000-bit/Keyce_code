using System.Text.RegularExpressions;

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

    // Un identifiant : une lettre ou « _ », puis lettres, chiffres ou « _ »
    private static readonly Regex WordRegex = new(@"[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);

    public static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>Le mot en cours de frappe juste avant le curseur, et sa position de début.</summary>
    public static (int Start, string Prefix) GetPrefix(string text, int caretOffset)
    {
        caretOffset = Math.Clamp(caretOffset, 0, text.Length);

        int start = caretOffset;
        while (start > 0 && IsIdentifierChar(text[start - 1]))
            start--;

        return (start, text[start..caretOffset]);
    }

    public IReadOnlyList<CompletionItem> GetCompletions(string text, int caretOffset, string? filePath)
    {
        (int start, string prefix) = GetPrefix(text, caretOffset);

        // Clé = texte de la suggestion : un même mot n'apparaît qu'une fois
        var results = new Dictionary<string, CompletionItem>(StringComparer.Ordinal);

        // 1. Mots-clés et fonctions du langage (prioritaires)
        foreach (CompletionItem item in LanguageKeywords.For(filePath))
        {
            if (StartsWith(item.Text, prefix))
                results.TryAdd(item.Text, item);
        }

        // 2. Mots déjà présents dans le fichier
        foreach (Match match in WordRegex.Matches(text))
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
            .Where(item => item.Text != prefix) // inutile de proposer exactement ce qui est déjà tapé
            .OrderBy(item => item.Text.StartsWith(prefix, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
            .Take(MaxItems)
            .ToList();
    }

    private static bool StartsWith(string candidate, string prefix) =>
        candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}