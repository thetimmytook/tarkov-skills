param(
    [string]$LogsDir = "",
    [int]$MaxFolders = 5
)

$ErrorActionPreference = "Stop"

function Get-DefaultTarkovLogsDir {
    $registryPaths = @(
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\EscapeFromTarkov",
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 3932890"
    )

    foreach ($registryPath in $registryPaths) {
        $item = Get-ItemProperty -Path $registryPath -ErrorAction SilentlyContinue
        if (-not $item -or -not $item.InstallLocation) {
            continue
        }

        $candidates = @(
            (Join-Path $item.InstallLocation "Logs"),
            (Join-Path $item.InstallLocation "build\Logs")
        )

        foreach ($candidate in $candidates) {
            if (Test-Path -LiteralPath $candidate -PathType Container) {
                return $candidate
            }
        }
    }

    return ""
}

function Convert-MapBundle {
    param([string]$BundleName)

    $mapBundles = @{
        "city_preset" = "TarkovStreets"
        "customs_preset" = "bigmap"
        "factory_day_preset" = "factory4_day"
        "factory_night_preset" = "factory4_night"
        "laboratory_preset" = "laboratory"
        "labyrinth_preset" = "Labyrinth"
        "lighthouse_preset" = "Lighthouse"
        "rezerv_base_preset" = "RezervBase"
        "sandbox_preset" = "Sandbox"
        "sandbox_high_preset" = "Sandbox_high"
        "shopping_mall" = "Interchange"
        "shoreline_preset" = "Shoreline"
        "woods_preset" = "Woods"
    }

    if ($mapBundles.ContainsKey($BundleName)) {
        return $mapBundles[$BundleName]
    }

    return $BundleName
}

function Convert-MapDisplayName {
    param([string]$MapId)

    $names = @{
        "TarkovStreets" = "Streets of Tarkov"
        "bigmap" = "Customs"
        "factory4_day" = "Factory"
        "factory4_night" = "Factory"
        "laboratory" = "The Lab"
        "Labyrinth" = "Labyrinth"
        "Lighthouse" = "Lighthouse"
        "RezervBase" = "Reserve"
        "Sandbox" = "Ground Zero"
        "Sandbox_high" = "Ground Zero"
        "Interchange" = "Interchange"
        "Shoreline" = "Shoreline"
        "Woods" = "Woods"
    }

    if ($names.ContainsKey($MapId)) {
        return $names[$MapId]
    }

    return $MapId
}

function Get-LogEventTime {
    param([string]$Line)

    if ($Line -match '^(?<time>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+)') {
        return $Matches["time"]
    }
    if ($Line -match '^(?<time>\d{4}\.\d{2}\.\d{2}_\d{2}-\d{2}-\d{2})') {
        return $Matches["time"]
    }
    return ""
}

if (-not $LogsDir) {
    $LogsDir = Get-DefaultTarkovLogsDir
}

if (-not $LogsDir -or -not (Test-Path -LiteralPath $LogsDir -PathType Container)) {
    [ordered]@{
        source = "tarkov_logs"
        found = $false
        logs_dir = $LogsDir
        confidence = "none"
        message = "Tarkov logs folder was not found."
    } | ConvertTo-Json -Depth 8
    exit 0
}

$logFolders = @(Get-ChildItem -LiteralPath $LogsDir -Directory -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First $MaxFolders)
$events = New-Object System.Collections.ArrayList
$context = [ordered]@{
    source = "tarkov_logs"
    found = $true
    logs_dir = $LogsDir
    log_folder = ""
    log_files = @()
    map_id = "unknown"
    map = "unknown"
    online = $null
    mode = "unknown"
    server_model = "unknown"
    raid_id = "unknown"
    game_version = "unknown"
    queue_time_sec = $null
    map_load_time_sec = $null
    started_at = ""
    starting_at = ""
    ended_at = ""
    confidence = "low"
    events = $events
}

foreach ($folder in $logFolders) {
    $files = @(Get-ChildItem -LiteralPath $folder.FullName -File -Filter "*.log" | Where-Object {
        $_.Name -match "application|backend|output"
    } | Sort-Object LastWriteTime)

    foreach ($file in $files) {
        $lastEventTime = ""
        $lines = Get-Content -LiteralPath $file.FullName -ErrorAction SilentlyContinue
        foreach ($line in $lines) {
            $eventTime = Get-LogEventTime -Line $line
            if ($eventTime) {
                $lastEventTime = $eventTime
            }

            if ($context.game_version -eq "unknown" -and $line -match '(?i)\bversion\b.*?(?<ver>\d+\.\d+\.\d+\.\d+(\.\d+)?)') {
                $context.game_version = $Matches["ver"]
            }

            if ($line -match 'scene preset path:maps\/(?<bundle>[a-zA-Z0-9_]+)\.bundle') {
                $mapId = Convert-MapBundle -BundleName $Matches["bundle"]
                $context.map_id = $mapId
                $context.map = Convert-MapDisplayName -MapId $mapId
                $context.log_folder = $folder.FullName
                [void]$events.Add([ordered]@{ type = "map_loading"; time = $eventTime; value = $mapId; file = $file.Name })
            }

            if ($line -match 'LocationLoaded:[0-9.,]+ real:(?<loadTime>[0-9.,]+)') {
                $context.map_load_time_sec = [double](($Matches["loadTime"] -replace ",", "."))
                $context.log_folder = $folder.FullName
                [void]$events.Add([ordered]@{ type = "location_loaded"; time = $eventTime; value = $context.map_load_time_sec; file = $file.Name })
            }

            if ($line -match 'MatchingCompleted:[0-9.,]+ real:(?<queueTime>[0-9.,]+)') {
                $context.queue_time_sec = [double](($Matches["queueTime"] -replace ",", "."))
                $context.log_folder = $folder.FullName
                [void]$events.Add([ordered]@{ type = "matching_completed"; time = $eventTime; value = $context.queue_time_sec; file = $file.Name })
            }

            if ($line -match 'TRACE-NetworkGameCreate profileStatus') {
                if ($line -match 'Location: (?<map>[^,]+)') {
                    $context.map_id = $Matches["map"]
                    $context.map = Convert-MapDisplayName -MapId $context.map_id
                }
                if ($line -match 'RaidMode: (?<raidMode>\w+)') {
                    $context.online = ($Matches["raidMode"] -eq "Online")
                    $context.server_model = if ($context.online) { "BSG server" } else { "local/offline" }
                    $context.mode = if ($context.online) { "online" } else { "offline/local" }
                }
                if ($line -match 'shortId: (?<raidId>[A-Z0-9]{6})') {
                    $context.raid_id = $Matches["raidId"]
                }
                $context.log_folder = $folder.FullName
                [void]$events.Add([ordered]@{ type = "network_game_create"; time = $eventTime; value = $context.raid_id; file = $file.Name })
            }

            if ($line -match 'application\|GameStarting') {
                $context.starting_at = $eventTime
                $context.log_folder = $folder.FullName
                [void]$events.Add([ordered]@{ type = "game_starting"; time = $eventTime; value = ""; file = $file.Name })
            }

            if ($line -match 'application\|GameStarted') {
                $context.started_at = $eventTime
                $context.ended_at = ""
                $context.log_folder = $folder.FullName
                [void]$events.Add([ordered]@{ type = "game_started"; time = $eventTime; value = ""; file = $file.Name })
            }

            if ($line -match 'Got notification \| UserMatchOver') {
                $context.ended_at = $eventTime
                if ($line -match '"location"\s*:\s*"(?<location>[^"]+)"') {
                    $context.map_id = $Matches["location"]
                    $context.map = Convert-MapDisplayName -MapId $context.map_id
                }
                if ($line -match '"shortId"\s*:\s*"(?<raidId>[^"]+)"') {
                    $context.raid_id = $Matches["raidId"]
                }
                $context.log_folder = $folder.FullName
                [void]$events.Add([ordered]@{ type = "match_over"; time = $eventTime; value = $context.raid_id; file = $file.Name })
            }

            if ($context.started_at -and $line -match 'EFT\.HideoutGameLoader:OnHideoutStart\(\)') {
                $context.ended_at = if ($eventTime) { $eventTime } else { $lastEventTime }
                $context.log_folder = $folder.FullName
                [void]$events.Add([ordered]@{ type = "hideout_started"; time = $context.ended_at; value = ""; file = $file.Name })
            }

            if ($context.started_at -and $line -match 'backend\|.*?/client/hideout/(settings|areas)') {
                $context.ended_at = $eventTime
                $context.log_folder = $folder.FullName
                [void]$events.Add([ordered]@{ type = "hideout_backend"; time = $eventTime; value = ""; file = $file.Name })
            }
        }
    }

    if ($context.log_folder) {
        $context.log_files = @($files | Select-Object -ExpandProperty FullName)

        # EFT log folder names embed the game build, e.g. log_2026.07.09_21-08-15_0.16.8.1.12345.
        if ($context.game_version -eq "unknown" -and $folder.Name -match '_(?<ver>\d+(\.\d+){3,4})$') {
            $context.game_version = $Matches["ver"]
        }
        break
    }
}

if ($context.map -ne "unknown" -and $context.raid_id -ne "unknown") {
    $context.confidence = "high"
}
elseif ($context.map -ne "unknown") {
    $context.confidence = "medium"
}
else {
    $context.confidence = "low"
}

$context | ConvertTo-Json -Depth 12
