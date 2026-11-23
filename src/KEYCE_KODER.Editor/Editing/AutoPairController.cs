using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using KEYCE_KODER.Core.Editing;
using KEYCE_KODER.Core.Languages;

namespace KEYCE_KODER.Editor.Editing;

/// <summary>
/// Fermeture automatique des parenthèses, crochets, accolades et guillemets,
/// saut du caractère fermant, sélection entourée, Retour arrière sur une paire vide.
/// </summary>
public sealed class AutoPairController
{
    private readonly TextEditor _editor;
    private LanguageDefinition _language = LanguageCatalog.PlainText;

    /// <summary>Permet de désactiver la fonctionnalité (ex. depuis les paramètres).</summary>
    public bool Enabled { get; set; } = true;

    public AutoPairController(TextEditor editor)
    {
        _editor = editor;

        _editor.TextArea.TextEntering += OnTextEntering;
        _editor.TextArea.PreviewKeyDown += OnPreviewKeyDown;
    }

    public void SetLanguage(string? filePath)
    {
        _language = LanguageCatalog.FromPath(filePath);
    }

    private TextDocument Document => _editor.Document;

    /// <summary>Caractère à une position, ou '\0' si on est hors du texte.</summary>
    private char CharAt(int offset) =>
        offset >= 0 && offset < Document.TextLength ? Document.GetCharAt(offset) : '\0';

    private void OnTextEntering(object sender, TextCompositionEventArgs e)
    {
        if (!Enabled || e.Text.Length != 1)
            return;

        // Sélection rectangulaire (Alt + souris) : on ne gère pas, comportement normal
        if (_editor.TextArea.Selection is RectangleSelection)
            return;

        int caret = _editor.CaretOffset;
        bool hasSelection = _editor.SelectionLength > 0;

        PairAction action = AutoPairRules.OnTyped(
            e.Text[0], CharAt(caret - 1), CharAt(caret), hasSelection, _language);

        switch (action.Kind)
        {
            case PairActionKind.InsertPair:
                e.Handled = true; // on insère nous-mêmes à la place d'AvalonEdit
                Document.Insert(caret, $"{action.Open}{action.Close}");
                _editor.CaretOffset = caret + 1; // curseur entre les deux
                break;

            case PairActionKind.SkipClosing:
                e.Handled = true;
                _editor.CaretOffset = caret + 1; // on passe par-dessus le fermant existant
                break;

            case PairActionKind.WrapSelection:
                e.Handled = true;
                int start = _editor.SelectionStart;
                string selected = _editor.SelectedText;
                Document.Replace(start, selected.Length, $"{action.Open}{selected}{action.Close}");
                _editor.Select(start + 1, selected.Length); // la sélection reste sur le texte entouré
                break;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Retour arrière seul, sans sélection, entre une paire vide : on efface les deux
        if (!Enabled || e.Key != Key.Back || Keyboard.Modifiers != ModifierKeys.None)
            return;

        if (_editor.SelectionLength > 0)
            return;

        int caret = _editor.CaretOffset;

        if (AutoPairRules.ShouldDeletePair(CharAt(caret - 1), CharAt(caret), _language))
        {
            e.Handled = true;
            Document.Remove(caret - 1, 2);
        }
    }
}