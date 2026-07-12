param(
    [ValidateSet(120, 240)]
    [int]$DurationSec = 120,

    [string]$OutputDir = "",
    [string]$PresentMonPath = "",
    [string]$ProcessName = "EscapeFromTarkov.exe",
    [switch]$RequestElevation
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "TarkovCommon.ps1")

if (-not $OutputDir) {
    $OutputDir = Get-TarkovDataDir -SubDir "captures"
}
New-Item -ItemType Directory -Force $OutputDir | Out-Null

$check = & (Join-Path $PSScriptRoot "check-presentmon.ps1") -PresentMonPath $PresentMonPath | ConvertFrom-Json
if (-not $check.found) {
    throw "PresentMon was not found. Download it from https://github.com/GameTechDev/PresentMon/releases and place PresentMon.exe under $($check.preferred_install_dir)."
}

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$csvPath = Join-Path $OutputDir "presentmon_$stamp.csv"
$arguments = @(
    "-process_name", $ProcessName,
    "-timed", $DurationSec,
    "-terminate_after_timed",
    "-output_file", $csvPath
)

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
$isElevated = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

 $process = Start-Process -FilePath $check.path -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -ne 0 -and $RequestElevation -and -not $isElevated) {
    # Most user-mode captures work without UAC. Retry elevated only after a failed attempt.
    Remove-Item -LiteralPath $csvPath -Force -ErrorAction SilentlyContinue
    $process = Start-Process -FilePath $check.path -ArgumentList $arguments -Verb RunAs -Wait -PassThru
}

if ($process.ExitCode -ne 0) {
    throw "PresentMon exited with code $($process.ExitCode)."
}
if (-not (Test-Path -LiteralPath $csvPath)) {
    throw "PresentMon finished but did not create a CSV capture."
}

$parsed = & (Join-Path $PSScriptRoot "parse-fps-csv.ps1") -Path $csvPath | ConvertFrom-Json
[ordered]@{
    source = "presentmon_capture"
    requested_duration_sec = $DurationSec
    parsed = $parsed
} | ConvertTo-Json -Depth 12
