namespace PaperTodo;

internal sealed partial class MarkdownSemanticPresentation
{
    internal void SetTransientFindReveal(int? absoluteOffset, int length)
    {
        if (_disposed ||
            absoluteOffset is not int requested ||
            length <= 0 ||
            !IsFullMode ||
            _editor.Document == null)
        {
            SetTransientFindReveal((int?)null);
            return;
        }

        var document = _editor.Document;
        var start = Math.Clamp(requested, 0, document.TextLength);
        var end = Math.Clamp(start + length, start, document.TextLength);
        if (end <= start)
        {
            SetTransientFindReveal((int?)null);
            return;
        }

        // Ask the existing collapse table what normal Full preview actually hides. Syncing to None
        // gives the baseline preview layout even when the previous find result was temporarily
        // revealed; after deciding, SetTransientFindReveal re-syncs the same table to the new
        // effective reveal. No second parser or element generator owns this layout.
        var table = EnsureCollapseTable();
        table.SyncTo(MarkdownCaretReveal.None);
        var runs = table.Runs;
        var index = LowerBoundStart(runs, start);
        if (index > 0 && runs[index - 1].End > start)
        {
            index--;
        }

        for (; index < runs.Count; index++)
        {
            var run = runs[index];
            if (run.Start >= end)
            {
                break;
            }
            if (run.End <= start)
            {
                continue;
            }

            // A synthetic caret anywhere inside the hidden syntax unit makes the existing reveal
            // rules expose the whole semantic unit (for example both sides of an inline link).
            SetTransientFindReveal(Math.Max(start, run.Start));
            return;
        }

        // Matches already visible in preview should stay visually stable.
        SetTransientFindReveal((int?)null);
    }
}
