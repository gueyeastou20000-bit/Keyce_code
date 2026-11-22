using System;
using System.Windows;
using SharpVectors.Converters;

namespace KEYCE_KODER.App.Views;

public static class SvgIconFactory
{
    public static SvgViewbox Create(string iconName, double size = 20)
    {
        return new SvgViewbox
        {
            Source = new Uri(
                $"pack://application:,,,/Assets/Icons/{iconName}.svg",
                UriKind.Absolute),

            Width = size,
            Height = size,
            Stretch = System.Windows.Media.Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }
}
