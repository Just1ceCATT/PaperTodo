using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
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

        check("Edge note preview clips overflow without scrolling and fills available space", () =>
        {
            var source = string.Join("\n", Enumerable.Range(1, 40).Select(i => $"正文 {i}"));
            var mode = MarkdownRenderModes.Off;
            var invalidation = new EdgeCapsulePreviewInvalidationSource();
            var context = new EdgeCapsulePreviewContext(
                new PaperData(), () => "笔记", false, () => source, () => mode,
                (_, _) => false, _ => false, () => new Style(), () => "", _ => { }, invalidation);
            var descriptor = MarkdownEdgeCapsulePreviewProvider.Instance.Describe(context);
            var view = (EdgeCapsuleLivePreviewView)descriptor.CreateContent(descriptor.Size);
            view.PrepareForFirstDisplay();
            var window = new Window { Content = view, Width = 460, Height = 410, ShowInTaskbar = false };
            try
            {
                window.Show();
                Pump();
                var viewport = view.Children.OfType<MarkdownEdgeCapsulePreviewViewport>().Single();
                var body = viewport.Children.OfType<StackPanel>().Single();
                var indicator = viewport.Children.OfType<TextBlock>().Single();
                foreach (var renderMode in new[] { MarkdownRenderModes.Off, MarkdownRenderModes.Basic, MarkdownRenderModes.Enhanced, MarkdownRenderModes.Full })
                {
                    mode = renderMode;
                    invalidation.Invalidate();
                    Pump();
                    Require(!EdgePreviewElements(view).OfType<ScrollViewer>().Any(), "note preview has no scrolling surface in any mode");
                    var clip = body.Clip.Bounds;
                    var last = (FrameworkElement)body.Children[^1];
                    Require(last.TranslatePoint(new Point(), viewport).Y >= clip.Bottom, "overflow remains outside the visible excerpt");
                    Equal(1.0, indicator.Opacity, "overflow shows an ellipsis");
                    Require(indicator.TranslatePoint(new Point(), viewport).Y >= clip.Bottom, "ellipsis does not cover visible text");
                    var first = (FrameworkElement)body.Children[0];
                    var top = first.TranslatePoint(new Point(), viewport);
                    first.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120)
                    {
                        RoutedEvent = Mouse.MouseWheelEvent
                    });
                    Pump();
                    Equal(top, first.TranslatePoint(new Point(), viewport), "mouse wheel cannot move the excerpt");
                    Equal(clip, body.Clip.Bounds, "mouse wheel cannot reveal more content");
                }

                source = string.Join("\n\n", Enumerable.Range(1, 10).Select(i => $"正文 {i}"));
                invalidation.Invalidate();
                Pump();
                var finalParagraph = (FrameworkElement)body.Children[^1];
                Require(EdgePreviewText(finalParagraph).Contains("正文 10"), "blank lines do not exhaust an arbitrary visible block count");
                Require(finalParagraph.TranslatePoint(new Point(0, finalParagraph.ActualHeight), viewport).Y <= body.Clip.Bounds.Bottom,
                    "later paragraph is actually visible when the card has room");
                Equal(0.0, indicator.Opacity, "fitting content has no ellipsis");

                window.Height = 180;
                Pump();
                Equal(1.0, indicator.Opacity, "a smaller viewport recomputes overflow");
                window.Height = 410;
                Pump();
                Equal(0.0, indicator.Opacity, "restoring space removes the overflow indicator");
                source = "";
                invalidation.Invalidate();
                Pump();
                Equal(0.0, indicator.Opacity, "empty content does not retain overflow state");
            }
            finally
            {
                window.Close();
                Pump();
            }
        });

        check("Edge note preview does not discard ordinary source before layout", () =>
        {
            var source = string.Join("\n\n", Enumerable.Range(1, 10).Select(i => $"正文 {i}"));
            var panel = new StackPanel();
            var truncated = MarkdownEdgeCapsulePreviewRenderer.RenderInto(panel, source, _ => { });
            Require(EdgePreviewText(panel).Contains("正文 10"), "later paragraphs remain available for layout");
            Require(!truncated, "ordinary note is not truncated");

            source = new string('文', 700) + "\n段落之后";
            truncated = MarkdownEdgeCapsulePreviewRenderer.RenderInto(panel, source, _ => { });
            Require(EdgePreviewText(panel).Contains("段落之后"), "long paragraph does not discard following text");
            Require(!truncated, "ordinary long paragraph is not truncated before layout");
        });

        check("Edge note preview still bounds pathological documents", () =>
        {
            foreach (var source in new[] { new string('文', 100000), string.Concat(Enumerable.Repeat("x\n", 10000)) })
            {
                var panel = new StackPanel();
                var truncated = MarkdownEdgeCapsulePreviewRenderer.RenderInto(panel, source, _ => { });
                Require(panel.Children.Count < 200, "visual tree remains bounded");
                var text = EdgePreviewText(panel);
                Require(text.Length < 20000 && truncated, "bounded text reports source truncation to the viewport");
            }
        });
    }

    private static IEnumerable<DependencyObject> EdgePreviewElements(DependencyObject element)
    {
        yield return element;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            foreach (var child in EdgePreviewElements(VisualTreeHelper.GetChild(element, i)))
            {
                yield return child;
            }
        }
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
