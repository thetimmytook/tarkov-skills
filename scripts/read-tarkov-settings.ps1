param(
    [string]$SettingsDir = "$env:APPDATA\Battlestate Games\Escape from Tarkov\Settings",
    [string[]]$Files = @("Graphics.ini", "PostFx.ini", "Game.ini")
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\TarkovCommon.ps1"

Read-TarkovSettings -SettingsDir $SettingsDir -Files $Files | ConvertTo-Json -Depth 30
