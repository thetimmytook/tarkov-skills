param(
    [Parameter(Mandatory = $true)]
    [string]$SettingsJsonPath,

    [Parameter(Mandatory = $true)]
    [string]$SystemJsonPath,

    [Parameter(Mandatory = $true)]
    [string]$FpsJsonPath,

    [Parameter(Mandatory = $true)]
    [ValidateSet("bsg_servers", "local")]
    [string]$Execution,

    [ValidateSet("clear", "cloudy", "rain", "fog", "snow", "unknown")]
    [string]$Weather = "unknown",

    [ValidateSet("day", "night", "dawn_dusk", "unknown")]
    [string]$TimeOfDay = "unknown",

    [string]$Map = "unknown",
    [string]$GameVersion = "unknown",
    [string]$BenchmarkPath = ""
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "TarkovCommon.ps1")

if (-not $BenchmarkPath) {
    $BenchmarkPath = Join-Path (Get-TarkovDataDir) "benchmark.json"
}

# A failed log read upstream yields empty strings instead of "unknown"; empty
# values would wrongly count as known context in the confidence calculation.
if ([string]::IsNullOrWhiteSpace($Map)) {
    $Map = "unknown"
}
if ([string]::IsNullOrWhiteSpace($GameVersion)) {
    $GameVersion = "unknown"
}

$settings = Get-Content -Raw -LiteralPath $SettingsJsonPath | ConvertFrom-Json
$system = Get-Content -Raw -LiteralPath $SystemJsonPath | ConvertFrom-Json
$fps = Get-Content -Raw -LiteralPath $FpsJsonPath | ConvertFrom-Json

function Remove-PrivateCaptureMetadata {
    param(
        [pscustomobject]$Settings,
        [pscustomobject]$Fps
    )

    if ($Settings) {
        [void]$Settings.PSObject.Properties.Remove("settings_dir")
        if ($Settings.files) {
            [void]$Settings.files.PSObject.Properties.Remove("Control.ini")
            [void]$Settings.files.PSObject.Properties.Remove("Sound.ini")
            foreach ($file in $Settings.files.PSObject.Properties) {
                if ($file.Value) {
                    [void]$file.Value.PSObject.Properties.Remove("path")
                }
            }
        }
    }
    if ($Fps) {
        [void]$Fps.PSObject.Properties.Remove("path")
        [void]$Fps.PSObject.Properties.Remove("csv_path")
    }
}

Remove-PrivateCaptureMetadata -Settings $settings -Fps $fps

$duration = $fps.duration_sec
$knownContext = $Map -ne "unknown" -and $Weather -ne "unknown" -and $TimeOfDay -ne "unknown"
if ($duration -is [double] -or $duration -is [int] -or $duration -is [long]) {
    if ($duration -ge 120 -and $knownContext) {
        $confidence = "high"
    }
    elseif ($duration -ge 60) {
        $confidence = "medium"
    }
    else {
        $confidence = "low"
    }
}
else {
    $confidence = "low"
}

if (Test-Path -LiteralPath $BenchmarkPath) {
    $benchmark = Get-Content -Raw -LiteralPath $BenchmarkPath | ConvertFrom-Json
    if ($benchmark.schema -ne "tarkov-performance-benchmark/v1") {
        throw "Benchmark file has an unsupported schema: $($benchmark.schema)"
    }
}
else {
    $benchmark = [ordered]@{
        schema = "tarkov-performance-benchmark/v1"
        created_at = (Get-Date).ToString("o")
        game = "Escape from Tarkov"
        system = $system
        runs = @()
    }
}

foreach ($existingRun in @($benchmark.runs)) {
    Remove-PrivateCaptureMetadata -Settings $existingRun.settings -Fps $existingRun.fps
}

$run = [ordered]@{
    captured_at = (Get-Date).ToString("o")
    game_version = $GameVersion
    settings = $settings
    context = [ordered]@{
        map = $Map
        execution = $Execution
        weather = $Weather
        time_of_day = $TimeOfDay
        confidence = $confidence
    }
    fps = $fps
}

$benchmark.system = $system
if ($benchmark -is [hashtable]) {
    $benchmark["updated_at"] = (Get-Date).ToString("o")
}
else {
    $benchmark | Add-Member -NotePropertyName "updated_at" -NotePropertyValue (Get-Date).ToString("o") -Force
}
$benchmark.runs = @($benchmark.runs) + @($run)

$json = Hide-TarkovUserPath -Text ($benchmark | ConvertTo-Json -Depth 35)
$directory = Split-Path -Parent $BenchmarkPath
New-Item -ItemType Directory -Force $directory | Out-Null
$temporaryPath = "$BenchmarkPath.tmp"
$json | Set-Content -LiteralPath $temporaryPath -Encoding UTF8
Move-Item -LiteralPath $temporaryPath -Destination $BenchmarkPath -Force

[ordered]@{
    benchmark_path = $BenchmarkPath
    run_count = @($benchmark.runs).Count
    run = $run
} | ConvertTo-Json -Depth 35
