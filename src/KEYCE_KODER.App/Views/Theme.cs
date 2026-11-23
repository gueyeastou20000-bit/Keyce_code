using System.Windows.Media;

namespace KEYCE_KODER.App.Views;

/// <summary>Palette visuelle centralisée de KEYCE KODER.</summary>
public static class Theme
{
    public static readonly SolidColorBrush Window = Brush(7, 13, 26);
    public static readonly SolidColorBrush ActivityBar = Brush(10, 18, 34);
    public static readonly SolidColorBrush Sidebar = Brush(13, 24, 44);
    public static readonly SolidColorBrush Editor = Brush(10, 19, 36);
    public static readonly SolidColorBrush Panel = Brush(9, 17, 32);
    public static readonly SolidColorBrush PanelHeader = Brush(13, 24, 44);
    public static readonly SolidColorBrush Surface = Brush(22, 36, 62);
    public static readonly SolidColorBrush SurfaceHover = Brush(28, 45, 76);
    public static readonly SolidColorBrush Accent = Brush(234, 108, 20);
    public static readonly SolidColorBrush AccentHover = Brush(251, 146, 60);
    public static readonly SolidColorBrush AccentSoft = Brush(58, 40, 30);
    public static readonly SolidColorBrush Text = Brush(232, 235, 243);
    public static readonly SolidColorBrush Muted = Brush(145, 151, 167);
    public static readonly SolidColorBrush Border = Brush(32, 48, 78);
    public static readonly SolidColorBrush GridLine = Brush(26, 40, 66);
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
            _ => Color.FromRgb(234, 108, 20)
        };

        AccentHover.Color = theme switch
        {
            "dark-blue" => Color.FromRgb(96, 170, 255),
            "graphite" => Color.FromRgb(205, 210, 218),
            _ => Color.FromRgb(251, 146, 60)
        };

        AccentSoft.Color = theme switch
        {
            "dark-blue" => Color.FromRgb(30, 53, 82),
            "graphite" => Color.FromRgb(48, 52, 59),
            _ => Color.FromRgb(58, 40, 30)
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
            Window.Color = Color.FromRgb(7, 13, 26);
            ActivityBar.Color = Color.FromRgb(10, 18, 34);
            Sidebar.Color = Color.FromRgb(13, 24, 44);
            Editor.Color = Color.FromRgb(10, 19, 36);
            Panel.Color = Color.FromRgb(9, 17, 32);
            PanelHeader.Color = Color.FromRgb(13, 24, 44);
            Surface.Color = Color.FromRgb(22, 36, 62);
        }
    }
}