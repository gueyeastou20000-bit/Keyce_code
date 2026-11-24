namespace KEYCE_KODER.Runner;

/// <summary>Un exécutable à essayer (ex. « py » avec l'argument « -3 »).</summary>
public sealed record ToolCandidate(string FileName, string[] PrefixArgs);

/// <summary>Décrit comment détecter un outil (interpréteur ou compilateur).</summary>
public sealed record ToolSpec(
    string Id,
    string DisplayName,
    string InstallHint,
    IReadOnlyList<ToolCandidate> Candidates,
    string[] VersionArgs,
    string VersionPattern);

public sealed record ResolvedTool(string FileName, IReadOnlyList<string> PrefixArgs, string Version)
{
    public string Display => PrefixArgs.Count == 0 ? FileName : $"{FileName} {string.Join(' ', PrefixArgs)}";
}

/// <summary>Une commande à lancer (compilation ou exécution).</summary>
public sealed record RunStep(string FileName, IReadOnlyList<string> Args, string Display, bool IsBuild = false);

public sealed record LanguageProfile(
    string Id,
    string DisplayName,
    IReadOnlyList<string> Extensions,
    ToolSpec Tool,
    Func<ResolvedTool, string, string, IReadOnlyList<RunStep>> BuildSteps);

/// <summary>Langages pris en charge. Pour en ajouter un : un ToolSpec + un LanguageProfile dans All.</summary>
public static class LanguageProfiles
{
    private static readonly ToolSpec PythonTool = new(
        "python", "Python 3", "Installez Python 3 depuis python.org (cochez « Add to PATH »).",
        [new ToolCandidate("python", []), new ToolCandidate("py", ["-3"])],
        ["--version"], @"^Python 3\.\d+");

    private static readonly ToolSpec NodeTool = new(
        "node", "Node.js", "Installez Node.js depuis nodejs.org.",
        [new ToolCandidate("node", [])],
        ["--version"], @"^v\d+\.");

    private static readonly ToolSpec JavaTool = new(
        "java", "Java (JDK)", "Installez un JDK 11 ou plus (par exemple Temurin, adoptium.net).",
        [new ToolCandidate("java", [])],
        ["-version"], @"version");

    private static readonly ToolSpec GccTool = new(
        "gcc", "GCC (MinGW)", "Installez MinGW-w64 ou MSYS2 et ajoutez son dossier « bin » au PATH.",
        [new ToolCandidate("gcc", [])],
        ["--version"], @"gcc");

    private static readonly ToolSpec GppTool = new(
        "g++", "G++ (MinGW)", "Installez MinGW-w64 ou MSYS2 et ajoutez son dossier « bin » au PATH.",
        [new ToolCandidate("g++", [])],
        ["--version"], @"g\+\+");

    private static readonly ToolSpec DotnetTool = new(
        "dotnet", ".NET SDK", "Installez le SDK .NET 10 depuis dotnet.microsoft.com (ou menu Outils → Langages et outils).",
        [new ToolCandidate("dotnet", [])],
        ["--version"], @"^\d+\.\d+\.\d+");

    public static IReadOnlyList<LanguageProfile> All { get; } =
    [
        new LanguageProfile("python", "Python", [".py"], PythonTool,
            (t, script, _) =>
                [new RunStep(t.FileName, [.. t.PrefixArgs, "-u", script], $"{t.Display} {Path.GetFileName(script)}")]),

        new LanguageProfile("javascript", "JavaScript", [".js", ".mjs", ".cjs"], NodeTool,
            (t, script, _) =>
                [new RunStep(t.FileName, [script], $"node {Path.GetFileName(script)}")]),

        new LanguageProfile("java", "Java", [".java"], JavaTool,
            (t, script, _) =>
                [new RunStep(t.FileName,
                    ["-Dfile.encoding=UTF-8", "-Dstdout.encoding=UTF-8", "-Dstderr.encoding=UTF-8", script],
                    $"java {Path.GetFileName(script)}")]),

        new LanguageProfile("c", "C", [".c"], GccTool,
            (t, script, buildDir) => Compiled(t, script, buildDir, "gcc")),

        new LanguageProfile("cpp", "C++", [".cpp", ".cc", ".cxx"], GppTool,
            (t, script, buildDir) => Compiled(t, script, buildDir, "g++")),

        new LanguageProfile("csharp", "C#", [".cs", ".csproj"], DotnetTool,
            (t, script, _) => CSharp(t, script)),
    ];

    public static IReadOnlyList<string> SupportedExtensions { get; } =
        All.SelectMany(p => p.Extensions).ToArray();

    public static LanguageProfile? ForFile(string path)
    {
        var ext = Path.GetExtension(path);
        return All.FirstOrDefault(p => p.Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// C# : si le fichier fait partie d'un projet (.csproj dans son dossier ou un dossier parent),
    /// on compile puis on lance ce projet. Sinon, fichier seul (.NET 10) : « dotnet run --file Programme.cs ».
    /// </summary>
    private static IReadOnlyList<RunStep> CSharp(ResolvedTool tool, string script)
    {
        var project = script.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            ? script
            : FindProject(Path.GetDirectoryName(script)!);

        if (project is null)
        {
            var file = Path.GetFileName(script);
            return
            [
                new RunStep(tool.FileName, ["build", script, "-nologo", "-v", "q"], $"dotnet build {file}", IsBuild: true),
                new RunStep(tool.FileName, ["run", "--file", script, "-v", "q"], $"dotnet run {file}"),
            ];
        }

        var name = Path.GetFileName(project);
        return
        [
            new RunStep(tool.FileName, ["build", project, "-nologo", "-v", "q"], $"dotnet build {name}", IsBuild: true),
            new RunStep(tool.FileName, ["run", "--project", project, "--no-build"], $"dotnet run --project {name}"),
        ];
    }

    /// <summary>Cherche un .csproj dans le dossier du fichier, puis jusqu'à 3 dossiers parents.</summary>
    private static string? FindProject(string folder)
    {
        var dir = new DirectoryInfo(folder);

        for (var level = 0; dir is not null && level < 4; level++, dir = dir.Parent)
        {
            try
            {
                var projects = dir.GetFiles("*.csproj");
                if (projects.Length > 0) return projects[0].FullName;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                return null; // dossier protégé : on considère le fichier comme seul
            }
        }

        return null;
    }

    private static IReadOnlyList<RunStep> Compiled(ResolvedTool tool, string script, string buildDir, string compiler)
    {
        var exe = Path.Combine(buildDir, Path.GetFileNameWithoutExtension(script) + ".exe");
        return
        [
            new RunStep(tool.FileName, [script, "-o", exe], $"{compiler} {Path.GetFileName(script)}", IsBuild: true),
            new RunStep(exe, [], Path.GetFileName(exe)),
        ];
    }
}
