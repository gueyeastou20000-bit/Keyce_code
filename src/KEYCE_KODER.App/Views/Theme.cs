using System.Windows.Media;

namespace KEYCE_KODER.App.Views;

/// <summary>Palette visuelle centralisée de KEYCE KODER.</summary>
public static class Theme
{
    public static readonly SolidColorBrush Window = Brush(9, 11, 16);
    public static readonly SolidColorBrush ActivityBar = Brush(13, 16, 23);
    public static readonly SolidColorBrush Sidebar = Brush(18, 22, 31);
    public static readonly SolidColorBrush Editor = Brush(15, 18, 26);
    public static readonly SolidColorBrush Panel = Brush(13, 16, 23);
    public static readonly SolidColorBrush PanelHeader = Brush(18, 22, 31);
    public static readonly SolidColorBrush Surface = Brush(27, 32, 43);
    public static readonly SolidColorBrush SurfaceHover = Brush(33, 39, 52);
    public static readonly SolidColorBrush Accent = Brush(139, 92, 246);
    public static readonly SolidColorBrush AccentHover = Brush(163, 130, 255);
    public static readonly SolidColorBrush AccentSoft = Brush(45, 38, 70);
    public static readonly SolidColorBrush Text = Brush(232, 235, 243);
    public static readonly SolidColorBrush Muted = Brush(145, 151, 167);
    public static readonly SolidColorBrush Border = Brush(43, 49, 63);
    public static readonly SolidColorBrush GridLine = Brush(36, 42, 54);
    public static readonly SolidColorBrush Green = Brush(80, 200, 151);
    public static readonly SolidColorBrush Red = Brush(242, 117, 119);

    private static SolidColorBrush Brush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        return brush;
    }

    public static void Apply(string theme)
    {
        Accent.Color = theme switch
        {
            "dark-blue" => Color.FromRgb(55, 148, 255),
            "graphite" => Color.FromRgb(171, 178, 190),
            _ => Color.FromRgb(139, 92, 246)
        };

        AccentHover.Color = theme switch
        {
            "dark-blue" => Color.FromRgb(96, 170, 255),
            "graphite" => Color.FromRgb(205, 210, 218),
            _ => Color.FromRgb(163, 130, 255)
        };

        AccentSoft.Color = theme switch
        {
            "dark-blue" => Color.FromRgb(30, 53, 82),
            "graphite" => Color.FromRgb(48, 52, 59),
            _ => Color.FromRgb(45, 38, 70)
        };

        if (theme == "graphite")
        {
            Window.Color = Color.FromRgb(14, 15, 17);
            ActivityBar.Color = Color.FromRgb(18, 19, 22);
            Sidebar.Color = Color.FromRgb(23, 25, 29);
            Editor.Color = Color.FromRgb(20, 21, 24);
            Panel.Color = Color.FromRgb(18, 19, 22);
            PanelHeader.Color = Color.FromRgb(24, 25, 29);
            Surface.Color = Color.FromRgb(38, 41, 46);
        }
        else if (theme == "dark-blue")
        {
            Window.Color = Color.FromRgb(8, 13, 20);
            ActivityBar.Color = Color.FromRgb(12, 18, 28);
            Sidebar.Color = Color.FromRgb(16, 24, 36);
            Editor.Color = Color.FromRgb(12, 18, 28);
            Panel.Color = Color.FromRgb(10, 16, 24);
            PanelHeader.Color = Color.FromRgb(16, 24, 36);
            Surface.Color = Color.FromRgb(27, 38, 54);
        }
        else
        {
            Window.Color = Color.FromRgb(9, 11, 16);
            ActivityBar.Color = Color.FromRgb(13, 16, 23);
            Sidebar.Color = Color.FromRgb(18, 22, 31);
            Editor.Color = Color.FromRgb(15, 18, 26);
            Panel.Color = Color.FromRgb(13, 16, 23);
            PanelHeader.Color = Color.FromRgb(18, 22, 31);
            Surface.Color = Color.FromRgb(27, 32, 43);
        }
    }
}
