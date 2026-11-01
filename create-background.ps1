# Generate an original fallback background; no third-party image is distributed.
$ErrorActionPreference='Stop'
$outputPath=Join-Path $PSScriptRoot 'background.png'
if(Test-Path -LiteralPath $outputPath){return}
Add-Type -AssemblyName System.Drawing
$bitmap=New-Object Drawing.Bitmap(1200,800)
$graphics=[Drawing.Graphics]::FromImage($bitmap)
$rect=New-Object Drawing.Rectangle(0,0,1200,800)
$brush=New-Object Drawing.Drawing2D.LinearGradientBrush($rect,[Drawing.Color]::FromArgb(24,24,24),[Drawing.Color]::FromArgb(55,55,55),35)
try{$graphics.FillRectangle($brush,$rect);$bitmap.Save($outputPath,[Drawing.Imaging.ImageFormat]::Png)}
finally{$brush.Dispose();$graphics.Dispose();$bitmap.Dispose()}
