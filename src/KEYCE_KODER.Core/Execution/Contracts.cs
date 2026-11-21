namespace KEYCE_KODER.Core.Execution;

public enum RunState { Idle, Running }
public enum RunResult { Completed, Stopped, TimedOut, FailedToStart }
public enum OutputKind { StdOut, StdErr, Info }

/// <summary>Texte brut à ajouter tel quel au panneau (retours à la ligne inclus).</summary>
public readonly record struct OutputLine(OutputKind Kind, string Text);

public sealed record RunCompletedEventArgs(RunResult Result, int? ExitCode, TimeSpan Duration);

/// <summary>Contrat consommé par le panneau inférieur. Tous les événements sont levés sur le thread UI
/// si le runner a été construit sur ce thread.</summary>
public interface IScriptRunner
{
    RunState State { get; }
    event EventHandler<IReadOnlyList<OutputLine>>? OutputReceived;   // par lots
    event EventHandler<RunCompletedEventArgs>? Completed;
    event EventHandler? StateChanged;

    Task RunAsync(string scriptPath, IEnumerable<string>? args = null, TimeSpan? timeout = null);
    void Stop();
}
