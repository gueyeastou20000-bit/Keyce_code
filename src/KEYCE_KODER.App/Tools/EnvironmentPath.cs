using System.IO;
using KEYCE_KODER.Runner;

namespace KEYCE_KODER.App.Tools;

/// <summary>
/// Gestion du PATH de Windows : permet d'utiliser un outil juste après son installation,
/// sans redémarrer KEYCE KODER.
/// </summary>
public static class EnvironmentPath
{
    /// <summary>
    /// Relit le PATH de Windows (machine + utilisateur) et l'applique au programme en cours.
    /// Un programme ne voit normalement que le PATH qu'il avait au démarrage.
    /// </summary>
    public static void Refresh()
    {
        string machine = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine) ?? string.Empty;
        string user = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? string.Empty;

        Environment.SetEnvironmentVariable("Path", $"{machine};{user}");

        // Le Runner garde en mémoire les outils déjà trouvés : on vide ce cache
        ToolLocator.ResetCache();
    }

    /// <summary>Ajoute un dossier au PATH de l'utilisateur (de façon permanente), puis rafraîchit.</summary>
    public static void AddToUserPath(string folder)
    {
        string user = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? string.Empty;

        bool alreadyPresent = user
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Any(entry => string.Equals(entry.TrimEnd('\\'), folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));

        if (!alreadyPresent)
        {
            string newPath = string.IsNullOrWhiteSpace(user) ? folder : $"{user.TrimEnd(';')};{folder}";
            Environment.SetEnvironmentVariable("Path", newPath, EnvironmentVariableTarget.User);
        }

        Refresh();
    }

    /// <summary>
    /// Cherche le dossier qui contient l'exécutable de l'outil (ex. gcc.exe),
    /// quand l'installation ne l'a pas ajouté au PATH.
    /// </summary>
    public static string? FindExecutableFolder(ToolInstallInfo info)
    {
        foreach (string rawRoot in info.SearchRoots)
        {
            string root = Environment.ExpandEnvironmentVariables(rawRoot);
            if (!Directory.Exists(root))
                continue;

            // Dans le dossier des paquets winget, on ne regarde que le paquet de cet outil
            IEnumerable<string> folders = root.EndsWith(@"WinGet\Packages", StringComparison.OrdinalIgnoreCase)
                ? Directory.GetDirectories(root, info.WingetId + "*")
                : new[] { root };

            foreach (string folder in folders)
            {
                try
                {
                    string? exe = Directory
                        .EnumerateFiles(folder, info.ExeName, SearchOption.AllDirectories)
                        .FirstOrDefault();

                    if (exe is not null)
                        return Path.GetDirectoryName(exe);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    // Dossier protégé : on passe au suivant
                }
            }
        }

        return null;
    }
}
