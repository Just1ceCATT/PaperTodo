using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Threading;
using PaperTodo;
using PaperTodo.Plugin;

internal static partial class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string LiveMarker = ".papertodo-data-reload-fixture";

    private static void RunIsolatedLiveChecks()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PaperTodo.DataReloadChecks.Live", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(AppContext.BaseDirectory, file);
                if (relative.StartsWith("plugins" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
                if (!(file.EndsWith(".exe") || file.EndsWith(".dll") || file.EndsWith(".pdb") ||
                      file.EndsWith(".deps.json") || file.EndsWith(".runtimeconfig.json"))) continue;
                var target = Path.Combine(directory, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target);
            }
            File.WriteAllText(Path.Combine(directory, LiveMarker), "isolated fixture");
            var start = new ProcessStartInfo(Path.Combine(directory, "PaperTodo.DataReloadChecks.exe"))
            {
                WorkingDirectory = directory, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
            };
            start.ArgumentList.Add("--live-fixture");
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(90_000))
            {
                process.Kill(entireProcessTree: true); process.WaitForExit();
                Console.Write(output.GetAwaiter().GetResult()); Console.Error.Write(error.GetAwaiter().GetResult());
                throw new TimeoutException("Live data reload fixture timed out.");
            }
            Console.Write(output.GetAwaiter().GetResult()); Console.Error.Write(error.GetAwaiter().GetResult());
            Require(process.ExitCode == 0, "Live data reload fixture failed.");
        }
        finally { try { Directory.Delete(directory, true); } catch { } }
    }

    private static int RunLiveFixture()
    {
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, LiveMarker)))
            throw new InvalidOperationException("Refusing to use a non-fixture data directory.");
        var result = 0;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Dispatcher.InvokeAsync(async () =>
        {
            try { await LiveChecks(); }
            catch (Exception ex) { result = 1; Console.Error.WriteLine(ex); }
            finally { app.Shutdown(); }
        });
        app.Run();
        return result;
    }

    private static T Field<T>(object instance, string name)
    {
        var type = instance.GetType();
        if (type.GetField(name, Private) is { } field) return (T)field.GetValue(instance)!;
        if (type.GetProperty(name, Private) is { } property) return (T)property.GetValue(instance)!;
        throw new MissingMemberException(type.FullName, name);
    }
    private static async Task Until(Func<bool> condition, string label)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(12)) throw new TimeoutException(label);
            await Task.Delay(20);
        }
        await Dispatcher.Yield(DispatcherPriority.Background);
    }
    private static JsonObject ReadLive() => J(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data.json")));
    private static void WriteLive(JsonObject value) =>
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "data.json"), value.ToJsonString());
    private static JsonNode LivePaper(JsonObject document, string id) =>
        document["papers"]!.AsArray().Single(p => Str(p!["id"]) == id)!;

    private static async Task LiveChecks()
    {
        var seed = NewState();
        seed.Papers.Add(new() { Id = "note-b", Type = PaperTypes.Note, Content = "second", X = 450, Y = 120 });
        seed.McpEnabled = true; seed.McpAllowFullWrites = true; seed.McpAllowDeletes = true;
        var seedStore = new StateStore(); seedStore.SaveJsonSync(seedStore.SerializeState(seed), 1);
        var controller = new AppController();
        try
        {
            await controller.StartAsync(createDefaultPaper: false);
            var windows = Field<Dictionary<string, PaperWindow>>(controller, "_windows");
            foreach (var window in windows.Values) window.EnsureShellBuilt();
            await Dispatcher.Yield(DispatcherPriority.Background);
            controller.SaveNow(sync: true);
            var stateIdentity = controller.State;
            var paperIdentity = controller.State.Papers.Single(p => p.Id == "note-a");
            var noteA = windows["note-a"]; var noteB = windows["note-b"];
            var editorA = Field<MarkdownTextBox>(noteA, "_noteBox");
            var editorB = Field<MarkdownTextBox>(noteB, "_noteBox");
            var original = ReadLive();
            editorB.Text = "local draft";
            editorB.CaretOffset = 3;
            var canUndo = editorB.Document.UndoStack.CanUndo;
            using var native = new PaperBodyPluginHostApi(controller, controller.PaperCommands, "note-a", "fixture.native", [], () => !noteA.IsClosed, () => true);
            using var observer = new PaperBodyPluginHostApi(controller, controller.PaperCommands, null, "fixture.observer", PaperTodoPermissionNames.All, () => true, () => true);
            var observed = new List<PaperTodoEvent>();
            using var subscription = observer.Subscribe(new() { ExcludeOwnOperations = false }, observed.Add);
            LivePaper(original, "note-a")["content"] = "external body";
            LivePaper(original, "note-a")["title"] = "external title";
            LivePaper(original, "note-a")["x"] = 180;
            LivePaper(original, "todo")["items"]![0]!["text"] = "external todo";
            original["papers"]!.AsArray().Add(new JsonObject { ["id"] = "added", ["type"] = "note", ["content"] = "new paper", ["x"] = 250, ["y"] = 350 });
            WriteLive(original);
            var result = native.ReloadData();
            Require(result.Applied && result.ConflictCount == 0 && !noteA.IsClosed, "Native API did not commit/return before surface teardown");
            await Until(() => !controller.IsDataReloading, "Native reload completion");
            Require(ReferenceEquals(stateIdentity, controller.State) && ReferenceEquals(paperIdentity, controller.State.Papers.Single(p => p.Id == "note-a")), "state/paper identity changed");
            Require(ReferenceEquals(editorA, Field<MarkdownTextBox>(windows["note-a"], "_noteBox")) && editorA.PersistentText == "external body", "changed note did not refresh in place");
            Require(ReferenceEquals(editorB, Field<MarkdownTextBox>(windows["note-b"], "_noteBox")) && editorB.PersistentText == "local draft" && editorB.CaretOffset == 3 && editorB.Document.UndoStack.CanUndo == canUndo, "unrelated note/editor/undo state changed");
            Require(windows.ContainsKey("added") && Math.Abs(noteA.Left - 180) < 2, "new paper/geometry did not apply");
            Require(controller.State.Papers.Single(p => p.Id == "todo").Items[0].Text == "external todo", "todo did not apply");
            Require(observed.Any(e => e.Metadata.Origin == PaperTodoEventOrigin.Plugin && e.Metadata.SourcePluginId == "fixture.native"), "reload event origin was lost");
            Console.WriteLine("PASS live native merge, model/editor identity, local pending edit, undo, geometry, add and events");

            controller.SaveNow(sync: true);
            var watched = ReadLive(); LivePaper(watched, "note-a")["title"] = "watch";
            var activeWatcher = Field<FileSystemWatcher?>(controller, "_dataWatcher");
            Require(activeWatcher is { EnableRaisingEvents: true }, "watcher was not active");
            WriteLive(watched);
            await Until(() => controller.State.Papers.Single(p => p.Id == "note-a").Title == "watch" && !controller.IsDataReloading, "watcher reload");
            Require(ReferenceEquals(noteB, windows["note-b"]), "watcher rebuilt an unrelated window");
            Console.WriteLine("PASS live FileSystemWatcher and self-write suppression");

            controller.SaveNow(sync: true);
            var web = ReadLive(); LivePaper(web, "note-a")["title"] = "web title"; WriteLive(web);
            var webResult = (DataReloadResult)WebPluginWorkspaceRequests.Execute(native, "data.reload", JsonSerializer.SerializeToElement(new { }))!;
            Require(webResult.Applied, "Web bridge failed");
            await Until(() => !controller.IsDataReloading, "Web reload");
            var status = (DataReloadStatus)WebPluginWorkspaceRequests.Execute(native, "data.status", JsonSerializer.SerializeToElement(new { }))!;
            Require(status.LastResult?.Applied == true && !status.PendingExternalChange, "Web status disagreed");

            controller.SaveNow(sync: true);
            var remote = ReadLive(); remote["theme"] = "dark"; WriteLive(remote);
            var mcp = new McpCommandService(controller, controller.PaperCommands);
            var mcpResult = (DataReloadResult)mcp.Execute(JsonSerializer.SerializeToElement(new { method = "reload_data" }))!;
            Require(mcpResult.Applied, "MCP reload failed");
            await Until(() => !controller.IsDataReloading, "MCP reload");
            Require(controller.State.Theme == "dark" && ReferenceEquals(editorB, Field<MarkdownTextBox>(windows["note-b"], "_noteBox")), "settings refresh destroyed note editor");
            Require(mcp.Execute(JsonSerializer.SerializeToElement(new { method = "get_data_reload_status" })) is DataReloadStatus, "MCP status missing");
            Console.WriteLine("PASS shared Web/MCP routes and live theme refresh");

            using (var runtime = new PaperPluginRuntimeWorkspaceApi(controller, "fixture.runtime", [], () => true))
            {
                controller.SaveNow(sync: true);
                var runtimeEdit = ReadLive(); LivePaper(runtimeEdit, "note-a")["title"] = "runtime title"; WriteLive(runtimeEdit);
                Require((await Task.Run(runtime.ReloadData)).Applied, "Runtime API was not marshaled to UI");
                await Until(() => !controller.IsDataReloading, "Runtime reload");
            }
            Console.WriteLine("PASS Native runtime thread dispatch without new plugin permissions");

            // 同一 paper 的多字段冲突必须聚合到一个 paper 级弹窗,而不是 N 个 leaf 弹窗。
            // 测试通过 ConflictDialogDriver seam 注入桩:所有 paper 桶都选 KeepExternal,
            // 全部 leaf 桶都选 KeepExternal;记录 driver 调用次数验证聚合(应为 1 次 paper + 0 leaf)。
            controller.SaveNow(sync: true);
            // 用全新的 paper id 避免被之前测试的 disk 修改干扰 baseline。
            const string freshPaperId = "fresh-conflict-paper";
            var freshPaper = new PaperData { Id = freshPaperId, Type = PaperTypes.Note, Content = "fresh", X = 100, Y = 100 };
            controller.State.Papers.Add(freshPaper);
            controller.SaveNow(sync: true);

            // 内存改 fresh paper 的 title/content(与即将写入 disk 的不同)。
            // Title 限制 6 字符(PaperTitles.DefaultMaxTitleLength),用短字符串。
            var freshRef = controller.State.Papers.Single(p => p.Id == freshPaperId);
            freshRef.Title = "MEM";
            freshRef.Content = "local body";

            // disk 改 fresh paper 的 content/title/x — 三方对 content/title 都不同 → 2+ 冲突
            var conflictEdit = ReadLive();
            LivePaper(conflictEdit, freshPaperId)["content"] = "external body";
            LivePaper(conflictEdit, freshPaperId)["title"] = "EXT";
            LivePaper(conflictEdit, freshPaperId)["x"] = 250;
            WriteLive(conflictEdit);

            var driverCalls = new List<ConflictBucket>();
            var previousDriver = AppController.ConflictDialogDriver;
            AppController.ConflictDialogDriver = (owner, bucket) =>
            {
                driverCalls.Add(bucket);
                return bucket is PaperBucket
                    ? (object)PaperConflictChoice.KeepExternal
                    : ConflictChoice.KeepExternal;
            };
            try
            {
                var conflictResult = controller.PaperCommands.ReloadData(PaperOperationContext.Mcp());
                Require(conflictResult.Applied && conflictResult.ConflictCount >= 2, $"expected at least 2 leaf conflicts, got {conflictResult.ConflictCount}");
                await Until(() => !controller.IsDataReloading, "paper conflict reload");
                Require(driverCalls.Count == 1, $"expected 1 aggregated paper dialog, got {driverCalls.Count}");
                var firstBucket = driverCalls[0];
                Require(firstBucket is PaperBucket, $"expected paper bucket, got {firstBucket.GetType().Name}");
                var paperBucket = (PaperBucket)firstBucket;
                Require(paperBucket.PaperId == freshPaperId, $"paper id mismatch: {paperBucket.PaperId}");
                var appliedFresh = controller.State.Papers.Single(p => p.Id == freshPaperId);
                Require(appliedFresh.Content == "external body" && appliedFresh.Title == "EXT", "paper-level KeepExternal did not apply external values");
                Require(ReferenceEquals(freshRef, appliedFresh), $"{freshPaperId} identity broken after paper-level apply");
                var diffFile = conflictResult.ConflictDiffFile;
                Require(diffFile != null, "paper conflict did not write dataconflictdiff");
                var diff = J(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, diffFile!)));
                Require(diff["conflicts"]!.AsArray().All(c => Str(c!["path"]!).StartsWith($"papers[id={freshPaperId}]")), $"leaf paths in diff must remain under papers[id={freshPaperId}]");
                // 选 KeepExternal 后,disk 上的 data.json 必须同步反映用户决策(避免 1-10s
                // autosave 延迟期间用户以为"保留传入没生效")。
                var diskAfter = ReadLive();
                var diskFresh = diskAfter["papers"]!.AsArray().Single(p => Str(p!["id"]) == freshPaperId);
                Require(Str(diskFresh["content"]) == "external body", $"disk Content should be 'external body' after KeepExternal, got '{Str(diskFresh["content"])}'");
                Require(Str(diskFresh["title"]) == "EXT", $"disk Title should be 'EXT' after KeepExternal, got '{Str(diskFresh["title"])}'");
            }
            finally
            {
                AppController.ConflictDialogDriver = previousDriver;
            }
            Console.WriteLine("PASS live paper-level conflict aggregation, identity, diff unchanged, KeepExternal applied, disk synced");

            // KeepMemory 对称路径:同一 paper 的多字段冲突,driver 返回 KeepMemory 时磁盘也必须
            // 同步反映 memory 版本(避免 1-10s autosave 延迟)。覆盖 SaveNow(sync:true) 在
            // KeepMemory 路径上的 disk 同步保证,与 KeepExternal 段对称。
            controller.SaveNow(sync: true);
            const string kmPaperId = "keep-memory-conflict-paper";
            var kmPaper = new PaperData { Id = kmPaperId, Type = PaperTypes.Note, Content = "km-fresh", X = 100, Y = 100 };
            controller.State.Papers.Add(kmPaper);
            controller.SaveNow(sync: true);
            var kmRef = controller.State.Papers.Single(p => p.Id == kmPaperId);
            kmRef.Title = "KM-MEM";
            kmRef.Content = "local km body";
            var kmEdit = ReadLive();
            LivePaper(kmEdit, kmPaperId)["content"] = "external km body";
            LivePaper(kmEdit, kmPaperId)["title"] = "KM-EXT";
            WriteLive(kmEdit);
            AppController.ConflictDialogDriver = (owner, bucket) =>
                bucket is PaperBucket ? (object)PaperConflictChoice.KeepMemory : ConflictChoice.KeepMemory;
            try
            {
                var kmResult = controller.PaperCommands.ReloadData(PaperOperationContext.Mcp());
                Require(kmResult.Applied && kmResult.ConflictCount >= 2, $"expected at least 2 leaf conflicts, got {kmResult.ConflictCount}");
                await Until(() => !controller.IsDataReloading, "paper keep-memory conflict reload");
                var kmApplied = controller.State.Papers.Single(p => p.Id == kmPaperId);
                Require(kmApplied.Content == "local km body" && kmApplied.Title == "KM-MEM", "KeepMemory did not retain memory values in state");
                var kmDiskAfter = ReadLive();
                var kmDiskFresh = kmDiskAfter["papers"]!.AsArray().Single(p => Str(p!["id"]) == kmPaperId);
                Require(Str(kmDiskFresh["content"]) == "local km body", $"disk Content should be 'local km body' after KeepMemory, got '{Str(kmDiskFresh["content"])}'");
                Require(Str(kmDiskFresh["title"]) == "KM-MEM", $"disk Title should be 'KM-MEM' after KeepMemory, got '{Str(kmDiskFresh["title"])}'");
            }
            finally
            {
                AppController.ConflictDialogDriver = previousDriver;
            }
            Console.WriteLine("PASS live paper-level conflict KeepMemory path disk synced");

            // UnknownBaseline + KeepExternal:整状态替换场景。
            // disk baseline 不匹配($paperTodoRevision="unknown")→ StateStore.Reload.cs:225
            // 走 unknown_baseline 分支,只产生一个 path=="$" 的冲突进 UnknownBaselineBucket。
            // P0 修复让弹窗 KeepExternal 真正让 disk 反映 external 内容(整 state 替换)。
            controller.SaveNow(sync: true);
            const string ubExt1Id = "ub-ext-1";
            const string ubMemOnlyId = "ub-mem-only";

            // 内存准备:theme="light"、ext-1(content="mem")、独有 mem-only。
            controller.State.Theme = "light";
            if (!controller.State.Papers.Any(p => p.Id == ubExt1Id))
                controller.State.Papers.Add(new() { Id = ubExt1Id, Type = PaperTypes.Note, Content = "mem", X = 100, Y = 100 });
            if (!controller.State.Papers.Any(p => p.Id == ubMemOnlyId))
                controller.State.Papers.Add(new() { Id = ubMemOnlyId, Type = PaperTypes.Note, Content = "memory-only", X = 200, Y = 200 });
            controller.SaveNow(sync: true);

            var ubExt1Ref = controller.State.Papers.Single(p => p.Id == ubExt1Id);

            // 改 disk:删 mem-only、改 ext-1 content="ext"、改 theme="dark"、
            // $paperTodoRevision="unknown",让 baseline 在 reload history 中找不到匹配,
            // 触发 unknown_baseline 整状态冲突。disk 上不写 mem-only,以便验证整状态替换后
            // memory 独有的 mem-only 被丢弃。
            var ubEdit = ReadLive();
            var ubPapersArray = ubEdit["papers"]!.AsArray();
            var ubMemOnlyNode = ubPapersArray.SingleOrDefault(p => Str(p!["id"]) == ubMemOnlyId);
            if (ubMemOnlyNode != null) ubPapersArray.Remove(ubMemOnlyNode);
            ubEdit[StateStore.ReloadRevisionProperty] = "unknown";
            ubEdit["theme"] = "dark";
            LivePaper(ubEdit, ubExt1Id)["content"] = "ext";
            WriteLive(ubEdit);

            AppController.ConflictDialogDriver = (owner, bucket) => ConflictChoice.KeepExternal;
            try
            {
                var ubResult = controller.PaperCommands.ReloadData(PaperOperationContext.Mcp());
                Require(ubResult.Applied && ubResult.ConflictCount == 1,
                    $"expected 1 unknown-baseline conflict, got {ubResult.ConflictCount}");
                await Until(() => !controller.IsDataReloading, "unknown-baseline apply");
                Require(controller.State.Theme == "dark",
                    $"external theme did not replace, got '{controller.State.Theme}'");
                var ubExt1Applied = controller.State.Papers.Single(p => p.Id == ubExt1Id);
                Require(ubExt1Applied.Content == "ext",
                    $"ext-1 content not replaced, got '{ubExt1Applied.Content}'");
                Require(!controller.State.Papers.Any(p => p.Id == ubMemOnlyId),
                    "mem-only should be dropped after whole-external replace");
                Require(ReferenceEquals(ubExt1Ref, ubExt1Applied),
                    "ext-1 PaperData identity broken after whole-external apply");
                var ubDiskAfter = ReadLive();
                Require(Str(ubDiskAfter["theme"]) == "dark",
                    $"disk theme should be 'dark' after KeepExternal, got '{Str(ubDiskAfter["theme"])}'");
                var ubDiskExt1 = ubDiskAfter["papers"]!.AsArray().Single(p => Str(p!["id"]) == ubExt1Id);
                Require(Str(ubDiskExt1["content"]) == "ext",
                    $"disk ext-1 content should be 'ext' after KeepExternal, got '{Str(ubDiskExt1["content"])}'");
                Require(!ubDiskAfter["papers"]!.AsArray().Any(p => Str(p!["id"]) == ubMemOnlyId),
                    "disk should not contain mem-only after KeepExternal");
                // 整状态替换下 memory 独有的 paper 不仅要从 State.Papers / disk 中移除,
                // 还必须关掉对应的 window,否则旧胶囊会一直留在屏幕上。
                Require(!windows.ContainsKey(ubMemOnlyId),
                    "memory-only window survived whole-external replace");
            }
            finally
            {
                AppController.ConflictDialogDriver = previousDriver;
            }
            Console.WriteLine("PASS live unknown-baseline KeepExternal replaces whole state, drops memory-only papers, preserves shared identity, syncs disk");

            // 场景 A:external 把 paper id 改了(rename)。close 循环必须用 effectiveNext 的 id 集合,
            // 才能在 Apply 之前关掉旧 id 的 window,避免新 id 走 ShowPaper 新建、留下多胶囊叠加。
            controller.SaveNow(sync: true);
            const string renOldId = "ub-rename-old";
            const string renNewId = "ub-rename-new";
            if (!controller.State.Papers.Any(p => p.Id == renOldId))
                controller.State.Papers.Add(new() { Id = renOldId, Type = PaperTypes.Note, Content = "mem-old", X = 100, Y = 100 });
            controller.SaveNow(sync: true);

            var renEdit = ReadLive();
            var renPapersArray = renEdit["papers"]!.AsArray();
            renPapersArray.Remove(renPapersArray.Single(p => Str(p!["id"]) == renOldId));
            renPapersArray.Add(new JsonObject
            {
                ["id"] = renNewId, ["type"] = "note", ["content"] = "ext-new", ["x"] = 110, ["y"] = 110
            });
            renEdit[StateStore.ReloadRevisionProperty] = "unknown-rename";
            WriteLive(renEdit);

            AppController.ConflictDialogDriver = (owner, bucket) => ConflictChoice.KeepExternal;
            try
            {
                var renResult = controller.PaperCommands.ReloadData(PaperOperationContext.Mcp());
                Require(renResult.Applied, "rename reload did not apply");
                await Until(() => !controller.IsDataReloading, "rename apply");
                Require(windows.ContainsKey(renNewId), $"renamed paper did not open a surface under '{renNewId}'");
                Require(!windows.ContainsKey(renOldId), $"old-id window '{renOldId}' survived id rename");
                var renApplied = controller.State.Papers.SingleOrDefault(p => p.Id == renNewId);
                Require(renApplied != null && renApplied.Content == "ext-new",
                    $"external rename content missing for '{renNewId}'");
                Require(!controller.State.Papers.Any(p => p.Id == renOldId),
                    $"old id '{renOldId}' leaked into State.Papers");
            }
            finally
            {
                AppController.ConflictDialogDriver = previousDriver;
            }
            Console.WriteLine("PASS live unknown-baseline KeepExternal closes old-id window when external renames paper id");

            // 场景 B:external 把 paper 整个删了。close 循环必须识别到"旧 id 在 oldPapers 里、不在
            // effectiveNext 里",才会在 Apply 之前关掉 window,否则 Apply 不再遍历该 paper → 永远泄漏。
            controller.SaveNow(sync: true);
            const string delMemId = "ub-delete-mem";
            if (!controller.State.Papers.Any(p => p.Id == delMemId))
                controller.State.Papers.Add(new() { Id = delMemId, Type = PaperTypes.Note, Content = "mem-del", X = 130, Y = 130 });
            controller.SaveNow(sync: true);

            var delEdit = ReadLive();
            var delPapersArray = delEdit["papers"]!.AsArray();
            delPapersArray.Remove(delPapersArray.Single(p => Str(p!["id"]) == delMemId));
            delEdit[StateStore.ReloadRevisionProperty] = "unknown-delete";
            WriteLive(delEdit);

            AppController.ConflictDialogDriver = (owner, bucket) => ConflictChoice.KeepExternal;
            try
            {
                var delResult = controller.PaperCommands.ReloadData(PaperOperationContext.Mcp());
                Require(delResult.Applied, "delete reload did not apply");
                await Until(() => !controller.IsDataReloading, "delete apply");
                Require(!windows.ContainsKey(delMemId), $"external-deleted paper window '{delMemId}' leaked");
                Require(!controller.State.Papers.Any(p => p.Id == delMemId),
                    $"external-deleted paper '{delMemId}' leaked into State.Papers");
            }
            finally
            {
                AppController.ConflictDialogDriver = previousDriver;
            }
            Console.WriteLine("PASS live unknown-baseline KeepExternal closes external-deleted paper window");

            controller.SaveNow(sync: true);
            var deletion = ReadLive(); deletion["papers"]!.AsArray().Remove(LivePaper(deletion, "note-a")); WriteLive(deletion);
            Require(native.ReloadData().Applied && !noteA.IsClosed, "originating session closed before API returned");
            await Until(() => !controller.IsDataReloading, "self-delete completion");
            Require(noteA.IsClosed && !windows.ContainsKey("note-a"), "removed paper surface survived");
            Throws<PaperTodoPluginException>(() => native.ReloadData());
            Console.WriteLine("PASS source-paper deletion returns before teardown and invalidates its lease");

            controller.SaveNow(sync: true);
            var capsule = ReadLive(); capsule["useCapsuleMode"] = true; capsule["useDeepCapsuleMode"] = true;
            LivePaper(capsule, "note-b")["isCollapsed"] = true; WriteLive(capsule);
            Require(controller.PaperCommands.ReloadData(PaperOperationContext.Mcp()).Applied, "capsule update rejected");
            await Until(() => !controller.IsDataReloading, "capsule reload");
            Require(controller.State.Papers.Single(p => p.Id == "note-b").IsCollapsed && windows["note-b"].HasVisibleSurface, "collapsed paper lost its visible surface");
            controller.SaveNow(sync: true);
            var expanded = ReadLive(); LivePaper(expanded, "note-b")["isCollapsed"] = false; WriteLive(expanded);
            Require(controller.PaperCommands.ReloadData(PaperOperationContext.Mcp()).Applied, "expand rejected");
            await Until(() => !controller.IsDataReloading, "expand reload");
            Require(windows["note-b"].HasExpandedPaperSurface, "expanded paper not displayed");
            Console.WriteLine("PASS existing capsule/form presentation path");
        }
        finally
        {
            controller.Dispose();
            Require(Field<FileSystemWatcher?>(controller, "_dataWatcher") == null, "watcher remained after dispose");
        }
    }
}
