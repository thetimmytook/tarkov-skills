param(
    [switch]$IncludePagefile
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

& (Join-Path $repoRoot "scripts\collect-system-info.ps1") -IncludePagefile:$IncludePagefile
