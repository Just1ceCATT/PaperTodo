using System;
using System.IO;
using System.Text;

namespace PaperTodo;

/// <summary>
/// 数据重载链路的诊断日志。追加写入 <c>paperTodo-data-reload.log</c>(位于
/// <c>%LOCALAPPDATA%\PaperTodo\logs\</c>,避免污染安装目录;若该路径不可用则回退到
/// <see cref="Path.GetTempPath"/>)。仅用于排查"data.json 冲突后无法被替换"类问题;
/// 日志只反映与 reload / save 相关的关键节点,不记录高频事件(autosave timer tick 等)
/// 以免淹没真正的问题信号。写日志失败是 non-fatal —— 仅 best-effort append。
/// </summary>
internal static class DataReloadDiagnostics
{
    private static readonly object _lock = new();
    private static readonly string _logPath = ResolveLogPath();

    private static string ResolveLogPath()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PaperTodo", "logs");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "paperTodo-data-reload.log");
        }
        catch
        {
            // 拿不到 LocalAppData 时不要污染安装目录;落到 temp 是 best-effort。
            return Path.Combine(Path.GetTempPath(), "paperTodo-data-reload.log");
        }
    }

    /// <summary>截断日志文件,写入分隔头。让每次运行都从干净状态开始。</summary>
    public static void Reset()
    {
        lock (_lock)
        {
            try
            {
                File.WriteAllText(_logPath,
                    $"--- PaperTodo data reload diagnostics started {DateTimeOffset.Now:O} ---\n",
                    new UTF8Encoding(false));
            }
            catch { /* log failure is non-fatal */ }
        }
    }

    /// <summary>追加一行日志。</summary>
    public static void Log(string category, string message)
    {
        var line = $"[{DateTimeOffset.Now:HH:mm:ss.fffffff}] [{category}] {message}\n";
        lock (_lock)
        {
            try { File.AppendAllText(_logPath, line, new UTF8Encoding(false)); }
            catch { /* log failure is non-fatal */ }
        }
    }
}
