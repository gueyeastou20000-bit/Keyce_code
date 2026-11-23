using System.IO;

namespace KEYCE_KODER.Core.Completion;

/// <summary>Mots-clés et fonctions courantes de chaque langage, pour l'autocomplétion.</summary>
public static class LanguageKeywords
{
    private static readonly Dictionary<string, IReadOnlyList<CompletionItem>> ByExtension =
        new(StringComparer.OrdinalIgnoreCase);

    static LanguageKeywords()
    {
        Register("Python", [".py"],
     "False None True and as assert async await break case class continue def del elif else " +
     "except finally for from global if import in is lambda match nonlocal not or pass raise " +
     "return try while with yield self",
     "print input len range int float str bool list dict set tuple type isinstance enumerate " +
     "zip map filter sorted reversed sum min max abs round open help dir super object " +
     "Exception ValueError TypeError KeyError IndexError __init__ __name__ __main__");

        Register("JavaScript", [".js", ".mjs", ".cjs"],
            "async await break case catch class const continue debugger default delete do else " +
            "export extends false finally for function if import in instanceof let new null of " +
            "return static super switch this throw true try typeof undefined var void while yield",
            "console log document window Math JSON Array Object String Number Boolean Promise Map " +
            "Set Date parseInt parseFloat setTimeout setInterval fetch alert require module exports");

        Register("Java", [".java"],
            "abstract assert boolean break byte case catch char class continue default do double " +
            "else enum extends final finally float for if implements import instanceof int " +
            "interface long new package private protected public record return short static super " +
            "switch synchronized this throw throws try var void volatile while true false null",
            "System out println print printf String Math Integer Double Boolean List ArrayList Map " +
            "HashMap Scanner Object Exception main args");

        Register("C", [".c", ".h"],
            "auto break case char const continue default do double else enum extern float for goto " +
            "if int long register return short signed sizeof static struct switch typedef union " +
            "unsigned void volatile while include define NULL",
            "printf scanf puts gets malloc calloc free strlen strcpy strcat strcmp memset memcpy " +
            "fopen fclose fprintf fscanf main stdio stdlib string");

        Register("C++", [".cpp", ".cc", ".cxx", ".hpp"],
            "auto bool break case catch char class const continue default delete do double else " +
            "enum false float for friend if include inline int long namespace new nullptr operator " +
            "private protected public return short static struct switch template this throw true " +
            "try typedef typename using virtual override void while",
            "std cout cin endl string vector map set pair iostream size push_back begin end " +
            "printf main");

        Register("C#", [".cs"],
            "abstract as async await base bool break byte case catch char class const continue " +
            "decimal default delegate do double else enum event false finally float for foreach " +
            "get if in init int interface internal is lock long namespace new null object out " +
            "override params private protected public readonly record ref return sealed set short " +
            "static string struct switch this throw true try typeof using var virtual void while",
            "Console WriteLine ReadLine Write Math List Dictionary Task String Int32 Exception " +
            "Main args ToString");
    }

    /// <summary>Suggestions du langage correspondant au fichier (liste vide si langage inconnu).</summary>
    public static IReadOnlyList<CompletionItem> For(string? filePath)
    {
        string extension = Path.GetExtension(filePath ?? string.Empty);

        return ByExtension.TryGetValue(extension, out var items)
            ? items
            : Array.Empty<CompletionItem>();
    }

    private static void Register(
        string languageName,
        string[] extensions,
        string keywords,
        string builtins)
    {
        var items = new List<CompletionItem>();

        foreach (string word in Split(keywords))
            items.Add(new CompletionItem(word, CompletionKind.Keyword, $"Mot-clé {languageName}"));

        foreach (string word in Split(builtins))
            items.Add(new CompletionItem(word, CompletionKind.Builtin, $"{languageName} : fonction / objet courant"));

        foreach (string extension in extensions)
            ByExtension[extension] = items;
    }

    private static string[] Split(string words) =>
        words.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}