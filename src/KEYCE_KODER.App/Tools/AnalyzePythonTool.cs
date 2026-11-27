using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using KEYCE_KODER.AI.Tools.Contracts;
using KEYCE_KODER.AI.Tools.Execution;
using KEYCE_KODER.Runner;

namespace KEYCE_KODER.App.Tools;

public sealed class AnalyzePythonTool : IAiTool
{
    private const long MaxFileSize = 1024 * 1024;

    private const string AnalyzerCode = """
import ast
import sys

source = sys.stdin.read()

try:
    ast.parse(source, filename=sys.argv[1])
except SyntaxError as error:
    print(f"SYNTAX_ERROR|{error.lineno or 0}|{error.offset or 0}|{error.msg}")
    raise SystemExit(1)

print("SYNTAX_OK")
""";

    public AiToolDefinition Definition { get; } = new(
        "analyze_python",
        "Analyse la syntaxe d'un fichier Python sans exécuter son programme. Retourne les erreurs avec leur ligne et colonne.",
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
                    description = "Chemin relatif du fichier Python dans le projet."
                }
            }
        });

    public async Task<AiToolResult> ExecuteAsync(
        JsonElement arguments,
        AiToolContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (arguments.ValueKind != JsonValueKind.Object ||
            !arguments.TryGetProperty("path", out var pathElement) ||
            pathElement.ValueKind != JsonValueKind.String)
        {
            return new AiToolResult(
                false,
                "Le chemin du fichier Python est obligatoire.",
                "missing_path");
        }

        string relativePath = pathElement.GetString()!;

        if (!relativePath.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
        {
            return new AiToolResult(
                false,
                "L'analyse concerne uniquement les fichiers .py.",
                "invalid_extension");
        }

        cancellationToken.ThrowIfCancellationRequested();

        string fullPath = context.ResolveProjectPath(relativePath);

        if (!File.Exists(fullPath))
        {
            return new AiToolResult(
                false,
                "Le fichier Python demandé est introuvable.",
                "file_not_found");
        }

        var info = new FileInfo(fullPath);

        if (info.Length > MaxFileSize)
        {
            return new AiToolResult(
                false,
                "Le fichier dépasse la limite de 1 Mo.",
                "file_too_large");
        }

        string source = await File.ReadAllTextAsync(
            fullPath,
            cancellationToken);

        var profile = LanguageProfiles.ForFile(fullPath);

        if (profile is null)
        {
            return new AiToolResult(
                false,
                "Profil Python introuvable.",
                "profile_not_found");
        }

        var python = await ToolLocator.LocateAsync(
            profile.Tool,
            ct: cancellationToken);

        if (python is null)
        {
            return new AiToolResult(
                false,
                "Python 3 est introuvable sur cet ordinateur.",
                "python_not_found");
        }

        var startInfo = new ProcessStartInfo(python.FileName)
        {
            WorkingDirectory = context.ProjectRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (string arg in python.PrefixArgs)
            startInfo.ArgumentList.Add(arg);

        startInfo.ArgumentList.Add("-X");
        startInfo.ArgumentList.Add("utf8");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(AnalyzerCode);
        startInfo.ArgumentList.Add(relativePath);

        using var process = new Process
        {
            StartInfo = startInfo
        };

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!process.Start())
            {
                return new AiToolResult(
                    false,
                    "Impossible de démarrer Python.",
                    "python_start_failed");
            }

            Task<string> outputTask =
                process.StandardOutput.ReadToEndAsync();

            Task<string> errorTask =
                process.StandardError.ReadToEndAsync();

            using var timeout = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken);

            timeout.CancelAfter(TimeSpan.FromSeconds(10));

            try
            {
                await process.StandardInput.WriteAsync(
                    source.AsMemory(),
                    timeout.Token);

                process.StandardInput.Close();

                await process.WaitForExitAsync(timeout.Token);

                string output = await outputTask;
                string error = await errorTask;

                if (output.StartsWith(
                    "SYNTAX_OK",
                    StringComparison.Ordinal))
                {
                    return new AiToolResult(
                        true,
                        $"Analyse Python réussie : {relativePath}\n" +
                        "Aucune erreur de syntaxe détectée. " +
                        "Le programme n'a pas été exécuté.");
                }

                if (output.StartsWith(
                    "SYNTAX_ERROR|",
                    StringComparison.Ordinal))
                {
                    string[] parts = output.Trim().Split('|', 4);

                    if (parts.Length == 4 &&
                        int.TryParse(parts[1], out int line) &&
                        int.TryParse(parts[2], out int column))
                    {
                        return new AiToolResult(
                            false,
                            $"Erreur de syntaxe dans {relativePath}\n" +
                            $"Ligne : {line}\n" +
                            $"Colonne : {column}\n" +
                            $"Détail : {parts[3]}",
                            "python_syntax_error");
                    }
                }

                return new AiToolResult(
                    false,
                    "L'analyse Python n'a pas abouti.\n" +
                    (string.IsNullOrWhiteSpace(error)
                        ? "Résultat inattendu de l'interpréteur."
                        : error.Trim()),
                    "python_analysis_failed");
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(true);
                }
                catch
                {
                    // Le processus peut déjà être terminé.
                }

                if (cancellationToken.IsCancellationRequested)
                    throw;

                return new AiToolResult(
                    false,
                    "L'analyse Python a dépassé le délai de 10 secondes.",
                    "python_analysis_timeout");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new AiToolResult(
                false,
                "Impossible de réaliser l'analyse Python.",
                "python_analysis_failed");
        }
    }
}
