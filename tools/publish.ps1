# 发布 KeyboardCapture 插件。
# 用法：pwsh tools/publish.ps1 [-Configuration Debug|Release]
# 产物输出到 ./cipx 目录（.cipx 为 ClassIsland 插件安装包格式，已加入 .gitignore）。

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (Test-Path -Path "./cipx") {
    Remove-Item "./cipx" -Recurse -Force
}

Write-Host "正在发布插件 ($Configuration)..." -ForegroundColor Cyan
dotnet publish KeyboardCapture.csproj -c $Configuration -p:CreateCipx=true -o "./cipx"

Write-Host "发布完成，产物位于: $root/cipx" -ForegroundColor Green
