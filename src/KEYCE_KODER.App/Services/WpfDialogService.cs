using System.Windows;
using KEYCE_KODER.Core.Interfaces;
using Microsoft.Win32;

namespace KEYCE_KODER.App.Services;

public sealed class WpfDialogService : IDialogService
{
    private const string FileFilter =
        "Tous les fichiers (*.*)|*.*" +
        "|Python (*.py)|*.py" +
        "|C# (*.cs)|*.cs" +
        "|JavaScript (*.js)|*.js" +
        "|HTML (*.html;*.htm)|*.html;*.htm" +
        "|CSS (*.css)|*.css" +
        "|JSON (*.json)|*.json" +
        "|XML (*.xml)|*.xml" +
        "|Texte (*.txt)|*.txt";

    private const string AppName = "KEYCE KODER";

    private readonly Window _owner;

    public WpfDialogService(Window owner)
    {
        _owner = owner;
    }

    public string? AskOpenFilePath()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Ouvrir un fichier",
            Filter = FileFilter,
            CheckFileExists = true
        };

        return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
    }

    public string? AskSaveFilePath(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Enregistrer sous",
            Filter = FileFilter,
            FileName = suggestedFileName,
            OverwritePrompt = true
        };

        return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
    }

    public SaveChangesChoice AskSaveChanges(string documentName)
    {
        MessageBoxResult result = MessageBox.Show(
            _owner,
            $"Voulez-vous enregistrer les modifications apportées à « {documentName} » ?",
            AppName,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        return result switch
        {
            MessageBoxResult.Yes => SaveChangesChoice.Save,
            MessageBoxResult.No => SaveChangesChoice.DontSave,
            _ => SaveChangesChoice.Cancel
        };
    }

    public void ShowError(string title, string message)
    {
        MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void ShowInfo(string title, string message)
    {
        MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}