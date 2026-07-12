param(
    [string]$SettingsDir = "$env:APPDATA\Battlestate Games\Escape from Tarkov\Settings",
    [string]$JsonOutputPath = "",
    [string]$MarkdownOutputPath = "",
    [string]$Goal = "",
    [int]$TargetFpsMin = 0,
    [string]$QualityPreference = "",
    [switch]$SaveGoal,
    [string]$GoalMemoryPath = ""
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "TarkovCommon.ps1")

if (-not $GoalMemoryPath) {
    $GoalMemoryPath = Join-Path (Get-TarkovDataDir -SubDir "memory") "current-goal.json"

    # Migrate goal memory saved by older versions inside the skill folder.
    $legacyGoalPath = Join-Path (Split-Path -Parent $PSScriptRoot) "memory\current-goal.json"
    if (-not (Test-Path -LiteralPath $GoalMemoryPath) -and (Test-Path -LiteralPath $legacyGoalPath)) {
        Copy-Item -LiteralPath $legacyGoalPath -Destination $GoalMemoryPath
    }
}

function Get-DefaultGoal {
    return [ordered]@{
        goal = "stable-fps"
        target_fps_min = 60
        quality_preference = "balanced visibility/performance"
        notes = "Default target: stable playable 50-60 FPS minimum when realistic."
        updated_at = $null
        source = "default"
    }
}

function Read-GoalMemory {
    param([string]$Path)

    # Merge over defaults so a goal file written by an older schema cannot
    # break property assignment later.
    $goal = Get-DefaultGoal
    if (Test-Path -LiteralPath $Path) {
        $saved = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
        foreach ($key in @($goal.Keys)) {
            $savedProp = $saved.PSObject.Properties[$key]
            if ($savedProp -and $null -ne $savedProp.Value) {
                $goal[$key] = $savedProp.Value
            }
        }
    }
    return $goal
}

function Save-GoalMemory {
    param(
        [string]$Path,
        $GoalObject
    )

    $dir = Split-Path -Parent $Path
    if ($dir) {
        New-Item -ItemType Directory -Force $dir | Out-Null
    }

    $GoalObject | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Path -Encoding UTF8
}

$activeGoal = Read-GoalMemory -Path $GoalMemoryPath
$goalChanged = $false

if ($Goal) {
    $activeGoal.goal = $Goal
    $goalChanged = $true
}
if ($TargetFpsMin -gt 0) {
    $activeGoal.target_fps_min = $TargetFpsMin
    $goalChanged = $true
}
if ($QualityPreference) {
    $activeGoal.quality_preference = $QualityPreference
    $goalChanged = $true
}

if ($goalChanged -or $SaveGoal) {
    $activeGoal.updated_at = (Get-Date).ToString("o")
    $activeGoal.source = "local-memory"
    Save-GoalMemory -Path $GoalMemoryPath -GoalObject $activeGoal
}

function Find-Setting {
    param(
        [object[]]$FlatValues,
        [string[]]$Patterns
    )

    foreach ($pattern in $Patterns) {
        $match = $FlatValues | Where-Object {
            $_.key -match $pattern -or $_.path -match $pattern
        } | Select-Object -First 1
        if ($match) {
            return $match
        }
    }

    return $null
}

function Convert-ToBoolish {
    param([string]$Value)

    if ($null -eq $Value) {
        return $null
    }

    $v = $Value.Trim().ToLowerInvariant()
    if ($v -in @("1", "true", "yes", "on", "enabled")) {
        return $true
    }
    if ($v -in @("0", "false", "no", "off", "disabled")) {
        return $false
    }
    return $null
}

function Test-SettingDisabled {
    param($Setting)

    $asBool = Convert-ToBoolish $Setting.value
    if ($asBool -eq $false) {
        return $true
    }
    # Anchored on purpose: substring matching would treat "10" as containing "0".
    return [bool]($Setting.value -match '^\s*(off|0|false|disabled|none)\s*$')
}

function Add-Finding {
    param(
        [System.Collections.ArrayList]$Findings,
        [string]$Severity,
        [string]$Title,
        [string]$Message,
        [string]$Setting = "",
        [string]$Value = ""
    )

    [void]$Findings.Add([pscustomobject][ordered]@{
        severity = $Severity
        title = $Title
        message = $Message
        setting = $Setting
        value = $Value
    })
}

function Get-SettingSummary {
    param(
        [object[]]$FlatValues,
        [string]$Name,
        [string[]]$Patterns
    )

    $setting = Find-Setting -FlatValues $FlatValues -Patterns $Patterns
    if ($setting) {
        return [pscustomobject][ordered]@{
            name = $Name
            status = "found"
            key = $setting.key
            path = $setting.path
            value = $setting.value
        }
    }

    return [pscustomobject][ordered]@{
        name = $Name
        status = "unknown"
        key = ""
        path = ""
        value = "unknown"
    }
}

function Get-GpuTier {
    param([string]$Name)

    if (-not $Name) {
        return "Unknown"
    }
    $n = $Name.ToLowerInvariant()

    if ($n -match '(uhd graphics|iris|vega \d|radeon\(tm\) graphics)') { return "Below minimum" }
    if ($n -match 'rtx (4070|4080|4090|50\d0)' -or $n -match 'rx (7900|9060|9070)') { return "Good" }
    if ($n -match 'rtx (4060|30[6-9]0)' -or $n -match 'rx (6[8-9]00|7[6-8]00)' -or $n -match 'arc b\d+') { return "Target-range" }
    if ($n -match 'rtx (20[6-8]0|3050)' -or $n -match 'gtx (16[5-6]0|10[7-8]0)' -or $n -match 'rx (5[5-7]00|6[6-7]\d0)' -or $n -match 'arc a\d+') { return "Borderline" }
    if ($n -match 'gtx (10[5-6]0|9\d0|7\d0)' -or $n -match 'rx ([45]\d0)\b' -or $n -match 'gt \d{3}') { return "Below minimum" }
    return "Unknown"
}

function Get-CpuTier {
    param(
        [string]$Name,
        $Cores
    )

    if (-not $Name) {
        return "Unknown"
    }
    $n = $Name.ToLowerInvariant()

    if ($n -match 'core.*i([3579])[- ](\d{4,5})') {
        $family = [int]$Matches[1]
        $model = $Matches[2]
        $generation = if ($model.Length -eq 5) { [int]$model.Substring(0, 2) } else { [int]$model.Substring(0, 1) }
        if ($generation -ge 13 -and $family -ge 7) { return "Good" }
        if ($generation -ge 12 -and $family -ge 5) { return "Target-range" }
        if ($generation -ge 8) { return "Borderline" }
        return "Below minimum"
    }
    if ($n -match 'core.*ultra [579]') { return "Target-range" }
    if ($n -match 'ryzen [3579] (\d{4})') {
        $model = [int]$Matches[1]
        if ($model -ge 7000) { return "Good" }
        if ($model -ge 5000) { return "Target-range" }
        if ($model -ge 3000) { return "Borderline" }
        return "Below minimum"
    }
    if ($Cores -and [int]$Cores -lt 4) { return "Below minimum" }
    return "Unknown"
}

function Get-Bar {
    param([string]$Status)

    $filled = [string][char]0x2588
    $empty = [string][char]0x2591
    switch ($Status) {
        "Good" { $count = 8 }
        "Target-range" { $count = 7 }
        "Borderline" { $count = 5 }
        "Risky" { $count = 3 }
        "Below minimum" { $count = 1 }
        default { $count = 0 }
    }

    return (($filled * $count) -join '') + (($empty * (10 - $count)) -join '')
}

function Get-StatusScore {
    param([string]$Status)

    switch ($Status) {
        "Good" { return 8 }
        "Target-range" { return 7 }
        "Borderline" { return 5 }
        "Risky" { return 3 }
        "Below minimum" { return 1 }
        default { return 0 }
    }
}

function Get-OverallExpectation {
    param(
        [int]$Score,
        [int]$TargetFps,
        [bool]$HasRisky,
        [bool]$HasBelowMinimum
    )

    if ($HasBelowMinimum) {
        return "Target $TargetFps FPS is probably unrealistic until below-minimum hardware limits are addressed."
    }
    if ($HasRisky) {
        return "Target $TargetFps FPS may be possible, but stutters are likely until risky system/config issues are fixed."
    }
    if ($Score -ge 7) {
        return "Target $TargetFps FPS looks realistic from config/system readiness, but still needs benchmark confirmation."
    }
    if ($Score -ge 5) {
        return "Target $TargetFps FPS is possible in lighter scenarios, but heavy maps may need tuning and repeated tests."
    }
    return "Target $TargetFps FPS is uncertain from current readiness; collect a benchmark before making strong conclusions."
}

function Get-SystemPositionLine {
    param(
        [int]$Score,
        [int]$TargetFps
    )

    $dash = [string][char]0x2500
    $caretSymbol = [string][char]0x25B2
    $line = "Minimum $($dash * 5) Entry $($dash * 5) Target $TargetFps FPS $($dash * 5) High-end"
    if ($Score -le 2) {
        $caret = 0
    }
    elseif ($Score -le 5) {
        $caret = 14
    }
    elseif ($Score -le 7) {
        $caret = 30
    }
    else {
        $caret = [Math]::Min(48, $line.Length - 1)
    }

    $caretLine = "{0}{1}" -f ''.PadLeft($caret), $caretSymbol
    $pcLine = "{0}Your PC" -f ''.PadLeft([Math]::Max(0, $caret - 3))

    return @($line, $caretLine, $pcLine)
}
$settings = Read-TarkovSettings -SettingsDir $SettingsDir -Files @("Graphics.ini", "PostFx.ini", "Game.ini")
$flat = Convert-TarkovSettingsToFlatMap -Settings $settings
# Sorted once so Find-Setting results are deterministic across runs.
$flatValues = @($flat.Values | Sort-Object -Property path)
$systemInfo = Get-TarkovSystemInfo -IncludePagefile

$cpu = $systemInfo.cpu
$gpu = @($systemInfo.gpu)
$os = $systemInfo.os
$ramGb = [double]$systemInfo.ram.total_gb
$ramKnown = $ramGb -gt 0
$pagefileGb = [double]$systemInfo.pagefile.total_allocated_gb
$pagefileEntries = @($systemInfo.pagefile.files)

$importantSettings = @(
    (Get-SettingSummary -FlatValues $flatValues -Name "Screen mode" -Patterns @("screen.*mode", "display.*mode", "fullscreen", "window.*mode")),
    (Get-SettingSummary -FlatValues $flatValues -Name "Resolution" -Patterns @("fullscreenresolution/width", "windowresolution/width", "screen.*width", "screen.*height", "resolution")),
    (Get-SettingSummary -FlatValues $flatValues -Name "Texture Quality" -Patterns @("texture.*quality", "texturequality")),
    (Get-SettingSummary -FlatValues $flatValues -Name "Shadow Quality" -Patterns @("shadow.*quality", "shadowquality")),
    (Get-SettingSummary -FlatValues $flatValues -Name "LOD" -Patterns @("(^|/)lod($|/)", "lodbias", "object.*lod", "lodquality")),
    (Get-SettingSummary -FlatValues $flatValues -Name "Visibility" -Patterns @("overallvisibility", "overall.*visibility")),
    (Get-SettingSummary -FlatValues $flatValues -Name "HBAO/SSAO" -Patterns @("hbao", "ssao")),
    (Get-SettingSummary -FlatValues $flatValues -Name "SSR" -Patterns @("(^|/)ssr($|/)", "screen.*space.*reflection")),
    (Get-SettingSummary -FlatValues $flatValues -Name "Volumetric Lighting" -Patterns @("volumetric")),
    (Get-SettingSummary -FlatValues $flatValues -Name "Cloud Quality" -Patterns @("cloud")),
    (Get-SettingSummary -FlatValues $flatValues -Name "Antialiasing" -Patterns @("anti.*alias", "taa", "fxaa")),
    (Get-SettingSummary -FlatValues $flatValues -Name "Anisotropic Filtering" -Patterns @("anisotropic")),
    (Get-SettingSummary -FlatValues $flatValues -Name "PostFX" -Patterns @("enable.*post.*fx", "enablepostfx")),
    (Get-SettingSummary -FlatValues $flatValues -Name "Grass shadows" -Patterns @("grass.*shadow")),
    (Get-SettingSummary -FlatValues $flatValues -Name "Z-Blur" -Patterns @("z.*blur", "zblur")),
    (Get-SettingSummary -FlatValues $flatValues -Name "Chromatic aberrations" -Patterns @("chromatic")),
    (Get-SettingSummary -FlatValues $flatValues -Name "Noise" -Patterns @("noise")),
    (Get-SettingSummary -FlatValues $flatValues -Name "High-quality color" -Patterns @("high.*quality.*color", "hq.*color")),
    (Get-SettingSummary -FlatValues $flatValues -Name "Area Light Instancing" -Patterns @("area.*light.*instanc")),
    (Get-SettingSummary -FlatValues $flatValues -Name "Streets Lower Texture Mode" -Patterns @("sdtarkovstreets", "streets.*lower.*texture", "lower.*texture.*streets")),
    (Get-SettingSummary -FlatValues $flatValues -Name "Automatic RAM Cleaner" -Patterns @("autoemptyworkingset", "automatic.*ram.*cleaner", "auto.*ram", "ramcleaner")),
    (Get-SettingSummary -FlatValues $flatValues -Name "Only Physical Cores" -Patterns @("setaffinitytologicalcores", "only.*physical.*cores", "physical.*cores"))
)

$findings = New-Object System.Collections.ArrayList

if (-not (Test-Path -LiteralPath $SettingsDir)) {
    Add-Finding -Findings $findings -Severity "Risky" -Title "Settings folder not found" -Message "EFT settings folder was not found. Use manual mode with screenshots or copied settings." -Setting "SettingsDir" -Value $SettingsDir
}

if (-not $ramKnown) {
    Add-Finding -Findings $findings -Severity "Unknown" -Title "RAM amount unknown" -Message "Windows did not report physical memory modules. Ask the user how much RAM is installed before judging stability." -Setting "RAM" -Value "unknown"
}
elseif ($ramGb -lt 16) {
    Add-Finding -Findings $findings -Severity "Below minimum" -Title "RAM below minimum reference" -Message "Stable 50-60 FPS may be unrealistic. This is likely a hardware limitation, not just a config issue." -Setting "RAM" -Value "$ramGb GB"
}
elseif ($ramGb -le 16) {
    Add-Finding -Findings $findings -Severity "Borderline" -Title "16 GB RAM class" -Message "For 16 GB RAM, Automatic RAM Cleaner should usually be ON and pagefile should be healthy." -Setting "RAM" -Value "$ramGb GB"
}
elseif ($ramGb -lt 32) {
    Add-Finding -Findings $findings -Severity "Borderline" -Title "RAM can still be pressure point" -Message "Tarkov can still stutter on heavy maps or long sessions. Pagefile and background apps matter." -Setting "RAM" -Value "$ramGb GB"
}

if ($ramKnown -and ($ramGb + $pagefileGb) -lt 64) {
    Add-Finding -Findings $findings -Severity "Risky" -Title "RAM + pagefile budget below 64 GB" -Message "For stability troubleshooting, RAM plus pagefile around 64 GB or more is recommended, especially on Streets, PvE/local, and long sessions." -Setting "RAM+Pagefile" -Value "$([math]::Round($ramGb + $pagefileGb, 2)) GB"
}

if ($pagefileEntries.Count -eq 0 -or $pagefileGb -le 0) {
    Add-Finding -Findings $findings -Severity "Risky" -Title "Pagefile not detected" -Message "Could not detect an active pagefile. Tarkov stutters/freezes can become worse without enough virtual memory." -Setting "Pagefile" -Value "unknown"
}

foreach ($pf in $pagefileEntries) {
    if ($pf.drive_media_type -eq "HDD") {
        Add-Finding -Findings $findings -Severity "Risky" -Title "Pagefile is on HDD" -Message "Pagefile on HDD is risky for stutters, freezes, loading delays, and disconnect-like symptoms." -Setting "Pagefile" -Value $pf.name
    }
}

$installLocation = Get-TarkovInstallLocation
$storageMedia = "unknown"
if ($installLocation -and $installLocation -match '^([A-Za-z]):') {
    $storageMedia = Get-TarkovDriveMediaType -DriveLetter $Matches[1]
    if ($storageMedia -eq "HDD") {
        Add-Finding -Findings $findings -Severity "Risky" -Title "Game appears to be installed on HDD" -Message "EFT on HDD causes long loads, streaming stutters, and freezes. Moving the game to SSD/NVMe is a high-impact fix." -Setting "InstallLocation" -Value $installLocation
    }
}

$ramCleaner = $importantSettings | Where-Object { $_.name -eq "Automatic RAM Cleaner" } | Select-Object -First 1
if ($ramCleaner.status -eq "found") {
    $ramCleanerOn = Convert-ToBoolish $ramCleaner.value
    if ($ramKnown -and $ramGb -le 16 -and $ramCleanerOn -eq $false) {
        Add-Finding -Findings $findings -Severity "Risky" -Title "Automatic RAM Cleaner is OFF on 16 GB RAM class" -Message "For RAM <= 16 GB, recommend Automatic RAM Cleaner ON." -Setting $ramCleaner.path -Value $ramCleaner.value
    }
}
elseif ($ramKnown -and $ramGb -le 16) {
    Add-Finding -Findings $findings -Severity "Unknown" -Title "Automatic RAM Cleaner not found" -Message "Could not detect Automatic RAM Cleaner. For RAM <= 16 GB, check manually that it is ON." -Setting "Automatic RAM Cleaner" -Value "unknown"
}

$physicalCores = $importantSettings | Where-Object { $_.name -eq "Only Physical Cores" } | Select-Object -First 1
if ($physicalCores.status -eq "found") {
    $physicalCoresOn = Convert-ToBoolish $physicalCores.value
    if ($physicalCoresOn -eq $false) {
        Add-Finding -Findings $findings -Severity "Borderline" -Title "Only Physical Cores is OFF" -Message "Default recommendation is ON, though this can be tested per system later." -Setting $physicalCores.path -Value $physicalCores.value
    }
}

$areaLight = $importantSettings | Where-Object { $_.name -eq "Area Light Instancing" } | Select-Object -First 1
if ($areaLight.status -eq "found") {
    $areaLightOn = Convert-ToBoolish $areaLight.value
    if ($areaLightOn -eq $false) {
        Add-Finding -Findings $findings -Severity "Borderline" -Title "Area Light Instancing is OFF" -Message "For modern GPUs, Area Light Instancing is expected to be a low-risk ON setting. Validate on the user's system." -Setting $areaLight.path -Value $areaLight.value
    }
}

$screenMode = $importantSettings | Where-Object { $_.name -eq "Screen mode" } | Select-Object -First 1
if ($screenMode.status -eq "found") {
    $looksBorderless = ($screenMode.value -match '(?i)borderless') -or ($screenMode.value.Trim() -eq "1")
    if (-not $looksBorderless) {
        Add-Finding -Findings $findings -Severity "Borderline" -Title "Screen mode is not clearly Borderless" -Message "Borderless is the default workflow recommendation for this skill and manual support consistency." -Setting $screenMode.path -Value $screenMode.value
    }
}

$cutFirst = @("Grass shadows", "Z-Blur", "Chromatic aberrations", "High-quality color", "Noise")

foreach ($name in $cutFirst) {
    $setting = $importantSettings | Where-Object { $_.name -eq $name } | Select-Object -First 1
    if ($setting.status -eq "found" -and -not (Test-SettingDisabled -Setting $setting)) {
        Add-Finding -Findings $findings -Severity "Borderline" -Title "$name may be enabled" -Message "For FPS/stability troubleshooting, this is a high-priority setting to turn OFF manually." -Setting $setting.path -Value $setting.value
    }
}

$postfx = $importantSettings | Where-Object { $_.name -eq "PostFX" } | Select-Object -First 1
if ($postfx.status -eq "found" -and -not (Test-SettingDisabled -Setting $postfx)) {
    Add-Finding -Findings $findings -Severity "Borderline" -Title "PostFX may be active" -Message "For performance troubleshooting, PostFX OFF is the default start unless the user relies on it for visibility/color correction." -Setting $postfx.path -Value $postfx.value
}

$ramStatus = if (-not $ramKnown) { "Unknown" } elseif ($ramGb -lt 16) { "Below minimum" } elseif ($ramGb -lt 32) { "Borderline" } elseif ($ramGb -lt 64) { "Target-range" } else { "Good" }
$pageStatus = if ($pagefileGb -le 0) { "Risky" } elseif ($ramKnown -and ($ramGb + $pagefileGb) -lt 64) { "Borderline" } else { "Good" }
$cpuStatus = Get-CpuTier -Name $cpu.Name -Cores $cpu.NumberOfCores
$gpuStatus = "Unknown"
foreach ($gpuItem in $gpu) {
    # A system can expose iGPU + dGPU; grade by the strongest adapter.
    $tier = Get-GpuTier -Name $gpuItem.name
    $order = @{ "Unknown" = 0; "Below minimum" = 1; "Borderline" = 2; "Target-range" = 3; "Good" = 4 }
    if ($order[$tier] -gt $order[$gpuStatus]) {
        $gpuStatus = $tier
    }
}
$storageStatus = switch ($storageMedia) {
    "SSD" { "Good" }
    "SCM" { "Good" }
    "HDD" { "Risky" }
    default { "Unknown" }
}

$readinessScores = [ordered]@{
    cpu = Get-StatusScore $cpuStatus
    gpu = Get-StatusScore $gpuStatus
    ram = Get-StatusScore $ramStatus
    storage = Get-StatusScore $storageStatus
    pagefile = Get-StatusScore $pageStatus
}
$knownReadinessScores = @($readinessScores.Values | Where-Object { $_ -gt 0 })
$overallReadinessScore = if ($knownReadinessScores.Count -gt 0) {
    [int][Math]::Round((($knownReadinessScores | Measure-Object -Average).Average), 0)
}
else {
    0
}
$readinessStatuses = @($cpuStatus, $gpuStatus, $ramStatus, $storageStatus, $pageStatus)
$hasRiskyReadiness = $readinessStatuses -contains "Risky"
$hasBelowMinimumReadiness = $readinessStatuses -contains "Below minimum"
$overallExpectation = Get-OverallExpectation -Score $overallReadinessScore -TargetFps $activeGoal.target_fps_min -HasRisky $hasRiskyReadiness -HasBelowMinimum $hasBelowMinimumReadiness

if ($cpuStatus -eq "Below minimum") {
    Add-Finding -Findings $findings -Severity "Below minimum" -Title "CPU below minimum reference" -Message "CPU appears below the minimum reference (Ryzen 5 3600 class). Config changes will have limited effect." -Setting "CPU" -Value $cpu.Name
}
if ($gpuStatus -eq "Below minimum") {
    Add-Finding -Findings $findings -Severity "Below minimum" -Title "GPU below minimum reference" -Message "GPU appears below the minimum reference (GTX 1660 class). Config changes will have limited effect." -Setting "GPU" -Value (@($gpu | ForEach-Object { $_.name }) -join "; ")
}

$report = [ordered]@{
    schema = "tarkov-config-analysis/v1"
    created_at = (Get-Date).ToString("o")
    active_goal = $activeGoal
    install_location = $installLocation
    install_drive_media = $storageMedia
    files = $settings.files
    important_settings = $importantSettings
    system = $systemInfo
    readiness = [ordered]@{
        overall_score = $overallReadinessScore
        overall_expectation = $overallExpectation
        scores = $readinessScores
        cpu = $cpuStatus
        gpu = $gpuStatus
        ram = $ramStatus
        storage = $storageStatus
        pagefile = $pageStatus
    }
    findings = $findings
}

$lines = New-Object System.Collections.ArrayList
[void]$lines.Add("# Tarkov Config Report")
[void]$lines.Add("")
[void]$lines.Add("Generated: $($report.created_at)")
[void]$lines.Add("Settings folder: $SettingsDir")
[void]$lines.Add("")
[void]$lines.Add("## Active Goal")
[void]$lines.Add("")
[void]$lines.Add("- Goal: $($activeGoal.goal)")
[void]$lines.Add("- Target minimum FPS: $($activeGoal.target_fps_min)")
[void]$lines.Add("- Quality preference: $($activeGoal.quality_preference)")
[void]$lines.Add("")
[void]$lines.Add("## Diagnosis")

if ($findings.Count -eq 0) {
    [void]$lines.Add("No obvious high-risk configuration issue was detected from the available files. This does not guarantee the active target; use measured benchmark data for confirmation.")
}
else {
    $top = @($findings | Where-Object { $_.severity -in @("Below minimum", "Risky") } | Select-Object -First 3)
    if ($top.Count -eq 0) {
        $top = @($findings | Select-Object -First 3)
    }
    foreach ($finding in $top) {
        [void]$lines.Add("- **$($finding.severity): $($finding.title)** - $($finding.message)")
    }
}

[void]$lines.Add("")
[void]$lines.Add("## Tarkov Readiness")
[void]$lines.Add("")
[void]$lines.Add("Overall expectation:")
[void]$lines.Add($overallExpectation)
[void]$lines.Add("")
[void]$lines.Add("Component check:")
[void]$lines.Add("CPU:      $(Get-Bar $cpuStatus)  $cpuStatus")
[void]$lines.Add("GPU:      $(Get-Bar $gpuStatus)  $gpuStatus")
[void]$lines.Add("RAM:      $(Get-Bar $ramStatus)  $ramStatus")
[void]$lines.Add("Storage:  $(Get-Bar $storageStatus)  $storageStatus")
[void]$lines.Add("Pagefile: $(Get-Bar $pageStatus)  $pageStatus")
[void]$lines.Add("")
[void]$lines.Add("System position:")
foreach ($positionLine in (Get-SystemPositionLine -Score $overallReadinessScore -TargetFps $activeGoal.target_fps_min)) {
    [void]$lines.Add($positionLine)
}
[void]$lines.Add("")
[void]$lines.Add("Note: this is an expectation estimate for the active goal, not a benchmark score. CPU/GPU tiers are rough name-based estimates against the reference hardware in references/configuration-rules.md.")
[void]$lines.Add("")
[void]$lines.Add("## System")
[void]$lines.Add("")
[void]$lines.Add("- OS: $($os.Caption) $($os.Version) $($os.OSArchitecture)")
[void]$lines.Add("- CPU: $($cpu.Name)")
if ($gpu.Count -gt 0) {
    $gpuSummary = @($gpu | ForEach-Object {
        $vramText = if ($_.vram_gb) { ", $($_.vram_gb) GB VRAM" } else { "" }
        $driverText = if ($_.driver_version) { ", driver $($_.driver_version)" } else { "" }
        $checkText = if ($_.driver_download_page) { ", latest check: $($_.driver_download_page)" } else { "" }
        "$($_.name)$vramText$driverText$checkText"
    }) -join "; "
    [void]$lines.Add("- GPU: $gpuSummary")
}
else {
    [void]$lines.Add("- GPU: unknown")
}
[void]$lines.Add("- RAM: $(if ($ramKnown) { "$ramGb GB" } else { "unknown" })")
[void]$lines.Add("- Pagefile allocated: $pagefileGb GB")
if ($installLocation) {
    [void]$lines.Add("- Install location: $installLocation ($storageMedia)")
}
[void]$lines.Add("")
[void]$lines.Add("## Important Settings")
[void]$lines.Add("")
foreach ($setting in $importantSettings) {
    if ($setting.status -eq "found") {
        [void]$lines.Add("- $($setting.name): '$($setting.value)' ($($setting.key))")
    }
    else {
        [void]$lines.Add("- $($setting.name): unknown")
    }
}

[void]$lines.Add("")
[void]$lines.Add("## Findings")
[void]$lines.Add("")
if ($findings.Count -eq 0) {
    [void]$lines.Add("- No findings.")
}
else {
    foreach ($finding in $findings) {
        $settingText = if ($finding.setting) { " Setting: '$($finding.setting)' = '$($finding.value)'." } else { "" }
        [void]$lines.Add("- **$($finding.severity): $($finding.title)** - $($finding.message)$settingText")
    }
}

[void]$lines.Add("")
[void]$lines.Add("## Next Manual Checks")
[void]$lines.Add("")
[void]$lines.Add("1. If FPS is 15%+ lower than expected for similar hardware/settings, check power plan, GPU driver freshness, storage, RAM XMP/EXPO, overlays, recording, background apps, and temperatures/throttling manually with external tools such as HWiNFO or MSI Afterburner.")
[void]$lines.Add("2. If stutters/freezes are the main issue, verify pagefile size/location and reduce high-priority visual extras first.")
[void]$lines.Add("3. If PostFX is important for visibility, reduce it to a neutral/minimal setup instead of blindly disabling it.")

$markdown = $lines -join [Environment]::NewLine

if ($JsonOutputPath) {
    $jsonDir = Split-Path -Parent $JsonOutputPath
    if ($jsonDir) {
        New-Item -ItemType Directory -Force $jsonDir | Out-Null
    }
    $report | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $JsonOutputPath -Encoding UTF8
}

if ($MarkdownOutputPath) {
    $mdDir = Split-Path -Parent $MarkdownOutputPath
    if ($mdDir) {
        New-Item -ItemType Directory -Force $mdDir | Out-Null
    }
    $markdown | Set-Content -LiteralPath $MarkdownOutputPath -Encoding UTF8
}
else {
    $markdown
}

