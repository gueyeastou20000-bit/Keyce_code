using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

using KEYCE_KODER.Core.Execution;

namespace KEYCE_KODER.Runner;

public sealed record PythonInterpreter(string FileName, IReadOnlyList<string> PrefixArgs, string Version)
{
    public string Display => PrefixArgs.Count == 0 ? FileName : $"{FileName} {string.Join(' ', PrefixArgs)}";
}

public static class PythonLocator
{
    private static PythonInterpreter? _cached;

    public static async Task<PythonInterpreter?> LocateAsync(string? customPath = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(customPath) && _cached is not null) return _cached;

        var candidates = new List<(string File, string[] Prefix)>();
        if (!string.IsNullOrWhiteSpace(customPath)) candidates.Add((customPath, []));
        candidates.Add(("python", []));
        candidates.Add(("py", ["-3"]));

        foreach (var (file, prefix) in candidates)
        {
            var version = await ProbeAsync(file, prefix, ct);
            if (version is null) continue;

            var found = new PythonInterpreter(file, prefix, version);
            if (string.IsNullOrWhiteSpace(customPath)) _cached = found;
            return found;
        }
        return null;
    }

    public static void ResetCache() => _cached = null;

    // Vérifie réellement l'exécution : l'alias "python" du Microsoft Store existe mais échoue.
    private static async Task<string?> ProbeAsync(string file, string[] prefix, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in prefix) psi.ArgumentList.Add(a);
        psi.ArgumentList.Add("--version");

        Process? p = null;
        try
        {
            p = Process.Start(psi);
            if (p is null) return null;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var outTask = p.StandardOutput.ReadToEndAsync(cts.Token);
            var errTask = p.StandardError.ReadToEndAsync(cts.Token);
            await p.WaitForExitAsync(cts.Token);

            var text = ((await outTask) + (await errTask)).Trim();
            return p.ExitCode == 0 && Regex.IsMatch(text, @"^Python 3\.\d+") ? text : null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or OperationCanceledException)
        {
            ct.ThrowIfCancellationRequested();
            try { if (p is { HasExited: false }) p.Kill(true); } catch { /* ignoré */ }
            return null;
        }
        finally { p?.Dispose(); }
    }
}
