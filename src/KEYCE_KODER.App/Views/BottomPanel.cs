using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.IO;
using KEYCE_KODER.Core.Execution;
using KEYCE_KODER.Terminal;


namespace KEYCE_KODER.App.Views;

/// <summary>Panneau inférieur : sortie, terminal, problèmes et débogueur.</summary>
public sealed class BottomPanel : Grid
{
    private static readonly Brush PanelBg = Theme.Panel;
    private static readonly Brush HeaderBg = Theme.PanelHeader;
    private static readonly Brush Accent = Theme.Accent;
    private static readonly Brush Gray = Theme.Muted;
    private static readonly Brush White = Theme.Text;
    private static readonly Brush Green = Theme.Green;
    private static readonly Brush Red = Theme.Red;

    private static readonly ControlTemplate FlatTemplate = (ControlTemplate)XamlReader.Parse(
        """
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         TargetType="Button">
          <Border x:Name="bd" Background="{TemplateBinding Background}"
                  BorderBrush="{TemplateBinding BorderBrush}"
                  BorderThickness="{TemplateBinding BorderThickness}"
                  Padding="{TemplateBinding Padding}">
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="bd" Property="Background" Value="#242A34"/>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.4"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
        """);

    private readonly IScriptRunner _runner;
    private readonly TerminalSession _terminal;
    private readonly Func<Task<string?>> _prepareScript;
    private readonly Func<string?> _getTerminalWorkingDirectory;

    // Onglet SORTIE
    private readonly Grid _outputHost = new();
    private readonly ConsoleView _output = new();
    private readonly Grid _programInputRow = new();
    private readonly TextBox _programInput = new();

    // Onglet TERMINAL
    private readonly Grid _terminalHost = new();
    private readonly ConsoleView _termView = new();
    private readonly TextBox _input = new();

    // Onglets PROBLÈMES et DÉBOGUEUR
    private readonly Grid _problemsHost = new();
    private readonly Grid _debugHost = new();
    private readonly TextBlock _problemsMessage = new();
    private readonly TextBlock _badgeText = new();
    private readonly Border _badge = new();

    private readonly TextBlock _status = new();
    private readonly Button _tabOutput = MakeButton("SORTIE");
    private readonly Button _tabTerminal = MakeButton("TERMINAL");
    private readonly Button _tabProblems = MakeButton("PROBLÈMES");
    private readonly Button _tabDebug = MakeButton("DÉBOGUEUR");
    private readonly Button _restart = MakeButton("↻ Redémarrer");
    private readonly Button _run = MakeButton("▶ Run", Green);
    private readonly Button _stop = MakeButton("■ Stop", Red);

    // Petites icônes à droite
    private readonly Button _newTerminal = MakeIcon("+", "Nouveau terminal");
    private readonly Button _clear = MakeIcon("🗑", "Effacer");
    private readonly Button _maximize = MakeIcon("^", "Agrandir le panneau");
    private readonly Button _close = MakeIcon("×", "Fermer le panneau");

    private int _currentTab; // 0 sortie, 1 terminal, 2 problèmes, 3 débogueur
    private bool _maximized;
    private GridLength _savedHeight = new(220);

    /// <param name="prepareScript">Sauvegarde le fichier actif et renvoie son chemin (null = annuler).</param>
    public BottomPanel(
        IScriptRunner runner,
        TerminalSession terminal,
        Func<Task<string?>> prepareScript,
        Func<string?> getTerminalWorkingDirectory)
    {
        _runner = runner;
        _terminal = terminal;
        _prepareScript = prepareScript;
        _getTerminalWorkingDirectory = getTerminalWorkingDirectory;

        Background = PanelBg;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        BuildHeader();
        BuildOutputHost();
        BuildTerminalHost();
        BuildProblemsHost();
        BuildDebugHost();

        SetRow(_outputHost, 1);
        SetRow(_terminalHost, 1);
        SetRow(_problemsHost, 1);
        SetRow(_debugHost, 1);
        Children.Add(_outputHost);
        Children.Add(_terminalHost);
        Children.Add(_problemsHost);
        Children.Add(_debugHost);

        _tabOutput.Click += (_, _) => ShowPanel(0);
        _tabTerminal.Click += (_, _) => ShowPanel(1);
        _tabProblems.Click += (_, _) => ShowPanel(2);
        _tabDebug.Click += (_, _) => ShowPanel(3);
        _run.Click += (_, _) => RequestRun();
        _stop.Click += (_, _) => RequestStop();

        _restart.Click += (_, _) => RestartTerminal();
        _newTerminal.Click += (_, _) => NewTerminal();
        _clear.Click += (_, _) => ClearCurrent();
        _maximize.Click += (_, _) => ToggleMaximize();
        _close.Click += (_, _) => ClosePanel();

        _input.PreviewKeyDown += OnInputKeyDown;
        _programInput.PreviewKeyDown += OnProgramInputKeyDown;

        _runner.OutputReceived += (_, lines) => _output.Append(lines);
        _runner.StateChanged += (_, _) => UpdateState();
        _runner.Completed += OnCompleted;
        _terminal.OutputReceived += (_, lines) => _termView.Append(lines);
        _terminal.Exited += OnTerminalExited;

        SetProblemCount(0);
        ShowPanel(0);
        UpdateState();
    }

    // ───── API publique (raccourcis clavier, menus) ─────

    public void RequestRun() => _ = RunAsync();

    public void RequestStop() => _runner.Stop();

    /// <summary>Redémarre le shell et affiche l'onglet Terminal.</summary>
    public void NewTerminal()
    {
        OpenPanel();
        ShowPanel(1);
        RestartTerminal();
    }

    /// <summary>Met à jour le badge rouge de l'onglet Problèmes.</summary>
    public void SetProblemCount(int count)
    {
        _badge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _badgeText.Text = count.ToString();
        _problemsMessage.Text = count == 0
            ? "Aucun problème détecté dans l'espace de travail."
            : $"{count} problème(s) détecté(s).";
    }

    /// <summary>Ferme le panneau s'il est ouvert, le rouvre sinon.</summary>
    public void TogglePanel()
    {
        if (Visibility == Visibility.Visible) ClosePanel();
        else OpenPanel();
    }

    // ───── Fermer / agrandir ─────

    private RowDefinition? MyRow =>
        Parent is Grid host ? host.RowDefinitions[GetRow(this)] : null;

    private void ToggleMaximize()
    {
        if (Parent is not Grid host || MyRow is not { } row) return;

        if (!_maximized)
        {
            _savedHeight = row.Height;
            row.Height = new GridLength(Math.Max(300, host.ActualHeight * 0.65));
            _maximize.Content = "⌄";
            _maximize.ToolTip = "Réduire le panneau";
        }
        else
        {
            row.Height = _savedHeight;
            _maximize.Content = "^";
            _maximize.ToolTip = "Agrandir le panneau";
        }
        _maximized = !_maximized;
    }

    private void ClosePanel()
    {
        if (Parent is not Grid host || MyRow is not { } row) return;

        if (!_maximized) _savedHeight = row.Height;
        row.MinHeight = 0;
        row.Height = new GridLength(0);
        Visibility = Visibility.Collapsed;
        foreach (var splitter in host.Children.OfType<GridSplitter>())
            splitter.Visibility = Visibility.Collapsed;
        _maximized = false;
    }

    private void OpenPanel()
    {
        if (Visibility == Visibility.Visible) return;
        if (Parent is not Grid host || MyRow is not { } row) return;

        row.MinHeight = 80;
        row.Height = _savedHeight.Value < 80 ? new GridLength(220) : _savedHeight;
        Visibility = Visibility.Visible;
        foreach (var splitter in host.Children.OfType<GridSplitter>())
            splitter.Visibility = Visibility.Visible;
        _maximize.Content = "^";
        _maximize.ToolTip = "Agrandir le panneau";
    }

    private void ClearCurrent()
    {
        if (_currentTab == 0) _output.Clear();
        else if (_currentTab == 1) _termView.Clear();
    }

    private void RestartTerminal()
    {
        _termView.Clear();
        UpdateTerminalWorkingDirectory();
        _terminal.Restart();
    }

    // ───── Exécution du programme ─────

    private async Task RunAsync()
    {
        if (_runner.State == RunState.Running || !_run.IsEnabled) return;

        _run.IsEnabled = false; // évite le double clic pendant la sauvegarde
        try
        {
            OpenPanel();
            ShowPanel(0);
            var path = await _prepareScript();
            if (path is null) return;

            _output.Clear();
            await _runner.RunAsync(path);
        }
        catch (Exception ex)
        {
            _output.Append(new[] { new OutputLine(OutputKind.StdErr, $"Erreur : {ex.Message}\n") });
        }
        finally
        {
            UpdateState();
        }
    }

    private void UpdateState()
    {
        var running = _runner.State == RunState.Running;
        _run.IsEnabled = !running;
        _stop.IsEnabled = running;
        _programInputRow.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        if (running) _status.Text = "En cours…";
    }

    private void OnCompleted(object? sender, RunCompletedEventArgs e)
    {
        var secs = e.Duration.TotalSeconds.ToString("0.0");
        _status.Text = e.Result switch
        {
            RunResult.Completed => $"Terminé (code {e.ExitCode}) · {secs} s",
            RunResult.Stopped => "Arrêté",
            RunResult.TimedOut => $"Timeout · {secs} s",
            _ => "Échec du lancement",
        };
    }

    /// <summary>Entrée pour le programme en cours (input(), scanf, Scanner…).</summary>
    private void OnProgramInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        e.Handled = true;
        var text = _programInput.Text;
        _programInput.Clear();

        _output.Append(new[] { new OutputLine(OutputKind.Info, text + "\n") }); // écho de ce qui est envoyé
        _runner.SendInput(text);
    }

    // ───── Terminal ─────

    private void EnsureTerminalStarted()
    {
        if (_terminal.IsRunning) return;
        try
        {
            UpdateTerminalWorkingDirectory();
            _terminal.Start();
        }
        catch (Exception ex)
        {
            _termView.Append(new[] { new OutputLine(OutputKind.StdErr, $"Impossible de démarrer le terminal : {ex.Message}\n") });
        }
    }

    private void UpdateTerminalWorkingDirectory()
    {
        string? projectFolder = _getTerminalWorkingDirectory();

        string userFolder = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);

        _terminal.WorkingDirectory =
            !string.IsNullOrWhiteSpace(projectFolder) &&
            Directory.Exists(projectFolder)
                ? Path.GetFullPath(projectFolder)
                : userFolder;
    }

    private void OnTerminalExited(object? sender, int exitCode)
    {
        if (_terminal.IsRunning) return; // le terminal vient d'être redémarré
        _termView.Append(new[]
        {
            new OutputLine(OutputKind.Info, $"\n[Terminal terminé, code {exitCode}. Tapez une commande pour le relancer.]\n"),
        });
    }

    private async void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                var command = _input.Text;
                _input.Clear();
                if (!string.IsNullOrWhiteSpace(command)) await SendAsync(command);
                break;

            case Key.Up:
                e.Handled = true;
                var previous = _terminal.History.Previous();
                if (previous is not null) SetInput(previous);
                break;

            case Key.Down:
                e.Handled = true;
                SetInput(_terminal.History.Next());
                break;
        }
    }

    private async Task SendAsync(string command)
    {
        try
        {
            if (!_terminal.IsRunning)
            {
                UpdateTerminalWorkingDirectory();
                _terminal.Start();
            }

            await _terminal.SendCommandAsync(command);
        }
        catch (Exception ex)
        {
            _termView.Append(new[] { new OutputLine(OutputKind.StdErr, $"Erreur : {ex.Message}\n") });
        }
    }

    private void SetInput(string text)
    {
        _input.Text = text;
        _input.CaretIndex = text.Length;
    }

    // ───── Construction de l'interface ─────

    private void ShowPanel(int index)
    {
        _currentTab = index;
        bool terminal = index == 1;

        _outputHost.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        _terminalHost.Visibility = terminal ? Visibility.Visible : Visibility.Collapsed;
        _problemsHost.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        _debugHost.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;

        _restart.Visibility = terminal ? Visibility.Visible : Visibility.Collapsed;
        _clear.IsEnabled = index <= 1;

        Select(_tabOutput, index == 0);
        Select(_tabTerminal, terminal);
        Select(_tabProblems, index == 2);
        Select(_tabDebug, index == 3);

        if (terminal)
        {
            EnsureTerminalStarted();
            Dispatcher.InvokeAsync(() => { _input.Focus(); }, DispatcherPriority.Input);
        }
    }

    private static void Select(Button tab, bool selected)
    {
        tab.BorderBrush = selected ? Accent : Brushes.Transparent;
        tab.Foreground = selected ? White : Gray;
    }

    private void BuildHeader()
    {
        var header = new Grid
        {
            Background = HeaderBg
        };
        header.MinHeight = 38;

        for (var i = 0; i < 7; i++)
        {
            header.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = i == 1 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto,
            });
        }

        _status.Foreground = Gray;
        _status.FontSize = 12;
        _status.VerticalAlignment = VerticalAlignment.Center;
        _status.Margin = new Thickness(0, 0, 12, 0);

        // Badge rouge de l'onglet Problèmes
        _badgeText.Foreground = Brushes.White;
        _badgeText.FontSize = 10;
        _badgeText.FontWeight = FontWeights.Bold;
        _badge.Background = Red;
        _badge.CornerRadius = new CornerRadius(9);
        _badge.MinWidth = 18;
        _badge.Height = 18;
        _badge.Padding = new Thickness(5, 0, 5, 0);
        _badge.Margin = new Thickness(6, 0, 0, 0);
        _badge.Child = _badgeText;
        _badgeText.HorizontalAlignment = HorizontalAlignment.Center;
        _badgeText.VerticalAlignment = VerticalAlignment.Center;

        var problemsContent = new StackPanel { Orientation = Orientation.Horizontal };
        problemsContent.Children.Add(new TextBlock { Text = "PROBLÈMES", VerticalAlignment = VerticalAlignment.Center });
        problemsContent.Children.Add(_badge);
        _tabProblems.Content = problemsContent;

        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        tabs.Children.Add(_tabOutput);
        tabs.Children.Add(_tabTerminal);
        tabs.Children.Add(_tabProblems);
        tabs.Children.Add(_tabDebug);

        var icons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(8, 0, 6, 0)
        };
        icons.Children.Add(_newTerminal);
        icons.Children.Add(_clear);
        icons.Children.Add(_maximize);
        icons.Children.Add(_close);

        Place(header, tabs, 0);
        Place(header, _status, 2);
        Place(header, _restart, 3);
        Place(header, _run, 4);
        Place(header, _stop, 5);
        Place(header, icons, 6);

        SetRow(header, 0);
        Children.Add(header);
    }

    private void BuildOutputHost()
    {
        _outputHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _outputHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        StyleInput(_programInput);
        _programInput.ToolTip = "Texte envoyé au programme (input(), scanf, Scanner…). Entrée pour envoyer.";

        var prompt = new TextBlock
        {
            Text = "Entrée du programme ›",
            Foreground = Accent,
            FontSize = 12,
            Margin = new Thickness(10, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        _programInputRow.Background = HeaderBg;
        _programInputRow.Visibility = Visibility.Collapsed; // visible seulement pendant une exécution
        _programInputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _programInputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        SetColumn(_programInput, 1);
        _programInputRow.Children.Add(prompt);
        _programInputRow.Children.Add(_programInput);

        SetRow(_output, 0);
        SetRow(_programInputRow, 1);
        _outputHost.Children.Add(_output);
        _outputHost.Children.Add(_programInputRow);
    }

    private void BuildTerminalHost()
    {
        _terminalHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _terminalHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        StyleInput(_input);

        var prompt = new TextBlock
        {
            Text = "›",
            Foreground = Accent,
            FontSize = 16,
            Margin = new Thickness(10, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var inputRow = new Grid { Background = HeaderBg };
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        SetColumn(_input, 1);
        inputRow.Children.Add(prompt);
        inputRow.Children.Add(_input);

        SetRow(_termView, 0);
        SetRow(inputRow, 1);
        _terminalHost.Children.Add(_termView);
        _terminalHost.Children.Add(inputRow);
    }

    private void BuildProblemsHost()
    {
        _problemsMessage.Foreground = Gray;
        _problemsMessage.FontSize = 13;
        _problemsMessage.Margin = new Thickness(16, 14, 16, 0);
        _problemsHost.Children.Add(_problemsMessage);
        _problemsHost.Visibility = Visibility.Collapsed;
    }

    private void BuildDebugHost()
    {
        _debugHost.Children.Add(new TextBlock
        {
            Text = "Le débogueur n'est pas encore disponible.",
            Foreground = Gray,
            FontSize = 13,
            Margin = new Thickness(16, 14, 16, 0)
        });
        _debugHost.Visibility = Visibility.Collapsed;
    }

    private static void StyleInput(TextBox box)
    {
        box.Background = HeaderBg;
        box.Foreground = White;
        box.CaretBrush = White;
        box.BorderThickness = new Thickness(0);
        box.FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New");
        box.FontSize = 13;
        box.Padding = new Thickness(4, 6, 4, 6);
        box.VerticalContentAlignment = VerticalAlignment.Center;
    }

    private static void Place(Grid grid, UIElement element, int column)
    {
        SetColumn(element, column);
        grid.Children.Add(element);
    }

    private static Button MakeButton(string text, Brush? foreground = null) => new()
    {
        Content = text,
        Template = FlatTemplate,
        Background = Brushes.Transparent,
        Foreground = foreground ?? Gray,
        BorderBrush = Brushes.Transparent,
        BorderThickness = new Thickness(0, 0, 0, 2),
        Padding = new Thickness(12, 6, 12, 6),
        FontSize = 12,
        Cursor = Cursors.Hand,
        Focusable = false, // ne vole pas le focus à l'éditeur
    };

    private static Button MakeIcon(string glyph, string tip) => new()
    {
        Content = glyph,
        ToolTip = tip,
        Template = FlatTemplate,
        Background = Brushes.Transparent,
        Foreground = Gray,
        BorderBrush = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Padding = new Thickness(8, 4, 8, 4),
        FontSize = 14,
        Cursor = Cursors.Hand,
        Focusable = false,
    };
}