param(
    [string]$OutputPath = (Join-Path (Split-Path -Parent $PSScriptRoot) "src\RcloneTransferManager\Assets\RcloneTransferManager.ico"),
    [string]$PreviewPath = ""
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function New-RoundedRectanglePath {
    param(
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [float]$Radius
    )

    $diameter = $Radius * 2
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconBitmap {
    param([int]$Size)

    $renderSize = $Size * 4
    $scale = $renderSize / 128.0
    $render = [System.Drawing.Bitmap]::new($renderSize, $renderSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($render)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        $tile = New-RoundedRectanglePath (4 * $scale) (4 * $scale) (120 * $scale) (120 * $scale) (26 * $scale)
        $tileBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 17, 17, 17))
        try { $graphics.FillPath($tileBrush, $tile) }
        finally { $tileBrush.Dispose(); $tile.Dispose() }

        $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::White, 12 * $scale)
        try {
            $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Square
            $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Square
            $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

            $rPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
            try {
                $rPath.StartFigure()
                $rPath.AddLine(32 * $scale, 94 * $scale, 32 * $scale, 34 * $scale)
                $rPath.AddLine(32 * $scale, 34 * $scale, 52 * $scale, 34 * $scale)
                $rPath.AddBezier(52 * $scale, 34 * $scale, 67 * $scale, 34 * $scale, 74 * $scale, 42 * $scale, 74 * $scale, 55 * $scale)
                $rPath.AddBezier(74 * $scale, 55 * $scale, 74 * $scale, 68 * $scale, 67 * $scale, 76 * $scale, 52 * $scale, 76 * $scale)
                $rPath.AddLine(52 * $scale, 76 * $scale, 32 * $scale, 76 * $scale)
                $rPath.StartFigure()
                $rPath.AddLine(53 * $scale, 76 * $scale, 72 * $scale, 94 * $scale)
                $graphics.DrawPath($pen, $rPath)
            }
            finally { $rPath.Dispose() }

            $graphics.DrawLine($pen, 82 * $scale, 40 * $scale, 106 * $scale, 40 * $scale)
            $graphics.DrawLine($pen, 94 * $scale, 40 * $scale, 94 * $scale, 94 * $scale)
        }
        finally { $pen.Dispose() }
    }
    finally { $graphics.Dispose() }

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $resizeGraphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $resizeGraphics.Clear([System.Drawing.Color]::Transparent)
        $resizeGraphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $resizeGraphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $resizeGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $resizeGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $resizeGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $resizeGraphics.DrawImage($render, 0, 0, $Size, $Size)
    }
    finally { $resizeGraphics.Dispose(); $render.Dispose() }

    return $bitmap
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = @()
foreach ($size in $sizes) {
    $bitmap = New-IconBitmap $size
    try {
        $stream = [System.IO.MemoryStream]::new()
        try {
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames += [PSCustomObject]@{ Size = $size; Bytes = $stream.ToArray() }
        }
        finally { $stream.Dispose() }

        if ($PreviewPath -and $size -eq 256) {
            $previewDirectory = Split-Path -Parent $PreviewPath
            if ($previewDirectory) { [System.IO.Directory]::CreateDirectory($previewDirectory) | Out-Null }
            $bitmap.Save($PreviewPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
    }
    finally { $bitmap.Dispose() }
}

$outputDirectory = Split-Path -Parent $OutputPath
if ($outputDirectory) { [System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null }
$file = [System.IO.File]::Create($OutputPath)
$writer = [System.IO.BinaryWriter]::new($file)
try {
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$frames.Count)

    $offset = 6 + (16 * $frames.Count)
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([Byte]$dimension)
        $writer.Write([Byte]$dimension)
        $writer.Write([Byte]0)
        $writer.Write([Byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$frame.Bytes.Length)
        $writer.Write([UInt32]$offset)
        $offset += $frame.Bytes.Length
    }

    foreach ($frame in $frames) { $writer.Write($frame.Bytes) }
}
finally { $writer.Dispose(); $file.Dispose() }

Write-Host "Created $OutputPath with sizes: $($sizes -join ', ')"
