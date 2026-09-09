using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using PaperTodo;

internal static partial class Program
{
    private static void RunEdgePreviewChecks(Action<string, Action> check)
    {
        check("Edge note preview follows all four Markdown modes", () =>
        {
            const string source = "## 标题\n  - [x] **完成**\n> *引用*\n[链接](https://example.com)\n![图片](i:asset)\n\\*原文\\*\n```md\n**代码**\n```";
            var panel = new StackPanel();
            foreach (var mode in new[] { MarkdownRenderModes.Off, MarkdownRenderModes.Basic, MarkdownRenderModes.Enhanced })
            {
                MarkdownEdgeCapsulePreviewRenderer.RenderInto(panel, source, _ => { }, mode);
                Equal(source, EdgePreviewText(panel), $"{mode} retains source syntax and indentation");
                Equal(0, ((TextBlock)panel.Children[7]).Inlines.OfType<Span>().Count(), "fenced code stays literal");
            }

            MarkdownEdgeCapsulePreviewRenderer.RenderInto(panel, "**粗体**", _ => { }, MarkdownRenderModes.Off);
            Require(!((TextBlock)panel.Children[0]).Inlines.OfType<Bold>().Any(), "Off does not style Markdown");
            foreach (var mode in new[] { MarkdownRenderModes.Basic, MarkdownRenderModes.Enhanced })
            {
                MarkdownEdgeCapsulePreviewRenderer.RenderInto(panel, "**粗体**", _ => { }, mode);
                var text = (TextBlock)panel.Children[0];
                Require(text.Inlines.OfType<Bold>().Any(), "enabled modes style emphasis");
                var marker = text.Inlines.OfType<Run>().First();
                Equal(mode == MarkdownRenderModes.Enhanced,
                    marker.ReadLocalValue(TextElement.ForegroundProperty) != DependencyProperty.UnsetValue,
                    "only Enhanced fades syntax");
            }

            MarkdownEdgeCapsulePreviewRenderer.RenderInto(panel, "## 标题\n**粗体**", _ => { }, MarkdownRenderModes.Full);
            Equal("标题\n粗体", EdgePreviewText(panel), "Full removes visible heading and emphasis syntax");
        });

        check("Edge note preview keeps Enhanced list markers readable and renders rules", () =>
        {
            var panel = new StackPanel();
            foreach (var source in new[] { "  - **条目**", "  + **条目**", "  * **条目**", "  12. **条目**", "  - [x] **条目**" })
            {
                foreach (var mode in new[] { MarkdownRenderModes.Off, MarkdownRenderModes.Basic, MarkdownRenderModes.Enhanced })
                {
                    MarkdownEdgeCapsulePreviewRenderer.RenderInto(panel, source, _ => { }, mode);
                    var bullet = mode == MarkdownRenderModes.Enhanced && !source.Contains("[x]") && !source.Contains("12.");
                    Equal(bullet ? "  • **条目**" : source, EdgePreviewText(panel), "marker respects mode and keeps indentation");
                    var row = (TextBlock)panel.Children[0];
                    Require(row.Inlines.FirstInline.Foreground is SolidColorBrush brush && brush.Color.A == 255,
                        "visible bullet, number or task marker is not faded");
                    if (mode == MarkdownRenderModes.Enhanced)
                    {
                        var syntax = row.Inlines.OfType<Run>().First(run => run.Text == "**");
                        Require(syntax.Foreground is SolidColorBrush faded && faded.Color.A < 255,
                            "inline emphasis syntax still fades independently of the list marker");
                    }
                }
            }

            foreach (var mode in new[] { MarkdownRenderModes.Off, MarkdownRenderModes.Basic, MarkdownRenderModes.Enhanced, MarkdownRenderModes.Full })
            {
                MarkdownEdgeCapsulePreviewRenderer.RenderInto(panel, "---", _ => { }, mode);
                var rule = panel.Children[0] as Border ?? (panel.Children[0] as Grid)?.Children.OfType<Border>().Single();
                Equal(mode != MarkdownRenderModes.Off, rule != null, "only enabled modes draw a rule");
                if (rule != null)
                {
                    panel.Measure(new Size(300, double.PositiveInfinity));
                    panel.Arrange(new Rect(0, 0, 300, panel.DesiredSize.Height));
                    Require(rule.ActualWidth > 200, "rule spans the available row width");
                }
                if (mode is MarkdownRenderModes.Basic or MarkdownRenderModes.Enhanced)
                {
                    var sourceText = ((Grid)panel.Children[0]).Children.OfType<TextBlock>().Single();
                    var alpha = ((SolidColorBrush)sourceText.Foreground).Color.A;
                    Equal(mode == MarkdownRenderModes.Enhanced ? (byte)0 : (byte)255, alpha,
                        "Basic keeps source visible; Enhanced replaces its visible markers");
                }
            }
        });

        check("Open edge note preview refreshes after the render setting changes", () =>
        {
            var mode = MarkdownRenderModes.Off;
            var invalidation = new EdgeCapsulePreviewInvalidationSource();
            var context = new EdgeCapsulePreviewContext(
                new PaperData(), () => "笔记", false, () => "**内容**", () => mode,
                (_, _) => false, _ => false, () => new Style(), () => "", _ => { }, invalidation);
            var descriptor = MarkdownEdgeCapsulePreviewProvider.Instance.Describe(context);
            var view = (EdgeCapsuleLivePreviewView)descriptor.CreateContent(descriptor.Size);
            view.PrepareForFirstDisplay();
            var window = new Window { Content = view, Width = 460, Height = 410, ShowInTaskbar = false };
            try
            {
                window.Show();
                Pump();
                Require(EdgePreviewText(view).Contains("**内容**"), "initial preview uses Off");
                mode = MarkdownRenderModes.Full;
                invalidation.Invalidate();
                Pump();
                var displayed = EdgePreviewText(view);
                Require(displayed.Contains("内容") && !displayed.Contains("**"), "open preview reads current mode on refresh");
            }
            finally
            {
                window.Close();
                Pump();
            }
        });

        check("Edge note preview keeps ordinary content beyond twelve source lines", () =>
        {
            var source = string.Join("\n\n", Enumerable.Range(1, 10).Select(i => $"正文 {i}"));
            var panel = new StackPanel();
            MarkdownEdgeCapsulePreviewRenderer.RenderInto(panel, source, _ => { });
            Require(EdgePreviewText(panel).Contains("正文 10"), "later paragraphs remain available");
            Require(!EdgePreviewText(panel).Contains('…'), "ordinary note is not truncated");

            source = new string('文', 700) + "\n段落之后";
            MarkdownEdgeCapsulePreviewRenderer.RenderInto(panel, source, _ => { });
            Require(EdgePreviewText(panel).Contains("段落之后"), "long paragraph does not discard following text");
        });

        check("Edge note preview still bounds pathological documents", () =>
        {
            foreach (var source in new[] { new string('文', 100000), string.Concat(Enumerable.Repeat("x\n", 10000)) })
            {
                var panel = new StackPanel();
                MarkdownEdgeCapsulePreviewRenderer.RenderInto(panel, source, _ => { });
                Require(panel.Children.Count < 200, "visual tree remains bounded");
                var text = EdgePreviewText(panel);
                Require(text.Length < 20000 && text.Contains('…'), "bounded text has a truncation indicator");
            }
        });
    }

    private static string EdgePreviewText(DependencyObject element)
    {
        if (element is TextBlock text)
        {
            return new TextRange(text.ContentStart, text.ContentEnd).Text;
        }

        return string.Join("\n", Enumerable.Range(0, VisualTreeHelper.GetChildrenCount(element))
            .Select(i => EdgePreviewText(VisualTreeHelper.GetChild(element, i))));
    }
}
