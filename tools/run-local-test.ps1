# 本地回归测试：使用本地 ClassIsland Debug 构建加载 KeyboardCapture 插件，
# 验证「插件加载 → 托管服务自动启动全局钩子 → 模拟按键 → Demo 插件收到事件」全链路。
#
# 用法：
#   pwsh tools/run-local-test.ps1 [-ClassIslandExe <路径>] [-PluginId <id>]
#
# 前置条件：先运行 dotnet build KeyboardCapture.sln -c Debug。

param(
    [string]$ClassIslandExe = "d:\code\ClassIsland\ClassIsland.Desktop\bin\Debug\net8.0-windows10.0.19041.0\ClassIsland.Desktop.exe",
    [string]$MainPluginId = "Starsky16.KeyboardCapture",
    [string]$DemoPluginId = "Starsky16.KeyboardCapture.Demo"
)

$ErrorActionPreference = "Stop"

$exeDir = Split-Path -Parent $ClassIslandExe
$pluginsRoot = Join-Path $exeDir "Plugins"
$cfgRoot = Join-Path $exeDir "Config\Plugins"
$mainOut = Join-Path (Split-Path -Parent $PSScriptRoot) "bin\Debug\net8.0-windows"
$demoOut = Join-Path (Split-Path -Parent $PSScriptRoot) "demo\KeyboardCapture.DemoPlugin\bin\Debug\net8.0-windows"

$marker = Join-Path $cfgRoot "$MainPluginId\loaded.marker"
$keylog = Join-Path $cfgRoot "$DemoPluginId\keylog.txt"

if (-not (Test-Path $ClassIslandExe)) { throw "找不到 ClassIsland: $ClassIslandExe" }
if (-not (Test-Path "$mainOut\KeyboardCapture.dll")) { throw "请先 dotnet build KeyboardCapture.sln -c Debug" }

Write-Host "== 1/5 部署插件到 Plugins 目录 ==" -ForegroundColor Cyan
foreach ($n in @($MainPluginId, $DemoPluginId)) {
    Remove-Item (Join-Path $pluginsRoot $n) -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path (Join-Path $pluginsRoot $n) -Force | Out-Null
}
Copy-Item "$mainOut\*" (Join-Path $pluginsRoot $MainPluginId) -Recurse -Force
Copy-Item "$demoOut\*" (Join-Path $pluginsRoot $DemoPluginId) -Recurse -Force

Write-Host "== 2/5 清理旧标记并启动 ClassIsland ==" -ForegroundColor Cyan
Get-Process -Name "ClassIsland*" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
Remove-Item $marker, $keylog -Force -ErrorAction SilentlyContinue
$env:KEYBOARD_CAPTURE_CI_MARKER = "1"
$env:KEYBOARD_CAPTURE_DEMO = "1"
$proc = Start-Process -FilePath $ClassIslandExe -ArgumentList @("--skip-oobe", "--quiet") -PassThru -WindowStyle Hidden

try {
    Write-Host "== 3/5 等待主插件加载标记 ==" -ForegroundColor Cyan
    $loaded = $false
    $deadline = (Get-Date).AddSeconds(75)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path $marker) { $loaded = $true; break }
        if ($proc.HasExited) { break }
        Start-Sleep -Seconds 3
    }
    if (-not $loaded) { throw "主插件未加载（无 loaded.marker）" }
    Write-Host "主插件加载成功" -ForegroundColor Green

    Write-Host "== 4/5 等待钩子就绪并模拟按键（F13 / A / Ctrl+F14，最多 3 轮）==" -ForegroundColor Cyan
    # loaded.marker 在插件 Initialize 阶段写入，早于主机启动托管服务（全局钩子）。
    # 先等待数秒确保钩子已激活，避免模拟按键落在钩子启动前。
    Start-Sleep -Seconds 10
    Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public class Kbd { [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, System.UIntPtr extra); }'
    function KeyD($k) { [Kbd]::keybd_event($k, 0, 0, [System.UIntPtr]::Zero) }
    function KeyU($k) { [Kbd]::keybd_event($k, 0, 2, [System.UIntPtr]::Zero) }
    function Key($k) { KeyD $k; Start-Sleep -Milliseconds 60; KeyU $k; Start-Sleep -Milliseconds 60 }

    $received = $false
    for ($attempt = 1; $attempt -le 3 -and -not $received; $attempt++) {
        Write-Host "  第 $attempt 轮模拟按键..."
        Key 0x7C # F13
        Key 0x41 # A
        KeyD 0xA2 # LeftCtrl down
        Key 0x7D # F14
        KeyU 0xA2 # LeftCtrl up

        Write-Host "== 5/5 等待 Demo 插件收到按键 ==" -ForegroundColor Cyan
        $deadline = (Get-Date).AddSeconds(35)
        while ((Get-Date) -lt $deadline -and -not $received) {
            if (Test-Path $keylog) {
                $content = Get-Content $keylog -Raw -ErrorAction SilentlyContinue
                if ($content -match "F13" -and $content -match "DOWN A" -and $content -match "F14 \[Ctrl\]") {
                    $received = $true
                    break
                }
            }
            if ($proc.HasExited) { break }
            Start-Sleep -Seconds 3
        }
    }
    if (-not $received) { throw "Demo 插件未收到预期按键事件" }
    Write-Host "Demo 插件收到按键事件" -ForegroundColor Green
    Write-Host "--- keylog ---"
    Get-Content $keylog
    Write-Host "TEST_PASS" -ForegroundColor Green
}
finally {
    Get-Process -Name "ClassIsland*" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}
