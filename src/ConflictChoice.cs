namespace PaperTodo;

/// <summary>
/// 用户在 data.json 冲突弹窗中的选择。
/// </summary>
internal enum ConflictChoice
{
    /// <summary>默认:保留当前 PaperTodo 内存值(关闭/取消/Esc 等价于此)。</summary>
    KeepMemory = 0,

    /// <summary>用户明确选择:保留外部文件传入的值。</summary>
    KeepExternal = 1,
}
