using System.Collections.Generic;

namespace PaperTodo;

/// <summary>
/// 引用「惰性续行」源码补齐的纯计算（无 WPF 依赖，可被 MarkdownSemanticChecks 直接链接测试）。
/// Full 档正文缩进依赖真实 `&gt;` 占位字形：语义属引用但物理行无 `&gt;` 的行（惰性续行）没有占位，
/// 正文会贴到 x≈0 与引用左竖条重叠。这里按「引用层级」算出缺失前缀的插入点，而不是看行内是否已写
/// `&gt;`——只要该行语义 QuoteLevel 高于其显式 marker 数就补差量。
/// </summary>
internal static class MarkdownQuoteNormalization
{
    /// <summary>
    /// 计算要把 source 中所有「纯惰性续行」（显式 `&gt;` 计数为 0 但语义 QuoteLevel&gt;0）提升到其
    /// 引用层级所需的前缀插入。返回按插入点升序的编辑。
    /// 保守策略：只补 k==0 的行。已有 ≥1 个显式 `&gt;` 的行是用户亲手写的结构（可能是有意的嵌套退出，
    /// 如 Markdig 在「深层紧邻浅行」会把浅行计入内层层级），改写会违背用户意图，故不自动补。
    /// </summary>
    public static List<MarkdownQuoteFillEdit> ComputeFillEdits(
        string source,
        MarkdownSemanticSnapshot snapshot)
    {
        var edits = new List<MarkdownQuoteFillEdit>();
        if (string.IsNullOrEmpty(source) || snapshot.LineCount <= 0)
        {
            return edits;
        }

        var lineStarts = snapshot.LineStarts;
        for (var line = 0; line < snapshot.LineCount; line++)
        {
            var level = snapshot.GetLine(line).QuoteLevel;
            if (level <= 0)
            {
                continue;
            }

            var lineStart = line < lineStarts.Length ? lineStarts[line] : source.Length;
            var lineEnd = line + 1 < lineStarts.Length ? lineStarts[line + 1] : source.Length;
            if (lineEnd <= lineStart)
            {
                continue;
            }

            // 行首最多 3 空格后的显式 marker 计数（与 Blocks.ExplicitQuoteMarkers 同规则）。
            var markerCount = 0;
            var index = lineStart;
            while (index < lineEnd)
            {
                var spaces = 0;
                while (index < lineEnd && spaces < 3 && source[index] == ' ')
                {
                    index++;
                    spaces++;
                }

                if (index >= lineEnd || source[index] != '>')
                {
                    break;
                }

                markerCount++;
                index++;
                if (index < lineEnd && (source[index] == ' ' || source[index] == '\t'))
                {
                    index++;
                }
            }

            if (markerCount != 0)
            {
                continue; // 已有显式 marker：视为用户手写结构，不自动改写。
            }

            // 无显式 marker 却被判引用 = 惰性续行；按其层级补规范前缀（每层 "> "）。
            var insertOffset = lineStart;
            while (insertOffset < lineEnd &&
                   insertOffset - lineStart < 3 &&
                   source[insertOffset] == ' ')
            {
                insertOffset++; // 保留 ≤3 前导空格作为 marker 缩进，marker 从内容起点前插入。
            }

            var prefix = level == 1 ? "> " : RepeatMarkerPrefix(level);
            edits.Add(new MarkdownQuoteFillEdit(insertOffset, prefix));
        }

        return edits;
    }

    private static string RepeatMarkerPrefix(int level)
    {
        var buffer = new System.Text.StringBuilder(level * 2);
        for (var index = 0; index < level; index++)
        {
            buffer.Append("> ");
        }

        return buffer.ToString();
    }
}

/// <summary>把 source 绝对偏移 Offset 处插入 Text（均为把行提到语义引用层级所需的前缀）。</summary>
internal readonly record struct MarkdownQuoteFillEdit(int Offset, string Text);
