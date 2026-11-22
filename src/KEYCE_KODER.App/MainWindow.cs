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

using KEYCE_KODER.App.Tabs;

namespace KEYCE_KODER.App;

public class MainWindow : Window
{

    // Onglets d'édition : un onglet par fichier ouvert
    private readonly TabControl _tabControl;
    private readonly TabManager _tabManager;
    private readonly DocumentManager _documentManager;
    private readonly FileService _fileService;
    private readonly DocumentService _documentService;
   

    // Exécution : Runner Python + terminal intégré + panneau inférieur
    private readonly PythonRunner _runner;
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

        // Fermeture d'un onglet (× ou clic molette).
        // Version simple pour l'instant : la question « Enregistrer ? » arrive à l'étape 12.
        _tabManager.CloseRequested += (_, tab) =>
        {
            _tabManager.Remove(tab);
            _documentManager.CloseDocument(tab.Document.Id);
        };

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

        _runner = new PythonRunner { UiContext = uiContext };
        _terminal = new TerminalSession(
            TerminalShell.PowerShell,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
        {
            UiContext = uiContext
        };
        _bottomPanel = new BottomPanel(_runner, _terminal, PrepareScriptAsync);

        // Création des panneaux de navigation
        var activityBar = new ActivityBar();
        var sidebar = new Sidebar();

        // Navigation entre les vues
        activityBar.ViewSelected += sidebar.ShowView;

        // Ouverture des fichiers depuis l'Explorateur
        sidebar.Explorer.FileOpenRequested +=
            path => _ = OpenFileAsync(path);

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

        Content = grid;

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

        // Fermeture propre : plus aucun processus Python ou shell orphelin
        Closing += (_, _) =>
        {
            _runner.Stop();
            _terminal.Dispose();
        };
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

    private async Task OpenFileAsync(string path)
    {
        // Fichier déjà ouvert : on active simplement son onglet
        var existing = _tabManager.FindByPath(path);
        if (existing != null)
        {
            _tabManager.Activate(existing);
            return;
        }

        try
        {
            var document = await _documentService.OpenDocumentAsync(path);
            _tabManager.Open(document);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Impossible d'ouvrir le fichier :\n{ex.Message}",
                "Erreur d'ouverture",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
