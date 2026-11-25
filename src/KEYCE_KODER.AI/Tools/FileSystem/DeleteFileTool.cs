
using System.Text.Json;
using KEYCE_KODER.AI.Tools.Contracts;
using KEYCE_KODER.AI.Tools.Execution;

namespace KEYCE_KODER.AI.Tools.FileSystem;

public sealed class DeleteFileTool : IAiTool
{

    public AiToolDefinition Definition { get; } = new(
        "delete_file",
        "Supprime un fichier existant dans le dossier du projet.",
        true,
        new
        {
            type = "object",
            required = new[] { "path" },
            properties = new
            {
                path = new
                {
                    type = "string",
                    description = "Chemin relatif du fichier à supprimer."
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

        if (!arguments.TryGetProperty("path", out JsonElement pathElement) ||
            pathElement.ValueKind != JsonValueKind.String)
        {
            return Task.FromResult(new AiToolResult(
                false,
                "Le chemin du fichier est obligatoire.",
                "missing_path"));
        }

        string relativePath = pathElement.GetString()!;

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return Task.FromResult(new AiToolResult(
                false,
                "Le chemin du fichier ne peut pas être vide.",
                "invalid_path"));
        }

        cancellationToken.ThrowIfCancellationRequested();

        string fullPath = context.ResolveProjectPath(relativePath);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult(new AiToolResult(
                false,
                "Le fichier à supprimer n'existe pas.",
                "file_not_found"));
        }

        File.Delete(fullPath);

        return Task.FromResult(new AiToolResult(
            true,
            $"Le fichier '{relativePath}' a été supprimé avec succès."));
    }
}
