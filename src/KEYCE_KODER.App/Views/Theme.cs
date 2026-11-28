using System;
using System.Windows;
using System.Windows.Media;

namespace KEYCE_KODER.App.Views
{
    /// <summary>Palette visuelle centralisée de KEYCE KODER (Thème bleu nuit &amp; orange d'Astou par défaut).</summary>
    public static class Theme
    {
        // ── Couleurs de base ────────────────────────────────────────────────────
        public static readonly SolidColorBrush Window      = MkBrush(7, 13, 26);
        public static readonly SolidColorBrush ActivityBar = MkBrush(10, 18, 34);
        public static readonly SolidColorBrush Sidebar     = MkBrush(13, 24, 44);
        public static readonly SolidColorBrush Editor      = MkBrush(10, 19, 36);
        public static readonly SolidColorBrush Panel       = MkBrush(9, 17, 32);
        public static readonly SolidColorBrush PanelHeader = MkBrush(13, 24, 44);
        public static readonly SolidColorBrush Surface     = MkBrush(22, 36, 62);
        public static readonly SolidColorBrush SurfaceHover = MkBrush(28, 45, 76);
        public static readonly SolidColorBrush Accent      = MkBrush(234, 108, 20);
        public static readonly SolidColorBrush AccentHover = MkBrush(251, 146, 60);
        public static readonly SolidColorBrush AccentSoft  = MkBrush(58, 40, 30);
        public static readonly SolidColorBrush Text        = MkBrush(232, 235, 243);
        public static readonly SolidColorBrush Muted       = MkBrush(145, 151, 167);
        public static readonly SolidColorBrush Border      = MkBrush(32, 48, 78);
        public static readonly SolidColorBrush GridLine    = MkBrush(26, 40, 66);
        public static readonly SolidColorBrush Green       = MkBrush(80, 200, 151);
        public static readonly SolidColorBrush Red         = MkBrush(242, 117, 119);

        // ── Couleurs supplémentaires utilisées par AiPanel ─────────────────────
        public static Brush Yellow   => new SolidColorBrush(Color.FromRgb(255, 204, 0));
        public static Brush Blue     => new SolidColorBrush(Color.FromRgb(0, 122, 204));
        public static Brush Orange   => new SolidColorBrush(Color.FromRgb(255, 140, 0));
        public static Brush OnAccent => new SolidColorBrush(Color.FromRgb(255, 255, 255));

        // ── Thème courant ───────────────────────────────────────────────────────
        public static string CurrentTheme { get; private set; } = "midnight-orange";
        public static event Action<string>? ThemeChanged;

        // ── Helpers privés ──────────────────────────────────────────────────────
        private static SolidColorBrush MkBrush(byte r, byte g, byte b)
            => new SolidColorBrush(Color.FromRgb(r, g, b));

        private static void SetColor(SolidColorBrush brush, byte r, byte g, byte b)
        {
            if (!brush.IsFrozen)
                brush.Color = Color.FromRgb(r, g, b);
        }

        // ── Thèmes ─────────────────────────────────────────────────────────────
        public static void Apply(string theme)
        {
            CurrentTheme = theme;

            switch (theme)
            {
                case "light":
                    SetColor(Window,      240, 242, 245);
                    SetColor(ActivityBar, 230, 233, 238);
                    SetColor(Sidebar,     235, 238, 242);
                    SetColor(Editor,      255, 255, 255);
                    SetColor(Panel,       245, 246, 248);
                    SetColor(PanelHeader, 235, 238, 242);
                    SetColor(Surface,     225, 228, 234);
                    SetColor(SurfaceHover,215, 220, 228);
                    SetColor(Accent,      234, 108, 20);
                    SetColor(AccentHover, 251, 146, 60);
                    SetColor(AccentSoft,  254, 237, 222);
                    SetColor(Text,        24,  28,  36);
                    SetColor(Muted,       100, 110, 125);
                    SetColor(Border,      210, 215, 225);
                    SetColor(GridLine,    220, 225, 235);
                    break;

                case "graphite":
                    SetColor(Window,      14, 15, 17);
                    SetColor(ActivityBar, 18, 19, 22);
                    SetColor(Sidebar,     23, 25, 29);
                    SetColor(Editor,      20, 21, 24);
                    SetColor(Panel,       18, 19, 22);
                    SetColor(PanelHeader, 24, 25, 29);
                    SetColor(Surface,     38, 41, 46);
                    SetColor(SurfaceHover,48, 52, 59);
                    SetColor(Accent,      171,178,190);
                    SetColor(AccentHover, 205,210,218);
                    SetColor(AccentSoft,  48, 52, 59);
                    SetColor(Text,        232,235,243);
                    SetColor(Muted,       145,151,167);
                    SetColor(Border,      43, 49, 63);
                    SetColor(GridLine,    36, 42, 54);
                    break;

                case "dark-blue":
                    SetColor(Window,      8,  13, 20);
                    SetColor(ActivityBar, 12, 18, 28);
                    SetColor(Sidebar,     16, 24, 36);
                    SetColor(Editor,      12, 18, 28);
                    SetColor(Panel,       10, 16, 24);
                    SetColor(PanelHeader, 16, 24, 36);
                    SetColor(Surface,     27, 38, 54);
                    SetColor(SurfaceHover,35, 48, 68);
                    SetColor(Accent,      55, 148,255);
                    SetColor(AccentHover, 96, 170,255);
                    SetColor(AccentSoft,  30, 53, 82);
                    SetColor(Text,        232,235,243);
                    SetColor(Muted,       145,151,167);
                    SetColor(Border,      32, 48, 78);
                    SetColor(GridLine,    26, 40, 66);
                    break;

                case "dark-violet":
                    SetColor(Window,      9,  11, 16);
                    SetColor(ActivityBar, 13, 16, 23);
                    SetColor(Sidebar,     18, 22, 31);
                    SetColor(Editor,      15, 18, 26);
                    SetColor(Panel,       13, 16, 23);
                    SetColor(PanelHeader, 18, 22, 31);
                    SetColor(Surface,     27, 32, 43);
                    SetColor(SurfaceHover,33, 39, 52);
                    SetColor(Accent,      139,92, 246);
                    SetColor(AccentHover, 163,130,255);
                    SetColor(AccentSoft,  45, 38, 70);
                    SetColor(Text,        232,235,243);
                    SetColor(Muted,       145,151,167);
                    SetColor(Border,      43, 49, 63);
                    SetColor(GridLine,    36, 42, 54);
                    break;

                default: // "midnight-orange" — signature Astou
                    SetColor(Window,      7,  13, 26);
                    SetColor(ActivityBar, 10, 18, 34);
                    SetColor(Sidebar,     13, 24, 44);
                    SetColor(Editor,      10, 19, 36);
                    SetColor(Panel,       9,  17, 32);
                    SetColor(PanelHeader, 13, 24, 44);
                    SetColor(Surface,     22, 36, 62);
                    SetColor(SurfaceHover,28, 45, 76);
                    SetColor(Accent,      234,108, 20);
                    SetColor(AccentHover, 251,146, 60);
                    SetColor(AccentSoft,  58, 40, 30);
                    SetColor(Text,        232,235,243);
                    SetColor(Muted,       145,151,167);
                    SetColor(Border,      32, 48, 78);
                    SetColor(GridLine,    26, 40, 66);
                    break;
            }

            // Mise à jour des ressources globales WPF (DynamicResource)
            if (Application.Current != null)
            {
                try
                {
                    Application.Current.Resources["KK.Track"]      = Editor;
                    Application.Current.Resources["KK.Thumb"]      = SurfaceHover;
                    Application.Current.Resources["KK.ThumbHover"] = Muted;
                    Application.Current.Resources["KK.Arrow"]      = Muted;
                }
                catch
                {
                    // Ignoré en mode test unitaire sans Application WPF
                }
            }

            ThemeChanged?.Invoke(CurrentTheme);
        }
    }
}
