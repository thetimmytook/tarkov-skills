param(
    [int]$DurationSec = 120,
    [string]$OutputDir = "",
    [string]$PresentMonPath = "",
    [string]$ProcessName = "EscapeFromTarkov.exe"
)

$ErrorActionPreference = "Stop"

if (-not $OutputDir) {
    $skillRoot = Split-Path -Parent $PSScriptRoot
    $OutputDir = Join-Path $skillRoot "captures"
}
New-Item -ItemType Directory -Force $OutputDir | Out-Null

$checkJson = & (Join-Path $PSScriptRoot "check-presentmon.ps1") -PresentMonPath $PresentMonPath
$check = $checkJson | ConvertFrom-Json
if (-not $check.found) {
    throw "PresentMon was not found. Provide -PresentMonPath, place PresentMon.exe under tools\PresentMon, or parse an existing CSV export."
}

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$csvPath = Join-Path $OutputDir "presentmon_$stamp.csv"

# PresentMon CLI flags differ across releases. This conservative command targets common console builds.
$arguments = @(
    "-process_name", $ProcessName,
    "-timed", $DurationSec,
    "-output_file", $csvPath
)

$process = Start-Process -FilePath $check.path -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -ne 0) {
    throw "PresentMon exited with code $($process.ExitCode). Parse an existing CSV export if this release has different CLI flags."
}

if (-not (Test-Path -LiteralPath $csvPath)) {
    throw "PresentMon finished but CSV was not created: $csvPath"
}

$parsedJson = & (Join-Path $PSScriptRoot "parse-fps-csv.ps1") -Path $csvPath
$parsed = $parsedJson | ConvertFrom-Json

[ordered]@{
    source = "presentmon_capture"
    presentmon_path = $check.path
    process_name = $ProcessName
    requested_duration_sec = $DurationSec
    csv_path = $csvPath
    parsed = $parsed
} | ConvertTo-Json -Depth 10
