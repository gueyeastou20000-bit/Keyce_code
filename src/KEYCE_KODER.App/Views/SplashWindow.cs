using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KEYCE_KODER.App.Themes;

namespace KEYCE_KODER.App.Views;

public class SplashWindow : Window
{
    public SplashWindow()
    {
        Title = "KEYCE KODER";
        Width = 500;
        Height = 320;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Background = ThemeColors.Background;
        ShowInTaskbar = false;

        var logo = new Image
        {
            Source = new BitmapImage(
                new Uri("pack://application:,,,/Assets/logo.png")),
            Width = 130,
            Height = 130,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var title = new TextBlock
        {
            Text = "KEYCE KODER",
            Foreground = ThemeColors.TextPrimary,
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 18, 0, 8)
        };

        var subtitle = new TextBlock
        {
            Text = "Votre environnement de développement",
            Foreground = ThemeColors.TextSecondary,
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var layout = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        layout.Children.Add(logo);
        layout.Children.Add(title);
        layout.Children.Add(subtitle);

        Content = layout;
    }
}