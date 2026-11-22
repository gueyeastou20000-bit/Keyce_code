using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KEYCE_KODER.Core.Interfaces;
using KEYCE_KODER.Core.Models;
using KEYCE_KODER.Editor.Controls;
using KEYCE_KODER.Editor.Services;

namespace KEYCE_KODER.App.Tabs;

public sealed class EditorTab
{
    private readonly TextBlock _title;

    public Document Document { get; }

    public CodeEditor Editor { get; }

    public IEditorService EditorService { get; }

    public IEditorActions Actions { get; }

    public TabItem TabItem { get; }

    public event EventHandler? CloseRequested;

    public event EventHandler? StateChanged;

    public EditorTab(Document document)
    {
        // 1. Le document et son éditeur
        Document = document;

        Editor = new CodeEditor();
        EditorService = new EditorService(Editor);
        EditorService.SetDocument(document);
        Actions = new EditorActions(Editor);

        // 2. L'en-tête : nom du fichier + bouton ×
        _title = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };

        var closeButton = new Button
        {
            Content = "×",
            FontSize = 13,
            Padding = new Thickness(4, 0, 4, 0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Focusable = false,
            ToolTip = "Fermer (Ctrl+W)",
            VerticalAlignment = VerticalAlignment.Center
        };
        closeButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Background = Brushes.Transparent
        };
        header.Children.Add(_title);
        header.Children.Add(closeButton);

        // 3. Clic molette sur l'en-tête = fermer
        header.PreviewMouseDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                e.Handled = true;
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        };

        // 4. L'onglet lui-même
        TabItem = new TabItem
        {
            Header = header,
            Content = Editor,
            Tag = this
        };

        // 5. À chaque frappe, on met à jour l'en-tête (indicateur ●)
        Editor.TextChanged += (_, _) => RefreshHeader();

        RefreshHeader();
    }

    public void RefreshHeader()
    {
        _title.Text = Document.IsDirty ? $"{Document.Name} ●" : Document.Name;
        TabItem.ToolTip = Document.FilePath ?? "Fichier non enregistré";
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}