
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using KEYCE_KODER.Editor.Highlighting;

namespace KEYCE_KODER.Editor.Controls;

public class CodeEditor : UserControl
{
    private readonly TextEditor _editor;

    public event EventHandler? TextChanged;

    public CodeEditor()
    {
        _editor = new TextEditor
        {
            ShowLineNumbers = true,
            FontSize = 14,
            FontFamily = new FontFamily("Consolas"),
            WordWrap = false,

            Background = new SolidColorBrush(
                Color.FromRgb(30, 30, 30)
            ),

            Foreground = new SolidColorBrush(
                Color.FromRgb(212, 212, 212)
            )
        };

        _editor.TextChanged += (_, _) =>
        {
            TextChanged?.Invoke(this, EventArgs.Empty);
        };

        Content = _editor;
    }
    /// <summary>
    /// Accès à l'éditeur AvalonEdit interne.
    /// Utilisé par EditorActions (menu Édition, recherche/remplacement).
    /// </summary>
    public TextEditor InnerEditor => _editor;

    public string Text
    {
        get => _editor.Text;
        set => _editor.Text = value;
    }

    public void SetLanguage(string? filePath)
    {
        _editor.SyntaxHighlighting =
            LanguageHighlighter.GetDefinition(filePath);
    }
}
