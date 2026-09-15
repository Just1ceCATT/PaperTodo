using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace PaperTodo;

/// <summary>
/// data.json 冲突弹窗:展示单个冲突的当前值和外部值,让用户二选一。
/// 默认(关闭/X/Esc)=KeepMemory;用户点 "Keep external" 才会覆盖 memory。
/// 展示框可滚动;冲突行用浅红/浅绿背景高亮(只在一侧存在的行)。
/// </summary>
internal static class DataReloadConflictDialog
{
    /// <summary>
    /// 弹一个 modal dialog,返回用户选择。
    /// </summary>
    public static ConflictChoice Show(Window? owner, StateMergeConflict conflict)
    {
        var dialog = new DataReloadConflictDialogWindow(conflict);
        if (owner != null && PresentationSource.FromVisual(owner) is HwndSource)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        var keepExternal = dialog.ShowDialog() == true;
        dialog.Close();
        return keepExternal ? ConflictChoice.KeepExternal : ConflictChoice.KeepMemory;
    }
}

internal sealed class DataReloadConflictDialogWindow : Window
{
    /// <summary>冲突行可视化标记。</summary>
    private enum LineMark { Common, OnlyInMemory, OnlyInExternal }

    public DataReloadConflictDialogWindow(StateMergeConflict conflict)
    {
        Title = Strings.Get("DataReloadConflictTitle");
        Width = 720;
        Height = 560;
        MinWidth = 520;
        MinHeight = 360;
        SizeToContent = SizeToContent.Manual;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        FontFamily = AppTypography.UiFontFamily;
        FontSize = AppTypography.Scale(12);
        Language = AppTypography.Language;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        AppTypography.ApplyTextRendering(this);

        var root = new Border
        {
            Margin = new Thickness(10),
            Padding = new Thickness(18, 16, 18, 16),
            Background = Theme.PaperBrush,
            BorderBrush = Theme.PaperBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Effect = new DropShadowEffect
            {
                BlurRadius = 20,
                ShadowDepth = 3,
                Opacity = Theme.IsDark ? 0.34 : 0.2
            }
        };

        var content = new DockPanel { LastChildFill = true };

        // 顶部:标题 + 关闭按钮
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleText = new TextBlock
        {
            Text = Strings.Get("DataReloadConflictTitle"),
            Foreground = Theme.TextBrush,
            FontSize = AppTypography.Scale(14),
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        };
        Grid.SetColumn(titleText, 0);
        headerGrid.Children.Add(titleText);

        var closeBtn = new Button
        {
            Content = "×",
            Width = 28,
            Height = 24,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = Theme.TextBrush,
            BorderBrush = Brushes.Transparent,
            FontSize = AppTypography.Scale(16),
            IsCancel = true,
            Cursor = Cursors.Hand
        };
        closeBtn.Click += (_, _) => { DialogResult = false; };
        Grid.SetColumn(closeBtn, 1);
        headerGrid.Children.Add(closeBtn);
        DockPanel.SetDock(headerGrid, Dock.Top);
        content.Children.Add(headerGrid);

        // 路径
        var pathText = new TextBlock
        {
            Text = Strings.Format("DataReloadConflictPath", conflict.Path),
            Foreground = Theme.WeakTextBrush,
            FontSize = AppTypography.Scale(11),
            Margin = new Thickness(0, 8, 0, 8),
            TextWrapping = TextWrapping.Wrap
        };
        DockPanel.SetDock(pathText, Dock.Top);
        content.Children.Add(pathText);

        // 底部:两个按钮
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var keepMemoryBtn = new Button
        {
            Content = Strings.Get("DataReloadConflictKeepMemory"),
            MinWidth = 140,
            Padding = new Thickness(14, 6, 14, 6),
            Margin = new Thickness(0, 0, 8, 0),
            IsCancel = true,
            Cursor = Cursors.Hand
        };
        keepMemoryBtn.Click += (_, _) => { DialogResult = false; };
        buttonRow.Children.Add(keepMemoryBtn);

        var keepExternalBtn = new Button
        {
            Content = Strings.Get("DataReloadConflictKeepExternal"),
            MinWidth = 140,
            Padding = new Thickness(14, 6, 14, 6),
            IsDefault = true,
            Cursor = Cursors.Hand
        };
        keepExternalBtn.Click += (_, _) => { DialogResult = true; };
        buttonRow.Children.Add(keepExternalBtn);
        DockPanel.SetDock(buttonRow, Dock.Bottom);
        content.Children.Add(buttonRow);

        // 中间:两个并排的 diff 视图
        var memoryText = FormatConflictValue(conflict.PaperTodoExists, conflict.PaperTodo);
        var externalText = FormatConflictValue(conflict.ExternalExists, conflict.External);

        // 如果双侧是相同文本(common all),退化为单边 + 提示,不浪费屏幕
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var memoryPanel = BuildDiffPanel(
            Strings.Get("DataReloadConflictMemoryHeader"),
            memoryText,
            externalText,
            LineMark.OnlyInMemory);
        Grid.SetColumn(memoryPanel, 0);
        grid.Children.Add(memoryPanel);

        var externalPanel = BuildDiffPanel(
            Strings.Get("DataReloadConflictExternalHeader"),
            externalText,
            memoryText,
            LineMark.OnlyInExternal);
        Grid.SetColumn(externalPanel, 2);
        grid.Children.Add(externalPanel);

        content.Children.Add(grid);

        root.Child = content;
        Content = root;
    }

    /// <summary>
    /// 构造一个可滚动的 diff 面板:头部 + ScrollViewer + StackPanel of colored TextBlocks。
    /// thisSideText 是本面板显示的内容,otherSideText 是对面面板的内容(用于 diff)。
    /// thisSideMark 指定"只在本侧出现的行"标记。
    /// </summary>
    private static UIElement BuildDiffPanel(string header, string thisSideText, string otherSideText, LineMark thisSideMark)
    {
        var stack = new StackPanel();
        var headerText = new TextBlock
        {
            Text = header,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.TextBrush,
            Margin = new Thickness(0, 0, 0, 4)
        };
        stack.Children.Add(headerText);

        // 用 LineDiff 计算逐行差异
        var diff = LineDiff.DiffLines(otherSideText, thisSideText);

        // 高亮策略:左侧 4px 色条 + 微妙底色 + 加粗 + 彩色前缀,而不是整行深色背景,
        // 保证文本在浅底色上仍清晰可读。
        // - 当前独有(OnlyInMemory,选 KeepExternal 会丢失):左侧红色条 + "+ 红" 字 + 极浅粉底
        // - 外部独有(OnlyInExternal,选 KeepMemory 会丢失):左侧绿色条 + "+ 绿" 字 + 极浅绿底
        // - 共有行:透明背景 + 普通字重
        var stripeBrush = thisSideMark switch
        {
            LineMark.OnlyInMemory => new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35)),  // Material Red 600
            LineMark.OnlyInExternal => new SolidColorBrush(Color.FromRgb(0x43, 0xA0, 0x47)), // Material Green 600
            _ => Brushes.Transparent
        };
        var subtleBg = thisSideMark switch
        {
            LineMark.OnlyInMemory => new SolidColorBrush(Color.FromRgb(0xFD, 0xF2, 0xF2)),  // 极浅粉
            LineMark.OnlyInExternal => new SolidColorBrush(Color.FromRgb(0xF1, 0xF8, 0xE9)), // 极浅绿
            _ => Brushes.Transparent
        };
        var markerBrush = thisSideMark switch
        {
            LineMark.OnlyInMemory => new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)),  // Material Red 800
            LineMark.OnlyInExternal => new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)), // Material Green 800
            _ => Theme.TextBrush
        };

        var linesPanel = new StackPanel();
        if (diff.Count == 0)
        {
            linesPanel.Children.Add(new TextBlock
            {
                Text = "(empty)",
                Foreground = Theme.WeakTextBrush,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(4)
            });
        }
        else
        {
            foreach (var entry in diff)
            {
                var isThisSideOnly = entry.Kind != LineDiff.ChangeKind.Common;
                var prefix = isThisSideOnly ? "+ " : "  ";
                var fullText = prefix + entry.Text;

                // 单行容器:左侧 4px 色条 + 文本
                var rowGrid = new Grid();
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var stripe = new Border
                {
                    Width = 4,
                    Background = isThisSideOnly ? stripeBrush : Brushes.Transparent
                };
                Grid.SetColumn(stripe, 0);
                rowGrid.Children.Add(stripe);

                var tb = new TextBlock
                {
                    Text = fullText,
                    FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                    FontSize = AppTypography.Scale(11.5),
                    // 高亮行用深色前景 + 浅背景,提升对比度;普通行用默认前景。
                    Foreground = isThisSideOnly ? markerBrush : Theme.TextBrush,
                    Background = isThisSideOnly ? subtleBg : Brushes.Transparent,
                    Padding = new Thickness(6, 2, 6, 2),
                    TextWrapping = TextWrapping.NoWrap,
                    FontWeight = isThisSideOnly ? FontWeights.SemiBold : FontWeights.Normal,
                    ToolTip = isThisSideOnly
                        ? (thisSideMark == LineMark.OnlyInMemory
                            ? Strings.Get("DataReloadConflictMemoryOnlyTooltip")
                            : Strings.Get("DataReloadConflictExternalOnlyTooltip"))
                        : null
                };
                Grid.SetColumn(tb, 1);
                rowGrid.Children.Add(tb);

                linesPanel.Children.Add(rowGrid);
            }
        }

        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 360,
            MinHeight = 120,
            VerticalContentAlignment = VerticalAlignment.Top,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Content = linesPanel,
            Background = Theme.PaperBrush,
            BorderBrush = Theme.PaperBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(2)
        };
        stack.Children.Add(scrollViewer);
        return stack;
    }

    /// <summary>
    /// 把 JsonNode 格式化为可读文本(不是原始 JSON 语法)。
    /// - JsonValue&lt;string&gt; → 字符串内容(不带引号、去掉转义)
    /// - JsonValue&lt;number/bool&gt; → 直接显示值
    /// - JsonObject → key=value 一行一对
    /// - JsonArray → 一行一项
    /// - null/missing → "(空)" / "(缺失)"
    /// </summary>
    private static string FormatConflictValue(bool exists, JsonNode? node)
    {
        if (!exists) return Strings.Get("DataReloadConflictValueMissing");
        if (node == null) return "(null)";
        var sb = new System.Text.StringBuilder();
        AppendFormatted(sb, node, indent: 0);
        return sb.ToString();
    }

    private static void AppendFormatted(StringBuilder sb, JsonNode? node, int indent)
    {
        if (node is JsonValue v)
        {
            if (v.TryGetValue<string>(out var s)) sb.Append(s);
            else sb.Append(v.ToString());
            return;
        }
        if (node is JsonObject obj)
        {
            foreach (var kv in obj)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(kv.Key).Append('=');
                AppendFormattedValue(sb, kv.Value);
            }
            return;
        }
        if (node is JsonArray arr)
        {
            for (var i = 0; i < arr.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append('[').Append(i).Append("] ");
                AppendFormattedValue(sb, arr[i]);
            }
            return;
        }
        sb.Append(node.ToString());
    }

    private static void AppendFormattedValue(StringBuilder sb, JsonNode? node)
    {
        if (node == null) { sb.Append("(null)"); return; }
        if (node is JsonValue v)
        {
            if (v.TryGetValue<string>(out var s)) sb.Append(s);
            else sb.Append(v.ToString());
            return;
        }
        // 嵌套 object/array:多行
        sb.Append('\n');
        AppendFormatted(sb, node, indent: 0);
    }
}

/// <summary>
/// 行级 LCS diff。返回一系列 Added / Removed / Common。
/// Removed 表示只在 old 里有,Added 表示只在 @new 里有,Common 表示两侧都有。
/// </summary>
internal static class LineDiff
{
    public enum ChangeKind { Common, Removed, Added }

    public readonly record struct Change(ChangeKind Kind, string Text);

    public static IReadOnlyList<Change> DiffLines(string oldText, string newText)
    {
        var oldLines = SplitLines(oldText ?? string.Empty);
        var newLines = SplitLines(newText ?? string.Empty);
        var lcs = LongestCommonSubsequence(oldLines, newLines);
        return BuildChanges(oldLines, newLines, lcs);
    }

    private static string[] SplitLines(string text) =>
        text.Length == 0 ? Array.Empty<string>() : text.Split('\n');

    private static int[,] LongestCommonSubsequence(string[] a, string[] b)
    {
        var dp = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
        {
            for (var j = b.Length - 1; j >= 0; j--)
            {
                dp[i, j] = string.Equals(a[i], b[j], StringComparison.Ordinal)
                    ? dp[i + 1, j + 1] + 1
                    : Math.Max(dp[i + 1, j], dp[i, j + 1]);
            }
        }
        return dp;
    }

    private static List<Change> BuildChanges(string[] a, string[] b, int[,] lcs)
    {
        var result = new List<Change>(capacity: a.Length + b.Length);
        var i = 0; var j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (string.Equals(a[i], b[j], StringComparison.Ordinal))
            {
                result.Add(new Change(ChangeKind.Common, a[i]));
                i++; j++;
            }
            else if (lcs[i + 1, j] >= lcs[i, j + 1])
            {
                result.Add(new Change(ChangeKind.Removed, a[i]));
                i++;
            }
            else
            {
                result.Add(new Change(ChangeKind.Added, b[j]));
                j++;
            }
        }
        while (i < a.Length) { result.Add(new Change(ChangeKind.Removed, a[i++])); }
        while (j < b.Length) { result.Add(new Change(ChangeKind.Added, b[j++])); }
        return result;
    }
}
