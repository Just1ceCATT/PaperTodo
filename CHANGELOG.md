# PaperTodo Changelog

[简体中文](CHANGELOG.zh.md)

> **Related Links**: [Back to Home](README.md) · [User Manual](doc/USER_GUIDE.en.md)

This log is written for general and power users alike. It focuses on user-facing features, behavioral changes, enhancements, and fixes.

---

### Planned / To-Do

- None currently.

### Under Evaluation

- **Scoop / Chocolatey Package Managers**: Scoop is a great fit for portable, green installations to simplify install and update flows; Chocolatey is more system-level. Release automation, package manifest maintenance, checksum verification, and user data retention need to be evaluated before adoption.

---

### Unreleased

#### Papers, Title Bar & Capsules

- **Paper menus and title-bar shortcuts**: On an expanded paper, the `···` button and right-click entry now open the same menu, including Quick, the paper title and Settings.
  - In Settings → General → Title-bar Shortcuts, move and reorder the four built-in actions (Associate by Drag, New Note, New Todo and External Editor) between the **Show in title bar** and **Show in menu** sections. Todo and Note papers share one layout; separate customization menus and visibility checkboxes are no longer needed.
  - On new installations, only Associate by Drag appears in the title bar; the menu lists New Note, New Todo and External Editor in that order. Upgrades preserve previously pinned actions. If a paper is too narrow to fit its built-in shortcuts, they move into the menu as a group, without duplicating actions still visible in the title bar. Plugin buttons use only the remaining title-bar space. Drag-to-associate works from either location.
  - The shortcut settings use the same Todo and Note icons as the capsules, with dedicated link and external-link icons for association and external editing. The action area now has a thin border, better divider spacing and icon alignment, and explanatory text beside the heading. Help Improve has moved below Fast Animations.
- **Fast animations**: General settings adds a Fast Animations switch that preserves visual transitions while reducing their duration to roughly 60% of the normal setting. It is off by default and does not affect reminders or other non-visual timers.
- **More compact edge previews**: Built-in Todo and Note hover previews share a 320-DIP maximum height; shorter previews continue to size themselves to their content. Plugin Mini size limits are unchanged.
- **Drag the entire master-capsule queue**: Drag the master capsule horizontally to move the entire queue to the opposite edge of the current monitor or either edge of another monitor, keeping member order unchanged. The queue temporarily retracts while a floating master capsule follows the pointer; releasing it triggers a smooth return animation into its new position rather than an abrupt jump. Hidden papers and expanded papers without a queue slot are unaffected. Vertical dragging still adjusts only the queue's starting height.
- **Compact master capsule**: Under General → Advanced, the master capsule can be reduced to an arrow-only style that hides the queue count.

#### Appearance, Skins & Materials

- **Seven experimental skins**: Paper, Mica, two Acrylic styles, tracing paper, Aero and pixel style each support independent color customization. Aero provides a translucent tint, localized reflections and parallax that responds to window position, without capturing the desktop background. Switching skins preserves the editor and text undo history. Papers remain resizable by dragging their visible edges; the Standard / Soft / Hidden grip options affect only how the grip is displayed, not whether resizing works.
- **Native Windows materials**: Windows 11 22H2 or later supports native Mica, Acrylic, tracing-paper and Aero backdrops on expanded papers and in Settings, with an option to retain the material's active-window appearance. The first time this is enabled, PaperTodo must be closed and reopened. Windows 11 24H2 (build 26100) or later can extend the material through the title bar; older supported systems use a compatible approach. Solid backgrounds remain in partially transparent views or where system transparency is disabled or unsupported. Decorative effects are hidden in high-contrast mode.
- **Five material-transparency levels**: Very Low / Low / Medium / High / Very High adjust the tint and cover strength of native and auxiliary materials. **Medium preserves the previous appearance.** These levels do not fade the whole window, so text and controls remain opaque; Aero reflections, relief and tracing-paper fibers remain visible at every level.
- **Material backgrounds and transition performance**: Capsules and menus using Mica, either Acrylic variant or tracing paper take one local background snapshot when shown and keep it stable while visible. During a capsule drag, a blurred desktop snapshot is reused until release, when the background is updated at the destination. Queue collapse and expansion retain the material already on screen to avoid repeated captures; visual-only animation overlays do not block clicks on other applications. Material refreshes during paper collapse and expansion also wait for the layout to settle, reducing stutter. When a paper with a native material collapses or expands, an opaque replacement background is prepared before the transition, preventing an extra transparent flash at the start. The full note or todo layout remains visible during scaling instead of being clipped. Shadows are confined to the surrounding gutter, and Aero reuses its rim-lighting assets to reduce rendering overhead.
- **Custom paper-background scaling**: With Stretch enabled, background images fill the paper while keeping their aspect ratio. With Stretch off, small images stay at their original size, while large images scale down only enough to fit in full, avoiding unnecessary enlargement or cropping.

#### Todos & Notes

- **Todo bottom-bar toggle**: The bottom quick-add bar is visible by default but can be turned off. Hiding it also disables deleting todos by dragging them into the trash.
- **Reorder selected todos as a group**: Move multiple selected todos together while preserving their relative order. Dragging near the top or bottom of the list scrolls smoothly, even while the trash preview is visible. Todos are deleted only when the pointer is released over the trash.
- **Multiline and collapsible todos**: `Shift+Enter` inserts a line break within the current todo; `Enter` still creates a new item. When a todo occupies more than four displayed lines, an arrow above the drag grip lets you collapse it to two lines or expand it again. The collapsed second line ends with an ellipsis; editing temporarily reveals the full text. Searching for text hidden by the fold highlights the todo and its expand arrow without automatically expanding it.
- **Linked-paper title updates**: Changing the maximum paper-title length immediately updates the linked-paper names displayed in Todo items.
- **Open notes in an external editor**: PaperTodo first uses the Windows application associated with the configured export extension. If no application is associated with that extension, it falls back to Notepad, even after the user changes the export extension.

#### Plugins, MCP & Codex

- **Plugin API 2.2 and compatibility**: Plugins targeting API 2.1 remain compatible. The `apiVersion` field now explicitly means the **minimum host API required by the plugin**. A plugin loads when its declared requirement falls within the host's supported API range; requirements below the minimum supported version or above the host's current version are rejected. Version 2.2 adds hidden-at-start plugin papers (`hidden`), custom Settings action buttons (`action`), the public application Settings API and permissions, and cross-paper presentation controls.
- **Cross-paper presentation controls**: MCP and Native/Web plugins can show, hide, expand, collapse or activate existing papers by ID. Show preserves whether a paper is collapsed; Hide changes visibility without deleting content. MCP paper queries also report the collapsed state.
- **Application Settings API**: MCP and Native/Web plugins can use one typed settings catalog to query and change user-facing preferences. Changes are rolled back if saving fails. MCP also supports unlinking a Todo from its associated paper.
- **MCP tool improvements**: When an argument has the wrong type, an array element is invalid or a required field is missing, the tool identifies the specific parameter and its location instead of returning a generic invocation error. Paper queries now expose `paper_id` alongside `id`; the create-paper schema states the configurable title-length limit, so common short titles do not require a separate settings query. `update_todo` also supports reordering through a zero-based `order` field.
- **Creating todos through plugins**: When a plugin creates or appends a todo, it can set the initial completion state, reminder and linked paper without requesting separate permission to edit existing todo content.
- **Plugin actions and hints**: Plugins can add Settings buttons handled by their own runtime, such as Refresh Now or Test Connection, without registering shortcuts; unavailable actions are disabled. Action-button and top-bar status-label tooltips can span multiple lines. Web and email links in plugin popups open through the system's default applications.
- **Plugin-body reads and writes**: Queries for papers, todos, notes or images no longer commit unrelated plugin bodies; external writes likewise avoid committing unrelated papers. If an external write targets a built-in Markdown note with pending user edits, those edits are applied first so they are not overwritten. Background saves and cleanup after a paper is deleted no longer block typing, and edits made during a save are retained for the next write.
- **Plugin data and failure handling**: Plugin state is stored in ordinary data files. If existing data cannot be read, PaperTodo reports an error instead of treating it as empty and creating a recovery file; old recovery files are not automatically deleted. Errors in routine theme, font or activation callbacks no longer remove the entire plugin body. An initial background startup failure no longer causes repeated automatic retries; a plugin that has already run successfully can attempt limited recovery before stopping, without endless reloads or rapid save retries.
- **Codex CLI Bridge**:
  - The default model is GPT-6 Astra (`gpt-6-astra`), with reasoning still set to `xhigh`. Leaving the model setting blank does not override the existing Codex CLI model selection. Sending a task opens a foreground PowerShell window by default, with background execution still available.
  - The plugin interface and default prompts follow PaperTodo's current language without replacing prompts the user has edited. Startup, foreground/background execution and model settings remain on the plugin card; reasoning and path options are grouped under **More Settings**.
  - Three **independent, default-on** switches control the plugin-development skill, PaperTodo operation skill and permission for the plugin to enable MCP when needed. Skills are injected when sending a task, separately from the editable default prompt. If permission to enable MCP is granted, the plugin can turn on the MCP master switch and its write/delete permissions without changing unrelated PaperTodo feature switches.
  - The built-in prompt asks for concise todos and suggests moving longer details into a linked Note only when todo-to-paper linking is enabled. For notes, it discourages unnecessary verbosity without dropping useful information.
  - The Codex prompt-editor paper is hidden at startup and can be opened directly through **More Settings → Edit Prompt**. On-demand MCP activation reports the actual outcome rather than merely confirming that a command was sent.

#### Fixes, Performance & Other Improvements

- **Todo performance and editing continuity**: Startup, paper opening and bulk paste involve less waiting when a Todo paper contains many items. Per-row context menus are created on demand, with mouse right-click, the Menu key and `Shift+F10` working on the first attempt. Reordering and undo/redo update only changed rows to reduce stutter while preserving editors, keyboard focus and text selections. Show/Hide All avoids rebuilding unchanged linked-todo rows and preserves text selections and undo history.
- **Edge-preview click targeting**: Fixed hit areas and forwarded clicks becoming misaligned with an edge preview's visible position during rapid switching.
- **Repeat-click linked-paper behavior**: When linked papers are excluded from the capsule queue, clicking the Todo link to an expanded paper again now retracts it if **Click capsule again to retract paper** is enabled; another click reopens it. This option was previously named **Click edge capsule again to retract paper**.
- **Capsule position memory**: The expanded paper's position and size are now remembered even when **Keep edge capsule visible while expanded** is off. Temporarily disabling capsule, edge-capsule or master-capsule features preserves the queue's starting height, which returns when the feature is re-enabled. Explicitly restoring defaults still clears that memory.
- **Multi-monitor startup and focus**: If a paper's saved coordinates are valid but its monitor has not yet been detected at startup, PaperTodo allows a brief, one-time grace period before applying normal off-screen position correction. Other papers restore immediately. Closing, hiding or deleting the active paper hands focus to an appropriate window rather than unexpectedly bringing a covered background window to the front; actions on background papers do not steal focus. Opening a paper from an edge capsule now brings it to the foreground before its expansion animation begins, rather than unexpectedly raising it at the end. Switching to another app mid-animation does not steal focus back.
- **Windows startup-app status**: Disabling PaperTodo through Windows Startup Apps or Task Manager is now reflected correctly in PaperTodo Settings. Startup can also be re-enabled from within PaperTodo.
- **Todo reminders**: Fixed multiple due reminders being marked as delivered when tray notifications were unavailable even though some were never shown. Reminders that were not actually displayed remain eligible for the normal retry path.
- **Web Mini interaction**: Fixed some plugin Mini surfaces becoming visible again while certain buttons remained unclickable.
- **Persistent PowerShell scripts**: Changing Prefer PowerShell 7 or Hide Run Window now affects only future script runs, without interrupting the current persistent PowerShell process. Explicitly disabling persistent-process mode still stops that process.
- **Review Archive data**: Unreadable, corrupt or unsupported existing archive data now triggers an explicit error. It is no longer mistaken for an empty archive that might later overwrite the original file.

#### Website & Dependencies

- **Interactive website**: The official site now uses a shared interactive workbench to demonstrate PaperTodo 4.0 papers, edge previews, Markdown, scripts and plugin creation. FAQs, paper colors and dark mode have also been updated. The page scales proportionally on viewports larger than 1600 × 900. First visits default to the browser's language (Chinese or English), unless the URL specifies a language or a previous preference has been saved.
- **Dependency updates**: WebView2 has been updated to `1.0.4191.47`, Microsoft.Extensions.Hosting to `10.0.12`, and the MCP SDK to `2.2.0`, incorporating runtime stability and compatibility fixes.
- Fixed edge capsules not remembering their expanded paper position and size when “Keep edge capsule visible while expanded” is turned off.
- **External-open fallback**: Opening a note externally still uses the app associated with the currently configured export extension; if Windows has no association for that extension, PaperTodo automatically falls back to Notepad.
- **Window focus**: Closing, hiding or deleting the active paper now keeps the next usable window in front instead of raising a covered background window. Removing a background paper leaves the current foreground alone.
- **Paper background scaling**: “Stretch” now fills the paper without changing the image aspect ratio; with Stretch off, smaller images keep their native size and oversized images are only scaled down proportionally until they fully fit inside the paper.
- **Codex CLI Bridge**: The default model is now GPT-6 Astra (`gpt-6-astra`); reasoning remains `xhigh`, and leaving the model setting empty still preserves the Codex CLI model setting. Settings now use the dedicated **More settings** page: startup, background execution, and model stay on the plugin card, while reasoning and path options remain available in the full settings page.
- **Interactive website**: The official website now introduces PaperTodo 4.0 through a shared workbench for papers, edge previews, Markdown, scripts and plugin creation. Includes updated FAQs, paper themes, dark mode and a layout that scales uniformly above 1600 × 900. First visits follow the browser language (Chinese or English), while an explicit URL language or saved choice takes priority.
- **Live data reload**: Editing `data.json` externally updates papers, todos, notes and settings without restarting. Independent changes merge automatically; conflicting values keep the running PaperTodo version, with the complete external input and conflict details saved separately and one notification per batch. Native / Web plugin APIs and MCP can explicitly reload and inspect the result. Interface language changes still require a restart.

### v4.0.0-beta1

#### Edge Preview & Capsules

- **Live edge previews**: Hover over an edge capsule to browse and interact with content without opening the full paper.
  - **Todo live preview**: Shows a simplified todo list with direct check / uncheck actions. Click the card background to open the full paper; linked-paper and quick-launch actions use stable dedicated click targets.
  - **Note live preview**: Supports up to 6000 characters and follows the note's Markdown rendering.
  - **Smoother handoff and intent prediction**: Adjustable pointer-intent prediction reduces accidental switching while browsing between capsules. “Prefer downward expansion while browsing” can be enabled; when enabled, downward switches expand downward whenever the target card still fits instead of filling the space above.
- **Repeat-click behavior**: Clicking an edge capsule again retracts its paper when the paper is still clearly visible; if the paper is substantially covered by another window, it is brought to the front instead of being collapsed out of sight.
- **More flexible edge-capsule titles**: Title length can be reduced further, including fully hiding the title.
- Todo and note capsule icons and titles now align consistently.
- Hiding the close button no longer leaves an empty slot.
- Master capsules use a more compact count-only presentation.

#### Plugin System & Desktop Micro-Apps

- **Desktop micro-app container**: Note papers can become independent desktop micro-apps such as Pomodoro timers, clocks, review pools or system monitors. Settings adds a dedicated Plugins center; plugin data is isolated under `plugins/data/` with 10 MB / 20 MB limits.
  - **Web / Native dual runtime architecture**:
    - **Web plugins**: Built with WebView2 and standard HTML/CSS/JS, with no compilation required.
    - **Native plugins**: Built with .NET 10 + WPF for fully custom, high-performance desktop UI.
  - **Deep host integration**:
    - **Custom capsules and Mini views**: Plugins can customize collapsed capsules with icons, text, dynamic progress bars / rings or fully custom WPF drawing, and provide lightweight Mini views for edge hover.
    - **Todo integration**: Plugins can add right-side todo action icons and context-menu actions.
    - **Top bar and keys**: Plugins can add top-bar actions and status labels, register dedicated global hotkeys, and exclusively handle `Esc` and body context menus.
    - **Paper menus and lightweight popups**: Plugins can add text actions to paper context menus, read permitted note images, and open small focus-loss-closing popups from clicked buttons or menu items.
    - **Internationalization**: Plugin manifests can provide localized text that follows the current UI language.
  - Security: there is no artificial security sandbox; install third-party plugins only from trusted sources.
  - Examples:
    - Example plugins include the Codex CLI Bridge, which can send todo or paper context to a local Codex CLI, plus native clocks, Pomodoro timers, review pools and Web clocks adapted to narrow windows, capsules and Mini views.
    - Full example source lives in `plugin-samples/`.
    - Ready-to-use plugins live in `plugins/`.

#### Todos & Markdown

- **Full-text search**: `Ctrl+F` searches all todos and notes, showing current-paper and global match counts. Use `Enter` / `Shift+Enter` or the navigation controls to move through matches across papers, with matching capsules opened automatically; the search bar can be dragged to reposition it.
- **WYSIWYG Markdown editing**: Full Render provides block-level Markdown rendering while editing, keeping the note visually rendered instead of switching to a separate plain-source view.
- **Markdown parsing and display consistency**: Headings, quotes, lists, code blocks, links, basic HTML, escapes and image-code boundaries now share the same Markdown semantics, reducing inconsistencies in complex nested content.
- **Todo multi-selection and batch actions**: Drag along the left side to select multiple todos, then batch check / uncheck, copy, delete from the context menu, or drag the group to the trash. Arrow-key editing can continue across items without racing through multiple todos on key repeat.
- **Todo reminders**: Set custom durations, preset intervals, this evening, tomorrow morning and other reminder times; due reminders locate and highlight the todo with a tray notification and sound.
- The paper limit increases from 100 to 200.
- `Ctrl+Shift+C` copies selected todos as Markdown tasks with completion states, or copies selected note text as plain text.
- Markdown supports bold-italic (`***text***` / `___text___`), combinations of bold / italic / strikethrough / links, and backslash escapes.

#### Important Fixes & Performance Improvements

- **Saving and input protection**: Improve saving reliability in extreme conditions, reduce backup frequency and check availability before updating.
- **Fullscreen avoidance**: Fix papers remaining above some administrator-elevated fullscreen apps in the foreground when fullscreen avoidance is enabled.
- **Faster startup and exit**: Parallel and queued startup / shutdown work reduces waiting and improves responsiveness.

#### Appearance, Settings & Interaction

- **Custom paper backgrounds**: Place `papertodo.png`, `papertodo.jpg` or `papertodo.jpeg` beside `PaperTodo.exe` to use a custom paper background.
  - Appearance settings can show the original image or blend it with paper colors. Stretch is a separate toggle; position can be Center / Bottom Left / Bottom Center / Bottom Right.
  - Decoding caps the longest edge at 4096 pixels.
- **Settings redesign**: Settings now uses left-side navigation and improved layout, adapting to the active work area on small screens, high scaling and cross-DPI moves. Switches and hotkey recording refresh locally to reduce flicker. Edge browsing settings now live in General instead of Experimental Labs.
- **Quick close for the active paper**: Expanded papers support `Ctrl+W`, and middle-clicking the title bar performs the same close / collapse action.

#### Experimental Labs Features

- **Local MCP**: Start with `--mcp` to expose a standard MCP service that lets authorized AI assistants read, create, append and manage PaperTodo notes / todos; Settings can copy the configuration and AI Skill prompt in one click.
- **External window tethering**: Drag the paper's top-bar tether control onto a third-party window to attach the paper and smoothly follow that window as it moves, minimizes and restores.
- **Magnetic edge capsules**: Floating capsules can snap near screen or external-window edges, slide out on hover and retract when the pointer leaves.
- **New global hotkeys**:
  - Lock all papers.
  - Toggle opacity for all papers or the active paper.
  - Toggle opacity for all capsules.
  - Send papers / capsules behind other windows.
- **Focus-loss automation**: Papers can fade, hide top-bar icons / the title bar, or collapse into capsules when focus is lost.
- **Resting translucency**: Normal and docked capsules can become translucent while idle, with separate opacity controls.
- Docked and master capsules can disable forced topmost behavior.

#### Other Fixes & Improvements

- Todo paste exceeding count / text limits and notes reaching the input-protection limit now show explicit notices instead of silently dropping or refusing input.
- Papers opened from capsules obtain foreground focus more reliably, avoiding keyboard input or `Ctrl+W` still going to the previous app.
- Fix “Restore defaults for this page” on Shortcuts failing to restore the “Distinguish numpad digits” option.
- Fix Settings dropdowns not fully following the current theme.
- Fix papers with “Remember expanded position” being pulled back to the capsule's monitor after collapsing when the paper and capsule are on different displays; newly remembered positions support mixed scaling.
- Fix edge capsules moved to a secondary monitor sometimes opening their paper back on the old monitor.
- Long-note edits update only affected Markdown ranges; multi-line fence changes track the actual range to avoid full reparsing or missed updates. Improve capsule animation, monitor switching and window tracking with high refresh rates and mixed-DPI displays.
- The Windows single-file package without .NET shrinks from about 33 MiB to 17 MiB.

---

**待办与 Markdown**
- **内置全文搜索**：`Ctrl+F` 可跨全部待办和 Markdown 笔记查找，包含已完成的待办，显示当前纸片与全局命中计数；`Enter` / `Shift+Enter` 或箭头按钮可循环跳转并定位到具体待办项或笔记文字，自动显示隐藏纸片、展开胶囊并接续搜索。搜索框唤出后可直接输入，从纸片内展开并允许超出边框；可通过靠右的圆角竖条拖动柄、计数区或空白区域移动，采用无阴影样式，上下箭头和关闭按钮使用矢量图标。
- **完全渲染升级为所见即所得块级编辑**：笔记选择「完全渲染」档，编辑时也按标题、列表、引用、代码块、图片与行内样式等最终排版直接显示，并隐藏多数 Markdown 标记
标题、加粗、链接等文字紧凑重排；任务框、无序列表和引用使用稳定占位，减少勾选、编辑切换与引用续行时的横向跳动；完全渲染中的任务复选框可直接点击勾选或取消，实际写回原始 Markdown `[ ]` / `[x]` 并正常进入撤销 / 重做历史；有序列表编号始终直接显示原文字，避免编辑切换时闪动；光标所在块会显示其源码标记，失焦回到整篇只读渲染。
「Markdown 渲染动画」勾选后会有短淡入，该设置仅在选择「完全渲染」时显示。
- **Markdown 解析与显示一致性重构**：笔记的标题、引用、列表、代码块、链接、简单 HTML、转义与图片代码区判断统一 Markdown 语义，减少复杂嵌套时样式、点击和列表编辑不一致。
- **Markdown 实时显示**：编辑时也会显示全量 Markdown 渲染
- **待办连续多选与批量操作**：在待办项左侧区域滑动即可连续框选多条事项，支持批量勾选完成/恢复、批量复制、右键删除全部或拖拽整组丢弃。
- Markdown 语法增强：行内样式支持粗斜体语法（`***文本***` / `___文本___`），粗体、斜体、删除线与链接可自然组合，并支持反斜杠转义 Markdown 标点符号。
- 长笔记 Markdown 输入优化：长笔记普通编辑只刷新附近受影响的 Markdown 语义；新建、删除或修改跨多行代码围栏时会轻量追踪实际影响范围后再刷新，减少整篇重复解析，并避免长代码块编辑过程中只局部更新的问题。
- 待办方向键连续编辑：方向键可跨待办项移动，长按不会连续穿过多条待办。
- 复制格式转换：待办多选可用 `Ctrl+Shift+C` 或右键“复制为 Markdown”生成保留完成状态的 Markdown 任务列表；笔记正文可用同一快捷键或右键“复制为纯文本”去除 Markdown 标记后复制。普通 `Ctrl+C` 保持原有复制方式。

**高级快捷键与系统交互**

- **设置面板重排**：设置窗口改为左侧导航，按常规、待办、笔记、外观、快捷键、插件、实验室分类；窗口与胶囊选项并入常规，常规 / 待办 / 笔记可分别恢复本页默认，每页独立记忆滚动位置，高级模式固定在侧栏底部；「帮助改进」移入常规并简化说明。小屏幕、高缩放和跨不同 DPI 显示器时会按当前工作区重新适配。
- **高级全局快捷键**：新增快捷键一键锁定全部便签、切换全部纸片透明度、全部胶囊透明度或当前焦点纸片透明度。
- **关联纸片显隐恢复**：高级模式可保留关联纸片在快捷隐藏前的隐藏状态，避免快捷键恢复时意外弹出原本隐藏的关联窗口。
- **失焦自动收起**：可让展开纸片在真正失去焦点后自动收起为胶囊；编辑、拖拽、菜单与 Passive 等交互状态不会误触发。
- **当前纸片快捷关闭**：展开纸片支持 `Ctrl+W`；在标题区域点击鼠标中键也可执行与右上角关闭按钮相同的关闭或收起动作。
- **胶囊展开后的焦点稳定性**：从胶囊打开纸片后会更可靠地取得前台焦点，减少纸片已经展开但键盘输入或 `Ctrl+W` 仍作用在之前应用窗口的情况。
- 设置页响应优化：设置项开关与快捷键录制改为局部刷新，切换选项更丝滑无闪动。

**实验室功能**

- **本地 MCP 接口**：支持通过 `--mcp` 模式启动标准 MCP 服务，允许外部 AI 助手按授权读取、新建、追加写入或管理 PaperTodo 便签与待办；设置页可一键复制配置与 AI Skill 提示词。
- **失焦与静置透明度**：可让纸片失焦后自动半透明，或让普通/贴边胶囊在静置时自动半透明，并分别调整透明度及主胶囊、激活状态的应用范围。
- **外部窗口绑定**：可将便签顶栏的「窗口绑定」按钮拖到任意第三方软件窗口，使便签自动贴靠并平滑跟随目标窗口移动、最小化与恢复。
- **边缘磁吸胶囊**：普通浮动胶囊拖近屏幕或外部窗口边缘时可自动吸附停靠，鼠标移入滑出、移开自动收回。
- **待办定时提醒**：待办事项支持设置自定义倒计时提醒（支持预设间隔、当天傍晚、次日早晨等），到期自动定位高亮并弹出托盘通知与提示音。
- **失焦形态与置顶自定义**：支持纸片失焦时仅隐藏顶栏按钮，或淡出标题栏并保留完整的圆角、描边和阴影，完全隐藏后可点击到后面的窗口，正文位置和窗口尺寸保持不变；支持让贴边胶囊与主胶囊不再强制保持系统最前台置顶。
- **置底与鼠标穿透**：支持通过快捷键将纸片或胶囊置底并开启鼠标点击穿透，彻底融入桌面背景。

**优化与修复**
- **数据保存可靠性增强**：优化保存逻辑，降低极端情况下丢失文件的可能性。优化备份逻辑，降低更新频率，更新前校验可用性。
- **输入限制提示**：待办批量粘贴超出数量 / 文本限制，或笔记达到输入保护范围时会给出明确提示，避免静默丢弃或无反馈拒绝。
- 修复快捷键页“恢复本页默认”未正确恢复“小键盘数字键区分”默认状态的问题。

- 优化高刷新率与多屏高 DPI 环境下的贴边胶囊动画、显示器切换与窗口跟随流畅度。
- 修复边缘胶囊拖拽至副屏后点击展开可能误回到原显示器的问题。
- 修复开启“记住展开位置”时，纸片与边缘胶囊位于不同屏幕，收起后再次展开会被拉回胶囊所在屏幕的问题；新记忆的位置支持不同缩放比例的屏幕。
- 修复设置页部分选择下拉菜单未完整跟随当前主题配色问题。

### v0.1 初始构建

- **全新原生框架**：基于 .NET 10 和 WPF 打造的轻量级无主窗口“纸片”待办应用，绿色免安装。
- **无重度依赖**：不使用 WebView、Tauri、Electron 等重度渲染引擎，无 MSIX/Store/AppX 打包限制，系统资源占用极低。
- **本地数据优先**：支持基础的待办管理与便签创建，数据采用本地 JSON 格式存储，完全离线。

### v0.2 纸片生命周期

- **多纸片管理**：支持创建和管理多张独立的纸片窗口，自动记录每张纸片的位置、大小、置顶状态与内容。
- **启动智能恢复**：程序启动时自动恢复此前未删除的纸片；检测到纸片因多显示器切换等原因超出当前屏幕边界时，会自动拉回可视区域，防止窗口丢失。
- **数据安全备份**：引入 data.backup.json 自动备份与损坏恢复机制，在主配置文件意外受损时可尝试自动恢复。
- **生命周期规范**：关闭窗口即为隐藏，点击删除才会触发真正的确认移除。

### v0.3 待办纸基础交互

- **待办快捷操作**：支持通过 Enter 快速新建待办、Backspace 删除空行，勾选复选框时已完成项会自动添加删除线并淡化显示。
- **追加与清理**：提供底部 ＋ 快速追加区，支持一键清除所有已完成事项，也可以单独删除单项。
- **输入节奏优化**：优化空行限制规则，允许连续创建并保留多个空待办行。

### v0.4 托盘与纸片入口

- **托盘中心控制**：引入任务栏托盘图标，支持一键新建待办/笔记、显示/隐藏全部纸片以及快速退出。
- **托盘纸片列表**：托盘菜单可实时列出所有活跃纸片并支持长标题自动省略，点击对应的纸片名称可快速切换显隐。
- **预览命名与置顶**：纸片名称可根据第一行内容自动生成；纸片左上角功能图标改置顶开关。
- **自定图标支持**：程序会自动读取同级目录下的 PaperTodo.ico 替换默认托盘图标。

### v0.5 菜单与视觉整理

- **右键菜单优化**：右键菜单重构为四个分类清晰的分组（事项、格式、编辑、当前纸片），移除了传统 Windows 右键菜单左侧的空白图标预留区。
- **精致视觉风格**：重写了删除确认弹窗，采用更窄的纸张风格细滚动条，统一了窗口边框、阴影、圆角和悬停反馈，并消除了 Windows 默认控件的蓝色预选框与焦点框。
- **手绘感复选框**：将待办默认复选框替换为更温润的暖褐色圆角边框与自绘对勾。

### v0.6 待办拖动与粘贴

- **平滑拖拽排序**：待办事项支持鼠标拖动排序，增加了落点指示线。拖拽时，底部追加区会自动转换为垃圾箱，支持拖放至该区域直接删除。
- **智能多行粘贴**：从外部复制多行文本粘贴到待办纸时，能自动剥离 Markdown 复选框、数字列表和项目符号。单次粘贴限制在 200 条以内，防止界面由于超大文本发生卡顿。

### v0.7 笔记纸与 Markdown

- **引入“笔记纸”**：新增笔记类型纸片，支持使用原生组件渲染基础 Markdown。
- **基础格式支持**：支持标题、引用、列表、代码块、超链接等基础排版格式，并支持在编辑与预览状态之间切换。
- **操作回滚兼容**：修复了通过右键菜单或快捷键应用文本格式时，导致文本编辑器撤销/重做（Undo/Redo）历史错乱的问题。

### v0.8 输入体验与基础稳定性

- **中文输入优化**：优化了中文输入法（IME）下的 Enter 回车行为，避免在拼音输入阶段误触并新建空白待办。
- **保存弱提示**：如果遇到本地文件写入失败，将通过系统托盘气泡进行非阻塞提示，避免弹窗打断工作。
- **输入焦点修复**：修复了首行空待办按删除键（Backspace）后输入焦点丢失的问题，改善了多处稳定性。

### v0.9 发布工程整理

- **多进程冲突解决**：修复了当重复启动程序（多实例冲突）拦截退出时，非主进程退出流程不当释放全局互斥锁（Mutex）导致主程序崩溃的问题，提升了运行稳定性。

### v0.10 聚焦式 Markdown 预览

- **自动预览切换**：笔记纸采用“编辑/预览”智能感知逻辑：默认展示 Markdown 预览，点击正文直接进入编辑状态；点击标题栏、空白处或使文本框失焦时，会自动重新渲染并恢复为预览。去除了繁琐的手动切换快捷键。
- **底层层级修复**：修复了在预览态下点击链接等复杂元素时，由于 WPF 树状结构层级查找导致的底层崩溃问题。

### v0.11 主题与系统设置

- **明暗主题切换**：托盘菜单新增“浅色、深色、跟随系统”三档切换。深色模式采用暗炭/深褐色背景配暖乳白文字，保护暗光环境下的视力。
- **自启动管理**：在托盘设置中增加了开机自启动选项，支持安全地读写当前 Windows 用户的注册表启动项。
- **输入光标自适应**：修复了深色模式下输入光标不可见的问题，现在光标会跟随主题改变颜色。

### v0.12 托盘与菜单打磨

- **分段主题选择器**：将深浅色主题选择从传统的二级垂直菜单，改为了更节省空间的横向分段选择器，并去除了默认的悬停蓝色边框。
- **紧凑界面布局**：优化并收紧了右键菜单与托盘菜单的行高、圆角和内边距，减少屏幕空间占用。精简了右键菜单中冗余的系统默认命令。
- **稳定性和易用性优化**：进行了3处稳定性问题修复和动画优化

### v0.13 待办撤销与编辑连续性

- **待办多步撤销**：待办列表引入深达 100 步的全局撤销/重做机制（Ctrl+Z / Ctrl+Y），支持对勾选、清理、新增、删除及拖拽顺序进行回滚。
- **智能按键分流**：按 Ctrl+Z 时会优先执行当前输入框内的局部字符撤销，在文本框无可撤销内容后自动过渡到整个列表的全局撤销。
- **右键定位焦点**：右键点击待办事项时，光标会自动移动至该行，并锁定当前行的高亮背景，保证编辑意图的一致性。
- **稳定性和易用性优化**：进行了3处稳定性问题修复和动画优化

### v0.14 性能与代码减重

- **笔记渲染优化**：合并并缩短了笔记纸高频点击切换状态时的渲染路径，降低内存和 CPU 的即时开销。
- **冗余代码清理**：移除了早期迭代中残留的静态画刷、布局辅助器和冗余样式代码，保持程序轻量。

### v0.15 数据保护与保存安全

- **异步快照保存**：数据写入时，先在主线程获取一份稳定快照，然后由后台线程进行串行异步 I/O 写入，避免保存时引起界面卡顿。
- **防空白覆盖**：如果检测到本地配置与备份均已意外损坏，程序将拒绝用空白状态覆盖旧数据，最大程度留存残余文件。
- **崩溃紧急救援**：注册了全局未处理异常崩溃兜底，在程序意外崩溃退出前，会最后尝试将内存中的数据安全写出至 data.crash_recovery.json，与主配置文件隔离，便于人工找回。

### v0.16 单实例唤醒与系统隔离

- **管道单例唤醒**：支持单实例模式。当程序已经在后台运行时，双击新运行的实例会通知主实例，将所有隐藏纸片唤醒、恢复置顶并获取焦点，便于外部快捷键工具或快捷方式的快速调用。
- **稳定性和易用性优化**：进行了6处稳定性问题修复和动画优化

### v1.0.0 正式版发行

- **新版图标上线**：更新并合成了全新的精致应用图标 `PaperTodo.ico`，优化高低分辨率及深浅色模式下的系统托盘与任务栏图标表现。
- **稳定性和易用性优化**：进行了3处稳定性问题修复和动画优化

### v1.1 胶囊模式

- **引入胶囊状态**：允许将纸片折叠为极简胶囊形态，大幅减少桌面空间占用。
- **平滑过渡动画**：设计了双轴过渡动画，解决动画期间变形的问题。
- **稳定性和易用性优化**：进行了7处稳定性问题修复、易用性优化和动画优化

### v1.2 多语言支持

- **新增多国语言**：可见文本提取至资源文件，消除了硬编码，支持中文、英文、日文、韩文四种界面语言，可根据系统语言自动适配。

### v1.3 参数支持与托盘布局优化

- **启动参数扩展**：新增支持命令行启动参数（--show 显全部、--hide 隐全部、--toggle 切换、--new-todo 新待办、--new-note 新笔记、--exit 退出），方便配合外部快捷键工具使用。
- **智能启动加载**：恢复纸片时若数据未发生变化，不再无条件重写磁盘上的 data.json，减少磁盘读写。
- **稳定性和易用性优化**：进行了6处稳定性问题修复、易用性优化和动画优化


### v1.4 视觉与过渡优化

- **动画打磨**：对部分基础界面切换、折叠过渡动画的响应细节进行了优化。

### v1.5 笔记编辑器重构与“胶囊自动贴边”

- **高性能编辑器切换**：将笔记纸的编辑与浏览态合并至同一个高性能 Markdown 文本控件中，解决了之前频繁切换导致的内容跳动与输入法焦点丢失。
- **胶囊自动贴边**：新增“胶囊自动贴边”功能。启用后，折叠的胶囊会按顺序靠在屏幕右上角。常态下胶囊会半隐藏至屏幕边缘，鼠标悬停时自动向内侧滑出，最大化利用桌面空间。
- **稳定性和易用性优化**：进行了2处易用性优化

### v1.6 Markdown 双模式与胶囊动画优化

- **Markdown 双模式**：笔记纸保留编辑态 / 浏览态双模式，编辑时专注源文本输入，浏览时按当前 MD 解析模式呈现轻量 Markdown 视觉，减少旧预览切换带来的跳动和状态同步成本。
- **新增 MD 解析模式**：托盘新增“不启用 / 启用 / 增强”三档，默认增强；不启用时不做额外 Markdown 解析，启用时保留基础样式，增强时启用浏览态标记淡化和列表圆点覆盖。
- **稳定性和易用性优化**：进行了6处稳定性问题修复、易用性优化和动画优化

### v2.0rc 测试版

**笔记与 Markdown**

- **精细化的 Markdown 元素渲染**：增强显示分割线和列表标记，淡化已确认属于 Markdown 结构。
- **部分html语法渲染**：html中和md渲染相似的简单语法支持渲染。
- **外部 Markdown 编辑与自定义后缀**：笔记纸顶栏新增 MD 按钮，支持直接调用系统默认编辑器打开临时 Markdown 文件。默认使用的编辑器在设置中自定义。可支持html，bat等代码一键启动。
- **外部打开按钮同步后缀**：设置中修改笔记外部打开后缀后，标题栏按钮会同步显示对应后缀；后缀超过两个字母时只显示前两个字母。
- **正文缩放与无缝刷新**：笔记纸支持使用 Ctrl + 鼠标滚轮 按 10% 步长调整正文文字大小。非 100% 缩放状态下，右下角会以半透明形式显示当前缩放比例，点击即可一键复位。
- **大段内容粘贴保护**：修复笔记纸粘贴超长 HTML / 代码内容时可能异常退出的问题；超出安全范围的内容会在粘贴前自动裁切。

**配色与视觉**

- **四套全新配色方案**：设置中新增独立「配色方案」功能，提供 **暖纸**（暖白与陶土）、**墨**（暗黑与墨蓝）、**林**（森绿）、**霞**（玫瑰）四套专属色彩风格。包含浅色和深色两份预设。
- **配色体系底层重构**：规范了底层色表设计。每套配色方案仅定义少量基色，半透明叠加色均基于基准色派生。链接色会自动融入当前主题色彩，消除视觉上的冷色突兀感。
- **全局文字对比度优化**：优化了已完成项、次要提示文本的弱化逻辑。通过调整明度而非偏色来降低饱和度，改善了此前在浅色背景下文字略显浑浊的问题。
- **规范化的圆角与阴影质感**：**圆角统一**：将纸片、子控件、菜单、复选框等圆角收敛统一处理。

**胶囊**

- **胶囊自适应宽度**：折叠后的胶囊会根据纸片的标题长度自适应。
- **一键收纳「主胶囊」**：新增顶部「收起全部」主胶囊。点击可一键收拢或展开当前所有的贴边胶囊。
- **贴边胶囊拖拽重排**：支持在屏幕右侧停靠区上下拖动任意一个贴边胶囊，实时调整它们在侧边的排列顺序。
- **展开后的前台标签占位**：从贴边状态点击展开纸片后，依旧保留边缘胶囊显示。

**标题与设置**

- **自定义纸片标题**：点击纸片顶部默认标题文字可进行编辑。
- **独立分类设置**：托盘右键菜单中新增了独立的「设置」窗口。
- **悬浮说明ⓘ**：每个设置项的末尾增加 ⓘ 提示图标。鼠标悬停即可查看该功能的具体作用、生效条件与开启效果，降低初次配置的门槛。
- **新增「显示悬停提示」开关**：支持关闭按钮和操作区的悬浮提示，仅在设置页面的 ⓘ 图标上保留扩展说明。
- **高度可定制的顶栏按钮**：允许用户在设置中选择单独关闭「新建待办」、「新建笔记」、「外部打开（MD）」按钮中的任意一项，精简不常用的顶栏操作区。
- **待办与笔记的交互关联**：支持通过拖拽建立关联。将一张笔记纸拖放到待办项上，即可自动将该待办项与笔记绑定。点击可呼出关联的笔记。

**其他**

- **全屏避让兼容**：优化了置顶和贴边胶囊的层级规则。当检测到当前前台运行了完全全屏的窗口（如全屏游戏、视频播放或全屏演示 PPT）时，置顶纸片和侧边胶囊会自动降低层级并避让；视频或演示进入全屏后也不再需要额外点击一次画面。
- **多维度的外部集成指令**：命令行和快捷键启动参数支持进一步扩展（支持包括 --show、--hide、--toggle、--new-todo、--new-note、--exit 等），方便使用第三方效率工具进行快捷调用。
- **稳定性和易用性优化**：进行了31处稳定性问题修复、易用性优化和动画优化

### v2.0 正式版

**新功能**

- **全屏置顶策略**：设置中新增「避让 / 不避让」两档，默认避让外部全屏窗口，并改善浏览器视频全屏播放时的自动识别，减少进入全屏后仍需点击画面的情况。
- **待办视觉大小**：设置中新增小 / 中 / 大 / 特大四档待办显示大小。

**胶囊相关改动**

- **贴边胶囊多屏 / 左右侧（多队列）**：每条屏幕边各有一条独立的贴边胶囊队列，直接拖动**单个**贴边胶囊即可把它移到屏幕左侧 / 右侧或其他显示器——松手时自动吸附到光标所在显示器的最近边并加入那条队列。
- **贴边胶囊起始高度与位置**：拖动「收起全部」主胶囊可在所属队列内上下滑动调整起始高度。
- 多屏贴边显示修复：贴边胶囊改为在窗口内部裁剪尾部，避免双屏相邻边界上尾部露到另一块屏幕。
- 贴边标签层级修复：修复贴边胶囊偶尔掉出置顶层级，被其他应用窗口遮住的问题。

**bug修复和边界修正**

- **设置窗口排版优化**：设置窗口改为横向分栏布局，减少选项拥挤和纵向滚动。
- **标题编辑稳定性**：修复修改纸片标题时可能触发系统错误弹窗的问题。
- 配置兼容性修复：修复旧版本（含早期多屏实验）写入的配置文件因含有已废弃字段导致启动崩溃的问题，现在会忽略无法识别的字段并正常加载。
- 新建纸片位置修复：从贴边纸片中新建纸片时，新纸片会继承同一贴边队列并避开边缘胶囊栏，避免压到侧边栏。
- 隐藏全部残留修复：修复从托盘「隐藏全部」时，处于停靠状态的折叠胶囊其边缘药丸窗口可能残留在屏幕上的问题。
- 动画开关完善：关闭动画后，纸片与胶囊之间的折叠 / 展开过渡也会立即完成。
- 纸片创建位置保护：新建纸片会避开任务栏工作区，避免在屏幕边缘创建后无法点击。
- 纸片数量保护：纸片总数限制为 100 张，达到上限时使用应用内提示提醒清理。
- 待办撤销视觉优化：撤销 / 重做后不再闪烁黄色高亮。
- 托盘菜单滚动：纸片过多时，托盘右键菜单可滚动查看，避免退出入口被挤出屏幕。
- 托盘菜单点击修复：修复托盘右键菜单在首次启动时偶尔第一次点击无效的问题。
- 其他可能带来的问题的稳定性修复
- 修复旧配置中主屏贴边胶囊可能被拆成两条队列，导致边缘胶囊和「收起全部」位置异常的问题。
- 修复侧边胶囊右键菜单可能被侧边胶囊遮住的问题。
- 修复上下拖拽侧边胶囊时鼠标指针显示为调整大小样式的问题。
- 修正发布包内容，云端 Release 同时提供自包含压缩版和 no-runtime 版，本地打包保持 no-runtime 单文件。

### v2.1rc1

**重点更新**

- **全面调整多语言文案**：增加可读性

- **增加默认字体**：统一观感设计

- **新增字体选择**

  可在语言默认、雅黑和等线之间切换、

  会自动加载同目录下 `papertodo.ttf` / `papertodo.otf` 自定义字体显示。

  设置自定义字体后，会统一界面、托盘、胶囊、待办和笔记正文的主文字观感。

  推荐中文使用MiSans
-   新增「**从窗口切换中隐藏纸片**」设置，可让展开的纸片从 Alt+Tab 和任务视图中隐藏。

**脚本胶囊**

- **脚本胶囊**：笔记第一行写入 `!p` / `!power` 后，折叠胶囊会显示闪电图标，左键运行后续 PowerShell 脚本，右键可打开纸片继续编辑；粘贴带统一缩进的脚本时会自动整理缩进。
- 「关联脚本点击运行」设置：待办项关联到脚本胶囊时，可让关联入口左键直接运行脚本，右键打开脚本胶囊编辑。
- PowerShell 运行设置：可优先使用 PowerShell 7、隐藏运行窗口，并可开启 `!pf` / `!powerf` 复用常驻 PowerShell 进程。

**笔记与 Markdown**

- 笔记 Markdown 列表编辑增强：按 Enter 可延续项目符号、数字列表和任务列表，空列表项再次回车会退出列表。

**优化和修复**

- 新增「关联笔记显示长标题」设置；关闭时保持原有紧凑显示，开启后关联笔记标题可按内容扩展显示。
- 优化托盘纸片列表的显示状态对齐，强化纸片类型图标显示，并让托盘脚本胶囊、待办关联脚本入口显示闪电图标。
- 设置扩展说明换行增加可读性
- 修复屏保或系统恢复后，非置顶纸片可能残留在实际置顶层级的问题。
- 修复点击胶囊后，下一次托盘右键菜单可能没有响应的问题。
- 修复退出过程中托盘图标短暂残留时，再次点击可能触发已关闭纸片窗口并导致崩溃的问题。
- 修复窗口化全屏切换到其他窗口时纸片闪烁的问题，非置顶纸片不再参与全屏避让层级调整。
- 修复部分状态切换后，边缘胶囊的激活描边可能残留的问题。
- 修复设置窗口关闭按钮悬停背景没有圆角的问题。

### v2.1rc2

**新功能**

- 新增“隐藏纸片任务栏图标”设置，默认开启；需要把展开纸片显示到 Windows 任务栏时，可在设置中关闭。
- 新增“完成后自动清除待办”设置；开启后，勾选完成的待办会立即从当前纸片移除，并可通过撤销恢复。

**胶囊相关改动**

- 新增“记住边缘胶囊展开位置”设置，默认开启；从边缘胶囊再次打开纸片时，会恢复上次展开后的窗口位置和大小，位置不可用时仍按边缘展开。

**笔记体验优化**

- 调整笔记文字渲染策略，降低对 AvalonEdit 文本渲染参数的接管，改善 MiSans 等字体在笔记中的显示清晰度。

**bug修复和边界修正**

- 修复设置窗口右上角关闭按钮缺少圆角预选框的问题，并减少按下 Alt 时出现传统虚线焦点框的情况。
- 修复显示关联笔记名的待办在输入到一行 / 两行临界点时，可能出现行高快速闪烁的问题。
- 优化取消关联笔记和多行粘贴时的界面响应，减少偶发卡顿。
- 修复了“从窗口切换中隐藏纸片”导致的跳转错误，把 `WS_EX_TOOLWINDOW` 换成了 hidden owner 技巧。
- 修复多行文本粘贴到待办时首行可能没有保留的问题。
- 修复笔记纸标题进入编辑后再次点击只能全选的问题，现在可以在标题中正常定位光标继续修改。

### v2.1 正式版

**胶囊相关改动**

- 修复贴边胶囊在标题变化后关闭区可能被挤出或拉入的问题，并收紧关闭区尺寸和间距，减少短标题贴边胶囊的多余留白。

**bug修复和边界修正**

- 修复笔记纸标题进入编辑后，再次点击标题输入框仍可能无法正常定位光标的问题。
- 优化“从窗口切换中隐藏纸片”和“隐藏纸片任务栏图标”的联动逻辑：从窗口切换中隐藏纸片时会同步隐藏任务栏图标，并在设置页锁定相关开关；关闭该选项后，启动和切换设置时不再残留窗口切换隐藏或任务栏显示状态。

### v2.1.1

**胶囊相关改动**

- 新安装或未配置过的状态下默认显示主胶囊，方便一键收起或展开边缘胶囊队列。
- 修复已展开的边缘胶囊在程序重启或重新显示后，可能没有恢复到上次展开位置的问题。
- 修复调整 Windows 显示比例或显示器参数后，贴边胶囊可能没有重新贴齐屏幕边缘的问题。

**bug修复和边界修正**

- 修复隐藏纸片任务栏图标时，纸片可能同时从窗口切换视图中消失或重新打开贴边胶囊后任务栏图标状态异常的问题。
- 修复跟随系统深浅色切换时，右键菜单和设置窗口可能停留在旧主题颜色的问题。
- 修复长文本待办完成后，删除线可能只横穿整块文本中间的问题，现在会按每行文字分别显示删除线。

### v2.1.2

**视觉优化**

- 放大纸片标题文字、左上角纸片类型图标和胶囊文字，换用更清爽且更醒目的待办纸图标，并收紧标题区域上下留白，让标题区显示更舒展。
- 优化纸片标题栏点击和拖动行为，减少单击时光标短暂切换、后方纸片图标闪烁以及连续点击后拖动失效的情况。
- 从当前纸片新建待办或笔记时，位置更稳定地贴近当前可见纸片。
- 主胶囊收起后显示数量时不再因短数字缩窄，并在两位数以上时略微增加可见宽度，减少文字被屏幕边缘裁掉的情况，同时补全英文数量显示的单位。
- 修复编辑纸片标题后，点击标题栏其他位置可能不会退出标题编辑的问题。
- 修复英文、日文、韩文界面下，新建待办纸和笔记纸的默认标题仍显示中文的问题。

### v2.2

**视觉和交互优化**

- 统一纸片左上角的窗口置顶按钮为**图钉图标**：未置顶时显示为空心图钉，置顶时显示为实心图钉，让置顶状态更加清晰直观。
- 优化**待办项关联笔记**的点击体验：如果关联的笔记已经打开在桌面上，再次点击待办里的笔记小图标时，会直接将该笔记折叠收回到侧边，或者通过闪烁来提醒其位置；同时，该笔记打开时，小图标会同步亮起提示。
- 新增侧边胶囊拖动排序时的**平滑滑开动画**，让胶囊上下调整顺序时的过渡更丝滑，不再生硬闪现。
- 完美适配 **Windows 系统分屏贴靠**（拖拽窗口到屏幕边缘分屏）：分屏贴靠时会自动隐藏纸片的阴影和边距，使其完全铺满分屏区域；且纸片折叠成胶囊后再展开、或者隐藏后重新显示，都会精准回到之前分屏占用的格子中。
- 优化贴边胶囊跨屏拖拽：跨屏拖拽更加自由。

**bug修复和边界修正**

- 修复编辑后0.5秒内关闭窗口时极小概率丢失操作。
- 优化多屏幕混合缩放环境下纸片收回为胶囊时的动画起点稳定性，避免收回动画开始前出现位置跳动。
- 修复开启“已关联笔记不显示为胶囊”后，删除或清除关联待办项时，笔记胶囊可能不会立即恢复显示的问题。
- 修复贴边胶囊跨屏瞬间的闪烁问题。
- 修复启用主胶囊时，断开副屏后胶囊回落到主屏同侧可能出现两个主胶囊的问题。

### v3.0 正式版

**笔记图片**

- **笔记支持从剪贴板、拖入或菜单插入图片**，按纸片宽度完整显示；删除对应 Markdown 引用即可去掉。
- 新插入图片默认使用 `![image|100%](i:001)` 短编号语法，支持 `![image|宽x高, 75%](...)` 或 `{width=75%}` 调整显示大小。
- 图片保存在本地资产文件`note-assets.lmdb`中；单张损坏只影响该图；删除或剪切后仍可撤销；外部打开笔记时会安全导出临时文件。
- 新增默认开启**「自动压缩过大图片」**：超过 8 MB 或长边超过 2048 时尝试压缩；失败或仍超限则取消导入；宽或高超过 4096 直接拒绝。关闭后保留原图，仍超 8 MB 则拒绝。

**快捷键**

- 快捷键页支持为**显示全部、隐藏全部、切换显隐、新建待办、新建笔记、退出**录入全局快捷键。
- 新增默认关闭的**「快速启动侧边胶囊」**：
- 左侧队列默认 `Ctrl+Shift`+`1-9`、右侧队列默认 `Ctrl+Alt` +`1-9`，开启后按下快捷键可快速打开边缘胶囊，如当前队列不存在则自动寻找本屏幕，跨屏幕胶囊。
- 队列快捷键下新增默认开启的**「快捷键打开到鼠标位置」**：开启后，队列快捷键展开纸片时放到当前鼠标位置；关闭时仍按贴边或记住的展开位置打开。
- **快捷键新建待办 / 笔记**会把纸片放到当前鼠标位置（居中并夹在工作区内），不再固定到屏幕左上角。
- 胶囊模式下，按下 **Esc**可快速收回胶囊。

**胶囊**

- **重构整体胶囊系统**

  降低多屏以及拖拽中出现的视觉bug，增强稳定性

  全面补全动画，提升美观度

  优化整体视觉美观性

  增加边缘胶囊会平滑吸到边缘

- **边缘胶囊和主胶囊**按真实宽度贴在屏幕边缘，只向屏幕内侧伸缩；关闭区固定在墙边，拖出队列后变为完整悬浮胶囊。

- **快速拖出或跨显示器拖动**时，胶囊在不同缩放比例下保持完整、紧跟鼠标且尺寸稳定；交接或拔掉副屏后也能稳定回到终态。

- 新增默认关闭**「悬停时隐藏关闭按钮」**边缘胶囊悬停时不显示关闭按钮；胶囊仍正常外伸，延长区域作为普通胶囊区域使用。

- 拖动边缘胶囊排序或跨队列移动时，当前胶囊始终保持在最上方不被遮挡。

- 边缘胶囊伸缩不再露出缝隙或因窗口移动反复闪烁。

- 修复鼠标从边缘胶囊内侧圆角斜向移出后，胶囊保持外伸、关闭区不收回的问题。

- 边缘胶囊打开右键菜单后保持外移，不会因鼠标进入菜单而缩回

- 可限制边缘胶囊标题最多显示前 N 个字符，默认“全部”兼容原有宽度

- 普通胶囊会在字体变化及形态过渡收尾时重新校正宽度，贴近右侧工作区时保持右边缘不动。

- 多个边缘胶囊同时让位、排序或收向主胶囊时更顺畅，队列较长时更不易卡顿。

**设置页**

- 设置窗口拆分为**「基础行为 / 视觉 / 快捷键」三页**，隐藏部分选项到高级模式
- 设置页 ⓘ 扩展说明重写：更短、更易读。

**视觉**

- 可在 **80%–120%** 间改变整体字体大小
- 可设置笔记正文、待办正文、标题和胶囊文字的**[小中大字号]与[加粗]**。
- 文字渲染提供**「标准 / 柔和 / 锐利」**三档，可按观感选择。
- 视觉页新增**图片标记显示方式**，可选择“始终显示 / 仅编辑时 / 始终隐藏”；隐藏只影响界面，不会删除或修改 Markdown 原文。
- **「自定义字体增强加粗」**：程序目录同时有 `papertodo` 正文字体与粗体文件（如 `PaperTodo_Bold.ttf` / `papertodo_bold.ttf`）时可勾选；开启后，笔记、待办、标题和胶囊中已勾选加粗的文字会改用该粗体文件显示。
- 单个笔记原有的百分比缩放会在全局与正文字号基础上叠加

**托盘视觉**

- 托盘纸片列表优化为**更现代的工具栏**：
- 版本号右侧齿轮加入设置页面
- 纸片区左侧眼睛可切换全部纸片显隐
- 纸片区右侧两个小图标可快速新建待办纸和笔记纸。

**启动与外部集成**

  该参数只在首次启动决定界面 UI 语言，不会写入配置，亦不会影响已在运行的实例。
  示例
  `--language=zh-CN`

  `--language=zh`

  `--lang=en`

**重要bug修复和优化**

- 多显示器下，外部程序全屏时只避让其所在显示器；其他显示器上的置顶纸片和胶囊保持正常置顶。
- 待办新增、粘贴或完成动画结束后，拖动时原位置会正确淡出，不再与拖动预览重叠。
- 滚动条会跟随当前配色；横向滚动条的方向、滑块外形和轨道点击行为恢复正常。
- 优化 **Windows 分屏贴靠**显示：贴靠时自动隐藏纸片阴影和边距，识别三列中间格、2/3 主列等 Windows 11 布局。
- 优化 Windows 分屏贴靠功能，正常恢复纸片大小。
- 开启“从窗口切换中隐藏纸片”后，**激活纸片不再把其他未置顶纸片一并带到前台**。
- 通过启动参数、托盘或首次启动新建的纸片，会创建在**鼠标所在显示器**并加入对应边缘队列。
- 优化连续新建多张纸片时的默认位置。
- 优化**冷启动和多纸片恢复**速度，更快显示纸片。
- 优化**长笔记连续输入**、Markdown 行渲染和自动保存开销；普通文字编辑不再反复复制全文、重扫整篇图片引用或重复整理已加载的数据。
- 边缘胶囊右键菜单稳定显示在置顶窗口之上，不再偶发被遮挡
- 首次右键托盘可以正常打开菜单
- 修复从待办打开已折叠的关联笔记时，胶囊出现视觉闪现的情况。
- 修复纸片可过度缩放对内容进行裁切的问题。
- 超过笔记输入范围的截断时会进行提示。

- 退出时更可靠地结束本次启动的脚本进程，不会因为各种问题导致退出失败。

**其他bug修复和优化**

- 拖动排序期间隐藏纸片、关闭胶囊模式或在悬浮交接时松开鼠标，都能正常结束排序并刷新队列。
- 拖动排序期间发生的队列重排和标题宽度刷新会在拖动结束后统一应用；
- 伸缩中的标题变化会更加平滑，不再丢失刷新或硬切关闭区。
- 修复关联笔记改绑、删除空待办、Undo/Redo 后胶囊显隐未及时更新的问题；
- 多行粘贴待办将被视为一次粘贴行为。
- 修复应用启动恢复耗时较长时，其他启动参数可能静默丢失的问题
- 修复应用刚启动就收到退出时，可能因主胶囊还在定位而崩溃的问题。
- 修复小概率的未成功保存也会提示已保存的问题

- 数据文件缺少核心纸片列表或该列表为空时，不再被当作首次启动的空数据；会尝试读取备份，主文件和备份都读不了则停止启动，避免默认纸片覆盖原文件。

- 纸片标题编辑上限改为 20 个字符。设置里标题最大显示长度可在 2–20 之间调节。

- 隐藏单张或全部纸片时保留原有展开/胶囊形态；只关闭贴边模式时，普通胶囊会从原队列位置接续显示，不再跳回旧位置或其他显示器。

- 关闭胶囊模式时，已折叠纸片正常展开为纸片形态

- 修复 150% / 200% 等高 DPI 缩放下普通胶囊、边缘胶囊和关联笔记拖拽过早触发、轻微手抖可能吞掉点击的问题。

### v3.0.1

**bug修复和边界修正**

- 修复启用动画时，最新一条待办保持透明、必须继续新增后才显示的问题。

### v3.1

**视觉和交互优化**

- 双击待办文字可选中整条内容，便于快速复制或整段替换。
- 主胶囊支持右键打开与任务栏托盘图标相同的功能菜单。
- 微软雅黑 / 等线预设改为所选字体优先（数字与英文也尽量同一套字形），Segoe UI 仅作缺字回退，观感更接近自定义字体。
- 系统默认下：标题 / 胶囊等界面文字优先 Microsoft YaHei UI；仅笔记与待办正文保持 Segoe 优先。

**图片资源**

- 笔记图片支持 **WebP**（拖入、粘贴与选图）；系统不支持时会提示失败。
- 修复从资源管理器拖入或通过右键菜单选择图片时，无法插入的问题。
- 允许从资源管理器复制本地图片文件后，直接粘贴到笔记中。
- 程序启动时会自动回收已清理图片的空编号，后续插入的图片优先使用这些编号；已有引用与备份中仍出现的编号不会被复用，已有图片编号保持不变。

**数据保存**

- 空闲约 1 秒后自动保存；持续编辑时最长约每 10 秒强制保存一次，减少长时间打字时的保存延迟。
- 自动保存失败且没有更新的保存请求时，约 10 秒后会再试一次，避免写盘失败后就不再尝试保存。
- 崩溃时只写 `PaperTodo.crash.log`，不再生成 `data.crash_recovery.json`。
- 数据安全依赖自动保存与 `data.json` / `data.backup.json`。

**bug修复和优化**

- **修复边缘胶囊拖拽判定：在管理员权限应用位于前台时可正常跨边缘、跨屏和排序。**
- 修复混合 DPI 边缘胶囊偶发拖拽起拖锚点偏移问题，避免跨缩放屏时抓住瞬间错位。
- 修复修改纸片标题后，展开纸片会被错误移动到贴边胶囊附近的问题。
- 修复因部分截图软件对剪贴板非标准操作，导致粘贴到笔记后整图透明的问题。
- 修复拖入或粘贴非图片文件时错误触发图片错误提示的问题。
- 若 .NET 运行环境组件加载失败，程序完全退出并写入崩溃日志，提示手动重新打开。
- 崩溃处理中异常分类失败时，仍会继续写崩溃日志，避免路径形态的程序集名导致二次崩溃后静默退出。

### v3.2

**优化**

- **缩放优化**
  纸片调整大小方式可在「视觉」中选三档（默认柔和）：

  标准为右下角点阵正常不透明

  柔和为右下角点阵约 50% 透明

  隐藏不显示点阵，可拖动纸片四边或四角缩放

- **优化打开速度，将纸片重建预热操作后台队列处理，提升启动速度**

- **优化退出，降低菜单视觉残留**

- **笔记图片缓存**：同一张笔记图片只保留一份解码缓存，最多约 20 张或约 50 MB 。

- **优化图片显示清晰度**，按DPI 和实际显示宽度精确解码。

- **窄纸片顶栏自适应**：纸片支持更小的最小宽度，并自动折叠顶栏按钮。

- 顶栏关联状态与对齐优化：笔记未关联待办时显示 `⌖`，已被待办关联时显示 `⦿` ；同时顶栏按钮视觉对齐。

- 编辑器依赖更新：AvalonEdit 更新至 6.3.1.120。

**bug修复和边界修正**

- **纸片右键菜单优化**：新增快速打开设置，笔记直接用外部 MD 编辑器打开，删除操作使用危险色提示，增加右键菜单动态读取展开折叠。

- **修复隐藏的胶囊重新打开后仍会显示的问题**，修复启动时忽略已隐藏纸片/胶囊的 `isVisible` 配置。

- 纸片不再响应 Windows 最小化命令：普通状态下按 Win+↓ 不会最小化；从最大化恢复到普通窗口及最大化操作仍保持系统默认行为。

- 修复隐藏单张纸片时，其他贴边标签可能连续闪动并重复向上挪移的问题；标签现在会一次移动到最终位置。

- 修复同一软件同时存在全屏与普通窗口时，切到普通窗口后纸片或胶囊可能反复闪烁的问题；切到桌面、任务栏或其他窗口会及时解除避让，同时保留浏览器视频和演示进入全屏时的自动识别。

- 修复从笔记右键菜单选择图片后，编辑器偶尔提前切回浏览态，导致图片没有插入且无错误提示的问题。

### v3.2.1

**bug修复和边界修正**

- 优化全屏避让：取消边缘胶囊的高频置顶保护，改为前台窗口事件触发、1 秒轻量检查和 5 秒全局兜底；关闭避让后不再保留相关监听与定时检查。
- 修复程序重启后，直接从未展开的贴边胶囊右键删除非空纸片时，删除确认框可能导致异常退出的问题。
- 修复笔记进入并退出编辑状态后，正文右键菜单可能恢复旧标题或旧操作状态的问题。

### v3.3

**待办与快启**

- **文件/文件夹快启**：待办项支持直接拖入本地文件或文件夹作为快启路径，可直接打开、打开所在位置或解除关联，路径失效时会有明显提示；快启路径与纸片关联互斥。
- **关联任意纸片**：待办关联由仅支持笔记扩展为支持关联任意纸片（待办纸或笔记纸），无缝兼容已有笔记关联数据。
- **已完成自动置底**：待办新增“已完成自动置底”选项，勾选完成后自动移到已完成区域末尾，取消完成时移回未完成区域末尾（开启自动清除时暂不触发）。

**Markdown 与笔记渲染**

- **WYSIWYG Markdown 编辑**：完整渲染模式提供块级 Markdown 渲染与编辑并存，正文保持已渲染形态，不再切换到独立源码视图。
- **Markdown 解析与展示一致性**：标题、引用、列表、代码块、链接、基础 HTML、转义与图片/代码边界现在统一使用同一份 Markdown 语义，减少复杂嵌套内容中的不一致。
- **Markdown 行内语法增强**：支持粗斜体（`***text***` / `___text___`）、粗/斜/删除线/链接的组合，以及反斜杠转义。
- **Markdown 裸链接识别**：正文中的纯文本 `http://` / `https://` 网址可直接点击打开；行内代码和代码块中的示例网址保持纯文本，句末标点、括号与成对强调标记不会混入实际网址。
- **含图笔记渲染性能优化**：修复含图片笔记在前台静止时，视口细微变化可能触发连续重绘导致 GPU 占用偏高的问题。
- **大图导入内存优化**：导入和压缩大图时直接按目标尺寸解码，不再提前完整解压原始像素，显著降低瞬时内存峰值。

**胶囊与视觉**

- **贴边胶囊间距调节**：视觉设置中新增贴边胶囊间距选项（0 / 4 / 8 DIP），默认保持为 4 DIP。
- **阴影层次优化**：重新调整了展开纸片、普通胶囊、拖出的贴边胶囊以及主/贴边胶囊的阴影层次，使不同形态更易区分。

**设置与系统体验**

- **多语言界面选择**：设置中新增语言切换选项（跟随系统、简体中文、English、日本語、한국어），保存后重启生效，修复语言切换无效。
- **小键盘数字键选项**：全局快捷键支持设置是否区分主键盘数字键与小键盘数字键（默认混合响应）。

**bug修复**

- **提升旧版 Windows 系统的启动兼容性**，避免部分特殊环境下启动异常。
- 修复通过全局快捷键、托盘“显示全部”或显隐切换唤醒纸片时，开启“记住展开位置”的纸片可能未回到上次展开位置的问题
- 优化多纸片批量恢复，减少重复重排并同步刷新关联状态。
- 修复调整 Windows 显示缩放、DPI 或跨不同显示器后，笔记图片未能及时重新排版并按新 DPI 重新解码的问题。
- 减少笔记预览与 Markdown 渲染模式下的重复刷新。
- 修复笔记编辑状态下首次打开纸片右键菜单时可能闪现后立即关闭的问题。
- 关闭胶囊或贴边胶囊模式时保留“显示主胶囊”的用户偏好，重新开启后自动沿用。
- 修复贴边胶囊右键菜单打开二级菜单后，点击子项可能被外部点击保护提前关闭的问题。
- 优化全屏避让体验，修复无边框全屏程序运行时，隐藏任务栏或窗口切换入口的纸片仍可能压在全屏画面上方的问题。

- 修复运行中切换 Windows 显示缩放或跨不同 DPI 显示器后，托盘右键菜单可能偏离鼠标位置或首次打开异常关闭的问题。

- 仅带有纸片关联或快启路径的空文字待办项会被妥善保留，不再被空项清理或按 Backspace 键静默删除。
- 恢复待办右键菜单的键盘入口，支持通过菜单键或 `Shift+F10` 唤出。
- 减少待办右键菜单的重复创建，降低无意义的界面开销。

### v3.31

**Bug fixes and improvements**

- **Reset shortcut settings correctly**: “Restore defaults for this page” now turns off “Distinguish numpad digits” as expected, so the number row and numpad continue to trigger the same shortcuts.
- **Preserve heading typography in Markdown**: Italic, strikethrough, and underlined text inside headings now retain the heading's bold weight and custom bold font, rather than only its font size.
- **Clearer advanced settings**: Advanced options now use subtly tinted backgrounds and grouped borders to make them easier to distinguish from common settings.
- **Fix repeated refreshes in notes with images**: Notes containing images no longer repeatedly refresh while idle in the foreground, avoiding sustained high CPU and GPU usage.
- **Faster resizing for notes with images**: Reuse displayed image content and reduce repeated refreshes and processing while resizing a paper.
- **Custom bold fonts in Markdown**: With enhanced bold enabled, Markdown headings, bold text, and `<b>` / `<strong>` elements correctly use the custom bold font.
- Fixed completed items losing their position at the bottom when new todos were added using the bottom ＋ button, Enter, or multi-line paste with “Move completed items to bottom” enabled.
- Fixed key recording with some input methods incorrectly recognizing keys and preventing global shortcuts from being saved or triggered.

---

### v3.3

**Todos and quick launch**

- **File and folder quick launch**: Drop a local file or folder onto a todo to link its path. Open it, reveal its location, or unlink it from the todo; invalid paths are clearly marked. A todo can link to either a path or a paper.
- **Link any paper**: Todos can now link to another todo paper as well as a note paper. Existing note links remain compatible.
- **Move completed items to bottom**: When enabled, checking an item moves it to the end of the completed section; unchecking it moves to the end of the active section. This behavior is suspended while automatic clearing is enabled.

**Notes and Markdown**

- **Clickable plain URLs**: Plain `http://` and `https://` URLs in note text can be opened directly. URLs in inline code and code blocks remain plain text, and trailing punctuation, brackets, and paired emphasis markers are excluded from the link.
- **Better rendering performance for notes with images**: Fixed small viewport changes causing continuous redraws and high GPU usage while a note was idle in the foreground.
- **Lower memory use when importing large images**: Images are decoded directly at the target size during import and compression, avoiding a full-resolution decode and reducing peak memory use.

**Capsules and appearance**

- **Adjustable edge capsule spacing**: Choose 0, 4, or 8 DIP in Appearance settings; the default remains 4 DIP.
- **Clearer shadows**: Refined shadows for expanded papers, floating capsules, capsules dragged out of a queue, and master/edge capsules to make their states easier to distinguish.

**Settings and system integration**

- **Interface language selection**: Choose System default, 简体中文, English, 日本語, or 한국어 in Settings. Changes take effect after saving and restarting. Fixed language changes not taking effect.
- **Numpad digit option**: Global shortcuts can distinguish numpad digits from the number row; by default, both trigger the same shortcut.

**Bug fixes and other changes**

- **Improved startup compatibility with older Windows versions**, avoiding startup failures in some environments.
- Fixed papers with “Remember expanded position” enabled sometimes opening in the wrong location when restored through global shortcuts, the tray's “Show all,” or visibility toggles.
- Improved batch restoration of multiple papers, reducing repeated layout work and refreshing link states together.
- Fixed note images not promptly updating their layout or decoding at the new DPI after changing Windows display scaling or moving between monitors.
- Reduced repeated refreshes in note preview and Markdown rendering modes.
- Fixed a paper's context menu sometimes flashing and immediately closing when first opened during note editing.
- Turning off capsule or edge capsule mode now preserves the “Show master capsule” preference for the next time the mode is enabled.
- Fixed edge capsule submenu items sometimes being closed prematurely by outside-click handling.
- Improved fullscreen avoidance: papers hidden from the taskbar or window switcher no longer remain above some borderless fullscreen apps.
- Fixed tray context menus appearing away from the pointer or closing on first use after changing display scaling or moving between monitors with different DPI.
- Added anonymous usage statistics to help improve future versions.
- Empty-text todos with a paper link or quick-launch path are retained, rather than silently removed by empty-item cleanup or Backspace.
- Restored keyboard access to todo context menus using the Menu key or `Shift+F10`.
- Reduced unnecessary recreation of todo context menus.

---

### v3.2.1

**Bug fixes and edge cases**

- Improved fullscreen avoidance: replaced frequent edge capsule topmost enforcement with foreground-window events, a lightweight 1-second check, and a 5-second fallback check. Disabling fullscreen avoidance also removes the related listeners and timers.
- Fixed a possible crash after restarting the app when deleting a non-empty paper directly from the context menu of an edge capsule that had not yet been expanded.
- Fixed a note's content context menu sometimes reverting to an old title or outdated actions after entering and leaving edit mode.

---

### v3.2

This release focuses on stability and refinements. Plugin support and other experimental features are not included.

**Improvements**

- **Resize handle options**: Choose one of three modes in Appearance settings. Standard shows an opaque dotted handle in the lower-right corner; Soft (the default) shows it at about 50% opacity; Hidden removes the dots while allowing resizing from any edge or corner.
- **Faster startup**: Paper restoration warm-up work now runs through a background queue.
- **Cleaner exit**: Reduced lingering menu visuals when closing the app.
- **Note image cache**: Keep one decoded copy of each image, with a cache of approximately 20 images or 50 MB, reducing memory use.
- **Sharper images**: Decode images according to the display DPI and their actual displayed width.
- **Adaptive title bars for narrow papers**: Papers support a smaller minimum width and automatically collapse title-bar buttons.
- Improved link indicators and title-bar alignment: unlinked notes show `⌖`, while notes linked from a todo show `⦿`.
- Updated AvalonEdit to 6.3.1.120.

**Bug fixes and edge cases**

- **Better paper context menus**: Added quick access to Settings and opening notes in an external Markdown editor, highlighted deletion with a warning color, and kept expand/collapse actions in sync with the paper's current state.
- **Preserve hidden papers and capsules after restart**: Fixed startup ignoring their saved `isVisible` state.
- Papers no longer respond to the Windows minimize command: `Win+↓` does not minimize a normal paper window. Maximizing and restoring a maximized window keep their standard Windows behavior.
- Fixed hiding a paper making other edge capsules flash and repeatedly shift upward. Capsules now move directly to their final positions.
- Fixed papers or capsules flickering when switching from a fullscreen window to a normal window of the same app. Fullscreen avoidance is promptly released when switching to the desktop, taskbar, or another window, while fullscreen browser video and presentations remain recognized.
- Fixed choosing an image from a note's context menu sometimes returning the editor to reading mode too early, leaving the image uninserted without an error message.

---

### v3.1

**Appearance and interaction**

- Double-click todo text to select the entire item for quick copying or replacement.
- Right-click the master capsule to open the same menu as the system tray icon.
- Microsoft YaHei and DengXian font presets now prioritize the selected font for digits and English text as well. Segoe UI is used only for missing glyphs, bringing the result closer to custom-font behavior.
- With the system-default font setting, titles, capsules, and other interface text prioritize Microsoft YaHei UI; only note and todo body text retain Segoe priority.

**Images**

- Notes support **WebP** through drag-and-drop, paste, and the image picker. An error is shown if the system does not support it.
- Fixed image insertion failing when dragging from File Explorer or choosing an image from the context menu.
- Copy local image files in File Explorer and paste them directly into a note.
- On startup, unused IDs from removed images are reclaimed for future imports. IDs still referenced by current data or backups are not reused, and existing image IDs remain unchanged.

**Saving data**

- Autosave runs after about 1 second of inactivity, or at least once every approximately 10 seconds during continuous editing.
- If autosave fails and there is no newer save request, it retries after about 10 seconds.
- Crashes now write only `PaperTodo.crash.log`; `data.crash_recovery.json` is no longer generated.
- Data recovery relies on autosave and `data.json` / `data.backup.json`.

**Bug fixes and improvements**

- **Fixed edge capsule dragging while an administrator app is in the foreground**: moving between edges and monitors, and reordering capsules, now work correctly.
- Fixed occasional drag-anchor offsets on mixed-DPI monitors that made a capsule jump as it was grabbed.
- Fixed renaming a paper sometimes moving its expanded window next to the edge capsule.
- Fixed images pasted from some screenshot tools appearing completely transparent because of nonstandard clipboard data.
- Fixed non-image files incorrectly triggering image error messages when dropped or pasted.
- If a .NET runtime component fails to load, the app now exits completely, writes a crash log, and asks the user to reopen it.
- Crash logging continues even if exception classification fails, avoiding a second crash and silent exit when an assembly name is formatted as a path.

---

### v3.0.1

A fix for a bug introduced in v3.0. See the [v3.0 release notes](https://github.com/snownico0722/PaperTodo/releases/tag/v3.0) for the full feature update.

**Bug fixes and edge cases**

- Fixed the newest todo remaining transparent with animations enabled, only becoming visible after another item was added.

---

### v3.0

**A major overhaul**

A broad rebuild and refinement of PaperTodo, with more changes than all previous releases combined.

**Images in notes**

- **Insert images from the clipboard, drag-and-drop, or the menu**. Images fit within the paper's width; remove the corresponding Markdown reference to remove an image.
- New images use the short-ID syntax `![image|100%](i:001)`. Use `![image|widthxheight, 75%](...)` or `{width=75%}` to adjust their display size.
- Images are stored locally in `note-assets.lmdb`. Corruption of a single image affects only that image; deletion and cutting can be undone. Opening a note externally safely exports temporary image files.
- **Automatically compress oversized images** is enabled by default. Images larger than 8 MB or with a longest side over 2048 pixels are compressed; import is canceled if compression fails or the result still exceeds the limits. Images wider or taller than 4096 pixels are rejected. With compression disabled, the original image is retained, subject to an 8 MB limit.

**Shortcuts**

- Record global shortcuts for **Show all, Hide all, Toggle visibility, New todo, New note, and Quit** on the Shortcuts page.
- Added **Quick-open edge capsules**, disabled by default. Left queues use `Ctrl+Shift` + `1–9`; right queues use `Ctrl+Alt` + `1–9`. If the relevant queue is unavailable, lookup falls back to other capsules on the current monitor and then other monitors.
- Added **Open at mouse position** for queue shortcuts, enabled by default. When turned off, papers use their docked or remembered expanded positions.
- **Creating a todo or note with a shortcut** now centers the paper at the pointer, keeping it within the working area, instead of placing it in the upper-left corner.
- Press **Esc** to collapse a paper when capsule mode is enabled.

**Capsules**

- **Rebuilt the capsule system** to improve stability, reduce visual bugs during dragging and multi-monitor use, refine animations and appearance, and make docking to screen edges smoother.
- **Edge and master capsules** dock using their actual widths and expand only inward. Their close area stays against the screen edge; dragging a capsule out of a queue turns it into a complete floating capsule.
- **Fast dragging and cross-monitor movement** keep capsules intact, close to the pointer, and consistently sized across display scales. Transitions also settle correctly after disconnecting a secondary monitor.
- Added **Hide close button on hover**, disabled by default. The edge capsule still extends on hover, but the extended area behaves as part of the capsule.
- A capsule being reordered or moved between queues stays above other capsules.
- Expanding and retracting edge capsules no longer expose gaps or flicker repeatedly as the window moves.
- Fixed capsules remaining extended with their close area open after the pointer left diagonally through an inner rounded corner.
- Edge capsules remain extended while their context menu is open, including when the pointer moves into the menu.
- Limit edge capsule titles to their first N characters; the default “All” preserves existing widths.
- Floating capsules recalculate their width after font changes and at the end of state transitions, keeping the right edge fixed when near the right side of the working area.
- Smoother movement when multiple edge capsules make room, reorder, or collapse toward the master capsule, with fewer stalls in long queues.

**Settings**

- Split Settings into **Basic behavior, Appearance, and Shortcuts**, with less frequently used options in advanced mode.
- Rewrote the ⓘ help descriptions to be shorter and easier to read.

**Appearance**

- Adjust global text size from **80% to 120%**.
- Set **small, medium, or large text sizes and bold styling** separately for note text, todo text, titles, and capsule text.
- Choose Standard, Soft, or Sharp text rendering.
- Added image marker visibility options: Always show, Only while editing, and Always hide. Hiding markers affects only the interface and does not change the original Markdown.
- **Enhanced bold for custom fonts**: when the app directory contains both a `papertodo` body font and a bold font, such as `PaperTodo_Bold.ttf` or `papertodo_bold.ttf`, enabling this option uses the bold file for text marked bold in notes, todos, titles, and capsules.
- Per-note percentage zoom is applied on top of global and body-text size settings.

**System tray**

- Reworked the paper list into a more modern toolbar.
- The gear beside the version number opens Settings.
- The eye icon on the left toggles all papers' visibility.
- Two icons on the right create a todo paper or a note paper.

**Startup and external integration**

Language arguments apply when starting a new app instance. They do not change saved settings or affect an already-running instance. Examples: `--language=zh-CN`, `--language=zh`, and `--lang=en`.

**Key bug fixes and improvements**

- Fullscreen avoidance now affects only the monitor containing the fullscreen app. Topmost papers and capsules on other monitors remain above other windows.
- After todo creation, paste, or completion animations, the original item fades correctly during dragging instead of overlapping the drag preview.
- Scrollbars follow the current color palette. Fixed horizontal scrollbar direction, thumb shape, and track-click behavior.
- Improved **Windows Snap layouts**: paper shadows and margins are hidden while snapped, including Windows 11 layouts with a middle third or a two-thirds main column.
- Fixed restoring paper sizes after Windows Snap.
- With “Hide papers from the window switcher” enabled, **activating a paper no longer brings unrelated, non-topmost papers to the foreground**.
- Papers created through launch arguments, the tray, or first launch appear on **the monitor containing the pointer** and join its edge queue.
- Improved default placement when creating several papers in succession.
- Faster **cold startup and restoration of multiple papers**.
- Improved **continuous typing in long notes**, Markdown line rendering, and autosave overhead. Ordinary edits no longer repeatedly copy the whole document, rescan all image references, or reorganize already-loaded data.
- Edge capsule context menus reliably appear above topmost windows.
- The tray menu now opens correctly on the first right-click.
- Fixed a capsule briefly flashing when opening a collapsed linked note from a todo.
- Fixed excessive resizing clipping paper content.
- A message is shown when note input is truncated at its length limit.
- More reliably terminate script processes started in the current session when quitting, preventing them from blocking exit.

**Other bug fixes and improvements**

- Hiding papers during reordering, disabling capsule mode, or releasing the pointer during a floating transition now ends reordering cleanly and refreshes the queue.
- Queue rearrangements and title-width updates received during dragging are applied together after the drag ends.
- Title changes during expansion or retraction are smoother, without lost updates or abrupt close-area changes.
- Fixed capsule visibility not updating promptly after relinking a note, deleting an empty todo, or undoing/redoing an action.
- Pasting multiple todo lines is treated as one operation.
- Fixed launch arguments sometimes being silently lost while startup restoration was taking a long time.
- Fixed a possible crash when a quit request arrived during startup while the master capsule was still being positioned.
- Fixed rare cases where a failed save was reported as successful.
- Data files with a missing or empty core paper list are no longer treated as a fresh installation. The app tries the backup, and stops startup if neither file can be read, preventing default papers from overwriting existing data.
- Paper titles can contain up to 20 characters. The title display-length setting can be adjusted from 2 to 20.
- Hiding one or all papers preserves their expanded or capsule states. Disabling only edge mode leaves floating capsules at their queue positions instead of moving them to an old position or another monitor.
- Disabling capsule mode correctly expands collapsed papers.
- Fixed dragging of floating capsules, edge capsules, and linked notes triggering too early at high DPI scales such as 150% and 200%, which could turn a small pointer movement into a missed click.

---

### v1.0.0

- **Initial Official Release**: Lightweight, multi-window, zero-framework Windows desktop paper note app built on native .NET and WPF.
