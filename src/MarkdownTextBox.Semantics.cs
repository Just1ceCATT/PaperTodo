namespace PaperTodo;

public sealed partial class MarkdownTextBox
{
    private MarkdownSemanticDocument? _semanticDocument;

    internal void SetSemanticDocument(MarkdownSemanticDocument? semanticDocument)
    {
        if (ReferenceEquals(_semanticDocument, semanticDocument))
        {
            return;
        }

        if (_semanticDocument != null)
        {
            DisableSemanticTracking();
            DisableSemanticImagePresentation();
        }

        _semanticDocument = semanticDocument;
        if (_semanticDocument != null)
        {
            EnableSemanticTracking();
            EnableSemanticImagePresentation();
        }

        // 引用惰性续行补齐跟踪随语义文档一起挂/摘；文档就绪且处于 Full 时立即排一次补齐，
        // 覆盖「文档先于 Full 档设置」的载入顺序（两种顺序都能在最后一步触发）。
        AttachQuoteContinuationTracking();
        if (_semanticDocument != null)
        {
            TryQueueQuotePrefixFill();
        }
    }

    private bool TryGetCurrentSemanticSnapshot(out MarkdownSemanticSnapshot snapshot)
    {
        snapshot = null!;
        return _semanticDocument != null &&
            _semanticDocument.TryGetCurrent(out snapshot);
    }
}
