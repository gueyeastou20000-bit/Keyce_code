using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using KEYCE_KODER.Core.Completion;

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

    private readonly CompletionItem _item;

    public CompletionData(CompletionItem item)
    {
        _item = item;
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
    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text);
    }

    // ----- Affichage -----

    private static UIElement BuildContent(CompletionItem item)
    {
        (string badge, Brush color) = item.Kind switch
        {
            CompletionKind.Keyword => ("mot-clé", KeywordBrush),
            CompletionKind.Builtin => ("ƒ", BuiltinBrush),
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