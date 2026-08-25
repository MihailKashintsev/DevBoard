using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace DevBoard.Services;

public static class MarkdownRenderer
{
    private const string FontMono = "Cascadia Code, Consolas, Menlo, monospace";

    private static readonly Regex HeadingRegex = new(@"^(#{1,6})\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex HrRegex = new(@"^\s*([-*_])\s*(?:\1\s*){2,}$", RegexOptions.Compiled);
    private static readonly Regex ListItemRegex = new(@"^(\s*)([-*+]|\d+\.)\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex InlineRegex = new(
        @"(`[^`\n]+`)|(\*\*([^*\n]+)\*\*)|(\*([^*\n]+)\*)|(\[([^\]\n]*)\]\(([^)\n]+)\))",
        RegexOptions.Compiled);

    private static readonly IBrush TextBrush = Brush("#B8B8D8");
    private static readonly IBrush HeadingBrush = Brush("#E8E8F0");
    private static readonly IBrush SubheadingBrush = Brush("#C8C8E0");
    private static readonly IBrush AccentBrush = Brush("#8B8BF5");
    private static readonly IBrush MutedBrush = Brush("#5A5A7A");
    private static readonly IBrush CodeForegroundBrush = Brush("#9FE8C8");
    private static readonly IBrush CodeBackgroundBrush = Brush("#12121F");
    private static readonly IBrush InlineCodeBackgroundBrush = Brush("#23233F");
    private static readonly IBrush QuoteBackgroundBrush = Brush("#16162E");
    private static readonly IBrush QuoteBarBrush = Brush("#3D3D6B");

    public static Control Render(string? markdown)
    {
        var root = new StackPanel { Spacing = 14 };

        if (string.IsNullOrWhiteSpace(markdown))
        {
            root.Children.Add(new SelectableTextBlock
            {
                Text = "(пусто)",
                Foreground = MutedBrush,
                FontSize = 14
            });
            return root;
        }

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var i = 0;

        while (i < lines.Length)
        {
            var line = lines[i];

            if (string.IsNullOrWhiteSpace(line)) { i++; continue; }

            // Fenced code block
            if (line.TrimStart().StartsWith("```"))
            {
                var codeLines = new List<string>();
                i++;
                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```"))
                {
                    codeLines.Add(lines[i]);
                    i++;
                }
                i++; // skip closing fence
                root.Children.Add(BuildCodeBlock(string.Join('\n', codeLines)));
                continue;
            }

            // Heading
            var heading = HeadingRegex.Match(line);
            if (heading.Success)
            {
                root.Children.Add(BuildHeading(heading.Groups[1].Length, heading.Groups[2].Value));
                i++;
                continue;
            }

            // Horizontal rule
            if (HrRegex.IsMatch(line))
            {
                root.Children.Add(new Border
                {
                    Height = 1,
                    Background = QuoteBarBrush,
                    Margin = new Thickness(0, 6)
                });
                i++;
                continue;
            }

            // Blockquote
            if (line.TrimStart().StartsWith(">"))
            {
                var quoteLines = new List<string>();
                while (i < lines.Length && lines[i].TrimStart().StartsWith(">"))
                {
                    quoteLines.Add(lines[i].TrimStart()[1..].TrimStart());
                    i++;
                }
                root.Children.Add(BuildQuote(quoteLines));
                continue;
            }

            // List
            var listItem = ListItemRegex.Match(line);
            if (listItem.Success)
            {
                var listPanel = new StackPanel { Spacing = 6 };
                while (i < lines.Length && (listItem = ListItemRegex.Match(lines[i])).Success)
                {
                    var indent = listItem.Groups[1].Value.Length;
                    var level = Math.Min(indent / 2, 2);
                    var marker = char.IsDigit(listItem.Groups[2].Value[0])
                        ? listItem.Groups[2].Value
                        : new[] { "•", "◦", "▪" }[level];

                    listPanel.Children.Add(BuildListItem(marker, listItem.Groups[3].Value, level));
                    i++;
                }
                root.Children.Add(listPanel);
                continue;
            }

            // Paragraph: collect until blank line or block start
            var paragraphLines = new List<string>();
            while (i < lines.Length
                   && !string.IsNullOrWhiteSpace(lines[i])
                   && !HeadingRegex.IsMatch(lines[i])
                   && !lines[i].TrimStart().StartsWith("```")
                   && !HrRegex.IsMatch(lines[i])
                   && !lines[i].TrimStart().StartsWith(">")
                   && !ListItemRegex.IsMatch(lines[i]))
            {
                paragraphLines.Add(lines[i].Trim());
                i++;
            }
            root.Children.Add(BuildParagraph(paragraphLines));
        }

        return root;
    }

    private static Control BuildHeading(int level, string text)
    {
        var size = level switch { 1 => 28.0, 2 => 24.0, 3 => 20.0, 4 => 18.0, 5 => 16.0, _ => 15.0 };
        var tb = new SelectableTextBlock
        {
            FontSize = size,
            FontWeight = FontWeight.SemiBold,
            Foreground = level <= 2 ? HeadingBrush : SubheadingBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, level <= 2 ? 10 : 4, 0, 0)
        };
        AppendInlines(Ins(tb), text, 0);
        return tb;
    }

    private static Control BuildParagraph(List<string> lines)
    {
        var tb = new SelectableTextBlock
        {
            Foreground = TextBrush,
            FontSize = 14,
            LineHeight = 24,
            TextWrapping = TextWrapping.Wrap
        };

        for (var j = 0; j < lines.Count; j++)
        {
            if (j > 0) Ins(tb).Add(new LineBreak());
            AppendInlines(Ins(tb), lines[j], 0);
        }
        return tb;
    }

    private static Control BuildListItem(string marker, string text, int level)
    {
        var content = new SelectableTextBlock
        {
            Foreground = TextBrush,
            FontSize = 14,
            LineHeight = 22,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 0)
        };
        AppendInlines(Ins(content), text, 0);

        return new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(new GridLength(1, GridUnitType.Star))
            },
            Margin = new Thickness(18 * level, 0, 0, 0),
            Children =
            {
                CreateMarker(marker).AssignColumn(0),
                content.AssignColumn(1)
            }
        };
    }

    private static TextBlock CreateMarker(string marker) => new()
    {
        Text = marker,
        Foreground = AccentBrush,
        FontSize = 13,
        FontWeight = FontWeight.Medium,
        VerticalAlignment = VerticalAlignment.Top
    };

    private static Control BuildQuote(List<string> lines)
    {
        var text = new SelectableTextBlock
        {
            Foreground = MutedBrush,
            FontSize = 14,
            FontStyle = FontStyle.Italic,
            LineHeight = 24,
            TextWrapping = TextWrapping.Wrap
        };

        for (var j = 0; j < lines.Count; j++)
        {
            if (j > 0) Ins(text).Add(new LineBreak());
            AppendInlines(Ins(text), lines[j], 0);
        }

        return new Border
        {
            Background = QuoteBackgroundBrush,
            CornerRadius = new CornerRadius(0, 8, 8, 0),
            Padding = new Thickness(16, 12),
            Child = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(new GridLength(1, GridUnitType.Star))
                },
                Children =
                {
                    new Border
                    {
                        Width = 3,
                        CornerRadius = new CornerRadius(2),
                        Background = AccentBrush,
                        Margin = new Thickness(-16, -12, 12, -12)
                    }.AssignColumn(0),
                    text.AssignColumn(1)
                }
            }
        };
    }

    private static Control BuildCodeBlock(string code) => new Border
    {
        Background = CodeBackgroundBrush,
        BorderBrush = QuoteBarBrush,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(16, 12),
        Child = new SelectableTextBlock
        {
            Text = code,
            FontFamily = new FontFamily(FontMono),
            FontSize = 13,
            LineHeight = 20,
            Foreground = TextBrush,
            TextWrapping = TextWrapping.Wrap
        }
    };

    private static void AppendInlines(InlineCollection target, string text, int depth)
    {
        var pos = 0;

        foreach (Match match in InlineRegex.Matches(text))
        {
            if (match.Index > pos)
                target.Add(new Run { Text = text[pos..match.Index], Foreground = TextBrush });

            target.Add(CreateInline(match, depth));
            pos = match.Index + match.Length;
        }

        if (pos < text.Length)
            target.Add(new Run { Text = text[pos..], Foreground = TextBrush });
    }

    private static Inline CreateInline(Match match, int depth)
    {
        if (depth >= 3 || match.Value.Length == 0)
            return new Run { Text = match.Value, Foreground = TextBrush };

        // `code`
        if (match.Groups[1].Success)
        {
            return new Run
            {
                Text = match.Groups[1].Value[1..^1],
                FontFamily = new FontFamily(FontMono),
                FontSize = 13,
                Foreground = CodeForegroundBrush,
                Background = InlineCodeBackgroundBrush
            };
        }

        // **bold**
        if (match.Groups[2].Success)
        {
            var span = new Span { FontWeight = FontWeight.Bold, Foreground = HeadingBrush };
            AppendInlines(Ins(span), match.Groups[3].Value, depth + 1);
            return span;
        }

        // *italic*
        if (match.Groups[4].Success)
        {
            var span = new Span { FontStyle = FontStyle.Italic, Foreground = TextBrush };
            AppendInlines(Ins(span), match.Groups[5].Value, depth + 1);
            return span;
        }

        // [text](url)
        var linkSpan = new Span
        {
            Foreground = AccentBrush,
            TextDecorations = TextDecorations.Underline
        };
        AppendInlines(Ins(linkSpan), match.Groups[7].Success ? match.Groups[7].Value : match.Groups[6].Value, depth + 1);
        return linkSpan;
    }

    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));

    private static InlineCollection Ins(SelectableTextBlock tb) => tb.Inlines ??= new InlineCollection();

    private static InlineCollection Ins(Span span) => span.Inlines ??= new InlineCollection();

    private static T AssignColumn<T>(this T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
