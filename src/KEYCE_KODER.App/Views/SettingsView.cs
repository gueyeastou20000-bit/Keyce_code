using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KEYCE_KODER.App.Views;

public sealed class SettingsView : StackPanel
{
    public event Action<string>? ThemeSelected;

    public SettingsView()
    {
        Margin = new Thickness(16, 8, 16, 16);

        Children.Add(new TextBlock
        {
            Text = "APPARENCE",
            Foreground = Theme.Text,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10)
        });

        Children.Add(ThemeButton("Sombre violet", "dark-violet", Theme.Accent));
        Children.Add(ThemeButton("Sombre bleu", "dark-blue", System.Windows.Media.Brushes.DodgerBlue));
        Children.Add(ThemeButton("Sombre graphite", "graphite", System.Windows.Media.Brushes.Gray));

        Children.Add(new TextBlock
        {
            Text = "Le thème sélectionné sera mémorisé au prochain lancement.",
            Foreground = Theme.Muted,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 14, 0, 0)
        });
    }

    private Button ThemeButton(string title, string id, System.Windows.Media.Brush accent)
    {
        var button = new Button
        {
            Content = title,
            Height = 36,
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 0, 12, 0),
            Background = Theme.Surface,
            Foreground = Theme.Text,
            BorderBrush = accent,
            BorderThickness = new Thickness(3, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        button.Click += (_, _) => ThemeSelected?.Invoke(id);
        return button;
    }
}
