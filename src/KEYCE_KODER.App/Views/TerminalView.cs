using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using KEYCE_KODER.Core.Execution;
using KEYCE_KODER.Core.Git;
using KEYCE_KODER.Terminal;

namespace KEYCE_KODER.App.Views;

/// <summary>
/// Terminal intégré (onglet TERMINAL) :
/// - invite avec le dossier courant et la branche Git (● modifications, ↑ à pousser, ↓ à récupérer) ;
/// - couleurs (git status, git diff...), historique ↑/↓, Tab pour compléter (fichiers, commandes git, branches) ;
/// - Ctrl+C arrête la commande en cours, Ctrl+L / cls / clear efface l'écran ;
/// - pendant une commande, le texte tapé est envoyé au programme (input(), Read-Host...).
/// </summary>
public sealed class TerminalView : Grid
{
    private const string Esc = "\u001b";

    private static readonly string[] GitCommands =
    [
        "add", "branch", "checkout", "cherry-pick", "clone", "commit", "config", "diff", "fetch", "init", "log",
        "merge", "mv", "pull", "push", "rebase", "remote", "reset", "restore", "revert", "rm", "show", "stash",
        "status", "switch", "tag"
    ];

    // Commandes git dont l'argument est une branche
    private static readonly HashSet<string> BranchCommands = new(StringComparer.Ordinal)
    {
        "switch", "checkout", "merge", "rebase", "branch", "push", "pull", "diff", "log", "cherry-pick"
    };

    private static readonly ControlTemplate FlatTemplate = (ControlTemplate)XamlReader.Parse(
        """
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         TargetType="Button">
          <Border x:Name="bd" Background="{TemplateBinding Background}" CornerRadius="3" Padding="{TemplateBinding Padding}">
            <ContentPresenter VerticalAlignment="Center"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="bd" Property="Background" Value="#33FFFFFF"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
        """);

    private static readonly Brush FolderBrush = Frozen(41, 184, 219);
    private static readonly Brush BranchBrush = Frozen(214, 112, 214);
    private static readonly Brush DirtyBrush = Frozen(226, 192, 141);
    private static readonly Brush FailedBrush = Frozen(241, 76, 76);

    private readonly TerminalSession _session;
    private readonly Func<string?> _getProjectFolder;
    private readonly GitService _git = new();

    private readonly ConsoleView _view = new();
    private readonly TextBox _input = new();
    private readonly StackPanel _prompt = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _shellLabel = new();
    private readonly TextBlock _folderLabel = new();
    private readonly Button _branchButton = new();
    private readonly TextBlock _busyLabel = new();
    private readonly TextBlock _lastFailed = new();

    private string? _branch;
    private IReadOnlyList<string> _branches = [];
    private int _gitTicket;

    /// <summary>Clic sur la branche Git de l'invite : ouvrir la vue Contrôle de source.</summary>
    public event Action? GitRequested;

    /// <param name="getProjectFolder">Dossier ouvert dans l'explorateur (dossier de départ du terminal).</param>
    public TerminalView(TerminalSession session, Func<string?> getProjectFolder)
    {
        _session = session;
        _getProjectFolder = getProjectFolder;

        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ---------- Invite : PS  dossier  ⎇ branche ●3 ↑1  › ----------
        _shellLabel.Foreground = Theme.Muted;
        _shellLabel.Margin = new Thickness(10, 0, 6, 0);
        _shellLabel.VerticalAlignment = VerticalAlignment.Center;

        _folderLabel.Foreground = FolderBrush;
        _folderLabel.VerticalAlignment = VerticalAlignment.Center;
        _folderLabel.Margin = new Thickness(0, 0, 6, 0);

        _branchButton.Template = FlatTemplate;
        _branchButton.Background = Brushes.Transparent;
        _branchButton.Padding = new Thickness(4, 1, 4, 1);
        _branchButton.Cursor = Cursors.Hand;
        _branchButton.Focusable = false;
        _branchButton.ToolTip = "Ouvrir le Contrôle de source (Git)";
        _branchButton.Visibility = Visibility.Collapsed;
        _branchButton.Click += (_, _) => GitRequested?.Invoke();

        _busyLabel.Foreground = DirtyBrush;
        _busyLabel.VerticalAlignment = VerticalAlignment.Center;
        _busyLabel.Text = "● commande en cours — Ctrl+C pour l'arrêter · le texte tapé va au programme";
        _busyLabel.Margin = new Thickness(10, 0, 6, 0);
        _busyLabel.Visibility = Visibility.Collapsed;

        _lastFailed.Text = "✗";
        _lastFailed.Foreground = FailedBrush;
        _lastFailed.VerticalAlignment = VerticalAlignment.Center;
        _lastFailed.Margin = new Thickness(0, 0, 4, 0);
        _lastFailed.ToolTip = "La dernière commande a échoué";
        _lastFailed.Visibility = Visibility.Collapsed;

        _prompt.Children.Add(_shellLabel);
        _prompt.Children.Add(_folderLabel);
        _prompt.Children.Add(_branchButton);
        _prompt.Children.Add(_lastFailed);
        _prompt.Children.Add(new TextBlock { Text = "›", Foreground = Theme.Accent, FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 4, 0) });

        // ---------- Zone de saisie ----------
        _input.Background = Theme.PanelHeader;
        _input.Foreground = Theme.Text;
        _input.CaretBrush = Theme.Text;
        _input.BorderThickness = new Thickness(0);
        _input.FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New");
        _input.FontSize = 13;
        _input.Padding = new Thickness(4, 6, 4, 6);
        _input.VerticalContentAlignment = VerticalAlignment.Center;
        _input.ToolTip = "Entrée : exécuter · ↑/↓ : historique · Tab : compléter · Ctrl+C : arrêter · Ctrl+L : effacer";
        _input.PreviewKeyDown += OnInputKeyDown;

        var promptHost = new Grid();
        promptHost.Children.Add(_prompt);
        promptHost.Children.Add(_busyLabel);

        var inputRow = new Grid { Background = Theme.PanelHeader };
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        SetColumn(_input, 1);
        inputRow.Children.Add(promptHost);
        inputRow.Children.Add(_input);

        SetRow(_view, 0);
        SetRow(inputRow, 1);
        Children.Add(_view);
        Children.Add(inputRow);

        // ---------- Session ----------
        _session.OutputReceived += (_, lines) => _view.Append(lines);
        _session.PromptReady += (_, prompt) => OnPromptReady(prompt);
        _session.BusyChanged += (_, _) => UpdateBusy();
        _session.Exited += OnExited;

        UpdatePrompt(_session.WorkingDirectory);
        UpdateBusy();
    }

    // =====================================================================
    // Démarrage
    // =====================================================================

    /// <summary>Démarre le shell s'il ne tourne pas (dans le dossier du projet).</summary>
    public void EnsureStarted()
    {
        if (_session.IsRunning)
            return;

        try
        {
            SetStartFolder();
            _session.Start();
            UpdatePrompt(_session.WorkingDirectory);
        }
        catch (Exception ex)
        {
            WriteError($"Impossible de démarrer le terminal : {ex.Message}");
        }
    }

    /// <summary>Bouton ↻ : nouveau shell dans le dossier du projet, écran effacé.</summary>
    public void Restart()
    {
        _view.Clear();
        SetStartFolder();

        try
        {
            _session.Restart();
        }
        catch (Exception ex)
        {
            WriteError($"Impossible de démarrer le terminal : {ex.Message}");
        }

        UpdatePrompt(_session.WorkingDirectory);
    }

    /// <summary>Change de shell (PowerShell, Invite de commandes, Git Bash) et redémarre.</summary>
    public void SwitchShell(TerminalShell shell)
    {
        if (_session.Shell == shell && _session.IsRunning)
            return;

        _session.Shell = shell;
        Restart();
    }

    public void FocusInput() =>
        Dispatcher.InvokeAsync(() => _input.Focus(), DispatcherPriority.Input);

    private void SetStartFolder()
    {
        string? project = _getProjectFolder();
        _session.WorkingDirectory = !string.IsNullOrWhiteSpace(project) && Directory.Exists(project)
            ? Path.GetFullPath(project)
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    // =====================================================================
    // Saisie
    // =====================================================================

    private async void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        bool control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                string command = _input.Text;
                _input.Clear();
                await SubmitAsync(command);
                break;

            case Key.Up when !_session.IsBusy:
                e.Handled = true;
                if (_session.History.Previous() is { } previous)
                    SetInput(previous);
                break;

            case Key.Down when !_session.IsBusy:
                e.Handled = true;
                SetInput(_session.History.Next());
                break;

            case Key.Tab:
                e.Handled = true;
                Complete();
                break;

            case Key.Escape:
                e.Handled = true;
                _input.Clear();
                break;

            case Key.L when control:
                e.Handled = true;
                _view.Clear();
                break;

            // Ctrl+C sans texte sélectionné : arrêter la commande (avec sélection : copier, comme d'habitude)
            case Key.C when control && _input.SelectionLength == 0:
                e.Handled = true;
                Interrupt();
                break;
        }
    }

    private async Task SubmitAsync(string command)
    {
        EnsureStarted();

        // cls / clear : effacer l'écran (géré ici, plus rapide et identique pour tous les shells)
        if (!_session.IsBusy && command.Trim() is "cls" or "clear")
        {
            _view.Clear();
            return;
        }

        // Écho de la commande avec l'invite en couleur (le shell ne la réaffiche pas)
        if (!_session.IsBusy)
            WriteEcho(command);

        try
        {
            await _session.SendCommandAsync(command);
        }
        catch (Exception ex)
        {
            WriteError($"Erreur : {ex.Message}");
        }
    }

    private void Interrupt()
    {
        if (!_session.IsBusy)
        {
            _view.Append([new OutputLine(OutputKind.Info, (_input.Text.Length > 0 ? _input.Text : "") + "^C\n")]);
            _input.Clear();
            return;
        }

        if (_session.Interrupt())
        {
            _view.Append([new OutputLine(OutputKind.Info, "^C\n")]);
            return;
        }

        // Commande interne du shell (ex. une boucle PowerShell) : impossible à arrêter seule
        string folder = _session.WorkingDirectory;
        _view.Append([new OutputLine(OutputKind.Info, "^C — la commande ne peut pas être interrompue : le terminal redémarre.\n")]);

        try
        {
            _session.Restart(); // garde le dossier courant
        }
        catch (Exception ex)
        {
            WriteError(ex.Message);
        }

        UpdatePrompt(folder);
    }

    private void SetInput(string text)
    {
        _input.Text = text;
        _input.CaretIndex = text.Length;
    }

    // =====================================================================
    // Invite (dossier + Git)
    // =====================================================================

    private void OnPromptReady(TerminalPrompt prompt)
    {
        _lastFailed.Visibility = prompt.Success ? Visibility.Collapsed : Visibility.Visible;
        UpdatePrompt(prompt.WorkingDirectory);
    }

    private void UpdatePrompt(string folder)
    {
        _shellLabel.Text = _session.Shell switch
        {
            TerminalShell.Cmd => "CMD",
            TerminalShell.GitBash => "BASH",
            _ => "PS"
        };

        _folderLabel.Text = ShortFolder(folder);
        _folderLabel.ToolTip = folder;

        _ = UpdateGitAsync(folder);
    }

    /// <summary>Branche, modifications (●), commits à pousser (↑) et à récupérer (↓).</summary>
    private async Task UpdateGitAsync(string folder)
    {
        int ticket = ++_gitTicket;

        try
        {
            string? root = Directory.Exists(folder) ? await _git.FindRepositoryRootAsync(folder) : null;
            if (ticket != _gitTicket)
                return;

            if (root is null)
            {
                _branch = null;
                _branches = [];
                _branchButton.Visibility = Visibility.Collapsed;
                return;
            }

            GitStatus status = await _git.GetStatusAsync(root);
            GitBranches branches = await _git.GetBranchesAsync(root);
            if (ticket != _gitTicket)
                return;

            _branch = status.Branch;
            _branches = branches.Local
                .Concat(branches.Remote.Select(r => r[(r.IndexOf('/') + 1)..]))
                .Distinct()
                .ToList();

            int changes = status.Staged.Count + status.Changes.Count + status.Conflicts.Count;

            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new TextBlock { Text = "⎇ " + (status.Branch ?? "HEAD détaché"), Foreground = BranchBrush });

            if (changes > 0)
                content.Children.Add(new TextBlock { Text = $" ●{changes}", Foreground = DirtyBrush });
            if (status.Ahead > 0)
                content.Children.Add(new TextBlock { Text = $" ↑{status.Ahead}", Foreground = Theme.Green });
            if (status.Behind > 0)
                content.Children.Add(new TextBlock { Text = $" ↓{status.Behind}", Foreground = FolderBrush });

            _branchButton.Content = content;
            _branchButton.ToolTip =
                $"Branche : {status.Branch ?? "HEAD détaché"}\n"
                + $"{changes} fichier(s) modifié(s)"
                + (status.Ahead > 0 ? $"\n{status.Ahead} commit(s) à pousser (git push)" : "")
                + (status.Behind > 0 ? $"\n{status.Behind} commit(s) à récupérer (git pull)" : "")
                + "\nClic : ouvrir le Contrôle de source";
            _branchButton.Visibility = Visibility.Visible;
        }
        catch (Exception)
        {
            // Git absent ou dépôt illisible : pas d'information Git dans l'invite
            _branchButton.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateBusy()
    {
        bool busy = _session.IsBusy && _session.IsRunning;
        _prompt.Visibility = busy ? Visibility.Collapsed : Visibility.Visible;
        _busyLabel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnExited(object? sender, int exitCode)
    {
        if (_session.IsRunning)
            return; // le terminal vient d'être redémarré

        UpdateBusy();
        _view.Append([new OutputLine(OutputKind.Info, $"\n[Terminal terminé, code {exitCode}. Tapez une commande pour le relancer.]\n")]);
    }

    /// <summary>Affiche « PS C:\...\projet (main)> commande » en couleur.</summary>
    private void WriteEcho(string command)
    {
        string shell = _session.Shell switch
        {
            TerminalShell.Cmd => "",
            TerminalShell.GitBash => "$ ",
            _ => "PS "
        };

        string branch = _branch is null ? "" : $" {Esc}[35m({_branch}){Esc}[0m";
        string line = $"{Esc}[90m{shell}{Esc}[36m{_session.WorkingDirectory}{Esc}[0m{branch}{Esc}[90m>{Esc}[0m {Esc}[97m{command}{Esc}[0m\n";

        _view.Append([new OutputLine(OutputKind.Info, line)]);
    }

    private void WriteError(string message) =>
        _view.Append([new OutputLine(OutputKind.StdErr, message + "\n")]);

    private static string ShortFolder(string folder)
    {
        string trimmed = Path.TrimEndingDirectorySeparator(folder);
        string name = Path.GetFileName(trimmed);
        string? parent = Path.GetFileName(Path.GetDirectoryName(trimmed) ?? "");

        if (string.IsNullOrEmpty(name))
            return folder;

        return string.IsNullOrEmpty(parent) ? name : $"…\\{parent}\\{name}";
    }

    // =====================================================================
    // Complétion (Tab)
    // =====================================================================

    private void Complete()
    {
        string text = _input.Text;
        int caret = _input.CaretIndex;
        string before = text[..caret];
        string after = text[caret..];

        int tokenStart = before.LastIndexOf(' ') + 1;
        string token = before[tokenStart..];
        string[] words = before.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        bool newWord = token.Length == 0;
        bool isGit = words.Length > 0 && words[0] == "git";

        List<string> candidates;

        if (isGit && ((words.Length == 1 && newWord) || (words.Length == 2 && !newWord)))
        {
            // git <commande>
            candidates = GitCommands.Where(c => c.StartsWith(token, StringComparison.Ordinal)).ToList();
        }
        else if (isGit && words.Length >= 2 && BranchCommands.Contains(words[1]))
        {
            // git switch <branche> (+ fichiers pour checkout, diff...)
            candidates = _branches.Where(b => b.StartsWith(token, StringComparison.OrdinalIgnoreCase)).ToList();
            candidates.AddRange(PathCandidates(token));
        }
        else
        {
            candidates = PathCandidates(token);
        }

        candidates = candidates.Distinct().ToList();
        if (candidates.Count == 0)
            return;

        string replacement;

        if (candidates.Count == 1)
        {
            replacement = candidates[0];
            bool isFolder = replacement.EndsWith('\\') || replacement.EndsWith('/');
            if (!isFolder)
                replacement += " ";
        }
        else
        {
            replacement = CommonPrefix(candidates);

            // Plusieurs possibilités : on les affiche (comme un vrai terminal)
            if (replacement.Length <= token.Length)
            {
                string list = string.Join("    ", candidates.Take(60)) + (candidates.Count > 60 ? "    …" : "");
                _view.Append([new OutputLine(OutputKind.Info, list + "\n")]);
                return;
            }
        }

        // Noms avec des espaces : entre guillemets
        if (replacement.Contains(' ') && !replacement.EndsWith(' '))
            replacement = $"\"{replacement}\"";

        _input.Text = before[..tokenStart] + replacement + after;
        _input.CaretIndex = tokenStart + replacement.Length;
    }

    /// <summary>Fichiers et dossiers du dossier courant qui commencent par le mot tapé.</summary>
    private List<string> PathCandidates(string token)
    {
        var result = new List<string>();
        string clean = token.Trim('"');
        char separator = clean.Contains('/') || _session.Shell == TerminalShell.GitBash ? '/' : '\\';

        int lastSeparator = clean.LastIndexOfAny(['\\', '/']);
        string folderPart = lastSeparator >= 0 ? clean[..(lastSeparator + 1)] : "";
        string namePart = clean[(lastSeparator + 1)..];

        try
        {
            string folder = Path.GetFullPath(Path.Combine(_session.WorkingDirectory, folderPart.Length == 0 ? "." : folderPart));
            if (!Directory.Exists(folder))
                return result;

            foreach (string directory in Directory.EnumerateDirectories(folder))
            {
                string name = Path.GetFileName(directory);
                if (name.StartsWith(namePart, StringComparison.OrdinalIgnoreCase))
                    result.Add(folderPart + name + separator);
            }

            foreach (string file in Directory.EnumerateFiles(folder))
            {
                string name = Path.GetFileName(file);
                if (name.StartsWith(namePart, StringComparison.OrdinalIgnoreCase))
                    result.Add(folderPart + name);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // dossier illisible : pas de complétion
        }

        return result.Take(500).ToList();
    }

    private static string CommonPrefix(IReadOnlyList<string> values)
    {
        string prefix = values[0];

        foreach (string value in values.Skip(1))
        {
            int length = 0;
            while (length < prefix.Length && length < value.Length
                   && char.ToLowerInvariant(prefix[length]) == char.ToLowerInvariant(value[length]))
                length++;

            prefix = prefix[..length];
        }

        return prefix;
    }

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
