using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using KEYCE_KODER.App.Actions;
using KEYCE_KODER.App.Commands;
using KEYCE_KODER.App.Debugging;
using KEYCE_KODER.App.Menus;
using KEYCE_KODER.App.Pdf;
using KEYCE_KODER.App.Search;
using KEYCE_KODER.App.Services;
using KEYCE_KODER.App.Status;
using KEYCE_KODER.App.Tabs;
using KEYCE_KODER.App.Tools;
using KEYCE_KODER.App.Views;
using KEYCE_KODER.Core.Models;
using KEYCE_KODER.Core.Services;
using KEYCE_KODER.Editor.Controls;
using KEYCE_KODER.Editor.Services;
using KEYCE_KODER.Runner;
using KEYCE_KODER.Runner.Debugging;
using KEYCE_KODER.Terminal;

namespace KEYCE_KODER.App;

public class MainWindow : Window
{
    // Onglets d'édition : un onglet par fichier ouvert
    private readonly TabControl _tabControl;
    private readonly TabManager _tabManager;
    private readonly FileActions _fileActions;
    private readonly EditActions _editActions;
    private readonly MainMenu _mainMenu;
    private readonly EditorStatusBar _statusBar;
    private FindReplaceWindow? _findReplaceWindow;
    private ToolsWindow? _toolsWindow;

    // Débogueur (points d'arrêt, pas à pas, variables...)
    private readonly BreakpointStore _breakpoints = new();
    private readonly DebugController _debugger;

    // Lecteurs PDF ouverts (un onglet par PDF)
    private readonly List<PdfViewer> _pdfViewers = new();
    private bool _closeConfirmed;
    private readonly DocumentManager _documentManager;
    private readonly FileService _fileService;
    private readonly DocumentService _documentService;

    // Exécution : Runner multi-langages + terminal intégré + panneau inférieur
    private readonly ScriptRunner _runner;
    private readonly TerminalSession _terminal;
    private readonly BottomPanel _bottomPanel;

    // Gestion du dimensionnement du panneau inférieur
    private readonly Grid _editorArea = new();
    private readonly GridSplitter _splitter;
    private readonly RowDefinition _editorRow = new() { Height = new GridLength(1, GridUnitType.Star), MinHeight = 120 };
    private readonly RowDefinition _splitterRow = new() { Height = GridLength.Auto };
    private readonly RowDefinition _bottomRow = new() { Height = new GridLength(220), MinHeight = 60 };
    private bool _bottomPanelMaximized;
    private GridLength _savedBottomHeight = new(220);

    public MainWindow()
    {
        // Application du thème sauvegardé
        Theme.Apply(AppSettings.Current.ThemeName);

        Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico"));

        Title = "KEYCE KODER";
        Width = 1200;
        Height = 750;
        MinWidth = 900;
        MinHeight = 600;

        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Theme.Window;
        Foreground = Theme.Text;

        // Initialisation des services Core
        _documentManager = new DocumentManager();
        _fileService = new FileService();

        _documentService = new DocumentService(_documentManager, _fileService);

        // Onglets d'édition : un onglet par fichier ouvert
        _tabControl = new TabControl
        {
            Background = Theme.Editor,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0)
        };
        _tabManager = new TabManager(_tabControl);

        // Actions du menu Fichier (nouveau, ouvrir, enregistrer, fermer...)
        _fileActions = new FileActions(
            _documentService,
            _documentManager,
            _tabManager,
            new WpfDialogService(this));

        // Les .pdf s'ouvrent dans le lecteur PDF au lieu de l'éditeur de texte
        _fileActions.OpenSpecialFileAsync = OpenPdfAsync;

        // Menu Édition, barre de menus et barre d'état
        _editActions = new EditActions(_tabManager);
        _mainMenu = new MainMenu(_fileActions, _editActions, this);

        // Recherche / remplacement dans le menu Édition
        _mainMenu.EditMenu.Items.Add(new Separator());
        _mainMenu.EditMenu.Items.Add(_mainMenu.Item("Rec_hercher…", "Ctrl+F",
            () => ShowFind(replaceMode: false), () => _editActions.HasEditor));
        _mainMenu.EditMenu.Items.Add(_mainMenu.Item("Rechercher su_ivant", "F3",
            FindNext, () => _editActions.HasEditor));
        _mainMenu.EditMenu.Items.Add(_mainMenu.Item("Rechercher _précédent", "Maj+F3",
            FindPrevious, () => _editActions.HasEditor));
        _mainMenu.EditMenu.Items.Add(_mainMenu.Item("Re_mplacer…", "Ctrl+H",
            () => ShowFind(replaceMode: true), () => _editActions.HasEditor));

        // Menu Outils : installation des langages (Python, Node.js, JDK, GCC...)
        var toolsMenu = new MenuItem { Header = "_Outils" };
        toolsMenu.Items.Add(_mainMenu.Item("_Langages et outils…", "", () => _ = ShowToolsAsync()));
        _mainMenu.Items.Add(toolsMenu);

        _statusBar = new EditorStatusBar();

        // Titre et barre d'état toujours à jour
        _tabManager.ActiveTabChanged += (_, _) => UpdateWindowState();
        _tabManager.ActiveEditorStateChanged += (_, _) => UpdateWindowState();

        // Document Python de démonstration, ouvert dans le premier onglet
        var demo = _documentService.CreateDocument();
        demo.Content =
            "def saluer(nom):\n" +
            "    print(f\"Bonjour {nom} !\")\n\n" +
            "saluer(\"KEYCE KODER\")";
        demo.MarkSaved();

        var demoTab = _tabManager.Open(demo);
        demoTab.EditorService.SetLanguage("main.py");

        // Exécution sur le thread UI
        var uiContext = new DispatcherSynchronizationContext(Dispatcher);

        _runner = new ScriptRunner { UiContext = uiContext };
        _terminal = new TerminalSession(
            TerminalShell.PowerShell,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
        {
            UiContext = uiContext
        };

        // Création des panneaux de navigation (ActivityBar 44px, Sidebar 220px)
        var activityBar = new ActivityBar();
        var sidebar = new Sidebar();

        _bottomPanel = new BottomPanel(
            _runner,
            _terminal,
            PrepareScriptAsync,
            () => sidebar.Explorer.CurrentFolder);

        // Navigation entre les vues
        activityBar.ViewSelected += id =>
        {
            if (id == "settings")
            {
                ShowSettings();
            }
            else
            {
                sidebar.ShowView(id);
            }
        };

        sidebar.OpenToolsRequested += () => _ = ShowToolsAsync();
        sidebar.Settings.OpenFullSettingsRequested += ShowSettings;
        sidebar.Settings.ThemeSelected += themeId =>
        {
            Theme.Apply(themeId);
            RefreshAllTabs();
        };

        // Terminal : clic sur la branche Git de l'invite → vue Contrôle de source
        _bottomPanel.GitViewRequested += () => activityBar.SelectView("git");

        // Contrôles du panneau inférieur (Fermeture, Agrandissement)
        _bottomPanel.CloseRequested += () => SetBottomPanelVisible(false);
        _bottomPanel.MaximizeRequested += ToggleMaximizeBottomPanel;

        // Débogueur : vue « Exécuter et déboguer » + onglet « CONSOLE DE DÉBOGAGE »
        var debugView = new DebugView();
        var debugConsole = new DebugConsole();
        sidebar.AddView("debug", "EXÉCUTER ET DÉBOGUER", debugView);
        _bottomPanel.SetDebugConsole(debugConsole);

        _debugger = new DebugController(
            this,
            _tabManager,
            _fileActions,
            _breakpoints,
            debugView,
            debugConsole,
            PrepareScriptAsync,
            () =>
            {
                activityBar.SelectView("debug");
                _bottomPanel.ShowDebugConsole();
                SetBottomPanelVisible(true);
            });

        _debugger.AttachEditors();
        _tabManager.ActiveTabChanged += (_, _) => _debugger.AttachEditors();

        // Ouverture des fichiers depuis l'Explorateur
        sidebar.Explorer.FileOpenRequested += path => _ = _fileActions.OpenPathAsync(path);

        // Contrôle de source (Git)
        sidebar.SourceControl.FileOpenRequested += path => _ = _fileActions.OpenPathAsync(path);
        sidebar.SourceControl.WorkingTreeChanged += ReloadTabsFromDisk;

        // Recherche dans les fichiers
        sidebar.Search.MatchOpenRequested += async (path, index, length) =>
        {
            await _fileActions.OpenPathAsync(path);

            EditorTab? tab = _tabManager.FindByPath(path);
            if (tab is null) return;

            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() => tab.Actions.Select(index, length)));
        };

        // Zone centrale : éditeur en haut, séparateur, panneau inférieur en bas
        _editorArea.RowDefinitions.Add(_editorRow);
        _editorArea.RowDefinitions.Add(_splitterRow);
        _editorArea.RowDefinitions.Add(_bottomRow);

        _splitter = new GridSplitter
        {
            Height = 4,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            ResizeDirection = GridResizeDirection.Rows,
            Background = Theme.Window
        };

        Grid.SetRow(_tabControl, 0);
        Grid.SetRow(_splitter, 1);
        Grid.SetRow(_bottomPanel, 2);

        _editorArea.Children.Add(_tabControl);
        _editorArea.Children.Add(_splitter);
        _editorArea.Children.Add(_bottomPanel);

        // Style des panneaux arrondis Astou
        activityBar.CornerRadius = new CornerRadius(8);
        activityBar.Margin = new Thickness(4, 4, 0, 4);

        sidebar.CornerRadius = new CornerRadius(8);
        sidebar.BorderThickness = new Thickness(1);
        sidebar.Margin = new Thickness(4, 4, 0, 4);

        var editorHost = new Border
        {
            Background = Theme.Editor,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(4),
            ClipToBounds = true,
            Child = _editorArea
        };

        // Mise en page horizontale principale : Activity Bar | Sidebar | Editeur
        var mainGrid = new Grid();
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(activityBar, 0);
        Grid.SetColumn(sidebar, 1);
        Grid.SetColumn(editorHost, 2);

        mainGrid.Children.Add(activityBar);
        mainGrid.Children.Add(sidebar);
        mainGrid.Children.Add(editorHost);

        // Construction des menus pour la TopBar Astou
        MenuItem MakeItem(string header, Action action, string gesture = "", bool enabled = true)
        {
            var mi = new MenuItem { Header = header, InputGestureText = gesture, IsEnabled = enabled };
            mi.Click += (_, _) => action();
            return mi;
        }

        var selectMenu = new MenuItem { Header = "Sélection" };
        selectMenu.Items.Add(MakeItem("Tout sélectionner", () => _tabManager.ActiveTab?.Actions.SelectAll(), "Ctrl+A"));

        var viewMenu = new MenuItem { Header = "Affichage" };
        viewMenu.Items.Add(MakeItem("Explorateur", () => sidebar.ShowView("explorer")));
        viewMenu.Items.Add(MakeItem("Recherche", () => sidebar.ShowView("search")));
        viewMenu.Items.Add(MakeItem("Contrôle de source", () => sidebar.ShowView("git")));
        viewMenu.Items.Add(MakeItem("Exécuter et déboguer", () => sidebar.ShowView("debug")));
        viewMenu.Items.Add(MakeItem("Assistant IA", () => sidebar.ShowView("ai")));
        viewMenu.Items.Add(new Separator());
        viewMenu.Items.Add(MakeItem("Afficher / masquer la barre latérale", sidebar.Toggle, "Ctrl+B"));
        viewMenu.Items.Add(MakeItem("Afficher / masquer le panneau du bas", ToggleBottomPanelVisibility));

        var goMenu = new MenuItem { Header = "Accéder" };
        goMenu.Items.Add(MakeItem("Rechercher dans les fichiers…", () => sidebar.ShowView("search")));
        goMenu.Items.Add(MakeItem("Fichier suivant", _tabManager.ActivateNext, "Ctrl+Tab"));
        goMenu.Items.Add(MakeItem("Fichier précédent", _tabManager.ActivatePrevious, "Ctrl+Maj+Tab"));

        var runMenu = new MenuItem { Header = "Exécuter" };
        runMenu.Items.Add(MakeItem("Lancer le débogage (Python) / Run", () => _ = OnF5Async(), "F5"));
        runMenu.Items.Add(MakeItem("Exécuter sans débogage (Run)", _bottomPanel.RequestRun, "Ctrl+F5"));
        runMenu.Items.Add(MakeItem("Arrêter l'exécution", () =>
        {
            if (_debugger.IsActive) _debugger.Stop();
            else _bottomPanel.RequestStop();
        }, "Maj+F5"));

        var terminalMenu = new MenuItem { Header = "Terminal" };
        terminalMenu.Items.Add(MakeItem("Nouveau terminal", _bottomPanel.NewTerminal));
        terminalMenu.Items.Add(MakeItem("Afficher / masquer le panneau", ToggleBottomPanelVisibility));
        terminalMenu.Items.Add(new Separator());
        terminalMenu.Items.Add(MakeItem("Exécuter le fichier actif", _bottomPanel.RequestRun));
        terminalMenu.Items.Add(MakeItem("Arrêter l'exécution", _bottomPanel.RequestStop));

        var helpMenu = new MenuItem { Header = "Aide" };
        helpMenu.Items.Add(MakeItem("Paramètres…", ShowSettings));
        helpMenu.Items.Add(MakeItem("Langages et outils…", () => _ = ShowToolsAsync()));
        helpMenu.Items.Add(MakeItem("À propos de KEYCE KODER", () => MessageBox.Show(
            "KEYCE KODER — IDE WPF / .NET 10 développé par le groupe.\nÉdition AvalonEdit, Exécution multi-langages, Débogueur Python, Contrôle Git et Assistant IA.",
            "À propos de KEYCE KODER", MessageBoxButton.OK, MessageBoxImage.Information)));

        var topMenus = _mainMenu.Items.OfType<MenuItem>().ToList();
        topMenus.Add(selectMenu);
        topMenus.Add(viewMenu);
        topMenus.Add(goMenu);
        topMenus.Add(runMenu);

        var moreMenu = new MenuItem { Header = "⋯" };
        moreMenu.Items.Add(terminalMenu);
        moreMenu.Items.Add(helpMenu);
        topMenus.Add(moreMenu);

        // L'ancien menu standard est masqué, la TopBar moderne le remplace
        _mainMenu.Visibility = Visibility.Collapsed;

        var topBar = new TopBar(topMenus, () => sidebar.ShowView("search"));
        topBar.SettingsRequested += ShowSettings;

        // Structure DockPanel racine
        var root = new DockPanel();
        DockPanel.SetDock(topBar, Dock.Top);
        root.Children.Add(topBar);

        DockPanel.SetDock(_statusBar, Dock.Bottom);
        root.Children.Add(_statusBar);

        root.Children.Add(mainGrid);
        Content = root;

        // Raccourcis clavier globaux
        AddShortcut(sidebar.Toggle, Key.B, ModifierKeys.Control);
        AddShortcut(() => _ = OnF5Async(), Key.F5, ModifierKeys.None);
        AddShortcut(_bottomPanel.RequestRun, Key.F5, ModifierKeys.Control);
        AddShortcut(() =>
        {
            if (_debugger.IsActive) _debugger.Stop();
            else _bottomPanel.RequestStop();
        }, Key.F5, ModifierKeys.Shift);
        AddShortcut(() => _ = _debugger.RestartAsync(), Key.F5, ModifierKeys.Control | ModifierKeys.Shift);
        AddShortcut(_debugger.Pause, Key.F6, ModifierKeys.None);
        AddShortcut(_debugger.ToggleBreakpointAtCaret, Key.F9, ModifierKeys.None);
        AddShortcut(_debugger.StepInto, Key.F11, ModifierKeys.None);
        AddShortcut(_debugger.StepOut, Key.F11, ModifierKeys.Shift);
        AddShortcut(() => activityBar.SelectView("debug"), Key.D, ModifierKeys.Control | ModifierKeys.Shift);
        AddShortcut(ShowSettings, Key.OemComma, ModifierKeys.Control); // Ctrl+, pour Paramètres

        // F10 intercepté
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.System && e.SystemKey == Key.F10 &&
                Keyboard.Modifiers == ModifierKeys.None && _debugger.IsActive)
            {
                e.Handled = true;
                _debugger.StepOver();
            }
        };

        // Raccourcis Fichier & Onglets
        AddShortcut(_fileActions.NewFile, Key.N, ModifierKeys.Control);
        AddShortcut(async () => await _fileActions.OpenAsync(), Key.O, ModifierKeys.Control);
        AddShortcut(async () =>
        {
            if (ActivePdf is PdfViewer pdf) await pdf.SaveAsync();
            else await _fileActions.SaveAsync();
        }, Key.S, ModifierKeys.Control);
        AddShortcut(async () => await _fileActions.SaveAsAsync(), Key.S, ModifierKeys.Control | ModifierKeys.Shift);
        AddShortcut(async () =>
        {
            if (ActivePdf is PdfViewer pdf) await ClosePdfAsync(pdf);
            else await _fileActions.CloseActiveAsync();
        }, Key.W, ModifierKeys.Control);
        AddShortcut(_tabManager.ActivateNext, Key.Tab, ModifierKeys.Control);
        AddShortcut(_tabManager.ActivatePrevious, Key.Tab, ModifierKeys.Control | ModifierKeys.Shift);
        AddShortcut(() => ShowFind(replaceMode: false), Key.F, ModifierKeys.Control);
        AddShortcut(() => ShowFind(replaceMode: true), Key.H, ModifierKeys.Control);
        AddShortcut(FindNext, Key.F3, ModifierKeys.None);
        AddShortcut(FindPrevious, Key.F3, ModifierKeys.Shift);
        AddShortcut(_editActions.FoldAll, Key.F, ModifierKeys.Control | ModifierKeys.Alt);
        AddShortcut(_editActions.UnfoldAll, Key.U, ModifierKeys.Control | ModifierKeys.Alt);

        Closing += OnWindowClosing;
    }

    public void ShowSettings()
    {
        var win = new SettingsWindow(settings =>
        {
            RefreshAllTabs();
        })
        {
            Owner = this
        };
        win.ShowDialog();
    }

    private void RefreshAllTabs()
    {
        foreach (var tab in _tabManager.Tabs)
        {
            tab.ApplySettings(AppSettings.Current);
        }
    }

    private void SetBottomPanelVisible(bool visible)
    {
        _bottomPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        _splitter.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (visible)
        {
            _bottomRow.Height = _savedBottomHeight;
        }
        else
        {
            _savedBottomHeight = _bottomRow.Height;
            _bottomRow.Height = new GridLength(0);
        }
    }

    private void ToggleBottomPanelVisibility()
    {
        bool isVisible = _bottomPanel.Visibility == Visibility.Visible;
        SetBottomPanelVisible(!isVisible);
    }

    private void ToggleMaximizeBottomPanel()
    {
        _bottomPanelMaximized = !_bottomPanelMaximized;
        if (_bottomPanelMaximized)
        {
            _savedBottomHeight = _bottomRow.Height;
            _editorRow.Height = new GridLength(0);
            _bottomRow.Height = new GridLength(1, GridUnitType.Star);
        }
        else
        {
            _editorRow.Height = new GridLength(1, GridUnitType.Star);
            _bottomRow.Height = _savedBottomHeight;
        }
    }

    private async Task OnF5Async()
    {
        if (_debugger.IsActive)
        {
            _debugger.Continue();
            return;
        }

        EditorTab? tab = _tabManager.ActiveTab;
        string? path = tab?.Document.FilePath;

        if (tab is not null && (path is null || PythonDebugSession.Supports(path)))
            await _debugger.StartAsync();
        else
            _bottomPanel.RequestRun();
    }

    private async Task<string?> PrepareScriptAsync()
    {
        var tab = _tabManager.ActiveTab;
        if (tab is null)
        {
            MessageBox.Show(
                "Aucun fichier ouvert à exécuter.",
                "KEYCE KODER",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return null;
        }

        var editorService = tab.EditorService;
        var text = editorService.Text;
        var utf8 = new UTF8Encoding(false);

        bool untitled = string.IsNullOrWhiteSpace(editorService.FilePath);
        string path = untitled
            ? Path.Combine(Path.GetTempPath(), "KEYCE_KODER", "main.py")
            : editorService.FilePath!;

        LanguageProfile? profile = LanguageProfiles.ForFile(path);
        if (profile is null)
        {
            MessageBox.Show(
                $"Run ne sait pas exécuter les fichiers « {Path.GetExtension(path)} ».\n\n" +
                $"Langages pris en charge : {string.Join(" ", LanguageProfiles.SupportedExtensions)}",
                "KEYCE KODER",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return null;
        }

        if (!await EnsureToolInstalledAsync(profile))
            return null;

        if (untitled)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, text, utf8);
            return path;
        }

        if (editorService.IsModified)
        {
            await File.WriteAllTextAsync(path, text, utf8);
            editorService.MarkSaved(path);
            tab.RefreshHeader();
        }

        return path;
    }

    private async Task<bool> EnsureToolInstalledAsync(LanguageProfile profile)
    {
        ResolvedTool? tool = await ToolLocator.LocateAsync(profile.Tool);
        if (tool is not null)
            return true;

        MessageBoxResult answer = MessageBox.Show(
            this,
            $"{profile.Tool.DisplayName} est nécessaire pour exécuter du {profile.DisplayName}, "
            + "mais il n'est pas installé sur ce PC.\n\nVoulez-vous l'installer maintenant ?",
            "KEYCE KODER",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer == MessageBoxResult.Yes)
            await ShowToolsAsync(profile.Tool.Id);

        return false;
    }

    private async Task ShowToolsAsync(string? highlightToolId = null)
    {
        _toolsWindow ??= new ToolsWindow(this);
        await _toolsWindow.ShowForAsync(highlightToolId);
    }

    private void ReloadTabsFromDisk()
    {
        foreach (EditorTab tab in _tabManager.Tabs)
        {
            string? path = tab.Document.FilePath;
            if (path is null || tab.Document.IsDirty || !File.Exists(path))
                continue;

            try
            {
                string content = File.ReadAllText(path);
                if (content == tab.EditorService.Text)
                    continue;

                tab.EditorService.Text = content;
                tab.EditorService.MarkSaved();
                tab.RefreshHeader();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        UpdateWindowState();
    }

    private PdfViewer? ActivePdf => (_tabControl.SelectedItem as TabItem)?.Tag as PdfViewer;

    private async Task<bool> OpenPdfAsync(string path)
    {
        if (!PdfViewer.IsPdf(path))
            return false;

        string fullPath = Path.GetFullPath(path);

        PdfViewer? existing = _pdfViewers.FirstOrDefault(v =>
            string.Equals(v.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            _tabControl.SelectedItem = existing.TabItem;
            return true;
        }

        PdfViewer? viewer = await PdfViewer.OpenAsync(fullPath, this);
        if (viewer is null)
            return true;

        viewer.CloseRequested += pdf => _ = ClosePdfAsync(pdf);
        viewer.StateChanged += _ => UpdateWindowState();

        _pdfViewers.Add(viewer);
        _tabControl.Items.Add(viewer.TabItem);
        _tabControl.SelectedItem = viewer.TabItem;
        return true;
    }

    private async Task<bool> ClosePdfAsync(PdfViewer viewer)
    {
        if (viewer.IsModified)
        {
            MessageBoxResult answer = MessageBox.Show(
                this,
                $"Enregistrer les modifications de « {Path.GetFileName(viewer.FilePath)} » ?",
                "KEYCE KODER",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (answer == MessageBoxResult.Cancel)
                return false;

            if (answer == MessageBoxResult.Yes && !await viewer.SaveAsync())
                return false;
        }

        _pdfViewers.Remove(viewer);
        _tabControl.Items.Remove(viewer.TabItem);
        viewer.Cleanup();
        UpdateWindowState();
        return true;
    }

    private async Task<bool> ConfirmClosePdfsAsync()
    {
        foreach (PdfViewer viewer in _pdfViewers.Where(v => v.IsModified).ToList())
        {
            _tabControl.SelectedItem = viewer.TabItem;
            if (!await ClosePdfAsync(viewer))
                return false;
        }
        return true;
    }

    private void UpdateWindowState()
    {
        if (ActivePdf is PdfViewer pdf)
        {
            Title = $"{(pdf.IsModified ? "● " : "")}{Path.GetFileName(pdf.FilePath)} — KEYCE KODER";
            _statusBar.Update(null);
            return;
        }

        EditorTab? tab = _tabManager.ActiveTab;

        Title = tab is null
            ? "KEYCE KODER"
            : $"{(tab.Document.IsDirty ? "● " : "")}{tab.Document.Name} — KEYCE KODER";

        _statusBar.Update(tab);
    }

    private FindReplaceWindow GetFindReplaceWindow()
    {
        return _findReplaceWindow ??= new FindReplaceWindow(this, () => _tabManager.ActiveTab?.Actions);
    }

    private void ShowFind(bool replaceMode) => GetFindReplaceWindow().ShowFor(replaceMode);

    private void FindNext()
    {
        FindReplaceWindow find = GetFindReplaceWindow();
        if (find.Query.Length == 0) find.ShowFor(replaceMode: false); else find.FindNext();
    }

    private void FindPrevious()
    {
        FindReplaceWindow find = GetFindReplaceWindow();
        if (find.Query.Length == 0) find.ShowFor(replaceMode: false); else find.FindPrevious();
    }

    private void AddShortcut(Action action, Key key, ModifierKeys modifiers)
    {
        InputBindings.Add(new KeyBinding(new RelayCommand(action), key, modifiers));
    }

    private async void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (!_closeConfirmed)
        {
            if (AppSettings.Current.ConfirmOnExit)
            {
                e.Cancel = true;

                if (!await ConfirmClosePdfsAsync())
                    return;

                if (!await _fileActions.ConfirmCloseAllAsync())
                    return;

                _closeConfirmed = true;

                if (_findReplaceWindow != null)
                    _findReplaceWindow.AllowClose = true;

                if (_toolsWindow != null)
                    _toolsWindow.AllowClose = true;

                _ = Dispatcher.BeginInvoke(new Action(Close));
                return;
            }
        }

        _debugger.Stop();
        _runner.Stop();
        _terminal.Dispose();

        foreach (PdfViewer viewer in _pdfViewers)
            viewer.Cleanup();
    }
}
