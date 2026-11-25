using System.Text;
using System.Text.Json;
using KEYCE_KODER.AI.Tools.Contracts;
using KEYCE_KODER.AI.Tools.Execution;

namespace KEYCE_KODER.AI.Tools.FileSystem;

public sealed class ListFilesTool : IAiTool
{
    private const int DefaultMaxItems = 100;
    private const int MaxItemsLimit = 200;
    private const int DefaultMaxDepth = 2;
    private const int MaxDepthLimit = 6;
    private const int MaxOutputCharacters = 12000;

    private static readonly HashSet<string> IgnoredDirectories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".vs", "bin", "obj",
            "node_modules", "venv", ".venv",
            "__pycache__", "dist", "build"
        };

    public AiToolDefinition Definition { get; } = new(
        "list_files",
        "Liste les fichiers et dossiers du projet. Permet le filtrage, la recherche récursive et le contrôle de profondeur.",
        false,
        new
        {
            type = "object",
            properties = new
            {
                path = new
                {
                    type = "string",
                    description = "Dossier relatif à explorer. Utiliser '.' ou omettre pour la racine du projet."
                },
                recursive = new
                {
                    type = "boolean",
                    description = "Active la recherche dans les sous-dossiers. Défaut : false."
                },
                max_depth = new
                {
                    type = "integer",
                    description = "Profondeur maximale, entre 1 et 6. Défaut : 2."
                },
                max_items = new
                {
                    type = "integer",
                    description = "Nombre maximal d'éléments, entre 1 et 200. Défaut : 100."
                },
                filter = new
                {
                    type = "string",
                    description = "Filtre facultatif pour les fichiers, par exemple *.py ou *.cs."
                }
            }
        });

    public Task<AiToolResult> ExecuteAsync(
        JsonElement arguments,
        AiToolContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return Task.FromResult(new AiToolResult(
                false,
                "Les arguments doivent être un objet JSON.",
                "invalid_arguments"));
        }

        string relativePath = ".";

        if (arguments.TryGetProperty("path", out JsonElement pathElement))
        {
            if (pathElement.ValueKind != JsonValueKind.String)
            {
                return Task.FromResult(new AiToolResult(
                    false,
                    "path doit être une chaîne de caractères.",
                    "invalid_path"));
            }

            relativePath = pathElement.GetString()!;
        }

        bool recursive = false;

        if (arguments.TryGetProperty("recursive", out JsonElement recursiveElement))
        {
            if (recursiveElement.ValueKind != JsonValueKind.True &&
                recursiveElement.ValueKind != JsonValueKind.False)
            {
                return Task.FromResult(new AiToolResult(
                    false,
                    "recursive doit être un booléen.",
                    "invalid_recursive"));
            }

            recursive = recursiveElement.GetBoolean();
        }

        int maxDepth = DefaultMaxDepth;

        if (arguments.TryGetProperty("max_depth", out JsonElement depthElement))
        {
            if (depthElement.ValueKind != JsonValueKind.Number ||
                !depthElement.TryGetInt32(out maxDepth) ||
                maxDepth < 1 ||
                maxDepth > MaxDepthLimit)
            {
                return Task.FromResult(new AiToolResult(
                    false,
                    $"max_depth doit être compris entre 1 et {MaxDepthLimit}.",
                    "invalid_max_depth"));
            }
        }

        int maxItems = DefaultMaxItems;

        if (arguments.TryGetProperty("max_items", out JsonElement itemsElement))
        {
            if (itemsElement.ValueKind != JsonValueKind.Number ||
                !itemsElement.TryGetInt32(out maxItems) ||
                maxItems < 1 ||
                maxItems > MaxItemsLimit)
            {
                return Task.FromResult(new AiToolResult(
                    false,
                    $"max_items doit être compris entre 1 et {MaxItemsLimit}.",
                    "invalid_max_items"));
            }
        }

        string? filter = null;

        if (arguments.TryGetProperty("filter", out JsonElement filterElement))
        {
            if (filterElement.ValueKind != JsonValueKind.String)
            {
                return Task.FromResult(new AiToolResult(
                    false,
                    "filter doit être une chaîne de caractères.",
                    "invalid_filter"));
            }

            filter = filterElement.GetString();

            if (!string.IsNullOrWhiteSpace(filter) &&
                (filter.Contains('/') || filter.Contains('\\')))
            {
                return Task.FromResult(new AiToolResult(
                    false,
                    "Le filtre doit contenir uniquement un nom ou un motif de fichier.",
                    "invalid_filter"));
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        string fullPath;

        if (string.IsNullOrWhiteSpace(relativePath) || relativePath == ".")
        {
            fullPath = context.ProjectRoot;
        }
        else
        {
            fullPath = context.ResolveProjectPath(relativePath);
        }

        if (!Directory.Exists(fullPath))
        {
            return Task.FromResult(new AiToolResult(
                false,
                "Le dossier demandé n'existe pas.",
                "directory_not_found"));
        }

        var output = new StringBuilder();

        output.AppendLine($"Projet : {context.ProjectRoot}");
        output.AppendLine($"Dossier exploré : {relativePath}");
        output.AppendLine();

        var pending = new Queue<(string Path, int Depth)>();
        pending.Enqueue((fullPath, 0));

        int itemCount = 0;
        bool hasMore = false;
        int inaccessibleDirectories = 0;

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var current = pending.Dequeue();

            if (current.Depth >= maxDepth && recursive)
            {
                // La profondeur maximale est atteinte.
            }

            string[] entries;

            try
            {
                entries = Directory.GetFileSystemEntries(current.Path);
                Array.Sort(entries, StringComparer.OrdinalIgnoreCase);
            }
            catch (UnauthorizedAccessException)
            {
                inaccessibleDirectories++;
                continue;
            }
            catch (IOException)
            {
                inaccessibleDirectories++;
                continue;
            }

            foreach (string entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                FileAttributes attributes;

                try
                {
                    attributes = File.GetAttributes(entry);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    continue;

                bool isDirectory =
                    (attributes & FileAttributes.Directory) != 0;

                string name = Path.GetFileName(entry);

                if (isDirectory && IgnoredDirectories.Contains(name))
                    continue;

                if (!isDirectory &&
                    !string.IsNullOrWhiteSpace(filter) &&
                    !System.IO.Enumeration.FileSystemName
                        .MatchesSimpleExpression(filter, name, true))
                {
                    continue;
                }

                if (itemCount >= maxItems ||
                    output.Length >= MaxOutputCharacters)
                {
                    hasMore = true;
                    break;
                }

                string displayPath = Path.GetRelativePath(
                    context.ProjectRoot,
                    entry);

                if (isDirectory)
                {
                    output.AppendLine($"[DOSSIER] {displayPath}");
                }
                else
                {
                    long size = 0;

                    try
                    {
                        size = new FileInfo(entry).Length;
                    }
                    catch (IOException)
                    {
                        // La taille n'est pas disponible.
                    }

                    output.AppendLine(
                        $"[FICHIER] {displayPath} ({size} octets)");
                }

                itemCount++;

                if (recursive &&
                    isDirectory &&
                    current.Depth + 1 < maxDepth)
                {
                    pending.Enqueue((entry, current.Depth + 1));
                }
            }

            if (hasMore)
                break;
        }

        if (itemCount == 0)
            output.AppendLine("Aucun élément trouvé.");

        output.AppendLine();
        output.AppendLine($"Nombre d'éléments : {itemCount}");

        if (hasMore)
            output.AppendLine("Résultat limité : utilisez un filtre ou un autre dossier pour continuer.");

        if (inaccessibleDirectories > 0)
            output.AppendLine($"Dossiers inaccessibles : {inaccessibleDirectories}");

        return Task.FromResult(new AiToolResult(
            true,
            output.ToString()));
    }
}
