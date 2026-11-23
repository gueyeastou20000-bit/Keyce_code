namespace KEYCE_KODER.Core.Completion;

/// <summary>Type de suggestion (affiché dans la liste).</summary>
public enum CompletionKind
{
    Keyword,   // mot-clé du langage : def, while, class...
    Builtin,   // fonction ou objet courant : print, len, console...
    Word,      // mot déjà présent dans le fichier : tes variables, tes fonctions
    Snippet    // structure de code complète : for, if, class, main...
}

/// <summary>Une suggestion d'autocomplétion. SnippetBody n'est rempli que pour un snippet.</summary>
public sealed record CompletionItem(string Text, CompletionKind Kind, string Description, string? SnippetBody = null);