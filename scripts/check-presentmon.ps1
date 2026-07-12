param(
    [string]$PresentMonPath = ""
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "TarkovCommon.ps1")

$dataToolsDir = Get-TarkovDataDir -SubDir "tools\PresentMon"
$repoToolsDir = Join-Path (Split-Path -Parent $PSScriptRoot) "tools\PresentMon"

$candidates = @()
if ($PresentMonPath) {
    $candidates += $PresentMonPath
}
$candidates += Join-Path $dataToolsDir "PresentMon.exe"
$candidates += @(Get-ChildItem -Path $dataToolsDir -Filter "PresentMon*.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)
$candidates += Join-Path $repoToolsDir "PresentMon.exe"
$candidates += @(Get-ChildItem -Path $repoToolsDir -Filter "PresentMon*.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)

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
    recommendation = if ($found) { "PresentMon is available." } else { "Download PresentMon and place PresentMon.exe under $dataToolsDir." }
} | ConvertTo-Json -Depth 6
