
using System.Text;
using System.Text.Json;
using KEYCE_KODER.AI.Tools.Contracts;
using KEYCE_KODER.AI.Tools.Execution;

namespace KEYCE_KODER.AI.Tools.FileSystem;

public sealed class ReadFileTool : IAiTool
{
    private const long MaxFileSize = 1024 * 1024; // 1 Mo


    public AiToolDefinition Definition { get; } = new(
        "read_file",
        "Lit le contenu d'un fichier situé dans le dossier du projet.",
        false,
        new
        {
            type = "object",
            required = new[] { "path" },
            properties = new
            {
                path = new
                {
                    type = "string",
                    description = "Chemin relatif du fichier à lire dans le projet."
                }
            }
        });


    public async Task<AiToolResult> ExecuteAsync(
        JsonElement arguments,
        AiToolContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return new AiToolResult(
                false,
                "Les arguments doivent être un objet JSON.",
                "invalid_arguments");
        }

        if (!arguments.TryGetProperty("path", out JsonElement pathElement) ||
            pathElement.ValueKind != JsonValueKind.String)
        {
            return new AiToolResult(
                false,
                "Le chemin du fichier est obligatoire.",
                "missing_path");
        }

        string relativePath = pathElement.GetString()!;

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return new AiToolResult(
                false,
                "Le chemin du fichier ne peut pas être vide.",
                "invalid_path");
        }

        cancellationToken.ThrowIfCancellationRequested();

        string fullPath = context.ResolveProjectPath(relativePath);

        if (!File.Exists(fullPath))
        {
            return new AiToolResult(
                false,
                "Le fichier demandé n'existe pas.",
                "file_not_found");
        }

        var fileInfo = new FileInfo(fullPath);

        if (fileInfo.Length > MaxFileSize)
        {
            return new AiToolResult(
                false,
                "Le fichier dépasse la taille maximale autorisée de 1 Mo.",
                "file_too_large");
        }

        string content = await File.ReadAllTextAsync(
            fullPath,
            Encoding.UTF8,
            cancellationToken);

        return new AiToolResult(
            true,
            $"Contenu du fichier '{relativePath}' :\n\n{content}");
    }
}
