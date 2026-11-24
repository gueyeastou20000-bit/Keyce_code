using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KEYCE_KODER.Runner;

namespace KEYCE_KODER.App.Tools;

/// <summary>
/// Fenêtre « Langages et outils » : affiche, pour chaque langage du Runner, si l'outil
/// nécessaire est installé, et permet de l'installer en un clic avec winget.
/// À créer seulement après l'affichage de la fenêtre principale (Owner).
/// </summary>
public sealed class ToolsWindow : Window
{
    private static readonly Brush BackgroundBrush = Frozen(30, 30, 30);
    private static readonly Brush RowBrush = Frozen(40, 40, 44);
    private static readonly Brush HighlightBrush = Frozen(0, 84, 148);
    private static readonly Brush TextBrush = Frozen(220, 220, 220);
    private static readonly Brush MutedBrush = Frozen(150, 150, 150);
    private static readonly Brush OkBrush = Frozen(115, 201, 145);
    private static readonly Brush MissingBrush = Frozen(244, 135, 113);

    private readonly ToolInstaller _installer = new();
    private readonly StackPanel _rows = new();

    private readonly TextBlock _status = new()
    {
        Foreground = TextBrush,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center
    };

    private readonly TextBox _log = new()
    {
        IsReadOnly = true,
        Height = 150,
        Margin = new Thickness(0, 10, 0, 0),
        FontFamily = new FontFamily("Cascadia Mono, Consolas"),
        FontSize = 11,
        Background = Frozen(20, 20, 20),
        Foreground = MutedBrush,
        BorderBrush = Frozen(60, 60, 60),
        TextWrapping = TextWrapping.NoWrap,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
    };

    private readonly Button _refreshButton = new()
    {
        Content = "Actualiser",
        Padding = new Thickness(14, 4, 14, 4),
        Margin = new Thickness(10, 0, 0, 0),
        ToolTip = "Vérifier à nouveau les outils installés"
    };

    private bool _busy;

    /// <summary>Mettre à true pour autoriser la vraie fermeture (quand l'application se ferme).</summary>
    public bool AllowClose { get; set; }

    public ToolsWindow(Window owner)
    {
        Owner = owner;
        Title = "Langages et outils";
        Width = 700;
        Height = 580;
        MinWidth = 520;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = BackgroundBrush;
        Foreground = TextBrush;

        _refreshButton.Click += async (_, _) => await RefreshAsync(highlightToolId: null, rescan: true);

        Content = BuildLayout();
    }

    /// <summary>Affiche la fenêtre ; highlightToolId met en évidence un outil (ex. "gcc").</summary>
    public async Task ShowForAsync(string? highlightToolId = null)
    {
        Show();
        Activate();

        if (!_busy)
            await RefreshAsync(highlightToolId, rescan: false);
    }

    // ---------------- Détection ----------------

    private async Task RefreshAsync(string? highlightToolId, bool rescan)
    {
        if (rescan)
            EnvironmentPath.Refresh();

        _refreshButton.IsEnabled = false;
        _rows.Children.Clear();
        _status.Text = "Détection des outils installés…";

        bool winget = await ToolInstaller.IsWingetAvailableAsync();

        foreach (LanguageProfile profile in LanguageProfiles.All)
        {
            ResolvedTool? tool = await ToolLocator.LocateAsync(profile.Tool);
            bool highlight = string.Equals(profile.Tool.Id, highlightToolId, StringComparison.OrdinalIgnoreCase);

            _rows.Children.Add(BuildRow(profile, tool, winget, highlight));
        }

        _rows.IsEnabled = true;
        _refreshButton.IsEnabled = true;

        _status.Text = winget
            ? "Les outils marqués ❌ peuvent être installés en un clic avec « Installer »."
            : "winget est introuvable sur ce PC : installez « App Installer » depuis le Microsoft Store, "
              + "ou installez les outils manuellement (survolez ❌ pour voir comment).";
    }

    // ---------------- Installation ----------------

    private async Task InstallAsync(LanguageProfile profile, ToolInstallInfo info)
    {
        if (_busy)
            return;

        _busy = true;
        _rows.IsEnabled = false;
        _refreshButton.IsEnabled = false;

        _log.Clear();
        AppendLog($"> winget install --id {info.WingetId} --exact");
        _status.Text = $"Installation de {profile.Tool.DisplayName}… "
                       + "Windows peut demander une autorisation administrateur : acceptez-la.";

        string finalMessage;

        try
        {
            int exitCode = await _installer.InstallAsync(
                info,
                line => Dispatcher.BeginInvoke(new Action(() => AppendLog(line))));

            AppendLog($"winget terminé (code {exitCode}).");

            ResolvedTool? tool = await ToolInstaller.MakeAvailableAsync(profile.Tool, info);

            finalMessage = tool is not null
                ? $"✅ {profile.Tool.DisplayName} est prêt ({Shorten(tool.Version)}). Vous pouvez cliquer sur Run."
                : $"⚠ {profile.Tool.DisplayName} n'est pas encore détecté. Redémarrez KEYCE KODER. "
                  + $"Si le problème persiste : {profile.Tool.InstallHint}";
        }
        catch (Exception ex)
        {
            finalMessage = $"❌ Installation impossible : {ex.Message}";
        }
        finally
        {
            _busy = false;
        }

        await RefreshAsync(highlightToolId: profile.Tool.Id, rescan: false);
        _status.Text = finalMessage;
    }

    // ---------------- Affichage ----------------

    private UIElement BuildRow(LanguageProfile profile, ResolvedTool? tool, bool winget, bool highlight)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var name = new TextBlock
        {
            Text = profile.DisplayName,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextBrush,
            VerticalAlignment = VerticalAlignment.Center
        };

        var details = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        details.Children.Add(new TextBlock
        {
            Text = $"{profile.Tool.DisplayName}   ({string.Join(" ", profile.Extensions)})",
            Foreground = MutedBrush,
            FontSize = 11
        });
        details.Children.Add(new TextBlock
        {
            Text = tool is null ? "❌ Non installé" : $"✅ {Shorten(tool.Version)}",
            Foreground = tool is null ? MissingBrush : OkBrush,
            ToolTip = tool is null ? profile.Tool.InstallHint : tool.Version,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        Place(grid, name, 0);
        Place(grid, details, 1);

        ToolInstallInfo? info = ToolInstallCatalog.For(profile.Tool.Id);

        if (tool is null && winget && info is not null)
        {
            var install = new Button
            {
                Content = "Installer",
                Padding = new Thickness(14, 4, 14, 4),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = $"winget install --id {info.WingetId}"
            };
            install.Click += async (_, _) => await InstallAsync(profile, info);

            Place(grid, install, 2);
        }

        return new Border
        {
            Background = highlight ? HighlightBrush : RowBrush,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 6),
            Child = grid
        };
    }

    private UIElement BuildLayout()
    {
        var root = new DockPanel { Margin = new Thickness(14) };

        var header = new TextBlock
        {
            Text = "Outils nécessaires pour exécuter chaque langage avec Run",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextBrush,
            Margin = new Thickness(0, 0, 0, 10)
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // Bas de la fenêtre : message d'état + bouton Actualiser
        var bottom = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(_refreshButton, Dock.Right);
        bottom.Children.Add(_refreshButton);
        bottom.Children.Add(_status);

        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);

        // Juste au-dessus : le journal de winget
        DockPanel.SetDock(_log, Dock.Bottom);
        root.Children.Add(_log);

        // Le reste : la liste des langages
        root.Children.Add(new ScrollViewer
        {
            Content = _rows,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        });

        return root;
    }

    private void AppendLog(string line)
    {
        _log.AppendText(line + Environment.NewLine);
        _log.ScrollToEnd();
    }

    private static void Place(Grid grid, UIElement element, int column)
    {
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    private static string Shorten(string text) =>
        text.Length > 60 ? text[..60] + "…" : text;

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    // La croix masque la fenêtre au lieu de la détruire (une installation peut continuer).
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }
}
