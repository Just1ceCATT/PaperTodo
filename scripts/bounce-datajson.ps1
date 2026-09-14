#requires -Version 5.1
<#
.SYNOPSIS
持续循环修改 data.json,用于压力测试热重载的 debounce + Save race 机制。

.DESCRIPTION
每 -IntervalSeconds 秒调用 replace-datajson.ps1 -Operation rename-titles 一次。
两个相邻写入(间隔 < 350ms)应被 FileSystemWatcher debounce 折叠为一次 reload,
两个间隔 > 350ms 的写入则各触发一次 reload。

Ctrl+C 停止。

.EXAMPLE
pwsh scripts/bounce-datajson.ps1 -IntervalSeconds 1
# 每秒改一次,验证 debounce 行为。

.EXAMPLE
pwsh scripts/bounce-datajson.ps1 -IntervalSeconds 10
# 每 10 秒改一次,验证每次 reload 都正确完成。
#>

[CmdletBinding()]
param(
    [int]$IntervalSeconds = 1,
    [int]$MaxIterations = 0   # 0 = 无限
)

$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'replace-datajson.ps1'

if (-not (Test-Path $script)) { throw "找不到 $script" }

Write-Host "开始 bounce,间隔 $IntervalSeconds 秒(MaxIterations=$MaxIterations,Ctrl+C 停止)..."
$i = 0
while ($true) {
    $i++
    if ($MaxIterations -gt 0 -and $i -gt $MaxIterations) { break }
    try {
        $args = @{
            Operation = 'rename-titles'
            Backup = $false
        }
        & $script @args
    } catch {
        Write-Warning "第 $i 次失败: $_"
    }
    if ($MaxIterations -gt 0 -and $i -ge $MaxIterations) { break }
    Start-Sleep -Seconds $IntervalSeconds
}
Write-Host "完成 $i 次"