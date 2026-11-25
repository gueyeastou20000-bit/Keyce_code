
namespace KEYCE_KODER.AI.Tools.Execution;

public sealed class AiToolContext
{
    public string ProjectRoot { get; }

    public AiToolContext(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            throw new ArgumentException(
                "Le dossier du projet est obligatoire.",
                nameof(projectRoot));
        }

        ProjectRoot = Path.GetFullPath(projectRoot);

        if (!Directory.Exists(ProjectRoot))
        {
            throw new DirectoryNotFoundException(
                "Le dossier du projet n'existe pas.");
        }

        if (IsReparsePoint(ProjectRoot))
        {
            throw new UnauthorizedAccessException(
                "Le dossier racine du projet ne peut pas être un lien symbolique.");
        }
    }

    public string ResolveProjectPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException(
                "Le chemin du fichier est obligatoire.",
                nameof(relativePath));
        }

        if (Path.IsPathRooted(relativePath))
        {
            throw new UnauthorizedAccessException(
                "Les chemins absolus ne sont pas autorisés.");
        }

        string fullPath = Path.GetFullPath(
            Path.Combine(ProjectRoot, relativePath));

        string relativeToRoot = Path.GetRelativePath(
            ProjectRoot,
            fullPath);

        if (Path.IsPathRooted(relativeToRoot) ||
            relativeToRoot == "." ||
            relativeToRoot == ".." ||
            relativeToRoot.StartsWith(
                ".." + Path.DirectorySeparatorChar,
                StringComparison.Ordinal) ||
            relativeToRoot.StartsWith(
                ".." + Path.AltDirectorySeparatorChar,
                StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "L'accès en dehors du dossier du projet est interdit.");
        }

        CheckForReparsePoints(relativeToRoot);

        return fullPath;
    }

    private void CheckForReparsePoints(string relativePath)
    {
        string currentPath = ProjectRoot;

        string[] segments = relativePath.Split(
            new[]
            {
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            },
            StringSplitOptions.RemoveEmptyEntries);

        foreach (string segment in segments)
        {
            currentPath = Path.Combine(currentPath, segment);

            if (!File.Exists(currentPath) &&
                !Directory.Exists(currentPath))
            {
                break;
            }

            if (IsReparsePoint(currentPath))
            {
                throw new UnauthorizedAccessException(
                    "Les liens symboliques et les jonctions ne sont pas autorisés dans le chemin.");
            }
        }
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            FileAttributes attributes = File.GetAttributes(path);

            return (attributes & FileAttributes.ReparsePoint) != 0;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }
}
