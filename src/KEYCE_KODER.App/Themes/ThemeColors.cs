using System.Windows.Media;

namespace KEYCE_KODER.App.Themes;

public static class ThemeColors
{
    // Couleurs principales
    public static readonly Color Background =
        Color.FromRgb(23, 25, 35);

    public static readonly Color Sidebar =
        Color.FromRgb(32, 36, 49);

    public static readonly Color ActivityBar =
        Color.FromRgb(21, 25, 35);

    public static readonly Color Panel =
        Color.FromRgb(37, 43, 58);

    // Couleur d'accentuation
    public static readonly Color Accent =
        Color.FromRgb(124, 92, 252);

    public static readonly Color AccentHover =
        Color.FromRgb(145, 117, 255);

    // Textes
    public static readonly Color TextPrimary =
        Color.FromRgb(241, 243, 249);

    public static readonly Color TextSecondary =
        Color.FromRgb(146, 154, 175);

    // Bordures
    public static readonly Color Border =
        Color.FromRgb(52, 59, 77);

    // Pinceaux réutilisables
    public static SolidColorBrush Brush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}