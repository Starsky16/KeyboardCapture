# 生成 KeyboardCapture 插件图标 icon.png（256x256，简单键盘图案）。
# 用法：pwsh tools/generate-icon.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$outPath = Join-Path $root "icon.png"

Add-Type -AssemblyName System.Drawing

function New-RoundedRectPath {
    param($x, $y, $w, $h, $r)
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

$size = 256
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

# 背景（蓝紫渐变）
$rect = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
$bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $rect,
    [System.Drawing.Color]::FromArgb(255, 96, 140, 255),
    [System.Drawing.Color]::FromArgb(255, 37, 64, 180),
    45)
$g.FillRectangle($bg, $rect)

# 键盘白色基底（圆角矩形）
$keyboardBlue = [System.Drawing.Color]::FromArgb(255, 37, 99, 235)
$base = New-RoundedRectPath 28 78 200 134 18
$baseBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$g.FillPath($baseBrush, $base)

# 键帽（网格布局）
$keyBrush = New-Object System.Drawing.SolidBrush($keyboardBlue)
$keyCols = @(44, 76, 108, 140, 172, 204)
$keyRows = @(94, 122, 150)
foreach ($row in $keyRows) {
    foreach ($col in $keyCols) {
        if ($row -eq 150 -and $col -in @(76, 108, 140)) { continue }  # 底行只留两端
        $g.FillRectangle($keyBrush, $col, $row, 26, 24)
    }
}
# 底行空格键与两端的键
$g.FillRectangle($keyBrush, 44, 150, 26, 24)
$g.FillRectangle($keyBrush, 172, 150, 26, 24)
$spaceBrush = New-Object System.Drawing.SolidBrush($keyboardBlue)
$g.FillRectangle($spaceBrush, 108, 150, 60, 24)

$g.Dispose()
$bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

Write-Host "图标已生成: $outPath" -ForegroundColor Green
