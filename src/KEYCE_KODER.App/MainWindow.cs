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

namespace KEYCE_KODER.App;

public class MainWindow : Window
{
    private readonly CodeEditor _codeEditor;
    private readonly TextBlock _editorTitle = new();
    private readonly TextBlock _breadcrumb = new();
    private readonly TextBlock _statusBarText = new();
    private readonly StackPanel _tabStrip = new();

    private readonly DocumentManager _documentManager;
    private readonly FileService _fileService;
    private readonly DocumentService _documentService;
    private readonly EditorService _editorService;

    // Exécution : Runner (Python, JS, Java, C, C++) + terminal intégré + panneau inférieur
    private readonly ScriptRunner _runner;
    private readonly TerminalSession _terminal;
    private readonly BottomPanel _bottomPanel;

    private Document? _activeDocument;

    private readonly Sidebar _sidebar;

    private readonly Document _currentDocument;

    private readonly DispatcherTimer _autoSaveTimer = new();
    private readonly List<Document> _documentHistory = new();

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
        Foreground = Theme.Text;

        // Initialisation des services Core
        _documentManager = new DocumentManager();
        _fileService = new FileService();

        _documentService = new DocumentService(
            _documentManager,
            _fileService);

        // Initialisation de l'éditeur
        _codeEditor = new CodeEditor();
        _editorService = new EditorService(_codeEditor);

        _editorTitle.Text = "main.py";

        // Document Python de démonstration
        _currentDocument = _documentService.CreateDocument();
        _activeDocument = _currentDocument;

        _activeDocument.Content =
            "def saluer(nom):\n" +
            "    print(f\"Bonjour {nom} !\")\n\n" +
            "saluer(\"KEYCE KODER\")";

        _editorService.SetDocument(_activeDocument);
        _editorService.SetLanguage("main.py");

        // Exécution (créés ici, sur le thread UI : les événements reviennent sur ce thread)
        var uiContext = new DispatcherSynchronizationContext(Dispatcher);

        _runner = new ScriptRunner { UiContext = uiContext };
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
        _sidebar = sidebar;

        // Navigation entre les vues
        activityBar.ViewSelected += _sidebar.ShowView;

        // Ouverture des fichiers depuis l'Explorateur
        _sidebar.Explorer.FileOpenRequested +=
            path => _ = OpenFileAsync(path);
        _sidebar.Search.FileOpenRequested +=
            path => _ = OpenFileAsync(path);

        // Mise en page : barre supérieure + Activity Bar | Sidebar | zone centrale
        var grid = new Grid();

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });

        var editorArea = new Grid();

        editorArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
        editorArea.RowDefinitions.Add(new RowDefinition
        {
            Height = new GridLength(1, GridUnitType.Star),
            MinHeight = 150
        });
        editorArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        editorArea.RowDefinitions.Add(new RowDefinition
        {
            Height = new GridLength(220),
            MinHeight = 80
        });

        var editorHeader = new Border
        {
            Background = Theme.PanelHeader,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(0, 0, 0, 1)
        };

        _tabStrip.Orientation = Orientation.Horizontal;
        _tabStrip.VerticalAlignment = VerticalAlignment.Stretch;
        editorHeader.Child = _tabStrip;

        var splitter = new GridSplitter
        {
            Height = 5,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            ResizeDirection = GridResizeDirection.Rows,
            Background = Theme.GridLine
        };

        Grid.SetRow(editorHeader, 0);
        Grid.SetRow(_codeEditor, 1);
        Grid.SetRow(splitter, 2);
        Grid.SetRow(_bottomPanel, 3);

        editorArea.Children.Add(editorHeader);
        editorArea.Children.Add(_codeEditor);
        editorArea.Children.Add(splitter);
        editorArea.Children.Add(_bottomPanel);

        Grid.SetColumn(activityBar, 0);
        Grid.SetColumn(_sidebar, 1);
        Grid.SetColumn(editorArea, 2);

        grid.Children.Add(activityBar);
        grid.Children.Add(_sidebar);
        grid.Children.Add(editorArea);

        var statusBar = CreateStatusBar();

        var shell = new DockPanel();
        var topBar = CreateTopBar(_bottomPanel, _sidebar, () => OpenCommandPalette());
        DockPanel.SetDock(topBar, Dock.Top);
        DockPanel.SetDock(statusBar, Dock.Bottom);
        shell.Children.Add(topBar);
        shell.Children.Add(statusBar);
        shell.Children.Add(grid);

        Content = shell;

        _codeEditor.TextChanged += (_, _) =>
        {
            UpdateStatusBar();
            if (_activeDocument != null)
                BuildTabs(_activeDocument);
        };

        BuildTabs(_activeDocument);

        // Raccourci Ctrl+B : afficher ou masquer la Sidebar
        InputBindings.Add(new KeyBinding(
            new RelayCommand(_sidebar.Toggle),
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

        // Ctrl+Maj+P : palette de commandes
        InputBindings.Add(new KeyBinding(
            new RelayCommand(OpenCommandPalette),
            Key.P,
            ModifierKeys.Control | ModifierKeys.Shift));

        // Ctrl+S : enregistrer
        InputBindings.Add(new KeyBinding(
            new RelayCommand(() => _ = SaveActiveAsync()),
            Key.S,
            ModifierKeys.Control));

        // Ctrl+Tab : parcourir les documents ouverts
        InputBindings.Add(new KeyBinding(
            new RelayCommand(CycleDocuments),
            Key.Tab,
            ModifierKeys.Control));

        // Fermeture propre : plus aucun processus Python ou shell orphelin
        Closing += (_, _) =>
        {
            try
            {
                var folder = GetAppDataFolder();
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "layout.txt"), $"{Width};{Height};{_sidebar.Width}");
            }
            catch { }
            _autoSaveTimer.Stop();
            _runner.Stop();
            _terminal.Dispose();
        };

        _sidebar.Welcome.OpenFolderRequested += _sidebar.Explorer.ChooseFolder;
        _sidebar.Settings.ThemeSelected += theme =>
        {
            var folder = GetAppDataFolder();
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "theme.txt"), theme);
            Theme.Apply(theme);
            UpdateThemeReferences();
        };

        _sidebar.Explorer.CurrentPathChanged += folder =>
        {
            RememberRecentFolder(folder);
            _sidebar.Search.SetFolder(folder);
        };

        _codeEditor.CaretPositionChanged += (_, _) => UpdateStatusBar();
        SetupAutoSave();
        LoadRecentFolders();
        LoadLayout();

        LoadSavedTheme();
    }

    /// <summary>Enregistre le fichier actif si nécessaire et renvoie le chemin à exécuter.</summary>
    private async Task<string?> PrepareScriptAsync()
    {
        var path = _editorService.FilePath;
        var text = _editorService.Text;
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

        if (LanguageProfiles.ForFile(path) is null)
        {
            MessageBox.Show(
                $"Run ne prend pas en charge ce type de fichier.\nPris en charge : {string.Join(' ', LanguageProfiles.SupportedExtensions)}",
                "KEYCE KODER",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return null;
        }

        // Sauvegarde avant exécution
        if (_editorService.IsModified)
        {
            await File.WriteAllTextAsync(path, text, utf8);
            _editorService.MarkSaved(path);
        }

        return path;
    }

    private async Task OpenFileAsync(string path)
    {
        try
        {
            var document = await _documentService.OpenDocumentAsync(path);

            _editorService.SetDocument(document);
            _editorTitle.Text = Path.GetFileName(path);
            BuildTabs(document);
            UpdateStatusBar();
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

    private Border CreateTopBar(BottomPanel bottomPanel, Sidebar sidebar, Action openPalette)
    {
        var bar = new Border
        {
            Height = 42,
            Background = Theme.ActivityBar,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(0, 0, 0, 1)
        };

        var grid = new Grid { Margin = new Thickness(10, 0, 10, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var brand = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        brand.Children.Add(new TextBlock
        {
            Text = "KEYCE",
            Foreground = Theme.Text,
            FontWeight = FontWeights.Bold,
            FontSize = 13
        });
        brand.Children.Add(new TextBlock
        {
            Text = "  KODER",
            Foreground = Theme.Accent,
            FontWeight = FontWeights.Bold,
            FontSize = 13
        });

        var command = new Button
        {
            Content = "⌕  Rechercher une commande…",
            Width = 280,
            Height = 28,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Theme.Surface,
            Foreground = Theme.Muted,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand
        };
        command.Click += (_, _) => openPalette();

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        var menuButton = new Button
        {
            Content = "•••",
            Width = 34,
            Height = 28,
            Margin = new Thickness(0, 0, 6, 0),
            Background = Brushes.Transparent,
            Foreground = Theme.Muted,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = "Afficher ou masquer l'explorateur (Ctrl+B)"
        };
        menuButton.Click += (_, _) => sidebar.Toggle();

        var runButton = new Button
        {
            Content = "▶  Exécuter",
            Height = 28,
            Padding = new Thickness(12, 0, 12, 0),
            Background = Theme.Accent,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        runButton.Click += (_, _) => bottomPanel.RequestRun();

        actions.Children.Add(menuButton);
        actions.Children.Add(runButton);

        Grid.SetColumn(brand, 0);
        Grid.SetColumn(command, 1);
        Grid.SetColumn(actions, 2);
        grid.Children.Add(brand);
        grid.Children.Add(command);
        grid.Children.Add(actions);
        bar.Child = grid;
        return bar;
    }

    private Border CreateStatusBar()
    {
        var bar = new Border
        {
            Height = 24,
            Background = Theme.Accent,
            BorderBrush = Theme.Accent,
            BorderThickness = new Thickness(0)
        };

        var grid = new Grid { Margin = new Thickness(10, 0, 10, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _breadcrumb.Foreground = Brushes.White;
        _breadcrumb.FontSize = 11;
        _breadcrumb.VerticalAlignment = VerticalAlignment.Center;

        _statusBarText.Foreground = Brushes.White;
        _statusBarText.FontSize = 11;
        _statusBarText.VerticalAlignment = VerticalAlignment.Center;

        var language = new TextBlock
        {
            Text = "Python",
            Foreground = Brushes.White,
            FontSize = 11,
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };

        Grid.SetColumn(_breadcrumb, 0);
        Grid.SetColumn(language, 2);
        Grid.SetColumn(_statusBarText, 2);
        grid.Children.Add(_breadcrumb);
        grid.Children.Add(language);
        grid.Children.Add(_statusBarText);

        bar.Child = grid;
        UpdateStatusBar();
        return bar;
    }

    private void BuildTabs(Document activeDocument)
    {
        _tabStrip.Children.Clear();

        foreach (var document in _documentManager.Documents)
        {
            var tab = new Border
            {
                MinWidth = 140,
                Height = 38,
                Margin = new Thickness(0),
                Padding = new Thickness(12, 0, 7, 0),
                Background = document == activeDocument ? Theme.Surface : Theme.PanelHeader,
                BorderBrush = document == activeDocument ? Theme.Accent : Theme.Border,
                BorderThickness = document == activeDocument
                    ? new Thickness(0, 0, 0, 2)
                    : new Thickness(0, 0, 1, 0),
                Cursor = Cursors.Hand
            };

            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var title = new TextBlock
            {
                Text = document.Name + (document.IsDirty ? " •" : ""),
                Foreground = document == activeDocument ? Theme.Text : Theme.Muted,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };

            var close = new Button
            {
                Content = "×",
                Width = 24,
                Height = 24,
                Margin = new Thickness(8, 0, 0, 0),
                Background = Brushes.Transparent,
                Foreground = Theme.Muted,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };

            tab.MouseLeftButtonUp += (_, _) =>
            {
                _editorService.SetDocument(document);
                _activeDocument = document;
                BuildTabs(document);
                UpdateStatusBar();
            };
            close.Click += (_, _) => CloseDocument(document);

            Grid.SetColumn(title, 0);
            Grid.SetColumn(close, 1);
            layout.Children.Add(title);
            layout.Children.Add(close);
            tab.Child = layout;
            _tabStrip.Children.Add(tab);
        }
    }

    private void CloseDocument(Document document)
    {
        if (_documentManager.Documents.Count <= 1)
            return;

        var index = _documentManager.Documents.ToList().FindIndex(d => d.Id == document.Id);
        _documentManager.CloseDocument(document.Id);

        var remaining = _documentManager.Documents;
        var next = remaining[Math.Max(0, Math.Min(index, remaining.Count - 1))];
        _editorService.SetDocument(next);
        BuildTabs(next);
        UpdateStatusBar();
    }

    private void UpdateStatusBar()
    {
        var filePath = _editorService.FilePath;
        _breadcrumb.Text = string.IsNullOrWhiteSpace(filePath)
            ? "Sans titre  ›  main"
            : filePath.Replace("\\", "  ›  ");

        var text = _editorService.Text;
        var lines = string.IsNullOrEmpty(text) ? 1 : text.Count(c => c == '\n') + 1;
        var language = Path.GetExtension(filePath ?? "").ToLowerInvariant() switch
        {
            ".py" => "Python",
            ".js" or ".mjs" or ".jsx" => "JavaScript",
            ".ts" or ".tsx" => "TypeScript",
            ".cs" => "C#",
            ".java" => "Java",
            ".c" or ".cpp" or ".h" => "C/C++",
            ".html" or ".htm" => "HTML",
            ".css" => "CSS",
            ".json" => "JSON",
            _ => "Texte"
        };

        var caret = _codeEditor.CaretPosition;

        _statusBarText.Text = $"Ln {caret.Line}, Col {caret.Column}     {_editorService.IsModified switch
        {
            true => "Modifié",
            false => "Enregistré"
        }}     {language}     {lines} lignes";
    }

    private void OpenCommandPalette()
    {
        var palette = new Window
        {
            Title = "Palette de commandes",
            Owner = this,
            Width = 520,
            Height = 310,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            Background = Theme.Window,
            BorderBrush = Theme.Accent,
            BorderThickness = new Thickness(1),
            ShowInTaskbar = false
        };

        var search = new TextBox
        {
            Margin = new Thickness(12, 12, 12, 8),
            Height = 38,
            Padding = new Thickness(10, 8, 10, 8),
            Background = Theme.Surface,
            Foreground = Theme.Text,
            CaretBrush = Theme.Text,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(1),
            FontSize = 13
        };

        var commands = new List<(string Name, Action Action)>
        {
            ("Exécuter le fichier actif", _bottomPanel.RequestRun),
            ("Arrêter l'exécution", _bottomPanel.RequestStop),
            ("Afficher ou masquer l'explorateur", _sidebar.Toggle),
            ("Enregistrer le fichier actif", () => _ = SaveActiveAsync()),
        };

        var list = new ListBox
        {
            Margin = new Thickness(12, 0, 12, 12),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = Theme.Text
        };

        void Refresh(string filter)
        {
            list.Items.Clear();
            foreach (var command in commands.Where(c =>
                         c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            {
                var item = new TextBlock
                {
                    Text = command.Name,
                    Padding = new Thickness(10, 8, 10, 8),
                    Foreground = Theme.Text
                };
                list.Items.Add(item);
            }
        }

        Refresh("");
        search.TextChanged += (_, _) => Refresh(search.Text);
        list.MouseDoubleClick += (_, _) =>
        {
            if (list.SelectedIndex < 0) return;
            var name = list.Items[list.SelectedIndex] as TextBlock;
            var command = commands.FirstOrDefault(c => c.Name == name?.Text);
            if (command.Action is not null)
                command.Action();
        };

        var panel = new StackPanel();
        panel.Children.Add(search);
        panel.Children.Add(list);
        palette.Content = panel;
        palette.Show();
        search.Focus();
    }

    private async Task SaveActiveAsync()
    {
        if (_activeDocument == null)
            return;

        try
        {
            if (string.IsNullOrWhiteSpace(_activeDocument.FilePath))
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Enregistrer le fichier",
                    Filter = "Tous les fichiers (*.*)|*.*|Python (*.py)|*.py|C# (*.cs)|*.cs|JavaScript (*.js)|*.js",
                    FileName = _activeDocument.Name
                };

                if (dialog.ShowDialog(this) != true)
                    return;

                await _documentService.SaveDocumentAsAsync(_activeDocument, dialog.FileName);
            }
            else
            {
                await _documentService.SaveDocumentAsync(_activeDocument);
            }

            _editorService.MarkSaved(_activeDocument.FilePath);
            _editorTitle.Text = _activeDocument.Name;
            BuildTabs(_activeDocument);
            UpdateStatusBar();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Impossible d'enregistrer le fichier :\n{ex.Message}",
                "Enregistrement",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static string GetAppDataFolder()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KEYCE_KODER");

    private void LoadSavedTheme()
    {
        try
        {
            var path = Path.Combine(GetAppDataFolder(), "theme.txt");
            if (File.Exists(path))
                Theme.Apply(File.ReadAllText(path).Trim());
        }
        catch { }
    }

    private void UpdateThemeReferences()
    {
        Background = Theme.Window;
        Foreground = Theme.Text;
    }

    private void SetupAutoSave()
    {
        _autoSaveTimer.Interval = TimeSpan.FromSeconds(30);
        _autoSaveTimer.Tick += async (_, _) =>
        {
            if (_activeDocument == null || !_editorService.IsModified)
                return;
            if (!string.IsNullOrWhiteSpace(_activeDocument.FilePath))
                await SaveActiveAsync();
        };
        _autoSaveTimer.Start();
    }

    private void RememberRecentFolder(string folder)
    {
        try
        {
            var path = Path.Combine(GetAppDataFolder(), "recent.txt");
            Directory.CreateDirectory(GetAppDataFolder());

            var existing = File.Exists(path)
                ? File.ReadAllLines(path).Where(p => !string.IsNullOrWhiteSpace(p))
                : Enumerable.Empty<string>();

            var recent = new[] { folder }
                .Concat(existing.Where(p => !p.Equals(folder, StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToArray();

            File.WriteAllLines(path, recent);
            _sidebar.Welcome.SetRecentFolders(recent);
        }
        catch { }
    }

    private void LoadRecentFolders()
    {
        try
        {
            var path = Path.Combine(GetAppDataFolder(), "recent.txt");
            if (!File.Exists(path))
                return;

            var recent = File.ReadAllLines(path)
                .Where(Directory.Exists)
                .Take(8)
                .ToArray();

            _sidebar.Welcome.SetRecentFolders(recent);
            _sidebar.Welcome.OpenRecentFolderRequested += folder =>
                _sidebar.Explorer.OpenFolder(folder);
        }
        catch { }
    }

    private void CycleDocuments()
    {
        var documents = _documentManager.Documents;
        if (documents.Count < 2)
            return;

        if (_activeDocument != null)
        {
            var index = _documentHistory.FindIndex(d => d.Id == _activeDocument.Id);
            if (index >= 0)
                _documentHistory.RemoveAt(index);
            _documentHistory.Insert(0, _activeDocument);
        }

        var next = documents.FirstOrDefault(d => d.Id != _activeDocument?.Id) ?? documents[0];
        _activeDocument = next;
        _editorService.SetDocument(next);
        BuildTabs(next);
        UpdateStatusBar();
    }

    private void LoadLayout()
    {
        try
        {
            var path = Path.Combine(GetAppDataFolder(), "layout.txt");
            if (!File.Exists(path)) return;
            var parts = File.ReadAllText(path).Split(';');
            if (parts.Length >= 3 && double.TryParse(parts[0], out var width) && double.TryParse(parts[1], out var height) && double.TryParse(parts[2], out var sidebar))
            {
                Width = width;
                Height = height;
                _sidebar.Width = sidebar;
            }
        }
        catch { }
    }
}
