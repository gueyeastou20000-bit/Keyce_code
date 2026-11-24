using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using KEYCE_KODER.App.Views;
using KEYCE_KODER.Runner.Debugging;

namespace KEYCE_KODER.App.Debugging;

/// <summary>
/// Vue « Exécuter et déboguer » de la barre latérale (comme dans VS Code) :
/// boutons de contrôle, variables, espions, pile d'appels, points d'arrêt.
/// Elle ne fait qu'afficher : les actions sont transmises au DebugController par des événements.
/// </summary>
public sealed class DebugView : DockPanel
{
    private const string Placeholder = "…";

    private static readonly Brush NameBrush = Frozen(156, 220, 254);
    private static readonly Brush ValueBrush = Frozen(206, 145, 120);

    private static readonly ControlTemplate FlatTemplate = (ControlTemplate)XamlReader.Parse(
        """
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         TargetType="Button">
          <Grid x:Name="root">
            <Border Background="{TemplateBinding Background}" CornerRadius="4"/>
            <Border x:Name="hover" CornerRadius="4" Background="#26FFFFFF" Visibility="Collapsed"/>
            <ContentPresenter Margin="{TemplateBinding Padding}"
                              HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                              VerticalAlignment="Center"/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="hover" Property="Visibility" Value="Visible"/>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter TargetName="root" Property="Opacity" Value="0.35"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
        """);

    // ---------- Boutons ----------
    private readonly Button _startButton;
    private readonly WrapPanel _controls = new() { Margin = new Thickness(0, 0, 0, 4) };
    private readonly Button _continueButton;
    private readonly Button _pauseButton;
    private readonly Button _stepOverButton;
    private readonly Button _stepIntoButton;
    private readonly Button _stepOutButton;
    private readonly Button _restartButton;
    private readonly Button _stopButton;
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11.5, Margin = new Thickness(0, 2, 0, 6) };

    // ---------- Sections ----------
    private readonly TreeView _variables = new();
    private readonly StackPanel _watchRows = new();
    private readonly TextBox _watchInput = new();
    private readonly ListBox _callStack = new();
    private readonly ListBox _breakpoints = new();
    private readonly List<string> _watches = new();
    private bool _updatingCallStack;

    public event Action? StartRequested;
    public event Action? ContinueRequested;
    public event Action? PauseRequested;
    public event Action? StepOverRequested;
    public event Action? StepIntoRequested;
    public event Action? StepOutRequested;
    public event Action? RestartRequested;
    public event Action? StopRequested;

    /// <summary>Une ligne de la pile d'appels a été choisie.</summary>
    public event Action<DebugFrame>? FrameSelected;

    /// <summary>La liste des expressions espionnées a changé.</summary>
    public event Action? WatchesChanged;

    /// <summary>Double-clic sur un point d'arrêt : aller à sa ligne.</summary>
    public event Action<Breakpoint>? BreakpointActivated;

    /// <summary>Charge le contenu d'une variable dépliable (fourni par le contrôleur).</summary>
    public Func<int, Task<IReadOnlyList<DebugVariable>>>? LoadChildren { get; set; }

    /// <summary>Expressions espionnées (onglet ESPION).</summary>
    public IReadOnlyList<string> Watches => _watches;

    public BreakpointStore? Store { get; private set; }

    public DebugView()
    {
        Margin = new Thickness(10, 0, 10, 6);

        // ---------- Haut : démarrer / contrôles ----------
        _startButton = MakeButton("▶  Démarrer le débogage (F5)", "Lancer le fichier Python actif sous le débogueur", () => StartRequested?.Invoke());
        _startButton.Background = Theme.Accent;
        _startButton.Foreground = Brushes.White;
        _startButton.FontWeight = FontWeights.SemiBold;
        _startButton.Padding = new Thickness(8, 7, 8, 7);
        _startButton.HorizontalContentAlignment = HorizontalAlignment.Center;
        _startButton.Margin = new Thickness(0, 0, 0, 6);

        _continueButton = IconButton("▶", "Continuer (F5)", () => ContinueRequested?.Invoke(), Theme.Green);
        _pauseButton = IconButton("❚❚", "Pause (F6)", () => PauseRequested?.Invoke(), null);
        _stepOverButton = IconButton("↷", "Pas à pas principal (F10) : exécute la ligne", () => StepOverRequested?.Invoke(), null);
        _stepIntoButton = IconButton("↓", "Pas à pas détaillé (F11) : entre dans la fonction", () => StepIntoRequested?.Invoke(), null);
        _stepOutButton = IconButton("↑", "Pas à pas sortant (Maj+F11) : sort de la fonction", () => StepOutRequested?.Invoke(), null);
        _restartButton = IconButton("⟳", "Redémarrer (Ctrl+Maj+F5)", () => RestartRequested?.Invoke(), null);
        _stopButton = IconButton("■", "Arrêter (Maj+F5)", () => StopRequested?.Invoke(), Theme.Red);

        foreach (Button button in new[] { _continueButton, _pauseButton, _stepOverButton, _stepIntoButton, _stepOutButton, _restartButton, _stopButton })
            _controls.Children.Add(button);

        _status.Foreground = Theme.Muted;

        var top = new StackPanel();
        top.Children.Add(_startButton);
        top.Children.Add(_controls);
        top.Children.Add(_status);
        SetDock(top, Dock.Top);
        Children.Add(top);

        // ---------- Sections ----------
        var sections = new Grid();
        sections.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sections.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star), MinHeight = 60 });
        sections.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sections.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sections.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sections.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.4, GridUnitType.Star), MinHeight = 50 });
        sections.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sections.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.2, GridUnitType.Star), MinHeight = 50 });

        StyleList(_variables);
        StyleList(_callStack);
        StyleList(_breakpoints);

        // Variables
        Add(sections, SectionHeader("VARIABLES", null), 0);
        Add(sections, _variables, 1);

        // Espions
        Add(sections, SectionHeader("ESPION", null), 2);
        _watchInput.Background = Theme.Surface;
        _watchInput.Foreground = Theme.Text;
        _watchInput.CaretBrush = Theme.Text;
        _watchInput.BorderBrush = Theme.Border;
        _watchInput.Padding = new Thickness(4, 2, 4, 2);
        _watchInput.ToolTip = "Expression Python à surveiller (ex. total, len(liste), x > 3). Entrée pour ajouter.";
        _watchInput.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && _watchInput.Text.Trim().Length > 0)
            {
                e.Handled = true;
                _watches.Add(_watchInput.Text.Trim());
                _watchInput.Clear();
                WatchesChanged?.Invoke();
            }
        };
        var watchArea = new StackPanel();
        watchArea.Children.Add(new ScrollViewer { Content = _watchRows, MaxHeight = 110, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        watchArea.Children.Add(_watchInput);
        Add(sections, watchArea, 3);

        // Pile d'appels
        Add(sections, SectionHeader("PILE DES APPELS", null), 4);
        _callStack.SelectionChanged += (_, _) =>
        {
            if (!_updatingCallStack && _callStack.SelectedItem is DebugFrame frame)
                FrameSelected?.Invoke(frame);
        };
        Add(sections, _callStack, 5);

        // Points d'arrêt
        Button clearAll = SmallButton("✕", "Supprimer tous les points d'arrêt", () => Store?.RemoveAll());
        Add(sections, SectionHeader("POINTS D'ARRÊT", clearAll), 6);
        _breakpoints.MouseDoubleClick += (_, _) =>
        {
            if (_breakpoints.SelectedItem is ListBoxItem { Tag: Breakpoint breakpoint })
                BreakpointActivated?.Invoke(breakpoint);
        };
        Add(sections, _breakpoints, 7);

        Children.Add(sections);

        SetState(DebugState.Idle);
        ClearVariables("Pas de session de débogage.");
        RenderWatches(null);
    }

    // =====================================================================
    // État
    // =====================================================================

    public void SetState(DebugState state)
    {
        bool idle = state == DebugState.Idle;
        bool paused = state == DebugState.Paused;
        bool running = state is DebugState.Running or DebugState.Starting;

        _startButton.Visibility = idle ? Visibility.Visible : Visibility.Collapsed;
        _controls.Visibility = idle ? Visibility.Collapsed : Visibility.Visible;

        _continueButton.IsEnabled = paused;
        _pauseButton.IsEnabled = state == DebugState.Running;
        _stepOverButton.IsEnabled = paused;
        _stepIntoButton.IsEnabled = paused;
        _stepOutButton.IsEnabled = paused;
        _restartButton.IsEnabled = !idle;
        _stopButton.IsEnabled = !idle;

        if (idle)
            SetStatus("Ouvrez un fichier Python, cliquez à gauche d'un numéro de ligne pour poser un point d'arrêt, puis F5.");
        else if (running)
            SetStatus(state == DebugState.Starting ? "Démarrage…" : "En cours d'exécution…  (F6 pour mettre en pause)");
    }

    public void SetStatus(string text, bool error = false)
    {
        _status.Text = text;
        _status.Foreground = error ? Theme.Red : Theme.Muted;
    }

    // =====================================================================
    // Variables
    // =====================================================================

    public void ClearVariables(string message)
    {
        _variables.Items.Clear();
        _variables.Items.Add(new TreeViewItem
        {
            Header = new TextBlock { Text = message, Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap },
            Focusable = false
        });
    }

    public void ShowScopes(DebugScopes scopes)
    {
        _variables.Items.Clear();
        _variables.Items.Add(ScopeItem("Locales", scopes.Locals));

        if (scopes.Globals is not null)
            _variables.Items.Add(ScopeItem("Globales", scopes.Globals, expanded: false));
    }

    private TreeViewItem ScopeItem(string title, IReadOnlyList<DebugVariable> variables, bool expanded = true)
    {
        var item = new TreeViewItem
        {
            Header = new TextBlock { Text = title, Foreground = Theme.Muted, FontWeight = FontWeights.SemiBold },
            IsExpanded = expanded,
            Foreground = Theme.Text
        };

        if (variables.Count == 0)
            item.Items.Add(new TreeViewItem { Header = new TextBlock { Text = "(aucune)", Foreground = Theme.Muted } });

        foreach (DebugVariable variable in variables)
            item.Items.Add(VariableItem(variable));

        return item;
    }

    private TreeViewItem VariableItem(DebugVariable variable)
    {
        var header = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        header.Inlines.Add(new Run(variable.Name) { Foreground = NameBrush });
        header.Inlines.Add(new Run(" = ") { Foreground = Theme.Muted });
        header.Inlines.Add(new Run(variable.Value) { Foreground = ValueBrush });

        var item = new TreeViewItem
        {
            Header = header,
            Foreground = Theme.Text,
            ToolTip = $"{variable.Name} ({variable.Type})\n{variable.Value}"
        };

        // Contenu chargé seulement quand on déplie (listes, dictionnaires, objets)
        if (variable.HasChildren)
        {
            item.Items.Add(Placeholder);
            item.Expanded += async (_, e) =>
            {
                if (!ReferenceEquals(e.OriginalSource, item))
                    return;

                if (item.Items.Count != 1 || item.Items[0] as string != Placeholder || LoadChildren is null)
                    return;

                try
                {
                    IReadOnlyList<DebugVariable> children = await LoadChildren(variable.Reference);
                    item.Items.Clear();
                    foreach (DebugVariable child in children)
                        item.Items.Add(VariableItem(child));
                }
                catch (Exception ex)
                {
                    item.Items.Clear();
                    item.Items.Add(new TreeViewItem { Header = new TextBlock { Text = ex.Message, Foreground = Theme.Red } });
                }
            };
        }

        return item;
    }

    // =====================================================================
    // Espions
    // =====================================================================

    /// <summary>Affiche les espions ; results = null quand le programme n'est pas en pause.</summary>
    public void RenderWatches(IReadOnlyList<(string Text, bool IsError)>? results)
    {
        _watchRows.Children.Clear();

        if (_watches.Count == 0)
        {
            _watchRows.Children.Add(new TextBlock
            {
                Text = "Ajoutez une expression ci-dessous.",
                Foreground = Theme.Muted,
                FontSize = 11,
                Margin = new Thickness(2, 0, 0, 4)
            });
            return;
        }

        for (int i = 0; i < _watches.Count; i++)
        {
            string expression = _watches[i];
            (string text, bool isError) = results is not null && i < results.Count ? results[i] : ("—", false);

            var line = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            line.Inlines.Add(new Run(expression) { Foreground = NameBrush });
            line.Inlines.Add(new Run(" = ") { Foreground = Theme.Muted });
            line.Inlines.Add(new Run(text) { Foreground = isError ? Theme.Red : ValueBrush });
            line.ToolTip = $"{expression} = {text}";

            Button remove = SmallButton("✕", "Retirer cet espion", () =>
            {
                _watches.Remove(expression);
                WatchesChanged?.Invoke();
            });

            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 2) };
            SetDock(remove, Dock.Right);
            row.Children.Add(remove);
            row.Children.Add(line);
            _watchRows.Children.Add(row);
        }
    }

    // =====================================================================
    // Pile d'appels
    // =====================================================================

    public void ShowFrames(IReadOnlyList<DebugFrame> frames)
    {
        _updatingCallStack = true;
        _callStack.Items.Clear();

        foreach (DebugFrame frame in frames)
            _callStack.Items.Add(frame);

        if (frames.Count > 0)
            _callStack.SelectedIndex = 0;

        _updatingCallStack = false;
    }

    public void ClearFrames() => ShowFrames([]);

    // =====================================================================
    // Points d'arrêt
    // =====================================================================

    public void AttachStore(BreakpointStore store)
    {
        Store = store;
        store.Changed += _ => RenderBreakpoints();
        RenderBreakpoints();
    }

    private void RenderBreakpoints()
    {
        _breakpoints.Items.Clear();
        if (Store is not { } store)
            return;

        foreach (Breakpoint breakpoint in store.All)
        {
            var check = new CheckBox
            {
                IsChecked = breakpoint.Enabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                ToolTip = "Activer / désactiver"
            };
            check.Click += (_, _) => store.SetEnabled(breakpoint, check.IsChecked == true);

            var text = new TextBlock
            {
                Text = breakpoint.ToString(),
                Foreground = breakpoint.Verified ? Theme.Text : Theme.Muted,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = $"{breakpoint.FilePath}\nLigne {breakpoint.Line}"
                          + (breakpoint.Verified ? "" : "\nLigne refusée par Python (vide ou commentaire ?)")
                          + "\nDouble-clic : aller à la ligne"
            };

            Button remove = SmallButton("✕", "Supprimer ce point d'arrêt", () => store.Remove(breakpoint));

            var row = new DockPanel();
            SetDock(check, Dock.Left);
            SetDock(remove, Dock.Right);
            row.Children.Add(check);
            row.Children.Add(remove);
            row.Children.Add(text);

            _breakpoints.Items.Add(new ListBoxItem { Content = row, Tag = breakpoint, Foreground = Theme.Text });
        }

        if (_breakpoints.Items.Count == 0)
        {
            _breakpoints.Items.Add(new ListBoxItem
            {
                Content = new TextBlock { Text = "Aucun point d'arrêt.", Foreground = Theme.Muted },
                IsEnabled = false
            });
        }
    }

    // =====================================================================
    // Petits outils
    // =====================================================================

    private static UIElement SectionHeader(string title, Button? action)
    {
        var header = new DockPanel { Margin = new Thickness(0, 8, 0, 3) };

        if (action is not null)
        {
            SetDock(action, Dock.Right);
            header.Children.Add(action);
        }

        header.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = Theme.Muted,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });

        return header;
    }

    private static void Add(Grid grid, UIElement element, int row)
    {
        Grid.SetRow(element, row);
        grid.Children.Add(element);
    }

    private static void StyleList(Control list)
    {
        list.Background = Brushes.Transparent;
        list.BorderThickness = new Thickness(0);
        list.Foreground = Theme.Text;
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
    }

    private static Button MakeButton(string content, string toolTip, Action action)
    {
        var button = new Button
        {
            Content = content,
            ToolTip = toolTip,
            Template = FlatTemplate,
            Background = Theme.Surface,
            Foreground = Theme.Text,
            Padding = new Thickness(6, 4, 6, 4),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Cursor = Cursors.Hand,
            Focusable = false
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static Button IconButton(string glyph, string toolTip, Action action, Brush? color)
    {
        Button button = MakeButton(glyph, toolTip, action);
        button.Background = Brushes.Transparent;
        button.Foreground = color ?? Theme.Text;
        button.FontSize = 15;
        button.Width = 30;
        button.Height = 28;
        button.Padding = new Thickness(0);
        button.Margin = new Thickness(0, 0, 2, 0);
        return button;
    }

    private static Button SmallButton(string glyph, string toolTip, Action action)
    {
        Button button = MakeButton(glyph, toolTip, action);
        button.Background = Brushes.Transparent;
        button.Foreground = Theme.Muted;
        button.FontSize = 10;
        button.Width = 20;
        button.Height = 20;
        button.Padding = new Thickness(0);
        return button;
    }

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
