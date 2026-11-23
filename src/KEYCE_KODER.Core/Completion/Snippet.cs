namespace KEYCE_KODER.Core.Completion;

/// <summary>
/// Une structure de code prête à l'emploi.
/// Dans Body : « \t » = un niveau d'indentation, « $0 » = position finale du curseur.
/// </summary>
public sealed record Snippet(string Prefix, string Description, string Body);

/// <summary>Transforme le modèle d'un snippet en texte réel à insérer.</summary>
public static class SnippetExpander
{
    public const string CaretMarker = "$0";

    /// <param name="indentation">Indentation de la ligne où l'on insère (ajoutée à chaque nouvelle ligne).</param>
    /// <param name="indentUnit">Un niveau d'indentation : 4 espaces, ou « \t ».</param>
    /// <param name="newLine">Fin de ligne du document : « \n » ou « \r\n ».</param>
    /// <returns>Le texte à insérer et la position du curseur dans ce texte.</returns>
    public static (string Text, int CaretOffset) Expand(
        string body, string indentation, string indentUnit, string newLine = "\n")
    {
        string text = body
            .Replace("\t", indentUnit)
            .Replace("\n", newLine + indentation);

        int caret = text.IndexOf(CaretMarker, StringComparison.Ordinal);

        if (caret < 0)
            return (text, text.Length); // pas de $0 : le curseur va à la fin

        return (text.Remove(caret, CaretMarker.Length), caret);
    }
}