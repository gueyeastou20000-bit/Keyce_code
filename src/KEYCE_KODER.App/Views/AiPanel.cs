using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KEYCE_KODER.App.Services;

namespace KEYCE_KODER.App.Views;

public enum AiMode
{
    Ask,
    Agent
}

public enum AssistantVisualState
{
    Ready,
    Thinking,
    Generating,
    ExecutingAction,
    Error,
    Interrupted
}

/// <summary>
/// Panneau de l'assistant IA de KEYCE KODER.
/// Présentation graphique prête à être connectée aux services d'IA.
/// </summary>
public sealed class AiPanel : Border
{
    public event Action<string, AiMode, string, bool, IReadOnlyList<string>>? MessageSubmitted;
    public event Action? ClearRequested;
    public event Action? CloseRequested;
    public event Action? StopRequested;
    public event Action? RetryRequested;
    public event Action<string>? ProposalAccepted;
    public event Action<string>? ProposalRejected;

    private readonly ComboBox _modelSelector = new();
    private readonly Button _askModeBtn = new();
    private readonly Button _agentModeBtn = new();
    private readonly Button _thinkingBtn = new();
    private readonly Button _clearBtn = new();
    private readonly Button _closeBtn = new();

    private readonly Border _statusDot = new();
    private readonly TextBlock _statusText = new();

    private readonly ScrollViewer _chatScroll = new();
    private readonly StackPanel _chatMessages = new();

    private readonly StackPanel _attachmentsHost = new();
    private readonly List<string> _currentAttachments = new();

    private readonly TextBox _inputBox = new();
    private readonly TextBlock _inputPlaceholder = new();
    private readonly Button _attachBtn = new();
    private readonly Button _sendBtn = new();
    private readonly Button _stopBtn = new();

    private readonly DispatcherTimer _animTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private int _dotCount;
    private string _baseStatusMessage = "Génération de réponse";
    private Border? _pendingBubble;
    private TextBlock? _pendingDotsText;

    private bool _isGenerating;
    private string _lastSubmittedPrompt = string.Empty;
    private AssistantVisualState _currentVisualState = AssistantVisualState.Ready;

    public bool IsGenerating => _isGenerating;

    private AiMode _currentMode = AiMode.Ask;
    private bool _thinkingEnabled = true;

    public AiPanel()
    {
        Background = Theme.Sidebar;
        BorderBrush = Theme.Border;
        BorderThickness = new Thickness(0);

        _animTimer.Tick += (_, _) =>
        {
            _dotCount = (_dotCount % 3) + 1;
            string dots = new string('.', _dotCount);
            _statusText.Text = $"{_baseStatusMessage}{dots}";
            if (_pendingDotsText != null)
            {
                _pendingDotsText.Text = dots;
            }
        };

        _thinkingEnabled = AppSettings.Current.AiThinkingEnabled;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header (Modes, Model, Thinking)
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // State status bar
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Chat history
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Attachments
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Input box & actions

        BuildHeader(root);
        BuildStatusBar(root);
        BuildChatHistory(root);
        BuildAttachmentsArea(root);
        BuildInputArea(root);

        Child = root;

        ShowWelcomeMessage();
        SetAssistantState(AssistantVisualState.Ready);
    }

    private void BuildHeader(Grid root)
    {
        var header = new Border
        {
            Background = Theme.PanelHeader,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(10, 8, 10, 8)
        };

        var headerGrid = new Grid();
        headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Row 0: Mode Ask / Agent + Clear
        var modeRow = new Grid();
        modeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        modeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var modeSelector = new Border
        {
            Background = Theme.Surface,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var modeSp = new StackPanel { Orientation = Orientation.Horizontal };

        _askModeBtn.Content = "Ask";
        _askModeBtn.Width = 60;
        _askModeBtn.Height = 26;
        _askModeBtn.FontSize = 12;
        _askModeBtn.FontWeight = FontWeights.SemiBold;
        _askModeBtn.BorderThickness = new Thickness(0);
        _askModeBtn.Cursor = Cursors.Hand;
        _askModeBtn.Click += (_, _) => SetMode(AiMode.Ask);

        _agentModeBtn.Content = "Agent";
        _agentModeBtn.Width = 60;
        _agentModeBtn.Height = 26;
        _agentModeBtn.FontSize = 12;
        _agentModeBtn.FontWeight = FontWeights.SemiBold;
        _agentModeBtn.BorderThickness = new Thickness(0);
        _agentModeBtn.Cursor = Cursors.Hand;
        _agentModeBtn.Click += (_, _) => SetMode(AiMode.Agent);

        modeSp.Children.Add(_askModeBtn);
        modeSp.Children.Add(_agentModeBtn);
        modeSelector.Child = modeSp;
        Grid.SetColumn(modeSelector, 0);
        modeRow.Children.Add(modeSelector);

        // Boutons à droite : vider et fermer
        var rightBtns = new StackPanel { Orientation = Orientation.Horizontal };

        _clearBtn.Content = SvgIconFactory.Create("trash", 14);
        _clearBtn.ToolTip = "Nouvelle conversation";
        _clearBtn.Background = Brushes.Transparent;
        _clearBtn.BorderThickness = new Thickness(0);
        _clearBtn.Width = 28;
        _clearBtn.Height = 28;
        _clearBtn.Margin = new Thickness(0, 0, 4, 0);
        _clearBtn.Cursor = Cursors.Hand;
        _clearBtn.Click += (_, _) =>
        {
            if (_isGenerating) return;
            _chatMessages.Children.Clear();
            _currentAttachments.Clear();
            _attachmentsHost.Children.Clear();
            ShowWelcomeMessage();
            SetAssistantState(AssistantVisualState.Ready);
            ClearRequested?.Invoke();
        };
        rightBtns.Children.Add(_clearBtn);

        _closeBtn.Content = SvgIconFactory.Create("close", 14);
        _closeBtn.ToolTip = "Masquer le panneau IA";
        _closeBtn.Background = Brushes.Transparent;
        _closeBtn.BorderThickness = new Thickness(0);
        _closeBtn.Width = 28;
        _closeBtn.Height = 28;
        _closeBtn.Cursor = Cursors.Hand;
        _closeBtn.Click += (_, _) => CloseRequested?.Invoke();
        rightBtns.Children.Add(_closeBtn);

        Grid.SetColumn(rightBtns, 1);
        modeRow.Children.Add(rightBtns);

        Grid.SetRow(modeRow, 0);
        headerGrid.Children.Add(modeRow);

        // Row 1: Model selector & Thinking toggle
        var optionsRow = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        optionsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        optionsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var models = new[] { "qwen3.5:latest", "qwen2.5-coder:latest" };
        foreach (var m in models) _modelSelector.Items.Add(m);
        _modelSelector.SelectedItem = models.FirstOrDefault(m => m == AppSettings.Current.AiModel) ?? models[0];
        _modelSelector.Height = 26;
        _modelSelector.FontSize = 11;
        _modelSelector.Margin = new Thickness(0, 0, 8, 0);
        _modelSelector.SelectionChanged += (_, _) =>
        {
            if (_modelSelector.SelectedItem is string m)
            {
                AppSettings.Current.AiModel = m;
                AppSettings.Current.Save();
            }
        };
        Grid.SetColumn(_modelSelector, 0);
        optionsRow.Children.Add(_modelSelector);

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
        Grid.SetColumn(_thinkingBtn, 1);
        optionsRow.Children.Add(_thinkingBtn);

        Grid.SetRow(optionsRow, 1);
        headerGrid.Children.Add(optionsRow);

        header.Child = headerGrid;
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        UpdateModeButtons();
    }

    private void BuildStatusBar(Grid root)
    {
        var bar = new Border
        {
            Background = Theme.Panel,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(10, 4, 10, 4)
        };

        var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        _statusDot.Width = 7;
        _statusDot.Height = 7;
        _statusDot.CornerRadius = new CornerRadius(3.5);
        _statusDot.Background = Theme.Green;
        _statusDot.Margin = new Thickness(0, 0, 6, 0);
        _statusDot.VerticalAlignment = VerticalAlignment.Center;
        sp.Children.Add(_statusDot);

        _statusText.Text = "Prêt";
        _statusText.Foreground = Theme.Muted;
        _statusText.FontSize = 11;
        _statusText.VerticalAlignment = VerticalAlignment.Center;
        sp.Children.Add(_statusText);

        bar.Child = sp;
        Grid.SetRow(bar, 1);
        root.Children.Add(bar);
    }

    private void BuildChatHistory(Grid root)
    {
        _chatScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _chatScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _chatMessages.Margin = new Thickness(10);
        _chatScroll.Content = _chatMessages;

        Grid.SetRow(_chatScroll, 2);
        root.Children.Add(_chatScroll);
    }

    private void BuildAttachmentsArea(Grid root)
    {
        _attachmentsHost.Orientation = Orientation.Horizontal;
        _attachmentsHost.Margin = new Thickness(10, 4, 10, 0);
        Grid.SetRow(_attachmentsHost, 3);
        root.Children.Add(_attachmentsHost);
    }

    private void BuildInputArea(Grid root)
    {
        var inputHost = new Border
        {
            Background = Theme.PanelHeader,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(10, 8, 10, 10)
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Saisie texte multiline avec placeholder
        var textWrapper = new Grid();

        _inputBox.Background = Theme.Surface;
        _inputBox.Foreground = Theme.Text;
        _inputBox.CaretBrush = Theme.Text;
        _inputBox.BorderBrush = Theme.Border;
        _inputBox.BorderThickness = new Thickness(1);
        _inputBox.Padding = new Thickness(8, 6, 8, 6);
        _inputBox.FontSize = 12;
        _inputBox.AcceptsReturn = true;
        _inputBox.TextWrapping = TextWrapping.Wrap;
        _inputBox.MinHeight = 60;
        _inputBox.MaxHeight = 140;
        _inputBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

        _inputPlaceholder.Text = "Demandez à KEYCE IA… (Entrée pour envoyer)";
        _inputPlaceholder.Foreground = Theme.Muted;
        _inputPlaceholder.FontSize = 12;
        _inputPlaceholder.Margin = new Thickness(10, 7, 0, 0);
        _inputPlaceholder.IsHitTestVisible = false;

        _inputBox.TextChanged += (_, _) =>
        {
            _inputPlaceholder.Visibility = string.IsNullOrEmpty(_inputBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (!_isGenerating && (_currentVisualState == AssistantVisualState.Interrupted || _currentVisualState == AssistantVisualState.Error))
            {
                SetAssistantState(AssistantVisualState.Ready);
            }
        };

        _inputBox.KeyDown += (s, e) =>
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

        // Actions sous le champ de saisie
        var actionsRow = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        actionsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _attachBtn.Content = SvgIconFactory.Create("attach", 14);
        _attachBtn.ToolTip = "Joindre un fichier de code";
        _attachBtn.Background = Brushes.Transparent;
        _attachBtn.BorderThickness = new Thickness(0);
        _attachBtn.Width = 26;
        _attachBtn.Height = 26;
        _attachBtn.Cursor = Cursors.Hand;
        _attachBtn.HorizontalAlignment = HorizontalAlignment.Left;
        _attachBtn.Click += (_, _) =>
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Sélectionner un fichier à joindre",
                Filter = "Fichiers de code|*.*"
            };
            if (ofd.ShowDialog() == true)
            {
                AddAttachment(ofd.FileName);
            }
        };
        Grid.SetColumn(_attachBtn, 0);
        actionsRow.Children.Add(_attachBtn);

        _sendBtn.Content = SvgIconFactory.Create("send", 14);
        _sendBtn.ToolTip = "Envoyer (Entrée)";
        _sendBtn.Background = Theme.Accent;
        _sendBtn.BorderThickness = new Thickness(0);
        _sendBtn.Width = 32;
        _sendBtn.Height = 26;
        _sendBtn.Cursor = Cursors.Hand;
        _sendBtn.Click += (_, _) => Submit();
        Grid.SetColumn(_sendBtn, 1);
        actionsRow.Children.Add(_sendBtn);

        _stopBtn.Content = SvgIconFactory.Create("stop", 14);
        _stopBtn.ToolTip = "Arrêter la génération";
        _stopBtn.Background = Theme.Red;
        _stopBtn.BorderThickness = new Thickness(0);
        _stopBtn.Width = 32;
        _stopBtn.Height = 26;
        _stopBtn.Cursor = Cursors.Hand;
        _stopBtn.Visibility = Visibility.Collapsed;
        _stopBtn.Click += (_, _) => StopRequested?.Invoke();
        Grid.SetColumn(_stopBtn, 1);
        actionsRow.Children.Add(_stopBtn);

        Grid.SetRow(actionsRow, 1);
        grid.Children.Add(actionsRow);

        inputHost.Child = grid;
        Grid.SetRow(inputHost, 4);
        root.Children.Add(inputHost);
    }

    private void SetMode(AiMode mode)
    {
        if (_currentMode == mode)
            return;

        if (_currentMode == AiMode.Ask && mode == AiMode.Agent)
        {
            var result = MessageBox.Show(
                "Vous allez passer en mode Agent.\n\n" +
                "Ce mode est destiné à planifier et exécuter des actions sur votre projet. " +
                "Aucune action ne sera lancée automatiquement.\n\n" +
                "Voulez-vous continuer ?",
                "Activer le mode Agent",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;
        }

        _currentMode = mode;
        UpdateModeButtons();
    }

    private void UpdateModeButtons()
    {
        if (_currentMode == AiMode.Ask)
        {
            _askModeBtn.Background = Theme.Accent;
            _askModeBtn.Foreground = Brushes.White;
            _agentModeBtn.Background = Brushes.Transparent;
            _agentModeBtn.Foreground = Theme.Muted;
        }
        else
        {
            _agentModeBtn.Background = Theme.Accent;
            _agentModeBtn.Foreground = Brushes.White;
            _askModeBtn.Background = Brushes.Transparent;
            _askModeBtn.Foreground = Theme.Muted;
        }
    }

    private void UpdateThinkingButton()
    {
        if (_thinkingEnabled)
        {
            _thinkingBtn.Background = Theme.AccentSoft;
            _thinkingBtn.BorderBrush = Theme.Accent;
            _thinkingBtn.BorderThickness = new Thickness(1);
            _thinkingBtn.Foreground = Theme.Accent;
            _thinkingBtn.Content = "Thinking: ON";
        }
        else
        {
            _thinkingBtn.Background = Theme.Surface;
            _thinkingBtn.BorderBrush = Theme.Border;
            _thinkingBtn.BorderThickness = new Thickness(1);
            _thinkingBtn.Foreground = Theme.Muted;
            _thinkingBtn.Content = "Thinking: OFF";
        }
    }

    public void SetAssistantState(AssistantVisualState state, string? customMessage = null)
    {
        _currentVisualState = state;
        switch (state)
        {
            case AssistantVisualState.Ready:
                _statusDot.Background = Theme.Green;
                _statusText.Text = customMessage ?? "Prêt";
                break;
            case AssistantVisualState.Thinking:
                _statusDot.Background = new SolidColorBrush(Color.FromRgb(96, 170, 255));
                _baseStatusMessage = customMessage ?? "Réflexion en cours";
                _statusText.Text = $"{_baseStatusMessage}…";
                break;
            case AssistantVisualState.Generating:
                _statusDot.Background = Theme.Accent;
                _baseStatusMessage = customMessage ?? "Génération de réponse";
                _statusText.Text = $"{_baseStatusMessage}…";
                break;
            case AssistantVisualState.ExecutingAction:
                _statusDot.Background = new SolidColorBrush(Color.FromRgb(167, 139, 250));
                _statusText.Text = customMessage ?? "Exécution d'une action…";
                break;
            case AssistantVisualState.Error:
                _statusDot.Background = Theme.Red;
                _statusText.Text = customMessage ?? "Erreur";
                break;
            case AssistantVisualState.Interrupted:
                _statusDot.Background = Theme.Muted;
                _statusText.Text = customMessage ?? "Interrompu";
                break;
        }
    }

    public void SetGenerating(bool isGenerating)
    {
        _isGenerating = isGenerating;
        _inputBox.IsEnabled = !isGenerating;
        _attachBtn.IsEnabled = !isGenerating;
        _sendBtn.Visibility = isGenerating ? Visibility.Collapsed : Visibility.Visible;
        _stopBtn.Visibility = isGenerating ? Visibility.Visible : Visibility.Collapsed;

        if (isGenerating)
        {
            _dotCount = 0;
            _animTimer.Start();

            if (_pendingBubble == null)
            {
                _pendingBubble = new Border
                {
                    Background = Theme.PanelHeader,
                    BorderBrush = Theme.Border,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 4, 20, 8),
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                var sp = new StackPanel();
                var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
                var aiIcon = SvgIconFactory.Create("ai", 13);
                aiIcon.Margin = new Thickness(0, 0, 6, 0);
                titleRow.Children.Add(aiIcon);
                titleRow.Children.Add(new TextBlock
                {
                    Text = "KEYCE IA",
                    FontWeight = FontWeights.Bold,
                    FontSize = 11,
                    Foreground = Theme.Text
                });
                sp.Children.Add(titleRow);

                var animRow = new StackPanel { Orientation = Orientation.Horizontal };
                animRow.Children.Add(new TextBlock
                {
                    Text = "Génération",
                    FontSize = 12,
                    Foreground = Theme.Muted
                });
                _pendingDotsText = new TextBlock
                {
                    Text = "...",
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = Theme.Accent,
                    Margin = new Thickness(2, 0, 0, 0)
                };
                animRow.Children.Add(_pendingDotsText);
                sp.Children.Add(animRow);

                _pendingBubble.Child = sp;
                _chatMessages.Children.Add(_pendingBubble);
                _chatScroll.ScrollToEnd();
            }
        }
        else
        {
            _animTimer.Stop();

            if (_pendingBubble != null)
            {
                _chatMessages.Children.Remove(_pendingBubble);
                _pendingBubble = null;
                _pendingDotsText = null;
            }

            _inputBox.Focus();
        }
    }

    public void RestorePrompt(string? prompt = null)
    {
        string toRestore = prompt ?? _lastSubmittedPrompt;
        if (!string.IsNullOrEmpty(toRestore))
        {
            _inputBox.Text = toRestore;
            _inputBox.CaretIndex = _inputBox.Text.Length;
            _inputBox.Focus();
        }
    }

    public void AddAttachment(string filePath)
    {
        if (_currentAttachments.Contains(filePath)) return;
        _currentAttachments.Add(filePath);

        string fileName = System.IO.Path.GetFileName(filePath);
        var badge = new Border
        {
            Background = Theme.Surface,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 0, 6, 4)
        };

        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(new TextBlock { Text = fileName, FontSize = 11, Foreground = Theme.Text, VerticalAlignment = VerticalAlignment.Center });

        var removeBtn = new Button
        {
            Content = "×",
            FontSize = 11,
            Foreground = Theme.Muted,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(4, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        removeBtn.Click += (_, _) =>
        {
            _currentAttachments.Remove(filePath);
            _attachmentsHost.Children.Remove(badge);
        };
        sp.Children.Add(removeBtn);
        badge.Child = sp;
        _attachmentsHost.Children.Add(badge);
    }

    private void Submit()
    {
        if (_isGenerating) return;

        string text = _inputBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        _lastSubmittedPrompt = text;
        var attachments = _currentAttachments.ToList();

        AddUserMessage(text, attachments);
        _inputBox.Clear();
        _currentAttachments.Clear();
        _attachmentsHost.Children.Clear();

        string model = _modelSelector.SelectedItem as string ?? "qwen3.5:latest";
        MessageSubmitted?.Invoke(text, _currentMode, model, _thinkingEnabled, attachments);
    }

    public void AddUserMessage(string message, IReadOnlyList<string>? attachments = null)
    {
        var card = new Border
        {
            Background = Theme.Surface,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10),
            Margin = new Thickness(20, 4, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = "Vous",
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            Foreground = Theme.Accent,
            Margin = new Thickness(0, 0, 0, 4)
        });

        sp.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 12,
            Foreground = Theme.Text,
            TextWrapping = TextWrapping.Wrap
        });

        if (attachments != null && attachments.Count > 0)
        {
            var attSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            foreach (var a in attachments)
            {
                attSp.Children.Add(new Border
                {
                    Background = Theme.Panel,
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 1, 4, 1),
                    Margin = new Thickness(0, 0, 4, 0),
                    Child = new TextBlock { Text = System.IO.Path.GetFileName(a), FontSize = 10, Foreground = Theme.Muted }
                });
            }
            sp.Children.Add(attSp);
        }

        card.Child = sp;
        _chatMessages.Children.Add(card);
        _chatScroll.ScrollToEnd();
    }

    public void AddAssistantMessage(
    string message,
    string? codeSnippet = null,
    string? proposalAction = null,
    bool isError = false,
    bool isTruncated = false,
    string? warningMessage = null,
    bool allowRetry = false,
    string? proposalId = null)
    {
        var card = new Border
        {
            Background = Theme.PanelHeader,
            BorderBrush = isError ? Theme.Red : Theme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 4, 20, 8),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var sp = new StackPanel();

        // ── Titre + bouton copier la réponse ─────────────────────────
        var titleGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var aiIcon = SvgIconFactory.Create("ai", 13);
        aiIcon.Margin = new Thickness(0, 0, 6, 0);
        titleLeft.Children.Add(aiIcon);
        titleLeft.Children.Add(new TextBlock
        {
            Text = "KEYCE IA",
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            Foreground = isError ? Theme.Red : Theme.Text
        });
        Grid.SetColumn(titleLeft, 0);
        titleGrid.Children.Add(titleLeft);

        var copyResponseBtn = new Button
        {
            Content = SvgIconFactory.Create("copy", 12),
            ToolTip = "Copier la réponse",
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Width = 22,
            Height = 22,
            Cursor = Cursors.Hand
        };
        copyResponseBtn.Click += (_, _) =>
        {
            Clipboard.SetText(message);
            copyResponseBtn.ToolTip = "Copié !";
            copyResponseBtn.Content = SvgIconFactory.Create("check", 12);
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            t.Tick += (_, _) =>
            {
                t.Stop();
                copyResponseBtn.ToolTip = "Copier la réponse";
                copyResponseBtn.Content = SvgIconFactory.Create("copy", 12);
            };
            t.Start();
        };
        Grid.SetColumn(copyResponseBtn, 1);
        titleGrid.Children.Add(copyResponseBtn);
        sp.Children.Add(titleGrid);

        // ── Message d'avertissement éventuel ─────────────────────────
        if (!string.IsNullOrEmpty(warningMessage))
        {
            var warnBorder = new Border
            {
                Background = Theme.Panel,
                BorderBrush = Theme.Accent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 0, 6)
            };
            warnBorder.Child = new TextBlock
            {
                Text = warningMessage,
                FontSize = 11,
                Foreground = Theme.Accent,
                TextWrapping = TextWrapping.Wrap
            };
            sp.Children.Add(warnBorder);
        }

        // ── Texte du message ─────────────────────────────────────────
        sp.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 12,
            Foreground = isError ? Theme.Red : Theme.Text,
            TextWrapping = TextWrapping.Wrap
        });

        // ── Bloc de code avec bouton copier ──────────────────────────
        if (!string.IsNullOrEmpty(codeSnippet))
        {
            var codeHost = new Border
            {
                Background = Theme.Editor,
                BorderBrush = Theme.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 8, 0, 4),
                Padding = new Thickness(8)
            };

            var codeGrid = new Grid();
            codeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            codeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var copyBtn = new Button
            {
                Content = SvgIconFactory.Create("copy", 12),
                ToolTip = "Copier le code",
                Background = Theme.Surface,
                BorderThickness = new Thickness(0),
                Width = 24,
                Height = 22,
                HorizontalAlignment = HorizontalAlignment.Right,
                Cursor = Cursors.Hand
            };
            copyBtn.Click += (_, _) =>
            {
                Clipboard.SetText(codeSnippet);
                copyBtn.ToolTip = "Copié !";
                copyBtn.Content = SvgIconFactory.Create("check", 12);
                var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                t.Tick += (_, _) =>
                {
                    t.Stop();
                    copyBtn.ToolTip = "Copier le code";
                    copyBtn.Content = SvgIconFactory.Create("copy", 12);
                };
                t.Start();
            };
            Grid.SetRow(copyBtn, 0);
            codeGrid.Children.Add(copyBtn);

            var codeText = new TextBlock
            {
                Text = codeSnippet,
                FontFamily = new FontFamily("Consolas, Cascadia Code"),
                FontSize = 11,
                Foreground = Theme.Text,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            };
            Grid.SetRow(codeText, 1);
            codeGrid.Children.Add(codeText);

            codeHost.Child = codeGrid;
            sp.Children.Add(codeHost);
        }

        // ── Action proposée (Accepter / Refuser) ─────────────────────
        if (!string.IsNullOrEmpty(proposalAction))
        {
            var actionBorder = new Border
            {
                Background = Theme.AccentSoft,
                BorderBrush = Theme.Accent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 8, 0, 0)
            };

            var actSp = new StackPanel();
            actSp.Children.Add(new TextBlock
            {
                Text = proposalAction,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.Accent,
                Margin = new Thickness(0, 0, 0, 6)
            });

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal };

            var acceptBtn = new Button
            {
                Content = "Accepter",
                Background = Theme.Green,
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(10, 3, 10, 3),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var rejectBtn = new Button
            {
                Content = "Refuser",
                Background = Theme.Surface,
                Foreground = Theme.Text,
                FontSize = 11,
                Padding = new Thickness(10, 3, 10, 3),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };

            // Anti-double-clic + feedback visuel
            string confirmationId = proposalId ?? proposalAction;
            bool decisionSent = false;

            void SubmitDecision(bool approved)
            {
                if (decisionSent)
                    return;

                decisionSent = true;

                // Remplace les boutons par le choix effectué.
                btnRow.Children.Clear();

                btnRow.Children.Add(new TextBlock
                {
                    Text = approved
                        ? "Autorisation accordée. Exécution en cours..."
                        : "Opération refusée.",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = approved ? Theme.Green : Theme.Text,
                    TextWrapping = TextWrapping.Wrap
                });

                acceptBtn.IsEnabled = false;
                rejectBtn.IsEnabled = false;

                if (approved)
                    ProposalAccepted?.Invoke(confirmationId);
                else
                    ProposalRejected?.Invoke(confirmationId);
            }

            acceptBtn.Click += (_, _) => SubmitDecision(true);
            rejectBtn.Click += (_, _) => SubmitDecision(false);

            btnRow.Children.Add(acceptBtn);
            btnRow.Children.Add(rejectBtn);
            actSp.Children.Add(btnRow);

            actionBorder.Child = actSp;
            sp.Children.Add(actionBorder);
        }

        // ── Bouton Réessayer ─────────────────────────────────────────
        if (allowRetry || isError || isTruncated)
        {
            var retryBtn = new Button
            {
                Background = Theme.Surface,
                BorderBrush = Theme.Border,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 8, 0, 0),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var retrySp = new StackPanel { Orientation = Orientation.Horizontal };
            var refreshIcon = SvgIconFactory.Create("refresh", 11);
            refreshIcon.Margin = new Thickness(0, 0, 4, 0);
            retrySp.Children.Add(refreshIcon);
            retrySp.Children.Add(new TextBlock
            {
                Text = "Réessayer",
                FontSize = 11,
                Foreground = Theme.Text,
                VerticalAlignment = VerticalAlignment.Center
            });
            retryBtn.Content = retrySp;
            retryBtn.Click += (_, _) => RetryRequested?.Invoke();
            sp.Children.Add(retryBtn);
        }

        card.Child = sp;
        _chatMessages.Children.Add(card);
        _chatScroll.ScrollToEnd();
    }

    private void ShowWelcomeMessage()
    {
        var welcome = new Border
        {
            Background = Theme.Surface,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 12)
        };

        var sp = new StackPanel();
        var icon = SvgIconFactory.Create("ai", 24);
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        icon.Margin = new Thickness(0, 0, 0, 8);
        sp.Children.Add(icon);

        sp.Children.Add(new TextBlock
        {
            Text = "Assistant KEYCE IA",
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = Theme.Text,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        });

        sp.Children.Add(new TextBlock
        {
            Text = "Mode Ask pour poser des questions sur votre code.\nMode Agent pour planifier et générer des modifications.",
            FontSize = 11,
            Foreground = Theme.Muted,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });

        welcome.Child = sp;
        _chatMessages.Children.Add(welcome);
    }
}


