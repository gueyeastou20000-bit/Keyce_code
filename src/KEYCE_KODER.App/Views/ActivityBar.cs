using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KEYCE_KODER.App.Views;

public class ActivityBar : Border
{
    public event Action<string>? ViewSelected;

    private readonly StackPanel _top = new();
    private readonly StackPanel _bottom = new();
    private Button? _active;

    public ActivityBar()
    {
        Width = 44; // Largeur compacte pour maximiser l'espace de l'éditeur
        Background = Theme.ActivityBar;
        BorderBrush = Theme.Border;
        BorderThickness = new Thickness(0, 0, 1, 0);

        var dock = new DockPanel();
        DockPanel.SetDock(_bottom, Dock.Bottom);
        dock.Children.Add(_bottom);
        dock.Children.Add(_top);
        Child = dock;

        AddLogo();

        // Icônes vectorielles SVG (taille 18px, zéro emoji)
        Button explorer = AddSvgButton(_top, "explorer", "explorer", "Explorateur");
        AddSvgButton(_top, "search", "search", "Recherche");
        AddSvgButton(_top, "git", "git", "Contrôle de source (Git)");
        AddSvgButton(_top, "debug", "debug", "Exécuter et déboguer (Ctrl+Maj+D)");

        AddSvgButton(_bottom, "tools", "tools", "Langages et outils");
        AddSvgButton(_bottom, "account", "account", "Compte");
        AddSvgButton(_bottom, "settings", "settings", "Paramètres");

        Select(explorer);
    }

    private void AddLogo()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
            if (!File.Exists(path))
            {
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Assets", "logo.png");
            }

            if (File.Exists(path))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 64;
                bitmap.EndInit();
                bitmap.Freeze();

                _top.Children.Add(new Image
                {
                    Source = bitmap,
                    Width = 30,
                    Height = 30,
                    Margin = new Thickness(0, 8, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    ToolTip = "KEYCE KODER"
                });
            }
        }
        catch
        {
            // Logo introuvable
        }
    }

    private Button AddSvgButton(Panel target, string id, string icon, string tooltip)
    {
        var button = new Button
        {
            Content = SvgIconFactory.Create(icon, 18),
            ToolTip = tooltip,
            Tag = id,
            Height = 40,
            Width = 44,
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

        target.Children.Add(button);
        return button;
    }

    /// <summary>Sélectionne une vue par son identifiant (ex: git, debug, settings).</summary>
    public void SelectView(string id)
    {
        foreach (UIElement child in _top.Children)
        {
            if (child is Button { Tag: string tag } button && tag == id)
            {
                Select(button);
                return;
            }
        }

        foreach (UIElement child in _bottom.Children)
        {
            if (child is Button { Tag: string tag } button && tag == id)
            {
                Select(button);
                return;
            }
        }
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
