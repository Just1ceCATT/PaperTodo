using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using PaperTodo.Plugin;
using Application = System.Windows.Application;

namespace PaperTodo;

public sealed partial class AppController
{
    private sealed record PendingDataReload(AppState Before, AppState Next,
        DataReloadResult Result, PaperOperationContext Origin,
        IReadOnlyList<StateMergeConflict>? Conflicts,
        JsonObject? ExternalJson);

    /// <summary>
    /// data.json 热重载状态机的阶段。<c>Idle</c> 之外任何值都视为"正在 reload",
    /// <c>AwaitingApply</c> 与 <c>Applying</c> 期间还会抑制编辑器提交(避免覆盖刚合并的内存)。
    /// </summary>
    private enum ReloadPhase { Idle, Evaluating, AwaitingApply, Applying }

    private FileSystemWatcher? _dataWatcher;
    private DispatcherTimer? _dataReloadTimer;
    private bool _dataReloadReady;
    private ReloadPhase _reloadPhase = ReloadPhase.Idle;
    private int _dataReloadNotificationPosted;
    private PendingDataReload? _pendingDataReload;
    private DateTimeOffset? _lastDataReloadAt;
    private DataReloadResult? _lastDataReloadResult;

    internal bool IsDataReloading =>
        _reloadPhase != ReloadPhase.Idle || _pendingDataReload != null;
    internal bool SuppressDataReloadEditorCommit =>
        _reloadPhase == ReloadPhase.Applying || _pendingDataReload != null;

    private void InitializeDataReload()
    {
        DataReloadDiagnostics.Reset();
        DataReloadDiagnostics.Log("RELOAD", "InitializeDataReload called");
        _store.EnableExternalReload(State);
        _dataReloadTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _dataReloadTimer.Tick += OnDataReloadTimer;
    }

    private void StartDataReloadWatcher()
    {
        if (IsExiting || _dataReloadReady) return;
        _dataReloadReady = true;
        try
        {
            _dataWatcher = new FileSystemWatcher(Path.GetDirectoryName(_store.FilePath)!, "data.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                IncludeSubdirectories = false
            };
            _dataWatcher.Changed += OnDataFileChanged;
            _dataWatcher.Created += OnDataFileChanged;
            _dataWatcher.Deleted += OnDataFileChanged;
            _dataWatcher.Renamed += OnDataFileRenamed;
            _dataWatcher.Error += OnDataWatcherError;
            _dataWatcher.EnableRaisingEvents = true;
            QueueDataReload(); // covers edits between Load and watcher installation
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _dataWatcher?.Dispose();
            _dataWatcher = null;
            _lastDataReloadResult = new() { Outcome = "failed", Error = ex.Message };
        }
    }

    private void OnDataFileChanged(object sender, FileSystemEventArgs e)
    {
        DataReloadDiagnostics.Log("FSW", $"Changed {e.ChangeType} {e.FullPath}");
        QueueDataReload();
    }
    private void OnDataFileRenamed(object sender, RenamedEventArgs e)
    {
        DataReloadDiagnostics.Log("FSW", $"Renamed {e.OldFullPath} -> {e.FullPath}");
        QueueDataReload();
    }
    private void OnDataWatcherError(object sender, ErrorEventArgs e)
    {
        DataReloadDiagnostics.Log("FSW", $"Error: {e.GetException()?.Message}");
        QueueDataReload();
    }

    private void QueueDataReload()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted ||
            Interlocked.Exchange(ref _dataReloadNotificationPosted, 1) != 0) return;
        dispatcher.BeginInvoke(new Action(() =>
        {
            Interlocked.Exchange(ref _dataReloadNotificationPosted, 0);
            if (IsExiting || !_dataReloadReady) return;
            _dataReloadTimer!.Stop();
            _dataReloadTimer.Start();
        }), DispatcherPriority.Background);
    }

    private void OnDataReloadTimer(object? sender, EventArgs e)
    {
        _dataReloadTimer!.Stop();
        if (IsExiting || !_store.HasExternalStateChange()) return;
        DataReloadDiagnostics.Log("RELOAD", "Timer tick — calling ReloadDataCore");
        var result = ReloadDataCore(ImportDataOperation());
        DataReloadDiagnostics.Log("RELOAD", $"ReloadDataCore returned outcome={result.Outcome} applied={result.Applied} conflictCount={result.ConflictCount}");
        // Only a real, outstanding change waits for an ongoing gesture/apply. Invalid JSON
        // waits for a new file event or explicit request, not a permanent retry loop.
        if (result.Outcome == "busy" && !IsExiting) _dataReloadTimer.Start();
    }

    private static PaperOperationContext ImportDataOperation() =>
        new(PaperTodoEventOrigin.Import, null, Guid.NewGuid(), DateTimeOffset.UtcNow);

    internal DataReloadStatus GetDataReloadStatusCore()
    {
        Application.Current.Dispatcher.VerifyAccess();
        return new()
        {
            IsReloading = IsDataReloading,
            PendingExternalChange = _store.HasExternalStateChange(),
            BaselineRevision = _store.ReloadRevision,
            LastAttemptAt = _lastDataReloadAt,
            LastResult = _lastDataReloadResult
        };
    }

    internal DataReloadResult ReloadDataCore(PaperOperationContext origin)
    {
        Application.Current.Dispatcher.VerifyAccess();
        if (IsExiting) return new() { Outcome = "failed", Error = "PaperTodo is exiting." };
        if (!_dataReloadReady || IsDataReloading || Mouse.Captured != null || HasDeepCapsuleReorderDragInProgress())
            return new() { Outcome = "busy" };

        _reloadPhase = ReloadPhase.Evaluating;
        _lastDataReloadAt = DateTimeOffset.UtcNow;
        try
        {
            // Flush edits into MEMORY, never overwrite the candidate file before reading it.
            // Reentrant saves are suppressed while collecting the current UI snapshot.
            CommitSettingsExternalMarkdownEditor(saveImmediately: false);
            foreach (var window in _windows.Values.ToArray()) window.CommitPendingEditsForSave();
            PrepareExternalPaperOperation();
            var before = StateStore.CopyForReload(State);
            var commit = _store.ReloadExternalState(State, Interlocked.Increment(ref _saveVersion));
            DataReloadDiagnostics.Log("RELOAD",
                $"ReloadExternalState outcome={commit.Result.Outcome} applied={commit.Result.Applied} conflictCount={commit.Result.ConflictCount} " +
                $"conflictFile={commit.Result.ConflictFile ?? "<none>"}");
            if (commit.Result.Outcome != "unchanged" || _lastDataReloadResult == null)
                _lastDataReloadResult = commit.Result;
            if (commit.State == null)
            {
                return commit.Result;
            }

            _saveTimer.Stop();
            _forceSaveTimer.Stop();
            _hasPendingDirty = false;
            _pendingDataReload = new(before, commit.State, commit.Result, origin, commit.Conflicts, commit.ExternalJson);
            // A plugin can delete its own paper through this call. Return to the caller before
            // tearing down that session/runtime. Send precedes the next input/render/timer turn;
            // shared commands and saves remain gated until the committed model is reconciled.
            Application.Current.Dispatcher.BeginInvoke(new Action(CompletePendingDataReload), DispatcherPriority.Send);
            return commit.Result;
        }
        catch (Exception ex)
        {
            DataReloadDiagnostics.Log("RELOAD", $"ReloadDataCore threw {ex.GetType().Name}: {ex.GetBaseException().Message}");
            var result = new DataReloadResult { Outcome = "failed", Error = ex.GetBaseException().Message };
            _lastDataReloadResult = result;
            return result;
        }
        finally { if (_reloadPhase == ReloadPhase.Evaluating) _reloadPhase = ReloadPhase.Idle; }
    }

    private void CompletePendingDataReload()
    {
        var pending = _pendingDataReload;
        if (pending == null || _reloadPhase == ReloadPhase.Applying || IsExiting) return;
        DataReloadDiagnostics.Log("RELOAD",
            $"CompletePendingDataReload start: conflicts={(pending.Conflicts?.Count ?? 0)}");
        _reloadPhase = ReloadPhase.Applying;
        var suppressDirty = _suppressDirty;
        _suppressDirty = true;
        _trayRefreshSuppressionDepth++;
        var events = SuppressPaperPluginEventScans();
        try
        {
            CancelStartupDisplayRestore();
            _paperSurfaceRestoreGeneration++;
            _startupShellPrewarmGeneration++;
            var oldPapers = pending.Before.Papers.ToDictionary(p => p.Id, StringComparer.Ordinal);
            // ====== 冲突时弹窗让用户决策(逐个冲突单选) ======
            // 必须在 close 循环之前调用:UnknownBaselineBucket 的 KeepExternal 会让 effectiveNext
            // 直接等于 disk externalJson(StateStore.Reload.cs:225 的 unknown_baseline 路径下
            // pending.Next 仍为 memory 的 deep clone)。所有"按 id 集合决定关哪些 window / 清理哪些
            // plugin 状态"的判断都要基于 effectiveNext,而不是 pending.Next。
            var effectiveNext = ResolveConflictsInteractively(pending);
            var nextPapers = effectiveNext.Papers.ToDictionary(p => p.Id, StringComparer.Ordinal);
            foreach (var pair in _windows.ToArray())
            {
                if (!nextPapers.TryGetValue(pair.Key, out var next) ||
                    (oldPapers.TryGetValue(pair.Key, out var old) &&
                     (old.Type != next.Type || old.BodyProviderId != next.BodyProviderId)))
                {
                    // Close against the OLD model/provider, before installing the new snapshot.
                    // All dirty editor state was captured before disk commit.
                    TryExitCleanup(pair.Value.CloseForReal);
                    _windows.Remove(pair.Key);
                }
                else if (oldPapers.TryGetValue(pair.Key, out var oldPaper) &&
                         (oldPaper.CapsuleSide != next.CapsuleSide ||
                          oldPaper.CapsuleMonitorDeviceName != next.CapsuleMonitorDeviceName))
                    pair.Value.PrepareForCapsulePresentationModeChange();
            }

            StateReloadModels.Apply(State, effectiveNext);
            foreach (var paper in State.Papers)
            {
                if (_windows.TryGetValue(paper.Id, out var window) && oldPapers.TryGetValue(paper.Id, out var old))
                {
                    window.ApplyReloadedPaper(old);
                    if (!StateReloadModels.Equivalent(old.Items, paper.Items)) PrunePluginTodoActionsForPaper(paper.Id);
                    if (!paper.IsVisible && old.IsVisible) HidePaper(paper);
                    else if (paper.IsVisible && !old.IsVisible) ShowPaper(paper, activate: false);
                }
                else if (paper.IsVisible) ShowPaper(paper, activate: false);
            }
            if (StateReloadModels.SettingsChanged(pending.Before, State)) RefreshReloadedSettings(pending.Before);
            foreach (var id in oldPapers.Keys.Except(nextPapers.Keys, StringComparer.Ordinal))
                QueuePluginPaperStateDeletion(id);
            TryFlushPendingPluginPaperStateDeletes();
            ReconcilePluginRuntimes();
            RefreshTodoReminderSchedule();
            ArrangeDeepCapsules(animate: false);
            foreach (var paper in State.Papers)
                if (!oldPapers.TryGetValue(paper.Id, out var old) || old.Title != paper.Title ||
                    old.Content != paper.Content || old.BodyHeaderText != paper.BodyHeaderText)
                    RefreshTodoRowsForLinkedPaper(paper.Id);
            Interlocked.Increment(ref _stateRevision);
            NotifyPluginEventMutationStampChanged();
        }
        catch (Exception ex)
        {
            // Disk is already committed. Do not report a rolled-back transaction or reapply the
            // external file. Preserve the authoritative candidate and report presentation failure.
            StateReloadModels.Apply(State, pending.Next);
            _lastDataReloadResult = pending.Result with
            {
                Outcome = "failed", Applied = true,
                Error = "The data was saved, but a display refresh failed: " + ex.GetBaseException().Message
            };
        }
        finally
        {
            _trayRefreshSuppressionDepth--;
            _suppressDirty = suppressDirty;
            _pendingDataReload = null;
            _reloadPhase = ReloadPhase.Idle;
            events.Dispose();
        }
        RefreshTrayMenu();
        PublishExternalPaperOperation(pending.Origin);
        // 同步写盘:ReloadExternalState 写的是 merged candidate(默认 Resolution=paperTodo),
        // 用户在冲突弹窗里选 KeepExternal 后 State 与 candidate 不一致,必须立即同步写盘
        // 让磁盘反映用户决策;用户选 KeepMemory 或无冲突时 State == candidate,写盘
        // 仍然安全(只是覆盖为相同内容)。无条件的 SaveNow(sync: true) 避免 Equivalent
        // 判断遗漏任何路径导致磁盘停留在 1-10s autosave 之前的旧版本。
        DataReloadDiagnostics.Log("RELOAD", "CompletePendingDataReload: calling SaveNow(sync: true)");
        SaveNow(sync: true);
    }

    private void RefreshReloadedSettings(AppState before)
    {
        _imageStore.AutoCompressLargeImages = State.AutoCompressLargeImages;
        AppTypography.Configure(State.UiFontPreset, State.Zoom, State.CustomFontEnhancedBold, State.TextRenderingProfile);
        NoteTypography.Configure(State.NoteTextSize, State.NoteTextBold);
        ApplyGeneralSettingsAfterRestore();
        foreach (var window in _windows.Values.ToArray())
        {
            window.UpdateImageReferenceTextMode();
            window.UpdateMarkdownEditAnimation();
            window.UpdateTodoVisualSize();
        }
        RefreshTypography();
        RefreshThemeSurfaces();
        InitializeGlobalHotkeys();
        RefreshPluginShortcuts();
        RefreshFullscreenAvoidanceRuntime();
        RefreshExperimentalWindowRuntime();
        RefreshEdgeCapsuleHoverIntentRuntime();
        RefreshExperimentalOpacitySurfaces(animate: false);
        RefreshExperimentalFocusPresentationSurfaces();
        TelemetryService.SetEnabled(State.TelemetryEnabled);
        if (before.UsePersistentPowerShellProcess != State.UsePersistentPowerShellProcess ||
            before.PreferPowerShell7 != State.PreferPowerShell7)
        {
            PaperWindow.StopPersistentScriptProcesses();
            PaperWindow.EnsurePersistentScriptProcessForSettings(State);
        }
        // Delay host transport changes until the originating request has returned.
        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!IsExiting) RefreshMcpRuntime();
        }), DispatcherPriority.ContextIdle);
    }

    private void StopDataReloadWatcher()
    {
        _dataReloadReady = false;
        _dataWatcher?.Dispose();
        _dataWatcher = null;
        if (_dataReloadTimer != null)
        {
            _dataReloadTimer.Stop();
            _dataReloadTimer.Tick -= OnDataReloadTimer;
            _dataReloadTimer = null;
        }
    }

    private bool FinishDataReloadBeforeExit()
    {
        if (IsDataReloading)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(Exit), DispatcherPriority.Background);
            return false;
        }
        if (_dataReloadReady && _store.HasExternalStateChange())
        {
            _ = ReloadDataCore(ImportDataOperation());
            if (_pendingDataReload != null)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(Exit), DispatcherPriority.Background);
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 测试 seam:用 lambda 替换真实 dialog 调用,返回任意 PaperConflictChoice 或 ConflictChoice。
    /// 默认 null → 用真实弹窗;测试可设为 "(owner, bucket) => PaperConflictChoice.KeepExternal"。
    /// 返回值的运行时类型决定走 paper 分支还是 leaf 分支;其它返回 null 等价于关闭弹窗(KeepMemory)。
    /// </summary>
    internal static Func<Window?, ConflictBucket, object?>? ConflictDialogDriver;

    /// <summary>
    /// 当 pending.Conflicts 非空时,按 ID 聚合后逐桶弹窗让用户选保留当前/保留传入。
    /// 同一 paper 下的所有 leaf 冲突聚合到一个 PaperBucket,只弹一次 paper 级弹窗;
    /// 不可聚合的路径(papers.$order / $ / 孤立 leaf)走既有 leaf 弹窗。
    /// 返回应用了用户决策后的 effectiveNext。
    /// </summary>
    private AppState ResolveConflictsInteractively(PendingDataReload pending)
    {
        var conflicts = pending.Conflicts;
        if (conflicts == null || conflicts.Count == 0) return pending.Next;

        var buckets = ConflictAggregator.Group(conflicts, pending.ExternalJson);
        if (buckets.Count == 0) return pending.Next;

        var owner = SelectDialogOwner();
        var paperChoices = new Dictionary<string, PaperConflictChoice>(StringComparer.Ordinal);
        var leafChoices = new Dictionary<string, ConflictChoice>(StringComparer.Ordinal);
        // UnknownBaselineBucket(path=="$")的 KeepExternal 不能走 leaf 合并:整状态覆盖
        // 通过返回反序列化 externalJson 让 StateReloadModels.Apply 接管身份保留的字段级替换。
        var wholeExternalReplace = false;

        foreach (var bucket in buckets)
        {
            switch (bucket)
            {
                case PaperBucket paperBucket:
                {
                    var choice = InvokePaperDialog(owner, paperBucket);
                    if (choice == PaperConflictChoice.KeepExternal)
                        paperChoices[paperBucket.PaperId] = choice;
                    break;
                }

                case UnknownBaselineBucket unknownBucket:
                {
                    // 整状态冲突只可能来自 disk 上的整文件差异,用户选 KeepExternal 时
                    // 必须用整 external 替换 next,不要把 "$" 灌进 leafChoices。
                    // KeepMemory 走既有默认分支(pending.Next 反映 memory),磁盘照常
                    // 由 SaveNow(sync:true) 同步。
                    if (InvokeLeafDialog(owner, unknownBucket.Conflict) == ConflictChoice.KeepExternal)
                        wholeExternalReplace = true;
                    break;
                }

                case OrderBucket orderBucket:
                case SingleLeafBucket singleBucket:
                {
                    var leaf = bucket switch
                    {
                        OrderBucket o => o.Conflict,
                        SingleLeafBucket s => s.Conflict,
                        _ => null,
                    };
                    if (leaf == null) break;
                    var choice = InvokeLeafDialog(owner, leaf);
                    if (choice == ConflictChoice.KeepExternal)
                        leafChoices[leaf.Path] = choice;
                    break;
                }
            }
        }

        if (wholeExternalReplace && pending.ExternalJson != null)
        {
            // 直接反序列化 externalJson 到新 AppState;调用方的 StateReloadModels.Apply
            // 会按字段级把 external 复制到现有 State,自然处理"外部独有 paper 增 / 共有
            // paper 字段覆盖 / 内部独有 paper 丢弃",PaperData 引用身份不变。
            return JsonSerializer.Deserialize<AppState>(
                pending.ExternalJson.ToJsonString(StateStore.JsonOptions),
                StateStore.JsonOptions)!;
        }

        var withLeafApplied = leafChoices.Count == 0
            ? pending.Next
            : JsonPath.ApplyChoices(pending.Next, conflicts, leafChoices);
        return paperChoices.Count == 0 || pending.ExternalJson == null
            ? withLeafApplied
            : JsonPath.ApplyPaperChoices(withLeafApplied, paperChoices, pending.ExternalJson);
    }

    private static PaperConflictChoice InvokePaperDialog(Window? owner, PaperBucket bucket)
    {
        if (ConflictDialogDriver is { } driver)
        {
            var result = driver(owner, bucket);
            if (result is PaperConflictChoice paper) return paper;
            // null / 其它类型 → 关闭弹窗语义(KeepMemory)
        }
        return DataReloadPaperConflictDialog.Show(owner, bucket);
    }

    private static ConflictChoice InvokeLeafDialog(Window? owner, StateMergeConflict conflict)
    {
        if (ConflictDialogDriver is { } driver)
        {
            // 用一个最小的 SingleLeafBucket 承载 leaf 冲突,便于测试 lambda 单一签名
            var result = driver(owner, new SingleLeafBucket(conflict));
            if (result is ConflictChoice leaf) return leaf;
        }
        return DataReloadConflictDialog.Show(owner, conflict);
    }

    private Window? SelectDialogOwner()
    {
        foreach (var w in _windows.Values)
        {
            if (w.IsVisible && PresentationSource.FromVisual(w) is HwndSource) return w;
        }
        return null;
    }

    private bool SaveForNormalShutdown()
    {
        if (TrySaveNow(sync: true)) return true;
        if (!_store.HasExternalStateChange()) return false;
        try
        {
            // 旁路恢复文件已落盘;恢复路径无法用 "PaperTodo 数据重载" 标题通知用户,
            // 因此 SaveForNormalShutdown 不再弹窗。其他退出失败对话框 (上方 catch 注释)
            // 仍按原状负责告知用户保存失败本身。
            _store.SavePendingExitRecovery(State);
            return true;
        }
        catch { return false; } // existing exit failure dialog handles inability to preserve data
    }
}
