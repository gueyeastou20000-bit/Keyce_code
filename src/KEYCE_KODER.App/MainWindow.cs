using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using KEYCE_KODER.App.Commands;
using KEYCE_KODER.App.Views;
using KEYCE_KODER.Core.Models;
using KEYCE_KODER.Core.Services;
using KEYCE_KODER.Editor.Controls;
using KEYCE_KODER.Editor.Services;
using KEYCE_KODER.Runner;
using KEYCE_KODER.Terminal;
using System.ComponentModel;
using KEYCE_KODER.App.Actions;
using KEYCE_KODER.App.Services;
using KEYCE_KODER.App.Menus;
using KEYCE_KODER.App.Status;
using KEYCE_KODER.App.Tabs;
using KEYCE_KODER.App.Search;
using KEYCE_KODER.App.Tools;
using KEYCE_KODER.App.Pdf;
using KEYCE_KODER.App.Debugging;
using KEYCE_KODER.Runner.Debugging;

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
   

    // Exécution : Runner (Python, JavaScript, Java, C, C++) + terminal intégré + panneau inférieur
    private readonly ScriptRunner _runner;
    private readonly TerminalSession _terminal;
    private readonly BottomPanel _bottomPanel;

   

    public MainWindow()
    {
        Icon = new BitmapImage(
            new Uri("pack://application:,,,/Assets/app.ico"));

        Title = "KEYCE KODER";
        Width = 1200;
        Height = 750;
        MinWidth = 900;
        MinHeight = 600;

        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        Foreground = Brushes.White;

        // Initialisation des services Core
        _documentManager = new DocumentManager();
        _fileService = new FileService();

        _documentService = new DocumentService(
            _documentManager,
            _fileService);

        // Onglets d'édition : un onglet par fichier ouvert
        _tabControl = new TabControl
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
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
        demo.MarkSaved(); // la démo n'est pas considérée comme « modifiée »

        var demoTab = _tabManager.Open(demo);
        demoTab.EditorService.SetLanguage("main.py");

        // Exécution (créés ici, sur le thread UI : les événements reviennent sur ce thread)
        var uiContext = new DispatcherSynchronizationContext(Dispatcher);

        _runner = new ScriptRunner { UiContext = uiContext };
        _terminal = new TerminalSession(
            TerminalShell.PowerShell,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
        {
            UiContext = uiContext
        };
        

        // Création des panneaux de navigation
        var activityBar = new ActivityBar();
        var sidebar = new Sidebar();

        _bottomPanel = new BottomPanel(
            _runner,
            _terminal,
            PrepareScriptAsync,
            () => sidebar.Explorer.CurrentFolder);

        // Navigation entre les vues
        activityBar.ViewSelected += sidebar.ShowView;

        // Terminal : clic sur la branche Git de l'invite → vue Contrôle de source
        _bottomPanel.GitViewRequested += () => activityBar.SelectView("git");

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
            });

        // Chaque onglet de code reçoit la marge des points d'arrêt
        _debugger.AttachEditors();
        _tabManager.ActiveTabChanged += (_, _) => _debugger.AttachEditors();

        // Ouverture des fichiers depuis l'Explorateur
        sidebar.Explorer.FileOpenRequested +=
    path => _ = _fileActions.OpenPathAsync(path);

        // Contrôle de source (Git) : ouvrir un fichier modifié, recharger les onglets après un pull
        sidebar.SourceControl.FileOpenRequested +=
            path => _ = _fileActions.OpenPathAsync(path);
        sidebar.SourceControl.WorkingTreeChanged += ReloadTabsFromDisk;

        // Recherche dans les fichiers : ouvrir le fichier et surligner l'occurrence
        sidebar.Search.MatchOpenRequested += async (path, index, length) =>
        {
            await _fileActions.OpenPathAsync(path);

            EditorTab? tab = _tabManager.FindByPath(path);
            if (tab is null)
                return; // ouverture impossible (le message d'erreur a déjà été affiché)

            // On attend que l'éditeur soit affiché avant de sélectionner le mot
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() => tab.Actions.Select(index, length)));
        };

        // Mise en page : Activity Bar | Sidebar | (Éditeur / Panneau inférieur)
        var grid = new Grid();

        grid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });

        // Zone centrale : éditeur en haut, panneau inférieur en bas, séparateur redimensionnable
        var editorArea = new Grid();

        editorArea.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star),
                MinHeight = 150
            });

        editorArea.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        editorArea.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(220),
                MinHeight = 80
            });

        var splitter = new GridSplitter
        {
            Height = 4,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            ResizeDirection = GridResizeDirection.Rows,
            Background = new SolidColorBrush(Color.FromRgb(60, 60, 60))
        };

        Grid.SetRow(_tabControl, 0);
        Grid.SetRow(splitter, 1);
        Grid.SetRow(_bottomPanel, 2);

        editorArea.Children.Add(_tabControl);
        editorArea.Children.Add(splitter);
        editorArea.Children.Add(_bottomPanel);

        Grid.SetColumn(activityBar, 0);
        Grid.SetColumn(sidebar, 1);
        Grid.SetColumn(editorArea, 2);

        grid.Children.Add(activityBar);
        grid.Children.Add(sidebar);
        grid.Children.Add(editorArea);

        // Menu en haut, barre d'état en bas, le reste (explorateur, onglets, panneau) au centre
        var root = new DockPanel();

        DockPanel.SetDock(_mainMenu, Dock.Top);
        root.Children.Add(_mainMenu);

        DockPanel.SetDock(_statusBar, Dock.Bottom);
        root.Children.Add(_statusBar);

        root.Children.Add(grid); // le dernier élément prend toute la place restante

        Content = root;

        // Raccourci Ctrl+B : afficher ou masquer la Sidebar
        InputBindings.Add(new KeyBinding(
            new RelayCommand(sidebar.Toggle),
            Key.B,
            ModifierKeys.Control));

        // Débogage (comme VS Code) :
        // F5 = déboguer (Python) ou continuer, Ctrl+F5 = exécuter sans débogage (Run),
        // Maj+F5 = arrêter, Ctrl+Maj+F5 = redémarrer, F6 = pause, F9 = point d'arrêt,
        // F10 = pas à pas principal, F11 = pas à pas détaillé, Maj+F11 = pas à pas sortant
        AddShortcut(() => _ = OnF5Async(), Key.F5, ModifierKeys.None);
        AddShortcut(_bottomPanel.RequestRun, Key.F5, ModifierKeys.Control);
        AddShortcut(() =>
        {
            if (_debugger.IsActive)
                _debugger.Stop();
            else
                _bottomPanel.RequestStop();
        }, Key.F5, ModifierKeys.Shift);
        AddShortcut(() => _ = _debugger.RestartAsync(), Key.F5, ModifierKeys.Control | ModifierKeys.Shift);
        AddShortcut(_debugger.Pause, Key.F6, ModifierKeys.None);
        AddShortcut(_debugger.ToggleBreakpointAtCaret, Key.F9, ModifierKeys.None);
        AddShortcut(_debugger.StepInto, Key.F11, ModifierKeys.None);
        AddShortcut(_debugger.StepOut, Key.F11, ModifierKeys.Shift);
        AddShortcut(() => activityBar.SelectView("debug"), Key.D, ModifierKeys.Control | ModifierKeys.Shift);

        // F10 est une touche « système » pour Windows (elle active le menu) : on l'intercepte à part
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.System && e.SystemKey == Key.F10
                && Keyboard.Modifiers == ModifierKeys.None && _debugger.IsActive)
            {
                e.Handled = true;
                _debugger.StepOver();
            }
        };



        // Raccourcis du menu Fichier et des onglets
        AddShortcut(_fileActions.NewFile, Key.N, ModifierKeys.Control);
        AddShortcut(async () => await _fileActions.OpenAsync(), Key.O, ModifierKeys.Control);
        AddShortcut(async () =>
        {
            if (ActivePdf is PdfViewer pdf)
                await pdf.SaveAsync();
            else
                await _fileActions.SaveAsync();
        }, Key.S, ModifierKeys.Control);
        AddShortcut(async () => await _fileActions.SaveAsAsync(), Key.S, ModifierKeys.Control | ModifierKeys.Shift);
        AddShortcut(async () =>
        {
            if (ActivePdf is PdfViewer pdf)
                await ClosePdfAsync(pdf);
            else
                await _fileActions.CloseActiveAsync();
        }, Key.W, ModifierKeys.Control);
        AddShortcut(_tabManager.ActivateNext, Key.Tab, ModifierKeys.Control);
        AddShortcut(_tabManager.ActivatePrevious, Key.Tab, ModifierKeys.Control | ModifierKeys.Shift);
        AddShortcut(() => ShowFind(replaceMode: false), Key.F, ModifierKeys.Control);
        AddShortcut(() => ShowFind(replaceMode: true), Key.H, ModifierKeys.Control);
        AddShortcut(FindNext, Key.F3, ModifierKeys.None);
        AddShortcut(FindPrevious, Key.F3, ModifierKeys.Shift);
        AddShortcut(_editActions.FoldAll, Key.F, ModifierKeys.Control | ModifierKeys.Alt);
        AddShortcut(_editActions.UnfoldAll, Key.U, ModifierKeys.Control | ModifierKeys.Alt);

        // Fermeture propre : plus aucun processus Python ou shell orphelin
        Closing += OnWindowClosing;
    }

    /// <summary>
    /// F5 : continue si le débogueur est en pause ; sinon débogue le fichier Python actif
    /// (ou un document sans titre, exécuté comme du Python) ; les autres langages sont exécutés avec Run.
    /// </summary>
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

    /// <summary>
    /// Prépare l'exécution du fichier actif : vérifie que le langage est pris en charge
    /// et que son outil est installé, enregistre si besoin, puis renvoie le chemin à exécuter
    /// (null = annuler l'exécution).
    /// </summary>
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

        // Document jamais enregistré (démo ou nouveau) : exécuté comme du Python, via une copie temporaire
        bool untitled = string.IsNullOrWhiteSpace(editorService.FilePath);
        string path = untitled
            ? Path.Combine(Path.GetTempPath(), "KEYCE_KODER", "main.py")
            : editorService.FilePath!;

        // 1. Le Runner sait-il exécuter ce type de fichier ? (Python, JavaScript, Java, C, C++...)
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

        // 2. L'outil nécessaire (python, node, java, gcc, g++) est-il installé ?
        if (!await EnsureToolInstalledAsync(profile))
            return null;

        // 3. Copie temporaire pour un document sans fichier
        if (untitled)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, text, utf8);
            return path;
        }

        // 4. Sauvegarde avant exécution
        if (editorService.IsModified)
        {
            await File.WriteAllTextAsync(path, text, utf8);
            editorService.MarkSaved(path);
            tab.RefreshHeader(); // le ● disparaît de l'onglet
        }

        return path;
    }

    /// <summary>
    /// Vérifie que l'outil du langage est installé. Sinon, propose de l'installer
    /// et ouvre la fenêtre « Langages et outils ». Retourne true si on peut exécuter.
    /// </summary>
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

    /// <summary>Ouvre la fenêtre « Langages et outils » (créée à la première utilisation).</summary>
    private async Task ShowToolsAsync(string? highlightToolId = null)
    {
        _toolsWindow ??= new ToolsWindow(this);
        await _toolsWindow.ShowForAsync(highlightToolId);
    }

    /// <summary>
    /// Git a modifié des fichiers sur le disque (pull, changement de branche, annulation) :
    /// les onglets sans modification non enregistrée sont rechargés avec le nouveau contenu.
    /// </summary>
    private void ReloadTabsFromDisk()
    {
        foreach (EditorTab tab in _tabManager.Tabs)
        {
            string? path = tab.Document.FilePath;

            // On ne touche jamais à un onglet modifié (●) : le travail non enregistré est conservé
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
                // Fichier verrouillé : l'onglet garde l'ancien contenu
            }
        }

        UpdateWindowState();
    }

    // =====================================================================
    // Lecteur PDF
    // =====================================================================

    /// <summary>Lecteur PDF de l'onglet actif (null si l'onglet actif est un fichier texte).</summary>
    private PdfViewer? ActivePdf => (_tabControl.SelectedItem as TabItem)?.Tag as PdfViewer;

    /// <summary>
    /// Appelé à chaque ouverture de fichier : si c'est un PDF, il s'ouvre dans le lecteur PDF.
    /// Retourne true si le fichier a été pris en charge ici.
    /// </summary>
    private async Task<bool> OpenPdfAsync(string path)
    {
        if (!PdfViewer.IsPdf(path))
            return false;

        string fullPath = Path.GetFullPath(path);

        // Déjà ouvert : on affiche simplement son onglet
        PdfViewer? existing = _pdfViewers.FirstOrDefault(v =>
            string.Equals(v.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            _tabControl.SelectedItem = existing.TabItem;
            return true;
        }

        PdfViewer? viewer = await PdfViewer.OpenAsync(fullPath, this);
        if (viewer is null)
            return true; // annulé ou erreur (message déjà affiché)

        viewer.CloseRequested += pdf => _ = ClosePdfAsync(pdf);
        viewer.StateChanged += _ => UpdateWindowState();

        _pdfViewers.Add(viewer);
        _tabControl.Items.Add(viewer.TabItem);
        _tabControl.SelectedItem = viewer.TabItem;
        return true;
    }

    /// <summary>Ferme un onglet PDF (en proposant d'enregistrer les modifications).</summary>
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

    /// <summary>Fermeture de l'application : chaque PDF modifié propose d'être enregistré.</summary>
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
        // Onglet PDF : titre de la fenêtre = nom du PDF
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

    // La fenêtre de recherche est créée à la première utilisation :
    // WPF exige que la fenêtre principale soit déjà affichée pour en être « propriétaire ».
    private FindReplaceWindow GetFindReplaceWindow()
    {
        return _findReplaceWindow ??= new FindReplaceWindow(this, () => _tabManager.ActiveTab?.Actions);
    }

    private void ShowFind(bool replaceMode)
    {
        GetFindReplaceWindow().ShowFor(replaceMode);
    }

    // F3 / Maj+F3 : si rien n'a encore été cherché, on ouvre la fenêtre de recherche
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
            // On bloque la fermeture le temps de demander « Enregistrer ? »
            e.Cancel = true;

            if (!await ConfirmClosePdfsAsync())
                return; // l'utilisateur a annulé

            if (!await _fileActions.ConfirmCloseAllAsync())
                return; // l'utilisateur a annulé : on reste ouvert

            _closeConfirmed = true;

            if (_findReplaceWindow != null)
                _findReplaceWindow.AllowClose = true;

            if (_toolsWindow != null)
                _toolsWindow.AllowClose = true;

            // Close() est interdit pendant l'événement Closing : on le reporte
            _ = Dispatcher.BeginInvoke(new Action(Close));
            return;
        }

        // Fermeture confirmée : plus aucun processus Python ou shell orphelin
        _debugger.Stop();
        _runner.Stop();
        _terminal.Dispose();

        foreach (PdfViewer viewer in _pdfViewers)
            viewer.Cleanup();
    }
}
