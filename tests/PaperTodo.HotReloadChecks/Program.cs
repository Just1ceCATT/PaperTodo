using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PaperTodo;

var checks = new (string Name, Action Run)[]
{
    ("v2-t1-save-race-stale-revision-discarded", V2T1SaveRaceStaleRevisionDiscarded),
    ("v2-t1b-save-race-fresh-revision-accepted", V2T1bSaveRaceFreshRevisionAccepted),
    ("v2-t1c-save-race-version-bump-discards-stale", V2T1cSaveRaceVersionBumpDiscardsStale),
    ("v2-t2-mcp-guard-blocks-during-reload", V2T2McpGuardBlocksDuringReload),
    ("v2-t3-paper-command-guard-blocks-during-reload", V2T3PaperCommandGuardBlocksDuringReload),
    ("v2-t4-state-store-stamp-fields-present", V2T4StateStoreStampFieldsPresent),
    ("v2-t5-reload-coordinator-present", V2T5ReloadCoordinatorPresent),
    ("v2-t6-data-hot-reloader-present", V2T6DataHotReloaderPresent),
    ("v2-t7-try-save-now-uses-captured-revision", V2T7TrySaveNowUsesCapturedRevision),
    ("v2-t8-event-hub-full-reload-reset-present", V2T8EventHubFullReloadResetPresent),
    ("v2-t9-mark-dirty-not-guarded-by-reload-flag", V2T9MarkDirtyNotGuardedByReloadFlag),
    ("v2-t10-single-instance-guard-skips-during-reload", V2T10SingleInstanceGuardSkipsDuringReload)
};

var failed = 0;
foreach (var check in checks)
{
    try
    {
        check.Run();
        Console.WriteLine($"PASS {check.Name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {check.Name}: {ex.Message}");
    }
}

if (failed != 0)
{
    Console.Error.WriteLine($"Hot reload checks failed: {failed}/{checks.Length}");
    return 1;
}

Console.WriteLine($"Hot reload checks passed: {checks.Length}/{checks.Length}");
return 0;

// =====================================================================
// V2-T1 系列:R4 Save race —— SaveJsonIfRevisionAsync generation validation
// =====================================================================

static void V2T1SaveRaceStaleRevisionDiscarded()
{
    using var scope = new TempDirectory();
    var store = NewStore(scope.Path);

    // 写入初始 json
    store.SaveJsonSync(store.SerializeState(NewState("light")), version: 1);

    // capturedRevision=1, currentRevision=2 → 应丢弃
    long current = 2;
    var task = store.SaveJsonIfRevisionAsync(
        "{\"theme\":\"stale\"}",
        version: 2,
        capturedRevision: 1,
        getCurrentRevision: () => current);

    var saved = task.GetAwaiter().GetResult();
    Assert(saved == false, "stale capturedRevision should be discarded");

    // data.json 应仍是 light(初始主题)
    var readBack = ReadTheme(store.FilePath);
    Assert(readBack == "light", $"stale write should not overwrite; got theme '{readBack}'");
}

static void V2T1bSaveRaceFreshRevisionAccepted()
{
    using var scope = new TempDirectory();
    var store = NewStore(scope.Path);
    store.SaveJsonSync(store.SerializeState(NewState("light")), version: 1);

    long current = 2;
    var task = store.SaveJsonIfRevisionAsync(
        "{\"theme\":\"dark\"}",
        version: 2,
        capturedRevision: 2,
        getCurrentRevision: () => current);

    var saved = task.GetAwaiter().GetResult();
    Assert(saved, "matching capturedRevision should write");

    var readBack = ReadTheme(store.FilePath);
    Assert(readBack == "dark", $"fresh write should commit; got theme '{readBack}'");
}

static void V2T1cSaveRaceVersionBumpDiscardsStale()
{
    using var scope = new TempDirectory();
    var store = NewStore(scope.Path);
    store.SaveJsonSync(store.SerializeState(NewState("light")), version: 5);

    // capturedRevision 匹配,但 version < _latestWrittenVersion(已写入 5)→ 物理并发丢弃
    long current = 7;
    var task = store.SaveJsonIfRevisionAsync(
        "{\"theme\":\"dark\"}",
        version: 3,
        capturedRevision: 7,
        getCurrentRevision: () => current);

    var saved = task.GetAwaiter().GetResult();
    Assert(saved == false, "version below _latestWrittenVersion should be discarded");

    var readBack = ReadTheme(store.FilePath);
    Assert(readBack == "light", "stale version write should not overwrite");
}

// =====================================================================
// V2-T2 / V2-T3:R2.3 mutation guard 静态验证(无需启动完整 WPF 应用)
// =====================================================================

static void V2T2McpGuardBlocksDuringReload()
{
    // 验证 McpCommandService.Execute 内有 IsReloading 检查且抛 McpApiException
    var srcPath = ResolveSourceFile("McpCommandService.cs");
    var source = File.ReadAllText(srcPath);

    Assert(source.Contains("_controller.IsReloading"),
        "McpCommandService must check _controller.IsReloading");
    Assert(source.Contains("McpApiException(\"state_reloading\""),
        "McpCommandService must throw McpApiException with code 'state_reloading'");
    // 确认不抛 PaperCommandException(因 Execute 末尾会重包丢失 error code)
    Assert(!ContainsReloadGuardViaPaperException(source),
        "McpCommandService must throw McpApiException directly, not PaperCommandException");
}

static void V2T3PaperCommandGuardBlocksDuringReload()
{
    var srcPath = ResolveSourceFile("PaperCommandService.cs");
    var source = File.ReadAllText(srcPath);

    Assert(source.Contains("_controller.IsReloading"),
        "PaperCommandService must check _controller.IsReloading");
    Assert(source.Contains("Error(\"state_reloading\""),
        "PaperCommandService must throw via Error(\"state_reloading\", ...)");
}

// =====================================================================
// V2-T4 系列:StateStore R4 新 API 暴露 + 字段验证
// =====================================================================

static void V2T4StateStoreStampFieldsPresent()
{
    // 验证 StateStore 暴露了 R4 新 API
    var storeType = typeof(StateStore);

    Assert(storeType.GetMethod(
        "SaveJsonIfRevisionAsync",
        BindingFlags.Public | BindingFlags.Instance,
        binder: null,
        types: new[] { typeof(string), typeof(long), typeof(long), typeof(Func<long>) },
        modifiers: null) != null,
        "StateStore must expose public SaveJsonIfRevisionAsync(string, long, long, Func<long>)");

    Assert(storeType.GetMethod(
        "DeserializeAppState",
        BindingFlags.NonPublic | BindingFlags.Instance,
        binder: null,
        types: new[] { typeof(byte[]) },
        modifiers: null) != null,
        "StateStore must expose internal DeserializeAppState(byte[])");

    // 验证 TryReadValidatedStateBytes 已从 private 升为 internal
    var tryRead = storeType.GetMethod(
        "TryReadValidatedStateBytes",
        BindingFlags.NonPublic | BindingFlags.Static,
        binder: null,
        types: new[] { typeof(string), typeof(byte[]).MakeByRefType() },
        modifiers: null);
    Assert(tryRead != null, "TryReadValidatedStateBytes must be accessible (internal)");
}

// =====================================================================
// V2-T5 / V2-T6:Reload 协调器 / DataHotReloader 存在性
// =====================================================================

static void V2T5ReloadCoordinatorPresent()
{
    // 验证 AppController 暴露 TryReloadState(AppState)
    var controllerType = typeof(AppController);
    var tryReload = controllerType.GetMethod(
        "TryReloadState",
        BindingFlags.NonPublic | BindingFlags.Instance,
        binder: null,
        types: new[] { typeof(AppState) },
        modifiers: null);
    Assert(tryReload != null, "AppController must expose internal TryReloadState(AppState)");

    // 验证 _isReloading 字段存在(R2 标志)
    var isReloadingField = controllerType.GetField(
        "_isReloading",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Assert(isReloadingField != null, "AppController must declare _isReloading field");
    Assert(isReloadingField!.FieldType == typeof(bool),
        "_isReloading must be bool");

    // 验证 IsReloading 内部访问器
    var isReloadingProp = controllerType.GetProperty(
        "IsReloading",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Assert(isReloadingProp != null && isReloadingProp.PropertyType == typeof(bool),
        "AppController must expose internal IsReloading property");
}

static void V2T6DataHotReloaderPresent()
{
    var assembly = typeof(AppController).Assembly;
    var reloaderType = assembly.GetType("PaperTodo.DataHotReloader", throwOnError: false);
    Assert(reloaderType != null, "PaperTodo.DataHotReloader must exist in main assembly");
    Assert(reloaderType!.GetInterface("IDisposable") != null,
        "DataHotReloader must implement IDisposable");

    // 验证构造参数签名
    var ctor = reloaderType.GetConstructor(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance,
        binder: null,
        types: new[] { typeof(StateStore), typeof(AppController), typeof(DateTime) },
        modifiers: null);
    Assert(ctor != null,
        "DataHotReloader constructor must accept (StateStore, AppController, DateTime)");
}

// =====================================================================
// V2-T7:TrySaveNow capture 时序 —— R1.3 SerializeState 之后捕获
// =====================================================================

static void V2T7TrySaveNowUsesCapturedRevision()
{
    var controllerType = typeof(AppController);
    var trySaveNow = controllerType.GetMethod(
        "TrySaveNow",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Assert(trySaveNow != null, "AppController.TrySaveNow must exist (private)");

    // 用 IL 静态检查 SerializeState 与 capturedRevision 读取的相对顺序。
    // 序:R4 要求 capturedRevision 在 SerializeState 之后读取。
    // (静态 IL 顺序检查替代运行时端到端测试 —— 后者需要完整 WPF 应用)
    var body = trySaveNow!.GetMethodBody() ?? throw new InvalidOperationException("no body");
    var module = trySaveNow.Module;

    // 简单检查:TrySaveNow 内必须引用 SaveJsonIfRevisionAsync(异步路径已切到 R4 API)
    // 通过 reader pattern 很难精确抓顺序,改为更可靠的源码 grep:
    var sourcePath = ResolveSourceFile("AppController.cs");
    var source = File.ReadAllText(sourcePath);

    // 寻找 TrySaveNow body 的开始与结束(粗略定位)
    var idxTrySave = source.IndexOf("private bool TrySaveNow(bool sync)", StringComparison.Ordinal);
    Assert(idxTrySave >= 0, "TrySaveNow source must be locatable");
    var idxEnd = source.IndexOf("internal void CommitPendingNoteContentsForSave", idxTrySave, StringComparison.Ordinal);
    Assert(idxEnd > idxTrySave, "TrySaveNow source must have an end");
    var body2 = source.Substring(idxTrySave, idxEnd - idxTrySave);

    // R1.3:capturedRevision 必须在 SerializeState 之后捕获
    var idxSerialize = body2.IndexOf("_store.SerializeState(State)", StringComparison.Ordinal);
    var idxCapture = body2.IndexOf("var capturedRevision", StringComparison.Ordinal);
    Assert(idxSerialize >= 0 && idxCapture >= 0, "TrySaveNow body must contain both SerializeState and capturedRevision");
    Assert(idxCapture > idxSerialize,
        $"R1.3: capturedRevision must be captured AFTER SerializeState (idxCapture={idxCapture}, idxSerialize={idxSerialize})");

    // R4:异步路径必须使用 SaveJsonIfRevisionAsync(非旧 SaveJsonAsync)
    Assert(body2.Contains("SaveJsonIfRevisionAsync"),
        "TrySaveNow async branch must use SaveJsonIfRevisionAsync");
}

// =====================================================================
// V2-T8:PaperBodyPluginEventHub.FullReloadReset 存在且签名正确
// =====================================================================

static void V2T8EventHubFullReloadResetPresent()
{
    var assembly = typeof(AppController).Assembly;
    var hubType = assembly.GetType("PaperTodo.PaperBodyPluginEventHub", throwOnError: false);
    Assert(hubType != null, "PaperTodo.PaperBodyPluginEventHub must exist");

    var fullReset = hubType!.GetMethod(
        "FullReloadReset",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Assert(fullReset != null, "PaperBodyPluginEventHub.FullReloadReset must be internal");

    var parameters = fullReset!.GetParameters();
    Assert(parameters.Length == 1,
        "FullReloadReset must take exactly one parameter");

    // ChangeStamp 必须是 internal 可见(否则调用方无法构造)
    var stampType = hubType.GetNestedType("ChangeStamp",
        BindingFlags.NonPublic | BindingFlags.Public);
    Assert(stampType != null, "PaperBodyPluginEventHub.ChangeStamp must be defined");
    Assert(!stampType!.IsNotPublic,
        "PaperBodyPluginEventHub.ChangeStamp must be internal (not private)");
}

// =====================================================================
// V2-T9:R2.3 —— MarkDirty 不应被 _isReloading 守卫(R2 硬约束)
// =====================================================================

static void V2T9MarkDirtyNotGuardedByReloadFlag()
{
    var controllerType = typeof(AppController);
    var markDirty = controllerType.GetMethod(
        "MarkDirty",
        BindingFlags.Public | BindingFlags.Instance);
    Assert(markDirty != null, "AppController.MarkDirty must exist");

    // MarkDirty body 内不应检查 _isReloading(R2.3 硬约束)。
    // 用 IL 提取 + 字符串匹配查找 IsReloading 引用。
    var body = markDirty!.GetMethodBody()
        ?? throw new InvalidOperationException("MarkDirty has no body");
    var module = markDirty.Module;

    // 通过 reflection 读 MarkDirty 所在 partial 文件源码以验证
    var sourcePath = ResolveSourceFile("AppController.cs");
    var source = File.ReadAllText(sourcePath);
    var idxMarkDirty = source.IndexOf("public void MarkDirty()", StringComparison.Ordinal);
    Assert(idxMarkDirty >= 0, "MarkDirty source locatable");
    // 找下一个 method 起始位置(粗略)
    var idxNext = source.IndexOf("\n    public", idxMarkDirty + 1, StringComparison.Ordinal);
    if (idxNext < 0) idxNext = source.Length;
    var idxPrev = source.LastIndexOf("\n    private", idxMarkDirty, StringComparison.Ordinal);
    var body2 = source.Substring(idxMarkDirty, idxNext - idxMarkDirty);

    Assert(!body2.Contains("IsReloading"),
        "R2.3 hard constraint: MarkDirty must NOT check _isReloading");
    Assert(!body2.Contains("_isReloading"),
        "R2.3 hard constraint: MarkDirty must NOT reference _isReloading");
}

// =====================================================================
// V2-T10:R2.3 —— App.HandleSingleInstanceCommand skip-and-retry 而非抛
// =====================================================================

static void V2T10SingleInstanceGuardSkipsDuringReload()
{
    // App.xaml.cs 在项目根,与 src/ 同级。从 PaperTodo.dll 向上探测任意祖先。
    var assemblyDir = Path.GetDirectoryName(typeof(AppController).Assembly.Location)!;
    string? sourcePath = null;
    var probe = assemblyDir;
    for (var depth = 0; depth < 8; depth++)
    {
        var candidate = Path.Combine(probe, "App.xaml.cs");
        if (File.Exists(candidate)) { sourcePath = candidate; break; }
        probe = Path.GetDirectoryName(probe.TrimEnd(Path.DirectorySeparatorChar))!;
        if (string.IsNullOrEmpty(probe)) break;
    }
    if (sourcePath == null)
    {
        throw new FileNotFoundException(
            $"App.xaml.cs not found under any ancestor of {assemblyDir}");
    }

    var source = File.ReadAllText(sourcePath);
    var idxDispatch = source.IndexOf("private void DispatchSingleInstanceCommand", StringComparison.Ordinal);
    Assert(idxDispatch >= 0, "DispatchSingleInstanceCommand must be locatable in App.xaml.cs");

    var idxNext = source.IndexOf("private void ExecuteSingleInstanceCommand", idxDispatch, StringComparison.Ordinal);
    Assert(idxNext > idxDispatch, "DispatchSingleInstanceCommand must have an end");
    var body = source.Substring(idxDispatch, idxNext - idxDispatch);

    Assert(body.Contains("IsReloading"),
        "DispatchSingleInstanceCommand must check IsReloading");
    Assert(body.Contains("BeginInvoke"),
        "DispatchSingleInstanceCommand must use BeginInvoke (skip-and-retry, not throw)");
    Assert(body.Contains("ApplicationIdle"),
        "DispatchSingleInstanceCommand retry must use ApplicationIdle priority");
}

// =====================================================================
// helpers
// =====================================================================

static StateStore NewStore(string directory) =>
    new(directory, DurableAtomicFileWriter.Shared);

static AppState NewState(string theme) => new()
{
    Theme = theme,
    Papers = []
};

static string ReadTheme(string path)
{
    using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
    return document.RootElement.GetProperty("theme").GetString()
        ?? throw new InvalidDataException($"Missing theme in {path}");
}

static string ResolveSourceFile(string fileName)
{
    // typeof(AppController).Assembly.Location 指向 PaperTodo.dll。
    // 在测试项目下,它位于 tests/<Test>/bin/Debug/<tfm>/PaperTodo.dll(从主输出复制而来);
    // 在主项目下,它位于 输出/<sub>/PaperTodo.dll。两种情况都需回溯到项目根。
    // 从项目根向上探测 src/<fileName>,最多向上 8 层。
    var assemblyDir = Path.GetDirectoryName(typeof(AppController).Assembly.Location)!;
    var probe = assemblyDir;
    for (var depth = 0; depth < 8; depth++)
    {
        var candidate = Path.Combine(probe, "src", fileName);
        if (File.Exists(candidate)) return candidate;
        probe = Path.GetDirectoryName(probe.TrimEnd(Path.DirectorySeparatorChar))!;
        if (string.IsNullOrEmpty(probe)) break;
    }
    throw new FileNotFoundException(
        $"{fileName} not found under any ancestor of {assemblyDir}");
}

static bool ContainsReloadGuardViaPaperException(string source)
{
    // 检查 IsReloading 检查附近是否抛 PaperCommandException(应避免)
    var idx = source.IndexOf("IsReloading", StringComparison.Ordinal);
    if (idx < 0) return false;
    var window = source.Substring(idx, Math.Min(200, source.Length - idx));
    return window.Contains("new PaperCommandException")
        || window.Contains("throw Error(");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "PaperTodo.HotReloadChecks",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}