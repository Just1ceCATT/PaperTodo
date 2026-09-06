"""One-shot, branch-scoped edit executor. Removed before the PR is delivered."""
from pathlib import Path
import os
import subprocess

BRANCH = "fix/threading-resource-followup"
if os.environ.get("GITHUB_REF") != "refs/heads/" + BRANCH:
    raise SystemExit("This executor is restricted to its repair branch")

def git(*args):
    return subprocess.check_output(["git", *args], text=True).strip()

expected = {
    "src/AppController.Tray.cs": "ca108e00d586cb4a810efc3c7dee822ea2cfd42e",
    "src/PaperWindow.cs": "b270e2322b21bd64da6621a378cf53d1c4b5860c",
    "src/AnimationHelper.cs": "97f9ae18f032b6582265867e520a16e22b94918e",
    "src/PaperPluginRuntimePapersApi.cs": "e872fb55dd2deb9add62d4052cd2b9fd5559214d",
    "src/AppController.PluginRuntimePapers.cs": "2c3bc1997cbd7b483c335ad5612db698f82f8fee",
    "src/SingleInstanceHelper.cs": "d27e898286ae0fd4183d5495720b0954f9a8d6a6",
}
for path, sha in expected.items():
    if git("rev-parse", "HEAD:" + path) != sha:
        raise SystemExit("Source moved since review: " + path)

def replace(path, old, new):
    p = Path(path)
    raw = p.read_bytes()
    crlf = b"\r\n" in raw
    text = raw.decode("utf-8").replace("\r\n", "\n")
    if text.count(old) != 1:
        raise RuntimeError(f"Expected one replacement in {path}: {old[:100]!r}; got {text.count(old)}")
    updated = text.replace(old, new)
    if crlf:
        updated = updated.replace("\n", "\r\n")
    p.write_bytes(updated.encode("utf-8"))

def commit(paths, message):
    subprocess.run(["git", "add", "--", *paths], check=True)
    subprocess.run(["git", "diff", "--cached", "--check"], check=True)
    subprocess.run(["git", "commit", "-m", message], check=True)

tray = "src/AppController.Tray.cs"
for name, kind in [
    ("TrayMenuTemplate", "ControlTemplate"), ("SeparatorTemplate", "ControlTemplate"),
    ("TrayMenuItemTemplate", "ControlTemplate"), ("SegmentMenuItemTemplate", "ControlTemplate"),
    ("TrayContentMenuItemTemplate", "ControlTemplate"), ("TrayMenuItemStyle", "Style"),
    ("TrayContentMenuItemStyle", "Style"), ("TrayToolbarItemStyle", "Style")
]:
    replace(tray,
        f"    private static readonly {kind} Shared{name} = Build{name}();",
        f"    [ThreadStatic]\n    private static {kind}? _shared{name};\n"
        f"    private static {kind} Shared{name} => _shared{name} ??= Build{name}();")
replace(tray, "    [ThreadStatic]\n    private static ControlTemplate? _sharedTrayMenuTemplate;",
    "    // AppController has non-UI helpers too. First type access must not create global\n"
    "    // dispatcher-owned resources. No ThreadStatic field may have an initializer.\n"
    "    [ThreadStatic]\n    private static ControlTemplate? _sharedTrayMenuTemplate;")

animation = "src/AnimationHelper.cs"
for name, value in [
    ("SmoothEase", "new CubicEase { EasingMode = EasingMode.EaseOut }"),
    ("QuickEase", "new QuadraticEase { EasingMode = EasingMode.EaseOut }"),
    ("SnapEase", "new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut }")
]:
    replace(animation, f"    public static readonly IEasingFunction {name} = {value};",
        f"    public static readonly IEasingFunction {name} = FreezeShared({value});")
replace(animation, "    // 确保元素有 RenderTransform", """    // These fixed curves are immutable Freezables, not per-window animation state.
    // Freeze before publication so the first caller need not be the UI thread.
    private static T FreezeShared<T>(T value) where T : Freezable
    {
        value.Freeze();
        return value;
    }

    // 确保元素有 RenderTransform""")

paper = "src/PaperWindow.cs"
replace(paper, "    private static Style? _sharedCompactMenuItemStyle;", """    private static Style? _sharedCompactMenuItemStyle;
    [ThreadStatic]
    private static double _sharedCompactMenuItemStyleScale;""")
replace(paper, """    private static Style SharedCompactMenuItemStyle =>
        _sharedCompactMenuItemStyle ??= BuildCompactMenuItemStyle();""", """    private static Style SharedCompactMenuItemStyle
    {
        get
        {
            var scale = AppTypography.ScaleFactor;
            if (_sharedCompactMenuItemStyle == null || _sharedCompactMenuItemStyleScale != scale)
            {
                // Sealed styles cannot be edited. Replace the thread's cache when its baked-in
                // glyph metrics change; live menus replace their resource during typography refresh.
                _sharedCompactMenuItemStyle = BuildCompactMenuItemStyle();
                _sharedCompactMenuItemStyleScale = scale;
            }
            return _sharedCompactMenuItemStyle;
        }
    }""")
replace(paper, """                menu.FontFamily = AppTypography.UiFontFamily;
                menu.FontSize = AppTypography.Scale(13);
                menu.Language = AppTypography.Language;
                AppTypography.ApplyTextRendering(menu);
                foreach (var header in menu.Items.OfType<MenuItem>().Where(item => !item.IsEnabled))
                {
                    header.FontSize = AppTypography.Scale(12);
                }""", "                RefreshContextMenuTypography(menu);")
replace(paper, "    private static void UpdateContextMenuTheme(ContextMenu menu)", """    private static void RefreshContextMenuTypography(ContextMenu menu)
    {
        menu.Resources[typeof(MenuItem)] = SharedCompactMenuItemStyle;
        menu.FontFamily = AppTypography.UiFontFamily;
        menu.FontSize = AppTypography.Scale(13);
        menu.Language = AppTypography.Language;
        AppTypography.ApplyTextRendering(menu);
        foreach (var header in menu.Items.OfType<MenuItem>().Where(item => !item.IsEnabled))
        {
            header.FontSize = AppTypography.Scale(12);
        }
    }

    private static void UpdateContextMenuTheme(ContextMenu menu)""")
commit([tray, animation, paper], "fix: keep menu resources thread-local and freeze shared easing curves")

api = "src/PaperPluginRuntimePapersApi.cs"
p = Path(api)
text = p.read_text(encoding="utf-8")
start = text.index("    public IReadOnlyList<PaperPluginRuntimePaper> List()")
end = text.index("    public PaperPluginRuntimePaper? Get(", start)
replace(api, text[start:end], """    public IReadOnlyList<PaperPluginRuntimePaper> List()
    {
        EnsureUsable();
        return OnUi(() =>
        {
            var snapshot = _controller.GetPluginRuntimePapers(_providerId);
            var completeStartupPresentation = false;
            lock (_gate)
            {
                EnsureUsableLocked();
                if (!_startupSnapshotCaptured)
                {
                    _knownPaperIds.Clear();
                    _knownPaperIds.UnionWith(snapshot.Select(paper => paper.PaperId));
                    _startupSnapshotCaptured = true;
                    completeStartupPresentation = true;
                }
            }

            if (completeStartupPresentation &&
                !_dispatcher.HasShutdownStarted &&
                !_dispatcher.HasShutdownFinished)
            {
                _ = _dispatcher.BeginInvoke(
                    (Action)CompleteStartupPresentation,
                    DispatcherPriority.Background);
            }
            return snapshot;
        });
    }

""")
text = p.read_text(encoding="utf-8")
start = text.index("    public void SetHeaderText(")
end = text.index("    internal bool TryGetCapsulePresentation(", start)
replace(api, text[start:end], """    public void SetHeaderText(string paperId, string text)
    {
        EnsureUsable();
        var normalized = NormalizePaperId(paperId);
        OnUi(() =>
        {
            _ = _controller.RequirePluginRuntimePaper(_providerId, normalized);
            lock (_gate)
            {
                EnsureUsableLocked();
                _publishedHeaderPaperIds.Add(normalized);
            }
            _controller.SetPluginRuntimePaperHeader(_providerId, normalized, text ?? string.Empty);
        });
    }

    public void SetCapsulePresentation(
        string paperId,
        PaperCapsulePresentation? presentation)
    {
        EnsureUsable();
        var paperIdNormalized = NormalizePaperId(paperId);
        var presentationNormalized = PaperWindow.NormalizePluginCapsulePresentation(presentation);
        OnUi(() =>
        {
            _ = _controller.RequirePluginRuntimePaper(_providerId, paperIdNormalized);
            // Cache publication and UI application use the same dispatcher order. A worker must
            // not publish a future value that an earlier queued UI update can overwrite visually.
            lock (_gate)
            {
                EnsureUsableLocked();
                _publishedCapsulePaperIds.Add(paperIdNormalized);
                if (presentationNormalized == null)
                {
                    _capsulePresentations.Remove(paperIdNormalized);
                }
                else
                {
                    _capsulePresentations[paperIdNormalized] = presentationNormalized;
                }
            }
            _controller.SetPluginRuntimePaperCapsule(_providerId, paperIdNormalized, presentationNormalized);
        });
    }

""")
replace(api, """        lock (_gate)
        {
            EnsureUsableLocked();
            _publishedHeaderPaperIds.Clear();
            _publishedCapsulePaperIds.Clear();
            _capsulePresentations.Clear();
        }
        OnUi(() => _controller.ClearPluginRuntimePresentation(_providerId));""", """        OnUi(() =>
        {
            lock (_gate)
            {
                EnsureUsableLocked();
                _publishedHeaderPaperIds.Clear();
                _publishedCapsulePaperIds.Clear();
                _capsulePresentations.Clear();
            }
            _controller.ClearPluginRuntimePresentation(_providerId);
        });""")
replace(api, """    private T OnUi<T>(Func<T> action) =>
        _dispatcher.CheckAccess()
            ? action()
            : _dispatcher.Invoke(action);

    private void OnUi(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
            return;
        }
        _dispatcher.Invoke(action);
    }""", """    private T OnUi<T>(Func<T> action)
    {
        T InvokeWhenUsable()
        {
            // The lease can end after a worker queues its call but before the UI executes it.
            // Never hold _gate across dispatcher waits or controller/plugin callbacks.
            EnsureUsable();
            return action();
        }
        return _dispatcher.CheckAccess()
            ? InvokeWhenUsable()
            : _dispatcher.Invoke(InvokeWhenUsable);
    }

    private void OnUi(Action action)
    {
        OnUi(() =>
        {
            action();
            return true;
        });
    }""")
controller = "src/AppController.PluginRuntimePapers.cs"
replace(controller, "    private PaperData RequirePluginRuntimePaper(string providerId, string paperId)",
                    "    internal PaperData RequirePluginRuntimePaper(string providerId, string paperId)")
commit([api, controller], "fix: serialize runtime presentation publication and recheck queued lease access")

single = "src/SingleInstanceHelper.cs"
replace(single, "    private readonly string _pipeName;", """    private readonly string _pipeName;
    private readonly TimeSpan _commandReadTimeout;""")
replace(single, "    private CancellationTokenSource? _listenerCts;", """    private CancellationTokenSource? _listenerCts;
    private Task? _listenerTask;""")
replace(single, """    public SingleInstanceHelper(string mutexName, string pipeName)
    {
        _mutexName = mutexName;
        _pipeName = pipeName;
    }""", """    public SingleInstanceHelper(string mutexName, string pipeName)
        : this(mutexName, pipeName, TimeSpan.FromSeconds(2))
    {
    }

    internal SingleInstanceHelper(string mutexName, string pipeName, TimeSpan commandReadTimeout)
    {
        if (commandReadTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(commandReadTimeout));
        }
        _mutexName = mutexName;
        _pipeName = pipeName;
        _commandReadTimeout = commandReadTimeout;
    }""")
replace(single, "        _ = Task.Run(async () =>", "        _listenerTask = Task.Run(async () =>")
replace(single, """                    using var reader = new StreamReader(server);
                    var message = await reader.ReadLineAsync();
                    onCommandSignal?.Invoke(DecodeArgs(message));
                }
                catch (OperationCanceledException)
                {
                    break;
                }""", """                    // One tiny local command per connection. A stalled peer must not monopolize
                    // the listener, and shutdown must cancel a peer already connected to the pipe.
                    using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                    readTimeout.CancelAfter(_commandReadTimeout);
                    using var reader = new StreamReader(server);
                    string? message;
                    try
                    {
                        message = await reader.ReadLineAsync(readTimeout.Token);
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested)
                    {
                        // Only this peer timed out. Dispose its pipe and accept the next client.
                        continue;
                    }
                    token.ThrowIfCancellationRequested();
                    onCommandSignal?.Invoke(DecodeArgs(message));
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }""")
replace(single, """            _listenerCts?.Cancel();
            _listenerCts?.Dispose();""", """            var listenerCts = _listenerCts;
            _listenerCts = null;
            listenerCts?.Cancel();
            if (listenerCts != null)
            {
                if (_listenerTask == null)
                {
                    listenerCts.Dispose();
                }
                else
                {
                    // Do not join here: a completed command can itself be waiting on the UI.
                    _ = _listenerTask.ContinueWith(completed =>
                    {
                        _ = completed.Exception;
                        listenerCts.Dispose();
                    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
            }""")
commit([single], "fix: cancel stalled single-instance reads without stopping the listener")
subprocess.run(["git", "diff", "d15df50e6a4c20dc022f8471be9bdc0ffd21a9b9", "--check"], check=True)
print("FIX_COMMIT=" + git("rev-parse", "HEAD"))
