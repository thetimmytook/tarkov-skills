param(
    [string]$LogsDir = "",
    [int]$MaxFolders = 5
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))

& (Join-Path $repoRoot "scripts\read-tarkov-raid-context.ps1") -LogsDir $LogsDir -MaxFolders $MaxFolders
