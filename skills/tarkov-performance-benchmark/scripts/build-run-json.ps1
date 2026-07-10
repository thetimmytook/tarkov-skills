param(
    [Parameter(Mandatory = $true)]
    [string]$SettingsJsonPath,

    [Parameter(Mandatory = $true)]
    [string]$SystemJsonPath,

    [Parameter(Mandatory = $true)]
    [string]$FpsJsonPath,

    [string]$Map = "unknown",
    [string]$Mode = "unknown",
    [string]$ServerModel = "unknown",
    [string]$Weather = "unknown",
    [string]$TimeOfDay = "unknown",
    [string]$Route = "unknown",
    [string]$RaidActivity = "unknown",
    [string]$GameVersion = "unknown",
    [string]$Notes = "",
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))

& (Join-Path $repoRoot "scripts\build-run-json.ps1") @PSBoundParameters
