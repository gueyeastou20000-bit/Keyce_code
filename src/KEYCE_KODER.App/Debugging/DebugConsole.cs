using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KEYCE_KODER.App.Views;
using KEYCE_KODER.Core.Execution;

namespace KEYCE_KODER.App.Debugging;

/// <summary>
/// Console de débogage (onglet du panneau du bas) :
/// - affiche la sortie du programme débogué (print, erreurs) et les messages du débogueur ;
/// - en pause : la zone de saisie évalue une expression Python (ex. « x * 2 », « len(liste) ») ;
/// - pendant l'exécution : elle envoie le texte au programme (input()).
/// </summary>
public sealed class DebugConsole : Grid
{
    private readonly ConsoleView _view = new();
    private readonly TextBox _input = new();
    private readonly TextBlock _prompt = new();
    private readonly List<string> _history = new();
    private int _historyIndex;

    /// <summary>L'utilisateur a validé une ligne (Entrée).</summary>
    public event Action<string>? Submitted;

    public DebugConsole()
    {
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _prompt.Foreground = Theme.Accent;
        _prompt.FontSize = 12;
        _prompt.Margin = new Thickness(10, 0, 6, 0);
        _prompt.VerticalAlignment = VerticalAlignment.Center;

        _input.Background = Theme.PanelHeader;
        _input.Foreground = Theme.Text;
        _input.CaretBrush = Theme.Text;
        _input.BorderThickness = new Thickness(0);
        _input.FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New");
        _input.FontSize = 13;
        _input.Padding = new Thickness(4, 6, 4, 6);
        _input.VerticalContentAlignment = VerticalAlignment.Center;
        _input.PreviewKeyDown += OnInputKeyDown;

        var inputRow = new Grid { Background = Theme.PanelHeader };
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        SetColumn(_input, 1);
        inputRow.Children.Add(_prompt);
        inputRow.Children.Add(_input);

        SetRow(_view, 0);
        SetRow(inputRow, 1);
        Children.Add(_view);
        Children.Add(inputRow);

        SetMode(paused: false, running: false);
    }

    /// <summary>Adapte l'invite : évaluer (en pause) ou envoyer au programme (en cours).</summary>
    public void SetMode(bool paused, bool running)
    {
        if (paused)
        {
            _prompt.Text = "Évaluer ›";
            _input.ToolTip = "Expression Python à évaluer à l'endroit de l'arrêt (ex. x * 2). Entrée pour évaluer.";
        }
        else if (running)
        {
            _prompt.Text = "Entrée du programme ›";
            _input.ToolTip = "Texte envoyé au programme (input()). Entrée pour envoyer.";
        }
        else
        {
            _prompt.Text = "›";
            _input.ToolTip = "Démarrez le débogage (F5) pour utiliser la console.";
        }
    }

    public void Append(IReadOnlyList<OutputLine> lines) => _view.Append(lines);

    public void WriteInfo(string text) => Write(OutputKind.Info, text);

    public void WriteError(string text) => Write(OutputKind.StdErr, text);

    public void WriteResult(string text) => Write(OutputKind.StdOut, text);

    public void Clear() => _view.Clear();

    public void FocusInput() => _input.Focus();

    private void Write(OutputKind kind, string text) =>
        _view.Append(new[] { new OutputLine(kind, text.EndsWith('\n') ? text : text + "\n") });

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                string text = _input.Text;
                _input.Clear();

                if (text.Length > 0)
                {
                    _history.Add(text);
                    _historyIndex = _history.Count;
                }

                Submitted?.Invoke(text);
                break;

            case Key.Up when _history.Count > 0:
                e.Handled = true;
                _historyIndex = Math.Max(0, _historyIndex - 1);
                SetInput(_history[_historyIndex]);
                break;

            case Key.Down when _history.Count > 0:
                e.Handled = true;
                _historyIndex = Math.Min(_history.Count, _historyIndex + 1);
                SetInput(_historyIndex < _history.Count ? _history[_historyIndex] : "");
                break;
        }
    }

    private void SetInput(string text)
    {
        _input.Text = text;
        _input.CaretIndex = text.Length;
    }
}
