
using System.Text;
using System.Text.Json;
using KEYCE_KODER.AI.Tools.Contracts;
using KEYCE_KODER.AI.Tools.Execution;

namespace KEYCE_KODER.AI.Tools.FileSystem;

public sealed class UpdateFileTool : IAiTool
{
    private const int MaxContentSize = 1024 * 1024; // 1 Mo


    public AiToolDefinition Definition { get; } = new(
        "update_file",
        "Remplace entièrement le contenu d'un fichier existant dans le projet.",
        true,
        new
        {
            type = "object",
            required = new[] { "path", "content" },
            properties = new
            {
                path = new
                {
                    type = "string",
                    description = "Chemin relatif du fichier à modifier."
                },
                content = new
                {
                    type = "string",
                    description = "Nouveau contenu complet du fichier."
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

        if (!arguments.TryGetProperty("content", out JsonElement contentElement) ||
            contentElement.ValueKind != JsonValueKind.String)
        {
            return new AiToolResult(
                false,
                "Le nouveau contenu du fichier est obligatoire.",
                "missing_content");
        }

        string relativePath = pathElement.GetString()!;
        string content = contentElement.GetString()!;

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return new AiToolResult(
                false,
                "Le chemin du fichier ne peut pas être vide.",
                "invalid_path");
        }

        if (Encoding.UTF8.GetByteCount(content) > MaxContentSize)
        {
            return new AiToolResult(
                false,
                "Le nouveau contenu dépasse la limite de 1 Mo.",
                "content_too_large");
        }

        cancellationToken.ThrowIfCancellationRequested();

        string fullPath = context.ResolveProjectPath(relativePath);

        if (!File.Exists(fullPath))
        {
            return new AiToolResult(
                false,
                "Impossible de modifier le fichier : il n'existe pas.",
                "file_not_found");
        }

        await File.WriteAllTextAsync(
            fullPath,
            content,
            Encoding.UTF8,
            cancellationToken);

        return new AiToolResult(
            true,
            $"Le fichier '{relativePath}' a été modifié avec succès.");
    }
}
