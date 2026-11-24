using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KEYCE_KODER.App.Views;
using KEYCE_KODER.Core.Interfaces;
using KEYCE_KODER.Core.Models;
using KEYCE_KODER.Editor.Controls;
using KEYCE_KODER.Editor.Services;

namespace KEYCE_KODER.App.Tabs;

public sealed class EditorTab
{
    private static Style? _tabStyle;

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
        Editor = new CodeEditor();
Editor.FontFamily = new FontFamily("Agency FB, Consolas");
Editor.FontSize = 17;
        EditorService = new EditorService(Editor);
        EditorService.SetDocument(document);
        Actions = new EditorActions(Editor);

        // 2. L'en-tête : nom du fichier + bouton ×
        _title = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var closeButton = new Button
        {
            Content = "×",
            FontSize = 14,
            Padding = new Thickness(4, 0, 4, 0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = Theme.Muted,
            Cursor = Cursors.Hand,
            Focusable = false,
            ToolTip = "Fermer (Ctrl+W)",
            VerticalAlignment = VerticalAlignment.Center
        };
        closeButton.MouseEnter += (_, _) => closeButton.Foreground = Theme.Accent;
        closeButton.MouseLeave += (_, _) => closeButton.Foreground = Theme.Muted;
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

        // 4. L'onglet lui-même (avec le style bleu nuit / orange)
        TabItem = new TabItem
        {
            Header = header,
            Content = Editor,
            Tag = this,
            Style = GetTabStyle()
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

    /// <summary>Style partagé par tous les onglets (créé une seule fois).</summary>
    private static Style GetTabStyle()
    {
        if (_tabStyle != null)
            return _tabStyle;

        var border = new FrameworkElementFactory(typeof(Border), "Bd");
        border.SetValue(Border.BackgroundProperty, Theme.Sidebar);
        border.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
        border.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 2));
        border.SetValue(Border.PaddingProperty, new Thickness(12, 6, 8, 6));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);

        var template = new ControlTemplate(typeof(TabItem)) { VisualTree = border };

        // Survol (onglet non actif)
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, Theme.SurfaceHover, "Bd"));
        hover.Setters.Add(new Setter(Control.ForegroundProperty, Theme.Text));
        template.Triggers.Add(hover);

        // Onglet actif : fond de l'éditeur + trait orange
        var selected = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Border.BackgroundProperty, Theme.Editor, "Bd"));
        selected.Setters.Add(new Setter(Border.BorderBrushProperty, Theme.Accent, "Bd"));
        selected.Setters.Add(new Setter(Control.ForegroundProperty, Theme.Text));
        template.Triggers.Add(selected);

        var style = new Style(typeof(TabItem));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Theme.Muted));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0, 0, 1, 0)));
        style.Seal();

        _tabStyle = style;
        return style;
    }
}