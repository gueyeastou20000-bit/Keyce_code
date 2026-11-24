using System.Windows;
using System.Windows.Controls;
using KEYCE_KODER.App.Views;

namespace KEYCE_KODER.App.Git;

/// <summary>
/// Petite fenêtre de saisie (nom de branche, adresse du dépôt, identité Git...).
/// Utilisation : string[]? valeurs = PromptWindow.Ask(owner, "Titre", "Message", ("Nom", ""));
/// </summary>
public sealed class PromptWindow : Window
{
    private readonly List<TextBox> _boxes = new();

    private PromptWindow(Window? owner, string title, string message, (string Label, string Value)[] fields)
    {
        if (owner is not null)
            Owner = owner;

        Title = title;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = owner is null
            ? WindowStartupLocation.CenterScreen
            : WindowStartupLocation.CenterOwner;
        Background = Theme.Sidebar;
        Foreground = Theme.Text;

        var panel = new StackPanel { Margin = new Thickness(16) };

        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Theme.Text,
            Margin = new Thickness(0, 0, 0, 12)
        });

        foreach ((string label, string value) in fields)
        {
            panel.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = Theme.Muted,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 3)
            });

            var box = new TextBox
            {
                Text = value,
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 10),
                Background = Theme.Surface,
                Foreground = Theme.Text,
                CaretBrush = Theme.Text,
                BorderBrush = Theme.Border
            };

            _boxes.Add(box);
            panel.Children.Add(box);
        }

        var ok = new Button
        {
            Content = "OK",
            IsDefault = true, // Entrée
            MinWidth = 90,
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 8, 0)
        };
        ok.Click += (_, _) => DialogResult = true;

        var cancel = new Button
        {
            Content = "Annuler",
            IsCancel = true, // Échap
            MinWidth = 90,
            Padding = new Thickness(10, 4, 10, 4)
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 6, 0, 0)
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        Content = panel;

        Loaded += (_, _) =>
        {
            if (_boxes.Count > 0)
            {
                _boxes[0].Focus();
                _boxes[0].SelectAll();
            }
        };
    }

    /// <summary>Affiche la fenêtre ; retourne les valeurs saisies (sans espaces autour), ou null si annulé.</summary>
    public static string[]? Ask(Window? owner, string title, string message, params (string Label, string Value)[] fields)
    {
        var window = new PromptWindow(owner, title, message, fields);

        return window.ShowDialog() == true
            ? window._boxes.Select(box => box.Text.Trim()).ToArray()
            : null;
    }
}
