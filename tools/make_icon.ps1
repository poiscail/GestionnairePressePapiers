# Génère src/PressePapiers/app.ico (presse-papiers bleu) en 16, 32, 48 et 256 px.
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot "..\src\PressePapiers\app.ico"

function RoundRect($x, $y, $w, $h, $r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, 2*$r, 2*$r, 180, 90); $p.AddArc($x+$w-2*$r, $y, 2*$r, 2*$r, 270, 90)
    $p.AddArc($x+$w-2*$r, $y+$h-2*$r, 2*$r, 2*$r, 0, 90); $p.AddArc($x, $y+$h-2*$r, 2*$r, 2*$r, 90, 90)
    $p.CloseFigure(); $p
}

function Draw($s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = "AntiAlias"
    $k = $s / 256.0
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(37, 99, 235))), (RoundRect (16*$k) (16*$k) (224*$k) (224*$k) (44*$k)))
    $g.FillPath([System.Drawing.Brushes]::White, (RoundRect (60*$k) (52*$k) (136*$k) (168*$k) (16*$k)))
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(30, 41, 59))), (RoundRect (92*$k) (34*$k) (72*$k) (40*$k) (12*$k)))
    if ($s -ge 32) {
        $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(147, 197, 253)), ([Math]::Max(2, 12*$k))
        $pen.StartCap = "Round"; $pen.EndCap = "Round"
        foreach ($y in 104, 138, 172) { $g.DrawLine($pen, 84*$k, $y*$k, ($(if ($y -eq 172) {140} else {172}))*$k, $y*$k) }
    }
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    ,$ms.ToArray()
}

# Fichier ICO : en-tête + entrées PNG
$sizes = 16, 32, 48, 256
$pngs = $sizes | ForEach-Object { ,(Draw $_) }
$fs = [System.IO.File]::Create($out); $w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $d = $(if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] })
    $w.Write([byte]$d); $w.Write([byte]$d); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Close()
"icone: $out"
