// AiPanel.cs – Panneau KEYCE IA : refonte visuelle + API complète pour MainWindow
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KEYCE_KODER.App.Services;
using KEYCE_KODER.AI;

namespace KEYCE_KODER.App.Views
{
    public enum AiMode { Ask, Agent }
    public enum AssistantVisualState { Ready, Thinking, Generating, ExecutingAction, Error, Interrupted }

    /// <summary>
    /// Panneau de l'assistant IA de KEYCE KODER.
    /// </summary>
    public sealed class AiPanel : Border
    {
        // ── Événements publics ───────────────────────────────────────────────────
        public event Action<string, AiMode, string, bool, IReadOnlyList<string>>? MessageSubmitted;
        public event Action<AiMode, string>? ConversationSettingsChanged;
        public event Action? ClearRequested;
        public event Action? HistoryRequested;
        public event Action<Guid>? ConversationSelected;
        public event Action<Guid>? ConversationDeleted;
        public event Action? CloseRequested;
        public event Action? StopRequested;
        public event Action? RetryRequested;
        public event Action<string>? ProposalAccepted;
        public event Action<string>? ProposalRejected;
        public event Action? ModelsRefreshRequested;

        // ── Contrôles UI ─────────────────────────────────────────────────────────
        private readonly ComboBox      _modelSelector     = new();
        private readonly Button        _askModeBtn        = new();
        private readonly Button        _agentModeBtn      = new();
        private readonly Button        _thinkingBtn       = new();
        private readonly Button        _historyBtn        = new();
        private readonly Button        _clearBtn          = new();
        private readonly Button        _closeBtn          = new();
        private readonly Border        _statusDot         = new();
        private readonly TextBlock     _statusText        = new();
        private readonly Border        _historyOverlay    = new();
        private readonly StackPanel    _historyItemsHost  = new();
        private readonly TextBlock     _historyCountText  = new();
        private readonly ScrollViewer  _chatScroll        = new();
        private readonly StackPanel    _chatMessages      = new();
        private readonly StackPanel    _attachmentsHost   = new();
        private readonly List<string>  _currentAttachments = new();
        private readonly TextBox       _inputBox          = new();
        private readonly TextBlock     _inputPlaceholder  = new();
        private readonly Button        _attachBtn         = new();
        private readonly Button        _sendBtn           = new();
        private readonly Button        _stopBtn           = new();
        private readonly DispatcherTimer _animTimer       = new() { Interval = TimeSpan.FromMilliseconds(350) };
        private readonly Button _refreshModelsBtn = new();

        // ── État interne ─────────────────────────────────────────────────────────
        private int    _dotCount;
        private string _baseStatusMessage = "Génération de réponse";
        private bool   _isGenerating;
        private string _lastSubmittedPrompt = string.Empty;
        private AssistantVisualState _currentVisualState = AssistantVisualState.Ready;
        private bool   _isRestoringConversationSettings;
        private AiMode _currentMode    = AiMode.Ask;
        private bool   _thinkingEnabled = true;

        public bool IsGenerating    => _isGenerating;
        public bool IsHistoryVisible => _historyOverlay.Visibility == Visibility.Visible;

        // ────────────────────────────────────────────────────────────────────────
        public AiPanel()
        {
            Background      = Theme.Sidebar;
            BorderBrush     = Theme.Border;
            BorderThickness = new Thickness(0);

            _animTimer.Tick += (_, _) =>
            {
                _dotCount = (_dotCount % 3) + 1;
                var dots = new string('.', _dotCount);
                _statusText.Text = $"{_baseStatusMessage}{dots}";
            };

            _thinkingEnabled = AppSettings.Current.AiThinkingEnabled;

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });           // Header
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });           // Status bar
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Chat
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });           // Attachments
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });           // Input

            BuildHeader(root);
            BuildStatusBar(root);
            BuildChatArea(root);
            BuildAttachmentsArea(root);
            BuildInputArea(root);
            BuildHistoryOverlay(root);

            Child = root;

            ShowWelcomeMessage();
            SetAssistantState(AssistantVisualState.Ready);
        }

        // ════════════════════════════════════════════════════════════════════════
        // SECTION 1 — Construction de l'UI
        // ════════════════════════════════════════════════════════════════════════

        private void BuildHeader(Grid root)
        {
            var header = new Border
            {
                Background      = Theme.PanelHeader,
                BorderBrush     = Theme.Border,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding         = new Thickness(12, 10, 12, 10)
            };

            var headerGrid = new Grid();
            headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Brand
            headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Mode
            headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Model & Thinking

            // ── Brand row ──────────────────────────────────────────────────────
            var brandRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            brandRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            brandRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var brandLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var aiBrandIcon = SvgIconFactory.Create("ai", 15);
            aiBrandIcon.Margin = new Thickness(0, 0, 6, 0);
            brandLeft.Children.Add(aiBrandIcon);
            brandLeft.Children.Add(new TextBlock
            {
                Text               = "KEYCE IA",
                FontWeight         = FontWeights.Bold,
                FontSize           = 13,
                Foreground         = Theme.Text,
                VerticalAlignment  = VerticalAlignment.Center
            });
            var assistantBadge = new Border
            {
                Background      = Theme.Surface,
                BorderBrush     = Theme.Border,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(3),
                Padding         = new Thickness(4, 1, 4, 1),
                Margin          = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text       = "Assistant",
                    FontSize   = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Theme.Muted
                }
            };
            brandLeft.Children.Add(assistantBadge);
            Grid.SetColumn(brandLeft, 0);
            brandRow.Children.Add(brandLeft);

            // Right buttons
            var rightBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            StyleIconButton(_historyBtn, "history", 14, "Historique des conversations");
            _historyBtn.Click += (_, _) => { if (!_isGenerating) HistoryRequested?.Invoke(); };
            rightBtns.Children.Add(_historyBtn);

            StyleIconButton(_clearBtn, "plus", 14, "Nouvelle conversation");
            _clearBtn.Click += (_, _) =>
            {
                if (_isGenerating) return;
                ShowHistoryView(false);
                ClearConversationMessages();
                ShowWelcomeMessage();
                SetAssistantState(AssistantVisualState.Ready);
                ClearRequested?.Invoke();
            };
            rightBtns.Children.Add(_clearBtn);

            StyleIconButton(_closeBtn, "close", 14, "Masquer le panneau IA");
            _closeBtn.Click += (_, _) => CloseRequested?.Invoke();
            rightBtns.Children.Add(_closeBtn);

            Grid.SetColumn(rightBtns, 1);
            brandRow.Children.Add(rightBtns);
            Grid.SetRow(brandRow, 0);
            headerGrid.Children.Add(brandRow);

            // ── Mode selector row ──────────────────────────────────────────────
            var modeRow  = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            var modeHost = new Border
            {
                Background      = Theme.Surface,
                BorderBrush     = Theme.Border,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(6),
                Padding         = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var modeGrid = new Grid();
            modeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            modeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StyleModeButton(_askModeBtn,   "Ask");
            StyleModeButton(_agentModeBtn, "Agent");
            _askModeBtn.Click   += (_, _) => SetMode(AiMode.Ask);
            _agentModeBtn.Click += (_, _) => SetMode(AiMode.Agent);
            Grid.SetColumn(_askModeBtn,   0);
            Grid.SetColumn(_agentModeBtn, 1);
            modeGrid.Children.Add(_askModeBtn);
            modeGrid.Children.Add(_agentModeBtn);
            modeHost.Child = modeGrid;
            modeRow.Children.Add(modeHost);
            Grid.SetRow(modeRow, 1);
            headerGrid.Children.Add(modeRow);

            // ── Model + Thinking row ───────────────────────────────────────────
            var optionsRow = new Grid();

            optionsRow.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            optionsRow.ColumnDefinitions.Add(
                new ColumnDefinition { Width = GridLength.Auto });

            optionsRow.ColumnDefinitions.Add(
                new ColumnDefinition { Width = GridLength.Auto });

            BuildModelComboBox();

            Grid.SetColumn(_modelSelector, 0);
            optionsRow.Children.Add(_modelSelector);

            // Bouton d'actualisation des modèles
            StyleIconButton(
                _refreshModelsBtn,
                "refresh",
                13,
                "Actualiser les modèles Ollama");

            _refreshModelsBtn.Click += (_, _) =>
            {
                if (!_isGenerating)
                    ModelsRefreshRequested?.Invoke();
            };

            Grid.SetColumn(_refreshModelsBtn, 1);
            optionsRow.Children.Add(_refreshModelsBtn);

            // Bouton Thinking
            _thinkingBtn.Height = 26;
            _thinkingBtn.Padding = new Thickness(8, 0, 8, 0);
            _thinkingBtn.FontSize = 11;
            _thinkingBtn.FontWeight = FontWeights.SemiBold;
            _thinkingBtn.Cursor = Cursors.Hand;

            _thinkingBtn.Click += (_, _) =>
            {
                _thinkingEnabled = !_thinkingEnabled;
                AppSettings.Current.AiThinkingEnabled = _thinkingEnabled;
                AppSettings.Current.Save();
                UpdateThinkingButton();
            };

            UpdateThinkingButton();

            Grid.SetColumn(_thinkingBtn, 2);
            optionsRow.Children.Add(_thinkingBtn);

            Grid.SetRow(optionsRow, 2);
            headerGrid.Children.Add(optionsRow);

            header.Child = headerGrid;
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            UpdateModeButtons();
        }

        // ── ComboBox sombre ────────────────────────────────────────────────────
        private void BuildModelComboBox()
        {
            var fallbackModels = new[] { "qwen3.5:latest", "qwen3.5:4b-mtp-q8_0", "qwen3.5:2b-q4_K_M", "qwen2.5-coder:latest" };
            foreach (var m in fallbackModels)
                _modelSelector.Items.Add(m);

            _modelSelector.SelectedItem = fallbackModels.FirstOrDefault(m => m == AppSettings.Current.AiModel) ?? fallbackModels[0];
            _modelSelector.Height    = 26;
            _modelSelector.FontSize  = 11;
            _modelSelector.Margin    = new Thickness(0, 0, 8, 0);
            _modelSelector.Background  = Theme.Surface;
            _modelSelector.Foreground  = Theme.Text;
            _modelSelector.BorderBrush = Theme.Border;
            _modelSelector.BorderThickness = new Thickness(1);
            _modelSelector.Padding   = new Thickness(6, 0, 4, 0);
            _modelSelector.Cursor    = Cursors.Hand;

            // Template sombre via FrameworkElementFactory
            _modelSelector.Template = BuildDarkComboTemplate();

            // Style des items
            var itemStyle = new Style(typeof(ComboBoxItem));
            itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Theme.Surface));
            itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, Theme.Text));
            itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 5, 8, 5)));
            itemStyle.Setters.Add(new Setter(Control.FontSizeProperty, 11.0));
            itemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            itemStyle.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));

            var hoverTrigger = new Trigger { Property = ComboBoxItem.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, Theme.SurfaceHover));
            hoverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Theme.Text));

            var selTrigger = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
            selTrigger.Setters.Add(new Setter(Control.BackgroundProperty, Theme.AccentSoft));
            selTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Theme.Accent));

            itemStyle.Triggers.Add(hoverTrigger);
            itemStyle.Triggers.Add(selTrigger);
            _modelSelector.ItemContainerStyle = itemStyle;

            _modelSelector.SelectionChanged += (_, _) =>
            {
                if (_isRestoringConversationSettings) return;
                if (_modelSelector.SelectedItem is string m)
                {
                    AppSettings.Current.AiModel = m;
                    AppSettings.Current.Save();
                    ConversationSettingsChanged?.Invoke(_currentMode, m);
                }
            };
        }

        /// <summary>
        /// Met à jour la liste des modèles disponibles depuis Ollama (/api/tags).
        /// </summary>
        
        public void SetAvailableModels(
            IReadOnlyList<string>? models,
            string? selectedModel = null,
            bool ollamaAvailable = true)
        {
            _isRestoringConversationSettings = true;

            try
            {
                string previousSelection =
                    selectedModel
                    ?? _modelSelector.SelectedItem as string
                    ?? AppSettings.Current.AiModel;

                _modelSelector.Items.Clear();

                if (ollamaAvailable && models is { Count: > 0 })
                {
                    var availableModels = models
                        .Where(m => !string.IsNullOrWhiteSpace(m))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    foreach (var model in availableModels)
                        _modelSelector.Items.Add(model);

                    if (availableModels.Contains(
                        previousSelection,
                        StringComparer.OrdinalIgnoreCase))
                    {
                        _modelSelector.SelectedItem =
                            availableModels.First(m =>
                                string.Equals(
                                    m,
                                    previousSelection,
                                    StringComparison.OrdinalIgnoreCase));
                    }
                    else
                    {
                        _modelSelector.SelectedIndex = 0;
                    }

                    _modelSelector.ToolTip =
                        $"{availableModels.Count} modèle(s) Ollama installé(s)";
                }
                else
                {
                    // Ollama indisponible : conserver des modèles de secours.
                    string[] fallbackModels =
                    {
                        "qwen3.5:latest",
                        "qwen3.5:4b-mtp-q8_0",
                        "qwen3.5:2b-q4_K_M",
                        "qwen2.5-coder:latest"
                    };

                    foreach (var model in fallbackModels)
                        _modelSelector.Items.Add(model);

                    _modelSelector.SelectedItem =
                        fallbackModels.Contains(previousSelection)
                            ? previousSelection
                            : fallbackModels[0];

                    _modelSelector.ToolTip =
                        "Ollama indisponible. Modèles de secours affichés.";
                }
            }
            finally
            {
                _isRestoringConversationSettings = false;
            }
        }


        private static ControlTemplate BuildDarkComboTemplate()
        {
            var template = new ControlTemplate(typeof(ComboBox));
            var root = new FrameworkElementFactory(typeof(Grid), "RootGrid");

            // ToggleButton pleine largeur
            var toggle = new FrameworkElementFactory(typeof(ToggleButton), "DropDownToggle");
            toggle.SetValue(UIElement.FocusableProperty, false);
            toggle.SetValue(ButtonBase.ClickModeProperty, ClickMode.Press);
            toggle.SetBinding(ToggleButton.IsCheckedProperty,
                new System.Windows.Data.Binding("IsDropDownOpen")
                {
                    RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent),
                    Mode = System.Windows.Data.BindingMode.TwoWay
                });

            // Template dédié pour le ToggleButton
            var toggleTemplate = new ControlTemplate(typeof(ToggleButton));
            var toggleBorder = new FrameworkElementFactory(typeof(Border));
            toggleBorder.SetBinding(Border.BackgroundProperty,
                new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            toggleBorder.SetBinding(Border.BorderBrushProperty,
                new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            toggleBorder.SetBinding(Border.BorderThicknessProperty,
                new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            toggleBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

            var toggleGrid = new FrameworkElementFactory(typeof(Grid));
            var toggleCol0 = new FrameworkElementFactory(typeof(ColumnDefinition));
            toggleCol0.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
            var toggleCol1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            toggleCol1.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
            toggleGrid.AppendChild(toggleCol0);
            toggleGrid.AppendChild(toggleCol1);

            var arrowText = new FrameworkElementFactory(typeof(TextBlock));
            arrowText.SetValue(TextBlock.TextProperty, "▾");
            arrowText.SetValue(TextBlock.FontSizeProperty, 9.5);
            arrowText.SetValue(TextBlock.ForegroundProperty, Theme.Muted);
            arrowText.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 7, 0));
            arrowText.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            arrowText.SetValue(Grid.ColumnProperty, 1);
            toggleGrid.AppendChild(arrowText);

            toggleBorder.AppendChild(toggleGrid);
            toggleTemplate.VisualTree = toggleBorder;

            toggle.SetValue(Control.TemplateProperty, toggleTemplate);
            toggle.SetBinding(Control.BackgroundProperty,
                new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            toggle.SetBinding(Control.BorderBrushProperty,
                new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            toggle.SetBinding(Control.BorderThicknessProperty,
                new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });

            root.AppendChild(toggle);

            // ContentPresenter pour afficher l'item sélectionné par-dessus le ToggleButton
            var cp = new FrameworkElementFactory(typeof(ContentPresenter), "ContentSite");
            cp.SetValue(ContentPresenter.MarginProperty, new Thickness(8, 0, 20, 0));
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            cp.SetValue(FrameworkElement.IsHitTestVisibleProperty, false);
            cp.SetBinding(ContentPresenter.ContentProperty,
                new System.Windows.Data.Binding("SelectionBoxItem") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            root.AppendChild(cp);

            // Popup
            var popup = new FrameworkElementFactory(typeof(Popup), "PART_Popup");
            popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
            popup.SetValue(Popup.AllowsTransparencyProperty, true);
            popup.SetValue(Popup.FocusableProperty, false);
            popup.SetValue(Popup.PopupAnimationProperty, PopupAnimation.Slide);
            popup.SetBinding(Popup.IsOpenProperty,
                new System.Windows.Data.Binding("IsDropDownOpen")
                {
                    RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent),
                    Mode = System.Windows.Data.BindingMode.TwoWay
                });

            var popupBorder = new FrameworkElementFactory(typeof(Border), "DropDownBorder");
            popupBorder.SetValue(Border.BackgroundProperty, Theme.Surface);
            popupBorder.SetValue(Border.BorderBrushProperty, Theme.Border);
            popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(0, 0, 4, 4));
            popupBorder.SetValue(FrameworkElement.MaxHeightProperty, 220.0);
            popupBorder.SetBinding(FrameworkElement.MinWidthProperty,
                new System.Windows.Data.Binding("ActualWidth") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });

            var scroll = new FrameworkElementFactory(typeof(ScrollViewer), "DropDownScrollViewer");
            scroll.SetValue(ScrollViewer.CanContentScrollProperty, true);
            scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            scroll.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);

            var ip = new FrameworkElementFactory(typeof(ItemsPresenter));
            scroll.AppendChild(ip);
            popupBorder.AppendChild(scroll);
            popup.AppendChild(popupBorder);
            root.AppendChild(popup);

            template.VisualTree = root;
            return template;
        }

        private void BuildStatusBar(Grid root)
        {
            var bar = new Border
            {
                Background      = Theme.Panel,
                BorderBrush     = Theme.Border,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding         = new Thickness(10, 4, 10, 4)
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _statusDot.Width  = 7; _statusDot.Height = 7;
            _statusDot.CornerRadius = new CornerRadius(3.5);
            _statusDot.Background   = Theme.Green;
            _statusDot.Margin       = new Thickness(0, 0, 6, 0);
            sp.Children.Add(_statusDot);

            _statusText.Text               = "Prêt";
            _statusText.Foreground         = Theme.Muted;
            _statusText.FontSize           = 11;
            _statusText.VerticalAlignment  = VerticalAlignment.Center;
            sp.Children.Add(_statusText);

            bar.Child = sp;
            Grid.SetRow(bar, 1);
            root.Children.Add(bar);
        }

        private void BuildChatArea(Grid root)
        {
            _chatScroll.VerticalScrollBarVisibility   = ScrollBarVisibility.Auto;
            _chatScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            _chatMessages.Margin = new Thickness(10);
            _chatScroll.Content  = _chatMessages;
            Grid.SetRow(_chatScroll, 2);
            root.Children.Add(_chatScroll);
        }

        private void BuildAttachmentsArea(Grid root)
        {
            _attachmentsHost.Orientation = Orientation.Horizontal;
            _attachmentsHost.Margin      = new Thickness(10, 4, 10, 0);
            Grid.SetRow(_attachmentsHost, 3);
            root.Children.Add(_attachmentsHost);
        }

        private void BuildInputArea(Grid root)
        {
            var inputHost = new Border
            {
                Background      = Theme.PanelHeader,
                BorderBrush     = Theme.Border,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding         = new Thickness(10, 8, 10, 10)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var textWrapper = new Grid();

            _inputBox.Background   = Theme.Surface;
            _inputBox.Foreground   = Theme.Text;
            _inputBox.CaretBrush   = Theme.Text;
            _inputBox.BorderBrush  = Theme.Border;
            _inputBox.BorderThickness = new Thickness(1);
            _inputBox.Padding      = new Thickness(8, 6, 8, 6);
            _inputBox.FontSize     = 12;
            _inputBox.AcceptsReturn = true;
            _inputBox.TextWrapping = TextWrapping.Wrap;
            _inputBox.MinHeight    = 60;
            _inputBox.MaxHeight    = 140;
            _inputBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            _inputPlaceholder.Text             = "Demandez à KEYCE IA… (Entrée pour envoyer)";
            _inputPlaceholder.Foreground       = Theme.Muted;
            _inputPlaceholder.FontSize         = 12;
            _inputPlaceholder.Margin           = new Thickness(10, 7, 0, 0);
            _inputPlaceholder.IsHitTestVisible = false;

            _inputBox.TextChanged += (_, _) =>
            {
                _inputPlaceholder.Visibility = string.IsNullOrEmpty(_inputBox.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                if (!_isGenerating && (_currentVisualState == AssistantVisualState.Interrupted || _currentVisualState == AssistantVisualState.Error))
                    SetAssistantState(AssistantVisualState.Ready);
            };
            _inputBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    e.Handled = true;
                    Submit();
                }
            };

            textWrapper.Children.Add(_inputBox);
            textWrapper.Children.Add(_inputPlaceholder);
            Grid.SetRow(textWrapper, 0);
            grid.Children.Add(textWrapper);

            var actionsRow = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            actionsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StyleIconButton(_attachBtn, "attach", 14, "Joindre un fichier de code");
            StyleIconButton(_sendBtn,   "send",   14, "Envoyer la requête");
            StyleIconButton(_stopBtn,   "stop",   14, "Arrêter la génération");
            _stopBtn.Visibility = Visibility.Collapsed;
            _sendBtn.Visibility = Visibility.Visible;
            _sendBtn.Click += (_, _) => Submit();
            _stopBtn.Click += (_, _) => StopGeneration();

            var btnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            btnStack.Children.Add(_attachBtn);
            btnStack.Children.Add(_sendBtn);
            btnStack.Children.Add(_stopBtn);
            Grid.SetColumn(btnStack, 1);
            actionsRow.Children.Add(btnStack);

            Grid.SetRow(actionsRow, 1);
            grid.Children.Add(actionsRow);

            inputHost.Child = grid;
            Grid.SetRow(inputHost, 4);
            root.Children.Add(inputHost);
        }

        private void BuildHistoryOverlay(Grid root)
        {
            _historyOverlay.Background      = Theme.Sidebar;
            _historyOverlay.BorderBrush     = Theme.Border;
            _historyOverlay.BorderThickness = new Thickness(0, 1, 0, 0);
            _historyOverlay.Visibility      = Visibility.Collapsed;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Header de l'overlay historique
            var headerBorder = new Border
            {
                Background      = Theme.PanelHeader,
                BorderBrush     = Theme.Border,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding         = new Thickness(10, 8, 10, 8)
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var backBtn = new Button
            {
                Content         = SvgIconFactory.Create("chevron-left", 14),
                ToolTip         = "Retour à la conversation",
                Background      = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Width           = 26, Height = 26,
                Margin          = new Thickness(0, 0, 6, 0),
                Cursor          = Cursors.Hand
            };
            backBtn.Click += (_, _) => ShowHistoryView(false);
            leftStack.Children.Add(backBtn);
            leftStack.Children.Add(new TextBlock
            {
                Text              = "Historique",
                FontWeight        = FontWeights.Bold,
                FontSize          = 13,
                Foreground        = Theme.Text,
                VerticalAlignment = VerticalAlignment.Center
            });
            _historyCountText.FontSize   = 11;
            _historyCountText.Foreground = Theme.Muted;
            _historyCountText.Margin     = new Thickness(6, 0, 0, 0);
            leftStack.Children.Add(_historyCountText);
            Grid.SetColumn(leftStack, 0);
            headerGrid.Children.Add(leftStack);

            var newConvBtn = new Button
            {
                Background      = Theme.Surface,
                BorderBrush     = Theme.Border,
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(8, 4, 8, 4),
                Cursor          = Cursors.Hand,
                ToolTip         = "Créer une nouvelle conversation"
            };
            var newSp = new StackPanel { Orientation = Orientation.Horizontal };
            var plusIcon = SvgIconFactory.Create("plus", 11);
            plusIcon.Margin = new Thickness(0, 0, 4, 0);
            newSp.Children.Add(plusIcon);
            newSp.Children.Add(new TextBlock { Text = "Nouveau", FontSize = 11, Foreground = Theme.Text, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            newConvBtn.Content = newSp;
            newConvBtn.Click  += (_, _) => { ShowHistoryView(false); ClearRequested?.Invoke(); };
            Grid.SetColumn(newConvBtn, 1);
            headerGrid.Children.Add(newConvBtn);

            headerBorder.Child = headerGrid;
            Grid.SetRow(headerBorder, 0);
            grid.Children.Add(headerBorder);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(10, 8, 10, 10)
            };
            _historyItemsHost.Orientation = Orientation.Vertical;
            scroll.Content = _historyItemsHost;
            Grid.SetRow(scroll, 1);
            grid.Children.Add(scroll);

            _historyOverlay.Child = grid;
            Grid.SetRow(_historyOverlay, 1);
            Grid.SetRowSpan(_historyOverlay, 4);
            root.Children.Add(_historyOverlay);
        }

        // ════════════════════════════════════════════════════════════════════════
        // SECTION 2 — API publique (contrat avec MainWindow)
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Affiche un message de l'assistant, avec rendu Markdown.</summary>
        public void AddAssistantMessage(
            string text,
            bool   isError        = false,
            string? proposalAction = null,
            string? proposalId    = null,
            bool   isTruncated    = false,
            string? warningMessage = null,
            bool   allowRetry     = false)
        {
            var container = new Border
            {
                Background      = isError ? new SolidColorBrush(Color.FromArgb(30, 242, 117, 119)) : Theme.Surface,
                BorderBrush     = isError ? Theme.Red : Theme.Border,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(6),
                Padding         = new Thickness(10, 8, 10, 8),
                Margin          = new Thickness(0, 0, 0, 8),
                MaxWidth        = double.PositiveInfinity
            };

            var stack = new StackPanel { Orientation = Orientation.Vertical };

            // En-tête du message assistant : Nom de l'assistant + Bouton Copier la réponse complète
            var headerRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var roleTag = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var iconAi = SvgIconFactory.Create("ai", 12);
            iconAi.Margin = new Thickness(0, 0, 4, 0);
            roleTag.Children.Add(iconAi);
            roleTag.Children.Add(new TextBlock
            {
                Text = "KEYCE IA",
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = isError ? Theme.Red : Theme.Accent,
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(roleTag, 0);
            headerRow.Children.Add(roleTag);

            var copyStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var copyIcon = SvgIconFactory.Create("copy", 11);
            copyIcon.Margin = new Thickness(0, 0, 4, 0);
            var copyLabel = new TextBlock
            {
                Text = "Copier",
                FontSize = 10,
                Foreground = Theme.Muted,
                VerticalAlignment = VerticalAlignment.Center
            };
            copyStack.Children.Add(copyIcon);
            copyStack.Children.Add(copyLabel);

            var copyAllBtn = new Button
            {
                Content = copyStack,
                ToolTip = "Copier l'intégralité de la réponse",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 2, 4, 2),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            copyAllBtn.MouseEnter += (_, _) => copyAllBtn.Background = Theme.SurfaceHover;
            copyAllBtn.MouseLeave += (_, _) => copyAllBtn.Background = Brushes.Transparent;

            string fullResponseText = text;
            copyAllBtn.Click += (_, _) =>
            {
                try
                {
                    Clipboard.SetText(fullResponseText);
                    copyStack.Children.Clear();
                    var checkIcon = SvgIconFactory.Create("check", 11);
                    checkIcon.Margin = new Thickness(0, 0, 4, 0);
                    copyStack.Children.Add(checkIcon);
                    copyStack.Children.Add(new TextBlock
                    {
                        Text = "Copié !",
                        FontSize = 10,
                        Foreground = Theme.Green,
                        FontWeight = FontWeights.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center
                    });

                    var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    t.Tick += (_, _) =>
                    {
                        t.Stop();
                        copyStack.Children.Clear();
                        var restoredIcon = SvgIconFactory.Create("copy", 11);
                        restoredIcon.Margin = new Thickness(0, 0, 4, 0);
                        copyStack.Children.Add(restoredIcon);
                        copyStack.Children.Add(copyLabel);
                    };
                    t.Start();
                }
                catch
                {
                    // Ignorer les éventuelles restrictions d'accès au presse-papiers
                }
            };
            Grid.SetColumn(copyAllBtn, 1);
            headerRow.Children.Add(copyAllBtn);

            stack.Children.Add(headerRow);

            // Contenu Markdown principal
            var rendered = AiMarkdownRenderer.Render(text, isError);
            stack.Children.Add(rendered);

            // Avertissement de troncature
            if (isTruncated && !string.IsNullOrWhiteSpace(warningMessage))
            {
                var warnBorder = new Border
                {
                    Background      = new SolidColorBrush(Color.FromArgb(25, 255, 204, 0)),
                    BorderBrush     = (Brush)Theme.Yellow,
                    BorderThickness = new Thickness(1),
                    CornerRadius    = new CornerRadius(4),
                    Padding         = new Thickness(8, 5, 8, 5),
                    Margin          = new Thickness(0, 6, 0, 0)
                };
                warnBorder.Child = new TextBlock
                {
                    Text        = "⚠ " + warningMessage,
                    FontSize    = 11,
                    Foreground  = (Brush)Theme.Yellow,
                    TextWrapping = TextWrapping.Wrap
                };
                stack.Children.Add(warnBorder);
            }

            // Bloc de proposition (Agent)
            if (!string.IsNullOrWhiteSpace(proposalAction) && !string.IsNullOrWhiteSpace(proposalId))
            {
                var proposalBorder = new Border
                {
                    Background      = Theme.Editor,
                    BorderBrush     = Theme.Border,
                    BorderThickness = new Thickness(1),
                    CornerRadius    = new CornerRadius(4),
                    Padding         = new Thickness(8),
                    Margin          = new Thickness(0, 8, 0, 0)
                };
                var propStack = new StackPanel();
                propStack.Children.Add(new TextBlock
                {
                    Text        = proposalAction,
                    FontSize    = 11,
                    FontFamily  = new FontFamily("Consolas, Courier New"),
                    Foreground  = Theme.Text,
                    TextWrapping = TextWrapping.Wrap
                });

                var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
                var capturedId = proposalId;

                var acceptBtn = new Button
                {
                    Content         = "✓ Autoriser",
                    Padding         = new Thickness(10, 5, 10, 5),
                    Margin          = new Thickness(0, 0, 8, 0),
                    Background      = Theme.AccentSoft,
                    BorderBrush     = Theme.Accent,
                    BorderThickness = new Thickness(1),
                    Foreground      = Theme.Accent,
                    FontSize        = 11,
                    FontWeight      = FontWeights.SemiBold,
                    Cursor          = Cursors.Hand
                };
                acceptBtn.Click += (_, _) =>
                {
                    acceptBtn.IsEnabled = false;
                    if (btnRow.Children.Count > 1 && btnRow.Children[1] is Button rb) rb.IsEnabled = false;
                    ProposalAccepted?.Invoke(capturedId);
                };

                var rejectBtn = new Button
                {
                    Content         = "✗ Refuser",
                    Padding         = new Thickness(10, 5, 10, 5),
                    Background      = Theme.Surface,
                    BorderBrush     = Theme.Border,
                    BorderThickness = new Thickness(1),
                    Foreground      = Theme.Muted,
                    FontSize        = 11,
                    Cursor          = Cursors.Hand
                };
                rejectBtn.Click += (_, _) =>
                {
                    rejectBtn.IsEnabled = false;
                    if (btnRow.Children.Count > 0 && btnRow.Children[0] is Button ab) ab.IsEnabled = false;
                    ProposalRejected?.Invoke(capturedId);
                };

                btnRow.Children.Add(acceptBtn);
                btnRow.Children.Add(rejectBtn);
                propStack.Children.Add(btnRow);
                proposalBorder.Child = propStack;
                stack.Children.Add(proposalBorder);
            }

            // Bouton Réessayer
            if (allowRetry)
            {
                var retryBtn = new Button
                {
                    Content         = "↺ Réessayer",
                    Padding         = new Thickness(10, 5, 10, 5),
                    Margin          = new Thickness(0, 8, 0, 0),
                    Background      = Theme.Surface,
                    BorderBrush     = Theme.Accent,
                    BorderThickness = new Thickness(1),
                    Foreground      = Theme.Accent,
                    FontSize        = 11,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Cursor          = Cursors.Hand
                };
                retryBtn.Click += (_, _) => RetryRequested?.Invoke();
                stack.Children.Add(retryBtn);
            }

            container.Child = stack;
            _chatMessages.Children.Add(container);
            _chatScroll.ScrollToEnd();
        }

        /// <summary>Affiche un message de l'utilisateur.</summary>
        public void AddUserMessage(string text)
        {
            var container = new Border
            {
                Background      = Theme.AccentSoft,
                BorderBrush     = Theme.Accent,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(6),
                Padding         = new Thickness(10, 8, 10, 8),
                Margin          = new Thickness(32, 0, 0, 8)
            };
            container.Child = new TextBlock
            {
                Text        = text,
                FontSize    = 12,
                Foreground  = Theme.Text,
                TextWrapping = TextWrapping.Wrap
            };
            _chatMessages.Children.Add(container);
            _chatScroll.ScrollToEnd();
        }

        /// <summary>Change l'état visuel du panneau (surcharge avec message optionnel).</summary>
        public void SetAssistantState(AssistantVisualState state, string? statusMessage = null)
        {
            _currentVisualState = state;
            _baseStatusMessage  = statusMessage ?? DefaultStatusFor(state);

            Brush dotColor;
            string displayText;

            switch (state)
            {
                case AssistantVisualState.Ready:
                    dotColor    = Theme.Green;
                    displayText = statusMessage ?? "Prêt";
                    _animTimer.Stop();
                    break;
                case AssistantVisualState.Thinking:
                    dotColor    = (Brush)Theme.Yellow;
                    displayText = (statusMessage ?? "Réflexion") + "...";
                    _animTimer.Start();
                    break;
                case AssistantVisualState.Generating:
                    dotColor    = (Brush)Theme.Blue;
                    displayText = (statusMessage ?? "Génération") + "...";
                    _animTimer.Start();
                    break;
                case AssistantVisualState.ExecutingAction:
                    dotColor    = Theme.Accent;
                    displayText = statusMessage ?? "Exécution d'une action";
                    _animTimer.Start();
                    break;
                case AssistantVisualState.Error:
                    dotColor    = Theme.Red;
                    displayText = statusMessage ?? "Erreur";
                    _animTimer.Stop();
                    break;
                case AssistantVisualState.Interrupted:
                    dotColor    = (Brush)Theme.Orange;
                    displayText = statusMessage ?? "Interrompu";
                    _animTimer.Stop();
                    break;
                default:
                    dotColor    = Theme.Muted;
                    displayText = string.Empty;
                    _animTimer.Stop();
                    break;
            }

            _statusDot.Background = dotColor;
            _statusText.Text      = displayText;
        }

        private static string DefaultStatusFor(AssistantVisualState state) => state switch
        {
            AssistantVisualState.Ready          => "Prêt",
            AssistantVisualState.Thinking       => "Réflexion",
            AssistantVisualState.Generating     => "Génération de réponse",
            AssistantVisualState.ExecutingAction => "Exécution d'une action",
            AssistantVisualState.Error          => "Erreur",
            AssistantVisualState.Interrupted    => "Interrompu",
            _                                   => string.Empty
        };

        
        public void SetGenerating(bool generating)
        {
            _isGenerating = generating;

            if (generating)
            {
                _sendBtn.Visibility = Visibility.Collapsed;
                _stopBtn.Visibility = Visibility.Visible;
                _stopBtn.IsEnabled = true;

                // Ne pas réinitialiser l'état visuel ici.
                // ExecuteAiRequestAsync définit déjà Thinking ou Generating.
            }
            else
            {
                _animTimer.Stop();

                _stopBtn.Visibility = Visibility.Collapsed;
                _sendBtn.Visibility = Visibility.Visible;
                _sendBtn.IsEnabled = true;

                SetAssistantState(AssistantVisualState.Ready);
            }
        }


        public void RestorePrompt(string prompt)
        {
            _inputBox.Text = prompt;
        }

        public void RestoreConversationSettings(string model, AiMode mode)
        {
            _isRestoringConversationSettings = true;
            try
            {
                _modelSelector.SelectedItem = model;
            }
            finally
            {
                _isRestoringConversationSettings = false;
            }
            _currentMode = mode;
            UpdateModeButtons();
        }

        public void RestoreConversationMessages(IReadOnlyList<AiConversationMessage> messages)
        {
            _chatMessages.Children.Clear();
            foreach (var m in messages)
            {
                if (m.Role == "user")
                    AddUserMessage(m.DisplayContent ?? m.Content);
                else if (m.Role == "assistant")
                    AddAssistantMessage(m.Content);
            }
            _chatScroll.ScrollToEnd();
        }

        public void ClearConversationMessages()
        {
            _chatMessages.Children.Clear();
        }

        public void ShowWelcomeMessage()
        {
            var welcome = new Border
            {
                Background      = Theme.Surface,
                BorderBrush     = Theme.Border,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(8),
                Padding         = new Thickness(16, 12, 16, 12),
                Margin          = new Thickness(0, 0, 0, 8)
            };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text       = "Bienvenue dans KEYCE IA",
                FontWeight = FontWeights.Bold,
                FontSize   = 14,
                Foreground = Theme.Text,
                Margin     = new Thickness(0, 0, 0, 6)
            });
            sp.Children.Add(new TextBlock
            {
                Text        = "Posez une question, demandez de l'aide sur votre code ou passez en mode Agent pour des modifications automatiques.",
                FontSize    = 12,
                Foreground  = Theme.Muted,
                TextWrapping = TextWrapping.Wrap
            });
            welcome.Child = sp;
            _chatMessages.Children.Add(welcome);
        }

        public void ShowHistoryView(bool show)
        {
            _historyOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            _historyBtn.Background     = show ? Theme.SurfaceHover : Brushes.Transparent;
        }

        public void UpdateHistoryList(IReadOnlyList<AiConversation> conversations, Guid? activeConversationId)
        {
            _historyItemsHost.Children.Clear();
            int count = conversations?.Count ?? 0;
            _historyCountText.Text = $"({count})";

            if (conversations == null || conversations.Count == 0)
            {
                _historyItemsHost.Children.Add(BuildEmptyHistoryPlaceholder());
                return;
            }

            foreach (var c in conversations.OrderByDescending(conv => conv.UpdatedAt))
            {
                bool isCurrent = activeConversationId.HasValue && c.Id == activeConversationId.Value;
                _historyItemsHost.Children.Add(BuildHistoryCard(c, isCurrent));
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // SECTION 3 — Helpers privés
        // ════════════════════════════════════════════════════════════════════════

        private void Submit()
        {
            if (_isGenerating) return;
            var text = _inputBox.Text?.Trim();
            if (string.IsNullOrEmpty(text)) return;

            _lastSubmittedPrompt = text;
            _inputBox.Text       = string.Empty;
            _inputPlaceholder.Visibility = Visibility.Visible;

            AddUserMessage(text);

            var model      = _modelSelector.SelectedItem as string ?? string.Empty;
            var attachments = _currentAttachments.ToList();
            MessageSubmitted?.Invoke(text, _currentMode, model, _thinkingEnabled, attachments);
        }

        private void StopGeneration()
        {
            StopRequested?.Invoke();
        }

        private void SetMode(AiMode mode)
        {
            if (_isGenerating) return;
            _currentMode = mode;
            if (!_isRestoringConversationSettings)
                ConversationSettingsChanged?.Invoke(_currentMode, _modelSelector.SelectedItem as string ?? string.Empty);
            UpdateModeButtons();
        }

        private void UpdateModeButtons()
        {
            bool isAsk = _currentMode == AiMode.Ask;
            _askModeBtn.Background   = isAsk   ? Theme.Accent       : Brushes.Transparent;
            _askModeBtn.Foreground   = isAsk   ? (Brush)Theme.OnAccent : Theme.Text;
            _agentModeBtn.Background = !isAsk  ? Theme.Accent       : Brushes.Transparent;
            _agentModeBtn.Foreground = !isAsk  ? (Brush)Theme.OnAccent : Theme.Text;
        }

        private void UpdateThinkingButton()
        {
            _thinkingBtn.Content = _thinkingEnabled
                ? SvgIconFactory.Create("brain", 14)
                : SvgIconFactory.Create("brain-off", 14);
            _thinkingBtn.ToolTip = _thinkingEnabled
                ? "Raisonnement (Thinking) activé - Cliquez pour désactiver"
                : "Raisonnement (Thinking) désactivé - Cliquez pour activer";
            _thinkingBtn.Background = _thinkingEnabled
                ? new SolidColorBrush(Color.FromArgb(35, 80, 200, 151))
                : Theme.Surface;
            _thinkingBtn.BorderBrush = _thinkingEnabled
                ? Theme.Green
                : Theme.Border;
            _thinkingBtn.BorderThickness = new Thickness(1);
        }

        private static void StyleIconButton(Button btn, string icon, double size, string tooltip)
        {
            btn.Content         = SvgIconFactory.Create(icon, size);
            btn.ToolTip         = tooltip;
            btn.Background      = Brushes.Transparent;
            btn.BorderThickness = new Thickness(0);
            btn.Width           = 26;
            btn.Height          = 26;
            btn.Margin          = new Thickness(0, 0, 2, 0);
            btn.Cursor          = Cursors.Hand;
        }

        private static void StyleModeButton(Button btn, string label)
        {
            btn.Content     = label;
            btn.Height      = 24;
            btn.FontSize    = 11;
            btn.FontWeight  = FontWeights.SemiBold;
            btn.BorderThickness = new Thickness(0);
            btn.Cursor      = Cursors.Hand;
            btn.Background  = Brushes.Transparent;
            btn.Foreground  = Theme.Text;
        }

        private FrameworkElement BuildEmptyHistoryPlaceholder()
        {
            var border = new Border { Padding = new Thickness(20, 50, 20, 20), HorizontalAlignment = HorizontalAlignment.Center };
            var sp     = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var icon   = SvgIconFactory.Create("history", 28);
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            icon.Margin = new Thickness(0, 0, 0, 12);
            sp.Children.Add(icon);
            sp.Children.Add(new TextBlock
            {
                Text          = "Aucune conversation enregistrée",
                FontWeight    = FontWeights.SemiBold,
                FontSize      = 13,
                Foreground    = Theme.Text,
                TextAlignment = TextAlignment.Center,
                Margin        = new Thickness(0, 0, 0, 4)
            });
            sp.Children.Add(new TextBlock
            {
                Text        = "Vos échanges avec KEYCE IA apparaîtront ici.",
                FontSize    = 11,
                Foreground  = Theme.Muted,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth    = 260
            });
            border.Child = sp;
            return border;
        }

        private Border BuildHistoryCard(AiConversation c, bool isCurrent)
        {
            var card = new Border
            {
                Background      = isCurrent ? Theme.SurfaceHover : Theme.Surface,
                BorderBrush     = isCurrent ? Theme.Accent : Theme.Border,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(6),
                Padding         = new Thickness(10, 8, 10, 8),
                Margin          = new Thickness(0, 0, 0, 8),
                Cursor          = Cursors.Hand
            };
            card.MouseEnter += (_, _) => { if (!isCurrent) card.Background = Theme.SurfaceHover; };
            card.MouseLeave += (_, _) => { if (!isCurrent) card.Background = Theme.Surface; };

            var cardGrid = new Grid();
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var titleRow  = new StackPanel { Orientation = Orientation.Horizontal };

            titleRow.Children.Add(new TextBlock
            {
                Text        = string.IsNullOrWhiteSpace(c.Title) ? "Nouvelle conversation" : c.Title,
                FontWeight  = isCurrent ? FontWeights.Bold : FontWeights.SemiBold,
                FontSize    = 12,
                Foreground  = isCurrent ? Theme.Accent : Theme.Text,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth    = 200
            });

            if (isCurrent)
            {
                titleRow.Children.Add(new Border
                {
                    Background      = Theme.AccentSoft,
                    BorderBrush     = Theme.Accent,
                    BorderThickness = new Thickness(1),
                    CornerRadius    = new CornerRadius(3),
                    Padding         = new Thickness(4, 1, 4, 1),
                    Margin          = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child           = new TextBlock { Text = "Active", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Theme.Accent }
                });
            }

            infoStack.Children.Add(titleRow);

            string timeText    = c.UpdatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            int    userMsgCount = c.Messages.Count(m => m.Role != "system");
            string msgCountText = userMsgCount <= 1 ? $"{userMsgCount} message" : $"{userMsgCount} messages";
            infoStack.Children.Add(new TextBlock
            {
                Text       = $"{timeText} • {c.ActiveMode} • {msgCountText}",
                FontSize   = 10,
                Foreground = Theme.Muted,
                Margin     = new Thickness(0, 3, 0, 0)
            });

            Grid.SetColumn(infoStack, 0);
            cardGrid.Children.Add(infoStack);

            var deleteBtn = new Button
            {
                Content         = SvgIconFactory.Create("trash", 12),
                ToolTip         = "Supprimer définitivement cette conversation",
                Background      = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Width           = 24, Height = 24,
                Cursor          = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin          = new Thickness(6, 0, 0, 0)
            };
            deleteBtn.Click += (s, e) =>
            {
                e.Handled = true;
                var res = MessageBox.Show(
                    Window.GetWindow(this),
                    $"Voulez-vous vraiment supprimer définitivement la conversation « {c.Title} » ?",
                    "Supprimer la conversation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                    ConversationDeleted?.Invoke(c.Id);
            };
            Grid.SetColumn(deleteBtn, 1);
            cardGrid.Children.Add(deleteBtn);

            card.Child = cardGrid;
            card.MouseLeftButtonUp += (_, _) =>
            {
                ConversationSelected?.Invoke(c.Id);
                ShowHistoryView(false);
            };

            return card;
        }

        public void SetModelsLoading(bool loading)
        {
            _refreshModelsBtn.IsEnabled = !loading;

            _refreshModelsBtn.ToolTip = loading
                ? "Actualisation des modèles..."
                : "Actualiser les modèles Ollama";
        }
    }
}
