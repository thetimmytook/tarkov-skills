[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$assetsDirectory = Join-Path $PSScriptRoot 'Assets'
New-Item -ItemType Directory -Path $assetsDirectory -Force | Out-Null

function New-BenchmarkAsset {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [int] $Width,
        [Parameter(Mandatory)] [int] $Height
    )

    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([System.Drawing.ColorTranslator]::FromHtml('#141514'))

        $scale = [Math]::Min($Width, $Height)
        $left = ($Width - ($scale * 0.72)) / 2
        $top = ($Height - ($scale * 0.56)) / 2
        $step = $scale * 0.12
        [System.Drawing.PointF[]] $points = @(
            [System.Drawing.PointF]::new([single]$left, [single]($top + $scale * 0.38)),
            [System.Drawing.PointF]::new([single]($left + $step), [single]($top + $scale * 0.31)),
            [System.Drawing.PointF]::new([single]($left + $step * 2), [single]($top + $scale * 0.36)),
            [System.Drawing.PointF]::new([single]($left + $step * 3), [single]($top + $scale * 0.15)),
            [System.Drawing.PointF]::new([single]($left + $step * 4), [single]($top + $scale * 0.28)),
            [System.Drawing.PointF]::new([single]($left + $step * 5), [single]($top + $scale * 0.18)),
            [System.Drawing.PointF]::new([single]($left + $step * 6), [single]($top + $scale * 0.23))
        )

        $lineWidth = [Math]::Max(2, $scale * 0.055)
        $linePen = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#AB9E6F'), [single]$lineWidth)
        $linePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $linePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $linePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        try {
            $graphics.DrawLines($linePen, $points)
        }
        finally {
            $linePen.Dispose()
        }

        $markerSize = [Math]::Max(4, $scale * 0.13)
        $markerBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#5C9A5A'))
        try {
            $marker = $points[3]
            $graphics.FillEllipse($markerBrush, [single]($marker.X - $markerSize / 2), [single]($marker.Y - $markerSize / 2), [single]$markerSize, [single]$markerSize)
        }
        finally {
            $markerBrush.Dispose()
        }

        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

New-BenchmarkAsset -Path (Join-Path $assetsDirectory 'StoreLogo.png') -Width 50 -Height 50
New-BenchmarkAsset -Path (Join-Path $assetsDirectory 'Square44x44Logo.png') -Width 44 -Height 44
New-BenchmarkAsset -Path (Join-Path $assetsDirectory 'Square150x150Logo.png') -Width 150 -Height 150
New-BenchmarkAsset -Path (Join-Path $assetsDirectory 'Wide310x150Logo.png') -Width 310 -Height 150
