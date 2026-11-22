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

/// <summary>Panneau inférieur : onglet SORTIE (Run/Stop) et onglet TERMINAL (shell intégré).</summary>
public sealed class BottomPanel : Grid
{
    private static readonly Brush PanelBg = Frozen(30, 30, 30);
    private static readonly Brush HeaderBg = Frozen(37, 37, 38);
    private static readonly Brush Accent = Frozen(0, 122, 204);
    private static readonly Brush Gray = Frozen(150, 150, 150);
    private static readonly Brush White = Frozen(230, 230, 230);
    private static readonly Brush Green = Frozen(115, 201, 145);
    private static readonly Brush Red = Frozen(244, 135, 113);

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
              <Setter TargetName="bd" Property="Background" Value="#33FFFFFF"/>
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

    private readonly ConsoleView _output = new();
    private readonly ConsoleView _termView = new();
    private readonly Grid _terminalHost = new();
    private readonly TextBox _input = new();
    private readonly TextBlock _status = new();

    private readonly Button _tabOutput = MakeButton("SORTIE");
    private readonly Button _tabTerminal = MakeButton("TERMINAL");
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
        BuildTerminalHost();

        SetRow(_output, 1);
        SetRow(_terminalHost, 1);
        Children.Add(_output);
        Children.Add(_terminalHost);

        _tabOutput.Click += (_, _) => ShowTab(terminal: false);
        _tabTerminal.Click += (_, _) => ShowTab(terminal: true);
        _run.Click += (_, _) => RequestRun();
        _stop.Click += (_, _) => RequestStop();

        _restart.Click += (_, _) =>
        {
            _termView.Clear();
            UpdateTerminalWorkingDirectory();
            _terminal.Restart();
        };
        
        _input.PreviewKeyDown += OnInputKeyDown;

        _runner.OutputReceived += (_, lines) => _output.Append(lines);
        _runner.StateChanged += (_, _) => UpdateState();
        _runner.Completed += OnCompleted;
        _terminal.OutputReceived += (_, lines) => _termView.Append(lines);
        _terminal.Exited += OnTerminalExited;

        ShowTab(terminal: false);
        UpdateState();
    }

    // ───── API publique (raccourcis clavier F5 / Maj+F5) ─────

    public void RequestRun() => _ = RunAsync();

    public void RequestStop() => _runner.Stop();

    // ───── Exécution du script ─────

    private async Task RunAsync()
    {
        if (_runner.State == RunState.Running || !_run.IsEnabled) return;

        _run.IsEnabled = false; // évite le double clic pendant la sauvegarde
        try
        {
            ShowTab(terminal: false);
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

    private void ShowTab(bool terminal)
    {
        _output.Visibility = terminal ? Visibility.Collapsed : Visibility.Visible;
        _terminalHost.Visibility = terminal ? Visibility.Visible : Visibility.Collapsed;
        _restart.Visibility = terminal ? Visibility.Visible : Visibility.Collapsed;

        Select(_tabOutput, !terminal);
        Select(_tabTerminal, terminal);

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
        var header = new Grid { Background = HeaderBg };
        for (var i = 0; i < 7; i++)
        {
            header.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = i == 2 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto,
            });
        }

        _status.Foreground = Gray;
        _status.FontSize = 12;
        _status.VerticalAlignment = VerticalAlignment.Center;
        _status.Margin = new Thickness(0, 0, 12, 0);

        Place(header, _tabOutput, 0);
        Place(header, _tabTerminal, 1);
        Place(header, _status, 3);
        Place(header, _restart, 4);
        Place(header, _run, 5);
        Place(header, _stop, 6);

        SetRow(header, 0);
        Children.Add(header);
    }

    private void BuildTerminalHost()
    {
        _terminalHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _terminalHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _input.Background = HeaderBg;
        _input.Foreground = White;
        _input.CaretBrush = White;
        _input.BorderThickness = new Thickness(0);
        _input.FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New");
        _input.FontSize = 13;
        _input.Padding = new Thickness(4, 6, 4, 6);
        _input.VerticalContentAlignment = VerticalAlignment.Center;

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
