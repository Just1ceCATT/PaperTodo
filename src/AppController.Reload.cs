using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PaperTodo.Plugin;

namespace PaperTodo;

/// <summary>
/// R3 + R4 + v2 §3:data.json 热重载协调器。
/// <see cref="TryReloadState"/> 6 阶段生命周期:
///   1. Quiesce(停 save timer / suppress plugin scans)
///   2. Load &amp; Validate(由 DataHotReloader 预校验,newState 已 Normalize)
///   3. Commit Snapshot(State = newState; Interlocked.Increment _stateRevision &amp; _saveVersion)
///   4. Reconcile Windows(R3 Diff:Remove / Replace / Add,任何差异 → Close + Recreate)
///   5. Reconcile Runtime Services(清除/重建依赖 State 的运行时集合)
///   6. Resume(_isReloading=false, restart save timer, dispose SuppressScans)
/// </summary>
public sealed partial class AppController
{
    /// <summary>
    /// R1.3 / R3 / R4 主入口。由 <see cref="DataHotReloader"/> 在 UI 线程调用。
    /// </summary>
    /// <returns>true 表示成功完成;false 表示被外部条件拒绝(IsExiting / IsRunning / 重入)。</returns>
    internal bool TryReloadState(AppState newState)
    {
        if (IsExiting)
        {
            return false;
        }
        if (_isReloading)
        {
            // 另一个 Reload 正在进行 —— 拒绝重入(防并发破坏 _windows 字典)。
            return false;
        }
        if (!IsRunning)
        {
            return false;
        }
        if (newState == null)
        {
            return false;
        }

        // 第一动作:在 State mutation 之前先 set _isReloading,
        // 阻止外部 mutation 命令在此之后基于错误 State 入队执行。
        _isReloading = true;

        try
        {
            // ====== Phase 1: Quiesce ======
            // 停 save timer,未来调度不再入队。已入线程池的 fire-and-forget
            // Task 由 R1+R4 的 capturedRevision 校验兜底。
            _saveTimer.Stop();
            _forceSaveTimer.Stop();
            // 持有 SuppressScans 返回的 IDisposable,Phase 6 Resume 时 Dispose。
            SuppressMutationGuardDuringReload = _paperBodyPluginEvents?.SuppressScans();

            // ====== Phase 2: Load & Validate ======
            // DataHotReloader 已通过 StateStore.TryReadValidatedStateBytes 预校验
            // 并由 DeserializeAppState 调 NormalizeAfterLoad,无需在此重复。

            // ====== Phase 3: Commit Snapshot ======
            var oldState = State;
            State = newState;
            // R1.3:Reload 自身 increment _stateRevision,与 mutation(MarkDirty)等价。
            // 这保证:Reload 前入队的 Save Task 写盘前校验失败 → 丢弃;
            //        Reload 后入队的 Save Task capturedRevision == current → 正常写盘。
            Interlocked.Increment(ref _stateRevision);
            Interlocked.Increment(ref _saveVersion);
            NotifyPluginEventMutationStampChanged();

            // ====== Phase 4: Reconcile Windows ======
            ReloadWindows(oldState?.Papers ?? new List<PaperData>(), newState.Papers);
            // 重新计算 deep capsule 队列位置(不动画,因为是热重载)。
            ArrangeDeepCapsules(animate: false, flushInitialPresentations: false);

            // ====== Phase 5: Reconcile Runtime Services ======
            RelinquishOldStateReferences(newState);

            // ====== Phase 6: Resume ======
        }
        finally
        {
            SuppressMutationGuardDuringReload?.Dispose();
            SuppressMutationGuardDuringReload = null;

            // 最后还原 _isReloading。BeginInvoke 到 dispatcher 已验证在 UI 线程;
            // 直接同步还原以保证 Reload 完成前没有 mutation 命令进得来。
            _isReloading = false;

            // 恢复 save 调度。
            _saveTimer.Start();
            // 刷新 UI 表面与主题资源(Theme 可能已变)。
            RefreshTrayMenu();
            RefreshTopmostForForegroundWindow(forceGlobalScan: true);
            RefreshApplicationThemeResources();
            // 同步 data.backup.json(沿用现有 TryRefreshBackupFromPrimary 流程)。
            _store.TryRefreshBackupFromPrimary();
        }

        return true;
    }

    /// <summary>
    /// R3 Diff:Remove / Replace / Add。
    /// 算法顺序:先 Remove,再 Replace,最后 Add —— 避免 GetOrCreatePaperWindow
    /// 在 Replace 中误返回旧实例。
    /// 跳过已关闭(由 Closed 事件清理由)的窗口。
    /// </summary>
    private void ReloadWindows(IList<PaperData> oldPapers, IList<PaperData> newPapers)
    {
        // 防御性全量清场:Reload 路径必须保证旧 docked HWND 与 master pill 在
        // Diff/Close 前已彻底释放,避免与新窗口的视觉对象叠加;否则多次 reload
        // 后旧 HWND 累积,造成重叠绘制甚至 UI 卡死/进程退出。
        DestroyAllMasterCapsules();
        // EdgeCapsuleQueueCompositionProxy(V3 Lite 队列合成代理)有独立 HWND,
        // 覆盖在 docked capsule 之上接管 input routing。不显式 dispose 会让旧
        // proxy 继续将 click 路由到已 closed 的 PaperWindow → 崩溃。
        DisposeEdgeCapsuleQueueCompositionProxies();
        foreach (var existing in _windows.Values)
        {
            if (existing.IsLoaded && !existing.IsClosed)
            {
                existing.DetachFromDeepCapsuleStack();
            }
        }

        var oldById = oldPapers.ToDictionary(p => p.Id, StringComparer.Ordinal);
        var newById = newPapers.ToDictionary(p => p.Id, StringComparer.Ordinal);

        // ---- Phase 4a: Remove(id 在 oldPapers 但不在 newPapers)----
        // 遍历 _windows 副本避免在迭代中修改字典。
        foreach (var pair in _windows.ToArray())
        {
            if (newById.ContainsKey(pair.Key))
            {
                continue;
            }
            var window = pair.Value;
            if (!window.IsLoaded || window.IsClosed)
            {
                continue;
            }
            window.Close();
        }

        // ---- Phase 4b: Replace(同 id 但 PaperDataFullyEquivalent == false)----
        foreach (var pair in _windows.ToArray())
        {
            if (!oldById.TryGetValue(pair.Key, out var oldPaper))
            {
                continue; // 新窗口,留给 Phase 4c
            }
            if (!newById.TryGetValue(pair.Key, out var newPaper))
            {
                continue; // 已 Remove
            }
            var window = pair.Value;
            if (!window.IsLoaded || window.IsClosed)
            {
                continue;
            }
            if (PaperDataFullyEquivalent(oldPaper, newPaper))
            {
                continue; // Keep —— 不变,不重建
            }
            window.Close();
            // 不立刻 GetOrCreatePaperWindow,留给 Phase 4c 统一 Add,
            // 这样 Closed 事件处理器先把 _windows.Remove 再 Add 回来,顺序干净。
            // 但需要把 newPaper 临时标记为 "待添加",因 Close 已异步。
            // 实际 Close() 在 WPF 是同步(只是事件异步通知),_windows 会在
            // Closed 事件回调时移除条目;此处直接 GetOrCreate 即可,
            // 因为 GetOrCreate 会处理 _windows 中存在的、Closed 的情况。
            GetOrCreatePaperWindow(newPaper, deferShellConstruction: false);
        }

        // ---- Phase 4c: Add(newPapers 中有但 _windows 中没有)----
        foreach (var newPaper in newPapers)
        {
            if (_windows.ContainsKey(newPaper.Id))
            {
                continue;
            }
            GetOrCreatePaperWindow(newPaper, deferShellConstruction: false);
        }
    }

    /// <summary>
    /// R3 "任何差异 → Replace" 的等价判定。
    /// 逐字段比较 PaperData 全部语义字段;PaperItem 按序逐字段比较。
    /// 不比较 Id 本身 —— Diff 算法保证同 id 时才调用本方法。
    /// </summary>
    private static bool PaperDataFullyEquivalent(PaperData a, PaperData b)
    {
        if (!string.Equals(a.Type, b.Type, StringComparison.Ordinal)) return false;
        if (!string.Equals(a.Title, b.Title, StringComparison.Ordinal)) return false;
        if (!string.Equals(a.BodyProviderId, b.BodyProviderId, StringComparison.Ordinal)) return false;
        if (!string.Equals(a.Content, b.Content, StringComparison.Ordinal)) return false;
        if (a.Items.Count != b.Items.Count) return false;
        for (var i = 0; i < a.Items.Count; i++)
        {
            var ai = a.Items[i];
            var bi = b.Items[i];
            if (!string.Equals(ai.Id, bi.Id, StringComparison.Ordinal)) return false;
            if (!string.Equals(ai.Text, bi.Text, StringComparison.Ordinal)) return false;
            if (ai.Done != bi.Done) return false;
            if (ai.Order != bi.Order) return false;
            if (!string.Equals(ai.LinkedPaperId, bi.LinkedPaperId, StringComparison.Ordinal)) return false;
            if (!string.Equals(ai.LinkedPath, bi.LinkedPath, StringComparison.Ordinal)) return false;
            if (ai.LinkedPathIsDirectory != bi.LinkedPathIsDirectory) return false;
            if (ai.ReminderAt != bi.ReminderAt) return false;
            if (ai.ReminderTriggered != bi.ReminderTriggered) return false;
        }
        if (a.X != b.X) return false;
        if (a.Y != b.Y) return false;
        if (a.Width != b.Width) return false;
        if (a.Height != b.Height) return false;
        if (a.IsVisible != b.IsVisible) return false;
        if (a.IsCollapsed != b.IsCollapsed) return false;
        if (a.AlwaysOnTop != b.AlwaysOnTop) return false;
        if (a.TextZoom != b.TextZoom) return false;
        if (!string.Equals(a.CapsuleSide, b.CapsuleSide, StringComparison.Ordinal)) return false;
        if (!string.Equals(a.CapsuleMonitorDeviceName, b.CapsuleMonitorDeviceName, StringComparison.Ordinal)) return false;
        if (!string.Equals(a.BodyHeaderText, b.BodyHeaderText, StringComparison.Ordinal)) return false;
        if (!string.Equals(a.BodyCapsuleText, b.BodyCapsuleText, StringComparison.Ordinal)) return false;
        if (!string.Equals(a.StartupOwnerPluginId, b.StartupOwnerPluginId, StringComparison.Ordinal)) return false;
        if (!string.Equals(a.StartupInstanceKey, b.StartupInstanceKey, StringComparison.Ordinal)) return false;
        if (a.DeepCapsuleExpandedX != b.DeepCapsuleExpandedX) return false;
        if (a.DeepCapsuleExpandedY != b.DeepCapsuleExpandedY) return false;
        if (a.DeepCapsuleExpandedWidth != b.DeepCapsuleExpandedWidth) return false;
        if (a.DeepCapsuleExpandedHeight != b.DeepCapsuleExpandedHeight) return false;
        if (a.DeepCapsuleExpandedDpiScale != b.DeepCapsuleExpandedDpiScale) return false;
        if (!string.Equals(a.DeepCapsuleExpandedSide, b.DeepCapsuleExpandedSide, StringComparison.Ordinal)) return false;
        if (!string.Equals(a.DeepCapsuleExpandedMonitorDeviceName, b.DeepCapsuleExpandedMonitorDeviceName, StringComparison.Ordinal)) return false;
        return true;
    }

    /// <summary>
    /// v2 §4 Runtime Ownership 协调:清除 / 重建依赖 State 的运行时集合。
    /// 严禁触碰 NoteImageStore.PrepareReusableImageNumbers(startup-only)。
    /// </summary>
    private void RelinquishOldStateReferences(AppState newState)
    {
        // 防御性冗余:即使 Phase 4 ReloadWindows 漏清理也能兜底,防止 master pill 残留。
        DestroyAllMasterCapsules();
        DisposeEdgeCapsuleQueueCompositionProxies();

        var newIds = new HashSet<string>(
            newState.Papers.Select(p => p.Id),
            StringComparer.Ordinal);

        // Startup-only:直接丢弃,无需从新 State 重建(startup 生命周期已结束)。
        _startupDisplayDeferredPapers.Clear();

        // 按 paperId 索引的派生集合:移除不属于 newIds 的条目。
        // Dictionary<TKey, TValue> 没有 RemoveWhere 扩展方法,改为 key-by-key Remove。
        foreach (var key in _visibilityAnimationVersions.Keys
                     .Where(k => !newIds.Contains(k))
                     .ToArray())
        {
            _visibilityAnimationVersions.Remove(key);
        }
        _deepCapsuleContextMenuOwners.RemoveWhere(id => !newIds.Contains(id));
        _pendingPluginPaperStateDeletes.RemoveWhere(id => !newIds.Contains(id));

        // 可见性快捷方式快照由可见性变化失效。
        InvalidateVisibilityShortcutSnapshotForExternalCommand();

        // 派生自 State.GlobalHotkeys 的去重集合,从新 State 重建。
        RebuildShortcutDuplicateIds(newState);

        // Plugin EventHub:R4 stamps 对齐;ResetBaseline 只能重设 stamps 但不停 flush timer,
        // FullReloadReset 组合了 _flushTimer.Stop / 重新设置 stamps / 清零 suppressionDepth。
        _paperBodyPluginEvents?.FullReloadReset(
            new PaperBodyPluginEventHub.ChangeStamp(
                Interlocked.Read(ref _stateRevision),
                Interlocked.Read(ref _saveVersion)));

        // 图像缓存:释放已 Reload 后不再引用的 bitmap。
        // 不调用 PrepareReusableImageNumbers(startup-only)。
        _imageStore.ReleaseUnreferencedBitmapCache(State);

        // 高级快捷方式运行时状态:重置。
        _advancedTransparentPaperIds.Clear();
        _advancedTransparentCapsuleIds.Clear();
        _advancedAllPapersLocked = false;
        RefreshAdvancedShortcutSurfaces(animate: false);

        // Experimental Passive 跟踪的当前窗口引用:重置。
        _experimentalCurrentPassiveWindow = null;
        _lastActivatedPaperWindow = null;
    }

    /// <summary>
    /// 从 newState.GlobalHotkeys 重新检测并填充 _shortcutDuplicateIds。
    /// 重复 gesture(相同键序列)绑定到多个 commandId 时,后者被记为重复,
    /// 与现有 InitializeGlobalHotkeys 中重复检测逻辑一致。
    /// </summary>
    private void RebuildShortcutDuplicateIds(AppState newState)
    {
        _shortcutDuplicateIds.Clear();
        var seenGestures = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in newState.GlobalHotkeys)
        {
            var commandId = pair.Key;
            var gestureText = pair.Value;
            if (string.IsNullOrWhiteSpace(gestureText))
            {
                continue;
            }
            if (!seenGestures.TryAdd(gestureText, commandId))
            {
                // 已有同 gesture 的 command,当前 commandId 标记为重复。
                _shortcutDuplicateIds.Add(commandId);
            }
        }
    }
}