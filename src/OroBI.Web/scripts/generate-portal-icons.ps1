# Raster variants of public/portal-icon.svg; no external image dependencies.
Add-Type -AssemblyName System.Drawing
$publicDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../public'))
foreach ($iconSize in @(192, 512, 180)) {
    $bitmap = New-Object System.Drawing.Bitmap($iconSize, $iconSize)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $goldPen = New-Object System.Drawing.Pen([System.Drawing.ColorTranslator]::FromHtml('#d6a62e'), 18)
    $lightBrush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#f2c94c'))
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([System.Drawing.ColorTranslator]::FromHtml('#21150c'))
        $graphics.ScaleTransform(($iconSize / 512.0), ($iconSize / 512.0))
        $graphics.DrawEllipse($goldPen, 111, 99, 290, 290)
        $graphics.FillRectangle($lightBrush, 174, 227, 37, 83)
        $graphics.FillRectangle($lightBrush, 238, 187, 37, 123)
        $graphics.FillRectangle($lightBrush, 302, 147, 37, 163)
        $graphics.FillRectangle($lightBrush, 184, 355, 144, 8)
        $name = if ($iconSize -eq 180) { 'apple-touch-icon.png' } else { "portal-icon-$iconSize.png" }
        $bitmap.Save((Join-Path $publicDirectory $name), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $lightBrush.Dispose()
        $goldPen.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}
