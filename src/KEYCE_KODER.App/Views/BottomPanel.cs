using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using KEYCE_KODER.Core.Execution;
using KEYCE_KODER.Terminal;
using KEYCE_KODER.App.Services;

namespace KEYCE_KODER.App.Views;

/// <summary>Panneau inférieur : sortie, terminal, problèmes et console de débogage.</summary>
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
              <Setter TargetName="bd" Property="Background" Value="#22242B"/>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.4"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
        """);

    private readonly IScriptRunner _runner;
    private readonly Func<Task<string?>> _prepareScript;
    private readonly Func<string?> _getTerminalWorkingDirectory;

    // Onglet SORTIE
    private readonly Grid _outputHost = new();
    private readonly ConsoleView _output = new();
    private readonly Grid _programInputRow = new();
    private readonly TextBox _programInput = new();

    // Onglet TERMINAL
    private readonly TerminalView _terminalView;
    private readonly ComboBox _shellChoice = new();

    // Onglet PROBLÈMES
    private readonly Grid _problemsHost = new();
    private readonly TextBlock _problemsMessage = new();
    private readonly Border _problemsBadge = new();
    private readonly TextBlock _problemsBadgeText = new();

    // Onglet CONSOLE DE DÉBOGAGE
    private readonly Border _debugHost = new();

    public enum PanelTab { Output, Terminal, Problems, Debug }
    private PanelTab _currentTab = PanelTab.Output;

    private bool _terminalHostReady;

    /// <summary>Clic sur la branche Git dans l'invite du terminal.</summary>
    public event Action? GitViewRequested;
    public event Action? CloseRequested;
    public event Action? MaximizeRequested;
    public event Action? NewTerminalRequested;

    private readonly TextBlock _status = new();
    private readonly Button _tabOutput = MakeButton("SORTIE");
    private readonly Button _tabTerminal = MakeButton("TERMINAL");
    private readonly Button _tabProblems = MakeButton("");
    private readonly Button _tabDebug = MakeButton("CONSOLE DE DÉBOGAGE");

    private readonly Button _restart;
    private readonly Button _run;
    private readonly Button _stop;

    // Boutons d'action modernes à droite
    private readonly Button _btnNewTerminal;
    private readonly Button _btnClear;
    private readonly Button _btnMaximize;
    private readonly Button _btnClose;

    private bool _isMaximized;

    /// <param name="prepareScript">Sauvegarde le fichier actif et renvoie son chemin (null = annuler).</param>
    public BottomPanel(
        IScriptRunner runner,
        TerminalSession terminal,
        Func<Task<string?>> prepareScript,
        Func<string?> getTerminalWorkingDirectory)
    {
        _runner = runner;
        _prepareScript = prepareScript;
        _getTerminalWorkingDirectory = getTerminalWorkingDirectory;

        _terminalView = new TerminalView(terminal, getTerminalWorkingDirectory);
        _terminalView.GitRequested += () => GitViewRequested?.Invoke();

        // Boutons avec icônes SVG
        _restart = MakeIconButton("restart", "Redémarrer", "Redémarrer le terminal");
        _run = MakeIconButton("play", "Run", "Exécuter le fichier actif (F5)", Green);
        _stop = MakeIconButton("stop", "Stop", "Arrêter l'exécution (Maj+F5)", Red);

        _btnNewTerminal = MakeIconButtonOnly("plus", "Nouveau terminal");
        _btnClear = MakeIconButtonOnly("trash", "Effacer le contenu");
        _btnMaximize = MakeIconButtonOnly("maximize", "Agrandir / Restaurer le panneau");
        _btnClose = MakeIconButtonOnly("close", "Fermer le panneau");

        Background = PanelBg;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        BuildHeader();
        BuildOutputHost();
        BuildProblemsHost();
        BuildShellChoice();

        SetRow(_outputHost, 1);
        SetRow(_terminalView, 1);
        SetRow(_problemsHost, 1);
        SetRow(_debugHost, 1);

        Children.Add(_outputHost);
        Children.Add(_terminalView);
        Children.Add(_problemsHost);
        Children.Add(_debugHost);

        _tabOutput.Click += (_, _) => ShowTab(PanelTab.Output);
        _tabTerminal.Click += (_, _) => ShowTab(PanelTab.Terminal);
        _tabProblems.Click += (_, _) => ShowTab(PanelTab.Problems);
        _tabDebug.Click += (_, _) => ShowTab(PanelTab.Debug);

        _run.Click += (_, _) => RequestRun();
        _stop.Click += (_, _) => RequestStop();
        _restart.Click += (_, _) => _terminalView.Restart();

        _btnNewTerminal.Click += (_, _) => NewTerminal();
        _btnClear.Click += (_, _) => ClearActiveTab();
        _btnMaximize.Click += (_, _) => ToggleMaximize();
        _btnClose.Click += (_, _) => ClosePanel();

        _programInput.PreviewKeyDown += OnProgramInputKeyDown;

        _runner.OutputReceived += (_, lines) => _output.Append(lines);
        _runner.StateChanged += (_, _) => UpdateState();
        _runner.Completed += OnCompleted;

        ShowTab(PanelTab.Output);
        UpdateState();
    }

    // ───── API publique (raccourcis clavier F5 / Maj+F5 / Affichage) ─────

    public void RequestRun() => _ = RunAsync();

    public void RequestStop() => _runner.Stop();

    public void NewTerminal()
    {
        _terminalView.Restart();
        ShowTab(PanelTab.Terminal);
        NewTerminalRequested?.Invoke();
    }

    public void ClearActiveTab()
    {
        if (_currentTab == PanelTab.Output)
        {
            _output.Clear();
        }
        else if (_currentTab == PanelTab.Terminal)
        {
            _terminalView.Restart();
        }
    }

    public void TogglePanel()
    {
        Visibility = Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }

    public void ClosePanel()
    {
        Visibility = Visibility.Collapsed;
        CloseRequested?.Invoke();
    }

    public void ToggleMaximize()
    {
        _isMaximized = !_isMaximized;
        _btnMaximize.Content = SvgIconFactory.Create(_isMaximized ? "restore" : "maximize", 12);
        _btnMaximize.ToolTip = _isMaximized ? "Restaurer le panneau" : "Agrandir le panneau";
        MaximizeRequested?.Invoke();
    }

    // ───── Console de débogage ─────

    public void SetDebugConsole(UIElement content) => _debugHost.Child = content;

    public void ShowDebugConsole() => ShowTab(PanelTab.Debug);

    public void SetProblemsCount(int count)
    {
        _problemsBadgeText.Text = count.ToString();
        _problemsBadge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _problemsMessage.Text = count == 0
            ? "Aucun problème détecté dans l'espace de travail."
            : $"{count} problème(s) identifié(s).";
    }

    // ───── Exécution du programme ─────

    private async Task RunAsync()
    {
        if (_runner.State == RunState.Running)
        {
            if (AppSettings.Current.AutoStopPreviousRun)
            {
                _runner.Stop();
                for (int i = 0; i < 20 && _runner.State == RunState.Running; i++)
                {
                    await Task.Delay(50);
                }
            }
            else
            {
                return;
            }
        }

        if (!_run.IsEnabled) return;

        _run.IsEnabled = false;
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

    private void OnProgramInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        e.Handled = true;
        var text = _programInput.Text;
        _programInput.Clear();

        _output.Append(new[] { new OutputLine(OutputKind.Info, text + "\n") });
        _runner.SendInput(text);
    }

    // ───── Affichage des onglets ─────

    public void ShowTab(PanelTab tab)
    {
        _currentTab = tab;
        bool isTerminal = tab == PanelTab.Terminal;

        _outputHost.Visibility = tab == PanelTab.Output ? Visibility.Visible : Visibility.Collapsed;
        _terminalView.Visibility = isTerminal ? Visibility.Visible : Visibility.Collapsed;
        _problemsHost.Visibility = tab == PanelTab.Problems ? Visibility.Visible : Visibility.Collapsed;
        _debugHost.Visibility = tab == PanelTab.Debug ? Visibility.Visible : Visibility.Collapsed;

        _restart.Visibility = isTerminal ? Visibility.Visible : Visibility.Collapsed;
        _shellChoice.Visibility = isTerminal ? Visibility.Visible : Visibility.Collapsed;

        Select(_tabOutput, tab == PanelTab.Output);
        Select(_tabTerminal, isTerminal);
        Select(_tabProblems, tab == PanelTab.Problems);
        Select(_tabDebug, tab == PanelTab.Debug);

        if (isTerminal)
        {
            _terminalView.EnsureStarted();
            _terminalView.FocusInput();
        }
    }

    private static void Select(Button tab, bool selected)
    {
        tab.BorderBrush = selected ? Accent : Brushes.Transparent;
        tab.Foreground = selected ? White : Gray;
    }

    private void BuildHeader()
    {
        var header = new Grid { Background = HeaderBg, MinHeight = 36 };

        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 0: Onglets
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 1: Espace
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 2: Status
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 3: ShellChoice
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 4: Restart
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 5: Run
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 6: Stop
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 7: Window controls (icons)

        // Onglet Problèmes avec badge
        var probSp = new StackPanel { Orientation = Orientation.Horizontal };
        probSp.Children.Add(new TextBlock { Text = "PROBLÈMES", VerticalAlignment = VerticalAlignment.Center });

        _problemsBadge.Background = Theme.Surface;
        _problemsBadge.CornerRadius = new CornerRadius(7);
        _problemsBadge.Padding = new Thickness(5, 1, 5, 1);
        _problemsBadge.Margin = new Thickness(6, 0, 0, 0);
        _problemsBadge.VerticalAlignment = VerticalAlignment.Center;
        _problemsBadge.Visibility = Visibility.Collapsed;

        _problemsBadgeText.Text = "0";
        _problemsBadgeText.FontSize = 10;
        _problemsBadgeText.FontWeight = FontWeights.Bold;
        _problemsBadgeText.Foreground = Theme.Accent;
        _problemsBadge.Child = _problemsBadgeText;
        probSp.Children.Add(_problemsBadge);
        _tabProblems.Content = probSp;

        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        tabs.Children.Add(_tabOutput);
        tabs.Children.Add(_tabTerminal);
        tabs.Children.Add(_tabProblems);
        tabs.Children.Add(_tabDebug);

        _status.Foreground = Gray;
        _status.FontSize = 12;
        _status.VerticalAlignment = VerticalAlignment.Center;
        _status.Margin = new Thickness(0, 0, 10, 0);

        var rightIcons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(8, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        rightIcons.Children.Add(_btnNewTerminal);
        rightIcons.Children.Add(_btnClear);
        rightIcons.Children.Add(_btnMaximize);
        rightIcons.Children.Add(_btnClose);

        Place(header, tabs, 0);
        Place(header, _status, 2);
        Place(header, _shellChoice, 3);
        Place(header, _restart, 4);
        Place(header, _run, 5);
        Place(header, _stop, 6);
        Place(header, rightIcons, 7);

        SetRow(header, 0);
        Children.Add(header);
    }

    private void BuildOutputHost()
    {
        _outputHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _outputHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        StyleInput(_programInput);
        _programInput.ToolTip = "Texte envoyé au programme. Entrée pour envoyer.";

        var prompt = new TextBlock
        {
            Text = "Entrée du programme ›",
            Foreground = Accent,
            FontSize = 12,
            Margin = new Thickness(10, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        _programInputRow.Background = HeaderBg;
        _programInputRow.Visibility = Visibility.Collapsed;
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

    private void BuildProblemsHost()
    {
        _problemsMessage.Text = "Aucun problème détecté dans l'espace de travail.";
        _problemsMessage.Foreground = Gray;
        _problemsMessage.FontSize = 12;
        _problemsMessage.Margin = new Thickness(16, 14, 16, 0);
        _problemsHost.Children.Add(_problemsMessage);
        _problemsHost.Visibility = Visibility.Collapsed;
    }

    private void BuildShellChoice()
    {
        _shellChoice.Items.Add(new ComboBoxItem { Content = "PowerShell", Tag = TerminalShell.PowerShell });
        _shellChoice.Items.Add(new ComboBoxItem { Content = "Invite de commandes", Tag = TerminalShell.Cmd });

        if (TerminalSession.FindGitBash() is not null)
            _shellChoice.Items.Add(new ComboBoxItem { Content = "Git Bash", Tag = TerminalShell.GitBash });

        _shellChoice.SelectedIndex = 0;
        _shellChoice.MinWidth = 130;
        _shellChoice.Margin = new Thickness(0, 0, 8, 0);
        _shellChoice.VerticalAlignment = VerticalAlignment.Center;
        _shellChoice.Focusable = false;
        _shellChoice.ToolTip = "Shell du terminal";
        _shellChoice.SelectionChanged += (_, _) =>
        {
            if (_shellChoice.SelectedItem is ComboBoxItem { Tag: TerminalShell shell } && _terminalHostReady)
            {
                _terminalView.SwitchShell(shell);
                _terminalView.FocusInput();
            }
        };
        _terminalHostReady = true;
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
        Focusable = false,
    };

    private static Button MakeIconButton(string iconName, string text, string tooltip, Brush? textBrush = null)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var icon = SvgIconFactory.Create(iconName, 13);
        icon.Margin = new Thickness(0, 0, 6, 0);
        sp.Children.Add(icon);
        sp.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = textBrush ?? White,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        });

        var btn = new Button
        {
            Content = sp,
            Template = FlatTemplate,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4, 8, 4),
            ToolTip = tooltip,
            Cursor = Cursors.Hand,
            Focusable = false,
            VerticalAlignment = VerticalAlignment.Center
        };
        return btn;
    }

    private static Button MakeIconButtonOnly(string iconName, string tooltip)
    {
        var icon = SvgIconFactory.Create(iconName, 13);
        var btn = new Button
        {
            Content = icon,
            Template = FlatTemplate,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 4, 6, 4),
            ToolTip = tooltip,
            Cursor = Cursors.Hand,
            Focusable = false,
            VerticalAlignment = VerticalAlignment.Center
        };
        return btn;
    }
}
