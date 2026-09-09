using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using PaperTodo;

internal static class QuoteRailAlignmentChecks
{
    private static readonly double[] FontScales =
    {
        0.5,
        0.75,
        1.0,
        1.1,
        1.25,
        1.45,
        1.5
    };

    [ModuleInitializer]
    internal static void Run()
    {
        foreach (var family in new[] { "Segoe UI", "Consolas" })
        foreach (var mode in new[] { TextFormattingMode.Display, TextFormattingMode.Ideal })
        foreach (var fontScale in FontScales)
        {
            foreach (var source in new[]
            {
                "- > a\n  > b", "- > a\n  b", "> a\nb",
                "> > a\n> b", "> - > a\n>   b", "> **a**\n**b**",
                "10. > a\n    > b", "-\t> a\n\t> b"
            })
            {
                Check(source, fontScale, family, mode);
            }
        }

        Console.WriteLine("PASS quote rails follow actual slots; lazy and explicit gutters share widths");
    }

    private static void Check(
        string source, double fontScale, string family, TextFormattingMode mode)
    {
        using var editor = new RailEditor(source, fontScale, family, mode);
        var message = $"{source.Replace('\n', '|')}: {family}, {mode}, {fontScale}x";
        var box = editor.Box;
        var view = box.TextArea.TextView;
        var snapshot = MarkdownSemanticSnapshot.Parse(source);
        var first = box.Document.GetLineByNumber(1);
        var second = box.Document.GetLineByNumber(2);
        var firstRails = GetRails(editor.Presentation, view, box.Document, snapshot, first);
        var secondRails = GetRails(editor.Presentation, view, box.Document, snapshot, second);

        foreach (var line in new[] { first, second })
        {
            var prefix = MarkdownContainerPrefix.Parse(
                box.Document.GetText(line), snapshot, line.Offset, line.EndOffset);
            var rails = line == first ? firstRails : secondRails;
            if (rails.Length != prefix.QuoteLevel)
            {
                throw new InvalidOperationException($"FAIL quote rail count: {message}");
            }
            var railIndex = 0;
            foreach (var token in prefix.Tokens.Where(token => token.IsQuote))
            {
                if (!MarkdownSemanticPresentation.TryGetTextPoint(
                    view, line, line.Offset + token.MarkerStart, VisualYPosition.TextMiddle, out var point))
                {
                    throw new InvalidOperationException($"FAIL quote marker position: {message}");
                }
                Near(point.X + 2.5 * editor.Presentation.ZoomFactor(), rails[railIndex++], message);
            }
        }

        // Ordered-list text intentionally keeps its native glyph widths. Check its rails against
        // those actual cells above, not against an independently measured string of spaces.
        if (!source.StartsWith("10.", StringComparison.Ordinal))
        {
            for (var index = 0; index < firstRails.Length; index++)
            {
                Near(firstRails[index], secondRails[index], message);
            }
            if (!MarkdownSemanticPresentation.TryGetTextPoint(
                    view, first, source.IndexOf('a'), VisualYPosition.TextMiddle, out var firstBody) ||
                !MarkdownSemanticPresentation.TryGetTextPoint(
                    view, second, source.IndexOf('b'), VisualYPosition.TextMiddle, out var secondBody))
            {
                throw new InvalidOperationException($"FAIL quote body position: {message}");
            }
            Near(firstBody.X, secondBody.X, message + " body alignment");
        }
        if (box.Text != source || box.CanUndo)
        {
            throw new InvalidOperationException($"FAIL quote layout mutated source/history: {message}");
        }
    }

    private static void Near(double expected, double actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.35)
        {
            throw new InvalidOperationException(
                $"FAIL quote rail alignment: {message}: {expected:F3} != {actual:F3}");
        }
    }

    private static double[] GetRails(
        MarkdownSemanticPresentation presentation,
        TextView view,
        IDocument document,
        MarkdownSemanticSnapshot snapshot,
        DocumentLine line)
    {
        var field = typeof(MarkdownSemanticPresentation).GetField(
            "_backgroundRenderer",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("FAIL quote rail alignment: background renderer field missing");
        var renderer = field.GetValue(presentation)
            ?? throw new InvalidOperationException("FAIL quote rail alignment: background renderer missing");
        var method = renderer.GetType().GetMethod(
            "GetQuoteRailXs",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("FAIL quote rail alignment: GetQuoteRailXs missing");
        var result = method.Invoke(
            renderer,
            new object[] { view, document, snapshot, line, presentation.ZoomFactor() });
        return result as double[]
            ?? throw new InvalidOperationException("FAIL quote rail alignment: unexpected rail result");
    }

    private sealed class RailEditor : IDisposable
    {
        private readonly MarkdownSemanticDocument _document;

        public RailEditor(string source, double fontScale, string family, TextFormattingMode mode)
        {
            Box = new MarkdownTextBox { Text = source };
            Box.FontSize = Math.Max(1, Box.FontSize * fontScale);
            Box.FontFamily = new FontFamily(family);
            TextOptions.SetTextFormattingMode(Box, mode);
            Box.SetMarkdownEditAnimationEnabled(false);
            _document = new MarkdownSemanticDocument(Box.Document);
            Box.SetSemanticDocument(_document);
            Presentation = new MarkdownSemanticPresentation(Box, _document);
            Box.SetMarkdownRenderMode(MarkdownRenderModes.Full);
            Box.SetPreviewMode(true);

            Box.ApplyTemplate();
            Box.Measure(new Size(480, 240));
            Box.Arrange(new Rect(0, 0, 480, 240));
            Box.UpdateLayout();
            var view = Box.TextArea.TextView;
            view.Measure(new Size(480, 240));
            view.Arrange(new Rect(0, 0, 480, 240));
            view.EnsureVisualLines();
        }

        public MarkdownTextBox Box { get; }
        public MarkdownSemanticPresentation Presentation { get; }

        public void Dispose()
        {
            Presentation.Dispose();
            Box.SetSemanticDocument(null);
            _document.Dispose();
        }
    }
}
