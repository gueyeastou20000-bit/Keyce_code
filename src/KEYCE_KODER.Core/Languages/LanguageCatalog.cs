using System.IO;

namespace KEYCE_KODER.Core.Languages;

/// <summary>Catalogue de tous les langages connus de l'éditeur.</summary>
public static class LanguageCatalog
{
    private static readonly List<LanguageDefinition> Languages = new();

    private static readonly Dictionary<string, LanguageDefinition> ByExtension =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Langage par défaut : fichier sans extension ou extension inconnue.</summary>
    public static LanguageDefinition PlainText { get; } =
        new("text", "Texte brut", [], [], []);

    public static IReadOnlyList<LanguageDefinition> All => Languages;

    /// <summary>Le langage d'un fichier, d'après son extension.</summary>
    public static LanguageDefinition FromPath(string? filePath)
    {
        string extension = Path.GetExtension(filePath ?? string.Empty);

        return ByExtension.TryGetValue(extension, out var language)
            ? language
            : PlainText;
    }

    static LanguageCatalog()
    {
        // ---------- Langages de programmation ----------
        Add(new("python", "Python", [".py", ".pyw"],
            Words("False None True and as assert async await break case class continue def del elif else " +
                  "except finally for from global if import in is lambda match nonlocal not or pass raise " +
                  "return try while with yield self"),
            Words("print input len range int float str bool list dict set tuple type isinstance enumerate " +
                  "zip map filter sorted reversed sum min max abs round open help dir super object append " +
                  "extend split join strip replace format keys values items get Exception ValueError " +
                  "TypeError KeyError IndexError __init__ __name__ __main__"),
            LineComment: "#", IndentAfterColon: true));

        Add(new("javascript", "JavaScript", [".js", ".mjs", ".cjs", ".jsx"],
            Words("async await break case catch class const continue debugger default delete do else " +
                  "export extends false finally for function if import in instanceof let new null of " +
                  "return static super switch this throw true try typeof undefined var void while yield"),
            Words("console log error warn document window Math JSON Array Object String Number Boolean " +
                  "Promise Map Set Date parseInt parseFloat setTimeout setInterval fetch alert require " +
                  "module exports querySelector getElementById addEventListener"),
            LineComment: "//", Quotes: "\"'`"));

        Add(new("typescript", "TypeScript", [".ts", ".tsx"],
            Words("abstract any as async await boolean break case catch class const continue declare " +
                  "default delete do else enum export extends false finally for from function if " +
                  "implements import in instanceof interface keyof let namespace never new null number " +
                  "of private protected public readonly return static string super switch this throw " +
                  "true try type typeof undefined unknown var void while"),
            Words("console log document window Math JSON Array Object Promise Map Set Date Record " +
                  "Partial Readonly fetch"),
            LineComment: "//", Quotes: "\"'`"));

        Add(new("java", "Java", [".java"],
            Words("abstract assert boolean break byte case catch char class continue default do double " +
                  "else enum extends final finally float for if implements import instanceof int " +
                  "interface long new package private protected public record return short static super " +
                  "switch synchronized this throw throws try var void volatile while true false null"),
            Words("System out println print printf String Math Integer Double Boolean List ArrayList Map " +
                  "HashMap Scanner Object Exception main args length size add get"),
            LineComment: "//"));

        Add(new("c", "C", [".c", ".h"],
            Words("auto break case char const continue default do double else enum extern float for goto " +
                  "if int long register return short signed sizeof static struct switch typedef union " +
                  "unsigned void volatile while include define NULL"),
            Words("printf scanf puts gets malloc calloc free strlen strcpy strcat strcmp memset memcpy " +
                  "fopen fclose fprintf fscanf main stdio stdlib string"),
            LineComment: "//"));

        Add(new("cpp", "C++", [".cpp", ".cc", ".cxx", ".hpp", ".hh"],
            Words("auto bool break case catch char class const continue default delete do double else " +
                  "enum false float for friend if include inline int long namespace new nullptr operator " +
                  "private protected public return short static struct switch template this throw true " +
                  "try typedef typename using virtual override void while"),
            Words("std cout cin endl string vector map set pair iostream size push_back begin end " +
                  "printf main"),
            LineComment: "//"));

        Add(new("csharp", "C#", [".cs"],
            Words("abstract as async await base bool break byte case catch char class const continue " +
                  "decimal default delegate do double else enum event false finally float for foreach " +
                  "get if in init int interface internal is lock long namespace new null object out " +
                  "override params private protected public readonly record ref return sealed set short " +
                  "static string struct switch this throw true try typeof using var virtual void while"),
            Words("Console WriteLine ReadLine Write Math List Dictionary Task String Int32 Exception " +
                  "Main args ToString"),
            LineComment: "//"));

        Add(new("go", "Go", [".go"],
            Words("break case chan const continue default defer else fallthrough for func go goto if " +
                  "import interface map package range return select struct switch type var true false " +
                  "nil iota"),
            Words("fmt Println Printf Sprintf Errorf append cap close copy delete len make new panic " +
                  "recover error string int int64 float64 bool byte rune main os strings strconv"),
            LineComment: "//", Quotes: "\"'`", UseTabs: true));

        Add(new("rust", "Rust", [".rs"],
            Words("as async await break const continue crate dyn else enum extern false fn for if impl " +
                  "in let loop match mod move mut pub ref return self Self static struct super trait true " +
                  "type unsafe use where while"),
            Words("println print format vec Vec String Option Some None Result Ok Err Box HashMap main " +
                  "i32 i64 u32 u64 f64 bool char str usize len push iter collect unwrap clone"),
            LineComment: "//", Quotes: "\""));   // en Rust, ' sert aussi aux durées de vie

        Add(new("kotlin", "Kotlin", [".kt", ".kts"],
            Words("as break class continue do else false for fun if in interface is null object package " +
                  "return super this throw true try typealias val var when while data sealed enum " +
                  "override open private protected public internal companion lateinit suspend import"),
            Words("println print listOf mutableListOf mapOf setOf arrayOf String Int Double Boolean List " +
                  "Map main let apply also run"),
            LineComment: "//"));

        Add(new("swift", "Swift", [".swift"],
            Words("as break case class continue default defer do else enum extension false for func guard " +
                  "if import in init inout internal is let nil private protocol public repeat return self " +
                  "static struct switch throw throws true try var where while"),
            Words("print String Int Double Bool Array Dictionary Set Optional count append map filter " +
                  "reduce"),
            LineComment: "//"));

        Add(new("dart", "Dart", [".dart"],
            Words("abstract as async await break case catch class const continue default do else enum " +
                  "extends false final finally for if implements import in is late new null required " +
                  "return static super switch this throw true try var void while with"),
            Words("print main String int double bool List Map Set Future Stream Widget setState build " +
                  "BuildContext"),
            LineComment: "//"));

        Add(new("php", "PHP", [".php"],
            Words("abstract and array as break case catch class const continue declare default do echo " +
                  "else elseif empty enum extends false final finally fn for foreach function global if " +
                  "implements include interface isset list match namespace new null or print private " +
                  "protected public readonly require return static switch this throw trait true try " +
                  "unset use var while"),
            Words("strlen str_replace explode implode count array_push array_merge in_array json_encode " +
                  "json_decode var_dump print_r htmlspecialchars mysqli PDO header session_start " +
                  "$_GET $_POST $_SESSION $_SERVER $this"),
            LineComment: "//", WordExtraChars: "$"));

        Add(new("ruby", "Ruby", [".rb"],
            Words("alias and begin break case class def defined do else elsif end ensure false for if in " +
                  "module next nil not or redo rescue retry return self super then true undef unless " +
                  "until when while yield require attr_accessor"),
            Words("puts print gets each map select reject times length size push to_s to_i include " +
                  "initialize new"),
            LineComment: "#"));

        Add(new("lua", "Lua", [".lua"],
            Words("and break do else elseif end false for function goto if in local nil not or repeat " +
                  "return then true until while"),
            Words("print pairs ipairs table string math tostring tonumber type require insert remove format"),
            LineComment: "--"));

        Add(new("r", "R", [".r"],
            Words("if else repeat while function for in next break TRUE FALSE NULL Inf NaN NA return library"),
            Words("print cat c vector list matrix data.frame length mean median sum paste paste0 seq rep " +
                  "apply sapply lapply read.csv summary plot"),
            LineComment: "#", WordExtraChars: "."));

        Add(new("sql", "SQL", [".sql"],
            Words("SELECT FROM WHERE INSERT INTO VALUES UPDATE SET DELETE CREATE TABLE DROP ALTER ADD " +
                  "PRIMARY KEY FOREIGN REFERENCES JOIN INNER LEFT RIGHT ON AS AND OR NOT NULL IS IN " +
                  "LIKE BETWEEN ORDER BY GROUP HAVING LIMIT DISTINCT UNION INDEX VIEW DATABASE"),
            Words("COUNT SUM AVG MIN MAX NOW CONCAT UPPER LOWER COALESCE INT VARCHAR TEXT DATE DATETIME " +
                  "BOOLEAN AUTO_INCREMENT"),
            LineComment: "--"));

        // ---------- Langages web et de balisage ----------
        Add(new("html", "HTML", [".html", ".htm"],
            Words("html head body title meta link script style div span section header footer nav main " +
                  "article aside form input button label select option textarea table thead tbody tr th " +
                  "td img a ul ol li h1 h2 h3 h4 p br hr strong em"),
            Words("class id href src alt type name value placeholder charset content rel width height " +
                  "onclick action method required disabled data-id aria-label"),
            WordExtraChars: "-"));

        Add(new("css", "CSS", [".css", ".scss", ".less"],
            Words("color background background-color margin padding border border-radius width height " +
                  "display flex grid position top left right bottom font-size font-family font-weight " +
                  "text-align line-height justify-content align-items gap overflow opacity z-index " +
                  "transition transform box-shadow cursor"),
            Words("none block inline inline-block absolute relative fixed sticky center auto bold solid " +
                  "hidden pointer important hover active focus root var calc rgb rgba px rem em vh vw"),
            WordExtraChars: "-"));

        Add(new("json", "JSON", [".json"],
            Words("true false null"), [],
            Quotes: "\""));

        Add(new("xml", "XML", [".xml", ".xaml", ".csproj", ".svg", ".config"],
            Words("xml version encoding"), [],
            WordExtraChars: "-"));

        Add(new("yaml", "YAML", [".yml", ".yaml"],
            Words("true false null yes no"),
            Words("name version services image ports volumes environment build depends_on steps run uses " +
                  "with on jobs"),
            LineComment: "#", IndentAfterColon: true));

        Add(new("markdown", "Markdown", [".md", ".markdown"], [], [],
            Quotes: "`"));

        // ---------- Scripts système ----------
        Add(new("shell", "Bash", [".sh", ".bash"],
            Words("if then else elif fi for while until do done case esac in function return exit local " +
                  "export readonly break continue"),
            Words("echo printf cd ls pwd mkdir rm cp mv cat grep sed awk chmod sudo apt git source read test"),
            LineComment: "#", WordExtraChars: "$"));

        Add(new("powershell", "PowerShell", [".ps1", ".psm1"],
            Words("begin break catch class continue do else elseif end exit filter finally for foreach " +
                  "function if in param process return switch throw trap try until while"),
            Words("Write-Host Write-Output Get-ChildItem Get-Content Set-Content Get-Item New-Item " +
                  "Remove-Item Copy-Item Move-Item Get-Process Start-Process Test-Path Join-Path " +
                  "Import-Module $true $false $null $PSScriptRoot"),
            LineComment: "#", WordExtraChars: "$-"));

        Add(new("batch", "Batch", [".bat", ".cmd"],
            Words("echo off set if else goto call exit for in do not exist errorlevel defined pause rem " +
                  "cls start setlocal endlocal"),
            [],
            LineComment: "REM", WordExtraChars: "%"));
    }

    private static void Add(LanguageDefinition language)
    {
        Languages.Add(language);

        foreach (string extension in language.Extensions)
            ByExtension[extension] = language;
    }

    private static string[] Words(string words) =>
        words.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}