param(
    [string]$PresentMonPath = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
. (Join-Path $repoRoot "scripts\TarkovCommon.ps1")

$dataToolsDir = Get-TarkovDataDir -SubDir "tools\PresentMon"

$candidates = @()
if ($PresentMonPath) {
    $candidates += $PresentMonPath
}
$candidates += Join-Path $dataToolsDir "PresentMon.exe"
$candidates += @(Get-ChildItem -Path $dataToolsDir -Filter "PresentMon*.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)
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
    preferred_install_dir = $dataToolsDir
    checked_paths = @($candidates | Where-Object { $_ } | Select-Object -Unique)
    recommendation = if ($found) { "PresentMon is available." } else { "PresentMon was not found. Place PresentMon.exe under $dataToolsDir, provide -PresentMonPath, or parse an existing CSV export." }
} | ConvertTo-Json -Depth 6
