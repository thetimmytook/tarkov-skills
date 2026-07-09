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
    [string]$Notes = "",
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

$settings = Get-Content -Raw -LiteralPath $SettingsJsonPath | ConvertFrom-Json
$system = Get-Content -Raw -LiteralPath $SystemJsonPath | ConvertFrom-Json
$fps = Get-Content -Raw -LiteralPath $FpsJsonPath | ConvertFrom-Json

$duration = $fps.duration_sec
$unknownCount = @($Map, $Mode, $ServerModel, $Weather, $TimeOfDay) | Where-Object { $_ -eq "unknown" }

if ($duration -is [double] -or $duration -is [int]) {
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
    schema = "tarkov-performance-run/v1"
    created_at = (Get-Date).ToString("o")
    game = "Escape from Tarkov"
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

$json = $run | ConvertTo-Json -Depth 30

if ($OutputPath) {
    $json | Set-Content -LiteralPath $OutputPath -Encoding UTF8
}
else {
    $json
}
