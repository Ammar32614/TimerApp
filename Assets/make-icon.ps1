# Regenerates Assets/stint.ico (multi-size, PNG-encoded frames).
# Run: powershell -ExecutionPolicy Bypass -File Assets/make-icon.ps1
Add-Type -AssemblyName System.Drawing

$sizes = 16, 24, 32, 48, 64, 128, 256
$frames = foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Rounded dark tile
    $r = [single]($s * 0.22)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, 2 * $r, 2 * $r, 180, 90)
    $path.AddArc($s - 2 * $r - 1, 0, 2 * $r, 2 * $r, 270, 90)
    $path.AddArc($s - 2 * $r - 1, $s - 2 * $r - 1, 2 * $r, 2 * $r, 0, 90)
    $path.AddArc(0, $s - 2 * $r - 1, 2 * $r, 2 * $r, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#1C1F27'))), $path)

    # Amber three-quarter ring, like a session in progress
    $t = [single]([Math]::Max(2, $s * 0.13))
    $m = [single]($s * 0.22 + $t / 2)
    $track = New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#2C313C')), $t
    $pen = New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#F5B14C')), $t
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $g.DrawEllipse($track, $m, $m, $s - 2 * $m, $s - 2 * $m)
    $g.DrawArc($pen, $m, $m, $s - 2 * $m, $s - 2 * $m, -90, 270)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , @($s, $ms.ToArray())
}

$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $s = $f[0]; $bytes = $f[1]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$bytes.Length); $w.Write([uint32]$offset)
    $offset += $bytes.Length
}
foreach ($f in $frames) { $w.Write([byte[]]$f[1]) }
[System.IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'stint.ico'), $out.ToArray())
"Wrote stint.ico ($($out.Length) bytes)"
