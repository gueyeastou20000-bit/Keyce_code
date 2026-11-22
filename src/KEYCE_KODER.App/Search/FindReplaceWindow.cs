using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KEYCE_KODER.Core.Interfaces;
using KEYCE_KODER.Core.Models;
using KEYCE_KODER.Core.Services;

namespace KEYCE_KODER.App.Search;

public sealed class FindReplaceWindow : Window
{
    private readonly Func<IEditorActions?> _getActiveEditor;
    private readonly SearchService _search = new();

    private readonly TextBox _findBox = new() { Margin = new Thickness(0, 0, 0, 6) };
    private readonly TextBox _replaceBox = new() { Margin = new Thickness(0, 0, 0, 6) };
    private readonly Label _replaceLabel = new() { Content = "Remplacer par :", Padding = new Thickness(0, 0, 8, 6) };
    private readonly CheckBox _matchCase = new() { Content = "Respecter la casse", Margin = new Thickness(0, 0, 12, 0) };
    private readonly CheckBox _wholeWord = new() { Content = "Mot entier" };
    private readonly Button _replaceButton = MakeButton("Remplacer");
    private readonly Button _replaceAllButton = MakeButton("Tout remplacer");
    private readonly TextBlock _status = new() { Margin = new Thickness(0, 8, 0, 0), MinHeight = 18 };

    public bool AllowClose { get; set; }

    public string Query => _findBox.Text;

    public FindReplaceWindow(Window owner, Func<IEditorActions?> getActiveEditor)
    {
        _getActiveEditor = getActiveEditor;

        Owner = owner;
        Title = "Rechercher";
        Width = 440;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = BuildLayout();

        // Entrée = suivant, Maj+Entrée = précédent
        _findBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) FindPrevious(); else FindNext();
                e.Handled = true;
            }
        };

        // Échap = masquer la fenêtre et revenir dans l'éditeur
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                _getActiveEditor()?.Focus();
                e.Handled = true;
            }
        };

        // Changer le texte ou une option met le compteur à jour
        _findBox.TextChanged += (_, _) => UpdateCount();
        _matchCase.Click += (_, _) => UpdateCount();
        _wholeWord.Click += (_, _) => UpdateCount();
    }

    private SearchOptions Options =>
     new(MatchCase: _matchCase.IsChecked == true, WholeWord: _wholeWord.IsChecked == true);

    public void ShowFor(bool replaceMode)
    {
        SetReplaceMode(replaceMode);

        // Pré-remplit avec le texte sélectionné s'il tient sur une ligne
        string selected = _getActiveEditor()?.SelectedText ?? string.Empty;

        if (selected.Length > 0 && !selected.Contains('\n'))
            _findBox.Text = selected;

        Show();
        Activate();
        _findBox.Focus();
        _findBox.SelectAll();
        UpdateCount();
    }

    public void FindNext()
    {
        IEditorActions? editor = _getActiveEditor();

        if (editor is null || Query.Length == 0)
            return;

        // On part de la fin de la sélection pour ne pas retrouver la même occurrence
        int start = editor.SelectionStart + editor.SelectionLength;
        SearchMatch? match = _search.FindNext(editor.Text, Query, start, Options);

        ShowResult(editor, match);
    }

    public void FindPrevious()
    {
        IEditorActions? editor = _getActiveEditor();

        if (editor is null || Query.Length == 0)
            return;

        SearchMatch? match = _search.FindPrevious(editor.Text, Query, editor.SelectionStart, Options);

        ShowResult(editor, match);
    }

    private void ReplaceCurrent()
    {
        IEditorActions? editor = _getActiveEditor();

        if (editor is null || Query.Length == 0)
            return;

        // On ne remplace que si la sélection est bien une occurrence ;
        // sinon, on cherche d'abord la suivante.
        if (_search.IsExactMatch(editor.SelectedText, Query, Options))
            editor.ReplaceSelection(_replaceBox.Text);

        FindNext();
    }

    private void ReplaceAll()
    {
        IEditorActions? editor = _getActiveEditor();

        if (editor is null || Query.Length == 0)
            return;

        ReplaceAllResult result = _search.ReplaceAll(editor.Text, Query, _replaceBox.Text, Options);

        if (result.Count > 0)
            editor.ReplaceAllText(result.Text);

        _status.Text = result.Count == 0
            ? "Aucune occurrence à remplacer."
            : $"{result.Count} occurrence(s) remplacée(s). Ctrl+Z pour annuler.";
    }

    private void ShowResult(IEditorActions editor, SearchMatch? match)
    {
        if (match is null)
        {
            _status.Text = $"Aucun résultat pour « {Query} ».";
            return;
        }

        editor.Select(match.Value.Index, match.Value.Length);
        UpdateCount();
    }

    private void UpdateCount()
    {
        IEditorActions? editor = _getActiveEditor();

        if (editor is null || Query.Length == 0)
        {
            _status.Text = string.Empty;
            return;
        }

        int count = _search.CountMatches(editor.Text, Query, Options);
        _status.Text = count == 0 ? "Aucun résultat." : $"{count} occurrence(s) dans le fichier.";
    }

    private void SetReplaceMode(bool replaceMode)
    {
        Visibility visibility = replaceMode ? Visibility.Visible : Visibility.Collapsed;

        _replaceLabel.Visibility = visibility;
        _replaceBox.Visibility = visibility;
        _replaceButton.Visibility = visibility;
        _replaceAllButton.Visibility = visibility;

        Title = replaceMode ? "Rechercher et remplacer" : "Rechercher";
    }

    private UIElement BuildLayout()
    {
        var grid = new Grid { Margin = new Thickness(12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (int i = 0; i < 5; i++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var findLabel = new Label { Content = "Rechercher :", Padding = new Thickness(0, 0, 8, 6) };
        Place(grid, findLabel, 0, 0);
        Place(grid, _findBox, 0, 1);
        Place(grid, _replaceLabel, 1, 0);
        Place(grid, _replaceBox, 1, 1);

        var options = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 8) };
        options.Children.Add(_matchCase);
        options.Children.Add(_wholeWord);
        Place(grid, options, 2, 0, columnSpan: 2);

        var previousButton = MakeButton("Précédent");
        var nextButton = MakeButton("Suivant");

        previousButton.Click += (_, _) => FindPrevious();
        nextButton.Click += (_, _) => FindNext();
        _replaceButton.Click += (_, _) => ReplaceCurrent();
        _replaceAllButton.Click += (_, _) => ReplaceAll();

        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(previousButton);
        buttons.Children.Add(nextButton);
        buttons.Children.Add(_replaceButton);
        buttons.Children.Add(_replaceAllButton);
        Place(grid, buttons, 3, 0, columnSpan: 2);

        Place(grid, _status, 4, 0, columnSpan: 2);

        return grid;
    }

    private static void Place(Grid grid, UIElement element, int row, int column, int columnSpan = 1)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        Grid.SetColumnSpan(element, columnSpan);
        grid.Children.Add(element);
    }

    private static Button MakeButton(string text) => new()
    {
        Content = text,
        MinWidth = 90,
        Margin = new Thickness(6, 0, 0, 0),
        Padding = new Thickness(8, 3, 8, 3)
    };

    // La croix de la fenêtre la masque au lieu de la détruire : on la réutilise.
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
            _getActiveEditor()?.Focus();
        }

        base.OnClosing(e);
    }
}