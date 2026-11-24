using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KEYCE_KODER.App.Services;

namespace KEYCE_KODER.App.Views;

public sealed class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Action<AppSettings>? _onApply;

    private readonly TextBox _searchBox = new();
    private readonly StackPanel _categoryList = new();
    private readonly ScrollViewer _contentScroll = new();
    private readonly StackPanel _contentPanel = new();

    private readonly List<(string Id, string Title, string Icon)> _categories = new()
    {
        ("general", "Général", "settings"),
        ("appearance", "Apparence", "sparkles"),
        ("editor", "Éditeur", "file"),
        ("fonts", "Polices et couleurs", "tools"),
        ("languages", "Langages", "folder"),
        ("terminal", "Terminal", "terminal"),
        ("execution", "Exécution et débogage", "play"),
        ("shortcuts", "Raccourcis clavier", "search"),
        ("extensions", "Extensions", "extensions"),
        ("ai", "Intelligence artificielle", "ai"),
        ("advanced", "Avancé", "warning")
    };

    private string _currentCategory = "appearance";

    public SettingsWindow(Action<AppSettings>? onApply = null)
    {
        _settings = AppSettings.Current;
        _onApply = onApply;

        Title = "Paramètres — KEYCE KODER";
        Width = 880;
        Height = 600;
        MinWidth = 750;
        MinHeight = 450;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Theme.Window;
        Foreground = Theme.Text;

        BuildUI();
        ShowCategory(_currentCategory);
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header / Search
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer / Buttons

        // --- Header avec Recherche ---
        var header = new Border
        {
            Background = Theme.PanelHeader,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 12, 16, 12)
        };

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var titleLabel = new TextBlock
        {
            Text = "PARAMÈTRES",
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Foreground = Theme.Accent,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(titleLabel, 0);
        headerGrid.Children.Add(titleLabel);

        // Barre de recherche
        var searchHost = new Border
        {
            Background = Theme.Sidebar,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Height = 32,
            Padding = new Thickness(8, 0, 8, 0)
        };

        var searchGrid = new Grid();
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var searchIcon = SvgIconFactory.Create("search", 14);
        searchIcon.Margin = new Thickness(0, 0, 6, 0);
        searchIcon.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(searchIcon, 0);
        searchGrid.Children.Add(searchIcon);

        _searchBox.Background = Brushes.Transparent;
        _searchBox.Foreground = Theme.Text;
        _searchBox.CaretBrush = Theme.Text;
        _searchBox.BorderThickness = new Thickness(0);
        _searchBox.VerticalContentAlignment = VerticalAlignment.Center;
        _searchBox.FontSize = 12;
        _searchBox.TextChanged += (_, _) => FilterCategories(_searchBox.Text);
        Grid.SetColumn(_searchBox, 1);
        searchGrid.Children.Add(_searchBox);

        searchHost.Child = searchGrid;
        Grid.SetColumn(searchHost, 1);
        headerGrid.Children.Add(searchHost);

        header.Child = headerGrid;
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        // --- Body: Sidebar catégories | Contenu ---
        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var navScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Theme.Sidebar
        };
        _categoryList.Margin = new Thickness(8, 8, 8, 8);
        navScroll.Content = _categoryList;
        Grid.SetColumn(navScroll, 0);
        body.Children.Add(navScroll);

        _contentScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _contentScroll.Background = Theme.Editor;
        _contentPanel.Margin = new Thickness(24, 20, 24, 20);
        _contentScroll.Content = _contentPanel;
        Grid.SetColumn(_contentScroll, 1);
        body.Children.Add(_contentScroll);

        Grid.SetRow(body, 1);
        root.Children.Add(body);

        // --- Footer: Appliquer & Fermer ---
        var footer = new Border
        {
            Background = Theme.PanelHeader,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16, 10, 16, 10)
        };

        var footerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var saveBtn = new Button
        {
            Content = "Enregistrer & Appliquer",
            Background = Theme.Accent,
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(16, 6, 16, 6),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 10, 0)
        };
        saveBtn.Click += (_, _) =>
        {
            _settings.Save();
            _onApply?.Invoke(_settings);
            DialogResult = true;
            Close();
        };

        var closeBtn = new Button
        {
            Content = "Fermer",
            Background = Theme.Surface,
            Foreground = Theme.Text,
            Padding = new Thickness(16, 6, 16, 6),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        closeBtn.Click += (_, _) => Close();

        footerPanel.Children.Add(saveBtn);
        footerPanel.Children.Add(closeBtn);
        footer.Child = footerPanel;

        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        Content = root;
        BuildCategoryNav();
    }

    private void BuildCategoryNav(string filter = "")
    {
        _categoryList.Children.Clear();
        foreach (var (id, title, icon) in _categories)
        {
            if (!string.IsNullOrEmpty(filter) && !title.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            bool isSelected = id == _currentCategory;
            var btn = new Button
            {
                Background = isSelected ? Theme.AccentSoft : Brushes.Transparent,
                BorderBrush = isSelected ? Theme.Accent : Brushes.Transparent,
                BorderThickness = new Thickness(isSelected ? 3 : 0, 0, 0, 0),
                Height = 36,
                Margin = new Thickness(0, 1, 0, 1),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(12, 0, 8, 0),
                Cursor = Cursors.Hand
            };

            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var iconElem = SvgIconFactory.Create(icon, 16);
            iconElem.Margin = new Thickness(0, 0, 10, 0);
            sp.Children.Add(iconElem);

            sp.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = isSelected ? Theme.Text : Theme.Muted,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            });

            btn.Content = sp;
            btn.Click += (_, _) =>
            {
                _currentCategory = id;
                BuildCategoryNav(_searchBox.Text);
                ShowCategory(id);
            };
            _categoryList.Children.Add(btn);
        }
    }

    private void FilterCategories(string query)
    {
        BuildCategoryNav(query);
    }

    private void ShowCategory(string categoryId)
    {
        _contentPanel.Children.Clear();

        var title = _categories.FirstOrDefault(c => c.Id == categoryId).Title ?? categoryId;
        _contentPanel.Children.Add(new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = Theme.Accent,
            Margin = new Thickness(0, 0, 0, 16)
        });

        switch (categoryId)
        {
            case "general":
                BuildGeneralOptions();
                break;
            case "appearance":
                BuildAppearanceOptions();
                break;
            case "editor":
                BuildEditorOptions();
                break;
            case "fonts":
                BuildFontOptions();
                break;
            case "languages":
                BuildLanguagesOptions();
                break;
            case "terminal":
                BuildTerminalOptions();
                break;
            case "execution":
                BuildExecutionOptions();
                break;
            case "shortcuts":
                BuildShortcutsOptions();
                break;
            case "extensions":
                BuildExtensionsOptions();
                break;
            case "ai":
                BuildAiOptions();
                break;
            case "advanced":
                BuildAdvancedOptions();
                break;
        }
    }

    private void BuildGeneralOptions()
    {
        AddSection("Comportement général");
        AddCheckbox("Confirmer la fermeture de l'application", _settings.ConfirmOnExit, v => _settings.ConfirmOnExit = v);
        AddCheckbox("Restaurer les onglets ouverts au démarrage", _settings.RestoreTabs, v => _settings.RestoreTabs = v);
        AddCheckbox("Restaurer l'état et la disposition des panneaux", _settings.RestorePanels, v => _settings.RestorePanels = v);

        AddSection("Sauvegarde automatique");
        AddCheckbox("Activer la sauvegarde automatique (AutoSave)", _settings.AutoSave, v => _settings.AutoSave = v);
    }

    private void BuildAppearanceOptions()
    {
        AddSection("Thème de l'interface");
        var themes = new[]
        {
            ("midnight-orange", "Bleu nuit & Orange (Signature Astou - Recommandé)"),
            ("dark-violet", "Sombre Violet (Classique)"),
            ("dark-blue", "Sombre Bleu"),
            ("graphite", "Sombre Graphite"),
            ("light", "Thème Clair (Light)")
        };

        var combo = new ComboBox
        {
            Width = 360,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 16)
        };

        int selectedIdx = 0;
        for (int i = 0; i < themes.Length; i++)
        {
            combo.Items.Add(new ComboBoxItem { Content = themes[i].Item2, Tag = themes[i].Item1 });
            if (themes[i].Item1 == _settings.ThemeName) selectedIdx = i;
        }
        combo.SelectedIndex = selectedIdx;

        // Prévisualisation du thème
        var previewBorder = new Border
        {
            Width = 400,
            Height = 70,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Theme.Border,
            Background = Theme.Window,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 16),
            Padding = new Thickness(8)
        };

        var previewSp = new StackPanel { Orientation = Orientation.Horizontal };
        var pSidebar = new Border { Width = 60, Background = Theme.Sidebar, CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 6, 0) };
        var pEditor = new Border { Width = 220, Background = Theme.Editor, CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 6, 0) };
        var pAccent = new Border { Width = 60, Background = Theme.Accent, CornerRadius = new CornerRadius(4) };
        previewSp.Children.Add(pSidebar);
        previewSp.Children.Add(pEditor);
        previewSp.Children.Add(pAccent);
        previewBorder.Child = previewSp;

        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is string themeId)
            {
                _settings.ThemeName = themeId;
                Theme.Apply(themeId);
                // Rafraîchir les couleurs du preview
                previewBorder.Background = Theme.Window;
                previewBorder.BorderBrush = Theme.Border;
                pSidebar.Background = Theme.Sidebar;
                pEditor.Background = Theme.Editor;
                pAccent.Background = Theme.Accent;
            }
        };

        _contentPanel.Children.Add(new TextBlock { Text = "Sélectionnez un thème d'interface :", Foreground = Theme.Muted, Margin = new Thickness(0, 0, 0, 4) });
        _contentPanel.Children.Add(combo);
        _contentPanel.Children.Add(new TextBlock { Text = "Prévisualisation du thème en direct :", Foreground = Theme.Muted, Margin = new Thickness(0, 0, 0, 6) });
        _contentPanel.Children.Add(previewBorder);

        AddEditorBackgroundCustomization();
    }

    private void BuildEditorOptions()
    {
        AddSection("Affichage du code");
        AddCheckbox("Afficher les numéros de ligne", _settings.ShowLineNumbers, v => _settings.ShowLineNumbers = v);
        AddCheckbox("Mettre en évidence la ligne actuelle", _settings.HighlightCurrentLine, v => _settings.HighlightCurrentLine = v);
        AddCheckbox("Mettre en évidence les parenthèses correspondantes", _settings.HighlightMatchingBrackets, v => _settings.HighlightMatchingBrackets = v);
        AddCheckbox("Afficher les repères d'indentation", _settings.ShowIndentGuides, v => _settings.ShowIndentGuides = v);
        AddCheckbox("Retour automatique à la ligne (Word Wrap)", _settings.WordWrap, v => _settings.WordWrap = v);
        AddCheckbox("Afficher les espaces et caractères invisibles", _settings.ShowWhitespace, v => _settings.ShowWhitespace = v);

        AddSection("Saisie intelligente");
        AddCheckbox("Fermeture automatique des parenthèses et guillemets", _settings.AutoCloseBrackets, v => _settings.AutoCloseBrackets = v);
        AddCheckbox("Indentation automatique intelligente", _settings.AutoIndent, v => _settings.AutoIndent = v);

        AddEditorBackgroundCustomization();
    }

    private void AddEditorBackgroundCustomization()
    {
        AddSection("Fond personnalisé de l'éditeur de code");

        _contentPanel.Children.Add(new TextBlock
        {
            Text = "Personnalisez indépendamment la couleur d'arrière-plan de la zone de code. Cette option n'affecte ni le terminal, ni l'explorateur, ni les autres panneaux.",
            Foreground = Theme.Muted,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });

        // Statut actuel
        var statusText = new TextBlock
        {
            Text = string.IsNullOrEmpty(_settings.CustomEditorBackground)
                ? "État : Fond par défaut du thème actif"
                : $"État : Fond personnalisé actif ({_settings.CustomEditorBackground})",
            Foreground = string.IsNullOrEmpty(_settings.CustomEditorBackground) ? Theme.Muted : Theme.Accent,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 10)
        };
        _contentPanel.Children.Add(statusText);

        // Zone de prévisualisation de code
        var codePreviewBorder = new Border
        {
            Background = Tabs.EditorTab.GetEditorBackgroundBrush(),
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 12),
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 440
        };

        var codeSp = new StackPanel();
        codeSp.Children.Add(new TextBlock { Text = "1   def saluer(nom):", Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 170)), FontFamily = new FontFamily("Consolas"), FontSize = 12 });
        codeSp.Children.Add(new TextBlock { Text = "2       print(f\"Bonjour {nom} !\")", Foreground = new SolidColorBrush(Color.FromRgb(206, 145, 120)), FontFamily = new FontFamily("Consolas"), FontSize = 12 });
        codeSp.Children.Add(new TextBlock { Text = "3   saluer(\"KEYCE KODER\")", Foreground = Theme.Text, FontFamily = new FontFamily("Consolas"), FontSize = 12 });
        codePreviewBorder.Child = codeSp;
        _contentPanel.Children.Add(codePreviewBorder);

        // Palette de présélections rapides
        _contentPanel.Children.Add(new TextBlock
        {
            Text = "Couleurs prédéfinies :",
            Foreground = Theme.Muted,
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var presets = new (string Color, string Name)[]
        {
            ("#0A1324", "Bleu Nuit (Astou)"),
            ("#1E1E1E", "Dark VS Code"),
            ("#141E30", "Deep Navy"),
            ("#181824", "Dark Charcoal"),
            ("#282C34", "One Dark"),
            ("#272822", "Monokai"),
            ("#121212", "Noir Mat"),
            ("#000000", "Noir Absolu"),
            ("#F8F9FA", "Clair")
        };

        var palettePanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        var hexInput = new TextBox
        {
            Text = _settings.CustomEditorBackground ?? "",
            Width = 120,
            Height = 28,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Theme.Surface,
            Foreground = Theme.Text,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 8, 0)
        };

        void ApplyNewEditorBg(string? hex)
        {
            _settings.CustomEditorBackground = hex;
            Brush brush;
            if (string.IsNullOrWhiteSpace(hex))
            {
                brush = Theme.Editor;
                statusText.Text = "État : Fond par défaut du thème actif";
                statusText.Foreground = Theme.Muted;
                hexInput.Text = "";
            }
            else
            {
                try
                {
                    var c = (Color)ColorConverter.ConvertFromString(hex);
                    brush = new SolidColorBrush(c);
                    statusText.Text = $"État : Fond personnalisé actif ({hex})";
                    statusText.Foreground = Theme.Accent;
                    hexInput.Text = hex;
                }
                catch
                {
                    brush = Theme.Editor;
                }
            }
            codePreviewBorder.Background = brush;
            _onApply?.Invoke(_settings);
        }

        foreach (var p in presets)
        {
            Color c;
            try { c = (Color)ColorConverter.ConvertFromString(p.Color); } catch { continue; }
            var swatch = new Border
            {
                Width = 26,
                Height = 26,
                Background = new SolidColorBrush(c),
                BorderBrush = Theme.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 8, 8),
                Cursor = Cursors.Hand,
                ToolTip = p.Name
            };
            swatch.MouseLeftButtonUp += (_, _) => ApplyNewEditorBg(p.Color);
            palettePanel.Children.Add(swatch);
        }
        _contentPanel.Children.Add(palettePanel);

        // Saisie manuelle de couleur Hex + boutons
        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        actionRow.Children.Add(new TextBlock { Text = "Code Hex : ", Foreground = Theme.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        actionRow.Children.Add(hexInput);

        var applyHexBtn = new Button
        {
            Content = "Appliquer",
            Height = 28,
            Padding = new Thickness(10, 0, 10, 0),
            Background = Theme.Surface,
            Foreground = Theme.Text,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 8, 0)
        };
        applyHexBtn.Click += (_, _) =>
        {
            string val = hexInput.Text.Trim();
            if (!string.IsNullOrEmpty(val) && !val.StartsWith("#") && (val.Length == 6 || val.Length == 8))
                val = "#" + val;
            ApplyNewEditorBg(val);
        };
        actionRow.Children.Add(applyHexBtn);

        var resetBtn = new Button
        {
            Content = "Rétablir le fond par défaut du thème",
            Height = 28,
            Padding = new Thickness(10, 0, 10, 0),
            Background = Theme.Surface,
            Foreground = Theme.Muted,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand
        };
        resetBtn.Click += (_, _) => ApplyNewEditorBg(null);
        actionRow.Children.Add(resetBtn);

        _contentPanel.Children.Add(actionRow);
    }

    private void BuildFontOptions()
    {
        AddSection("Police de l'éditeur");

        var fonts = new[] { "Cascadia Code, Consolas", "Consolas", "Cascadia Mono", "Courier New", "Agency FB, Consolas" };
        var fontCombo = new ComboBox
        {
            Width = 300,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 12)
        };
        foreach (var f in fonts) fontCombo.Items.Add(f);
        fontCombo.SelectedItem = fonts.FirstOrDefault(f => f == _settings.EditorFontFamily) ?? fonts[0];
        fontCombo.SelectionChanged += (_, _) =>
        {
            if (fontCombo.SelectedItem is string sf) _settings.EditorFontFamily = sf;
        };
        _contentPanel.Children.Add(new TextBlock { Text = "Famille de police :", Foreground = Theme.Muted });
        _contentPanel.Children.Add(fontCombo);

        var sizeBox = new TextBox
        {
            Text = _settings.EditorFontSize.ToString("0"),
            Width = 80,
            Height = 28,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = Theme.Surface,
            Foreground = Theme.Text,
            BorderBrush = Theme.Border,
            Margin = new Thickness(0, 4, 0, 12)
        };
        sizeBox.TextChanged += (_, _) =>
        {
            if (double.TryParse(sizeBox.Text, out double d) && d >= 8 && d <= 48)
                _settings.EditorFontSize = d;
        };
        _contentPanel.Children.Add(new TextBlock { Text = "Taille de police (pt) :", Foreground = Theme.Muted });
        _contentPanel.Children.Add(sizeBox);

        AddSection("Indentation et tabulations");
        var tabSizeBox = new TextBox
        {
            Text = _settings.TabSize.ToString(),
            Width = 80,
            Height = 28,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = Theme.Surface,
            Foreground = Theme.Text,
            BorderBrush = Theme.Border,
            Margin = new Thickness(0, 4, 0, 12)
        };
        tabSizeBox.TextChanged += (_, _) =>
        {
            if (int.TryParse(tabSizeBox.Text, out int s) && s > 0 && s <= 16)
                _settings.TabSize = s;
        };
        _contentPanel.Children.Add(new TextBlock { Text = "Espaces par tabulation :", Foreground = Theme.Muted });
        _contentPanel.Children.Add(tabSizeBox);

        AddCheckbox("Insérer des espaces au lieu des tabulations", _settings.InsertSpaces, v => _settings.InsertSpaces = v);
    }

    private void BuildLanguagesOptions()
    {
        AddSection("Coloration syntaxique par langage");

        var languages = new[] { "C#", "Python", "JavaScript" };
        var langCombo = new ComboBox
        {
            Width = 200,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 12)
        };
        foreach (var l in languages) langCombo.Items.Add(l);
        langCombo.SelectedIndex = 0;
        _contentPanel.Children.Add(new TextBlock { Text = "Langage cible :", Foreground = Theme.Muted });
        _contentPanel.Children.Add(langCombo);

        var tableHost = new StackPanel { Margin = new Thickness(0, 6, 0, 16) };
        _contentPanel.Children.Add(tableHost);

        void RefreshCategories(string lang)
        {
            tableHost.Children.Clear();
            if (!_settings.SyntaxColors.TryGetValue(lang, out var categories)) return;

            foreach (var kv in categories)
            {
                var row = new Grid { Margin = new Thickness(0, 3, 0, 3), Width = 400, HorizontalAlignment = HorizontalAlignment.Left };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

                var nameBlock = new TextBlock
                {
                    Text = kv.Key,
                    Foreground = Theme.Text,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 13
                };
                Grid.SetColumn(nameBlock, 0);
                row.Children.Add(nameBlock);

                var colorBox = new TextBox
                {
                    Text = kv.Value,
                    Background = Theme.Surface,
                    Foreground = Theme.Text,
                    BorderBrush = Theme.Border,
                    Height = 26,
                    HorizontalContentAlignment = HorizontalAlignment.Center
                };
                colorBox.TextChanged += (_, _) =>
                {
                    _settings.SyntaxColors[lang][kv.Key] = colorBox.Text;
                };
                Grid.SetColumn(colorBox, 1);
                row.Children.Add(colorBox);

                tableHost.Children.Add(row);
            }
        }

        langCombo.SelectionChanged += (_, _) =>
        {
            if (langCombo.SelectedItem is string l) RefreshCategories(l);
        };
        RefreshCategories("C#");

        _contentPanel.Children.Add(new TextBlock
        {
            Text = "Note d'architecture : La personnalisation fine par jeton utilise la cartographie AvalonEdit du moteur Editor. Les schémas de couleurs personnalisés sont mémorisés pour chaque langage.",
            Foreground = Theme.Muted,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0)
        });
    }

    private void BuildTerminalOptions()
    {
        AddSection("Configuration du terminal intégré");

        var shells = new[] { "PowerShell", "Invite de commandes (CMD)", "Git Bash" };
        var shellCombo = new ComboBox
        {
            Width = 260,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 12)
        };
        foreach (var s in shells) shellCombo.Items.Add(s);
        shellCombo.SelectedIndex = 0;
        shellCombo.SelectionChanged += (_, _) =>
        {
            if (shellCombo.SelectedItem is string s) _settings.DefaultShell = s;
        };
        _contentPanel.Children.Add(new TextBlock { Text = "Shell par défaut :", Foreground = Theme.Muted });
        _contentPanel.Children.Add(shellCombo);

        var termFonts = new[] { "Consolas", "Cascadia Mono", "Lucida Console" };
        var termFontCombo = new ComboBox
        {
            Width = 260,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 12)
        };
        foreach (var tf in termFonts) termFontCombo.Items.Add(tf);
        termFontCombo.SelectedItem = termFonts.FirstOrDefault(f => f == _settings.TerminalFontFamily) ?? termFonts[0];
        termFontCombo.SelectionChanged += (_, _) =>
        {
            if (termFontCombo.SelectedItem is string tf) _settings.TerminalFontFamily = tf;
        };
        _contentPanel.Children.Add(new TextBlock { Text = "Police du terminal :", Foreground = Theme.Muted });
        _contentPanel.Children.Add(termFontCombo);
    }

    private void BuildExecutionOptions()
    {
        AddSection("Exécution multi-langages & Débogage");
        _contentPanel.Children.Add(new TextBlock
        {
            Text = "KEYCE KODER dispose d'un moteur d'exécution ScriptRunner supportant Python, JavaScript, Java, C#, C, C++ ainsi que d'un débogueur Python natif façon VS Code.",
            Foreground = Theme.Muted,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        AddCheckbox("Activer les messages d'exécution colorés", true, _ => { });
        AddCheckbox("Arrêt automatique du processus précédent au lancement d'un nouveau Run", true, _ => { });
    }

    private void BuildShortcutsOptions()
    {
        AddSection("Raccourcis clavier enregistrés");
        var shortcuts = new[]
        {
            ("Ctrl+N", "Nouveau document"),
            ("Ctrl+O", "Ouvrir un fichier"),
            ("Ctrl+S", "Enregistrer le fichier"),
            ("Ctrl+Maj+S", "Enregistrer sous…"),
            ("Ctrl+W", "Fermer l'onglet actif"),
            ("Ctrl+B", "Afficher / masquer la barre latérale"),
            ("Ctrl+F", "Rechercher"),
            ("Ctrl+H", "Remplacer"),
            ("Ctrl+K", "Recherche globale"),
            ("F5", "Exécuter le fichier actif (Run)"),
            ("Maj+F5", "Arrêter l'exécution (Stop)"),
            ("Ctrl+Tab", "Onglet suivant"),
            ("Ctrl+Maj+Tab", "Onglet précédent")
        };

        foreach (var (keys, desc) in shortcuts)
        {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3), Width = 450, HorizontalAlignment = HorizontalAlignment.Left };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var keyBadge = new Border
            {
                Background = Theme.Surface,
                BorderBrush = Theme.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock { Text = keys, Foreground = Theme.Accent, FontWeight = FontWeights.SemiBold, FontSize = 12 }
            };
            Grid.SetColumn(keyBadge, 0);
            row.Children.Add(keyBadge);

            var descBlock = new TextBlock { Text = desc, Foreground = Theme.Text, VerticalAlignment = VerticalAlignment.Center, FontSize = 13 };
            Grid.SetColumn(descBlock, 1);
            row.Children.Add(descBlock);

            _contentPanel.Children.Add(row);
        }
    }

    private void BuildExtensionsOptions()
    {
        AddSection("Extensions et bibliothèques intégrées");
        var list = new[]
        {
            ("AvalonEdit", "Moteur d'édition de code performant avec pliage et numérotation."),
            ("SharpVectors.Wpf", "Moteur de rendu d'icônes SVG vectorielles haute fidélité."),
            ("PDFsharp & PdfPig", "Lecteur et éditeur de documents PDF intégrés."),
            ("KEYCE_KODER.Terminal", "Terminal virtuel multi-shell interactif avec support Git."),
            ("KEYCE_KODER.Runner", "Runner universel de code et scripts avec débogueur pas à pas.")
        };

        foreach (var (name, desc) in list)
        {
            var card = new Border
            {
                Background = Theme.Surface,
                BorderBrush = Theme.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 480
            };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.Bold, Foreground = Theme.Text, FontSize = 13 });
            sp.Children.Add(new TextBlock { Text = desc, Foreground = Theme.Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            card.Child = sp;
            _contentPanel.Children.Add(card);
        }
    }

    private void BuildAiOptions()
    {
        AddSection("Configuration de l'assistant IA");

        var models = new[] { "Gemini 1.5 Pro", "Claude 3.5 Sonnet", "GPT-4o", "DeepSeek-R1" };
        var modelCombo = new ComboBox
        {
            Width = 260,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 12)
        };
        foreach (var m in models) modelCombo.Items.Add(m);
        modelCombo.SelectedItem = models.FirstOrDefault(m => m == _settings.AiModel) ?? models[0];
        modelCombo.SelectionChanged += (_, _) =>
        {
            if (modelCombo.SelectedItem is string m) _settings.AiModel = m;
        };
        _contentPanel.Children.Add(new TextBlock { Text = "Modèle IA par défaut :", Foreground = Theme.Muted });
        _contentPanel.Children.Add(modelCombo);

        AddCheckbox("Mode Réflexion (Thinking) actif par défaut", _settings.AiThinkingEnabled, v => _settings.AiThinkingEnabled = v);

        _contentPanel.Children.Add(new TextBlock
        {
            Text = "Le mode Ask et le mode Agent partagent le même contexte de conversation et respectent l'état Thinking configuré ici.",
            Foreground = Theme.Muted,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0)
        });
    }

    private void BuildAdvancedOptions()
    {
        AddSection("Options avancées & maintenance");

        var resetBtn = new Button
        {
            Content = "Réinitialiser tous les paramètres",
            Background = Theme.Red,
            Foreground = Brushes.White,
            Padding = new Thickness(12, 6, 12, 6),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 4, 0, 12)
        };
        resetBtn.Click += (_, _) =>
        {
            var res = MessageBox.Show(this, "Voulez-vous rétablir les paramètres d'origine ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes)
            {
                _settings.ThemeName = "midnight-orange";
                _settings.EditorFontFamily = "Cascadia Code, Consolas";
                _settings.EditorFontSize = 14;
                _settings.Save();
                Theme.Apply("midnight-orange");
                Close();
            }
        };
        _contentPanel.Children.Add(resetBtn);
    }

    private void AddSection(string title)
    {
        _contentPanel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Text,
            Margin = new Thickness(0, 12, 0, 8)
        });
    }

    private void AddCheckbox(string text, bool isChecked, Action<bool> onChanged)
    {
        var cb = new CheckBox
        {
            Content = text,
            IsChecked = isChecked,
            Foreground = Theme.Text,
            FontSize = 13,
            Margin = new Thickness(0, 4, 0, 6)
        };
        cb.Checked += (_, _) => onChanged(true);
        cb.Unchecked += (_, _) => onChanged(false);
        _contentPanel.Children.Add(cb);
    }
}
