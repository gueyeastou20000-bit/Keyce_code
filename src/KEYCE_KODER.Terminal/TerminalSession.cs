using System.ComponentModel;
using System.Diagnostics;
using System.Text;

using KEYCE_KODER.Core.Execution;

namespace KEYCE_KODER.Terminal;

public enum TerminalShell { PowerShell, Cmd }

/// <summary>Shell persistant avec stdin redirigé. Limite connue : pas de pseudo-terminal (ConPTY),
/// donc pas de programmes interactifs ni de Ctrl+C ; utiliser Restart() pour interrompre.
/// À instancier sur le thread UI.</summary>
public sealed class TerminalSession : IDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private Process? _proc;
    private OutputPump? _pump;

    /// <summary>Contexte sur lequel les événements sont levés (thread UI). À renseigner explicitement en WPF ;
    /// sinon capturé au premier Start.</summary>
    public SynchronizationContext? UiContext { get; set; } = SynchronizationContext.Current;

    public TerminalShell Shell { get; }
    public string WorkingDirectory { get; set; }
    public CommandHistory History { get; } = new();
    public bool IsRunning { get { lock (_gate) return _proc is { HasExited: false }; } }

    public event EventHandler<IReadOnlyList<OutputLine>>? OutputReceived;
    public event EventHandler<int>? Exited;

    public TerminalSession(TerminalShell shell = TerminalShell.PowerShell, string? workingDirectory = null)
    {
        Shell = shell;
        WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory;
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_proc is { HasExited: false }) return;
            UiContext ??= SynchronizationContext.Current;

            var utf8 = new UTF8Encoding(false);
            var psi = new ProcessStartInfo(Shell == TerminalShell.PowerShell ? "powershell.exe" : "cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = utf8,
                StandardOutputEncoding = utf8,
                StandardErrorEncoding = utf8,
                WorkingDirectory = WorkingDirectory,
            };
            if (Shell == TerminalShell.PowerShell)
                foreach (var a in new[] { "-NoLogo", "-NoProfile", "-NoExit", "-Command", "-" }) psi.ArgumentList.Add(a);
            else
                foreach (var a in new[] { "/Q", "/K" }) psi.ArgumentList.Add(a);

            var proc = Process.Start(psi) ?? throw new InvalidOperationException("Impossible de démarrer le shell.");
            proc.StandardInput.AutoFlush = true;

            var pump = new OutputPump(b => OutputReceived?.Invoke(this, b), UiContext);
            _proc = proc;
            _pump = pump;

            var outT = ReadChunksAsync(proc.StandardOutput, OutputKind.StdOut, pump);
            var errT = ReadChunksAsync(proc.StandardError, OutputKind.StdErr, pump);
            _ = MonitorAsync(proc, pump, outT, errT);

            // Sortie en UTF-8 pour un affichage correct des accents.
            proc.StandardInput.WriteLine(Shell == TerminalShell.PowerShell
                ? "[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)"
                : "chcp 65001 >nul");
        }
    }

    public async Task SendCommandAsync(string command)
    {
        Process? proc; OutputPump? pump;
        lock (_gate) { proc = _proc; pump = _pump; }
        if (proc is null || proc.HasExited)
            throw new InvalidOperationException("Le terminal n'est pas démarré.");

        command = command.TrimEnd();
        if (command.Length == 0) return;

        History.Add(command);
        // Le shell n'écho pas l'entrée redirigée : on l'affiche nous-mêmes.
        pump?.Write(new(OutputKind.Info, (Shell == TerminalShell.PowerShell ? "PS> " : "") + command + "\n"));

        await _writeLock.WaitAsync();
        try { await proc.StandardInput.WriteLineAsync(command); }
        finally { _writeLock.Release(); }
    }

    public void Restart()
    {
        Stop();
        Start();
    }

    public void Stop()
    {
        Process? proc;
        lock (_gate) { proc = _proc; _proc = null; }
        if (proc is null) return;
        try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }
    }

    public void Dispose() => Stop();

    // Lecture par blocs de caractères : le prompt n'a pas de retour à la ligne, ReadLine le bloquerait.
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

    private async Task MonitorAsync(Process proc, OutputPump pump, Task outT, Task errT)
    {
        try
        {
            await proc.WaitForExitAsync();
            await Task.WhenAny(Task.WhenAll(outT, errT), Task.Delay(1000));
            await pump.CompleteAsync();
            var code = proc.ExitCode;
            UiDispatch.Post(UiContext, () => Exited?.Invoke(this, code));
        }
        catch (Exception) { /* process déjà libéré */ }
        finally { proc.Dispose(); }
    }
}
