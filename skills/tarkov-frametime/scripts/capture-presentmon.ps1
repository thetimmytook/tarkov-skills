param(
    [int]$DurationSec = 120,
    [string]$OutputDir = "",
    [string]$PresentMonPath = "",
    [string]$ProcessName = "EscapeFromTarkov.exe"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
. (Join-Path $repoRoot "scripts\TarkovCommon.ps1")

if (-not $OutputDir) {
    $OutputDir = Get-TarkovDataDir -SubDir "captures"
}
New-Item -ItemType Directory -Force $OutputDir | Out-Null

$checkJson = & (Join-Path $PSScriptRoot "check-presentmon.ps1") -PresentMonPath $PresentMonPath
$check = $checkJson | ConvertFrom-Json
if (-not $check.found) {
    throw "PresentMon was not found. Provide -PresentMonPath, place PresentMon.exe under $($check.preferred_install_dir), or parse an existing CSV export."
}

# PresentMon starts an ETW trace session, which normally requires elevation
# (or membership in the Performance Log Users group).
$isElevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$csvPath = Join-Path $OutputDir "presentmon_$stamp.csv"

# PresentMon CLI flags differ across releases. This conservative command targets
# common console builds. -terminate_after_timed is required: without it PresentMon
# keeps running after the timed capture ends and -Wait never returns.
$arguments = @(
    "-process_name", $ProcessName,
    "-timed", $DurationSec,
    "-terminate_after_timed",
    "-output_file", $csvPath
)

$process = Start-Process -FilePath $check.path -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -ne 0) {
    $elevationHint = if ($isElevated) { "" } else { " PresentMon usually needs an elevated (Run as administrator) console for ETW capture; this session is not elevated." }
    throw "PresentMon exited with code $($process.ExitCode).$elevationHint If this release has different CLI flags, capture manually and parse the exported CSV."
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
    elevated = $isElevated
    csv_path = $csvPath
    parsed = $parsed
} | ConvertTo-Json -Depth 10
