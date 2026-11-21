using System.ComponentModel;
using System.Diagnostics;
using System.Text;

using KEYCE_KODER.Core.Execution;

namespace KEYCE_KODER.Runner;

/// <summary>À instancier sur le thread UI (capture du SynchronizationContext).</summary>
public sealed class PythonRunner : IScriptRunner
{
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private bool _stopRequested;
    private RunState _state = RunState.Idle;

    public string? CustomPythonPath { get; set; }
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(60);

    public RunState State { get { lock (_gate) return _state; } }

    public event EventHandler<IReadOnlyList<OutputLine>>? OutputReceived;
    public event EventHandler<RunCompletedEventArgs>? Completed;
    public event EventHandler? StateChanged;

    public async Task RunAsync(string scriptPath, IEnumerable<string>? args = null, TimeSpan? timeout = null)
    {
        CancellationTokenSource cts;
        lock (_gate)
        {
            if (_state == RunState.Running)
                throw new InvalidOperationException("Une exécution est déjà en cours.");
            _state = RunState.Running;
            _stopRequested = false;
            cts = _cts = new CancellationTokenSource();
        }
        UiDispatch.Post(_ui, () => StateChanged?.Invoke(this, EventArgs.Empty));

        var pump = new OutputPump(batch => OutputReceived?.Invoke(this, batch), _ui);
        var sw = Stopwatch.StartNew();
        var result = RunResult.FailedToStart;
        int? exitCode = null;

        try
        {
            var python = await PythonLocator.LocateAsync(CustomPythonPath, cts.Token);
            if (python is null)
            {
                pump.Write(new(OutputKind.StdErr, "Python 3 introuvable (essayé : python, py -3). Définissez un chemin dans les paramètres.\n"));
                return;
            }

            var full = Path.GetFullPath(scriptPath);
            if (!File.Exists(full))
            {
                pump.Write(new(OutputKind.StdErr, $"Fichier introuvable : {full}\n"));
                return;
            }

            using var proc = new Process { StartInfo = BuildStartInfo(python, full, args) };
            pump.Write(new(OutputKind.Info, $"> {python.Display} {Path.GetFileName(full)}\n"));

            proc.Start();
            proc.StandardInput.Close(); // pas d'entrée interactive : input() lève EOFError au lieu de bloquer

            var outTask = ReadLinesAsync(proc.StandardOutput, OutputKind.StdOut, pump);
            var errTask = ReadLinesAsync(proc.StandardError, OutputKind.StdErr, pump);

            var limit = timeout ?? DefaultTimeout;
            cts.CancelAfter(limit);

            try
            {
                await proc.WaitForExitAsync(cts.Token);
                result = RunResult.Completed;
            }
            catch (OperationCanceledException)
            {
                bool stopped;
                lock (_gate) stopped = _stopRequested;
                result = stopped ? RunResult.Stopped : RunResult.TimedOut;
                Kill(proc);
                await proc.WaitForExitAsync();
            }

            // Les derniers flux ne sont fiables qu'après le null final ; délai de garde si un petit-fils garde le pipe.
            await Task.WhenAny(Task.WhenAll(outTask, errTask), Task.Delay(2000));

            exitCode = proc.HasExited ? proc.ExitCode : null;
            var msg = result switch
            {
                RunResult.Completed => $"\nTerminé (code {exitCode}) en {sw.Elapsed.TotalSeconds:0.0} s\n",
                RunResult.Stopped => "\nArrêté par l'utilisateur\n",
                _ => $"\nTimeout après {limit.TotalSeconds:0} s : processus arrêté\n",
            };
            pump.Write(new(OutputKind.Info, msg));
        }
        catch (OperationCanceledException)
        {
            result = RunResult.Stopped; // Stop pendant la détection de Python
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException)
        {
            pump.Write(new(OutputKind.StdErr, $"Échec du lancement : {ex.Message}\n"));
        }
        finally
        {
            await pump.CompleteAsync(); // garantit que Completed arrive après le dernier lot
            lock (_gate)
            {
                _cts?.Dispose();
                _cts = null;
                _state = RunState.Idle;
            }
            var args2 = new RunCompletedEventArgs(result, exitCode, sw.Elapsed);
            UiDispatch.Post(_ui, () =>
            {
                Completed?.Invoke(this, args2);
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
            _cts?.Cancel();
        }
    }

    private static ProcessStartInfo BuildStartInfo(PythonInterpreter py, string script, IEnumerable<string>? args)
    {
        var utf8 = new UTF8Encoding(false);
        var psi = new ProcessStartInfo(py.FileName)
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
        foreach (var a in py.PrefixArgs) psi.ArgumentList.Add(a);
        psi.ArgumentList.Add("-u");
        psi.ArgumentList.Add(script);
        if (args is not null) foreach (var a in args) psi.ArgumentList.Add(a);

        psi.Environment["PYTHONUNBUFFERED"] = "1";
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        return psi;
    }

    private static async Task ReadLinesAsync(StreamReader reader, OutputKind kind, OutputPump pump)
    {
        try
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) is not null)
                pump.Write(new(kind, line + "\n"));
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    private static void Kill(Process p)
    {
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }
    }
}
