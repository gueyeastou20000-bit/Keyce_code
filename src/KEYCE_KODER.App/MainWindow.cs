
using System.Windows;
using System.Windows.Media;
using KEYCE_KODER.Core.Models;
using KEYCE_KODER.Core.Services;
using KEYCE_KODER.Editor.Controls;
using KEYCE_KODER.Editor.Services;
using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace KEYCE_KODER.App;

public class MainWindow : Window
{
    private readonly CodeEditor _codeEditor;

    private readonly DocumentManager _documentManager;
    private readonly FileService _fileService;
    private readonly DocumentService _documentService;
    private readonly EditorService _editorService;

    private readonly Document _currentDocument;
    public MainWindow()
    {

	// Icône de l'application
	// Icône de la fenêtre
    Icon = new BitmapImage(
        new Uri("pack://application:,,,/Assets/app.ico")
    );

    Title = "KEYCE KODER";
	Icon = new BitmapImage(
    	new Uri("pack://application:,,,/Assets/app.ico")
	);
        Width = 1200;
        Height = 750;
        MinWidth = 900;
        MinHeight = 600;

        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        Background = new SolidColorBrush(
            Color.FromRgb(30, 30, 30)
        );

        Foreground = Brushes.White;

        // Initialisation des services Core
        _documentManager = new DocumentManager();
        _fileService = new FileService();

        _documentService = new DocumentService(
            _documentManager,
            _fileService
        );

        // Création de l'éditeur
        _codeEditor = new CodeEditor();

        _editorService = new EditorService(_codeEditor);

        // Création du document de démonstration
        _currentDocument = _documentService.CreateDocument();

        _currentDocument.Content =
            "def saluer(nom):\n" +
            "    print(f\"Bonjour {nom} !\")\n\n" +
            "saluer(\"KEYCE KODER\")";

        // Association du document à l'éditeur
        // Association du document à l'éditeur
	_editorService.SetDocument(_currentDocument);

	// Activation de la coloration Python pour la démonstration
	_editorService.SetLanguage("main.py");

	Content = _codeEditor;
    }
}
