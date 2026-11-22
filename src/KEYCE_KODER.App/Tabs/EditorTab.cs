using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KEYCE_KODER.App.Themes;
using KEYCE_KODER.Core.Interfaces;
using KEYCE_KODER.Core.Models;
using KEYCE_KODER.Editor.Controls;
using KEYCE_KODER.Editor.Services;

namespace KEYCE_KODER.App.Tabs;

public sealed class EditorTab
{
    private readonly TextBlock _title;
    private readonly Button _closeButton;

    public Document Document { get; }
    public CodeEditor Editor { get; }
    public IEditorService EditorService { get; }
    public IEditorActions Actions { get; }
    public TabItem TabItem { get; }

    public event EventHandler? CloseRequested;
    public event EventHandler? StateChanged;

    public EditorTab(Document document)
    {
        Document = document;

        Editor = new CodeEditor();
        EditorService = new EditorService(Editor);
        EditorService.SetDocument(document);
        Actions = new EditorActions(Editor);

        _title = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Foreground = ThemeColors.TextSecondary,
            FontSize = 12
        };

        _closeButton = new Button
        {
            Content = "×",
            FontSize = 16,
            Padding = new Thickness(5, 0, 5, 0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = ThemeColors.TextSecondary,
            Cursor = Cursors.Hand,
            Focusable = false,
            ToolTip = "Fermer (Ctrl+W)",
            VerticalAlignment = VerticalAlignment.Center
        };

        _closeButton.MouseEnter += (_, _) =>
            _closeButton.Foreground = ThemeColors.TextPrimary;

        _closeButton.MouseLeave += (_, _) =>
            _closeButton.Foreground = ThemeColors.TextSecondary;

        _closeButton.Click += (_, _) =>
            CloseRequested?.Invoke(this, EventArgs.Empty);

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Background = Brushes.Transparent
        };

        header.Children.Add(_title);
        header.Children.Add(_closeButton);

        header.PreviewMouseDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Middle)
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
            Background = ThemeColors.TabBackground,
            Foreground = ThemeColors.TextPrimary,
            BorderBrush = ThemeColors.Border,
            Padding = new Thickness(12, 7, 12, 7)
        };

        Editor.TextChanged += (_, _) => RefreshHeader();

        RefreshHeader();
    }

    public void RefreshHeader()
    {
        _title.Text = Document.IsDirty
            ? $"{Document.Name} ●"
            : Document.Name;

        _title.Foreground = Document.IsDirty
            ? ThemeColors.Accent
            : ThemeColors.TextSecondary;

        TabItem.ToolTip = Document.FilePath ?? "Fichier non enregistré";

        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}