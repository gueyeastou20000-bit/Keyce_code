using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;

namespace KEYCE_KODER.App.Views;

public class ActivityBar : Border
{
    public event Action<string>? ViewSelected;

    private readonly StackPanel _panel = new();
    private Button? _active;

    public ActivityBar()
    {
        Width = 52;
        Background = Theme.ActivityBar;
        BorderBrush = Theme.Border;
        BorderThickness = new Thickness(0, 0, 1, 0);
        Child = _panel;

        AddLogo();

        Button explorer = AddButton("explorer", "explorer", "Explorateur");
        AddButton("search", "search", "Recherche");
        AddTextButton("home", "⌂", "Accueil");
        AddTextButton("settings", "⚙", "Paramètres");

        Select(explorer);
    }

    /// <summary>Logo K en haut de la barre (non cliquable, ne déclenche aucune vue).</summary>
    private void AddLogo()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
            if (!File.Exists(path))
                return;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 64;
            bitmap.EndInit();
            bitmap.Freeze();

            _panel.Children.Add(new Image
            {
                Source = bitmap,
                Width = 30,
                Height = 30,
                Margin = new Thickness(0, 12, 0, 12),
                HorizontalAlignment = HorizontalAlignment.Center,
                ToolTip = "KEYCE KODER"
            });
        }
        catch
        {
            // logo introuvable : la barre fonctionne sans
        }
    }

    private Button AddButton(string id, string icon, string tooltip)
    {
        var button = new Button
        {
            Content = SvgIconFactory.Create(icon, 21),
            ToolTip = tooltip,
            Tag = id,
            Height = 52,
            Width = 52,
            Margin = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand
        };

        button.MouseEnter += (_, _) =>
        {
            if (button != _active) button.Background = Theme.Surface;
        };
        button.MouseLeave += (_, _) =>
        {
            if (button != _active) button.Background = Brushes.Transparent;
        };
        button.Click += (_, _) => Select(button);
        _panel.Children.Add(button);
        return button;
    }

    private Button AddTextButton(string id, string text, string tooltip)
    {
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = text,
                Foreground = Theme.Muted,
                FontSize = 20,
                TextAlignment = TextAlignment.Center
            },
            ToolTip = tooltip,
            Tag = id,
            Height = 52,
            Width = 52,
            Margin = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand
        };

        button.MouseEnter += (_, _) =>
        {
            if (button != _active) button.Background = Theme.Surface;
        };
        button.MouseLeave += (_, _) =>
        {
            if (button != _active) button.Background = Brushes.Transparent;
        };
        button.Click += (_, _) => Select(button);
        _panel.Children.Add(button);
        return button;
    }

    private void Select(Button button)
    {
        if (_active != null)
        {
            _active.Background = Brushes.Transparent;
            _active.BorderThickness = new Thickness(0);
        }

        _active = button;
        button.Background = Theme.AccentSoft;
        button.BorderBrush = Theme.Accent;
        button.BorderThickness = new Thickness(3, 0, 0, 0);

        ViewSelected?.Invoke((string)button.Tag);
    }
}