using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;

namespace Keyce_code.App;
public class EditorView : UserControl
{
    private readonly TextEditor _editor;
    private string? _currentPath;

    public EditorView()
    {
        _editor = new TextEditor
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            Foreground = Brushes.White,
            LineNumbersForeground = Brushes.Gray,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 14,
            ShowLineNumbers = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        // Ctrl+S : enregistre le fichier ouvert
        _editor.PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Save();
                e.Handled = true;
            }
        };

        Content = _editor;
    }

    public void OpenFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Length > 2 * 1024 * 1024)
            {
                MessageBox.Show("Fichier trop gros (plus de 2 Mo).", "Ouverture impossible");
                return;
            }

            // Refuse les fichiers binaires (PDF, images...)
            var start = new byte[Math.Min(8000, (int)info.Length)];
            using (var fs = File.OpenRead(path))
            {
                int read = fs.Read(start, 0, start.Length);
                if (start.Take(read).Contains((byte)0))
                {
                    MessageBox.Show("Ce fichier n'est pas un fichier texte.", "Ouverture impossible");
                    return;
                }
            }

            _editor.Text = File.ReadAllText(path);
            _editor.SyntaxHighlighting =
                HighlightingManager.Instance.GetDefinitionByExtension(info.Extension);
            _currentPath = path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(ex.Message, "Erreur de lecture");
        }
    }

    private void Save()
    {
        if (_currentPath != null)
            File.WriteAllText(_currentPath, _editor.Text);
    }
}