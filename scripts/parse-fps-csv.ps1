param(
    [Parameter(Mandatory = $true)]
    [string]$Path
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Path)) {
    throw "CSV file not found: $Path"
}

$rows = Import-Csv -LiteralPath $Path
if (-not $rows -or $rows.Count -eq 0) {
    throw "CSV file is empty or could not be parsed: $Path"
}

$headers = @($rows[0].PSObject.Properties.Name)

function Find-Header {
    param(
        [string[]]$Names,
        [string[]]$Patterns
    )

    foreach ($pattern in $Patterns) {
        $match = $Names | Where-Object { $_ -match $pattern } | Select-Object -First 1
        if ($match) {
            return $match
        }
    }

    return $null
}

function Get-Percentile {
    param(
        [double[]]$Values,
        [double]$Percentile
    )

    if ($Values.Count -eq 0) {
        return $null
    }

    $sorted = @($Values | Sort-Object)
    $index = [math]::Ceiling(($Percentile / 100.0) * $sorted.Count) - 1
    $index = [math]::Max(0, [math]::Min($sorted.Count - 1, $index))
    return [double]$sorted[$index]
}

$frametimeHeader = Find-Header -Names $headers -Patterns @(
    '^(MsBetweenPresents|msBetweenPresents)$',
    '^(Frame ?Time|Frametime|FrameTime).*(ms)?$',
    '.*frame.*time.*'
)

$fpsHeader = Find-Header -Names $headers -Patterns @(
    '^FPS$',
    '.*frames.*per.*second.*',
    '.*fps.*'
)

$frametimes = @()
$fpsSamples = @()

if ($frametimeHeader) {
    foreach ($row in $rows) {
        $value = $row.$frametimeHeader
        $parsed = 0.0
        if ([double]::TryParse(($value -replace ',', '.'), [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$parsed) -and $parsed -gt 0) {
            $frametimes += $parsed
        }
    }
}
elseif ($fpsHeader) {
    foreach ($row in $rows) {
        $value = $row.$fpsHeader
        $parsed = 0.0
        if ([double]::TryParse(($value -replace ',', '.'), [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$parsed) -and $parsed -gt 0) {
            $fpsSamples += $parsed
        }
    }
}
else {
    throw "Could not find a frametime or FPS column. Headers: $($headers -join ', ')"
}

if ($frametimes.Count -gt 0) {
    $totalSeconds = (($frametimes | Measure-Object -Sum).Sum) / 1000.0
    $avgFps = if ($totalSeconds -gt 0) { $frametimes.Count / $totalSeconds } else { $null }
    $slowest = @($frametimes | Sort-Object -Descending)
    $onePercentCount = [math]::Max(1, [math]::Ceiling($slowest.Count * 0.01))
    $pointOnePercentCount = [math]::Max(1, [math]::Ceiling($slowest.Count * 0.001))
    $onePercentAvgMs = (($slowest | Select-Object -First $onePercentCount) | Measure-Object -Average).Average
    $pointOnePercentAvgMs = (($slowest | Select-Object -First $pointOnePercentCount) | Measure-Object -Average).Average

    $result = [ordered]@{
        source = "fps_csv"
        method = "frametime_ms"
        path = $Path
        captured_at = (Get-Date).ToString("o")
        detected_column = $frametimeHeader
        sample_count = $frametimes.Count
        duration_sec = [math]::Round($totalSeconds, 3)
        avg_fps = [math]::Round($avgFps, 2)
        p1_low_fps = [math]::Round(1000.0 / $onePercentAvgMs, 2)
        p0_1_low_fps = [math]::Round(1000.0 / $pointOnePercentAvgMs, 2)
        avg_frametime_ms = [math]::Round((($frametimes | Measure-Object -Average).Average), 3)
        p95_frametime_ms = [math]::Round((Get-Percentile -Values $frametimes -Percentile 95), 3)
        p99_frametime_ms = [math]::Round((Get-Percentile -Values $frametimes -Percentile 99), 3)
    }
}
else {
    $avgFps = ($fpsSamples | Measure-Object -Average).Average
    $sortedFps = @($fpsSamples | Sort-Object)
    $onePercentCount = [math]::Max(1, [math]::Ceiling($sortedFps.Count * 0.01))
    $pointOnePercentCount = [math]::Max(1, [math]::Ceiling($sortedFps.Count * 0.001))

    $result = [ordered]@{
        source = "fps_csv"
        method = "fps_samples"
        path = $Path
        captured_at = (Get-Date).ToString("o")
        detected_column = $fpsHeader
        sample_count = $fpsSamples.Count
        duration_sec = "unknown"
        avg_fps = [math]::Round($avgFps, 2)
        p1_low_fps = [math]::Round(((($sortedFps | Select-Object -First $onePercentCount) | Measure-Object -Average).Average), 2)
        p0_1_low_fps = [math]::Round(((($sortedFps | Select-Object -First $pointOnePercentCount) | Measure-Object -Average).Average), 2)
        avg_frametime_ms = if ($avgFps -gt 0) { [math]::Round(1000.0 / $avgFps, 3) } else { $null }
        p95_frametime_ms = "unknown"
        p99_frametime_ms = "unknown"
    }
}

$result | ConvertTo-Json -Depth 8
