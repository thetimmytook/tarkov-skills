[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$assetsDirectory = Join-Path $PSScriptRoot 'Assets'
$sourcePath = Join-Path $PSScriptRoot 'Source\AppIconSource.png'
New-Item -ItemType Directory -Path $assetsDirectory -Force | Out-Null

if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "Application icon source is missing: $sourcePath"
}

$source = [System.Drawing.Bitmap]::new($sourcePath)

function New-ResizedBitmap {
    param(
        [Parameter(Mandatory)] [int] $Width,
        [Parameter(Mandatory)] [int] $Height
    )

    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.Clear($source.GetPixel(0, 0))

        $scale = [Math]::Min($Width / $source.Width, $Height / $source.Height)
        $renderWidth = [int][Math]::Round($source.Width * $scale)
        $renderHeight = [int][Math]::Round($source.Height * $scale)
        $left = [int][Math]::Floor(($Width - $renderWidth) / 2)
        $top = [int][Math]::Floor(($Height - $renderHeight) / 2)
        $graphics.DrawImage($source, $left, $top, $renderWidth, $renderHeight)
        return $bitmap
    }
    catch {
        $bitmap.Dispose()
        throw
    }
    finally {
        $graphics.Dispose()
    }
}

function New-BenchmarkAsset {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [int] $Width,
        [Parameter(Mandatory)] [int] $Height
    )

    $bitmap = New-ResizedBitmap -Width $Width -Height $Height
    try {
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
}

function Get-IconPngBytes {
    param([Parameter(Mandatory)] [int] $Size)

    $bitmap = New-ResizedBitmap -Width $Size -Height $Size
    $stream = [System.IO.MemoryStream]::new()
    try {
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return $stream.ToArray()
    }
    finally {
        $stream.Dispose()
        $bitmap.Dispose()
    }
}

try {
    New-BenchmarkAsset -Path (Join-Path $assetsDirectory 'StoreLogo.png') -Width 50 -Height 50
    New-BenchmarkAsset -Path (Join-Path $assetsDirectory 'Square44x44Logo.png') -Width 44 -Height 44
    New-BenchmarkAsset -Path (Join-Path $assetsDirectory 'Square150x150Logo.png') -Width 150 -Height 150
    New-BenchmarkAsset -Path (Join-Path $assetsDirectory 'Wide310x150Logo.png') -Width 310 -Height 150
    New-BenchmarkAsset -Path (Join-Path $assetsDirectory 'AppIcon.png') -Width 256 -Height 256

    $iconSizes = @(16, 24, 32, 48, 64, 128, 256)
    $iconImages = @($iconSizes | ForEach-Object { Get-IconPngBytes -Size $_ })
    $iconPath = Join-Path $assetsDirectory 'AppIcon.ico'
    $stream = [System.IO.File]::Open($iconPath, [System.IO.FileMode]::Create)
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$iconSizes.Count)

        $offset = 6 + (16 * $iconSizes.Count)
        for ($index = 0; $index -lt $iconSizes.Count; $index++) {
            $size = $iconSizes[$index]
            $bytes = $iconImages[$index]
            $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
            $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$bytes.Length)
            $writer.Write([uint32]$offset)
            $offset += $bytes.Length
        }

        foreach ($bytes in $iconImages) {
            $writer.Write($bytes)
        }
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}
finally {
    $source.Dispose()
}
