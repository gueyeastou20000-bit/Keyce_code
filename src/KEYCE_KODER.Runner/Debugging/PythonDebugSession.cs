using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KEYCE_KODER.Core.Execution;

namespace KEYCE_KODER.Runner.Debugging;

/// <summary>
/// Session de débogage Python, écrite entièrement en C# côté IDE.
/// Le programme est lancé avec le moteur keyce_debugger.py (module bdb de Python, rien à installer) ;
/// les deux dialoguent en JSON sur un socket local. Les sorties du programme (print, erreurs)
/// et l'entrée clavier (input) passent par les flux standard, comme avec Run.
/// Les événements sont levés sur le thread de l'interface (UiContext).
/// </summary>
public sealed class PythonDebugSession
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private Process? _process;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private int _seq;
    private bool _stopRequested;
    private DebugState _state = DebugState.Idle;

    /// <summary>Contexte sur lequel les événements sont levés (thread de l'interface).</summary>
    public SynchronizationContext? UiContext { get; set; } = SynchronizationContext.Current;

    public DebugState State => _state;

    /// <summary>Sortie du programme (print, erreurs), par lots.</summary>
    public event EventHandler<IReadOnlyList<OutputLine>>? OutputReceived;

    /// <summary>Le programme s'est arrêté (point d'arrêt, pas à pas, pause, exception).</summary>
    public event EventHandler<DebugStoppedEventArgs>? Stopped;

    /// <summary>Le programme repart (continuer, pas à pas).</summary>
    public event EventHandler? Continued;

    /// <summary>Le programme est terminé (code de sortie ; null s'il a été arrêté).</summary>
    public event EventHandler<int?>? Terminated;

    public event EventHandler? StateChanged;

    /// <summary>Ce fichier peut-il être débogué ? (Python pour l'instant)</summary>
    public static bool Supports(string path) =>
        string.Equals(Path.GetExtension(path), ".py", StringComparison.OrdinalIgnoreCase);

    // =====================================================================
    // Démarrage / arrêt
    // =====================================================================

    /// <summary>Lance le script sous le débogueur avec ses points d'arrêt (clé = chemin du fichier).</summary>
    public async Task StartAsync(
        string scriptPath,
        IReadOnlyDictionary<string, IReadOnlyList<SourceBreakpoint>> breakpoints)
    {
        if (_state != DebugState.Idle)
            throw new InvalidOperationException("Une session de débogage est déjà en cours.");

        UiContext ??= SynchronizationContext.Current;
        _stopRequested = false;
        SetState(DebugState.Starting);

        try
        {
            await LaunchAsync(Path.GetFullPath(scriptPath));

            foreach ((string file, IReadOnlyList<SourceBreakpoint> list) in breakpoints)
                await SetBreakpointsAsync(file, list);

            // Les points d'arrêt sont posés : le programme peut démarrer
            await RequestAsync(new JsonObject { ["cmd"] = "configurationDone" });

            if (_state == DebugState.Starting)
                SetState(DebugState.Running);
        }
        catch
        {
            if (_process is null)
                SetState(DebugState.Idle); // rien n'a été lancé
            else
                Stop();                    // la fin du processus terminera la session

            throw;
        }
    }

    /// <summary>Arrête le programme (et la session).</summary>
    public void Stop()
    {
        _stopRequested = true;
        Process? process = _process;
        if (process is null)
            return;

        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }
    }

    private async Task LaunchAsync(string script)
    {
        LanguageProfile profile = LanguageProfiles.ForFile(script)
            ?? throw new InvalidOperationException("Seuls les fichiers Python (.py) peuvent être débogués.");

        ResolvedTool tool = await ToolLocator.LocateAsync(profile.Tool)
            ?? throw new InvalidOperationException($"{profile.Tool.DisplayName} introuvable. {profile.Tool.InstallHint}");

        // Le moteur Python est écrit dans le dossier temporaire à chaque lancement
        string folder = Path.Combine(Path.GetTempPath(), "KEYCE_KODER", "debug");
        Directory.CreateDirectory(folder);
        string helper = Path.Combine(folder, PythonDebugHelper.FileName);
        await File.WriteAllTextAsync(helper, PythonDebugHelper.Script, Utf8);

        // Socket local : le moteur se connecte à ce port
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var startInfo = new ProcessStartInfo(tool.FileName)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = Utf8,
                StandardOutputEncoding = Utf8,
                StandardErrorEncoding = Utf8,
                WorkingDirectory = Path.GetDirectoryName(script)!
            };

            foreach (string argument in tool.PrefixArgs)
                startInfo.ArgumentList.Add(argument);

            startInfo.ArgumentList.Add("-u");
            startInfo.ArgumentList.Add(helper);
            startInfo.ArgumentList.Add(port.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add(script);

            startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
            startInfo.Environment["PYTHONUNBUFFERED"] = "1";

            var process = new Process { StartInfo = startInfo };
            process.Start();
            _process = process;

            var pump = new OutputPump(batch => OutputReceived?.Invoke(this, batch), UiContext);
            Task outTask = ReadChunksAsync(process.StandardOutput, OutputKind.StdOut, pump);
            Task errTask = ReadChunksAsync(process.StandardError, OutputKind.StdErr, pump);
            _ = WatchExitAsync(process, outTask, errTask, pump);

            // On attend que le moteur se connecte (ou qu'il s'arrête sur une erreur)
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            Task<TcpClient> accept = listener.AcceptTcpClientAsync(timeout.Token).AsTask();
            Task exited = process.WaitForExitAsync(timeout.Token);

            Task first = await Task.WhenAny(accept, exited);
            if (first != accept || !accept.IsCompletedSuccessfully)
            {
                throw new InvalidOperationException(timeout.IsCancellationRequested
                    ? "Le débogueur Python ne répond pas (délai dépassé)."
                    : "Le débogueur Python s'est arrêté au démarrage (voir la console de débogage).");
            }

            _client = accept.Result;
            _stream = _client.GetStream();
        }
        finally
        {
            listener.Stop();
        }

        _ = ReadMessagesAsync(_stream);
    }

    private async Task WatchExitAsync(Process process, Task outTask, Task errTask, OutputPump pump)
    {
        try
        {
            await process.WaitForExitAsync();
        }
        catch (InvalidOperationException) { }

        // Dernières lignes de sortie, puis fin de la session
        await Task.WhenAny(Task.WhenAll(outTask, errTask), Task.Delay(2000));

        try { await pump.CompleteAsync(); }
        catch (Exception) { }

        int? exitCode = null;
        try { exitCode = process.ExitCode; }
        catch (InvalidOperationException) { }

        UiDispatch.Post(UiContext, () => Finish(exitCode));
    }

    private void Finish(int? exitCode)
    {
        if (_state == DebugState.Idle)
            return;

        foreach (TaskCompletionSource<JsonElement> pending in _pending.Values)
            pending.TrySetException(new InvalidOperationException("Le programme est terminé."));
        _pending.Clear();

        try { _client?.Dispose(); }
        catch (Exception) { }

        _client = null;
        _stream = null;
        _process?.Dispose();
        _process = null;

        SetState(DebugState.Idle);
        Terminated?.Invoke(this, _stopRequested ? null : exitCode);
    }

    // =====================================================================
    // Commandes
    // =====================================================================

    public Task ContinueAsync() => ResumeAsync("continue");

    /// <summary>Pas à pas principal (F10) : exécute la ligne sans entrer dans les fonctions.</summary>
    public Task StepOverAsync() => ResumeAsync("next");

    /// <summary>Pas à pas détaillé (F11) : entre dans les fonctions appelées.</summary>
    public Task StepIntoAsync() => ResumeAsync("stepIn");

    /// <summary>Pas à pas sortant (Maj+F11) : termine la fonction en cours.</summary>
    public Task StepOutAsync() => ResumeAsync("stepOut");

    /// <summary>Met en pause le programme en cours d'exécution.</summary>
    public Task PauseAsync() =>
        _state == DebugState.Running ? SendAsync(new JsonObject { ["cmd"] = "pause" }) : Task.CompletedTask;

    private async Task ResumeAsync(string command)
    {
        if (_state != DebugState.Paused)
            return;

        SetState(DebugState.Running);
        Continued?.Invoke(this, EventArgs.Empty);
        await SendAsync(new JsonObject { ["cmd"] = command });
    }

    /// <summary>Remplace les points d'arrêt d'un fichier. Retourne les lignes acceptées par Python.</summary>
    public async Task<IReadOnlyList<int>> SetBreakpointsAsync(string file, IReadOnlyList<SourceBreakpoint> breakpoints)
    {
        if (_stream is null)
            return [];

        var list = new JsonArray();
        foreach (SourceBreakpoint breakpoint in breakpoints)
        {
            var item = new JsonObject { ["line"] = breakpoint.Line };
            if (!string.IsNullOrWhiteSpace(breakpoint.Condition))
                item["condition"] = breakpoint.Condition;
            list.Add(item);
        }

        JsonElement body = await RequestAsync(new JsonObject
        {
            ["cmd"] = "setBreakpoints",
            ["file"] = Path.GetFullPath(file),
            ["breakpoints"] = list
        });

        var lines = new List<int>();
        if (body.ValueKind == JsonValueKind.Object
            && body.TryGetProperty("lines", out JsonElement array)
            && array.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement line in array.EnumerateArray())
                lines.Add(line.GetInt32());
        }

        return lines;
    }

    /// <summary>Variables locales (et globales) d'une ligne de la pile.</summary>
    public async Task<DebugScopes> GetScopesAsync(int frameId)
    {
        JsonElement body = await RequestAsync(new JsonObject { ["cmd"] = "scopes", ["frame"] = frameId });
        ThrowIfError(body);

        IReadOnlyList<DebugVariable> locals = ParseVariables(body, "locals");
        IReadOnlyList<DebugVariable>? globals = body.TryGetProperty("globals", out _)
            ? ParseVariables(body, "globals")
            : null;

        return new DebugScopes(locals, globals);
    }

    /// <summary>Contenu d'une variable dépliable (liste, dictionnaire, objet).</summary>
    public async Task<IReadOnlyList<DebugVariable>> GetVariablesAsync(int reference)
    {
        JsonElement body = await RequestAsync(new JsonObject { ["cmd"] = "variables", ["ref"] = reference });
        ThrowIfError(body);
        return ParseVariables(body, "variables");
    }

    /// <summary>Évalue une expression (ou exécute une instruction) dans une ligne de la pile.</summary>
    public async Task<DebugEvaluation> EvaluateAsync(string expression, int frameId)
    {
        JsonElement body = await RequestAsync(new JsonObject
        {
            ["cmd"] = "evaluate",
            ["expression"] = expression,
            ["frame"] = frameId
        });

        string? error = ErrorOf(body);
        return error is not null
            ? new DebugEvaluation(string.Empty, string.Empty, 0, error)
            : new DebugEvaluation(Text(body, "result"), Text(body, "type"), Number(body, "ref"), null);
    }

    /// <summary>Envoie une ligne au programme (input()).</summary>
    public void SendInput(string text)
    {
        Process? process = _process;
        if (process is null)
            return;

        try
        {
            process.StandardInput.WriteLine(text);
            process.StandardInput.Flush();
        }
        catch (IOException) { }
        catch (InvalidOperationException) { }
    }

    // =====================================================================
    // Protocole JSON (une ligne par message)
    // =====================================================================

    private async Task<JsonElement> RequestAsync(JsonObject payload)
    {
        int seq = Interlocked.Increment(ref _seq);
        payload["seq"] = seq;

        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[seq] = completion;

        try
        {
            await SendAsync(payload);

            using var timeout = new CancellationTokenSource(RequestTimeout);
            using (timeout.Token.Register(() =>
                       completion.TrySetException(new TimeoutException("Le débogueur ne répond pas."))))
            {
                return await completion.Task;
            }
        }
        finally
        {
            _pending.TryRemove(seq, out _);
        }
    }

    private async Task SendAsync(JsonObject payload)
    {
        NetworkStream stream = _stream ?? throw new InvalidOperationException("Aucune session de débogage.");
        byte[] data = Utf8.GetBytes(payload.ToJsonString() + "\n");

        await _writeLock.WaitAsync();
        try
        {
            await stream.WriteAsync(data);
            await stream.FlushAsync();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            throw new InvalidOperationException("Le programme est terminé.", ex);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadMessagesAsync(NetworkStream stream)
    {
        try
        {
            using var reader = new StreamReader(stream, Utf8, detectEncodingFromByteOrderMarks: false, bufferSize: -1, leaveOpen: true);

            while (await reader.ReadLineAsync() is { } line)
            {
                if (line.Length == 0)
                    continue;

                JsonElement message;
                try
                {
                    using JsonDocument document = JsonDocument.Parse(line);
                    message = document.RootElement.Clone();
                }
                catch (JsonException)
                {
                    continue;
                }

                Handle(message);
            }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    private void Handle(JsonElement message)
    {
        string type = Text(message, "type");

        if (type == "response")
        {
            int seq = Number(message, "seq");
            if (_pending.TryRemove(seq, out TaskCompletionSource<JsonElement>? completion))
                completion.TrySetResult(message.TryGetProperty("body", out JsonElement body) ? body : default);
            return;
        }

        if (type != "event")
            return;

        switch (Text(message, "event"))
        {
            case "stopped":
                DebugStoppedEventArgs stopped = ParseStopped(message);
                UiDispatch.Post(UiContext, () =>
                {
                    if (_state == DebugState.Idle)
                        return;

                    SetState(DebugState.Paused);
                    Stopped?.Invoke(this, stopped);
                });
                break;

            case "continued":
                UiDispatch.Post(UiContext, () =>
                {
                    if (_state == DebugState.Paused)
                        SetState(DebugState.Running);
                });
                break;

            // « terminated » : la fin du processus suffit (voir WatchExitAsync)
        }
    }

    private static DebugStoppedEventArgs ParseStopped(JsonElement message)
    {
        var frames = new List<DebugFrame>();

        if (message.TryGetProperty("frames", out JsonElement array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement frame in array.EnumerateArray())
            {
                frames.Add(new DebugFrame(
                    Number(frame, "id"),
                    Text(frame, "name"),
                    Text(frame, "file"),
                    Number(frame, "line")));
            }
        }

        string description = Text(message, "description");
        return new DebugStoppedEventArgs(
            Text(message, "reason"),
            description.Length == 0 ? null : description,
            frames);
    }

    private static IReadOnlyList<DebugVariable> ParseVariables(JsonElement body, string property)
    {
        var variables = new List<DebugVariable>();

        if (body.ValueKind == JsonValueKind.Object
            && body.TryGetProperty(property, out JsonElement array)
            && array.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in array.EnumerateArray())
            {
                variables.Add(new DebugVariable(
                    Text(item, "name"),
                    Text(item, "value"),
                    Text(item, "type"),
                    Number(item, "ref")));
            }
        }

        return variables;
    }

    private static string? ErrorOf(JsonElement body) =>
        body.ValueKind == JsonValueKind.Object && body.TryGetProperty("error", out JsonElement error)
            ? error.GetString()
            : null;

    private static void ThrowIfError(JsonElement body)
    {
        if (ErrorOf(body) is { } error)
            throw new InvalidOperationException(error);
    }

    private static string Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int Number(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;

    // =====================================================================
    // Divers
    // =====================================================================

    private void SetState(DebugState state)
    {
        if (_state == state)
            return;

        _state = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    // Lecture par blocs : un input("Nom : ") n'a pas de retour à la ligne
    private static async Task ReadChunksAsync(StreamReader reader, OutputKind kind, OutputPump pump)
    {
        var buffer = new char[4096];
        try
        {
            int count;
            while ((count = await reader.ReadAsync(buffer)) > 0)
                pump.Write(new OutputLine(kind, new string(buffer, 0, count)));
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }
}
