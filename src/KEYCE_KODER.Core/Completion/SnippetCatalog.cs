namespace KEYCE_KODER.Core.Completion;

/// <summary>Les structures de code (snippets) de chaque langage, indexées par Id de langage.</summary>
public static class SnippetCatalog
{
    private static readonly Dictionary<string, IReadOnlyList<Snippet>> ByLanguage =
        new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<Snippet> For(string languageId) =>
        ByLanguage.TryGetValue(languageId, out var snippets) ? snippets : Array.Empty<Snippet>();

    private static void Add(string languageId, params Snippet[] snippets) =>
        ByLanguage[languageId] = snippets;

    private static Snippet S(string prefix, string description, string body) =>
        new(prefix, description, body);

    static SnippetCatalog()
    {
        // Les mêmes structures pour JavaScript et TypeScript
        Snippet[] js =
        [
            S("if", "Condition if", "if ($0) {\n\t\n}"),
            S("ifelse", "Condition if / else", "if ($0) {\n\t\n} else {\n\t\n}"),
            S("for", "Boucle for", "for (let i = 0; i < $0; i++) {\n\t\n}"),
            S("forof", "Boucle for...of", "for (const item of $0) {\n\t\n}"),
            S("while", "Boucle while", "while ($0) {\n\t\n}"),
            S("function", "Fonction", "function $0() {\n\t\n}"),
            S("arrow", "Fonction fléchée", "const $0 = () => {\n\t\n};"),
            S("class", "Classe", "class $0 {\n\tconstructor() {\n\t\t\n\t}\n}"),
            S("try", "try / catch", "try {\n\t$0\n} catch (error) {\n\tconsole.error(error);\n}"),
            S("log", "console.log", "console.log($0);")
        ];

        Add("python",
            S("if", "Condition if", "if $0:\n\tpass"),
            S("ifelse", "Condition if / else", "if $0:\n\tpass\nelse:\n\tpass"),
            S("for", "Boucle for", "for item in $0:\n\tpass"),
            S("forr", "Boucle for avec range", "for i in range($0):\n\tpass"),
            S("while", "Boucle while", "while $0:\n\tpass"),
            S("def", "Fonction", "def $0():\n\tpass"),
            S("class", "Classe avec constructeur", "class $0:\n\tdef __init__(self):\n\t\tpass"),
            S("main", "Programme principal", "def main():\n\t$0\n\n\nif __name__ == \"__main__\":\n\tmain()"),
            S("try", "try / except", "try:\n\t$0\nexcept Exception as e:\n\tprint(e)"),
            S("with", "Ouvrir un fichier", "with open(\"$0\", \"r\", encoding=\"utf-8\") as f:\n\tcontenu = f.read()"));

        Add("javascript", js);
        Add("typescript", [.. js, S("interface", "Interface", "interface $0 {\n\t\n}")]);

        Add("java",
            S("main", "Méthode main", "public static void main(String[] args) {\n\t$0\n}"),
            S("class", "Classe avec main", "public class $0 {\n\n\tpublic static void main(String[] args) {\n\t\t\n\t}\n}"),
            S("sout", "System.out.println", "System.out.println($0);"),
            S("for", "Boucle for", "for (int i = 0; i < $0; i++) {\n\t\n}"),
            S("foreach", "Boucle for-each", "for (var item : $0) {\n\t\n}"),
            S("if", "Condition if", "if ($0) {\n\t\n}"),
            S("ifelse", "Condition if / else", "if ($0) {\n\t\n} else {\n\t\n}"),
            S("while", "Boucle while", "while ($0) {\n\t\n}"),
            S("try", "try / catch", "try {\n\t$0\n} catch (Exception e) {\n\te.printStackTrace();\n}"));

        Add("c",
            S("main", "Programme principal", "#include <stdio.h>\n\nint main(void) {\n\t$0\n\treturn 0;\n}"),
            S("for", "Boucle for", "for (int i = 0; i < $0; i++) {\n\t\n}"),
            S("if", "Condition if", "if ($0) {\n\t\n}"),
            S("ifelse", "Condition if / else", "if ($0) {\n\t\n} else {\n\t\n}"),
            S("while", "Boucle while", "while ($0) {\n\t\n}"),
            S("printf", "Afficher du texte", "printf(\"$0\\n\");"),
            S("struct", "Structure", "struct $0 {\n\t\n};"));

        Add("cpp",
            S("main", "Programme principal", "#include <iostream>\n\nint main() {\n\tstd::cout << \"$0\" << std::endl;\n\treturn 0;\n}"),
            S("for", "Boucle for", "for (int i = 0; i < $0; i++) {\n\t\n}"),
            S("forr", "Boucle sur une collection", "for (const auto& item : $0) {\n\t\n}"),
            S("if", "Condition if", "if ($0) {\n\t\n}"),
            S("while", "Boucle while", "while ($0) {\n\t\n}"),
            S("class", "Classe", "class $0 {\npublic:\n\t\nprivate:\n\t\n};"),
            S("cout", "Afficher du texte", "std::cout << $0 << std::endl;"));

        Add("csharp",
            S("main", "Méthode Main", "static void Main(string[] args)\n{\n\t$0\n}"),
            S("class", "Classe", "public class $0\n{\n\t\n}"),
            S("cw", "Console.WriteLine", "Console.WriteLine($0);"),
            S("for", "Boucle for", "for (int i = 0; i < $0; i++)\n{\n\t\n}"),
            S("foreach", "Boucle foreach", "foreach (var item in $0)\n{\n\t\n}"),
            S("if", "Condition if", "if ($0)\n{\n\t\n}"),
            S("try", "try / catch", "try\n{\n\t$0\n}\ncatch (Exception ex)\n{\n\tConsole.WriteLine(ex.Message);\n}"),
            S("prop", "Propriété", "public string $0 { get; set; }"));

        Add("go",
            S("main", "Programme principal", "package main\n\nimport \"fmt\"\n\nfunc main() {\n\tfmt.Println($0)\n}"),
            S("func", "Fonction", "func $0() {\n\t\n}"),
            S("for", "Boucle for", "for i := 0; i < $0; i++ {\n\t\n}"),
            S("forr", "Boucle range", "for _, item := range $0 {\n\t\n}"),
            S("if", "Condition if", "if $0 {\n\t\n}"),
            S("iferr", "Gestion d'erreur", "if err != nil {\n\treturn $0err\n}"));

        Add("rust",
            S("main", "Programme principal", "fn main() {\n\tprintln!(\"$0\");\n}"),
            S("fn", "Fonction", "fn $0() {\n\t\n}"),
            S("for", "Boucle for", "for item in $0 {\n\t\n}"),
            S("if", "Condition if", "if $0 {\n\t\n}"),
            S("struct", "Structure", "struct $0 {\n\t\n}"),
            S("match", "match", "match $0 {\n\t_ => {}\n}"));

        Add("kotlin",
            S("main", "Programme principal", "fun main() {\n\tprintln($0)\n}"),
            S("fun", "Fonction", "fun $0() {\n\t\n}"),
            S("for", "Boucle for", "for (item in $0) {\n\t\n}"),
            S("if", "Condition if", "if ($0) {\n\t\n}"),
            S("class", "Classe", "class $0 {\n\t\n}"));

        Add("swift",
            S("func", "Fonction", "func $0() {\n\t\n}"),
            S("for", "Boucle for", "for item in $0 {\n\t\n}"),
            S("if", "Condition if", "if $0 {\n\t\n}"),
            S("class", "Classe", "class $0 {\n\t\n}"),
            S("struct", "Structure", "struct $0 {\n\t\n}"));

        Add("dart",
            S("main", "Programme principal", "void main() {\n\tprint($0);\n}"),
            S("class", "Classe", "class $0 {\n\t\n}"),
            S("for", "Boucle for", "for (var i = 0; i < $0; i++) {\n\t\n}"),
            S("if", "Condition if", "if ($0) {\n\t\n}"));

        Add("php",
            S("php", "Balise PHP", "<?php\n\n$0"),
            S("function", "Fonction", "function $0() {\n\t\n}"),
            S("foreach", "Boucle foreach", "foreach ($0 as $item) {\n\t\n}"),
            S("if", "Condition if", "if ($0) {\n\t\n}"),
            S("class", "Classe avec constructeur", "class $0 {\n\tpublic function __construct() {\n\t\t\n\t}\n}"),
            S("echo", "Afficher", "echo $0;"));

        Add("ruby",
            S("def", "Méthode", "def $0\n\t\nend"),
            S("class", "Classe", "class $0\n\tdef initialize\n\t\t\n\tend\nend"),
            S("each", "Boucle each", "$0.each do |item|\n\t\nend"),
            S("if", "Condition if", "if $0\n\t\nend"));

        Add("lua",
            S("function", "Fonction", "function $0()\n\t\nend"),
            S("for", "Boucle for", "for i = 1, $0 do\n\t\nend"),
            S("if", "Condition if", "if $0 then\n\t\nend"));

        Add("r",
            S("function", "Fonction", "$0 <- function() {\n\t\n}"),
            S("for", "Boucle for", "for (i in $0) {\n\t\n}"),
            S("if", "Condition if", "if ($0) {\n\t\n}"));

        Add("sql",
            S("select", "Requête SELECT", "SELECT $0\nFROM table_name\nWHERE condition;"),
            S("insert", "Requête INSERT", "INSERT INTO $0 (colonne1, colonne2)\nVALUES (valeur1, valeur2);"),
            S("update", "Requête UPDATE", "UPDATE $0\nSET colonne = valeur\nWHERE condition;"),
            S("delete", "Requête DELETE", "DELETE FROM $0\nWHERE condition;"),
            S("create", "Créer une table", "CREATE TABLE $0 (\n\tid INT PRIMARY KEY AUTO_INCREMENT,\n\tnom VARCHAR(100) NOT NULL\n);"));

        Add("html",
            S("html", "Page HTML complète", "<!DOCTYPE html>\n<html lang=\"fr\">\n<head>\n\t<meta charset=\"UTF-8\">\n\t<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\n\t<title>$0</title>\n\t<link rel=\"stylesheet\" href=\"style.css\">\n</head>\n<body>\n\t\n\t<script src=\"script.js\"></script>\n</body>\n</html>"),
            S("div", "Bloc div", "<div class=\"$0\">\n\t\n</div>"),
            S("link", "Lien vers une feuille CSS", "<link rel=\"stylesheet\" href=\"$0\">"),
            S("script", "Script JavaScript", "<script src=\"$0\"></script>"),
            S("img", "Image", "<img src=\"$0\" alt=\"\">"),
            S("ul", "Liste à puces", "<ul>\n\t<li>$0</li>\n</ul>"),
            S("form", "Formulaire", "<form action=\"$0\" method=\"post\">\n\t\n\t<button type=\"submit\">Envoyer</button>\n</form>"));

        Add("css",
            S("flex", "Centrer avec flexbox", "display: flex;\njustify-content: center;\nalign-items: center;$0"),
            S("grid", "Grille", "display: grid;\ngrid-template-columns: repeat($0, 1fr);\ngap: 1rem;"),
            S("media", "Media query (mobile)", "@media (max-width: 768px) {\n\t$0\n}"));

        Add("markdown",
            S("code", "Bloc de code", "```$0\n\n```"),
            S("table", "Tableau", "| Colonne 1 | Colonne 2 |\n| --- | --- |\n| $0 |  |"),
            S("link", "Lien", "[$0](url)"));

        Add("shell",
            S("bash", "En-tête de script", "#!/bin/bash\n\n$0"),
            S("if", "Condition if", "if [ $0 ]; then\n\t\nfi"),
            S("for", "Boucle for", "for item in $0; do\n\t\ndone"),
            S("function", "Fonction", "$0() {\n\t\n}"));

        Add("powershell",
            S("function", "Fonction", "function $0 {\n\tparam()\n\t\n}"),
            S("if", "Condition if", "if ($0) {\n\t\n}"),
            S("foreach", "Boucle foreach", "foreach ($item in $0) {\n\t\n}"),
            S("try", "try / catch", "try {\n\t$0\n} catch {\n\tWrite-Host $_.Exception.Message\n}"));

        Add("xml",
            S("xml", "Déclaration XML", "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n$0"));
    }
}