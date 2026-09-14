#requires -Version 5.1
<#
.SYNOPSIS
列出 data.json 中所有 paper 的 id + type + title,便于获取真实 id。

.PARAMETER DataJsonPath
默认 <repo-root>/输出/PaperTodo-v4.0.0-preview/data.json。

.EXAMPLE
powershell -ExecutionPolicy Bypass -File Z:\tool\PaperTodo-dev\scripts\list-papers.ps1

.EXAMPLE
powershell -ExecutionPolicy Bypass -File Z:\tool\PaperTodo-dev\scripts\list-papers.ps1 | clip
# 输出复制到剪贴板,方便粘贴到 set-title 命令。
#>

[CmdletBinding()]
param(
    [string]$DataJsonPath = (Join-Path $PSScriptRoot '..\输出\PaperTodo-v4.0.0-preview\data.json')
)

$ErrorActionPreference = 'Stop'

# 兜底:$PSScriptRoot 在 cmd.exe 直接调用时可能为空
if (-not $PSScriptRoot -or [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    $PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
}
if (-not $PSScriptRoot -or [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    $PSScriptRoot = (Get-Location).Path
}

$resolvedPath = (Resolve-Path -LiteralPath $DataJsonPath -ErrorAction Stop).Path
$state = Get-Content -LiteralPath $resolvedPath -Raw | ConvertFrom-Json

if (-not $state.papers -or $state.papers.Count -eq 0) {
    Write-Host "(data.json 中没有 paper — 请先在 PaperTodo 里创建一个)"
    return
}

Write-Host "文件: $resolvedPath"
Write-Host "paper 数量: $($state.papers.Count)"
Write-Host ""

# 表格输出:让 PowerShell 处理中文字符宽度
$state.papers | ForEach-Object {
    [PSCustomObject]@{
        id = $_.id
        type = $_.type
        title = $_.title
        visible = $_.isVisible
    }
} | Format-Table -AutoSize -Wrap | Out-String -Width 4096 | Write-Host

Write-Host "--- 直接复制用 ---"
Write-Host ""
foreach ($p in $state.papers) {
    Write-Host ("id:    {0}" -f $p.id)
    Write-Host ("type:  {0}" -f $p.type)
    Write-Host ("title: {0}" -f $p.title)
    Write-Host ""
}