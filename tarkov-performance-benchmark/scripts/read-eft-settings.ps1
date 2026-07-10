param(
    [string]$SettingsDir = "$env:APPDATA\Battlestate Games\Escape from Tarkov\Settings"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

& (Join-Path $repoRoot "scripts\read-tarkov-settings.ps1") -SettingsDir $SettingsDir
