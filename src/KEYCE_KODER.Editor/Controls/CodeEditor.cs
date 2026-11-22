using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Search;

using KEYCE_KODER.Editor.Highlighting;

namespace KEYCE_KODER.Editor.Controls;

public class CodeEditor : UserControl
{
    private readonly TextEditor _editor;
    private readonly SearchPanel _searchPanel;

    private CompletionWindow? _completionWindow;

    private readonly string[] _pythonSuggestions =
    {
        "print",
        "input",
        "len",
        "range",
        "str",
        "int",
        "float",
        "list",
        "dict",
        "tuple",
        "set",
        "def",
        "class",
        "return",
        "if",
        "elif",
        "else",
        "for",
        "while",
        "break",
        "continue",
        "import",
        "from",
        "as",
        "try",
        "except",
        "finally",
        "with",
        "lambda",
        "async",
        "await",
        "True",
        "False",
        "None"
    };

    public event EventHandler? TextChanged;

    public CodeEditor()
    {
        _editor = new TextEditor
        {
            ShowLineNumbers = true,
            FontSize = 14,
            FontFamily = new FontFamily("Consolas"),
            WordWrap = false,
            Padding = new Thickness(10),

            Background = new SolidColorBrush(
                Color.FromRgb(30, 30, 30)),

            Foreground = new SolidColorBrush(
                Color.FromRgb(212, 212, 212))
        };

        _editor.TextChanged += (_, _) =>
        {
            TextChanged?.Invoke(this, EventArgs.Empty);
        };

        _editor.TextArea.TextEntered += OnTextEntered;
        _editor.TextArea.PreviewKeyDown += OnPreviewKeyDown;

        _searchPanel = SearchPanel.Install(_editor.TextArea);

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

    private void OnPreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key == Key.Space &&
            Keyboard.Modifiers == ModifierKeys.Control)
        {
            ShowCompletion();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F &&
            Keyboard.Modifiers == ModifierKeys.Control)
        {
            _searchPanel.Open();
            e.Handled = true;
        }
    }

    private void OnTextEntered(
        object? sender,
        TextCompositionEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text))
            return;

        if (char.IsLetterOrDigit(e.Text[0]) ||
            e.Text[0] == '_')
        {
            ShowCompletion();
        }
    }

    private void ShowCompletion()
    {
        if (_completionWindow != null)
            return;

        int offset = _editor.CaretOffset;
        int start = offset;

        while (start > 0)
        {
            char c = _editor.Document.GetCharAt(start - 1);

            if (!char.IsLetterOrDigit(c) && c != '_')
                break;

            start--;
        }

        string prefix = _editor.Document.GetText(
            start,
            offset - start);

        if (prefix.Length == 0)
            return;

        var suggestions = _pythonSuggestions
            .Where(word =>
                word.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (suggestions.Count == 0)
            return;

        _completionWindow = new CompletionWindow(
            _editor.TextArea);

        IList<ICompletionData> data =
            _completionWindow.CompletionList.CompletionData;

        foreach (string suggestion in suggestions)
        {
            data.Add(new PythonCompletionData(suggestion));
        }

        _completionWindow.Closed += (_, _) =>
        {
            _completionWindow = null;
        };

        _completionWindow.Show();
    }

    private class PythonCompletionData : ICompletionData
    {
        public PythonCompletionData(string text)
        {
            Text = text;
        }

        public ImageSource? Image => null;

        public string Text { get; }

        public object Content => Text;

        public object Description =>
            $"Suggestion Python : {Text}";

        public double Priority => 0;

        public void Complete(
            TextArea textArea,
            ISegment completionSegment,
            EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(
                completionSegment,
                Text);
        }
    }
}