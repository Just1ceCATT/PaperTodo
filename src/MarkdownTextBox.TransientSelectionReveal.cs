using System.Windows.Media.TextFormatting;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Rendering;

namespace PaperTodo;

public sealed partial class MarkdownTextBox
{
    private TransientSelectionRevealElementGenerator? _transientSelectionRevealGenerator;
    private readonly List<MarkdownCollapseRun> _transientSelectionRevealRuns = [];
    private int _transientSelectionScrollGeneration;

    /// <summary>
    /// Makes a programmatic preview selection usable as a transient find result. Only source cells
    /// that Full mode would otherwise collapse are expanded; ordinary visible text is untouched.
    /// </summary>
    internal void SetTransientSelectionRevealActive(bool active)
    {
        EnsureTransientSelectionRevealGenerator();

        var next = new List<MarkdownCollapseRun>();
        if (active &&
            IsPreviewMode &&
            RenderModeIsFull &&
            SelectionLength > 0 &&
            TryGetCurrentSemanticSnapshot(out var snapshot))
        {
            var selectionStart = SelectionStart;
            var selectionEnd = selectionStart + SelectionLength;
            foreach (var run in MarkdownSemanticCollapseLayout.ComputeCollapsedRuns(
                         snapshot,
                         Text ?? string.Empty,
                         MarkdownCaretReveal.None))
            {
                if (run.Start < selectionEnd && run.End > selectionStart)
                {
                    next.Add(run);
                }
            }
        }

        ReplaceTransientSelectionRevealRuns(next);
        if (active && IsPreviewMode && SelectionLength > 0)
        {
            QueueTransientSelectionScroll();
        }
        else
        {
            _transientSelectionScrollGeneration++;
        }
    }

    private void EnsureTransientSelectionRevealGenerator()
    {
        if (_transientSelectionRevealGenerator != null)
        {
            return;
        }

        _transientSelectionRevealGenerator = new TransientSelectionRevealElementGenerator(this);
        // Search reveal must win ties with the normal Full-mode collapse generator at the same
        // document offset. Inserting first leaves every non-selected run on the existing path.
        TextArea.TextView.ElementGenerators.Insert(0, _transientSelectionRevealGenerator);
    }

    private void ReplaceTransientSelectionRevealRuns(List<MarkdownCollapseRun> next)
    {
        if (_transientSelectionRevealRuns.SequenceEqual(next))
        {
            return;
        }

        var previous = _transientSelectionRevealRuns.ToArray();
        _transientSelectionRevealRuns.Clear();
        _transientSelectionRevealRuns.AddRange(next);

        foreach (var run in previous)
        {
            RedrawTransientSelectionRun(run);
        }
        foreach (var run in _transientSelectionRevealRuns)
        {
            RedrawTransientSelectionRun(run);
        }
    }

    private void RedrawTransientSelectionRun(MarkdownCollapseRun run)
    {
        if (Document == null || run.Length <= 0)
        {
            return;
        }

        var start = Math.Clamp(run.Start, 0, Document.TextLength);
        var length = Math.Clamp(run.Length, 0, Document.TextLength - start);
        if (length > 0)
        {
            TextArea.TextView.Redraw(start, length, DispatcherPriority.Render);
        }
    }

    private void QueueTransientSelectionScroll()
    {
        var generation = ++_transientSelectionScrollGeneration;
        var selectionStart = SelectionStart;
        var selectionLength = SelectionLength;
        _ = Dispatcher.BeginInvoke((Action)(() =>
        {
            if (generation != _transientSelectionScrollGeneration ||
                !IsPreviewMode ||
                SelectionStart != selectionStart ||
                SelectionLength != selectionLength ||
                selectionLength <= 0 ||
                Document == null)
            {
                return;
            }

            // ScrollToLine() only knows the physical document line. Passing the exact source column
            // makes AvalonEdit resolve the correct wrapped visual row for long Markdown paragraphs.
            var offset = Math.Clamp(selectionStart, 0, Document.TextLength);
            var location = Document.GetLocation(offset);
            ScrollTo(location.Line, location.Column);
        }), DispatcherPriority.Background);
    }

    private sealed class TransientSelectionRevealElementGenerator : VisualLineElementGenerator
    {
        private readonly MarkdownTextBox _owner;

        public TransientSelectionRevealElementGenerator(MarkdownTextBox owner)
        {
            _owner = owner;
        }

        public override int GetFirstInterestedOffset(int startOffset)
        {
            foreach (var run in _owner._transientSelectionRevealRuns)
            {
                if (run.Start >= startOffset)
                {
                    return run.Start;
                }
            }
            return -1;
        }

        public override VisualLineElement ConstructElement(int offset)
        {
            foreach (var run in _owner._transientSelectionRevealRuns)
            {
                if (run.Start == offset && run.Length > 0)
                {
                    return new TransientSelectionRevealText(CurrentContext.VisualLine, run.Length);
                }
            }
            return null!;
        }
    }

    private sealed class TransientSelectionRevealText : VisualLineText
    {
        public TransientSelectionRevealText(VisualLine parentVisualLine, int length)
            : base(parentVisualLine, length)
        {
        }

        protected override VisualLineText CreateInstance(int length) =>
            new TransientSelectionRevealText(ParentVisualLine, length);

        public override TextRun CreateTextRun(
            int startVisualColumn,
            ITextRunConstructionContext context)
        {
            // Full preview normally makes these source cells transparent. Search is explicitly
            // asking the user to inspect them, so keep the existing typography but make the source
            // glyphs visible for this transient element only.
            TextRunProperties.SetForegroundBrush(Theme.ActiveBrush);
            return base.CreateTextRun(startVisualColumn, context);
        }
    }
}
