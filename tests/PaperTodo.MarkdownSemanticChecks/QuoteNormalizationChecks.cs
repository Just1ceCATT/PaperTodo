using System.Runtime.CompilerServices;

namespace PaperTodo;

/// <summary>校验引用惰性续行前缀补齐（MarkdownQuoteNormalization）的纯逻辑。</summary>
internal static class QuoteNormalizationChecks
{
    [ModuleInitializer]
    internal static void Run()
    {
        CheckSimpleLazyFill();
        CheckNestedLazyFill();
        CheckIdempotentAfterApply();
        CheckPartialMarkerRowsUntouched();
        Console.WriteLine("PASS quote fill edits pure logic");
    }

    private static void CheckSimpleLazyFill()
    {
        var source = "> a\nb";
        var snapshot = MarkdownSemanticSnapshot.Parse(source);
        var edits = MarkdownQuoteNormalization.ComputeFillEdits(source, snapshot);
        Expect(edits.Count == 1, "one markerless lazy line yields one edit");
        Expect(edits[0].Offset == source.IndexOf('b', StringComparison.Ordinal), "edit lands at lazy line start");
        Expect(edits[0].Text == "> ", "single level fills one '> ' group");

        var applied = Apply(source, edits);
        var reparsed = MarkdownSemanticSnapshot.Parse(applied);
        Expect(reparsed.GetLine(1).QuoteLevel == 1, "applied fill keeps quote level");
        Expect(reparsed.GetLine(1).IsQuoted, "applied fill line stays quoted");
        Expect(MarkdownQuoteNormalization.ComputeFillEdits(applied, reparsed).Count == 0,
            "applied fill is idempotent (markers now match level)");
    }

    private static void CheckNestedLazyFill()
    {
        var source = "> a\n> > b\nlazy";
        var snapshot = MarkdownSemanticSnapshot.Parse(source);
        var edits = MarkdownQuoteNormalization.ComputeFillEdits(source, snapshot);
        Expect(edits.Count == 1, "deep lazy line yields exactly one edit");
        Expect(edits[0].Text == "> > ", "depth 2 lazy fills two '> ' groups");

        var applied = Apply(source, edits);
        var reparsed = MarkdownSemanticSnapshot.Parse(applied);
        Expect(reparsed.GetLine(2).QuoteLevel == 2, "applied deep fill keeps level 2");
        Expect(MarkdownQuoteNormalization.ComputeFillEdits(applied, reparsed).Count == 0,
            "applied deep fill is idempotent");
    }

    private static void CheckIdempotentAfterApply()
    {
        // 已规范的行（marker 数与层级一致）不应再产生编辑。
        var source = "> a\n> b\n> > c";
        var snapshot = MarkdownSemanticSnapshot.Parse(source);
        Expect(MarkdownQuoteNormalization.ComputeFillEdits(source, snapshot).Count == 0,
            "already canonical rows yield no edits");
    }

    private static void CheckPartialMarkerRowsUntouched()
    {
        // 已有 ≥1 显式 marker 的行（即使 Markdig 因紧邻嵌套给更高层级）不自动改写，尊重手写结构。
        var source = "> a\n> > b\n> c";
        var snapshot = MarkdownSemanticSnapshot.Parse(source);
        var edits = MarkdownQuoteNormalization.ComputeFillEdits(source, snapshot);
        Expect(edits.Count == 0, "rows with explicit markers stay untouched");
    }

    private static string Apply(string source, System.Collections.Generic.List<MarkdownQuoteFillEdit> edits)
    {
        var buffer = new System.Text.StringBuilder(source);
        for (var index = edits.Count - 1; index >= 0; index--)
        {
            buffer.Insert(edits[index].Offset, edits[index].Text);
        }

        return buffer.ToString();
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"FAIL quote normalization: {message}");
        }
    }
}
