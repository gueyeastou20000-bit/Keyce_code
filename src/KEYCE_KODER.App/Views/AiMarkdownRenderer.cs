using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KEYCE_KODER.AI;

namespace KEYCE_KODER.App.Views;

/// <summary>
/// Convertit du Markdown structuré en contrôles WPF stylisés pour KEYCE IA (sans XAML).
/// </summary>
public static class AiMarkdownRenderer
{
    private static readonly FontFamily MonoFont = new("Consolas, Cascadia Code, Courier New");

    public static FrameworkElement Render(string markdown, bool isError = false)
    {
        var container = new StackPanel
        {
            Orientation = Orientation.Vertical
        };

        var blocks = AiMarkdownParser.Parse(markdown);
        if (blocks.Count == 0)
        {
            container.Children.Add(new TextBlock
            {
                Text = markdown ?? string.Empty,
                FontSize = 12.5,
                Foreground = isError ? Theme.Red : Theme.Text,
                TextWrapping = TextWrapping.Wrap
            });
            return container;
        }

        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            bool isLast = i == blocks.Count - 1;
            FrameworkElement elem = RenderBlock(block, isError, isLast);
            container.Children.Add(elem);
        }

        return container;
    }

    private static FrameworkElement RenderBlock(AiMarkdownBlock block, bool isError, bool isLast)
    {
        Brush textBrush = isError ? Theme.Red : Theme.Text;
        double bottomMargin = isLast ? 0 : 7;

        switch (block)
        {
            case AiMarkdownHeadingBlock h:
            {
                double fontSize = h.Level switch
                {
                    1 => 15.5,
                    2 => 14,
                    3 => 13,
                    _ => 12.5
                };

                var tb = CreateInlineTextBlock(h.Inlines, textBrush, fontSize);
                tb.FontWeight = FontWeights.Bold;
                tb.Margin = new Thickness(0, 4, 0, isLast ? 0 : 5);
                return tb;
            }

            case AiMarkdownParagraphBlock p:
            {
                var tb = CreateInlineTextBlock(p.Inlines, textBrush, 12.5);
                tb.LineHeight = 19;
                tb.Margin = new Thickness(0, 0, 0, bottomMargin);
                return tb;
            }

            case AiMarkdownListItemBlock li:
            {
                var grid = new Grid
                {
                    Margin = new Thickness(4, 0, 0, isLast ? 0 : 4)
                };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var marker = new TextBlock
                {
                    Text = li.Marker,
                    FontSize = 12.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = isError ? Theme.Red : Theme.Accent,
                    Margin = new Thickness(0, 0, 7, 0),
                    VerticalAlignment = VerticalAlignment.Top
                };
                Grid.SetColumn(marker, 0);
                grid.Children.Add(marker);

                var content = CreateInlineTextBlock(li.Inlines, textBrush, 12.5);
                content.LineHeight = 18.5;
                Grid.SetColumn(content, 1);
                grid.Children.Add(content);

                return grid;
            }

            case AiMarkdownQuoteBlock q:
            {
                var tb = CreateInlineTextBlock(q.Inlines, Theme.Muted, 12);
                tb.FontStyle = FontStyles.Italic;
                tb.LineHeight = 18;

                return new Border
                {
                    Background = Theme.Surface,
                    BorderBrush = Theme.Accent,
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    CornerRadius = new CornerRadius(0, 4, 4, 0),
                    Padding = new Thickness(10, 6, 10, 6),
                    Margin = new Thickness(0, 2, 0, bottomMargin),
                    Child = tb
                };
            }

            case AiMarkdownCodeBlock c:
            {
                var elem = CreateCodeBlock(c.Language, c.Code);
                elem.Margin = new Thickness(0, 2, 0, isLast ? 2 : 8);
                return elem;
            }

            default:
                return new TextBlock
                {
                    Text = string.Empty
                };
        }
    }

    public static Border CreateCodeBlock(string? language, string code)
    {
        var codeHost = new Border
        {
            Background = Theme.Editor,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true
        };

        var codeGrid = new Grid();
        codeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        codeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // En-tête du bloc de code : langage à gauche + bouton copier à droite
        var topBar = new Border
        {
            Background = Theme.Panel,
            BorderBrush = Theme.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(10, 5, 8, 5)
        };

        var topGrid = new Grid();
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        string langDisplay = string.IsNullOrWhiteSpace(language) ? "code" : language.Trim().ToLowerInvariant();
        var langText = new TextBlock
        {
            Text = langDisplay,
            FontFamily = MonoFont,
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Muted,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(langText, 0);
        topGrid.Children.Add(langText);

        var copyContent = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var copyIcon = SvgIconFactory.Create("copy", 11);
        copyIcon.Margin = new Thickness(0, 0, 4, 0);
        var copyLabel = new TextBlock
        {
            Text = "Copier",
            FontSize = 10.5,
            Foreground = Theme.Muted,
            VerticalAlignment = VerticalAlignment.Center
        };
        copyContent.Children.Add(copyIcon);
        copyContent.Children.Add(copyLabel);

        var copyBtn = new Button
        {
            Content = copyContent,
            ToolTip = "Copier le code",
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 2, 6, 2),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center
        };
        copyBtn.MouseEnter += (_, _) => copyBtn.Background = Theme.SurfaceHover;
        copyBtn.MouseLeave += (_, _) => copyBtn.Background = Brushes.Transparent;
        copyBtn.Click += (_, _) =>
        {
            Clipboard.SetText(code);
            copyBtn.ToolTip = "Copié !";
            copyContent.Children.Clear();
            var checkIcon = SvgIconFactory.Create("check", 11);
            checkIcon.Margin = new Thickness(0, 0, 4, 0);
            copyContent.Children.Add(checkIcon);
            copyContent.Children.Add(new TextBlock
            {
                Text = "Copié !",
                FontSize = 10.5,
                Foreground = Theme.Green,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });

            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            t.Tick += (_, _) =>
            {
                t.Stop();
                copyBtn.ToolTip = "Copier le code";
                copyContent.Children.Clear();
                var restoredIcon = SvgIconFactory.Create("copy", 11);
                restoredIcon.Margin = new Thickness(0, 0, 4, 0);
                copyContent.Children.Add(restoredIcon);
                copyContent.Children.Add(copyLabel);
            };
            t.Start();
        };
        Grid.SetColumn(copyBtn, 1);
        topGrid.Children.Add(copyBtn);

        topBar.Child = topGrid;
        Grid.SetRow(topBar, 0);
        codeGrid.Children.Add(topBar);

        // Contenu du code avec défilement horizontal pour les lignes longues
        var codeScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(10, 8, 10, 8)
        };

        var codeText = new TextBlock
        {
            Text = code,
            FontFamily = MonoFont,
            FontSize = 11.5,
            Foreground = Theme.Text,
            TextWrapping = TextWrapping.NoWrap
        };
        codeScroll.Content = codeText;

        Grid.SetRow(codeScroll, 1);
        codeGrid.Children.Add(codeScroll);

        codeHost.Child = codeGrid;
        return codeHost;
    }

    private static TextBlock CreateInlineTextBlock(
        IReadOnlyList<AiMarkdownInline> inlines,
        Brush foreground,
        double fontSize)
    {
        var tb = new TextBlock
        {
            FontSize = fontSize,
            Foreground = foreground,
            TextWrapping = TextWrapping.Wrap
        };

        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case AiMarkdownTextInline t:
                    tb.Inlines.Add(new Run(t.Text));
                    break;

                case AiMarkdownBoldInline b:
                    tb.Inlines.Add(new Bold(new Run(b.Text)));
                    break;

                case AiMarkdownItalicInline it:
                    tb.Inlines.Add(new Italic(new Run(it.Text)));
                    break;

                case AiMarkdownBoldItalicInline bi:
                    tb.Inlines.Add(new Bold(new Italic(new Run(bi.Text))));
                    break;

                case AiMarkdownCodeInline c:
                    tb.Inlines.Add(new Run($" {c.Code} ")
                    {
                        FontFamily = MonoFont,
                        FontSize = Math.Max(10.5, fontSize - 0.5),
                        Background = Theme.Surface,
                        Foreground = Theme.AccentHover
                    });
                    break;

                case AiMarkdownLinkInline l:
                    var link = new Hyperlink(new Run(l.Text))
                    {
                        Foreground = Theme.Accent,
                        TextDecorations = TextDecorations.Underline,
                        ToolTip = l.Url
                    };
                    link.Click += (_, _) => OpenUrlSafely(l.Url);
                    tb.Inlines.Add(link);
                    break;
            }
        }

        return tb;
    }

    private static void OpenUrlSafely(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return;

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch
        {
            // Ignorer si l'ouverture du navigateur échoue.
        }
    }
}
