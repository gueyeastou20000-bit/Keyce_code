using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using KEYCE_KODER.Core.Execution;

namespace KEYCE_KODER.Runner;

/// <summary>Exécute un fichier source (Python, JavaScript, Java, C, C++) : détecte l'outil, compile si besoin,
/// transmet stdout/stderr en continu et accepte une saisie au clavier (stdin).</summary>
public sealed class ScriptRunner : IScriptRunner
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private bool _stopRequested;
    private Process? _proc;
    private StreamWriter? _stdin;
    private RunState _state = RunState.Idle;

    /// <summary>Contexte sur lequel les événements sont levés (thread UI). À renseigner explicitement en WPF ;
    /// sinon capturé au premier Run.</summary>
    public SynchronizationContext? UiContext { get; set; } = SynchronizationContext.Current;

    /// <summary>Chemins personnalisés par outil (clé = ToolSpec.Id : « python », « node », « java », « gcc », « g++ »).</summary>
    public Dictionary<string, string> CustomToolPaths { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Durée maximale du programme lui-même (la compilation n'est pas comptée).</summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(60);

    public RunState State { get { lock (_gate) return _state; } }

    public event EventHandler<IReadOnlyList<OutputLine>>? OutputReceived;
    public event EventHandler<RunCompletedEventArgs>? Completed;
    public event EventHandler? StateChanged;

    public async Task RunAsync(string scriptPath, IEnumerable<string>? args = null, TimeSpan? timeout = null)
    {
        UiContext ??= SynchronizationContext.Current;

        CancellationTokenSource cts;
        lock (_gate)
        {
            if (_state == RunState.Running)
                throw new InvalidOperationException("Une exécution est déjà en cours.");
            _state = RunState.Running;
            _stopRequested = false;
            cts = _cts = new CancellationTokenSource();
        }
        UiDispatch.Post(UiContext, () => StateChanged?.Invoke(this, EventArgs.Empty));

        var pump = new OutputPump(batch => OutputReceived?.Invoke(this, batch), UiContext);
        var sw = Stopwatch.StartNew();
        var result = RunResult.FailedToStart;
        int? exitCode = null;
        string? buildDir = null;

        try
        {
            var script = Path.GetFullPath(scriptPath);
            if (!File.Exists(script))
            {
                Error(pump, $"Fichier introuvable : {script}");
                return;
            }

            var profile = LanguageProfiles.ForFile(script);
            if (profile is null)
            {
                Error(pump, $"Type de fichier non pris en charge : {Path.GetExtension(script)}. " +
                            $"Pris en charge : {string.Join(' ', LanguageProfiles.SupportedExtensions)}");
                return;
            }

            CustomToolPaths.TryGetValue(profile.Tool.Id, out var customPath);
            var tool = await ToolLocator.LocateAsync(profile.Tool, customPath, cts.Token);
            if (tool is null)
            {
                Error(pump, $"{profile.Tool.DisplayName} introuvable. {profile.Tool.InstallHint}");
                return;
            }

            buildDir = Path.Combine(Path.GetTempPath(), "KEYCE_KODER", "build", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(buildDir);

            var steps = profile.BuildSteps(tool, script, buildDir);
            var limit = timeout ?? DefaultTimeout;
            var buildFailed = false;

            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var last = i == steps.Count - 1;
                pump.Write(new(OutputKind.Info, $"> {step.Display}\n"));

                (result, exitCode) = await ExecuteStepAsync(
                    step, last ? args : null, last,
                    last ? limit : Timeout.InfiniteTimeSpan,
                    script, pump, cts);

                if (result != RunResult.Completed) break;
                if (!last && exitCode != 0) { buildFailed = true; break; }
            }

            var message = buildFailed
                ? $"\nÉchec de la compilation (code {exitCode})\n"
                : result switch
                {
                    RunResult.Completed => $"\nTerminé (code {exitCode}) en {sw.Elapsed.TotalSeconds:0.0} s\n",
                    RunResult.Stopped => "\nArrêté par l'utilisateur\n",
                    _ => $"\nTimeout après {limit.TotalSeconds:0} s : processus arrêté\n",
                };
            pump.Write(new(OutputKind.Info, message));
        }
        catch (OperationCanceledException)
        {
            result = RunResult.Stopped; // Stop pendant la détection de l'outil
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException)
        {
            Error(pump, $"Échec du lancement : {ex.Message}");
        }
        finally
        {
            // Garantit que Completed arrive après le dernier lot ; une erreur d'affichage ne doit jamais bloquer l'état.
            try { await pump.CompleteAsync(); } catch (Exception) { }

            lock (_gate)
            {
                _cts?.Dispose();
                _cts = null;
                _proc = null;
                _stdin = null;
                _state = RunState.Idle;
            }

            if (buildDir is not null)
            {
                try { Directory.Delete(buildDir, recursive: true); } catch (Exception) { }
            }

            var done = new RunCompletedEventArgs(result, exitCode, sw.Elapsed);
            UiDispatch.Post(UiContext, () =>
            {
                Completed?.Invoke(this, done);
                StateChanged?.Invoke(this, EventArgs.Empty);
            });
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_state != RunState.Running) return;
            _stopRequested = true;
            _cts?.Cancel();                // d'abord : le résultat sera « Stopped »
            if (_proc is { } p) Kill(p);   // puis arrêt immédiat du processus et de ses enfants
        }
    }

    public void SendInput(string text)
    {
        StreamWriter? writer;
        lock (_gate) writer = _stdin;
        if (writer is null) return;

        try { writer.WriteLine(text); writer.Flush(); }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    private async Task<(RunResult Result, int? ExitCode)> ExecuteStepAsync(
        RunStep step, IEnumerable<string>? args, bool interactive, TimeSpan limit,
        string script, OutputPump pump, CancellationTokenSource cts)
    {
        using var proc = new Process { StartInfo = BuildStartInfo(step, args, script) };
        proc.Start();

        lock (_gate)
        {
            _proc = proc;
            _stdin = interactive ? proc.StandardInput : null;
        }
        if (!interactive) proc.StandardInput.Close();

        var outTask = ReadChunksAsync(proc.StandardOutput, OutputKind.StdOut, pump);
        var errTask = ReadChunksAsync(proc.StandardError, OutputKind.StdErr, pump);

        if (limit != Timeout.InfiniteTimeSpan) cts.CancelAfter(limit);

        var result = RunResult.Completed;
        try
        {
            await proc.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            bool stopped;
            lock (_gate) stopped = _stopRequested;
            result = stopped ? RunResult.Stopped : RunResult.TimedOut;
            Kill(proc);
            await proc.WaitForExitAsync();
        }

        // Les derniers flux ne sont fiables qu'à la fin de lecture ; délai de garde si un petit-fils garde le pipe.
        await Task.WhenAny(Task.WhenAll(outTask, errTask), Task.Delay(2000));

        lock (_gate)
        {
            _proc = null;
            _stdin = null;
        }
        int? code = proc.HasExited ? proc.ExitCode : null;
        return (result, code);
    }

    private static ProcessStartInfo BuildStartInfo(RunStep step, IEnumerable<string>? args, string script)
    {
        var utf8 = new UTF8Encoding(false);
        var psi = new ProcessStartInfo(step.FileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = utf8,
            StandardOutputEncoding = utf8,
            StandardErrorEncoding = utf8,
            WorkingDirectory = Path.GetDirectoryName(script)!,
        };
        foreach (var a in step.Args) psi.ArgumentList.Add(a);
        if (args is not null) foreach (var a in args) psi.ArgumentList.Add(a);

        psi.Environment["PYTHONUNBUFFERED"] = "1";
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        if (step.IsBuild) psi.Environment["LC_ALL"] = "C"; // messages du compilateur en anglais, sans accents cassés
        return psi;
    }

    // Lecture par blocs (et non par lignes) : un prompt comme input("Nom : ") n'a pas de retour à la ligne.
    private static async Task ReadChunksAsync(StreamReader reader, OutputKind kind, OutputPump pump)
    {
        var buffer = new char[4096];
        try
        {
            int n;
            while ((n = await reader.ReadAsync(buffer)) > 0)
                pump.Write(new(kind, new string(buffer, 0, n)));
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    private static void Error(OutputPump pump, string message)
        => pump.Write(new(OutputKind.StdErr, message + "\n"));

    private static void Kill(Process p)
    {
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }
    }
}
