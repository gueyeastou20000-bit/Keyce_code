using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using KEYCE_KODER.App.Git;
using KEYCE_KODER.App.Views;
using Microsoft.Win32;

namespace KEYCE_KODER.App.Pdf;

/// <summary>
/// Onglet « lecteur PDF » de KEYCE KODER :
/// - affichage des pages (moteur PDF de Windows), zoom, miniatures ;
/// - décodage : texte de la page, recherche dans tout le document, informations ;
/// - modification : pivoter, déplacer, insérer, supprimer des pages, fusionner un PDF,
///   ajouter du texte, surligner, masquer une zone ; annuler ; enregistrer.
/// Le fichier d'origine n'est modifié qu'au moment d'« Enregistrer » :
/// on travaille sur des copies dans le dossier temporaire.
/// </summary>
public sealed class PdfViewer : DockPanel
{
    private const double MinZoom = 0.25;
    private const double MaxZoom = 4.0;
    private const double ThumbnailWidth = 110;

    private static readonly Brush CanvasBackground = Frozen(40, 44, 52);
    private static readonly Brush HighlightFill = FrozenArgb(90, 255, 230, 0);
    private static readonly Brush MaskFill = FrozenArgb(200, 255, 255, 255);

    // Bouton plat (même style que le reste de l'interface)
    private static readonly ControlTemplate FlatTemplate = (ControlTemplate)XamlReader.Parse(
        """
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         TargetType="ButtonBase">
          <Grid x:Name="root">
            <Border Background="{TemplateBinding Background}" CornerRadius="4"/>
            <Border x:Name="hover" CornerRadius="4" Background="#26FFFFFF" Visibility="Collapsed"/>
            <ContentPresenter Margin="{TemplateBinding Padding}" HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="hover" Property="Visibility" Value="Visible"/>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter TargetName="root" Property="Opacity" Value="0.35"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
        """);

    private enum Tool
    {
        None,
        Text,
        Highlight,
        Mask
    }

    // ---------- Document ----------
    private readonly string _workFolder;
    private readonly Stack<string> _history = new(); // versions précédentes (pour Annuler)
    private string _workingPath;                     // version affichée
    private string _savedPath;                       // version correspondant au fichier enregistré
    private string? _password;
    private PdfRenderer _renderer;
    private int _version;
    private int _page;
    private double _zoom = 1.0;
    private bool _busy;
    private Tool _tool = Tool.None;
    private CancellationTokenSource? _searchCancellation;
    private int _renderTicket;
    private int _thumbnailTicket;

    // ---------- Interface ----------
    private readonly TextBlock _title = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
    private readonly TextBox _pageBox = new() { Width = 42, TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBlock _pageCount = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0) };
    private readonly TextBlock _zoomText = new() { VerticalAlignment = VerticalAlignment.Center, Width = 46, TextAlignment = TextAlignment.Center };
    private readonly TextBlock _status = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly ListBox _thumbnails = new();
    private readonly ScrollViewer _scroll = new();
    private readonly Image _pageImage = new() { Stretch = Stretch.Fill };
    private readonly Canvas _overlay = new() { Background = Brushes.Transparent };
    private readonly Rectangle _selection = new() { StrokeThickness = 1, Visibility = Visibility.Collapsed };
    private readonly Border _textPanel = new();
    private readonly TextBox _pageText = new();
    private readonly TextBox _searchBox = new();
    private readonly ListBox _searchResults = new();
    private readonly TextBlock _searchStatus = new();
    private readonly Button _undoButton;
    private readonly Button _saveButton;
    private readonly List<ButtonBase> _editButtons = new();
    private readonly Dictionary<Tool, ToggleButton> _toolButtons = new();
    private Point _dragStart;
    private bool _dragging;

    /// <summary>Chemin du fichier PDF ouvert.</summary>
    public string FilePath { get; private set; }

    /// <summary>Des modifications ne sont pas encore enregistrées.</summary>
    public bool IsModified => !string.Equals(_workingPath, _savedPath, StringComparison.Ordinal);

    /// <summary>L'onglet à ajouter dans la zone des onglets.</summary>
    public TabItem TabItem { get; }

    /// <summary>Clic sur × de l'onglet (ou clic molette).</summary>
    public event Action<PdfViewer>? CloseRequested;

    /// <summary>Le titre a changé (fichier modifié ●, enregistré, renommé).</summary>
    public event Action<PdfViewer>? StateChanged;

    // =====================================================================
    // Ouverture
    // =====================================================================

    public static bool IsPdf(string path) =>
        string.Equals(System.IO.Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Ouvre un PDF dans un nouvel onglet. Demande le mot de passe si le fichier est protégé.
    /// Retourne null si l'ouverture est annulée ou impossible (le message a déjà été affiché).
    /// </summary>
    public static async Task<PdfViewer?> OpenAsync(string path, Window owner)
    {
        string fullPath = System.IO.Path.GetFullPath(path);
        string workFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "KEYCE_KODER", "pdf", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(workFolder);
            string firstCopy = System.IO.Path.Combine(workFolder, "v0.pdf");
            File.Copy(fullPath, firstCopy, overwrite: true);

            string? password = null;

            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    PdfRenderer renderer = await PdfRenderer.LoadAsync(firstCopy, password);
                    return new PdfViewer(fullPath, workFolder, firstCopy, password, renderer);
                }
                catch (Exception ex) when (PdfRenderer.IsPasswordError(ex) && attempt < 3)
                {
                    string[]? values = PromptWindow.Ask(
                        owner,
                        "PDF protégé",
                        attempt == 0
                            ? $"« {System.IO.Path.GetFileName(fullPath)} » est protégé par un mot de passe."
                            : "Mot de passe incorrect, réessayez.",
                        ("Mot de passe", ""));

                    if (values is null)
                    {
                        TryDeleteFolder(workFolder);
                        return null;
                    }

                    password = values[0];
                }
            }
        }
        catch (Exception ex)
        {
            TryDeleteFolder(workFolder);
            MessageBox.Show(owner,
                $"Impossible d'ouvrir ce PDF :\n{fullPath}\n\n{ex.Message}",
                "Lecteur PDF", MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }
    }

    private PdfViewer(string filePath, string workFolder, string workingPath, string? password, PdfRenderer renderer)
    {
        FilePath = filePath;
        _workFolder = workFolder;
        _workingPath = workingPath;
        _savedPath = workingPath;
        _password = password;
        _renderer = renderer;

        Background = Theme.Editor;
        Focusable = true;

        TabItem = BuildTabItem();

        // ---------- Barre d'outils ----------
        var toolbar = new WrapPanel { Margin = new Thickness(6, 4, 6, 4) };

        toolbar.Children.Add(IconButton("◀", "Page précédente (Page préc.)", () => GoToPageAsync(_page - 1)));
        _pageBox.Background = Theme.Surface;
        _pageBox.Foreground = Theme.Text;
        _pageBox.CaretBrush = Theme.Text;
        _pageBox.BorderBrush = Theme.Border;
        _pageBox.Margin = new Thickness(4, 0, 0, 0);
        _pageBox.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter && int.TryParse(_pageBox.Text, out int number))
            {
                e.Handled = true;
                await GoToPageAsync(number - 1);
            }
        };
        toolbar.Children.Add(_pageBox);
        _pageCount.Foreground = Theme.Muted;
        toolbar.Children.Add(_pageCount);
        toolbar.Children.Add(IconButton("▶", "Page suivante (Page suiv.)", () => GoToPageAsync(_page + 1)));

        toolbar.Children.Add(MakeSeparator());
        toolbar.Children.Add(IconButton("−", "Zoom arrière (Ctrl+molette)", () => SetZoomAsync(_zoom / 1.25)));
        _zoomText.Foreground = Theme.Text;
        toolbar.Children.Add(_zoomText);
        toolbar.Children.Add(IconButton("+", "Zoom avant (Ctrl+molette)", () => SetZoomAsync(_zoom * 1.25)));
        toolbar.Children.Add(TextButton("Largeur", "Ajuster à la largeur", FitWidthAsync));

        toolbar.Children.Add(MakeSeparator());
        toolbar.Children.Add(EditButton("⟲", "Pivoter la page à gauche", () => EditAsync("Page pivotée", PdfEditor.RotatePage(_page, -90), _page)));
        toolbar.Children.Add(EditButton("⟳", "Pivoter la page à droite", () => EditAsync("Page pivotée", PdfEditor.RotatePage(_page, 90), _page)));
        toolbar.Children.Add(EditButton("⇡", "Déplacer la page vers le haut", MovePageUpAsync));
        toolbar.Children.Add(EditButton("⇣", "Déplacer la page vers le bas", MovePageDownAsync));
        toolbar.Children.Add(EditButton("＋", "Insérer une page blanche après cette page", () => EditAsync("Page blanche insérée", PdfEditor.InsertBlankPageAfter(_page), _page + 1)));
        toolbar.Children.Add(EditButton("✕", "Supprimer cette page", DeletePageAsync));
        toolbar.Children.Add(EditButton("Fusionner…", "Ajouter les pages d'un autre PDF à la fin", MergeAsync));

        toolbar.Children.Add(MakeSeparator());
        toolbar.Children.Add(ToolButton(Tool.Text, "T Texte", "Ajouter du texte : cliquez ensuite sur la page"));
        toolbar.Children.Add(ToolButton(Tool.Highlight, "Surligner", "Surligner : tracez un rectangle sur la page"));
        toolbar.Children.Add(ToolButton(Tool.Mask, "Masquer", "Masquer en blanc : tracez un rectangle sur la page"));

        toolbar.Children.Add(MakeSeparator());
        _undoButton = TextButton("↶ Annuler", "Annuler la dernière modification", UndoAsync);
        toolbar.Children.Add(_undoButton);
        _saveButton = TextButton("Enregistrer", "Enregistrer les modifications dans le fichier (Ctrl+S)", () => SaveAsync());
        toolbar.Children.Add(_saveButton);
        toolbar.Children.Add(TextButton("Enregistrer sous…", "Enregistrer une copie", SaveAsAsync));

        toolbar.Children.Add(MakeSeparator());
        toolbar.Children.Add(TextButton("Texte", "Afficher le texte de la page et la recherche", ToggleTextPanelAsync));
        toolbar.Children.Add(TextButton("Infos", "Informations du document", ShowInfoAsync));

        var toolbarHost = new Border
        {
            Background = Theme.PanelHeader,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = toolbar
        };
        SetDock(toolbarHost, Dock.Top);
        Children.Add(toolbarHost);

        // ---------- Barre d'état ----------
        _status.Foreground = Theme.Muted;
        var statusHost = new Border
        {
            Background = Theme.PanelHeader,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(10, 4, 10, 4),
            Child = _status
        };
        SetDock(statusHost, Dock.Bottom);
        Children.Add(statusHost);

        // ---------- Miniatures (à gauche) ----------
        _thumbnails.Width = ThumbnailWidth + 40;
        _thumbnails.Background = Theme.Sidebar;
        _thumbnails.BorderBrush = Theme.Border;
        _thumbnails.BorderThickness = new Thickness(0, 0, 1, 0);
        _thumbnails.Foreground = Theme.Muted;
        _thumbnails.HorizontalContentAlignment = HorizontalAlignment.Center;
        ScrollViewer.SetHorizontalScrollBarVisibility(_thumbnails, ScrollBarVisibility.Disabled);
        _thumbnails.SelectionChanged += async (_, _) =>
        {
            if (_thumbnails.SelectedIndex >= 0 && _thumbnails.SelectedIndex != _page)
                await GoToPageAsync(_thumbnails.SelectedIndex);
        };
        SetDock(_thumbnails, Dock.Left);
        Children.Add(_thumbnails);

        // ---------- Texte et recherche (à droite, masqué au départ) ----------
        BuildTextPanel();
        SetDock(_textPanel, Dock.Right);
        Children.Add(_textPanel);

        // ---------- Page (au centre) ----------
        var pageHost = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(20)
        };
        pageHost.Children.Add(new Border
        {
            Background = Brushes.White,
            Child = _pageImage
        });
        _overlay.Children.Add(_selection);
        pageHost.Children.Add(_overlay);

        _overlay.MouseLeftButtonDown += OnOverlayMouseDown;
        _overlay.MouseMove += OnOverlayMouseMove;
        _overlay.MouseLeftButtonUp += OnOverlayMouseUp;

        _scroll.Content = pageHost;
        _scroll.Background = CanvasBackground;
        _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _scroll.PreviewMouseWheel += OnMouseWheel;
        Children.Add(_scroll);

        // ---------- Clavier ----------
        PreviewKeyDown += async (_, e) =>
        {
            if (e.OriginalSource is TextBox)
                return; // on tape dans une zone de texte

            if (e.Key == Key.PageDown || (e.Key == Key.Right && Keyboard.Modifiers == ModifierKeys.None))
            {
                e.Handled = true;
                await GoToPageAsync(_page + 1);
            }
            else if (e.Key == Key.PageUp || (e.Key == Key.Left && Keyboard.Modifiers == ModifierKeys.None))
            {
                e.Handled = true;
                await GoToPageAsync(_page - 1);
            }
            else if (e.Key == Key.Escape)
            {
                SelectTool(Tool.None);
            }
            else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                await UndoAsync();
            }
        };

        Loaded += async (_, _) =>
        {
            if (_version == 0)
            {
                _version = 1;
                await ReloadAsync(0);
                ShowStatus("Astuce : Ctrl+molette pour zoomer, Page préc./Page suiv. pour changer de page.");
            }
        };

        UpdateButtons();
    }

    // =====================================================================
    // Affichage des pages
    // =====================================================================

    private async Task ReloadAsync(int page)
    {
        _page = Math.Clamp(page, 0, _renderer.PageCount - 1);
        _pageCount.Text = $"/ {_renderer.PageCount}";
        await RenderPageAsync();
        _ = RenderThumbnailsAsync();
    }

    private async Task RenderPageAsync()
    {
        int ticket = ++_renderTicket;

        try
        {
            Size size = _renderer.GetPageSize(_page);
            double width = size.Width * _zoom;
            double height = size.Height * _zoom;
            double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;

            BitmapSource bitmap = await _renderer.RenderAsync(_page, width * dpi);

            // Un rendu plus récent a été demandé entre-temps : on ignore celui-ci
            if (ticket != _renderTicket)
                return;

            _pageImage.Source = bitmap;
            _pageImage.Width = width;
            _pageImage.Height = height;
            _overlay.Width = width;
            _overlay.Height = height;

            _pageBox.Text = (_page + 1).ToString();
            _zoomText.Text = $"{Math.Round(_zoom * 100)} %";

            if (_thumbnails.SelectedIndex != _page && _page < _thumbnails.Items.Count)
                _thumbnails.SelectedIndex = _page;

            if (_textPanel.Visibility == Visibility.Visible)
                await LoadPageTextAsync();
        }
        catch (Exception ex)
        {
            ShowStatus($"❌ Affichage impossible : {ex.Message}", error: true);
        }

        UpdateButtons();
    }

    private async Task RenderThumbnailsAsync()
    {
        int ticket = ++_thumbnailTicket;
        int count = _renderer.PageCount;
        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;

        _thumbnails.Items.Clear();
        var images = new List<Image>();

        for (int i = 0; i < count; i++)
        {
            var image = new Image { Width = ThumbnailWidth, Stretch = Stretch.Uniform };
            var item = new StackPanel { Margin = new Thickness(0, 6, 0, 2) };
            item.Children.Add(new Border { Background = Brushes.White, Child = image });
            item.Children.Add(new TextBlock
            {
                Text = (i + 1).ToString(),
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Theme.Muted,
                Margin = new Thickness(0, 3, 0, 0)
            });

            images.Add(image);
            _thumbnails.Items.Add(item);
        }

        _thumbnails.SelectedIndex = _page;

        // Les miniatures sont dessinées une par une, sans bloquer l'interface
        for (int i = 0; i < count; i++)
        {
            if (ticket != _thumbnailTicket)
                return; // document rechargé entre-temps

            try
            {
                images[i].Source = await _renderer.RenderAsync(i, ThumbnailWidth * dpi);
            }
            catch (Exception)
            {
                // Miniature impossible : on laisse la case blanche
            }
        }
    }

    private async Task GoToPageAsync(int page)
    {
        if (page < 0 || page >= _renderer.PageCount || page == _page && _pageImage.Source is not null)
        {
            _pageBox.Text = (_page + 1).ToString();
            return;
        }

        _page = page;
        await RenderPageAsync();
        _scroll.ScrollToTop();
    }

    private async Task SetZoomAsync(double zoom)
    {
        _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        await RenderPageAsync();
    }

    private Task FitWidthAsync()
    {
        Size size = _renderer.GetPageSize(_page);
        double available = _scroll.ViewportWidth - 40;
        return available > 50 ? SetZoomAsync(available / size.Width) : Task.CompletedTask;
    }

    private async void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Ctrl+molette = zoom
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            await SetZoomAsync(e.Delta > 0 ? _zoom * 1.1 : _zoom / 1.1);
            return;
        }

        // En bas de la page, la molette passe à la page suivante (et inversement)
        if (e.Delta < 0 && _scroll.VerticalOffset >= _scroll.ScrollableHeight && _page < _renderer.PageCount - 1)
        {
            e.Handled = true;
            await GoToPageAsync(_page + 1);
        }
        else if (e.Delta > 0 && _scroll.VerticalOffset <= 0 && _page > 0)
        {
            e.Handled = true;
            await GoToPageAsync(_page - 1);
            _scroll.ScrollToBottom();
        }
    }

    // =====================================================================
    // Modifications
    // =====================================================================

    /// <summary>
    /// Applique une modification sur une nouvelle copie, puis l'affiche.
    /// L'ancienne copie est gardée pour pouvoir annuler.
    /// </summary>
    private async Task EditAsync(string description, PdfEdit edit, int pageAfter)
    {
        if (_busy)
            return;

        SetBusy(true);
        ShowStatus("Modification en cours…");

        string next = System.IO.Path.Combine(_workFolder, $"v{_history.Count + 1}-{Guid.NewGuid():N}.pdf");

        try
        {
            await PdfEditor.ApplyAsync(_workingPath, next, _password, edit);
            PdfRenderer renderer = await PdfRenderer.LoadAsync(next, _password);

            _history.Push(_workingPath);
            _workingPath = next;
            _renderer = renderer;

            await ReloadAsync(pageAfter);
            ShowStatus($"✓ {description}. (Ctrl+Z pour annuler, « Enregistrer » pour garder)");
        }
        catch (Exception ex)
        {
            TryDelete(next);
            ShowStatus($"❌ {ex.Message}", error: true);
        }
        finally
        {
            SetBusy(false);
            StateChanged?.Invoke(this);
        }
    }

    private Task MovePageUpAsync() =>
        _page == 0 ? Task.CompletedTask : EditAsync("Page déplacée", PdfEditor.MovePage(_page, _page - 1), _page - 1);

    private Task MovePageDownAsync() =>
        _page >= _renderer.PageCount - 1 ? Task.CompletedTask : EditAsync("Page déplacée", PdfEditor.MovePage(_page, _page + 1), _page + 1);

    private Task DeletePageAsync()
    {
        MessageBoxResult answer = MessageBox.Show(
            Window.GetWindow(this)!,
            $"Supprimer la page {_page + 1} ?\n(Vous pourrez annuler avec Ctrl+Z tant que vous n'avez pas enregistré.)",
            "Lecteur PDF", MessageBoxButton.YesNo, MessageBoxImage.Question);

        return answer == MessageBoxResult.Yes
            ? EditAsync("Page supprimée", PdfEditor.DeletePage(_page), Math.Max(0, _page - 1))
            : Task.CompletedTask;
    }

    private Task MergeAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "PDF à ajouter à la fin du document",
            Filter = "Documents PDF (*.pdf)|*.pdf"
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            return Task.CompletedTask;

        return EditAsync($"Pages de « {System.IO.Path.GetFileName(dialog.FileName)} » ajoutées",
            PdfEditor.AppendDocument(dialog.FileName), _renderer.PageCount);
    }

    private async Task UndoAsync()
    {
        if (_busy || _history.Count == 0)
            return;

        SetBusy(true);

        try
        {
            string previous = _history.Pop();
            _renderer = await PdfRenderer.LoadAsync(previous, _password);
            _workingPath = previous;

            await ReloadAsync(_page);
            ShowStatus("↶ Modification annulée.");
        }
        catch (Exception ex)
        {
            ShowStatus($"❌ {ex.Message}", error: true);
        }
        finally
        {
            SetBusy(false);
            StateChanged?.Invoke(this);
        }
    }

    // ---------- Outils : texte, surligner, masquer ----------

    private void SelectTool(Tool tool)
    {
        _tool = _tool == tool ? Tool.None : tool;

        foreach ((Tool key, ToggleButton button) in _toolButtons)
            button.IsChecked = key == _tool;

        _overlay.Cursor = _tool == Tool.None ? null : Cursors.Cross;

        ShowStatus(_tool switch
        {
            Tool.Text => "Cliquez sur la page à l'endroit où écrire le texte (Échap pour arrêter).",
            Tool.Highlight => "Tracez un rectangle sur la zone à surligner (Échap pour arrêter).",
            Tool.Mask => "Tracez un rectangle sur la zone à masquer en blanc (Échap pour arrêter).",
            _ => ""
        });
    }

    private async void OnOverlayMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_tool == Tool.None || _busy || _overlay.ActualWidth <= 0)
            return;

        Point position = e.GetPosition(_overlay);
        e.Handled = true;

        if (_tool == Tool.Text)
        {
            await AddTextAtAsync(position);
            return;
        }

        // Surligner / masquer : début du rectangle
        _dragStart = position;
        _dragging = true;
        _overlay.CaptureMouse();

        _selection.Fill = _tool == Tool.Highlight ? HighlightFill : MaskFill;
        _selection.Stroke = Theme.Accent;
        Canvas.SetLeft(_selection, position.X);
        Canvas.SetTop(_selection, position.Y);
        _selection.Width = 0;
        _selection.Height = 0;
        _selection.Visibility = Visibility.Visible;
    }

    private void OnOverlayMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging)
            return;

        Point position = e.GetPosition(_overlay);
        double x = Math.Clamp(Math.Min(position.X, _dragStart.X), 0, _overlay.ActualWidth);
        double y = Math.Clamp(Math.Min(position.Y, _dragStart.Y), 0, _overlay.ActualHeight);
        double right = Math.Clamp(Math.Max(position.X, _dragStart.X), 0, _overlay.ActualWidth);
        double bottom = Math.Clamp(Math.Max(position.Y, _dragStart.Y), 0, _overlay.ActualHeight);

        Canvas.SetLeft(_selection, x);
        Canvas.SetTop(_selection, y);
        _selection.Width = right - x;
        _selection.Height = bottom - y;
    }

    private async void OnOverlayMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
            return;

        _dragging = false;
        _overlay.ReleaseMouseCapture();
        _selection.Visibility = Visibility.Collapsed;

        double width = _overlay.ActualWidth;
        double height = _overlay.ActualHeight;
        double fx = Canvas.GetLeft(_selection) / width;
        double fy = Canvas.GetTop(_selection) / height;
        double fw = _selection.Width / width;
        double fh = _selection.Height / height;

        if (_selection.Width < 4 || _selection.Height < 4)
            return; // simple clic : rien à faire

        if (_tool == Tool.Highlight)
            await EditAsync("Zone surlignée", PdfEditor.Highlight(_page, fx, fy, fw, fh), _page);
        else if (_tool == Tool.Mask)
            await EditAsync("Zone masquée", PdfEditor.Mask(_page, fx, fy, fw, fh), _page);
    }

    private async Task AddTextAtAsync(Point position)
    {
        string[]? values = PromptWindow.Ask(
            Window.GetWindow(this),
            "Ajouter du texte",
            "Texte à écrire sur la page (le coin haut-gauche du texte sera à l'endroit cliqué).\n"
            + "Pour plusieurs lignes, séparez-les par « \\n ».",
            ("Texte", ""),
            ("Taille (points)", "12"),
            ("Couleur : noir, rouge ou bleu", "noir"));

        if (values is null || values[0].Length == 0)
            return;

        if (!double.TryParse(values[1].Replace(',', '.'), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double size) || size < 4 || size > 200)
            size = 12;

        PdfTextColor color = values[2].ToLowerInvariant() switch
        {
            "rouge" or "red" => PdfTextColor.Red,
            "bleu" or "blue" => PdfTextColor.Blue,
            _ => PdfTextColor.Black
        };

        string text = values[0].Replace("\\n", "\n");
        double fx = position.X / _overlay.ActualWidth;
        double fy = position.Y / _overlay.ActualHeight;

        await EditAsync("Texte ajouté", PdfEditor.AddText(_page, fx, fy, text, size, color), _page);
    }

    // =====================================================================
    // Enregistrer
    // =====================================================================

    /// <summary>Enregistre dans le fichier d'origine. Retourne false en cas d'échec.</summary>
    public Task<bool> SaveAsync()
    {
        if (!IsModified)
        {
            ShowStatus("Aucune modification à enregistrer.");
            return Task.FromResult(true);
        }

        return SaveToAsync(FilePath);
    }

    private async Task SaveAsAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Enregistrer le PDF sous",
            Filter = "Documents PDF (*.pdf)|*.pdf",
            FileName = System.IO.Path.GetFileName(FilePath),
            InitialDirectory = System.IO.Path.GetDirectoryName(FilePath),
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
            await SaveToAsync(dialog.FileName);
    }

    private async Task<bool> SaveToAsync(string destination)
    {
        try
        {
            string source = _workingPath;
            await Task.Run(() => File.Copy(source, destination, overwrite: true));

            FilePath = System.IO.Path.GetFullPath(destination);
            _savedPath = _workingPath;
            RefreshHeader();
            ShowStatus($"✓ Enregistré : {FilePath}");
            StateChanged?.Invoke(this);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus($"❌ Enregistrement impossible : {ex.Message}", error: true);
            return false;
        }
    }

    // =====================================================================
    // Décodage : texte, recherche, informations
    // =====================================================================

    private void BuildTextPanel()
    {
        _textPanel.Width = 320;
        _textPanel.Visibility = Visibility.Collapsed;
        _textPanel.Background = Theme.Sidebar;
        _textPanel.BorderBrush = Theme.Border;
        _textPanel.BorderThickness = new Thickness(1, 0, 0, 0);

        StyleTextBox(_searchBox);
        _searchBox.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await SearchAsync();
            }
        };

        _searchStatus.Foreground = Theme.Muted;
        _searchStatus.FontSize = 11;
        _searchStatus.Margin = new Thickness(0, 4, 0, 4);
        _searchStatus.Text = "Tapez un mot puis Entrée.";

        _searchResults.Height = 170;
        _searchResults.Background = Brushes.Transparent;
        _searchResults.Foreground = Theme.Text;
        _searchResults.BorderBrush = Theme.Border;
        _searchResults.MouseDoubleClick += async (_, _) =>
        {
            if (_searchResults.SelectedItem is PdfSearchHit hit)
                await GoToPageAsync(hit.PageIndex);
        };

        StyleTextBox(_pageText);
        _pageText.IsReadOnly = true;
        _pageText.TextWrapping = TextWrapping.Wrap;
        _pageText.AcceptsReturn = true;
        _pageText.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _pageText.FontFamily = new FontFamily("Cascadia Mono, Consolas");
        _pageText.FontSize = 12;

        var top = new StackPanel();
        top.Children.Add(SectionTitle("RECHERCHER DANS LE PDF"));
        top.Children.Add(_searchBox);
        top.Children.Add(_searchStatus);
        top.Children.Add(_searchResults);
        top.Children.Add(SectionTitle("TEXTE DE LA PAGE (sélectionnable, Ctrl+C)"));

        var layout = new DockPanel { Margin = new Thickness(10) };
        SetDock(top, Dock.Top);
        layout.Children.Add(top);
        layout.Children.Add(_pageText);

        _textPanel.Child = layout;
    }

    private async Task ToggleTextPanelAsync()
    {
        bool show = _textPanel.Visibility != Visibility.Visible;
        _textPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

        if (show)
        {
            await LoadPageTextAsync();
            _searchBox.Focus();
        }
    }

    private async Task LoadPageTextAsync()
    {
        int page = _page;
        _pageText.Text = "Lecture du texte…";

        try
        {
            string text = await PdfTextReader.GetPageTextAsync(_workingPath, _password, page);

            if (page == _page)
            {
                _pageText.Text = string.IsNullOrWhiteSpace(text)
                    ? "(Aucun texte sur cette page : c'est peut-être une image ou un document scanné.)"
                    : text;
            }
        }
        catch (Exception ex)
        {
            _pageText.Text = $"Impossible de lire le texte : {ex.Message}";
        }
    }

    private async Task SearchAsync()
    {
        string query = _searchBox.Text.Trim();
        _searchCancellation?.Cancel();
        _searchResults.Items.Clear();

        if (query.Length == 0)
        {
            _searchStatus.Text = "Tapez un mot puis Entrée.";
            return;
        }

        var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;
        _searchStatus.Text = "Recherche en cours…";

        try
        {
            IReadOnlyList<PdfSearchHit> hits =
                await PdfTextReader.SearchAsync(_workingPath, _password, query, cancellation.Token);

            if (cancellation != _searchCancellation)
                return;

            foreach (PdfSearchHit hit in hits)
                _searchResults.Items.Add(hit);

            _searchStatus.Text = hits.Count == 0
                ? "Aucun résultat."
                : $"{hits.Count} résultat(s) — double-clic pour aller à la page.";
        }
        catch (OperationCanceledException)
        {
            // Une recherche plus récente a été lancée
        }
        catch (Exception ex)
        {
            _searchStatus.Text = $"Recherche impossible : {ex.Message}";
        }
    }

    private async Task ShowInfoAsync()
    {
        try
        {
            PdfInfo info = await PdfTextReader.GetInfoAsync(_workingPath, _password);

            string text =
                $"Fichier : {FilePath}\n" +
                $"Taille : {info.FileSize / 1024.0:0.#} Ko\n" +
                $"Pages : {info.PageCount}\n" +
                $"Version PDF : {info.Version}\n" +
                $"Protégé (chiffré) : {(info.IsEncrypted ? "oui" : "non")}\n\n" +
                $"Titre : {info.Title ?? "—"}\n" +
                $"Auteur : {info.Author ?? "—"}\n" +
                $"Créé avec : {info.Creator ?? "—"}\n" +
                $"Producteur PDF : {info.Producer ?? "—"}";

            MessageBox.Show(Window.GetWindow(this)!, text, "Informations du PDF",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowStatus($"❌ Informations illisibles : {ex.Message}", error: true);
        }
    }

    // =====================================================================
    // Onglet, état, nettoyage
    // =====================================================================

    private TabItem BuildTabItem()
    {
        var close = new Button
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
        close.Click += (_, _) => CloseRequested?.Invoke(this);

        var header = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Transparent };
        header.Children.Add(new TextBlock
        {
            Text = "PDF",
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.IndianRed,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0)
        });
        header.Children.Add(_title);
        header.Children.Add(close);

        header.PreviewMouseDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                e.Handled = true;
                CloseRequested?.Invoke(this);
            }
        };

        var tab = new TabItem { Header = header, Content = this, Tag = this };
        RefreshHeader(tab);
        return tab;
    }

    private void RefreshHeader(TabItem? tab = null)
    {
        tab ??= TabItem;
        string name = System.IO.Path.GetFileName(FilePath);
        _title.Text = IsModified ? $"{name} ●" : name;
        tab.ToolTip = FilePath;
    }

    private void UpdateButtons()
    {
        foreach (ButtonBase button in _editButtons)
            button.IsEnabled = !_busy;

        _undoButton.IsEnabled = !_busy && _history.Count > 0;
        _saveButton.IsEnabled = !_busy && IsModified;

        if (TabItem is not null)
            RefreshHeader();
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        Cursor = busy ? Cursors.Wait : null;
        UpdateButtons();
    }

    private void ShowStatus(string text, bool error = false)
    {
        _status.Text = text;
        _status.Foreground = error ? Theme.Red : Theme.Muted;
        _status.ToolTip = text.Length > 80 ? text : null;
    }

    /// <summary>Supprime les copies de travail (à appeler quand l'onglet est fermé).</summary>
    public void Cleanup()
    {
        _searchCancellation?.Cancel();
        _thumbnailTicket++;
        _renderTicket++;
        TryDeleteFolder(_workFolder);
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception) { /* fichier encore utilisé : il restera dans le dossier temporaire */ }
    }

    private static void TryDeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
        catch (Exception)
        {
            // Une copie est encore ouverte par Windows : le dossier temporaire sera nettoyé plus tard
        }
    }

    // =====================================================================
    // Petits outils d'interface
    // =====================================================================

    private Button IconButton(string glyph, string toolTip, Func<Task> action)
    {
        Button button = TextButton(glyph, toolTip, action);
        button.MinWidth = 28;
        button.FontSize = 14;
        return button;
    }

    private Button EditButton(string content, string toolTip, Func<Task> action)
    {
        Button button = content.Length <= 2 ? IconButton(content, toolTip, action) : TextButton(content, toolTip, action);
        _editButtons.Add(button);
        return button;
    }

    private Button TextButton(string content, string toolTip, Func<Task> action)
    {
        var button = new Button
        {
            Content = content,
            ToolTip = toolTip,
            Template = FlatTemplate,
            Background = Brushes.Transparent,
            Foreground = Theme.Text,
            Padding = new Thickness(7, 4, 7, 4),
            Margin = new Thickness(1, 1, 1, 1),
            Cursor = Cursors.Hand,
            Focusable = false
        };

        button.Click += async (_, _) =>
        {
            await action();
            Focus(); // le clavier revient au lecteur (Page suiv., Ctrl+Z...)
        };

        return button;
    }

    private ToggleButton ToolButton(Tool tool, string content, string toolTip)
    {
        var button = new ToggleButton
        {
            Content = content,
            ToolTip = toolTip,
            Template = FlatTemplate,
            Foreground = Theme.Text,
            Padding = new Thickness(7, 4, 7, 4),
            Margin = new Thickness(1),
            Cursor = Cursors.Hand,
            Focusable = false
        };

        // Bouton enfoncé = outil actif (fond violet)
        button.Checked += (_, _) => button.Background = Theme.AccentSoft;
        button.Unchecked += (_, _) => button.Background = Brushes.Transparent;
        button.Background = Brushes.Transparent;
        button.Click += (_, _) => SelectTool(tool);

        _toolButtons[tool] = button;
        _editButtons.Add(button);
        return button;
    }

    private static UIElement MakeSeparator() => new Border
    {
        Width = 1,
        Height = 20,
        Margin = new Thickness(6, 0, 6, 0),
        Background = Theme.Border,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static TextBlock SectionTitle(string text) => new()
    {
        Text = text,
        Foreground = Theme.Muted,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 8, 0, 4)
    };

    private static void StyleTextBox(TextBox box)
    {
        box.Background = Theme.Surface;
        box.Foreground = Theme.Text;
        box.CaretBrush = Theme.Text;
        box.BorderBrush = Theme.Border;
        box.Padding = new Thickness(5, 3, 5, 3);
    }

    private static SolidColorBrush Frozen(byte r, byte g, byte b) => FrozenArgb(255, r, g, b);

    private static SolidColorBrush FrozenArgb(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}
