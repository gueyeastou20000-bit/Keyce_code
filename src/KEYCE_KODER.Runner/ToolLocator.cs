using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace KEYCE_KODER.Runner;

/// <summary>Détecte un interpréteur ou un compilateur en l'exécutant réellement (version + code de sortie 0).
/// Cela écarte par exemple l'alias « python » du Microsoft Store.</summary>
public static class ToolLocator
{
    private static readonly ConcurrentDictionary<string, ResolvedTool> Cache = new();

    public static async Task<ResolvedTool?> LocateAsync(ToolSpec spec, string? customPath = null, CancellationToken ct = default)
    {
        var hasCustom = !string.IsNullOrWhiteSpace(customPath);
        if (!hasCustom && Cache.TryGetValue(spec.Id, out var cached)) return cached;

        var candidates = new List<ToolCandidate>();
        if (hasCustom) candidates.Add(new ToolCandidate(customPath!, []));
        candidates.AddRange(spec.Candidates);

        foreach (var candidate in candidates)
        {
            var version = await ProbeAsync(candidate, spec, ct);
            if (version is null) continue;

            var found = new ResolvedTool(candidate.FileName, candidate.PrefixArgs, version);
            if (!hasCustom) Cache[spec.Id] = found;
            return found;
        }
        return null;
    }

    public static void ResetCache() => Cache.Clear();

    private static async Task<string?> ProbeAsync(ToolCandidate candidate, ToolSpec spec, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(candidate.FileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in candidate.PrefixArgs) psi.ArgumentList.Add(a);
        foreach (var a in spec.VersionArgs) psi.ArgumentList.Add(a);

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

            var text = ((await outTask) + "\n" + (await errTask)).Trim();
            if (p.ExitCode != 0) return null;
            if (!Regex.IsMatch(text, spec.VersionPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline)) return null;

            return text.Split('\n')[0].Trim();
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
