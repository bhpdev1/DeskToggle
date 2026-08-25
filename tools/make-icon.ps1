Add-Type -AssemblyName System.Drawing
$size = 64
$bmp  = New-Object System.Drawing.Bitmap($size, $size)
$g    = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.Clear([System.Drawing.Color]::Transparent)

$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 235, 235, 235), 4)
$g.DrawRectangle($pen, 6, 6, ($size - 13), ($size - 13))

$brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 120, 180, 255))
foreach ($p in @(@(18, 18), @(36, 18), @(18, 36), @(36, 36))) {
    $g.FillRectangle($brush, $p[0], $p[1], 10, 10)
}
$g.Dispose()

$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$png = $ms.ToArray()
$bmp.Dispose()

$out = New-Object System.IO.MemoryStream
$w   = New-Object System.IO.BinaryWriter($out)
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]1)          # ICONDIR
$w.Write([Byte]$size); $w.Write([Byte]$size); $w.Write([Byte]0); $w.Write([Byte]0)
$w.Write([UInt16]1); $w.Write([UInt16]32)
$w.Write([UInt32]$png.Length); $w.Write([UInt32]22)                    # ICONDIRENTRY
$w.Write($png)
$w.Flush()

$dir = Join-Path $PSScriptRoot '..\src\DesktopIconsToggle'
[System.IO.File]::WriteAllBytes((Join-Path (Resolve-Path -LiteralPath $dir).Path 'app.ico'), $out.ToArray())
Write-Output "app.ico written"
