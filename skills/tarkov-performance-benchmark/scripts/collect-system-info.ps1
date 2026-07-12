param(
    [switch]$IncludePagefile
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\TarkovCommon.ps1"

Get-TarkovSystemInfo -IncludePagefile:$IncludePagefile | ConvertTo-Json -Depth 20
