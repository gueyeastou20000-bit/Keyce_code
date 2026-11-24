namespace KEYCE_KODER.App.Tools;

/// <summary>
/// Comment installer un outil du Runner avec winget.
/// ToolId    : identifiant de l'outil dans LanguageProfiles (Runner) : "python", "node", "java", "gcc", "g++", "dotnet".
/// WingetId  : identifiant du paquet winget.
/// ExeName   : exécutable à chercher si l'installation ne l'ajoute pas au PATH.
/// SearchRoots : dossiers où chercher cet exécutable (variables %...% autorisées).
/// </summary>
public sealed record ToolInstallInfo(
    string ToolId,
    string WingetId,
    string ExeName,
    IReadOnlyList<string> SearchRoots);

/// <summary>Catalogue d'installation des outils nécessaires à Run.</summary>
public static class ToolInstallCatalog
{
    private const string WingetPackages = @"%LOCALAPPDATA%\Microsoft\WinGet\Packages";

    public static IReadOnlyList<ToolInstallInfo> All { get; } =
    [
        new("python", "Python.Python.3.13", "python.exe",
            [@"%LOCALAPPDATA%\Programs\Python", @"%ProgramFiles%\Python313"]),

        new("node", "OpenJS.NodeJS.LTS", "node.exe",
            [@"%ProgramFiles%\nodejs"]),

        new("java", "EclipseAdoptium.Temurin.21.JDK", "java.exe",
            [@"%ProgramFiles%\Eclipse Adoptium"]),

        new("gcc", "BrechtSanders.WinLibs.POSIX.UCRT", "gcc.exe",
            [WingetPackages, @"C:\msys64\ucrt64\bin"]),

        new("g++", "BrechtSanders.WinLibs.POSIX.UCRT", "g++.exe",
            [WingetPackages, @"C:\msys64\ucrt64\bin"]),

        new("dotnet", "Microsoft.DotNet.SDK.10", "dotnet.exe",
            [@"%ProgramFiles%\dotnet"]),
    ];

    /// <summary>Informations d'installation d'un outil, ou null s'il n'est pas dans le catalogue.</summary>
    public static ToolInstallInfo? For(string toolId) =>
        All.FirstOrDefault(info => string.Equals(info.ToolId, toolId, StringComparison.OrdinalIgnoreCase));
}
