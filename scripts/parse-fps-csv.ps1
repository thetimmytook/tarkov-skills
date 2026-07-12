param(
    [Parameter(Mandatory = $true)]
    [string]$Path
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Path)) {
    throw "CSV file not found: $Path"
}

$allLines = [System.IO.File]::ReadAllLines((Resolve-Path -LiteralPath $Path).Path)

# CapFrameX and some tools prepend comment/metadata lines before the header.
$startIndex = 0
while ($startIndex -lt $allLines.Count) {
    $trimmed = $allLines[$startIndex].TrimStart()
    if ($trimmed.Length -gt 0 -and -not $trimmed.StartsWith("#") -and -not $trimmed.StartsWith("//")) {
        break
    }
    $startIndex++
}
if ($startIndex -ge $allLines.Count) {
    throw "CSV file is empty or could not be parsed: $Path"
}

# European locales export semicolon- or tab-separated files with decimal commas.
$headerLine = $allLines[$startIndex]
$delimiter = ","
$bestCount = ($headerLine -split ',').Count
foreach ($candidate in @(";", "`t")) {
    $count = ($headerLine -split [regex]::Escape($candidate)).Count
    if ($count -gt $bestCount) {
        $bestCount = $count
        $delimiter = $candidate
    }
}

$rows = @($allLines[$startIndex..($allLines.Count - 1)] | ConvertFrom-Csv -Delimiter $delimiter)
if (-not $rows -or $rows.Count -eq 0) {
    throw "CSV file has a header but no data rows: $Path"
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
        [System.Collections.Generic.List[double]]$SortedValues,
        [double]$Percentile
    )

    if ($SortedValues.Count -eq 0) {
        return $null
    }

    $index = [math]::Ceiling(($Percentile / 100.0) * $SortedValues.Count) - 1
    $index = [math]::Max(0, [math]::Min($SortedValues.Count - 1, $index))
    return [double]$SortedValues[$index]
}

function Read-NumericColumn {
    param(
        [object[]]$Rows,
        [string]$Header
    )

    $values = [System.Collections.Generic.List[double]]::new()
    $invariant = [Globalization.CultureInfo]::InvariantCulture
    foreach ($row in $Rows) {
        $raw = [string]$row.$Header
        $parsed = 0.0
        if ([double]::TryParse(($raw -replace ',', '.'), [Globalization.NumberStyles]::Float, $invariant, [ref]$parsed) -and $parsed -gt 0) {
            $values.Add($parsed)
        }
    }
    return $values
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

if (-not $frametimeHeader -and -not $fpsHeader) {
    throw "Could not find a frametime or FPS column. Headers: $($headers -join ', ')"
}

if ($frametimeHeader) {
    $frametimes = Read-NumericColumn -Rows $rows -Header $frametimeHeader
    if ($frametimes.Count -eq 0) {
        throw "Column '$frametimeHeader' contains no usable numeric values: $Path"
    }

    $totalMs = 0.0
    $sorted = [System.Collections.Generic.List[double]]::new()
    foreach ($value in $frametimes) {
        $totalMs += $value
        $sorted.Add($value)
    }
    $totalSeconds = $totalMs / 1000.0
    $avgFps = if ($totalSeconds -gt 0) { $frametimes.Count / $totalSeconds } else { $null }

    $sorted.Sort()

    $onePercentCount = [math]::Max(1, [math]::Ceiling($sorted.Count * 0.01))
    $pointOnePercentCount = [math]::Max(1, [math]::Ceiling($sorted.Count * 0.001))

    # 1%/0.1% lows: average of the slowest N frametimes (CapFrameX-style low averages).
    $slowestSum = 0.0
    for ($i = $sorted.Count - $onePercentCount; $i -lt $sorted.Count; $i++) {
        $slowestSum += $sorted[$i]
    }
    $onePercentAvgMs = $slowestSum / $onePercentCount

    $slowestSum = 0.0
    for ($i = $sorted.Count - $pointOnePercentCount; $i -lt $sorted.Count; $i++) {
        $slowestSum += $sorted[$i]
    }
    $pointOnePercentAvgMs = $slowestSum / $pointOnePercentCount

    $result = [ordered]@{
        source = "fps_csv"
        method = "frametime_ms"
        captured_at = (Get-Date).ToString("o")
        detected_column = $frametimeHeader
        detected_delimiter = if ($delimiter -eq "`t") { "tab" } else { $delimiter }
        sample_count = $frametimes.Count
        duration_sec = [math]::Round($totalSeconds, 3)
        avg_fps = [math]::Round($avgFps, 2)
        p1_low_fps = [math]::Round(1000.0 / $onePercentAvgMs, 2)
        p0_1_low_fps = [math]::Round(1000.0 / $pointOnePercentAvgMs, 2)
        avg_frametime_ms = [math]::Round($totalMs / $frametimes.Count, 3)
        p95_frametime_ms = [math]::Round((Get-Percentile -SortedValues $sorted -Percentile 95), 3)
        p99_frametime_ms = [math]::Round((Get-Percentile -SortedValues $sorted -Percentile 99), 3)
    }
}
else {
    $fpsSamples = Read-NumericColumn -Rows $rows -Header $fpsHeader
    if ($fpsSamples.Count -eq 0) {
        throw "Column '$fpsHeader' contains no usable numeric values: $Path"
    }

    $sortedFps = [System.Collections.Generic.List[double]]::new()
    $fpsSum = 0.0
    foreach ($value in $fpsSamples) {
        $fpsSum += $value
        $sortedFps.Add($value)
    }
    $avgFps = $fpsSum / $fpsSamples.Count

    $sortedFps.Sort()

    $onePercentCount = [math]::Max(1, [math]::Ceiling($sortedFps.Count * 0.01))
    $pointOnePercentCount = [math]::Max(1, [math]::Ceiling($sortedFps.Count * 0.001))

    $lowSum = 0.0
    for ($i = 0; $i -lt $onePercentCount; $i++) {
        $lowSum += $sortedFps[$i]
    }
    $onePercentLow = $lowSum / $onePercentCount

    $lowSum = 0.0
    for ($i = 0; $i -lt $pointOnePercentCount; $i++) {
        $lowSum += $sortedFps[$i]
    }
    $pointOnePercentLow = $lowSum / $pointOnePercentCount

    $result = [ordered]@{
        source = "fps_csv"
        method = "fps_samples"
        captured_at = (Get-Date).ToString("o")
        detected_column = $fpsHeader
        detected_delimiter = if ($delimiter -eq "`t") { "tab" } else { $delimiter }
        sample_count = $fpsSamples.Count
        duration_sec = "unknown"
        avg_fps = [math]::Round($avgFps, 2)
        p1_low_fps = [math]::Round($onePercentLow, 2)
        p0_1_low_fps = [math]::Round($pointOnePercentLow, 2)
        avg_frametime_ms = if ($avgFps -gt 0) { [math]::Round(1000.0 / $avgFps, 3) } else { $null }
        p95_frametime_ms = "unknown"
        p99_frametime_ms = "unknown"
    }
}

$result | ConvertTo-Json -Depth 8
