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

/// <summary>Panneau inférieur : sortie d'exécution, terminal intégré et console de débogage.</summary>
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

    private readonly TextBlock _status = new();
    private readonly Button _tabOutput = MakeButton("SORTIE");
    private readonly Button _tabTerminal = MakeButton("TERMINAL");
    private readonly Button _tabDebug = MakeButton("CONSOLE DE DÉBOGAGE");

    // Onglet CONSOLE DE DÉBOGAGE (contenu fourni par le débogueur)
    private readonly Border _debugHost = new();

    private enum PanelTab { Output, Terminal, Debug }
    private readonly Button _restart = MakeButton("↻ Redémarrer");
    private readonly Button _run = MakeButton("▶ Run", Green);
    private readonly Button _stop = MakeButton("■ Stop", Red);

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

        SetRow(_outputHost, 1);
        SetRow(_terminalHost, 1);
        SetRow(_debugHost, 1);
        Children.Add(_outputHost);
        Children.Add(_terminalHost);
        Children.Add(_debugHost);

        _tabOutput.Click += (_, _) => ShowTab(PanelTab.Output);
        _tabTerminal.Click += (_, _) => ShowTab(PanelTab.Terminal);
        _tabDebug.Click += (_, _) => ShowTab(PanelTab.Debug);
        _run.Click += (_, _) => RequestRun();
        _stop.Click += (_, _) => RequestStop();

        _restart.Click += (_, _) =>
        {
            _termView.Clear();
            UpdateTerminalWorkingDirectory();
            _terminal.Restart();
        };
        
        _input.PreviewKeyDown += OnInputKeyDown;
        _programInput.PreviewKeyDown += OnProgramInputKeyDown;

        _runner.OutputReceived += (_, lines) => _output.Append(lines);
        _runner.StateChanged += (_, _) => UpdateState();
        _runner.Completed += OnCompleted;
        _terminal.OutputReceived += (_, lines) => _termView.Append(lines);
        _terminal.Exited += OnTerminalExited;

        ShowTab(PanelTab.Output);
        UpdateState();
    }

    // ───── API publique (raccourcis clavier F5 / Maj+F5) ─────

    public void RequestRun() => _ = RunAsync();

    public void RequestStop() => _runner.Stop();

    // ───── Console de débogage ─────

    /// <summary>Place la console de débogage dans l'onglet « CONSOLE DE DÉBOGAGE ».</summary>
    public void SetDebugConsole(UIElement content) => _debugHost.Child = content;

    /// <summary>Affiche l'onglet « CONSOLE DE DÉBOGAGE ».</summary>
    public void ShowDebugConsole() => ShowTab(PanelTab.Debug);

    // ───── Exécution du programme ─────

    private async Task RunAsync()
    {
        if (_runner.State == RunState.Running || !_run.IsEnabled) return;

        _run.IsEnabled = false; // évite le double clic pendant la sauvegarde
        try
        {
            ShowTab(PanelTab.Output);
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

    private void ShowTab(PanelTab tab)
    {
        bool terminal = tab == PanelTab.Terminal;

        _outputHost.Visibility = tab == PanelTab.Output ? Visibility.Visible : Visibility.Collapsed;
        _terminalHost.Visibility = terminal ? Visibility.Visible : Visibility.Collapsed;
        _debugHost.Visibility = tab == PanelTab.Debug ? Visibility.Visible : Visibility.Collapsed;
        _restart.Visibility = terminal ? Visibility.Visible : Visibility.Collapsed;

        Select(_tabOutput, tab == PanelTab.Output);
        Select(_tabTerminal, terminal);
        Select(_tabDebug, tab == PanelTab.Debug);

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

        for (var i = 0; i < 8; i++)
        {
            header.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = i == 3 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto,
            });
        }

        _status.Foreground = Gray;
        _status.FontSize = 12;
        _status.VerticalAlignment = VerticalAlignment.Center;
        _status.Margin = new Thickness(0, 0, 12, 0);

        Place(header, _tabOutput, 0);
        Place(header, _tabTerminal, 1);
        Place(header, _tabDebug, 2);
        Place(header, _status, 4);
        Place(header, _restart, 5);
        Place(header, _run, 6);
        Place(header, _stop, 7);

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

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
