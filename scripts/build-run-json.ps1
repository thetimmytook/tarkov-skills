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
. (Join-Path $PSScriptRoot "TarkovCommon.ps1")

$settings = Get-Content -Raw -LiteralPath $SettingsJsonPath | ConvertFrom-Json
$system = Get-Content -Raw -LiteralPath $SystemJsonPath | ConvertFrom-Json
$fps = Get-Content -Raw -LiteralPath $FpsJsonPath | ConvertFrom-Json

$duration = $fps.duration_sec
$unknownCount = @(@($Map, $Mode, $ServerModel, $Weather, $TimeOfDay) | Where-Object { $_ -eq "unknown" })

# Confidence thresholds are defined in references/measurement-rules.md.
if ($duration -is [double] -or $duration -is [int] -or $duration -is [long]) {
    if ($duration -ge 120 -and $unknownCount.Count -eq 0) {
        $confidence = "high"
    }
    elseif ($duration -ge 60 -and $unknownCount.Count -le 2) {
        $confidence = "medium"
    }
    else {
        $confidence = "low"
    }
}
else {
    $confidence = "low"
}

$run = [ordered]@{
    schema = "tarkov-performance-run/v2"
    created_at = (Get-Date).ToString("o")
    game = "Escape from Tarkov"
    game_version = $GameVersion
    test_context = [ordered]@{
        map = $Map
        mode = $Mode
        server_model = $ServerModel
        weather = $Weather
        time_of_day = $TimeOfDay
        route = $Route
        raid_activity = $RaidActivity
        notes = $Notes
        confidence = $confidence
    }
    fps = $fps
    system = $system
    settings = $settings
}

# run.json is meant for sharing/upload: strip user and host names from all paths.
$json = Hide-TarkovUserPath -Text ($run | ConvertTo-Json -Depth 30)

if ($OutputPath) {
    $json | Set-Content -LiteralPath $OutputPath -Encoding UTF8
}
else {
    $json
}
