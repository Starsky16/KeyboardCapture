# 生成 KeyboardCapture 插件图标 icon.png（512x512，圆角透明背景 + 键盘图案）。
# 用法：pwsh tools/generate-icon.ps1
#
# 设计约定（参考 ClassIsland 生态公开插件图标）：
#   - 512x512 方形 PNG，四角留空（透明），便于市场的圆角 / 深色底展示；
#   - 大面积主色 + 白色键盘主体，整体只保留少量高对比色块，保证缩小到 48px 仍可辨认；
#   - 单个琥珀色键帽作为视觉焦点，呼应「捕捉」这一插件主题。

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$outPath = Join-Path $root "icon.png"

Add-Type -AssemblyName System.Drawing

function New-RoundedRectPath {
    param([double]$x, [double]$y, [double]$w, [double]$h, [double]$r)
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    # 半径不能超过短边的一半，否则圆弧会相互穿插
    if ($d -gt $w) { $d = $w }
    if ($d -gt $h) { $d = $h }
    $p.AddArc([float]$x, [float]$y, [float]$d, [float]$d, 180, 90)
    $p.AddArc([float]($x + $w - $d), [float]$y, [float]$d, [float]$d, 270, 90)
    $p.AddArc([float]($x + $w - $d), [float]($y + $h - $d), [float]$d, [float]$d, 0, 90)
    $p.AddArc([float]$x, [float]($y + $h - $d), [float]$d, [float]$d, 90, 90)
    $p.CloseFigure()
    return $p
}

$size = 512
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

# ---- 背景：靛蓝渐变圆角方块，四角保持透明 ----
$bg = New-RoundedRectPath 0 0 $size $size 112
$bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Rectangle(0, 0, $size, $size)),
    [System.Drawing.Color]::FromArgb(255, 90, 107, 255),
    [System.Drawing.Color]::FromArgb(255, 42, 33, 166),
    45)
$g.FillPath($bgBrush, $bg)

# ---- 键盘主体：白色圆角矩形 ----
$white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$board = New-RoundedRectPath 92 156 328 200 44
$g.FillPath($white, $board)

# ---- 键帽：5 列 x 3 行，第 3 行右端为加宽的琥珀色回车键，底行为两端键 + 空格键 ----
# 键帽只占主体约 3/4 宽度，保证缩小后键与键之间仍有可见的白色间隙，不会糊成一片。
$keyBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 79, 70, 229))
$accent = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 176, 32))

$keyW = 46
$keyH = 30
$keyR = 8
$gapX = 13.5
$gapY = 12
$colX = 0..4 | ForEach-Object { 114 + $_ * ($keyW + $gapX) }
$rowY = 0..3 | ForEach-Object { 178 + $_ * ($keyH + $gapY) }

for ($row = 0; $row -lt 3; $row++) {
    foreach ($x in $colX) {
        if ($row -eq 2 -and $x -eq 292.5) { continue }  # 第 3 行右端留给加宽的回车键
        $path = New-RoundedRectPath $x $rowY[$row] $keyW $keyH $keyR
        $g.FillPath($keyBrush, $path)
        $path.Dispose()
    }
}

# 加宽的回车键（琥珀色），是整幅图标唯一的暖色焦点
$enter = New-RoundedRectPath 292.5 $rowY[2] 105.5 $keyH $keyR
$g.FillPath($accent, $enter)
$enter.Dispose()

# 底行：两端普通键 + 中间空格键
$bottom = $rowY[3]
foreach ($x in @(114, 352)) {
    $path = New-RoundedRectPath $x $bottom $keyW $keyH $keyR
    $g.FillPath($keyBrush, $path)
    $path.Dispose()
}
$space = New-RoundedRectPath 173.5 $bottom 224.5 $keyH $keyR
$g.FillPath($keyBrush, $space)

$space.Dispose()
$bgBrush.Dispose()
$white.Dispose()
$keyBrush.Dispose()
$accent.Dispose()
$bg.Dispose()
$board.Dispose()
$g.Dispose()
$bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

Write-Host "图标已生成: $outPath" -ForegroundColor Green