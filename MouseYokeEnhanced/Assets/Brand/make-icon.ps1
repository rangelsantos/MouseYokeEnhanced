# Builds MouseYoke.ico from mouseyoke-enhanced-icon-1024.png: 16-64 px as 32-bit BMP frames
# (what the shell and WinForms read best at small sizes) and 256 px as PNG.
# Run it again after changing the source image:  powershell -File make-icon.ps1
Add-Type -AssemblyName System.Drawing

$dir = $PSScriptRoot
$src = [System.Drawing.Bitmap]::new((Join-Path $dir 'mouseyoke-enhanced-icon-1024.png'))
# The plane sits a little low with a wide margin; a square crop around it keeps it readable at 16 px.
$crop = [System.Drawing.Rectangle]::new(80, 108, 864, 864)
$sizes = 16, 20, 24, 32, 40, 48, 64, 256

function Render([int]$n) {
	$b = [System.Drawing.Bitmap]::new($n, $n, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
	$g = [System.Drawing.Graphics]::FromImage($b)
	$g.InterpolationMode = 'HighQualityBicubic'
	$g.PixelOffsetMode = 'HighQuality'
	$g.CompositingQuality = 'HighQuality'
	$g.DrawImage($src, [System.Drawing.Rectangle]::new(0, 0, $n, $n), $crop, [System.Drawing.GraphicsUnit]::Pixel)
	$g.Dispose()
	return $b
}

function Frame([System.Drawing.Bitmap]$b) {
	$n = $b.Width
	$ms = [System.IO.MemoryStream]::new()
	if ($n -ge 256) {
		$b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
		return , $ms.ToArray()
	}
	$data = $b.LockBits([System.Drawing.Rectangle]::new(0, 0, $n, $n), 'ReadOnly', 'Format32bppArgb')
	$px = [byte[]]::new($n * $n * 4)
	[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $px, 0, $px.Length)
	$b.UnlockBits($data)
	$w = [System.IO.BinaryWriter]::new($ms)
	# BITMAPINFOHEADER; the height counts the color rows and the AND mask.
	$w.Write([int]40); $w.Write([int]$n); $w.Write([int]($n * 2)); $w.Write([int16]1); $w.Write([int16]32)
	for ($i = 0; $i -lt 6; $i++) { $w.Write([int]0) }
	for ($y = $n - 1; $y -ge 0; $y--) { $w.Write($px, $y * $n * 4, $n * 4) }
	$w.Write([byte[]]::new([Math]::Floor(($n + 31) / 32) * 4 * $n))
	$w.Flush()
	return , $ms.ToArray()
}

$frames = @()
foreach ($n in $sizes) {
	$b = Render $n
	$frames += , (Frame $b)
	$b.Dispose()
}
$src.Dispose()

$out = [System.IO.MemoryStream]::new()
$w = [System.IO.BinaryWriter]::new($out)
$w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
	$n = $sizes[$i]
	$dim = [byte]($n % 256)
	$w.Write($dim); $w.Write($dim); $w.Write([byte]0); $w.Write([byte]0)
	$w.Write([int16]1); $w.Write([int16]32); $w.Write([int]$frames[$i].Length); $w.Write([int]$offset)
	$offset += $frames[$i].Length
}
foreach ($f in $frames) { $w.Write($f) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $dir 'MouseYoke.ico'), $out.ToArray())
'MouseYoke.ico: ' + $out.Length + ' bytes'
