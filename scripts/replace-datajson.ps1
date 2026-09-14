#requires -Version 5.1
<#
.SYNOPSIS
替换 PaperTodo 的 data.json,触发热重载。

.DESCRIPTION
本脚本读取指定(或默认)data.json,按 -Operation 修改字段,原子写入(写到 .tmp 后
File.Move 替换),触发 DataHotReloader 检测。

.PARAMETER DataJsonPath
data.json 完整路径。默认 <repo-root>/输出/PaperTodo-v4.0.0-preview/data.json。

.PARAMETER Operation
要执行的修改类型:
  rename-titles        给所有 paper 的 title 加 [RELOAD n] 后缀,n 自增(Keep 路径验证)
  set-title            用 -TargetId + -Title 改指定 paper 的 title(Replace 路径验证)
  add-paper           加一个新 paper(Add 路径验证)
  remove-paper        用 -TargetId 删 paper(Remove 路径验证)
  toggle-visibility   翻转指定 paper 的 IsVisible
  cycle-theme         在 light/dark/high-contrast 三者间循环 theme(替换整个文件以触发 Keep→Replace)

.PARAMETER TargetId
要操作的 paper 的 id(用于 set-title / remove-paper / toggle-visibility)。

.PARAMETER Title
新 title(用于 set-title)。

.PARAMETER Backup
是否备份原文件。默认 true。

.EXAMPLE
pwsh scripts/replace-datajson.ps1 -Operation rename-titles
# 给所有 paper 的 title 加 [RELOAD n] 后缀 → Keep 路径(逐字段等值)触发。

.EXAMPLE
pwsh scripts/replace-datajson.ps1 -Operation set-title -TargetId 19cfae9d5e7b4aea8c3f6a357b9445e1 -Title '笔记1 (reloaded)'
# 改指定 paper 的 title → Replace 路径触发。

.EXAMPLE
pwsh scripts/replace-datajson.ps1 -Operation add-paper
# 追加新 paper → Add 路径触发。
#>

[CmdletBinding()]
param(
    [string]$DataJsonPath = (Join-Path $PSScriptRoot '..\输出\PaperTodo-v4.0.0-preview\data.json'),
    [ValidateSet('rename-titles', 'set-title', 'add-paper', 'remove-paper', 'toggle-visibility', 'cycle-theme')]
    [string]$Operation = 'rename-titles',
    [string]$TargetId = '',
    [string]$Title = '',
    [bool]$Backup = $true
)

$ErrorActionPreference = 'Stop'
$resolvedPath = (Resolve-Path -LiteralPath $DataJsonPath -ErrorAction Stop).Path
Write-Host "目标文件: $resolvedPath"

# 读原文件为字符串 + 解析为对象(便于按字段操作;原始字节最后保留缩进写入)
$rawJson = Get-Content -LiteralPath $resolvedPath -Raw -Encoding UTF8
$state = $rawJson | ConvertFrom-Json

switch ($Operation) {
    'rename-titles' {
        $counterPath = Join-Path $PSScriptRoot '.reload-counter'
        $n = if (Test-Path $counterPath) { [int](Get-Content $counterPath -Raw) } else { 0 }
        $n++
        Set-Content -LiteralPath $counterPath -Value $n -NoNewline
        $stamp = "[RELOAD $n $(Get-Date -Format 'HH:mm:ss')]"
        foreach ($p in $state.papers) {
            # 注意:Keep 路径要求 PaperDataFullyEquivalent → 改 title 实际触发 Replace 路径,
            # 这正是验证 R3 Close + Recreate 的最简手段。
            $p.title = "$($p.title) $stamp"
        }
        Write-Host "操作: 给 $($state.papers.Count) 个 paper 的 title 加 $stamp"
    }
    'set-title' {
        if (-not $TargetId -or -not $Title) { throw 'set-title 需要 -TargetId 和 -Title' }
        $p = $state.papers | Where-Object { $_.id -eq $TargetId } | Select-Object -First 1
        if (-not $p) { throw "找不到 id=$TargetId 的 paper" }
        $old = $p.title
        $p.title = $Title
        Write-Host "操作: paper $TargetId title '$old' -> '$Title'"
    }
    'add-paper' {
        $new = [ordered]@{
            id = ([guid]::NewGuid().ToString('N'))
            type = 'note'
            title = 'Test Reload Paper'
            x = 200; y = 200; width = 320; height = 360
            isVisible = $true
            alwaysOnTop = $false
            isCollapsed = $false
            textZoom = 1
            bodyProviderId = 'markdown'
            bodyHeaderText = ''
            bodyCapsuleText = ''
            startupOwnerPluginId = ''
            startupInstanceKey = ''
            capsuleSide = ''
            capsuleMonitorDeviceName = ''
            deepCapsuleExpandedSide = ''
            deepCapsuleExpandedMonitorDeviceName = ''
            items = @()
            content = ''
        }
        # ConvertFrom-Json 默认输出 PSCustomObject;转回数组形式
        $papers = @($state.papers) + (New-Object psobject -Property $new)
        $state.papers = $papers
        Write-Host "操作: 追加新 paper id=$($new.id)"
    }
    'remove-paper' {
        if (-not $TargetId) { throw 'remove-paper 需要 -TargetId' }
        $before = $state.papers.Count
        $state.papers = @($state.papers | Where-Object { $_.id -ne $TargetId })
        Write-Host "操作: 删除 paper $TargetId ($before -> $($state.papers.Count))"
    }
    'toggle-visibility' {
        if (-not $TargetId) { throw 'toggle-visibility 需要 -TargetId' }
        $p = $state.papers | Where-Object { $_.id -eq $TargetId } | Select-Object -First 1
        if (-not $p) { throw "找不到 id=$TargetId 的 paper" }
        $p.isVisible = -not $p.isVisible
        Write-Host "操作: paper $TargetId isVisible -> $($p.isVisible)"
    }
    'cycle-theme' {
        # 修改全局 theme 字段;State.Theme 变化时 PaperTodo 整体重新加载主题资源。
        $themes = @('light', 'dark', 'high-contrast')
        $current = if ($state.PSObject.Properties['theme']) { $state.theme } else { 'light' }
        $idx = ($themes.IndexOf($current) + 1) % $themes.Count
        $next = $themes[$idx]
        # State.Theme 字段不在现有文件里;补充
        if (-not $state.PSObject.Properties['theme']) {
            $state | Add-Member -NotePropertyName theme -NotePropertyValue $next
        } else {
            $state.theme = $next
        }
        Write-Host "操作: theme '$current' -> '$next'"
    }
}

# 备份(可选)
if ($Backup) {
    $bak = "$resolvedPath.bak"
    Copy-Item -LiteralPath $resolvedPath -Destination $bak -Force
    Write-Host "备份: $bak"
}

# 原子写入(模拟 DurableAtomicFileWriter 的 tmp+rename 模式)
$tmp = "$resolvedPath.tmp"
$newJson = $state | ConvertTo-Json -Depth 20
[System.IO.File]::WriteAllText($tmp, $newJson, [System.Text.UTF8Encoding]::new($false))
# Move-Item -Force 在 Windows 上等价 File.Move(..., overwrite:true)
Move-Item -LiteralPath $tmp -Destination $resolvedPath -Force
Write-Host "已写入: $resolvedPath"
Write-Host "DataHotReloader 应在 ~350ms 内检测到变更并触发 reload"