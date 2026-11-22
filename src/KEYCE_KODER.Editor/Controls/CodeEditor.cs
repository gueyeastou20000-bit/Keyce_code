using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using KEYCE_KODER.Editor.Highlighting;

namespace KEYCE_KODER.Editor.Controls;

public class CodeEditor : UserControl
{
    private readonly TextEditor _editor;
    private readonly TextBlock _minimap = new();

    public event EventHandler? TextChanged;
    public event EventHandler? CaretPositionChanged;

    public (int Line, int Column) CaretPosition
    {
        get
        {
            var location = _editor.TextArea.Caret.Location;
            return (location.Line, location.Column);
        }
    }

    public CodeEditor()
    {
        _editor = new TextEditor
        {
            ShowLineNumbers = true,
            FontSize = 14,
            FontFamily = new FontFamily("Cascadia Code, Consolas"),
            WordWrap = false,
            Background = new SolidColorBrush(Color.FromRgb(15, 18, 26)),
            Foreground = new SolidColorBrush(Color.FromRgb(218, 222, 232)),
            Padding = new Thickness(8, 6, 8, 8),
            LineNumbersForeground = new SolidColorBrush(Color.FromRgb(99, 108, 126))
        };

        _editor.TextChanged += (_, _) =>
        {
            UpdateMinimap();
            TextChanged?.Invoke(this, EventArgs.Empty);
        };
        _editor.TextArea.Caret.PositionChanged += (_, _) =>
            CaretPositionChanged?.Invoke(this, EventArgs.Empty);

        _minimap.FontFamily = new FontFamily("Cascadia Mono, Consolas");
        _minimap.FontSize = 3;
        _minimap.Foreground = new SolidColorBrush(Color.FromRgb(125, 132, 148));
        _minimap.Opacity = 0.72;
        _minimap.Padding = new Thickness(8, 8, 8, 8);
        _minimap.TextWrapping = TextWrapping.NoWrap;

        var minimapBorder = new Border
        {
            Width = 110,
            Background = new SolidColorBrush(Color.FromRgb(13, 16, 23)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(43, 49, 63)),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                Content = _minimap
            }
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_editor, 0);
        Grid.SetColumn(minimapBorder, 1);
        grid.Children.Add(_editor);
        grid.Children.Add(minimapBorder);
        Content = grid;

        UpdateMinimap();
    }

    public string Text
    {
        get => _editor.Text;
        set => _editor.Text = value;
    }

    public void SetLanguage(string? filePath)
    {
        _editor.SyntaxHighlighting = LanguageHighlighter.GetDefinition(filePath);
    }

    private void UpdateMinimap()
    {
        var text = _editor.Text ?? "";
        var lines = text.Split('\n');
        _minimap.Text = string.Join('\n', lines.Take(500));
    }
}
