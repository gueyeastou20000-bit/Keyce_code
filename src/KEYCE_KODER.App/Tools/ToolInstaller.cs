using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using KEYCE_KODER.Runner;

namespace KEYCE_KODER.App.Tools;

/// <summary>Installe les outils (Python, Node.js, JDK, GCC...) avec winget, le gestionnaire de paquets de Windows.</summary>
public sealed class ToolInstaller
{
    private static bool? _wingetAvailable;

    /// <summary>winget est-il disponible sur ce PC ? (résultat mémorisé)</summary>
    public static async Task<bool> IsWingetAvailableAsync()
    {
        if (_wingetAvailable is bool known)
            return known;

        try
        {
            using var process = Process.Start(new ProcessStartInfo("winget", "--version")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
            {
                _wingetAvailable = false;
                return false;
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await process.WaitForExitAsync(timeout.Token);
            _wingetAvailable = process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is Win32Exception or OperationCanceledException or InvalidOperationException)
        {
            _wingetAvailable = false;
        }

        return _wingetAvailable.Value;
    }

    /// <summary>Lance « winget install » et transmet chaque ligne affichée à onOutput. Retourne le code de sortie.</summary>
    public async Task<int> InstallAsync(
        ToolInstallInfo info,
        Action<string> onOutput,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo("winget")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        string[] arguments =
        [
            "install", "--id", info.WingetId, "--exact", "--silent",
            "--accept-package-agreements", "--accept-source-agreements",
            "--disable-interactivity"
        ];

        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => Forward(e.Data, onOutput);
        process.ErrorDataReceived += (_, e) => Forward(e.Data, onOutput);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }

    /// <summary>
    /// Après l'installation : rend l'outil utilisable tout de suite (PATH rafraîchi,
    /// et si besoin dossier ajouté au PATH). Retourne l'outil détecté, ou null.
    /// </summary>
    public static async Task<ResolvedTool?> MakeAvailableAsync(ToolSpec spec, ToolInstallInfo info)
    {
        EnvironmentPath.Refresh();

        ResolvedTool? tool = await ToolLocator.LocateAsync(spec);
        if (tool is not null)
            return tool;

        // Certains paquets (ex. GCC WinLibs) ne s'ajoutent pas au PATH : on cherche l'exécutable
        string? folder = EnvironmentPath.FindExecutableFolder(info);
        if (folder is null)
            return null;

        EnvironmentPath.AddToUserPath(folder);
        return await ToolLocator.LocateAsync(spec);
    }

    private static void Forward(string? line, Action<string> onOutput)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        string clean = line.Trim();

        // winget affiche une animation (- \ | /) et des barres de progression : on les ignore
        if (clean.Length <= 2 || clean.Contains('█') || clean.Contains('▒'))
            return;

        onOutput(clean);
    }
}
