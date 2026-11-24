using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KEYCE_KODER.App.Services;

namespace KEYCE_KODER.App.Views;

public sealed class SettingsView : StackPanel
{
    public event Action<string>? ThemeSelected;
    public event Action? OpenFullSettingsRequested;

    public SettingsView()
    {
        Margin = new Thickness(16, 8, 16, 16);

        var openFullBtn = new Button
        {
            Content = "Ouvrir tous les paramètres…",
            Height = 36,
            Margin = new Thickness(0, 0, 0, 16),
            Background = Theme.Accent,
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        openFullBtn.Click += (_, _) => OpenFullSettingsRequested?.Invoke();
        Children.Add(openFullBtn);

        Children.Add(new TextBlock
        {
            Text = "THÈME RAPIDE",
            Foreground = Theme.Text,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10)
        });

        Children.Add(ThemeButton("Bleu nuit & orange", "midnight-orange", Theme.Accent));
        Children.Add(ThemeButton("Sombre violet", "dark-violet", System.Windows.Media.Brushes.MediumPurple));
        Children.Add(ThemeButton("Sombre bleu", "dark-blue", System.Windows.Media.Brushes.DodgerBlue));
        Children.Add(ThemeButton("Sombre graphite", "graphite", System.Windows.Media.Brushes.Gray));
        Children.Add(ThemeButton("Thème clair", "light", System.Windows.Media.Brushes.Orange));

        Children.Add(new TextBlock
        {
            Text = "Le choix est immédiatement appliqué et sauvegardé.",
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
            Height = 34,
            Margin = new Thickness(0, 0, 0, 6),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 0, 12, 0),
            Background = Theme.Surface,
            Foreground = Theme.Text,
            BorderBrush = accent,
            BorderThickness = new Thickness(3, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        button.Click += (_, _) =>
        {
            ThemeSelected?.Invoke(id);
            AppSettings.Current.ThemeName = id;
            AppSettings.Current.Save();
        };
        return button;
    }
}
