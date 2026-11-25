using System.Text;
using System.Text.Json;
using KEYCE_KODER.AI.Tools.Contracts;
using KEYCE_KODER.AI.Tools.Execution;

namespace KEYCE_KODER.AI.Tools.FileSystem;

public sealed class ReadFileTool : IAiTool
{
    private const long MaxFileSize = 1024 * 1024;
    private const int DefaultMaxLines = 150;
    private const int MaxLinesLimit = 300;
    private const int MaxOutputCharacters = 16000;

    public AiToolDefinition Definition { get; } = new(
        "read_file",
        "Lit le contenu d'un fichier du projet. Permet de choisir la ligne de départ et le nombre de lignes.",
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
                    description = "Chemin relatif du fichier dans le projet."
                },
                start_line = new
                {
                    type = "integer",
                    description = "Numéro de la première ligne à lire. Défaut : 1."
                },
                max_lines = new
                {
                    type = "integer",
                    description = "Nombre maximal de lignes à lire. Défaut : 150, maximum : 300."
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

        int startLine = 1;
        int maxLines = DefaultMaxLines;

        if (arguments.TryGetProperty("start_line", out JsonElement startElement))
        {
            if (startElement.ValueKind != JsonValueKind.Number ||
                !startElement.TryGetInt32(out startLine) ||
                startLine < 1)
            {
                return new AiToolResult(
                    false,
                    "start_line doit être un entier supérieur ou égal à 1.",
                    "invalid_start_line");
            }
        }

        if (arguments.TryGetProperty("max_lines", out JsonElement maxElement))
        {
            if (maxElement.ValueKind != JsonValueKind.Number ||
                !maxElement.TryGetInt32(out maxLines) ||
                maxLines < 1 ||
                maxLines > MaxLinesLimit)
            {
                return new AiToolResult(
                    false,
                    $"max_lines doit être compris entre 1 et {MaxLinesLimit}.",
                    "invalid_max_lines");
            }
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

        var output = new StringBuilder();
        int currentLine = 0;
        int returnedLines = 0;
        bool hasMore = false;

        await using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        using var reader = new StreamReader(stream, Encoding.UTF8, true);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? line = await reader.ReadLineAsync(cancellationToken);

            if (line is null)
                break;

            currentLine++;

            if (currentLine < startLine)
                continue;

            if (returnedLines >= maxLines)
            {
                hasMore = true;
                break;
            }

            string formattedLine = $"{currentLine}: {line}";
            int remaining = MaxOutputCharacters - output.Length;

            if (formattedLine.Length + Environment.NewLine.Length > remaining)
            {
                if (remaining > 0)
                {
                    output.Append(
                        formattedLine.Substring(
                            0,
                            Math.Min(formattedLine.Length, remaining)));
                }

                hasMore = true;
                break;
            }

            output.AppendLine(formattedLine);
            returnedLines++;
        }

        if (returnedLines == 0 && output.Length == 0)
        {
            return new AiToolResult(
                false,
                $"Aucune ligne trouvée à partir de la ligne {startLine}.",
                "line_not_found");
        }

        string message =
            $"Fichier : {relativePath}\n" +
            $"Lignes affichées : {startLine} à {currentLine}\n" +
            $"Nombre de lignes : {returnedLines}\n" +
            $"Suite disponible : {(hasMore ? "oui" : "non")}\n\n" +
            output;

        if (hasMore)
        {
            message +=
                $"\nLa lecture est limitée. Pour continuer, " +
                $"demandez le fichier à partir de la ligne {currentLine + 1}.";
        }

        return new AiToolResult(true, message);
    }
}
