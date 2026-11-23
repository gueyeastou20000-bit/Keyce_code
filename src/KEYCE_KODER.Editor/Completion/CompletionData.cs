using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using KEYCE_KODER.Core.Completion;
using ICSharpCode.AvalonEdit.Document;

namespace KEYCE_KODER.Editor.Completion;

/// <summary>
/// Adapte une CompletionItem (Core) au format attendu par la liste d'AvalonEdit.
/// </summary>
public sealed class CompletionData : ICompletionData
{
    private static readonly Brush TextBrush = Frozen(220, 220, 220);
    private static readonly Brush KeywordBrush = Frozen(197, 134, 192);  // violet
    private static readonly Brush BuiltinBrush = Frozen(220, 220, 170);  // jaune pâle
    private static readonly Brush WordBrush = Frozen(140, 140, 140);     // gris

    private static readonly Brush SnippetBrush = Frozen(78, 201, 176);   // turquoise

    private readonly CompletionItem _item;

    private readonly string _indentUnit;

    public CompletionData(CompletionItem item, string indentUnit)
    {
        _item = item;
        _indentUnit = indentUnit;
        Content = BuildContent(item);
    }

    // ----- Ce qu'AvalonEdit demande (contrat ICompletionData) -----

    public ImageSource? Image => null;

    /// <summary>Texte inséré, et utilisé pour filtrer la liste pendant la frappe.</summary>
    public string Text => _item.Text;

    /// <summary>Ce qui est affiché dans la liste (badge + texte).</summary>
    public object Content { get; }

    /// <summary>Info-bulle affichée à côté de la suggestion sélectionnée.</summary>
    public object Description => _item.Description;

    public double Priority => 0;

    /// <summary>Appelée quand l'utilisateur valide (Entrée ou Tab) : remplace le mot tapé.</summary>
    /// <summary>Appelée quand l'utilisateur valide (Entrée ou Tab).</summary>
    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        // Suggestion simple : on remplace le mot tapé
        if (_item.SnippetBody is null)
        {
            textArea.Document.Replace(completionSegment, Text);
            return;
        }

        // Snippet : on développe le modèle avec l'indentation de la ligne actuelle
        TextDocument document = textArea.Document;
        int offset = completionSegment.Offset;
        DocumentLine line = document.GetLineByOffset(offset);

        string indentation = document.GetText(TextUtilities.GetLeadingWhitespace(document, line));
        string newLine = TextUtilities.GetNewLineFromDocument(document, line.LineNumber);

        (string text, int caret) = SnippetExpander.Expand(_item.SnippetBody, indentation, _indentUnit, newLine);

        document.Replace(completionSegment, text);
        textArea.Caret.Offset = offset + caret; // le curseur va à la place de $0
    }

    // ----- Affichage -----

    private static UIElement BuildContent(CompletionItem item)
    {
        (string badge, Brush color) = item.Kind switch
        {
            CompletionKind.Keyword => ("mot-clé", KeywordBrush),
            CompletionKind.Builtin => ("ƒ", BuiltinBrush),
            CompletionKind.Snippet => ("snippet", SnippetBrush),
            _ => ("abc", WordBrush)
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        panel.Children.Add(new TextBlock
        {
            Text = badge,
            Foreground = color,
            FontSize = 10,
            Width = 48,
            VerticalAlignment = VerticalAlignment.Center
        });

        panel.Children.Add(new TextBlock { Text = item.Text, Foreground = TextBrush });

        return panel;
    }

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}