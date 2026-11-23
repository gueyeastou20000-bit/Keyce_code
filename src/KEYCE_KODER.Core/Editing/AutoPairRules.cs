using KEYCE_KODER.Core.Languages;

namespace KEYCE_KODER.Core.Editing;

/// <summary>Ce que l'éditeur doit faire quand l'utilisateur tape un caractère.</summary>
public enum PairActionKind
{
    None,           // comportement normal : on laisse taper le caractère
    InsertPair,     // insérer l'ouvrant ET le fermant, curseur au milieu : (|)
    SkipClosing,    // le fermant existe déjà juste après : on le saute
    WrapSelection   // entourer le texte sélectionné : (sélection)
}

public readonly record struct PairAction(PairActionKind Kind, char Open = '\0', char Close = '\0')
{
    public static PairAction None => new(PairActionKind.None);
}

/// <summary>
/// Règles de fermeture automatique des parenthèses, crochets, accolades et guillemets.
/// Travaille uniquement sur les caractères autour du curseur : indépendant de l'éditeur, testable.
/// </summary>
public static class AutoPairRules
{
    private static readonly Dictionary<char, char> Brackets = new()
    {
        ['('] = ')',
        ['['] = ']',
        ['{'] = '}'
    };

    // Après le curseur, on ne ferme que si le caractère suivant « laisse de la place »
    private const string FreeAfter = ")]};,:.>";

    /// <param name="previous">Caractère juste avant le curseur ('\0' s'il n'y en a pas).</param>
    /// <param name="next">Caractère juste après le curseur ('\0' s'il n'y en a pas).</param>
    public static PairAction OnTyped(
        char typed, char previous, char next, bool hasSelection, LanguageDefinition language)
    {
        bool isQuote = language.Quotes.Contains(typed);
        bool isOpening = Brackets.ContainsKey(typed);
        bool isClosing = Brackets.ContainsValue(typed);

        // 1. Du texte est sélectionné et on tape un ouvrant : on entoure la sélection
        if (hasSelection && (isOpening || isQuote))
            return new(PairActionKind.WrapSelection, typed, ClosingFor(typed));

        if (hasSelection)
            return PairAction.None;

        // 2. On tape un fermant (ou un guillemet) et c'est déjà le caractère suivant : on le saute
        if ((isClosing || isQuote) && next == typed)
            return new(PairActionKind.SkipClosing);

        // 3. On tape un ouvrant : on ajoute le fermant si la place est libre
        if ((isOpening || isQuote) && IsFree(next))
        {
            // Un guillemet collé à un mot est une apostrophe (it's, l'arbre) : pas de paire
            if (isQuote && (language.IsWordChar(previous) || previous == typed))
                return PairAction.None;

            return new(PairActionKind.InsertPair, typed, ClosingFor(typed));
        }

        return PairAction.None;
    }

    /// <summary>Retour arrière entre une paire vide, ex. (|) ou "|" : faut-il effacer les deux ?</summary>
    public static bool ShouldDeletePair(char previous, char next, LanguageDefinition language)
    {
        if (Brackets.TryGetValue(previous, out char closing))
            return closing == next;

        return language.Quotes.Contains(previous) && previous == next;
    }

    private static char ClosingFor(char opening) =>
        Brackets.TryGetValue(opening, out char closing) ? closing : opening; // un guillemet se ferme par lui-même

    private static bool IsFree(char next) =>
        next == '\0' || char.IsWhiteSpace(next) || FreeAfter.Contains(next);
}