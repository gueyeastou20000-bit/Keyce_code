using System.Windows.Media;

namespace KEYCE_KODER.App.Themes;

public static class ThemeColors
{
    public static readonly SolidColorBrush Background = Create("#1E1E1E");
    public static readonly SolidColorBrush ActivityBar = Create("#181818");
    public static readonly SolidColorBrush Sidebar = Create("#252526");
    public static readonly SolidColorBrush Panel = Create("#252526");
    public static readonly SolidColorBrush TabBackground = Create("#2D2D30");
    public static readonly SolidColorBrush ActiveTab = Create("#1E1E1E");

    public static readonly SolidColorBrush Accent = Create("#007ACC");
    public static readonly SolidColorBrush TextPrimary = Create("#FFFFFF");
    public static readonly SolidColorBrush TextSecondary = Create("#A0A0A0");
    public static readonly SolidColorBrush Border = Create("#3F3F46");
    public static readonly SolidColorBrush Hover = Create("#37373D");

    public static readonly SolidColorBrush Success = Create("#73C991");
    public static readonly SolidColorBrush Error = Create("#F48771");

    private static SolidColorBrush Create(string hex)
    {
        var brush = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(hex)!);

        brush.Freeze();
        return brush;
    }
}