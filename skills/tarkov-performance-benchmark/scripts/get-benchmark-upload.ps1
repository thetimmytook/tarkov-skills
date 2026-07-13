param(
    [string]$BenchmarkPath = "",
    [switch]$IncludeAll,
    [switch]$MarkUploaded
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "TarkovCommon.ps1")

if (-not $BenchmarkPath) {
    $BenchmarkPath = Join-Path (Get-TarkovDataDir) "benchmark.json"
}
if (-not (Test-Path -LiteralPath $BenchmarkPath)) {
    throw "Benchmark file not found: $BenchmarkPath"
}

$benchmark = Get-Content -Raw -LiteralPath $BenchmarkPath | ConvertFrom-Json
$totalRuns = @($benchmark.runs).Count

$alreadyUploaded = 0
$uploadedProp = $benchmark.PSObject.Properties['uploaded_run_count']
if ($uploadedProp -and $uploadedProp.Value) {
    $alreadyUploaded = [int]$uploadedProp.Value
}
# A trimmed or reset file can hold fewer runs than the recorded upload marker.
if ($alreadyUploaded -gt $totalRuns) {
    $alreadyUploaded = 0
}

$startIndex = if ($IncludeAll) { 0 } else { $alreadyUploaded }
$payloadRuns = @()
if ($totalRuns -gt $startIndex) {
    $payloadRuns = @($benchmark.runs)[$startIndex..($totalRuns - 1)]
}

$payload = [ordered]@{
    schema = $benchmark.schema
    game = $benchmark.game
    system = $benchmark.system
    runs = $payloadRuns
}

if ($MarkUploaded) {
    $benchmark | Add-Member -NotePropertyName "uploaded_run_count" -NotePropertyValue $totalRuns -Force
    $benchmark | Add-Member -NotePropertyName "last_uploaded_at" -NotePropertyValue (Get-Date).ToString("o") -Force
    $json = Hide-TarkovUserPath -Text ($benchmark | ConvertTo-Json -Depth 35)
    $temporaryPath = "$BenchmarkPath.tmp"
    $json | Set-Content -LiteralPath $temporaryPath -Encoding UTF8
    Move-Item -LiteralPath $temporaryPath -Destination $BenchmarkPath -Force
}

[ordered]@{
    benchmark_path = $BenchmarkPath
    total_run_count = $totalRuns
    already_uploaded_count = $alreadyUploaded
    new_run_count = $payloadRuns.Count
    payload_json = ($payload | ConvertTo-Json -Depth 35)
} | ConvertTo-Json -Depth 40
