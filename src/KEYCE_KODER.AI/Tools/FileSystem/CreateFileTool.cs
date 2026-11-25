
using System.Text;
using System.Text.Json;
using KEYCE_KODER.AI.Tools.Contracts;
using KEYCE_KODER.AI.Tools.Execution;

namespace KEYCE_KODER.AI.Tools.FileSystem;

public sealed class CreateFileTool : IAiTool
{

    public AiToolDefinition Definition { get; } = new(
        "create_file",
        "Crée un nouveau fichier dans le dossier du projet.",
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
                    description = "Chemin relatif du nouveau fichier dans le projet."
                },
                content = new
                {
                    type = "string",
                    description = "Contenu complet à écrire dans le nouveau fichier."
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
                "Le contenu du fichier est obligatoire.",
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

        cancellationToken.ThrowIfCancellationRequested();

        string fullPath = context.ResolveProjectPath(relativePath);

        string? directory = Path.GetDirectoryName(fullPath);

        if (directory is null || !Directory.Exists(directory))
        {
            return new AiToolResult(
                false,
                "Le dossier parent du fichier n'existe pas.",
                "directory_not_found");
        }

        byte[] contentBytes = Encoding.UTF8.GetBytes(content);

        await using var stream = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);

        await stream.WriteAsync(contentBytes, cancellationToken);

        return new AiToolResult(
            true,
            $"Le fichier '{relativePath}' a été créé avec succès.");
    }
}
