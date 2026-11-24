using System.IO;
using System.Windows;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using KEYCE_KODER.App.Actions;
using KEYCE_KODER.App.Tabs;
using KEYCE_KODER.Editor.Controls;
using KEYCE_KODER.Runner.Debugging;

namespace KEYCE_KODER.App.Debugging;

/// <summary>
/// Chef d'orchestre du débogage : relie la session Python, les éditeurs (points d'arrêt, ligne courante),
/// la vue « Exécuter et déboguer » et la console de débogage.
/// </summary>
public sealed class DebugController
{
    private static readonly string TempRoot = Path.Combine(Path.GetTempPath(), "KEYCE_KODER");

    private readonly Window _owner;
    private readonly TabManager _tabs;
    private readonly FileActions _files;
    private readonly BreakpointStore _store;
    private readonly DebugView _view;
    private readonly DebugConsole _console;
    private readonly Func<Task<string?>> _prepareScript;
    private readonly Action _showDebugUi;
    private readonly DebugLocation _location = new();
    private readonly Dictionary<CodeEditor, BreakpointMargin> _margins = new();

    private PythonDebugSession? _session;
    private IReadOnlyList<DebugFrame> _frames = [];
    private int _currentFrame;
    private string? _lastScript;
    private bool _restartRequested;

    /// <param name="prepareScript">Enregistre le fichier actif et renvoie le chemin à exécuter (null = annuler).</param>
    /// <param name="showDebugUi">Affiche la vue Débogage et la console de débogage.</param>
    public DebugController(
        Window owner,
        TabManager tabs,
        FileActions files,
        BreakpointStore store,
        DebugView view,
        DebugConsole console,
        Func<Task<string?>> prepareScript,
        Action showDebugUi)
    {
        _owner = owner;
        _tabs = tabs;
        _files = files;
        _store = store;
        _view = view;
        _console = console;
        _prepareScript = prepareScript;
        _showDebugUi = showDebugUi;

        _view.AttachStore(store);
        _view.StartRequested += () => _ = StartAsync();
        _view.ContinueRequested += Continue;
        _view.PauseRequested += Pause;
        _view.StepOverRequested += StepOver;
        _view.StepIntoRequested += StepInto;
        _view.StepOutRequested += StepOut;
        _view.RestartRequested += () => _ = RestartAsync();
        _view.StopRequested += Stop;
        _view.FrameSelected += frame => _ = SelectFrameAsync(frame.Id);
        _view.WatchesChanged += () => _ = RefreshWatchesAsync();
        _view.BreakpointActivated += breakpoint => _ = RevealAsync(breakpoint.FilePath, breakpoint.Line);
        _view.LoadChildren = LoadChildrenAsync;

        _console.Submitted += text => _ = OnConsoleSubmittedAsync(text);

        // Point d'arrêt ajouté / retiré pendant le débogage : on prévient Python tout de suite
        _store.Changed += file => _ = SyncBreakpointsAsync(file);

        // La ligne courante a changé : on redessine les éditeurs
        _location.Changed += () =>
        {
            foreach (CodeEditor editor in _margins.Keys)
                editor.InnerEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        };
    }

    /// <summary>Une session de débogage est en cours.</summary>
    public bool IsActive => _session is not null;

    public DebugState State => _session?.State ?? DebugState.Idle;

    // =====================================================================
    // Éditeurs : marge des points d'arrêt + surlignage de la ligne courante
    // =====================================================================

    /// <summary>Ajoute la marge de débogage aux éditeurs qui n'en ont pas encore (à appeler quand un onglet s'ouvre).</summary>
    public void AttachEditors()
    {
        foreach (EditorTab tab in _tabs.Tabs)
        {
            CodeEditor editor = tab.Editor;
            if (_margins.ContainsKey(editor))
                continue;

            EditorTab owner = tab;
            Func<string?> filePath = () => owner.Document.FilePath;

            var margin = new BreakpointMargin(_store, _location, filePath);
            editor.InnerEditor.TextArea.LeftMargins.Insert(0, margin);
            editor.InnerEditor.TextArea.TextView.BackgroundRenderers.Add(new DebugLineMarker(_location, filePath));

            _margins[editor] = margin;
        }

        // Onglets fermés : on oublie leurs éditeurs
        foreach (CodeEditor closed in _margins.Keys.Where(e => _tabs.Tabs.All(t => t.Editor != e)).ToList())
            _margins.Remove(closed);
    }

    /// <summary>F9 : pose / retire un point d'arrêt sur la ligne du curseur.</summary>
    public void ToggleBreakpointAtCaret()
    {
        EditorTab? tab = _tabs.ActiveTab;
        if (tab is null)
            return;

        AttachEditors();
        int line = tab.Editor.InnerEditor.TextArea.Caret.Line;
        _margins[tab.Editor].ToggleAtCaret(line);
    }

    // =====================================================================
    // Démarrer / arrêter
    // =====================================================================

    /// <summary>F5 : démarre le débogage du fichier actif (ou continue s'il est en pause).</summary>
    public async Task StartAsync()
    {
        if (_session is not null)
        {
            Continue();
            return;
        }

        string? script = await _prepareScript();
        if (script is null)
            return;

        if (!PythonDebugSession.Supports(script))
        {
            MessageBox.Show(_owner,
                "Le débogueur prend en charge Python (.py) pour l'instant.\n"
                + "Pour ce fichier, utilisez « ▶ Run » (Ctrl+F5).",
                "Débogage", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await StartSessionAsync(script);
    }

    private async Task StartSessionAsync(string script)
    {
        _lastScript = script;

        var session = new PythonDebugSession { UiContext = SynchronizationContext.Current };
        session.OutputReceived += (_, lines) => _console.Append(lines);
        session.Stopped += (_, e) => _ = OnStoppedAsync(session, e);
        session.Continued += (_, _) => OnContinued(session);
        session.StateChanged += (_, _) => UpdateState();
        session.Terminated += (_, exitCode) => OnTerminated(session, exitCode);
        _session = session;

        _console.Clear();
        _console.WriteInfo($"Débogage de {Path.GetFileName(script)} — F5 continuer, F10 pas à pas, F11 entrer, Maj+F5 arrêter.");
        _showDebugUi();
        UpdateState();

        try
        {
            await session.StartAsync(script, _store.ToSourceByFile());
        }
        catch (Exception ex)
        {
            _console.WriteError($"Impossible de démarrer le débogage : {ex.Message}");
            _view.SetStatus(ex.Message, error: true);

            if (session.State == DebugState.Idle && ReferenceEquals(_session, session))
            {
                _session = null;
                UpdateState();
            }
        }
    }

    /// <summary>Maj+F5 : arrête le programme débogué.</summary>
    public void Stop() => _session?.Stop();

    /// <summary>Ctrl+Maj+F5 : relance le débogage depuis le début.</summary>
    public async Task RestartAsync()
    {
        if (_session is null)
        {
            await StartAsync();
            return;
        }

        _restartRequested = true;
        _session.Stop(); // la relance se fait à la fin de la session (OnTerminated)
    }

    // =====================================================================
    // Contrôle du programme
    // =====================================================================

    public void Continue() => Send(session => session.ContinueAsync());

    public void Pause() => Send(session => session.PauseAsync());

    public void StepOver() => Send(session => session.StepOverAsync());

    public void StepInto() => Send(session => session.StepIntoAsync());

    public void StepOut() => Send(session => session.StepOutAsync());

    private async void Send(Func<PythonDebugSession, Task> command)
    {
        if (_session is not { } session)
            return;

        try
        {
            await command(session);
        }
        catch (Exception ex)
        {
            _console.WriteError(ex.Message);
        }
    }

    // =====================================================================
    // Événements de la session
    // =====================================================================

    private async Task OnStoppedAsync(PythonDebugSession session, DebugStoppedEventArgs e)
    {
        if (!ReferenceEquals(session, _session))
            return;

        _frames = e.Frames;
        _currentFrame = 0;
        _view.ShowFrames(e.Frames);

        DebugFrame? top = e.Frames.Count > 0 ? e.Frames[0] : null;
        string where = top is null ? "" : $"{top.Name}, ligne {top.Line}";

        if (e.Reason == "exception")
        {
            _console.WriteError($"Exception non gérée : {e.Description}");
            _console.WriteInfo("Examinez les variables, puis F5 (ou ■) pour terminer.");
            _view.SetStatus($"Exception : {e.Description}  ({where})", error: true);
        }
        else
        {
            string reason = e.Reason switch
            {
                "breakpoint" => "point d'arrêt",
                "step" => "pas à pas",
                "pause" => "pause",
                _ => e.Reason
            };
            _view.SetStatus($"En pause ({reason}) — {where}");
        }

        if (top is not null)
            await ShowFrameLocationAsync(top, isTopFrame: true);

        await LoadScopesAsync(0);
        await RefreshWatchesAsync();

        _owner.Activate();
    }

    private void OnContinued(PythonDebugSession session)
    {
        if (!ReferenceEquals(session, _session))
            return;

        _frames = [];
        _location.Clear();
        _view.ClearFrames();
        _view.ClearVariables("En cours d'exécution…");
        _view.RenderWatches(null);
        UpdateState();
    }

    private void OnTerminated(PythonDebugSession session, int? exitCode)
    {
        if (!ReferenceEquals(session, _session))
            return;

        _session = null;
        _frames = [];
        _location.Clear();
        _view.ClearFrames();
        _view.ClearVariables("Pas de session de débogage.");
        _view.RenderWatches(null);

        _console.WriteInfo(exitCode is null
            ? "\n■ Débogage arrêté."
            : $"\nProgramme terminé (code {exitCode}).");

        UpdateState();

        if (_restartRequested)
        {
            _restartRequested = false;
            _ = RestartSessionAsync();
        }
    }

    private async Task RestartSessionAsync()
    {
        // Si le fichier débogué est l'onglet actif, on l'enregistre avant de relancer
        string? script = _tabs.ActiveTab?.Document.FilePath is { } active
                         && _lastScript is not null
                         && string.Equals(Path.GetFullPath(active), _lastScript, StringComparison.OrdinalIgnoreCase)
            ? await _prepareScript()
            : _lastScript;

        if (script is not null)
            await StartSessionAsync(script);
    }

    private void UpdateState()
    {
        DebugState state = State;
        _view.SetState(state);
        _console.SetMode(paused: state == DebugState.Paused, running: state is DebugState.Running or DebugState.Starting);
    }

    // =====================================================================
    // Pile, variables, espions
    // =====================================================================

    private async Task SelectFrameAsync(int frameId)
    {
        DebugFrame? frame = _frames.FirstOrDefault(f => f.Id == frameId);
        if (frame is null || _session is not { State: DebugState.Paused })
            return;

        _currentFrame = frameId;
        await ShowFrameLocationAsync(frame, isTopFrame: frameId == 0);
        await LoadScopesAsync(frameId);
        await RefreshWatchesAsync();
    }

    private async Task LoadScopesAsync(int frameId)
    {
        if (_session is not { State: DebugState.Paused } session)
            return;

        try
        {
            _view.ShowScopes(await session.GetScopesAsync(frameId));
        }
        catch (Exception ex)
        {
            _view.ClearVariables(ex.Message);
        }
    }

    private Task<IReadOnlyList<DebugVariable>> LoadChildrenAsync(int reference) =>
        _session is { State: DebugState.Paused } session
            ? session.GetVariablesAsync(reference)
            : Task.FromResult<IReadOnlyList<DebugVariable>>([]);

    private async Task RefreshWatchesAsync()
    {
        if (_session is not { State: DebugState.Paused } session)
        {
            _view.RenderWatches(null);
            return;
        }

        var results = new List<(string Text, bool IsError)>();

        foreach (string expression in _view.Watches.ToList())
        {
            try
            {
                DebugEvaluation evaluation = await session.EvaluateAsync(expression, _currentFrame);
                results.Add(evaluation.Failed ? (evaluation.Error!, true) : (evaluation.Result, false));
            }
            catch (Exception ex)
            {
                results.Add((ex.Message, true));
            }
        }

        _view.RenderWatches(results);
    }

    // =====================================================================
    // Console de débogage
    // =====================================================================

    private async Task OnConsoleSubmittedAsync(string text)
    {
        if (_session is not { } session)
        {
            _console.WriteInfo("Démarrez le débogage (F5) pour utiliser la console.");
            return;
        }

        // Programme en cours : le texte est envoyé à input()
        if (session.State != DebugState.Paused)
        {
            _console.WriteInfo(text);
            session.SendInput(text);
            return;
        }

        // En pause : on évalue l'expression à l'endroit de l'arrêt
        if (text.Trim().Length == 0)
            return;

        _console.WriteInfo("› " + text);

        try
        {
            DebugEvaluation evaluation = await session.EvaluateAsync(text, _currentFrame);

            if (evaluation.Failed)
                _console.WriteError(evaluation.Error!);
            else if (evaluation.Result.Length > 0)
                _console.WriteResult(evaluation.Result);

            // Une instruction (ex. x = 5) a pu changer les variables
            await LoadScopesAsync(_currentFrame);
            await RefreshWatchesAsync();
        }
        catch (Exception ex)
        {
            _console.WriteError(ex.Message);
        }
    }

    // =====================================================================
    // Points d'arrêt pendant le débogage
    // =====================================================================

    private async Task SyncBreakpointsAsync(string file)
    {
        if (_session is not { State: DebugState.Running or DebugState.Paused } session)
            return;

        try
        {
            IReadOnlyList<int> accepted = await session.SetBreakpointsAsync(file, _store.ToSource(file));

            foreach (Breakpoint breakpoint in _store.For(file))
                breakpoint.Verified = !breakpoint.Enabled || accepted.Contains(breakpoint.Line);

            foreach (BreakpointMargin margin in _margins.Values)
                margin.InvalidateVisual();
        }
        catch (Exception ex)
        {
            _console.WriteError($"Points d'arrêt non transmis : {ex.Message}");
        }
    }

    // =====================================================================
    // Aller à une ligne
    // =====================================================================

    private async Task ShowFrameLocationAsync(DebugFrame frame, bool isTopFrame)
    {
        // Document « Sans titre » exécuté depuis une copie temporaire : pas d'onglet à ouvrir
        if (frame.FilePath.StartsWith(TempRoot, StringComparison.OrdinalIgnoreCase))
        {
            _console.WriteInfo($"Arrêt ligne {frame.Line} (enregistrez le fichier pour voir la ligne dans l'éditeur).");
            return;
        }

        if (!File.Exists(frame.FilePath))
            return;

        _location.Set(frame.FilePath, frame.Line, isTopFrame);
        await RevealAsync(frame.FilePath, frame.Line);
    }

    /// <summary>Ouvre le fichier (si besoin), place le curseur sur la ligne et la fait défiler à l'écran.</summary>
    private async Task RevealAsync(string file, int line)
    {
        await _files.OpenPathAsync(file);

        EditorTab? tab = _tabs.FindByPath(file);
        if (tab is null)
            return;

        _tabs.Activate(tab);
        AttachEditors();

        TextEditor editor = tab.Editor.InnerEditor;
        _ = editor.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (line < 1 || line > editor.Document.LineCount)
                return;

            editor.TextArea.Caret.Location = new TextLocation(line, 1);
            editor.ScrollToLine(line);
            editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        }));
    }
}
