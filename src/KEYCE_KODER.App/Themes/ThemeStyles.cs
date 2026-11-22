using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KEYCE_KODER.App.Themes;

public static class ThemeStyles
{
    public static Style CreateButtonStyle()
    {
        var style = new Style(typeof(Button));

        style.Setters.Add(new Setter(
            Control.BackgroundProperty, ThemeColors.Panel));

        style.Setters.Add(new Setter(
            Control.ForegroundProperty, ThemeColors.TextPrimary));

        style.Setters.Add(new Setter(
            Control.BorderBrushProperty, ThemeColors.Border));

        style.Setters.Add(new Setter(
            Control.BorderThicknessProperty, new Thickness(1)));

        style.Setters.Add(new Setter(
            Control.PaddingProperty, new Thickness(12, 6, 12, 6)));

        style.Setters.Add(new Setter(
            Control.CursorProperty, System.Windows.Input.Cursors.Hand));

        style.Setters.Add(new Setter(
            Control.FontSizeProperty, 13.0));

        var template = new ControlTemplate(typeof(Button));

        var border = new FrameworkElementFactory(typeof(Border));

        border.SetValue(
            Border.BackgroundProperty,
            new TemplateBindingExtension(Control.BackgroundProperty));

        border.SetValue(
            Border.BorderBrushProperty,
            new TemplateBindingExtension(Control.BorderBrushProperty));

        border.SetValue(
            Border.BorderThicknessProperty,
            new TemplateBindingExtension(Control.BorderThicknessProperty));

        border.SetValue(
            Border.CornerRadiusProperty,
            new CornerRadius(4));

        var content = new FrameworkElementFactory(typeof(ContentPresenter));

        content.SetValue(
            FrameworkElement.HorizontalAlignmentProperty,
            HorizontalAlignment.Center);

        content.SetValue(
            FrameworkElement.VerticalAlignmentProperty,
            VerticalAlignment.Center);

        content.SetValue(
            FrameworkElement.MarginProperty,
            new TemplateBindingExtension(Control.PaddingProperty));

        border.AppendChild(content);
        template.VisualTree = border;

        style.Setters.Add(new Setter(
            Control.TemplateProperty, template));

        return style;
    }
}