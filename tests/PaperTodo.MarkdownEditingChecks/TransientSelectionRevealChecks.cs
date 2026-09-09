using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using PaperTodo;

internal static class TransientSelectionRevealChecks
{
    [ModuleInitializer]
    internal static void Run()
    {
        CheckCollapsedSourceRevealIsScoped();
        CheckWrappedSelectionScrollsToExactColumn();
        Console.WriteLine("PASS transient selection reveal");
    }

    private static void CheckCollapsedSourceRevealIsScoped()
    {
        const string source = "[label](https://needle.example) tail";
        using var editor = new PreviewEditor(source, width: 800, height: 180);
        editor.Layout();

        var tailOffset = source.IndexOf("tail", StringComparison.Ordinal);
        var initialTailX = XAtOffset(editor.Box, tailOffset);

        var labelOffset = source.IndexOf("label", StringComparison.Ordinal);
        editor.Box.Select(labelOffset, "label".Length);
        editor.Box.SetTransientSelectionRevealActive(true);
        editor.Layout();
        Near(
            initialTailX,
            XAtOffset(editor.Box, tailOffset),
            "selecting visible link text does not expand hidden syntax");

        var needleOffset = source.IndexOf("needle", StringComparison.Ordinal);
        editor.Box.Select(needleOffset, "needle".Length);
        editor.Box.SetTransientSelectionRevealActive(true);
        editor.Layout();
        var revealedTailX = XAtOffset(editor.Box, tailOffset);
        if (revealedTailX <= initialTailX + 20)
        {
            throw new InvalidOperationException(
                $"FAIL transient selection reveal: hidden link destination did not expand: {initialTailX:F2} -> {revealedTailX:F2}");
        }

        editor.Box.SetTransientSelectionRevealActive(false);
        editor.Layout();
        Near(
            initialTailX,
            XAtOffset(editor.Box, tailOffset),
            "closing transient reveal restores normal Full preview layout");
    }

    private static void CheckWrappedSelectionScrollsToExactColumn()
    {
        var source = string.Join(' ', Enumerable.Repeat("wrapped", 80)) + " needle";
        using var editor = new PreviewEditor(source, width: 180, height: 90);
        editor.Layout();

        var needleOffset = source.LastIndexOf("needle", StringComparison.Ordinal);
        editor.Box.Select(needleOffset, "needle".Length);
        editor.Box.SetTransientSelectionRevealActive(true);
        Pump();
        editor.Box.UpdateLayout();
        editor.Box.TextArea.TextView.EnsureVisualLines();

        if (editor.Box.TextArea.TextView.VerticalOffset <= 0.5)
        {
            throw new InvalidOperationException(
                "FAIL transient selection reveal: wrapped match stayed at the physical line start");
        }
    }

    private static double XAtOffset(MarkdownTextBox box, int offset)
    {
        var view = box.TextArea.TextView;
        var line = box.Document.GetLineByOffset(offset);
        var indexInLine = Math.Clamp(offset - line.Offset, 0, line.Length);
        var point = view.GetVisualPosition(
            new TextViewPosition(line.LineNumber, indexInLine + 1),
            VisualYPosition.TextMiddle);
        return point.X - view.HorizontalOffset;
    }

    private static void Near(double expected, double actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.35)
        {
            throw new InvalidOperationException(
                $"FAIL transient selection reveal: {message}: {expected:F2} != {actual:F2}");
        }
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private sealed class PreviewEditor : IDisposable
    {
        private readonly MarkdownSemanticDocument _document;
        private readonly MarkdownSemanticPresentation _presentation;
        private readonly double _width;
        private readonly double _height;

        public PreviewEditor(string source, double width, double height)
        {
            _width = width;
            _height = height;
            Box = new MarkdownTextBox { Text = source };
            Box.SetMarkdownEditAnimationEnabled(false);
            _document = new MarkdownSemanticDocument(Box.Document);
            Box.SetSemanticDocument(_document);
            _presentation = new MarkdownSemanticPresentation(Box, _document);
            Box.SetMarkdownRenderMode(MarkdownRenderModes.Full);
            Box.SetPreviewMode(true);
        }

        public MarkdownTextBox Box { get; }

        public void Layout()
        {
            Box.ApplyTemplate();
            Box.Measure(new Size(_width, _height));
            Box.Arrange(new Rect(0, 0, _width, _height));
            Box.UpdateLayout();
            var view = Box.TextArea.TextView;
            view.Measure(new Size(_width, _height));
            view.Arrange(new Rect(0, 0, _width, _height));
            view.EnsureVisualLines();
            Pump();
        }

        public void Dispose()
        {
            Box.SetTransientSelectionRevealActive(false);
            _presentation.Dispose();
            Box.SetSemanticDocument(null);
            _document.Dispose();
        }
    }
}
