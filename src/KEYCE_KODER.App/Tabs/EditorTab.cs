using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KEYCE_KODER.App.Services;
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
    public EditorService EditorService { get; }
    public EditorActions Actions { get; }
    public TabItem TabItem { get; }

    public event EventHandler? CloseRequested;
    public event EventHandler? StateChanged;

    public EditorTab(Document document)
    {
        Document = document;

        Editor = new CodeEditor();
        Editor.InnerEditor.FontFamily = new FontFamily(AppSettings.Current.EditorFontFamily);
        Editor.InnerEditor.FontSize = AppSettings.Current.EditorFontSize;
        Editor.InnerEditor.ShowLineNumbers = AppSettings.Current.ShowLineNumbers;
        Editor.InnerEditor.WordWrap = AppSettings.Current.WordWrap;

        EditorService = new EditorService(Editor);
        EditorService.SetDocument(document);
        Actions = new EditorActions(Editor);

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
            Orientation = Orientation.Horizontal
        };
        header.Children.Add(_title);
        header.Children.Add(closeButton);

        header.MouseDown += (_, e) =>
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                e.Handled = true;
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        };

        TabItem = new TabItem
        {
            Header = header,
            Content = Editor,
            Tag = this,
            Style = GetTabStyle()
        };

        Editor.TextChanged += (_, _) => RefreshHeader();

        RefreshHeader();
    }

    public void RefreshHeader()
    {
        _title.Text = Document.IsDirty
            ? $"●  {Document.Name}"
            : Document.Name;

        TabItem.ToolTip = Document.FilePath ?? "Fichier non enregistré";
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplySettings(AppSettings settings)
    {
        Editor.InnerEditor.FontFamily = new FontFamily(settings.EditorFontFamily);
        Editor.InnerEditor.FontSize = settings.EditorFontSize;
        Editor.InnerEditor.ShowLineNumbers = settings.ShowLineNumbers;
        Editor.InnerEditor.WordWrap = settings.WordWrap;
    }

    /// <summary>Style partagé par tous les onglets (design bleu nuit / orange d'Astou).</summary>
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
