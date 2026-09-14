#requires -Version 5.1
<#
.SYNOPSIS
重置 rename-titles 使用的计数器,使下一次从 [RELOAD 1] 开始。

.EXAMPLE
pwsh scripts/reset-reload-counter.ps1
#>

[CmdletBinding()]
param()

$counterPath = Join-Path $PSScriptRoot '.reload-counter'
if (Test-Path $counterPath) {
    Remove-Item -LiteralPath $counterPath -Force
    Write-Host "已删除 $counterPath"
} else {
    Write-Host "计数器不存在,无需重置"
}