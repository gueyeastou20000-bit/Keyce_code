using System;
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
    private bool _closeConfirmed;
    private readonly DocumentManager _documentManager;
    private readonly FileService _fileService;
    private readonly DocumentService _documentService;

    // Exécution : Runner Python + terminal intégré + panneau inférieur
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
        Background = Theme.Window;
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

        // Ouverture des fichiers depuis l'Explorateur
        sidebar.Explorer.FileOpenRequested +=
            path => _ = _fileActions.OpenPathAsync(path);

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
            Background = Theme.Window
        };

        Grid.SetRow(_tabControl, 0);
        Grid.SetRow(splitter, 1);
        Grid.SetRow(_bottomPanel, 2);

        editorArea.Children.Add(_tabControl);
        editorArea.Children.Add(splitter);
        editorArea.Children.Add(_bottomPanel);

        // Panneaux arrondis séparés par de petits espaces
        activityBar.CornerRadius = new CornerRadius(8);
        activityBar.BorderThickness = new Thickness(0);
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
            Child = editorArea
        };

        Grid.SetColumn(activityBar, 0);
        Grid.SetColumn(sidebar, 1);
        Grid.SetColumn(editorHost, 2);

        grid.Children.Add(activityBar);
        grid.Children.Add(sidebar);
        grid.Children.Add(editorHost);

        // Barre du haut, barre d'état en bas, le reste au centre
        var root = new DockPanel();

        // ---------- Menus supplémentaires (en plus de Fichier et Édition) ----------
        MenuItem Item(string header, Action action, string gesture = "", bool enabled = true)
        {
            var mi = new MenuItem { Header = header, InputGestureText = gesture, IsEnabled = enabled };
            mi.Click += (_, _) => action();
            return mi;
        }

        // Sélection
        var selectMenu = new MenuItem { Header = "Sélection" };
        selectMenu.Items.Add(Item("Tout sélectionner",
            () => _tabManager.ActiveTab?.Actions.SelectAll(), "Ctrl+A"));

        // Affichage
        var viewMenu = new MenuItem { Header = "Affichage" };
        viewMenu.Items.Add(Item("Explorateur", () => sidebar.ShowView("explorer")));
        viewMenu.Items.Add(Item("Recherche", () => sidebar.ShowView("search")));
        viewMenu.Items.Add(new Separator());
        viewMenu.Items.Add(Item("Afficher / masquer la barre latérale", sidebar.Toggle, "Ctrl+B"));
        viewMenu.Items.Add(Item("Afficher / masquer le panneau du bas", _bottomPanel.TogglePanel));

        // Accéder
        var goMenu = new MenuItem { Header = "Accéder" };
        goMenu.Items.Add(Item("Rechercher dans les fichiers…", () => sidebar.ShowView("search")));
        goMenu.Items.Add(Item("Fichier suivant", _tabManager.ActivateNext, "Ctrl+Tab"));
        goMenu.Items.Add(Item("Fichier précédent", _tabManager.ActivatePrevious, "Ctrl+Maj+Tab"));

        // Exécuter
        var runMenu = new MenuItem { Header = "Exécuter" };
        runMenu.Items.Add(Item("Lancer le fichier actif", _bottomPanel.RequestRun, "F5"));
        runMenu.Items.Add(Item("Arrêter l'exécution", _bottomPanel.RequestStop, "Maj+F5"));

        // Terminal
        var terminalMenu = new MenuItem { Header = "Terminal" };
        terminalMenu.Items.Add(Item("Nouveau terminal", _bottomPanel.NewTerminal));
        terminalMenu.Items.Add(Item("Afficher / masquer le panneau", _bottomPanel.TogglePanel));
        terminalMenu.Items.Add(new Separator());
        terminalMenu.Items.Add(Item("Exécuter le fichier actif", _bottomPanel.RequestRun));
        terminalMenu.Items.Add(Item("Arrêter l'exécution", _bottomPanel.RequestStop));

        // Aide
        var helpMenu = new MenuItem { Header = "Aide" };
        helpMenu.Items.Add(Item("À propos de KEYCE KODER", () => MessageBox.Show(
            "KEYCE KODER : éditeur de code développé par le groupe.",
            "À propos", MessageBoxButton.OK, MessageBoxImage.Information)));

        // Boutons visibles : Fichier, Édition, Sélection, Affichage, Accéder, Exécuter
        var menus = _mainMenu.Items.OfType<MenuItem>().ToList();
        menus.Add(selectMenu);
        menus.Add(viewMenu);
        menus.Add(goMenu);
        menus.Add(runMenu);

        // Bouton ⋯ : le reste (Terminal, Aide), avec sous-menus. On peut en ajouter d'autres ici.
        var moreMenu = new MenuItem { Header = "⋯" };
        moreMenu.Items.Add(terminalMenu);
        moreMenu.Items.Add(helpMenu);
        menus.Add(moreMenu);

        // L'ancien menu blanc est caché, la barre du haut le remplace
        _mainMenu.Visibility = Visibility.Collapsed;
        root.Children.Add(_mainMenu);

        var topBar = new TopBar(menus, () => sidebar.ShowView("search"));
        DockPanel.SetDock(topBar, Dock.Top);
        root.Children.Add(topBar);

        DockPanel.SetDock(_statusBar, Dock.Bottom);
        root.Children.Add(_statusBar);

        root.Children.Add(grid); // le dernier élément prend toute la place restante

        Content = root;

        // Raccourci Ctrl+B : afficher ou masquer la Sidebar
        InputBindings.Add(new KeyBinding(
            new RelayCommand(sidebar.Toggle),
            Key.B,
            ModifierKeys.Control));

        // F5 : exécuter le fichier actif / Maj+F5 : arrêter
        InputBindings.Add(new KeyBinding(
            new RelayCommand(_bottomPanel.RequestRun),
            Key.F5,
            ModifierKeys.None));

        InputBindings.Add(new KeyBinding(
            new RelayCommand(_bottomPanel.RequestStop),
            Key.F5,
            ModifierKeys.Shift));

        // Raccourcis du menu Fichier et des onglets
        AddShortcut(_fileActions.NewFile, Key.N, ModifierKeys.Control);
        AddShortcut(async () => await _fileActions.OpenAsync(), Key.O, ModifierKeys.Control);
        AddShortcut(async () => await _fileActions.SaveAsync(), Key.S, ModifierKeys.Control);
        AddShortcut(async () => await _fileActions.SaveAsAsync(), Key.S, ModifierKeys.Control | ModifierKeys.Shift);
        AddShortcut(async () => await _fileActions.CloseActiveAsync(), Key.W, ModifierKeys.Control);
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

    /// <summary>Enregistre le fichier actif si nécessaire et renvoie le chemin à exécuter.</summary>
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
        var path = editorService.FilePath;
        var text = editorService.Text;
        var utf8 = new UTF8Encoding(false);

        // Document sans fichier (démo ou nouveau) : copie temporaire
        if (string.IsNullOrWhiteSpace(path))
        {
            var dir = Path.Combine(Path.GetTempPath(), "KEYCE_KODER");
            Directory.CreateDirectory(dir);

            path = Path.Combine(dir, "main.py");
            await File.WriteAllTextAsync(path, text, utf8);
            return path;
        }

        if (!string.Equals(Path.GetExtension(path), ".py", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                "Run n'exécute que les fichiers Python (.py).",
                "KEYCE KODER",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return null;
        }

        // Sauvegarde avant exécution
        if (editorService.IsModified)
        {
            await File.WriteAllTextAsync(path, text, utf8);
            editorService.MarkSaved(path);
            tab.RefreshHeader(); // le ● disparaît de l'onglet
        }

        return path;
    }

    private void UpdateWindowState()
    {
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

            if (!await _fileActions.ConfirmCloseAllAsync())
                return; // l'utilisateur a annulé : on reste ouvert

            _closeConfirmed = true;

            if (_findReplaceWindow != null)
                _findReplaceWindow.AllowClose = true;

            // Close() est interdit pendant l'événement Closing : on le reporte
            _ = Dispatcher.BeginInvoke(new Action(Close));
            return;
        }

        // Fermeture confirmée : plus aucun processus Python ou shell orphelin
        _runner.Stop();
        _terminal.Dispose();
    }
}