using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using KEYCE_KODER.App.Tools;
using KEYCE_KODER.App.Views;
using KEYCE_KODER.Core.Git;
using Microsoft.Win32;

namespace KEYCE_KODER.App.Git;

/// <summary>
/// Vue « Contrôle de source » de la barre latérale (comme dans VS Code) :
/// branche, fichiers modifiés, indexer / désindexer / annuler, commit, pull, push,
/// changement de branche, historique, initialisation et clonage de dépôt.
/// Toutes les commandes passent par GitService (projet Core).
/// </summary>
public sealed class GitPanel : DockPanel
{
    private const int MaxFilesPerSection = 300;

    // Installation de Git avec winget (même mécanisme que la fenêtre « Langages et outils »)
    private static readonly ToolInstallInfo GitInstall =
        new("git", "Git.Git", "git.exe", [@"%ProgramFiles%\Git\cmd"]);

    private static readonly Brush ModifiedBrush = Frozen(226, 192, 141);
    private static readonly Brush RenamedBrush = Frozen(110, 170, 255);
    private static readonly Brush WarningBrush = Frozen(226, 192, 141);

    // Bouton plat : survol = léger voile blanc, désactivé = transparent
    private static readonly ControlTemplate FlatTemplate = (ControlTemplate)XamlReader.Parse(
        """
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         TargetType="Button">
          <Grid x:Name="root">
            <Border Background="{TemplateBinding Background}"
                    BorderBrush="{TemplateBinding BorderBrush}"
                    BorderThickness="{TemplateBinding BorderThickness}"
                    CornerRadius="4"/>
            <Border x:Name="hover" CornerRadius="4" Background="#22FFFFFF" Visibility="Collapsed"/>
            <ContentPresenter Margin="{TemplateBinding Padding}"
                              HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                              VerticalAlignment="{TemplateBinding VerticalContentAlignment}"/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="hover" Property="Visibility" Value="Visible"/>
            </Trigger>
            <Trigger Property="IsPressed" Value="True">
              <Setter TargetName="hover" Property="Background" Value="#38FFFFFF"/>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter TargetName="root" Property="Opacity" Value="0.4"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
        """);

    private readonly Func<string?> _getFolder;
    private readonly GitService _git = new();
    private readonly DispatcherTimer _refreshTimer;

    private FileSystemWatcher? _watcher;
    private string? _watchedRoot;

    private string? _root;          // racine du dépôt affiché (null = pas de dépôt)
    private GitStatus? _status;     // dernier état lu
    private bool _gitFound;         // git.exe a déjà été trouvé
    private bool _busy;             // une commande git est en cours
    private bool _refreshing;
    private bool _refreshAgain;

    // ---- Zones de la vue ----
    private readonly StackPanel _stateArea = new() { Margin = new Thickness(10, 4, 10, 0) };
    private readonly DockPanel _repoArea = new();
    private readonly TextBlock _info = new()
    {
        TextWrapping = TextWrapping.Wrap,
        FontSize = 11.5,
        Margin = new Thickness(10, 6, 10, 8),
        Visibility = Visibility.Collapsed
    };

    // ---- Dépôt ----
    private readonly Button _branchButton;
    private readonly Button _pullButton;
    private readonly Button _pushButton;
    private readonly Button _refreshButton;
    private readonly TextBox _message;
    private readonly TextBlock _messagePlaceholder;
    private readonly Button _commitButton;
    private readonly StackPanel _sections = new();
    private readonly StackPanel _history = new();
    private readonly ScrollViewer _historyScroll;
    private readonly Button _historyToggle;

    /// <summary>Ouvrir un fichier dans l'éditeur (chemin complet).</summary>
    public event Action<string>? FileOpenRequested;

    /// <summary>Ouvrir un dossier dans l'explorateur (après un clone).</summary>
    public event Action<string>? FolderOpenRequested;

    /// <summary>Demande à l'explorateur d'afficher « Choisir un dossier ».</summary>
    public event Action? ChooseFolderRequested;

    /// <summary>Git a modifié des fichiers sur le disque (pull, changement de branche, annulation).</summary>
    public event Action? WorkingTreeChanged;

    public GitPanel(Func<string?> getFolder)
    {
        _getFolder = getFolder;

        // Rafraîchissement automatique, regroupé : 0,8 s après le dernier changement de fichier
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _refreshTimer.Tick += async (_, _) =>
        {
            _refreshTimer.Stop();
            await RefreshAsync();
        };

        // ---------- En-tête : branche + actualiser ----------
        _branchButton = FlatButton("", "Changer de branche, créer une branche…");
        _branchButton.HorizontalContentAlignment = HorizontalAlignment.Left;
        _branchButton.Padding = new Thickness(8, 5, 8, 5);
        _branchButton.Background = Theme.Surface;
        _branchButton.Click += async (_, _) => await ShowBranchMenuAsync();

        _refreshButton = FlatButton("⟳", "Actualiser");
        _refreshButton.Width = 30;
        _refreshButton.FontSize = 15;
        _refreshButton.Margin = new Thickness(6, 0, 0, 0);
        _refreshButton.Click += async (_, _) => await RefreshAsync();

        var branchRow = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(_refreshButton, Dock.Right);
        branchRow.Children.Add(_refreshButton);
        branchRow.Children.Add(_branchButton);

        // ---------- Pull / Push ----------
        _pullButton = FlatButton("↓ Pull", "Récupérer les commits de GitHub (git pull)");
        _pullButton.Background = Theme.Surface;
        _pullButton.Margin = new Thickness(0, 0, 3, 0);
        _pullButton.Click += async (_, _) => await PullAsync();

        _pushButton = FlatButton("↑ Push", "Envoyer vos commits sur GitHub (git push)");
        _pushButton.Background = Theme.Surface;
        _pushButton.Margin = new Thickness(3, 0, 0, 0);
        _pushButton.Click += async (_, _) => await PushAsync();

        var syncRow = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 8) };
        syncRow.Children.Add(_pullButton);
        syncRow.Children.Add(_pushButton);

        // ---------- Message de commit ----------
        _message = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 54,
            MaxHeight = 120,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(6, 4, 6, 4),
            Background = Theme.Surface,
            Foreground = Theme.Text,
            CaretBrush = Theme.Text,
            BorderBrush = Theme.Border
        };

        _messagePlaceholder = new TextBlock
        {
            Text = "Message du commit (Ctrl+Entrée pour valider)",
            Foreground = Theme.Muted,
            Margin = new Thickness(9, 5, 9, 0),
            IsHitTestVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _message.TextChanged += (_, _) =>
            _messagePlaceholder.Visibility = _message.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Ctrl+Entrée = commit
        _message.PreviewKeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                e.Handled = true;
                await CommitAsync();
            }
        };

        var messageHost = new Grid();
        messageHost.Children.Add(_message);
        messageHost.Children.Add(_messagePlaceholder);

        _commitButton = FlatButton("✓ Valider (commit)", "Enregistrer les modifications indexées dans l'historique (Ctrl+Entrée)");
        _commitButton.Background = Theme.Accent;
        _commitButton.Foreground = Brushes.White;
        _commitButton.FontWeight = FontWeights.SemiBold;
        _commitButton.Padding = new Thickness(8, 6, 8, 6);
        _commitButton.Margin = new Thickness(0, 6, 0, 0);
        _commitButton.Click += async (_, _) => await CommitAsync();

        var top = new StackPanel { Margin = new Thickness(10, 0, 10, 0) };
        top.Children.Add(branchRow);
        top.Children.Add(syncRow);
        top.Children.Add(messageHost);
        top.Children.Add(_commitButton);

        // ---------- Historique (en bas, repliable) ----------
        _historyScroll = new ScrollViewer
        {
            Content = _history,
            MaxHeight = 190,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(10, 0, 10, 4)
        };

        _historyToggle = FlatButton("▾ HISTORIQUE", "Afficher / masquer les derniers commits");
        _historyToggle.HorizontalContentAlignment = HorizontalAlignment.Left;
        _historyToggle.Foreground = Theme.Muted;
        _historyToggle.FontSize = 11;
        _historyToggle.FontWeight = FontWeights.SemiBold;
        _historyToggle.Padding = new Thickness(10, 5, 10, 5);
        _historyToggle.Click += (_, _) =>
        {
            bool show = _historyScroll.Visibility != Visibility.Visible;
            _historyScroll.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            _historyToggle.Content = show ? "▾ HISTORIQUE" : "▸ HISTORIQUE";
        };

        var historyArea = new StackPanel();
        historyArea.Children.Add(new Border { Height = 1, Background = Theme.Border });
        historyArea.Children.Add(_historyToggle);
        historyArea.Children.Add(_historyScroll);

        // ---------- Assemblage de la zone « dépôt » ----------
        DockPanel.SetDock(top, Dock.Top);
        _repoArea.Children.Add(top);

        DockPanel.SetDock(historyArea, Dock.Bottom);
        _repoArea.Children.Add(historyArea);

        _repoArea.Children.Add(new ScrollViewer
        {
            Content = _sections,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(10, 4, 10, 8)
        });

        // ---------- Assemblage de la vue ----------
        // Message d'information en bas, puis l'une des deux zones (état ou dépôt)
        DockPanel.SetDock(_info, Dock.Bottom);
        Children.Add(_info);

        DockPanel.SetDock(_stateArea, Dock.Top);
        Children.Add(_stateArea);
        Children.Add(_repoArea);

        _repoArea.Visibility = Visibility.Collapsed;

        // La vue devient visible : on relit l'état du dépôt
        IsVisibleChanged += async (_, _) =>
        {
            if (IsVisible)
                await RefreshAsync();
        };
    }

    /// <summary>À appeler quand l'explorateur ouvre un autre dossier.</summary>
    public void OnFolderChanged()
    {
        _root = null;
        _status = null;

        // Vue cachée : elle sera relue quand elle redeviendra visible
        if (IsVisible)
            _ = RefreshAsync();
    }

    // =====================================================================
    // Lecture de l'état
    // =====================================================================

    /// <summary>Relit l'état du dépôt et met la vue à jour.</summary>
    public async Task RefreshAsync()
    {
        // Un rafraîchissement est déjà en cours : on en refera un juste après
        if (_refreshing)
        {
            _refreshAgain = true;
            return;
        }

        _refreshing = true;

        try
        {
            do
            {
                _refreshAgain = false;
                await RefreshCoreAsync();
            }
            while (_refreshAgain);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private async Task RefreshCoreAsync()
    {
        string? folder = _getFolder();

        if (folder is null || !Directory.Exists(folder))
        {
            SetRepository(null);
            ShowState(
                "Aucun dossier ouvert",
                "Ouvrez le dossier de votre projet pour utiliser Git, ou clonez un dépôt GitHub.",
                ("Ouvrir un dossier", () =>
                {
                    ChooseFolderRequested?.Invoke();
                    return Task.CompletedTask;
                }),
                ("Cloner un dépôt…", CloneAsync));
            return;
        }

        if (!_gitFound)
        {
            _gitFound = await _git.IsInstalledAsync();

            if (!_gitFound)
            {
                SetRepository(null);
                await ShowGitMissingAsync();
                return;
            }
        }

        string? root = await _git.FindRepositoryRootAsync(folder);

        if (root is null)
        {
            SetRepository(null);
            ShowState(
                "Pas encore de dépôt Git",
                $"Le dossier « {Path.GetFileName(Path.TrimEndingDirectorySeparator(folder))} » n'est pas suivi par Git.\n"
                + "Initialisez un dépôt pour commencer à enregistrer l'historique de vos modifications.",
                ("Initialiser le dépôt", () => InitAsync(folder)),
                ("Cloner un dépôt…", CloneAsync));
            return;
        }

        GitStatus status;
        IReadOnlyList<GitCommitInfo> history;

        try
        {
            status = await _git.GetStatusAsync(root);
            history = await _git.GetHistoryAsync(root, 15);
        }
        catch (GitException ex)
        {
            SetRepository(null);
            ShowState("Erreur Git", ex.Message);
            return;
        }

        SetRepository(root);
        _status = status;
        ShowRepository(status, history);
    }

    private void SetRepository(string? root)
    {
        _root = root;

        if (root is null)
            _status = null;

        Watch(root);
    }

    // =====================================================================
    // Affichage
    // =====================================================================

    /// <summary>Affiche un message (pas de dossier, pas de dépôt, Git absent...) et des boutons d'action.</summary>
    private void ShowState(string title, string text, params (string Label, Func<Task> Action)[] actions)
    {
        _repoArea.Visibility = Visibility.Collapsed;
        _stateArea.Visibility = Visibility.Visible;
        _stateArea.Children.Clear();

        _stateArea.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = Theme.Text,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        });

        _stateArea.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = Theme.Muted,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        bool first = true;

        foreach ((string label, Func<Task> action) in actions)
        {
            Button button = FlatButton(label, null);
            button.Padding = new Thickness(8, 6, 8, 6);
            button.Margin = new Thickness(0, 0, 0, 8);
            button.Background = first ? Theme.Accent : Theme.Surface;
            button.Foreground = first ? Brushes.White : Theme.Text;
            button.Click += async (_, _) => await action();

            _stateArea.Children.Add(button);
            first = false;
        }
    }

    private async Task ShowGitMissingAsync()
    {
        bool winget = await ToolInstaller.IsWingetAvailableAsync();

        if (winget)
        {
            ShowState(
                "Git n'est pas installé",
                "Git est nécessaire pour suivre les versions de votre projet et travailler avec GitHub.",
                ("Installer Git", InstallGitAsync));
        }
        else
        {
            ShowState(
                "Git n'est pas installé",
                "Téléchargez Git sur https://git-scm.com/download/win, installez-le, "
                + "puis cliquez sur « Réessayer ».",
                ("Réessayer", RefreshAsync));
        }
    }

    private void ShowRepository(GitStatus status, IReadOnlyList<GitCommitInfo> history)
    {
        _stateArea.Visibility = Visibility.Collapsed;
        _repoArea.Visibility = Visibility.Visible;

        // ---------- Branche ----------
        string branchText = status.IsDetached ? "HEAD détaché" : status.Branch!;
        if (!status.HasCommits)
            branchText += "  (aucun commit)";

        string sync = string.Empty;
        if (status.Ahead > 0)
            sync += $"  ↑{status.Ahead}";
        if (status.Behind > 0)
            sync += $"  ↓{status.Behind}";

        _branchButton.Content = $"⎇  {branchText}{sync}";
        _branchButton.ToolTip =
            $"Dépôt : {_root}\n"
            + (status.Upstream is null
                ? "Branche locale : pas encore publiée sur GitHub (Push la publiera)."
                : status.UpstreamGone
                    ? $"La branche distante {status.Upstream} n'existe plus."
                    : $"Suit la branche distante {status.Upstream}.")
            + "\nCliquez pour changer de branche ou en créer une.";

        _pullButton.Content = status.Behind > 0 ? $"↓ Pull ({status.Behind})" : "↓ Pull";
        _pushButton.Content = status.Ahead > 0 ? $"↑ Push ({status.Ahead})" : "↑ Push";

        // ---------- Fichiers ----------
        _sections.Children.Clear();

        if (status.HasConflicts)
            AddSection("CONFLITS DE FUSION", status.Conflicts, SectionKind.Conflicts);

        if (status.HasStaged)
            AddSection("MODIFICATIONS INDEXÉES", status.Staged, SectionKind.Staged);

        AddSection("MODIFICATIONS", status.Changes, SectionKind.Changes);

        if (status.IsClean)
        {
            _sections.Children.Add(new TextBlock
            {
                Text = "Aucune modification : tout est enregistré dans l'historique.",
                Foreground = Theme.Muted,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        _commitButton.Content = status.HasStaged
            ? $"✓ Valider (commit) — {status.Staged.Count} fichier(s)"
            : "✓ Valider (commit)";

        // ---------- Historique ----------
        _history.Children.Clear();

        if (history.Count == 0)
        {
            _history.Children.Add(new TextBlock
            {
                Text = "Aucun commit pour l'instant.",
                Foreground = Theme.Muted,
                FontSize = 11.5
            });
        }

        foreach (GitCommitInfo commit in history)
            _history.Children.Add(BuildCommitRow(commit));
    }

    private enum SectionKind
    {
        Conflicts,
        Staged,
        Changes
    }

    private void AddSection(string title, IReadOnlyList<GitFileChange> changes, SectionKind kind)
    {
        // ---------- En-tête de section ----------
        var header = new DockPanel { Margin = new Thickness(0, 10, 0, 4) };

        if (kind == SectionKind.Staged)
        {
            Button unstageAll = SmallButton("−", "Désindexer tout");
            unstageAll.Click += async (_, _) => await UnstageAllAsync();
            DockPanel.SetDock(unstageAll, Dock.Right);
            header.Children.Add(unstageAll);
        }
        else if (kind == SectionKind.Changes && changes.Count > 0)
        {
            Button stageAll = SmallButton("+", "Indexer tout (préparer pour le commit)");
            stageAll.Click += async (_, _) => await StageAllAsync();
            DockPanel.SetDock(stageAll, Dock.Right);
            header.Children.Add(stageAll);
        }

        header.Children.Add(new TextBlock
        {
            Text = $"{title}  {changes.Count}",
            Foreground = kind == SectionKind.Conflicts ? Theme.Red : Theme.Muted,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });

        _sections.Children.Add(header);

        if (kind == SectionKind.Conflicts)
        {
            _sections.Children.Add(new TextBlock
            {
                Text = "Ouvrez chaque fichier, gardez la bonne version entre <<<<<<< et >>>>>>>, "
                       + "enregistrez, puis cliquez sur + pour le marquer comme résolu.",
                Foreground = Theme.Muted,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            });
        }

        // ---------- Fichiers ----------
        foreach (GitFileChange change in changes.Take(MaxFilesPerSection))
            _sections.Children.Add(BuildFileRow(change, kind));

        if (changes.Count > MaxFilesPerSection)
        {
            _sections.Children.Add(new TextBlock
            {
                Text = $"… et {changes.Count - MaxFilesPerSection} autre(s) fichier(s). "
                       + "Pensez à un fichier .gitignore (bin, obj…).",
                Foreground = Theme.Muted,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }
    }

    private UIElement BuildFileRow(GitFileChange change, SectionKind kind)
    {
        var row = new Grid
        {
            Background = Brushes.Transparent, // nécessaire pour recevoir la souris partout sur la ligne
            Cursor = Cursors.Hand,
            MinHeight = 24
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Lettre : M (modifié), A (ajouté), D (supprimé), U (nouveau), R (renommé), ! (conflit)
        var badge = new TextBlock
        {
            Text = change.Letter,
            Width = 16,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 6, 0),
            Foreground = BadgeBrush(change.Kind)
        };

        // Nom du fichier + dossier en gris
        var name = new TextBlock
        {
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };

        var fileName = new Run(change.FileName) { Foreground = Theme.Text };
        if (change.Kind == GitChangeKind.Deleted)
            fileName.TextDecorations = TextDecorations.Strikethrough;
        name.Inlines.Add(fileName);

        if (change.Folder.Length > 0)
            name.Inlines.Add(new Run("  " + change.Folder) { Foreground = Theme.Muted, FontSize = 11 });

        // Boutons d'action, visibles au survol de la ligne
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Visibility = Visibility.Hidden,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (change.Kind != GitChangeKind.Deleted)
        {
            Button open = SmallButton("✎", "Ouvrir le fichier");
            open.Click += (_, _) => OpenFile(change);
            actions.Children.Add(open);
        }

        switch (kind)
        {
            case SectionKind.Changes:
                Button discard = SmallButton("↶", change.Kind == GitChangeKind.Untracked
                    ? "Supprimer ce nouveau fichier"
                    : "Annuler les modifications");
                discard.Click += async (_, _) => await DiscardAsync(change);
                actions.Children.Add(discard);

                Button stage = SmallButton("+", "Indexer (préparer pour le commit)");
                stage.Click += async (_, _) => await StageAsync(change);
                actions.Children.Add(stage);
                break;

            case SectionKind.Staged:
                Button unstage = SmallButton("−", "Désindexer");
                unstage.Click += async (_, _) => await UnstageAsync(change);
                actions.Children.Add(unstage);
                break;

            case SectionKind.Conflicts:
                Button resolve = SmallButton("+", "Marquer comme résolu");
                resolve.Click += async (_, _) => await StageAsync(change);
                actions.Children.Add(resolve);
                break;
        }

        Grid.SetColumn(badge, 0);
        Grid.SetColumn(name, 1);
        Grid.SetColumn(actions, 2);
        row.Children.Add(badge);
        row.Children.Add(name);
        row.Children.Add(actions);

        row.MouseEnter += (_, _) =>
        {
            row.Background = Theme.Surface;
            actions.Visibility = Visibility.Visible;
        };
        row.MouseLeave += (_, _) =>
        {
            row.Background = Brushes.Transparent;
            actions.Visibility = Visibility.Hidden;
        };

        // Double-clic = voir les modifications
        row.MouseLeftButtonDown += async (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                e.Handled = true;
                await ShowDiffAsync(change);
            }
        };

        string renamed = change.OriginalPath is null ? "" : $"\nRenommé depuis : {change.OriginalPath}";
        row.ToolTip = $"{change.Path}{renamed}\n{change.Description}"
                      + (kind == SectionKind.Staged ? " — indexé" : "")
                      + "\nDouble-clic : voir les modifications";

        row.ContextMenu = BuildContextMenu(change, kind);
        return row;
    }

    private ContextMenu BuildContextMenu(GitFileChange change, SectionKind kind)
    {
        var menu = new ContextMenu();

        menu.Items.Add(MenuEntry("Voir les modifications", () => ShowDiffAsync(change)));

        if (change.Kind != GitChangeKind.Deleted)
        {
            menu.Items.Add(MenuEntry("Ouvrir le fichier", () =>
            {
                OpenFile(change);
                return Task.CompletedTask;
            }));
        }

        menu.Items.Add(new Separator());

        switch (kind)
        {
            case SectionKind.Changes:
                menu.Items.Add(MenuEntry("Indexer (+)", () => StageAsync(change)));
                menu.Items.Add(MenuEntry(change.Kind == GitChangeKind.Untracked
                    ? "Supprimer ce nouveau fichier…"
                    : "Annuler les modifications…", () => DiscardAsync(change)));
                break;

            case SectionKind.Staged:
                menu.Items.Add(MenuEntry("Désindexer (−)", () => UnstageAsync(change)));
                break;

            case SectionKind.Conflicts:
                menu.Items.Add(MenuEntry("Marquer comme résolu (+)", () => StageAsync(change)));
                break;
        }

        return menu;
    }

    private static UIElement BuildCommitRow(GitCommitInfo commit)
    {
        var text = new TextBlock
        {
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 2, 0, 4),
            ToolTip = $"{commit.Hash} — {commit.Author}, {commit.When}\n{commit.Subject}"
        };

        text.Inlines.Add(new Run(commit.Subject) { Foreground = Theme.Text });
        text.Inlines.Add(new LineBreak());
        text.Inlines.Add(new Run($"{commit.Hash} · {commit.Author} · {commit.When}")
        {
            Foreground = Theme.Muted,
            FontSize = 10.5
        });

        return text;
    }

    // =====================================================================
    // Actions sur les fichiers
    // =====================================================================

    private Task StageAsync(GitFileChange change) =>
        ExecuteAsync("Indexation…", null, () => _git.StageAsync(_root!, PathsOf(change)));

    private Task StageAllAsync() =>
        ExecuteAsync("Indexation…", null, () => _git.StageAllAsync(_root!));

    private Task UnstageAsync(GitFileChange change) =>
        ExecuteAsync("Désindexation…", null, () => _git.UnstageAsync(_root!, PathsOf(change)));

    private Task UnstageAllAsync() =>
        ExecuteAsync("Désindexation…", null, () => _git.UnstageAllAsync(_root!));

    private async Task DiscardAsync(GitFileChange change)
    {
        if (_root is null)
            return;

        string question = change.Kind == GitChangeKind.Untracked
            ? $"Supprimer le nouveau fichier « {change.Path} » ?\n\nCette action est définitive."
            : $"Annuler les modifications de « {change.Path} » ?\n\n"
              + "Le fichier reviendra à sa dernière version enregistrée dans Git. Cette action est définitive.";

        MessageBoxResult answer = MessageBox.Show(
            Owner(),
            question,
            "Git",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (answer != MessageBoxResult.Yes)
            return;

        string root = _root;
        await ExecuteAsync("Annulation des modifications…", null,
            () => _git.DiscardAsync(root, change), changesFiles: true);
    }

    private async Task ShowDiffAsync(GitFileChange change)
    {
        if (_root is null)
            return;

        string diff = await _git.GetDiffAsync(_root, change);
        new DiffWindow(Owner(), change.FileName, diff).Show();
    }

    private void OpenFile(GitFileChange change)
    {
        if (_root is null)
            return;

        string fullPath = GitService.ToFullPath(_root, change.Path);

        if (File.Exists(fullPath))
            FileOpenRequested?.Invoke(fullPath);
        else
            ShowInfo($"⚠ Fichier introuvable : {change.Path}", InfoKind.Warning);
    }

    private static IEnumerable<string> PathsOf(GitFileChange change) =>
        change.OriginalPath is null
            ? new[] { change.Path }
            : new[] { change.Path, change.OriginalPath };

    // =====================================================================
    // Commit
    // =====================================================================

    private async Task CommitAsync()
    {
        if (_root is null || _status is null || _busy)
            return;

        string root = _root;
        GitStatus status = _status;
        string message = _message.Text.Trim();

        if (status.HasConflicts)
        {
            ShowInfo("⚠ Résolvez d'abord les conflits, puis marquez chaque fichier comme résolu avec +.", InfoKind.Warning);
            return;
        }

        if (status.IsClean)
        {
            ShowInfo("Rien à valider : aucune modification.");
            return;
        }

        if (message.Length == 0)
        {
            ShowInfo("⚠ Écrivez d'abord un message qui décrit vos modifications (ex. « Ajout du menu Fichier »).", InfoKind.Warning);
            _message.Focus();
            return;
        }

        // Rien d'indexé : comme VS Code, on propose de tout valider
        bool stageAll = false;
        if (!status.HasStaged)
        {
            MessageBoxResult answer = MessageBox.Show(
                Owner(),
                "Aucune modification n'est indexée.\n\nVoulez-vous indexer toutes les modifications et les valider ?",
                "Git",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
                return;

            stageAll = true;
        }

        if (!await EnsureIdentityAsync(root))
            return;

        bool success = await ExecuteAsync("Validation (commit)…", "✅ Commit effectué. Cliquez sur « Push » pour l'envoyer sur GitHub.",
            async () =>
            {
                if (stageAll)
                {
                    GitResult staged = await _git.StageAllAsync(root);
                    if (!staged.Success)
                        return staged;
                }

                return await _git.CommitAsync(root, message);
            });

        if (success)
            _message.Clear();
    }

    /// <summary>Git exige un nom et un e-mail pour signer les commits : on les demande une seule fois.</summary>
    private async Task<bool> EnsureIdentityAsync(string root)
    {
        if (await _git.HasIdentityAsync(root))
            return true;

        string[]? values = PromptWindow.Ask(
            Owner(),
            "Identité Git",
            "Git a besoin de votre nom et de votre e-mail pour signer vos commits.\n"
            + "Utilisez de préférence l'e-mail de votre compte GitHub. (Demandé une seule fois sur ce PC.)",
            ("Nom", ""),
            ("E-mail", ""));

        if (values is null)
            return false;

        if (values[0].Length == 0 || !values[1].Contains('@'))
        {
            ShowInfo("⚠ Nom ou e-mail invalide : le commit n'a pas été fait.", InfoKind.Warning);
            return false;
        }

        GitResult result = await _git.SetIdentityAsync(root, values[0], values[1]);
        if (!result.Success)
        {
            (string text, InfoKind kind) = DescribeFailure(result);
            ShowInfo(text, kind, fullText: result.Message);
            return false;
        }

        return true;
    }

    // =====================================================================
    // Pull / Push / Branches
    // =====================================================================

    private async Task PullAsync()
    {
        if (_root is null || _status is null || _busy)
            return;

        if (_status.Upstream is null)
        {
            ShowInfo("Cette branche n'est pas encore liée à GitHub : faites d'abord « Push ».");
            return;
        }

        string root = _root;
        await ExecuteAsync("Pull en cours…", "✅ Pull terminé : votre projet est à jour.",
            () => _git.PullAsync(root), changesFiles: true);
    }

    private async Task PushAsync()
    {
        if (_root is null || _status is null || _busy)
            return;

        string root = _root;
        GitStatus status = _status;

        if (status.IsDetached)
        {
            ShowInfo("⚠ Impossible de pousser : aucune branche active (HEAD détaché).", InfoKind.Warning);
            return;
        }

        if (!status.HasCommits)
        {
            ShowInfo("⚠ Faites d'abord un commit avant de pousser.", InfoKind.Warning);
            return;
        }

        // Première publication de la branche : il faut un dépôt distant (origin)
        string? remote = null;
        if (status.Upstream is null)
        {
            remote = await EnsureRemoteAsync(root);
            if (remote is null)
                return;
        }

        string branch = status.Branch!;
        await ExecuteAsync(
            "Push en cours… (une fenêtre de connexion à GitHub peut s'ouvrir la première fois)",
            "✅ Push terminé : vos commits sont sur GitHub.",
            () => _git.PushAsync(root, branch, remote));
    }

    /// <summary>Nom du dépôt distant à utiliser ; s'il n'y en a pas, demande l'adresse et ajoute « origin ».</summary>
    private async Task<string?> EnsureRemoteAsync(string root)
    {
        IReadOnlyList<string> remotes = await _git.GetRemotesAsync(root);

        if (remotes.Contains("origin"))
            return "origin";

        if (remotes.Count > 0)
            return remotes[0];

        string[]? values = PromptWindow.Ask(
            Owner(),
            "Dépôt distant",
            "Ce projet n'est relié à aucun dépôt en ligne.\n"
            + "Créez un dépôt vide sur GitHub, puis collez son adresse ici "
            + "(ex. https://github.com/nom/projet.git).",
            ("Adresse du dépôt", ""));

        if (values is null || values[0].Length == 0)
            return null;

        GitResult result = await _git.AddRemoteAsync(root, "origin", values[0]);
        if (!result.Success)
        {
            (string text, InfoKind kind) = DescribeFailure(result);
            ShowInfo(text, kind, fullText: result.Message);
            return null;
        }

        return "origin";
    }

    private async Task ShowBranchMenuAsync()
    {
        if (_root is null || _status is null || _busy)
            return;

        string root = _root;
        GitStatus status = _status;
        GitBranches branches = await _git.GetBranchesAsync(root);

        var menu = new ContextMenu
        {
            PlacementTarget = _branchButton,
            Placement = PlacementMode.Bottom
        };

        // Branches locales (la branche actuelle est cochée)
        foreach (string branch in branches.Local)
        {
            bool current = branch == status.Branch;
            MenuItem item = MenuEntry(branch, () => SwitchAsync(root, branch, remote: false));
            item.IsChecked = current;
            item.IsEnabled = !current;
            menu.Items.Add(item);
        }

        // Branches qui n'existent que sur GitHub
        if (branches.Remote.Count > 0)
        {
            menu.Items.Add(new Separator());
            foreach (string branch in branches.Remote)
                menu.Items.Add(MenuEntry($"☁ {branch}", () => SwitchAsync(root, branch, remote: true)));
        }

        if (menu.Items.Count > 0)
            menu.Items.Add(new Separator());

        menu.Items.Add(MenuEntry("+ Nouvelle branche…", () => CreateBranchAsync(root)));
        menu.Items.Add(MenuEntry("⟳ Récupérer les branches de GitHub (fetch)", () =>
            ExecuteAsync("Fetch en cours…", "✅ Fetch terminé.", () => _git.FetchAsync(root))));

        menu.IsOpen = true;
    }

    private Task SwitchAsync(string root, string branch, bool remote)
    {
        if (_status is not null && !_status.IsClean)
            ShowInfo("Attention : vos modifications non validées suivent la branche (ou bloquent le changement).");

        return ExecuteAsync(
            $"Passage sur {branch}…",
            $"✅ Vous êtes maintenant sur la branche {(remote ? branch[(branch.IndexOf('/') + 1)..] : branch)}.",
            () => remote ? _git.SwitchToRemoteAsync(root, branch) : _git.SwitchAsync(root, branch),
            changesFiles: true);
    }

    private async Task CreateBranchAsync(string root)
    {
        string[]? values = PromptWindow.Ask(
            Owner(),
            "Nouvelle branche",
            "Nom de la nouvelle branche (ex. feature/integration-git).\n"
            + "Elle partira de la branche actuelle, avec vos modifications en cours.",
            ("Nom de la branche", "feature/"));

        if (values is null)
            return;

        string name = values[0].Replace(' ', '-');
        if (name.Length == 0 || name.EndsWith('/'))
        {
            ShowInfo("⚠ Nom de branche invalide.", InfoKind.Warning);
            return;
        }

        await ExecuteAsync($"Création de {name}…", $"✅ Branche {name} créée : vous êtes dessus.",
            () => _git.CreateBranchAsync(root, name));
    }

    // =====================================================================
    // Initialiser / cloner / installer Git
    // =====================================================================

    private Task InitAsync(string folder) =>
        ExecuteAsync("Initialisation du dépôt…",
            "✅ Dépôt créé. Écrivez un message puis « Valider » pour faire votre premier commit.",
            () => _git.InitAsync(folder));

    private async Task CloneAsync()
    {
        if (_busy)
            return;

        string[]? values = PromptWindow.Ask(
            Owner(),
            "Cloner un dépôt",
            "Adresse du dépôt à copier sur ce PC (ex. https://github.com/nom/projet.git).",
            ("Adresse du dépôt", ""));

        if (values is null || values[0].Length == 0)
            return;

        string url = values[0];

        var dialog = new OpenFolderDialog { Title = "Dans quel dossier créer la copie du dépôt ?" };
        if (dialog.ShowDialog(Owner()) != true)
            return;

        string destination = Path.Combine(dialog.FolderName, RepositoryNameFromUrl(url));

        if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any())
        {
            ShowInfo($"⚠ Le dossier « {destination} » existe déjà et n'est pas vide.", InfoKind.Warning);
            return;
        }

        bool success = await ExecuteAsync(
            "Clonage en cours… (une fenêtre de connexion à GitHub peut s'ouvrir)",
            $"✅ Dépôt cloné dans {destination}",
            () => _git.CloneAsync(url, destination));

        if (success)
            FolderOpenRequested?.Invoke(destination);
    }

    private async Task InstallGitAsync()
    {
        if (_busy)
            return;

        SetBusy(true);
        ShowInfo("Installation de Git avec winget… Acceptez la demande d'autorisation de Windows si elle apparaît.");

        try
        {
            int exitCode = await new ToolInstaller().InstallAsync(
                GitInstall,
                line => Dispatcher.BeginInvoke(new Action(() => ShowInfo(line))));

            // Git vient d'être installé : on relit le PATH (pas besoin de redémarrer)
            EnvironmentPath.Refresh();

            if (!await _git.IsInstalledAsync())
            {
                string? folder = EnvironmentPath.FindExecutableFolder(GitInstall);
                if (folder is not null)
                    EnvironmentPath.AddToUserPath(folder);
            }

            _gitFound = await _git.IsInstalledAsync();

            ShowInfo(_gitFound
                    ? "✅ Git est installé."
                    : $"⚠ Git n'est pas encore détecté (code {exitCode}). Redémarrez KEYCE KODER.",
                _gitFound ? InfoKind.Success : InfoKind.Warning);
        }
        catch (Exception ex)
        {
            ShowInfo("❌ Installation impossible : " + ex.Message, InfoKind.Error);
        }
        finally
        {
            SetBusy(false);
        }

        await RefreshAsync();
    }

    private static string RepositoryNameFromUrl(string url)
    {
        string name = url.TrimEnd('/', '\\');
        int separator = name.LastIndexOfAny(['/', '\\', ':']);
        if (separator >= 0)
            name = name[(separator + 1)..];

        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];

        foreach (char invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');

        return name.Length == 0 ? "depot" : name;
    }

    // =====================================================================
    // Exécution des commandes
    // =====================================================================

    /// <summary>
    /// Lance une opération Git : bloque la vue pendant l'opération, affiche le résultat,
    /// puis relit l'état du dépôt. Retourne true si l'opération a réussi.
    /// </summary>
    private async Task<bool> ExecuteAsync(
        string runningText,
        string? successText,
        Func<Task<GitResult>> operation,
        bool changesFiles = false)
    {
        if (_busy)
            return false;

        SetBusy(true);
        ShowInfo(runningText);

        GitResult result;

        try
        {
            result = await operation();
        }
        catch (Exception ex)
        {
            result = GitResult.Fail(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }

        // Pull, changement de branche, annulation : les onglets ouverts doivent être rechargés
        if (changesFiles)
            WorkingTreeChanged?.Invoke();

        await RefreshAsync();

        if (result.Success)
        {
            ShowInfo(successText ?? string.Empty, InfoKind.Success);
        }
        else
        {
            (string text, InfoKind kind) = DescribeFailure(result);
            ShowInfo(text, kind, fullText: result.Message);
        }

        return result.Success;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _repoArea.IsEnabled = !busy;
        _stateArea.IsEnabled = !busy;
        Cursor = busy ? Cursors.Wait : null;
    }

    /// <summary>Type de message : seules les vraies erreurs sont en rouge.</summary>
    private enum InfoKind
    {
        Normal,   // gris : en cours, information
        Success,  // vert : opération réussie
        Warning,  // orange : action à faire de votre côté (pas une erreur)
        Error     // rouge : l'opération a échoué
    }

    private void ShowInfo(string text, InfoKind kind = InfoKind.Normal, string? fullText = null)
    {
        _info.Text = text;
        _info.Foreground = kind switch
        {
            InfoKind.Success => Theme.Green,
            InfoKind.Warning => WarningBrush,
            InfoKind.Error => Theme.Red,
            _ => Theme.Muted
        };
        _info.ToolTip = string.IsNullOrWhiteSpace(fullText) ? null : fullText;
        _info.Visibility = text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Traduit les échecs Git les plus courants en une explication claire.
    /// Certains « échecs » ne sont pas des erreurs (ex. conflits à résoudre) : ils sont en orange.
    /// </summary>
    private static (string Text, InfoKind Kind) DescribeFailure(GitResult result)
    {
        string message = result.Message;
        bool Has(string text) => message.Contains(text, StringComparison.OrdinalIgnoreCase);

        if (Has("CONFLICT") || Has("Automatic merge failed"))
            return ("⚠ Conflits de fusion : ouvrez les fichiers de la section CONFLITS, gardez la bonne version, "
                    + "enregistrez, cliquez sur + puis « Valider ».", InfoKind.Warning);

        if (Has("non-fast-forward") || Has("fetch first") || Has("[rejected]"))
            return ("⚠ GitHub contient des commits que vous n'avez pas encore : faites d'abord « Pull », puis « Push ».",
                    InfoKind.Warning);

        if (Has("would be overwritten") || Has("Please commit your changes or stash them"))
            return ("⚠ Vos modifications non validées bloquent l'opération : validez-les (commit) ou annulez-les d'abord.",
                    InfoKind.Warning);

        if (Has("nothing to commit"))
            return ("Rien à valider : aucune modification.", InfoKind.Normal);

        if (Has("Already up to date") || Has("Everything up-to-date"))
            return ("Déjà à jour.", InfoKind.Success);

        if (Has("already exists"))
            return ("❌ Ce nom existe déjà (branche ou dossier).", InfoKind.Error);

        if (Has("Authentication failed") || Has("could not read Username") || Has("terminal prompts disabled"))
            return ("❌ Connexion à GitHub refusée : vérifiez votre compte (une fenêtre de connexion doit s'ouvrir).",
                    InfoKind.Error);

        if (Has("Permission denied") || Has("403"))
            return ("❌ Accès refusé : votre compte n'a pas le droit d'écrire dans ce dépôt.", InfoKind.Error);

        if (Has("Repository not found") || Has("does not appear to be a git repository"))
            return ("❌ Dépôt introuvable : vérifiez l'adresse (et vos droits d'accès).", InfoKind.Error);

        if (Has("Could not resolve host") || Has("unable to access"))
            return ("❌ Impossible de joindre GitHub : vérifiez votre connexion Internet.", InfoKind.Error);

        return ("❌ " + Shorten(message), InfoKind.Error);
    }

    /// <summary>Garde les dernières lignes d'un message Git (souvent long).</summary>
    private static string Shorten(string message)
    {
        string[] lines = message
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        string text = string.Join("\n", lines.TakeLast(6));
        if (text.Length == 0)
            text = "La commande Git a échoué.";

        return text.Length > 500 ? text[..500] + "…" : text;
    }

    // =====================================================================
    // Surveillance des fichiers : la vue se met à jour toute seule
    // =====================================================================

    private void Watch(string? root)
    {
        if (string.Equals(root, _watchedRoot, StringComparison.OrdinalIgnoreCase))
            return;

        _watcher?.Dispose();
        _watcher = null;
        _watchedRoot = root;

        if (root is null)
            return;

        try
        {
            var watcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
            };

            watcher.Changed += OnFileSystemChanged;
            watcher.Created += OnFileSystemChanged;
            watcher.Deleted += OnFileSystemChanged;
            watcher.Renamed += OnFileSystemChanged;
            watcher.EnableRaisingEvents = true;

            _watcher = watcher;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or PlatformNotSupportedException)
        {
            // Pas de surveillance possible : le bouton ⟳ reste disponible
        }
    }

    // Appelé sur un thread de fond : on repasse sur le thread de l'interface
    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        if (IsRelevant(e.FullPath))
            Dispatcher.BeginInvoke(new Action(ScheduleRefresh));
    }

    private void ScheduleRefresh()
    {
        // Vue cachée : elle sera relue quand elle redeviendra visible
        if (!IsVisible)
            return;

        // On redémarre le minuteur : un seul rafraîchissement après une série de changements
        _refreshTimer.Stop();
        _refreshTimer.Start();
    }

    private static bool IsRelevant(string fullPath)
    {
        string[] parts = fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string name = parts[^1];

        if (name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
            return false;

        int gitIndex = Array.FindIndex(parts, part => part.Equals(".git", StringComparison.OrdinalIgnoreCase));
        if (gitIndex >= 0)
        {
            // Dans .git : seuls l'index, HEAD et les références comptent (commit, branche, pull...)
            return name is "index" or "HEAD"
                   || (gitIndex + 1 < parts.Length && parts[gitIndex + 1] == "refs");
        }

        return !parts.Any(part =>
            part.Equals(".vs", StringComparison.OrdinalIgnoreCase)
            || part.Equals("node_modules", StringComparison.OrdinalIgnoreCase));
    }

    // =====================================================================
    // Petits outils d'interface
    // =====================================================================

    private Window Owner() => Window.GetWindow(this) ?? Application.Current.MainWindow;

    private static Button FlatButton(string content, string? toolTip)
    {
        return new Button
        {
            Content = content,
            ToolTip = toolTip,
            Template = FlatTemplate,
            Background = Brushes.Transparent,
            Foreground = Theme.Text,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 4, 6, 4),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
            Focusable = false
        };
    }

    private static Button SmallButton(string glyph, string toolTip)
    {
        Button button = FlatButton(glyph, toolTip);
        button.Width = 22;
        button.Height = 22;
        button.Padding = new Thickness(0);
        button.FontSize = 13;
        button.Margin = new Thickness(1, 0, 0, 0);
        return button;
    }

    private static MenuItem MenuEntry(string header, Func<Task> action)
    {
        // Header en TextBlock : les « _ » des noms de branches restent affichés tels quels
        var item = new MenuItem { Header = new TextBlock { Text = header } };
        item.Click += async (_, _) => await action();
        return item;
    }

    private static Brush BadgeBrush(GitChangeKind kind) => kind switch
    {
        GitChangeKind.Added or GitChangeKind.Untracked => Theme.Green,
        GitChangeKind.Deleted or GitChangeKind.Conflicted => Theme.Red,
        GitChangeKind.Renamed or GitChangeKind.Copied => RenamedBrush,
        _ => ModifiedBrush
    };

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
