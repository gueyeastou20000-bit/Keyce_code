using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

using KEYCE_KODER.Core.Execution;

namespace KEYCE_KODER.Terminal;

public enum TerminalShell { PowerShell, Cmd, GitBash }

/// <summary>Fin d'une commande : dossier courant du shell et réussite de la commande.</summary>
public sealed record TerminalPrompt(string WorkingDirectory, bool Success);

/// <summary>
/// Shell persistant (PowerShell, Invite de commandes ou Git Bash) avec entrées/sorties redirigées.
/// Après chaque commande, un « marqueur » invisible est demandé au shell : il indique la fin de la
/// commande, le dossier courant et la réussite. On sait ainsi quand une commande est en cours
/// (le texte tapé est alors envoyé au programme : input(), Read-Host...) et on peut l'interrompre (Ctrl+C).
/// Git est réglé pour ce terminal : couleurs, pas de pager, éditeur = Bloc-notes.
/// À instancier sur le thread UI.
/// </summary>
public sealed class TerminalSession : IDisposable
{
    private const string Marker = "__KEYCE_DONE__";

    // __KEYCE_DONE__<dossier>__<0 ou 1>
    private static readonly Regex MarkerPattern = new(
        Regex.Escape(Marker) + @"(?<dir>.*?)__(?<ok>\d+)\r?\n", RegexOptions.Compiled);

    private readonly object _gate = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private Process? _proc;
    private OutputPump? _pump;
    private bool _busy;
    private TaskCompletionSource _startup = new(TaskCreationOptions.RunContinuationsAsynchronously); // première invite

    /// <summary>Contexte sur lequel les événements sont levés (thread UI). À renseigner explicitement en WPF ;
    /// sinon capturé au premier Start.</summary>
    public SynchronizationContext? UiContext { get; set; } = SynchronizationContext.Current;

    /// <summary>Shell utilisé au prochain démarrage (Restart pour changer tout de suite).</summary>
    public TerminalShell Shell { get; set; }

    /// <summary>Dossier courant : mis à jour après chaque commande (cd...).</summary>
    public string WorkingDirectory { get; set; }

    public CommandHistory History { get; } = new();

    public bool IsRunning { get { lock (_gate) return _proc is { HasExited: false }; } }

    /// <summary>Une commande est en cours d'exécution (le texte tapé lui est envoyé).</summary>
    public bool IsBusy { get { lock (_gate) return _busy; } }

    public event EventHandler<IReadOnlyList<OutputLine>>? OutputReceived;
    public event EventHandler<int>? Exited;

    /// <summary>Le shell attend une nouvelle commande (dossier courant, réussite de la précédente).</summary>
    public event EventHandler<TerminalPrompt>? PromptReady;

    /// <summary>Une commande commence ou se termine.</summary>
    public event EventHandler? BusyChanged;

    public TerminalSession(TerminalShell shell = TerminalShell.PowerShell, string? workingDirectory = null)
    {
        Shell = shell;
        WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory;
    }

    /// <summary>Chemin de Git Bash (installé avec Git pour Windows), ou null.</summary>
    public static string? FindGitBash()
    {
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "bash.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git", "bin", "bash.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Git", "bin", "bash.exe"),
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    // =====================================================================
    // Démarrage / arrêt
    // =====================================================================

    public void Start()
    {
        lock (_gate)
        {
            if (_proc is { HasExited: false }) return;
            UiContext ??= SynchronizationContext.Current;

            if (!Directory.Exists(WorkingDirectory))
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var utf8 = new UTF8Encoding(false);
            var psi = new ProcessStartInfo(ShellExecutable())
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

            string[] arguments = Shell switch
            {
                TerminalShell.PowerShell => ["-NoLogo", "-NoProfile", "-NoExit", "-Command", "-"],
                TerminalShell.Cmd => ["/Q", "/V:ON", "/K"], // /V:ON : !CD! lu au moment de l'exécution
                _ => ["--login", "-s"],
            };
            foreach (var a in arguments) psi.ArgumentList.Add(a);

            ConfigureEnvironment(psi);

            var proc = Process.Start(psi) ?? throw new InvalidOperationException("Impossible de démarrer le shell.");
            proc.StandardInput.AutoFlush = true;

            var pump = new OutputPump(b => OutputReceived?.Invoke(this, b), UiContext);
            _proc = proc;
            _pump = pump;

            var outT = ReadStdOutAsync(proc.StandardOutput, pump);
            var errT = ReadChunksAsync(proc.StandardError, OutputKind.StdErr, pump);
            _ = MonitorAsync(proc, pump, outT, errT);

            // Sortie en UTF-8 (accents), puis premier marqueur pour afficher l'invite
            if (Shell == TerminalShell.PowerShell)
                proc.StandardInput.WriteLine("[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)");
            else if (Shell == TerminalShell.Cmd)
                proc.StandardInput.WriteLine("chcp 65001 >nul");

            _startup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            SetBusy(true);
            proc.StandardInput.WriteLine(MarkerCommand());
        }
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
        SetBusy(false);
        if (proc is null) return;
        try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }
    }

    public void Dispose() => Stop();

    // =====================================================================
    // Commandes
    // =====================================================================

    /// <summary>
    /// Envoie une ligne : si aucune commande n'est en cours, c'est une nouvelle commande
    /// (avec son marqueur de fin) ; sinon, c'est une réponse au programme en cours (input()...).
    /// </summary>
    public async Task SendCommandAsync(string command)
    {
        // Shell qui démarre : on attend sa première invite (sinon la commande serait prise pour du texte de programme)
        Task startup;
        lock (_gate) startup = _startup.Task;
        if (!startup.IsCompleted)
        {
            try { await startup.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (TimeoutException) { }
        }

        Process? proc; OutputPump? pump; bool busy;
        lock (_gate) { proc = _proc; pump = _pump; busy = _busy; }
        if (proc is null || proc.HasExited)
            throw new InvalidOperationException("Le terminal n'est pas démarré.");

        // Commande en cours : le texte est destiné au programme
        if (busy)
        {
            pump?.Write(new(OutputKind.Info, command + "\n"));
            await WriteLineAsync(proc, command);
            return;
        }

        command = command.TrimEnd();
        if (command.Length == 0)
        {
            await WriteLineAsync(proc, MarkerCommand()); // simple Entrée : nouvelle invite
            SetBusy(true);
            return;
        }

        History.Add(command);
        SetBusy(true);

        // Commande et marqueur envoyés ensemble : le programme lancé ne peut pas « lire » le marqueur
        // à la place de ce que vous taperez ensuite (input()...).
        string line = AdaptForPipe(command);
        string payload = Shell switch
        {
            TerminalShell.PowerShell => line + "\n" + MarkerCommand(), // une seule écriture, lue d'un bloc
            TerminalShell.Cmd => line + " & " + MarkerCommand(),
            _ => line + " ; " + MarkerCommand(),
        };

        await WriteLineAsync(proc, payload);
    }

    /// <summary>
    /// Ctrl+C : arrête le programme lancé par la commande en cours (le shell reste ouvert).
    /// Retourne false s'il n'y avait aucun programme à arrêter.
    /// </summary>
    public bool Interrupt()
    {
        Process? proc;
        lock (_gate) proc = _proc;
        if (proc is null || proc.HasExited) return false;

        bool killed = false;
        foreach (int childId in ProcessTree.ChildrenOf(proc.Id))
        {
            try
            {
                using var child = Process.GetProcessById(childId);

                // conhost : la console du shell lui-même, à ne pas arrêter
                if (child.ProcessName.Equals("conhost", StringComparison.OrdinalIgnoreCase))
                    continue;

                child.Kill(entireProcessTree: true);
                killed = true;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
            {
                // déjà terminé
            }
        }

        return killed;
    }

    private async Task WriteLineAsync(Process proc, string line)
    {
        await _writeLock.WaitAsync();
        try { await proc.StandardInput.WriteLineAsync(line); }
        catch (IOException) { }
        finally { _writeLock.Release(); }
    }

    /// <summary>
    /// Sans vrai terminal, certains programmes attendent une fin de fichier au lieu d'afficher
    /// leur invite : on les lance en mode interactif (python -i, node -i).
    /// </summary>
    private static string AdaptForPipe(string command) => command.Trim() switch
    {
        "python" or "python3" or "py" => command.Trim() + " -i -u",
        "node" => "node -i",
        _ => command,
    };

    private string MarkerCommand() => Shell switch
    {
        TerminalShell.PowerShell =>
            "$__keyceOk = $?; [Console]::Out.WriteLine('" + Marker + "' + $PWD.ProviderPath + '__' + $(if ($__keyceOk) { 0 } else { 1 })); [Console]::Out.Flush()",
        TerminalShell.Cmd =>
            "echo " + Marker + "!CD!__!ERRORLEVEL!",
        _ =>
            "__keyce_ok=$?; printf '" + Marker + "%s__%s\\n' \"$(pwd -W)\" \"$__keyce_ok\"",
    };

    private string ShellExecutable() => Shell switch
    {
        TerminalShell.PowerShell => "powershell.exe",
        TerminalShell.Cmd => "cmd.exe",
        _ => FindGitBash() ?? throw new InvalidOperationException(
            "Git Bash est introuvable : installez Git pour Windows (git-scm.com) ou choisissez PowerShell."),
    };

    /// <summary>Réglages pour que git, python... fonctionnent bien sans vrai terminal.</summary>
    private void ConfigureEnvironment(ProcessStartInfo psi)
    {
        var env = psi.Environment;

        // Git : couleurs (affichées par la console), pas de pager, éditeur visible pour « git commit » sans -m
        env["GIT_CONFIG_COUNT"] = "1";
        env["GIT_CONFIG_KEY_0"] = "color.ui";
        env["GIT_CONFIG_VALUE_0"] = "always";
        env["GIT_PAGER"] = "cat";
        env["PAGER"] = "cat";
        env["GIT_EDITOR"] = "notepad";

        // Python : sortie immédiate et en UTF-8 (input("Nom : ") s'affiche tout de suite)
        env["PYTHONUNBUFFERED"] = "1";
        env["PYTHONIOENCODING"] = "utf-8";

        // Git Bash : rester dans le dossier demandé au lieu d'aller dans le dossier personnel
        if (Shell == TerminalShell.GitBash)
            env["CHERE_INVOKING"] = "1";
    }

    // =====================================================================
    // Lecture des sorties
    // =====================================================================

    /// <summary>Sortie standard : on retire les marqueurs (fin de commande) avant affichage.</summary>
    private async Task ReadStdOutAsync(StreamReader reader, OutputPump pump)
    {
        var pending = new StringBuilder(); // texte lu mais pas encore affiché (marqueur peut-être coupé)
        var buffer = new char[4096];
        try
        {
            int n;
            while ((n = await reader.ReadAsync(buffer)) > 0)
            {
                pending.Append(buffer, 0, n);
                ProcessPendingOutput(pending, pump);
            }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }

        if (pending.Length > 0)
            pump.Write(new(OutputKind.StdOut, pending.ToString()));
    }

    private void ProcessPendingOutput(StringBuilder pending, OutputPump pump)
    {
        string text = pending.ToString();
        int position = 0;

        foreach (Match match in MarkerPattern.Matches(text))
        {
            if (match.Index > position)
                pump.Write(new(OutputKind.StdOut, text[position..match.Index]));

            position = match.Index + match.Length;
            string directory = match.Groups["dir"].Value.Trim();
            bool success = match.Groups["ok"].Value == "0";
            OnPrompt(directory, success);
        }

        string rest = text[position..];

        // Début possible d'un marqueur coupé en deux : on le garde pour la suite
        int keep = PartialMarkerLength(rest);
        if (rest.Length - keep > 0)
            pump.Write(new(OutputKind.StdOut, rest[..(rest.Length - keep)]));

        pending.Clear();
        pending.Append(rest[(rest.Length - keep)..]);
    }

    /// <summary>Nombre de caractères à garder en fin de texte (marqueur incomplet).</summary>
    private static int PartialMarkerLength(string text)
    {
        int full = text.LastIndexOf(Marker, StringComparison.Ordinal);
        if (full >= 0 && text.IndexOf('\n', full) < 0)
            return text.Length - full; // marqueur commencé, pas encore terminé

        for (int length = Math.Min(Marker.Length - 1, text.Length); length >= 3; length--)
        {
            if (text.EndsWith(Marker[..length], StringComparison.Ordinal))
                return length;
        }

        return 0;
    }

    private void OnPrompt(string directory, bool success)
    {
        if (directory.Length > 0)
        {
            // Git Bash donne C:/Users/... : on remet des « \ »
            if (Shell == TerminalShell.GitBash)
                directory = directory.Replace('/', '\\');

            WorkingDirectory = directory;
        }

        SetBusy(false);
        _startup.TrySetResult();
        var prompt = new TerminalPrompt(WorkingDirectory, success);
        UiDispatch.Post(UiContext, () => PromptReady?.Invoke(this, prompt));
    }

    private void SetBusy(bool busy)
    {
        bool changed;
        lock (_gate)
        {
            changed = _busy != busy;
            _busy = busy;
        }

        if (changed)
            UiDispatch.Post(UiContext, () => BusyChanged?.Invoke(this, EventArgs.Empty));
    }

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

            bool current;
            lock (_gate) current = _proc is null || ReferenceEquals(_proc, proc);
            if (current) SetBusy(false); // un ancien shell (après Restart) ne touche pas au nouveau
            UiDispatch.Post(UiContext, () => Exited?.Invoke(this, code));
        }
        catch (Exception) { /* process déjà libéré */ }
        finally { proc.Dispose(); }
    }
}
