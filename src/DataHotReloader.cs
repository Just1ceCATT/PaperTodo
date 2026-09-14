using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Threading;

namespace PaperTodo;

/// <summary>
/// R1+R3+R4 实施:监听 <c>data.json</c> 的文件系统变更,
/// 防抖 350ms 后通过 <see cref="Dispatcher.InvokeAsync"/> 调度到
/// <see cref="AppController.TryReloadState"/>,由 AppController.R5 阶段协调实际重载。
/// 启动后 2 秒内(<see cref="_startupGraceUntil"/>)的变更视为启动期副作用,忽略。
/// </summary>
internal sealed class DataHotReloader : IDisposable
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan ReadRetryInterval = TimeSpan.FromMilliseconds(200);
    private const int ReadRetryAttempts = 3;

    private readonly StateStore _store;
    private readonly AppController _controller;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _debounceTimer;
    private readonly DateTime _startupGraceUntil;

    private FileSystemWatcher? _watcher;
    private byte[]? _lastAcceptedHash;
    private bool _disposed;

    public DataHotReloader(
        StateStore store,
        AppController controller,
        DateTime startupGraceUntil)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(controller);

        _store = store;
        _controller = controller;
        _dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException(
                "DataHotReloader requires an active WPF Application with a Dispatcher.");
        _startupGraceUntil = startupGraceUntil;
        _debounceTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = DebounceInterval
        };
        _debounceTimer.Tick += OnDebounceTick;
    }

    /// <summary>
    /// 开始监听 <c>data.json</c> 的变更。多次调用幂等。
    /// </summary>
    public void Start()
    {
        if (_disposed)
        {
            return;
        }

        var directory = Path.GetDirectoryName(_store.FilePath);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        _watcher = new FileSystemWatcher(directory, "data.json")
        {
            NotifyFilter = NotifyFilters.LastWrite
                | NotifyFilters.Size
                | NotifyFilters.CreationTime
                | NotifyFilters.FileName,
            EnableRaisingEvents = false,
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Renamed += OnFileRenamed;
        _watcher.EnableRaisingEvents = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        _debounceTimer.Stop();
        _debounceTimer.Tick -= OnDebounceTick;

        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnFileChanged;
            _watcher.Created -= OnFileChanged;
            _watcher.Renamed -= OnFileRenamed;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e) => RestartDebounce();

    private void OnFileRenamed(object sender, RenamedEventArgs e) => RestartDebounce();

    private void RestartDebounce()
    {
        if (_disposed)
        {
            return;
        }
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private async void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        if (_disposed)
        {
            return;
        }

        // 启动期抑制:DataHotReloader 装配后 ~2 秒内的写入视为启动期副作用。
        if (DateTime.UtcNow < _startupGraceUntil)
        {
            return;
        }

        // AppController 已进入 Exiting / Disposed,放弃本次 reload。
        if (!_controller.IsRunning)
        {
            return;
        }

        byte[] bytes;
        try
        {
            bytes = await _dispatcher.InvokeAsync(
                () => ReadValidatedBytesWithRetry(),
                DispatcherPriority.Background).Task.ConfigureAwait(false);
        }
        catch
        {
            return; // 读取失败,保持当前 State 不变
        }
        if (bytes.Length == 0)
        {
            return;
        }

        var hash = ComputeHash(bytes);
        if (_lastAcceptedHash != null && hash.SequenceEqual(_lastAcceptedHash))
        {
            return; // 与上次成功 reload 的内容完全相同,跳过
        }
        _lastAcceptedHash = hash;

        AppState? newState;
        try
        {
            newState = _store.DeserializeAppState(bytes);
        }
        catch
        {
            return;
        }
        if (newState == null)
        {
            return;
        }

        // 调度到 UI 线程执行 6 阶段 reload。
        await _dispatcher.InvokeAsync(
            () => _controller.TryReloadState(newState),
            DispatcherPriority.Normal).Task.ConfigureAwait(false);
    }

    /// <summary>
    /// 读取并校验 <c>data.json</c> 字节。最多重试 3 次(间隔 200ms),
    /// 容忍 <see cref="DurableAtomicFileWriter"/> 的 tmp+rename 过程中
    /// 短暂出现的"文件不存在"或"读取冲突"窗口。
    /// </summary>
    private byte[] ReadValidatedBytesWithRetry()
    {
        for (var attempt = 0; attempt < ReadRetryAttempts; attempt++)
        {
            if (StateStore.TryReadValidatedStateBytes(_store.FilePath, out var bytes)
                && bytes.Length > 0)
            {
                return bytes;
            }
            System.Threading.Thread.Sleep(ReadRetryInterval);
        }
        return [];
    }

    private static byte[] ComputeHash(byte[] bytes)
    {
        return SHA256.HashData(bytes);
    }
}