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

    private readonly StackPanel _top = new();
    private readonly StackPanel _bottom = new();
    private Button? _active;

    public ActivityBar()
    {
        Width = 52;
        Background = Theme.ActivityBar;
        BorderBrush = Theme.Border;
        BorderThickness = new Thickness(0, 0, 1, 0);

        var dock = new DockPanel();
        DockPanel.SetDock(_bottom, Dock.Bottom);
        dock.Children.Add(_bottom);
        dock.Children.Add(_top);
        Child = dock;

        AddLogo();

        Button explorer = AddSvgButton("explorer", "explorer", "Explorateur");
        AddSvgButton("search", "search", "Recherche");
        AddTextButton(_top, "git", "⎇", "Contrôle de code source");
        AddTextButton(_top, "run", "▷", "Exécuter et déboguer");
        AddTextButton(_top, "extensions", "⊞", "Extensions");
        AddTextButton(_top, "test", "⚗", "Test");
        AddTextButton(_top, "home", "⌂", "Accueil");

        AddTextButton(_bottom, "account", "☺", "Compte");
        AddTextButton(_bottom, "settings", "⚙", "Paramètres");

        Select(explorer);
    }

    /// <summary>Logo K en haut de la barre (non cliquable).</summary>
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
            bitmap.DecodePixelWidth = 80;
            bitmap.EndInit();
            bitmap.Freeze();

            _top.Children.Add(new Image
            {
                Source = bitmap,
                Width = 38,
                Height = 38,
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

    private Button AddSvgButton(string id, string icon, string tooltip)
    {
        Button button = CreateButton(id, SvgIconFactory.Create(icon, 21), tooltip);
        _top.Children.Add(button);
        return button;
    }

    private Button AddTextButton(Panel target, string id, string glyph, string tooltip)
    {
        var content = new TextBlock
        {
            Text = glyph,
            Foreground = Theme.Muted,
            FontFamily = new FontFamily("Segoe UI Symbol"),
            FontSize = 20,
            TextAlignment = TextAlignment.Center
        };
        Button button = CreateButton(id, content, tooltip);
        target.Children.Add(button);
        return button;
    }

    private Button CreateButton(string id, object content, string tooltip)
    {
        var button = new Button
        {
            Content = content,
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