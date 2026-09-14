using System;

namespace PaperTodo;

/// <summary>
/// R2 (MutationGuard):外部 mutation 入口(PaperCommandService.EnsureRunning、
/// McpCommandService.Execute、App.HandleSingleInstanceCommand)的守卫状态。
/// 不解决线程同步(由 UI Dispatcher 负责),只拒绝 Reload 生命周期期间
/// 排队却基于错误 State 执行的外部 mutation 命令。
/// </summary>
public sealed partial class AppController
{
    /// <summary>
    /// Reload 进行中标志。Reload 开始时设为 true,结束时(false)还原。
    /// 第一动作:必须先于任何 State mutation,否则其他 mutation 可能在
    /// State 已替换但 _isReloading 仍为 false 的窗口进入。
    /// </summary>
    private bool _isReloading;

    /// <summary>
    /// 外部 mutation 入口守卫检查此访问器。
    /// R2.3:仅检查三个外部 mutation 入口;不接入 MarkDirty/SaveTimer.Tick。
    /// </summary>
    internal bool IsReloading => _isReloading;

    /// <summary>
    /// 持有 Phase 1 调用 SuppressScans 返回的 IDisposable,
    /// 至 Phase 6 Resume 时 Dispose 以恢复 plugin event 广播。
    /// </summary>
    internal IDisposable? SuppressMutationGuardDuringReload;

    /// <summary>
    /// 启动后由 App.xaml.cs 创建并 Start。Dispose 由 App exit 路径调用。
    /// </summary>
    internal DataHotReloader? AttachedHotReloader { get; set; }
}