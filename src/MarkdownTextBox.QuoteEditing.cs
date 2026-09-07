using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Document;

namespace PaperTodo;

public sealed partial class MarkdownTextBox
{
    private bool _quoteFillQueued;
    private bool _quoteFilling;
    private bool _quoteChangedSubscribed;
    private TextDocument? _quoteChangedDocument;

    /// <summary>
    /// 引用「惰性续行」源码补齐的编辑器侧接线。Full 档正文缩进依赖真实 `&gt;` 占位字形，惰性续行
    /// 没有占位会与引用左竖条重叠（见 MarkdownQuoteNormalization）。策略：只把「语义 QuoteLevel&gt;0 但
    /// 显式 `&gt;` 为 0」的纯惰性行按层级补上前缀，不改写用户已手写的 marker 结构。
    /// </summary>
    private void AttachQuoteContinuationTracking()
    {
        if (_quoteChangedSubscribed)
        {
            _quoteChangedSubscribed = false;
            if (_quoteChangedDocument != null)
            {
                _quoteChangedDocument.Changed -= OnQuoteContinuationDocumentChanged;
            }
            _quoteChangedDocument = null;
        }

        var document = Document;
        if (_semanticDocument != null && document != null)
        {
            document.Changed += OnQuoteContinuationDocumentChanged;
            _quoteChangedSubscribed = true;
            _quoteChangedDocument = document;
        }
    }

    /// <summary>
    /// 行结构类变更（插入/删除含换行）才可能新建惰性续行，排队一次补齐；单行内输入不会造成惰性行，
    /// 不排队，避免每个键都全篇扫描。Fill 自身的前缀插入不含换行，天然不会自我再触发。
    /// </summary>
    private void OnQuoteContinuationDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        if (_quoteFilling ||
            !RenderModeIsFull ||
            IsPreviewMode ||
            IsReadOnly ||
            (e.InsertedText == null || e.InsertedText.TextLength == 0) &&
            (e.RemovedText == null || e.RemovedText.TextLength == 0))
        {
            return;
        }

        if (ContainsLineBreak(e.InsertedText) || ContainsLineBreak(e.RemovedText))
        {
            TryQueueQuotePrefixFill();
        }
    }

    private static bool ContainsLineBreak(ICSharpCode.AvalonEdit.Document.ITextSource? text)
    {
        if (text == null || text.TextLength <= 0)
        {
            return false;
        }

        return text.IndexOf('\n', 0, text.TextLength) >= 0 ||
            text.IndexOf('\r', 0, text.TextLength) >= 0;
    }

    /// <summary>Full 档（可编辑）且文档就绪时排队一次引用补齐（Dispatcher 去重）。</summary>
    internal void TryQueueQuotePrefixFill()
    {
        if (_quoteFilling ||
            _quoteFillQueued ||
            !RenderModeIsFull ||
            IsPreviewMode ||
            IsReadOnly ||
            Document == null)
        {
            return;
        }

        _quoteFillQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)RunQuotePrefixFill);
    }

    private void RunQuotePrefixFill()
    {
        _quoteFillQueued = false;
        if (_quoteFilling ||
            !RenderModeIsFull ||
            IsPreviewMode ||
            IsReadOnly ||
            Document == null ||
            !TryGetCurrentSemanticSnapshot(out var snapshot) ||
            snapshot.LineCount == 0)
        {
            return;
        }

        var edits = MarkdownQuoteNormalization.ComputeFillEdits(Document.Text, snapshot);
        if (edits.Count == 0)
        {
            return;
        }

        // 备份 caret/选区后应用（编辑点按升序排，倒序插入保证偏移稳定）。
        var caret = Math.Clamp(CaretOffset, 0, Document.TextLength);
        var selectionStart = Math.Clamp(SelectionStart, 0, Document.TextLength);
        var selectionLength = Math.Clamp(SelectionLength, 0, Document.TextLength - selectionStart);
        var inserted = new int[edits.Count];
        _quoteFilling = true;
        try
        {
            Document.BeginUpdate();
            try
            {
                for (var index = edits.Count - 1; index >= 0; index--)
                {
                    Document.Insert(edits[index].Offset, edits[index].Text);
                    inserted[index] = edits[index].Text.Length;
                }
            }
            finally
            {
                Document.EndUpdate();
            }
        }
        finally
        {
            _quoteFilling = false;
        }

        // 按各插入点换算 caret/选区在新文档中的位置（插入点位于偏移之前或恰在偏移处都算前置插入）。
        var caretShift = ShiftFor(edits, inserted, caret);
        var startShift = ShiftFor(edits, inserted, selectionStart);
        var endShift = ShiftFor(edits, inserted, selectionStart + selectionLength);
        var newCaret = Math.Clamp(caret + caretShift, 0, Document.TextLength);
        var newSelectionStart = Math.Clamp(selectionStart + startShift, 0, Document.TextLength);
        var newSelectionEnd = Math.Clamp(selectionStart + selectionLength + endShift, newSelectionStart, Document.TextLength);
        try
        {
            CaretOffset = newCaret;
            if (newSelectionEnd > newSelectionStart)
            {
                Select(newSelectionStart, newSelectionEnd - newSelectionStart);
            }
            else
            {
                Select(newSelectionStart, 0);
            }
        }
        catch
        {
            // 极端越界兜底：只修光标，不抛出。
            try
            {
                CaretOffset = newCaret;
            }
            catch
            {
                // 忽略
            }
        }
    }

    private static int ShiftFor(
        System.Collections.Generic.List<MarkdownQuoteFillEdit> edits,
        int[] inserted,
        int offset)
    {
        var shift = 0;
        for (var index = 0; index < edits.Count; index++)
        {
            if (edits[index].Offset <= offset)
            {
                shift += inserted[index];
            }
        }

        return shift;
    }

    /// <summary>
    /// Full 编辑态在引用内容行按 Enter：新行按语义层级续上前缀（与列表续行一致），让后续输入仍在引用内；
    /// 引用行为空（仅 marker）时返回 false，交给默认换行产生空行以结束引用。
    /// </summary>
    private bool TryContinueQuoteOnEnter(DocumentLine line, string text)
    {
        if (!RenderModeIsFull || !TryGetCurrentSemanticSnapshot(out var snapshot))
        {
            return false;
        }

        var level = snapshot.GetLine(Math.Max(0, line.LineNumber - 1)).QuoteLevel;
        if (level <= 0)
        {
            return false;
        }

        var caret = Math.Clamp(CaretOffset, 0, Document!.TextLength);
        var indexInLine = Math.Clamp(caret - line.Offset, 0, text.Length);
        var contentStart = QuoteContentStart(text);
        if (indexInLine < contentStart)
        {
            // 光标还停在 marker 前缀里：不主动续行，走默认换行。
            return false;
        }

        if (IsQuoteLineEmpty(text, contentStart))
        {
            // 空引用行 Enter → 默认换行产生空行，引用到此结束。
            return false;
        }

        var prefix = RepeatQuotePrefix(level);
        var insertion = NewLineTextFor(line) + prefix;
        if (MaxLength > 0 && Text.Length + insertion.Length > MaxLength)
        {
            return false;
        }

        Document.BeginUpdate();
        try
        {
            Document.Insert(caret, insertion);
            CaretOffset = caret + insertion.Length;
            Select(CaretOffset, 0);
        }
        finally
        {
            Document.EndUpdate();
        }

        return true;
    }

    /// <summary>行首显式引用 marker 组之后的正文起点（与渲染侧 ExplicitQuoteMarkers 同规则）。</summary>
    private static int QuoteContentStart(string text)
    {
        var index = 0;
        while (index < text.Length)
        {
            var spaces = 0;
            while (index < text.Length && spaces < 3 && text[index] == ' ')
            {
                index++;
                spaces++;
            }

            if (index >= text.Length || text[index] != '>')
            {
                break;
            }

            index++;
            if (index < text.Length && (text[index] == ' ' || text[index] == '\t'))
            {
                index++;
            }
        }

        return index;
    }

    private static bool IsQuoteLineEmpty(string text, int contentStart)
    {
        for (var index = contentStart; index < text.Length; index++)
        {
            if (!char.IsWhiteSpace(text[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static string RepeatQuotePrefix(int level)
    {
        var buffer = new System.Text.StringBuilder(level * 2);
        for (var index = 0; index < level; index++)
        {
            buffer.Append("> ");
        }

        return buffer.ToString();
    }
}
