using System.Text.RegularExpressions;

namespace KEYCE_KODER.Core.Languages;

/// <summary>
/// Fiche d'identité d'un langage : tout ce que l'éditeur doit savoir pour
/// l'autocomplétion, les guillemets automatiques, l'indentation et les commentaires.
/// </summary>
public sealed record LanguageDefinition(
    string Id,
    string DisplayName,
    IReadOnlyList<string> Extensions,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> Builtins,
    string? LineComment = null,
    string Quotes = "\"'",
    string WordExtraChars = "",
    bool IndentAfterColon = false,
    bool UseTabs = false)
{
    private Regex? _wordRegex;

    /// <summary>Ce caractère peut-il faire partie d'un mot dans ce langage ?</summary>
    public bool IsWordChar(char c) =>
        char.IsLetterOrDigit(c) || c == '_' || WordExtraChars.Contains(c);

    /// <summary>Expression régulière qui trouve les mots (identifiants) de ce langage.</summary>
    public Regex WordRegex => _wordRegex ??= BuildWordRegex();

    private Regex BuildWordRegex()
    {
        // Dans une classe de caractères [...], le « - » doit être échappé
        string extra = string.Concat(
            WordExtraChars.Select(c => Regex.Escape(c.ToString()).Replace("-", @"\-")));

        return new Regex($@"[\p{{L}}_{extra}][\p{{L}}\p{{N}}_{extra}]*", RegexOptions.Compiled);
    }
}