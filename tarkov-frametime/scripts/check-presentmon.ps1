param(
    [string]$PresentMonPath = ""
)

$ErrorActionPreference = "Stop"
$skillRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $skillRoot

$candidates = @()
if ($PresentMonPath) {
    $candidates += $PresentMonPath
}
$candidates += Join-Path $repoRoot "tools\PresentMon\PresentMon.exe"
$candidates += @(Get-ChildItem -Path (Join-Path $repoRoot "tools\PresentMon") -Filter "PresentMon*.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)

$pathCommand = Get-Command PresentMon.exe -ErrorAction SilentlyContinue
if ($pathCommand) {
    $candidates += $pathCommand.Source
}

$found = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1

[ordered]@{
    source = "presentmon_check"
    found = [bool]$found
    path = if ($found) { $found } else { "" }
    checked_paths = @($candidates | Where-Object { $_ } | Select-Object -Unique)
    recommendation = if ($found) { "PresentMon is available." } else { "PresentMon was not found. Provide PresentMon.exe path, place it under tools\PresentMon, or parse an existing CSV export." }
} | ConvertTo-Json -Depth 6
