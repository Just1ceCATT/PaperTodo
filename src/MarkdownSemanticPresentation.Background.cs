using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace PaperTodo;

internal sealed partial class MarkdownSemanticPresentation
{
    private sealed class SemanticBackgroundRenderer : IBackgroundRenderer
    {
        private readonly MarkdownSemanticPresentation _owner;

        public SemanticBackgroundRenderer(MarkdownSemanticPresentation owner)
        {
            _owner = owner;
        }

        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            var document = textView.Document;
            if (!_owner.RenderBlocks || document == null || !textView.VisualLinesValid)
            {
                return;
            }

            var snapshot = _owner.CurrentSnapshot();
            var zoom = _owner.ZoomFactor();
            var quotePen = new Pen(Theme.QuoteBorderBrush, 3 * zoom);
            var inlineCodeBuilder = new BackgroundGeometryBuilder
            {
                AlignToWholePixels = true,
                CornerRadius = 3 * zoom,
                BorderThickness = 0
            };

            var visible = new List<DocumentLine>();
            foreach (var visualLine in textView.VisualLines)
            {
                for (var line = visualLine.FirstDocumentLine;
                     line != null && line.LineNumber <= visualLine.LastDocumentLine.LineNumber;
                     line = line.NextLine)
                {
                    if (visible.Count > 0 && ReferenceEquals(visible[^1], line))
                    {
                        continue;
                    }

                    visible.Add(line);
                }
            }

            foreach (var line in visible)
            {
                var lineIndex = LineIndex(line);
                foreach (var span in snapshot.SpansForLine(lineIndex))
                {
                    if (span.Kind == MarkdownSemanticSpanKind.InlineCode && span.Length > 0)
                    {
                        inlineCodeBuilder.AddSegment(
                            textView,
                            new TextSegment
                            {
                                StartOffset = span.Start,
                                Length = span.Length
                            });
                    }
                }
            }

            DrawCodeRuns(textView, drawingContext, snapshot, visible, zoom);
            DrawQuoteRails(textView, drawingContext, document, snapshot, visible, quotePen, zoom);

            var inlineCodeGeometry = inlineCodeBuilder.CreateGeometry();
            if (inlineCodeGeometry != null)
            {
                drawingContext.DrawGeometry(Theme.CodeBrush, null, inlineCodeGeometry);
            }
        }

        private bool IsCodeRow(MarkdownSemanticSnapshot snapshot, DocumentLine line)
        {
            var semantic = snapshot.GetLine(LineIndex(line));
            if (!semantic.IsCode)
            {
                return false;
            }

            if (semantic.IsFencedCodeMarker && !_owner.IsFullMode)
            {
                return false;
            }

            return true;
        }

        private void DrawCodeRuns(
            TextView textView,
            DrawingContext drawingContext,
            MarkdownSemanticSnapshot snapshot,
            List<DocumentLine> visible,
            double zoom)
        {
            var count = visible.Count;
            var index = 0;
            while (index < count)
            {
                if (!IsCodeRow(snapshot, visible[index]))
                {
                    index++;
                    continue;
                }

                var first = visible[index];
                var last = first;
                while (index + 1 < count && IsCodeRow(snapshot, visible[index + 1]))
                {
                    index++;
                    last = visible[index];
                }

                var top = RowTop(textView, first);
                var bottom = RowBottom(textView, last);
                var height = Math.Max(1, bottom - top);
                var cornerRadius = 4 * zoom;
                drawingContext.DrawRoundedRectangle(
                    Theme.CodeBrush,
                    null,
                    new Rect(
                        0,
                        top + 1,
                        Math.Max(0, textView.ActualWidth - 4),
                        Math.Max(1, height - 2)),
                    cornerRadius,
                    cornerRadius);
                index++;
            }
        }

        /// <summary>
        /// 引用轨道直接跟随 TextView 中真实或虚拟引用槽的位置。不要把源码前缀改写为空格后
        /// 再测量：那会让轨道、正文和光标使用不同的宽度规则，尤其是 Tab 和混合字体。
        /// </summary>
        private void DrawQuoteRails(
            TextView textView,
            DrawingContext drawingContext,
            IDocument document,
            MarkdownSemanticSnapshot snapshot,
            List<DocumentLine> visible,
            Pen quotePen,
            double zoom)
        {
            var railRows = new List<double[]>(visible.Count);
            foreach (var line in visible)
            {
                railRows.Add(GetQuoteRailXs(
                    textView,
                    document,
                    snapshot,
                    line,
                    zoom));
            }

            for (var row = 0; row < visible.Count; row++)
            {
                var rails = railRows[row];
                if (rails.Length == 0)
                {
                    continue;
                }

                var line = visible[row];
                var previousRails = row > 0 && visible[row - 1].NextLine == line
                    ? railRows[row - 1]
                    : Array.Empty<double>();
                var nextRails = row + 1 < visible.Count && line.NextLine == visible[row + 1]
                    ? railRows[row + 1]
                    : Array.Empty<double>();
                var top = RowTop(textView, line);
                var bottom = RowBottom(textView, line);
                foreach (var x in rails)
                {
                    var joinsPrevious = ContainsNear(previousRails, x);
                    var joinsNext = ContainsNear(nextRails, x);
                    var startY = top + (joinsPrevious ? -0.5 : 1);
                    var endY = bottom + (joinsNext ? 0.5 : -1);
                    if (endY < startY)
                    {
                        endY = startY;
                    }

                    drawingContext.DrawLine(
                        quotePen,
                        new Point(x, startY),
                        new Point(x, endY));
                }
            }
        }

        private double[] GetQuoteRailXs(
            TextView textView,
            IDocument document,
            MarkdownSemanticSnapshot snapshot,
            DocumentLine line,
            double zoom)
        {
            var semantic = snapshot.GetLine(LineIndex(line));
            if (!semantic.IsQuoted || semantic.QuoteLevel <= 0)
            {
                return Array.Empty<double>();
            }

            var text = document.GetText(line);
            var container = MarkdownContainerPrefix.Parse(
                text,
                snapshot,
                line.Offset,
                line.EndOffset);
            var rails = new List<double>(semantic.QuoteLevel);
            foreach (var token in container.Tokens)
            {
                if (!token.IsQuote)
                {
                    continue;
                }

                if (MarkdownSemanticPresentation.TryGetTextPoint(
                        textView,
                        line,
                        line.Offset + token.MarkerStart,
                        VisualYPosition.TextMiddle,
                        out var point))
                {
                    rails.Add(point.X + 2.5 * zoom);
                }
            }

            if (container.MissingQuoteLevels > 0)
            {
                var visualLine = textView.GetVisualLine(line.LineNumber);
                var indent = visualLine?.Elements.OfType<QuoteIndentElement>().FirstOrDefault();
                if (visualLine != null && indent != null)
                {
                    // Source-offset mapping deliberately skips a zero-source indent. Use the
                    // element's actual visual column, including when it consumes a hidden opener.
                    var indentPoint = visualLine.GetVisualPosition(indent.VisualColumn, VisualYPosition.TextMiddle);
                    for (var level = 0; level < indent.Levels; level++)
                    {
                        rails.Add(indentPoint.X - textView.HorizontalOffset + indent.UnitWidth * level + 2.5 * zoom);
                    }
                }
                else if (!_owner.IsFullMode && visualLine?.Elements.FirstOrDefault() is { } element &&
                    MarkdownSemanticPresentation.TryGetTextPoint(
                        textView, line, line.Offset + container.ContentStart,
                        VisualYPosition.TextMiddle, out var point))
                {
                    // Basic/Enhanced have no inserted gutter: start at the source position, never
                    // subtract a width for an element that does not exist in those modes.
                    var unitWidth = _owner.GetQuoteUnitWidth(textView, element.TextRunProperties);
                    for (var level = 0; level < container.MissingQuoteLevels; level++)
                    {
                        rails.Add(point.X + unitWidth * level + 2.5 * zoom);
                    }
                }
            }

            rails.Sort();
            return rails.ToArray();
        }

        private static bool ContainsNear(IReadOnlyList<double> values, double target)
        {
            foreach (var value in values)
            {
                if (Math.Abs(value - target) <= 0.75)
                {
                    return true;
                }
            }

            return false;
        }

        private static double RowTop(TextView textView, DocumentLine line) =>
            textView.GetVisualTopByDocumentLine(line.LineNumber) - textView.VerticalOffset;

        private static int LineIndex(DocumentLine line) =>
            Math.Max(0, line.LineNumber - 1);

        private static double RowBottom(TextView textView, DocumentLine line)
        {
            if (line.NextLine != null)
            {
                return RowTop(textView, line.NextLine);
            }

            var bottom = RowTop(textView, line);
            foreach (var visualLine in textView.VisualLines)
            {
                var first = visualLine.FirstDocumentLine;
                if (first != null && first.LineNumber == line.LineNumber)
                {
                    bottom += visualLine.Height;
                }
            }

            return bottom;
        }
    }
}
