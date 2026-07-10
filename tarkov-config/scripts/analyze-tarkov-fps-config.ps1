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

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repoRoot "scripts\TarkovCommon.ps1")

if (-not $GoalMemoryPath) {
    $GoalMemoryPath = Join-Path (Split-Path -Parent $PSScriptRoot) "memory\current-goal.json"
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

    if (Test-Path -LiteralPath $Path) {
        return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    }

    return [pscustomobject](Get-DefaultGoal)
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
        [hashtable]$Flat,
        [string[]]$Patterns
    )

    foreach ($pattern in $Patterns) {
        $match = $Flat.Values | Where-Object {
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
        [hashtable]$Flat,
        [string]$Name,
        [string[]]$Patterns
    )

    $setting = Find-Setting -Flat $Flat -Patterns $Patterns
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

function Get-Bar {
    param([string]$Status)

    switch ($Status) {
        "Good" { return "[########..]" }
        "Target-range" { return "[#######...]" }
        "Borderline" { return "[#####.....]" }
        "Risky" { return "[###.......]" }
        "Below minimum" { return "[#.........]" }
        default { return "[..........]" }
    }
}

$settings = Read-TarkovSettings -SettingsDir $SettingsDir -Files @("Graphics.ini", "PostFx.ini", "Game.ini")
$flat = Convert-TarkovSettingsToFlatMap -Settings $settings
$systemInfo = Get-TarkovSystemInfo -IncludePagefile

$cpu = $systemInfo.cpu
$gpu = @($systemInfo.gpu)
$os = $systemInfo.os
$ramGb = [double]$systemInfo.ram.total_gb
$pagefileGb = [double]$systemInfo.pagefile.total_allocated_gb
$pagefiles = @(Get-CimInstance Win32_PageFileUsage -ErrorAction SilentlyContinue)

$importantSettings = @(
    (Get-SettingSummary -Flat $flat -Name "Screen mode" -Patterns @("screen.*mode", "display.*mode", "fullscreen", "window.*mode")),
    (Get-SettingSummary -Flat $flat -Name "Resolution" -Patterns @("fullscreenresolution/width", "windowresolution/width", "screen.*width", "screen.*height", "resolution")),
    (Get-SettingSummary -Flat $flat -Name "Texture Quality" -Patterns @("texture.*quality", "texturequality")),
    (Get-SettingSummary -Flat $flat -Name "Shadow Quality" -Patterns @("shadow.*quality", "shadowquality")),
    (Get-SettingSummary -Flat $flat -Name "LOD" -Patterns @("(^|/)lod($|/)", "lodbias", "object.*lod", "lodquality")),
    (Get-SettingSummary -Flat $flat -Name "Visibility" -Patterns @("overallvisibility", "overall.*visibility")),
    (Get-SettingSummary -Flat $flat -Name "HBAO/SSAO" -Patterns @("hbao", "ssao")),
    (Get-SettingSummary -Flat $flat -Name "SSR" -Patterns @("(^|/)ssr($|/)", "screen.*space.*reflection")),
    (Get-SettingSummary -Flat $flat -Name "Volumetric Lighting" -Patterns @("volumetric")),
    (Get-SettingSummary -Flat $flat -Name "Cloud Quality" -Patterns @("cloud")),
    (Get-SettingSummary -Flat $flat -Name "Antialiasing" -Patterns @("anti.*alias", "taa", "fxaa")),
    (Get-SettingSummary -Flat $flat -Name "Anisotropic Filtering" -Patterns @("anisotropic")),
    (Get-SettingSummary -Flat $flat -Name "PostFX" -Patterns @("enable.*post.*fx", "enablepostfx")),
    (Get-SettingSummary -Flat $flat -Name "Grass shadows" -Patterns @("grass.*shadow")),
    (Get-SettingSummary -Flat $flat -Name "Z-Blur" -Patterns @("z.*blur", "zblur")),
    (Get-SettingSummary -Flat $flat -Name "Chromatic aberrations" -Patterns @("chromatic")),
    (Get-SettingSummary -Flat $flat -Name "Noise" -Patterns @("noise")),
    (Get-SettingSummary -Flat $flat -Name "High-quality color" -Patterns @("high.*quality.*color", "hq.*color")),
    (Get-SettingSummary -Flat $flat -Name "Area Light Instancing" -Patterns @("area.*light.*instanc")),
    (Get-SettingSummary -Flat $flat -Name "Streets Lower Texture Mode" -Patterns @("sdtarkovstreets", "streets.*lower.*texture", "lower.*texture.*streets")),
    (Get-SettingSummary -Flat $flat -Name "Automatic RAM Cleaner" -Patterns @("autoemptyworkingset", "automatic.*ram.*cleaner", "auto.*ram", "ramcleaner")),
    (Get-SettingSummary -Flat $flat -Name "Only Physical Cores" -Patterns @("setaffinitytologicalcores", "only.*physical.*cores", "physical.*cores"))
)

$findings = New-Object System.Collections.ArrayList

if (-not (Test-Path -LiteralPath $SettingsDir)) {
    Add-Finding -Findings $findings -Severity "Risky" -Title "Settings folder not found" -Message "EFT settings folder was not found. Use manual mode with screenshots or copied settings." -Setting "SettingsDir" -Value $SettingsDir
}

if ($ramGb -gt 0 -and $ramGb -lt 16) {
    Add-Finding -Findings $findings -Severity "Below minimum" -Title "RAM below minimum reference" -Message "Stable 50-60 FPS may be unrealistic. This is likely a hardware limitation, not just a config issue." -Setting "RAM" -Value "$ramGb GB"
}
elseif ($ramGb -le 16) {
    Add-Finding -Findings $findings -Severity "Borderline" -Title "16 GB RAM class" -Message "For 16 GB RAM, Automatic RAM Cleaner should usually be ON and pagefile should be healthy." -Setting "RAM" -Value "$ramGb GB"
}
elseif ($ramGb -lt 32) {
    Add-Finding -Findings $findings -Severity "Borderline" -Title "RAM can still be pressure point" -Message "Tarkov can still stutter on heavy maps or long sessions. Pagefile and background apps matter." -Setting "RAM" -Value "$ramGb GB"
}

if (($ramGb + $pagefileGb) -lt 64) {
    Add-Finding -Findings $findings -Severity "Risky" -Title "RAM + pagefile budget below 64 GB" -Message "For stability troubleshooting, RAM plus pagefile around 64 GB or more is recommended, especially on Streets, PvE/local, and long sessions." -Setting "RAM+Pagefile" -Value "$([math]::Round($ramGb + $pagefileGb, 2)) GB"
}

if ($pagefiles.Count -eq 0 -or $pagefileGb -le 0) {
    Add-Finding -Findings $findings -Severity "Risky" -Title "Pagefile not detected" -Message "Could not detect an active pagefile. Tarkov stutters/freezes can become worse without enough virtual memory." -Setting "Pagefile" -Value "unknown"
}

foreach ($pf in $pagefiles) {
    if ($pf.Name -match '^[A-Za-z]:') {
        $drive = $pf.Name.Substring(0, 2)
        $logicalDisk = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='$drive'" -ErrorAction SilentlyContinue
        if ($logicalDisk -and $logicalDisk.DriveType -eq 3) {
            $partition = Get-CimAssociatedInstance -InputObject $logicalDisk -ResultClassName Win32_DiskPartition -ErrorAction SilentlyContinue | Select-Object -First 1
            $disk = if ($partition) { Get-CimAssociatedInstance -InputObject $partition -ResultClassName Win32_DiskDrive -ErrorAction SilentlyContinue | Select-Object -First 1 } else { $null }
            if ($disk -and $disk.MediaType -match "HDD") {
                Add-Finding -Findings $findings -Severity "Risky" -Title "Pagefile may be on HDD" -Message "Pagefile on HDD is risky for stutters, freezes, loading delays, and disconnect-like symptoms." -Setting "Pagefile" -Value $pf.Name
            }
        }
    }
}

$ramCleaner = $importantSettings | Where-Object { $_.name -eq "Automatic RAM Cleaner" } | Select-Object -First 1
if ($ramCleaner.status -eq "found") {
    $ramCleanerOn = Convert-ToBoolish $ramCleaner.value
    if ($ramGb -le 16 -and $ramCleanerOn -eq $false) {
        Add-Finding -Findings $findings -Severity "Risky" -Title "Automatic RAM Cleaner is OFF on 16 GB RAM class" -Message "For RAM <= 16 GB, recommend Automatic RAM Cleaner ON." -Setting $ramCleaner.path -Value $ramCleaner.value
    }
}
elseif ($ramGb -le 16) {
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
if ($screenMode.status -eq "found" -and $screenMode.value -notmatch "borderless|1") {
    Add-Finding -Findings $findings -Severity "Borderline" -Title "Screen mode is not clearly Borderless" -Message "Borderless is the default workflow recommendation for this skill and manual support consistency." -Setting $screenMode.path -Value $screenMode.value
}

$cutFirst = @(
    @{ Name = "Grass shadows"; OffValue = "off" },
    @{ Name = "Z-Blur"; OffValue = "off" },
    @{ Name = "Chromatic aberrations"; OffValue = "off" },
    @{ Name = "High-quality color"; OffValue = "off" },
    @{ Name = "Noise"; OffValue = "off" }
)

foreach ($item in $cutFirst) {
    $setting = $importantSettings | Where-Object { $_.name -eq $item.Name } | Select-Object -First 1
    if ($setting.status -eq "found") {
        $asBool = Convert-ToBoolish $setting.value
        if ($asBool -eq $true -or $setting.value -notmatch "off|0|false|disabled") {
            Add-Finding -Findings $findings -Severity "Borderline" -Title "$($item.Name) may be enabled" -Message "For FPS/stability troubleshooting, this is a high-priority setting to turn OFF manually." -Setting $setting.path -Value $setting.value
        }
    }
}

$postfx = $importantSettings | Where-Object { $_.name -eq "PostFX" } | Select-Object -First 1
if ($postfx.status -eq "found") {
    $postfxOn = Convert-ToBoolish $postfx.value
    if ($postfxOn -ne $false -and $postfx.value -notmatch "off|0|false|disabled") {
        Add-Finding -Findings $findings -Severity "Borderline" -Title "PostFX may be active" -Message "For performance troubleshooting, PostFX OFF is the default start unless the user relies on it for visibility/color correction." -Setting $postfx.path -Value $postfx.value
    }
}

$ramStatus = if ($ramGb -lt 16) { "Below minimum" } elseif ($ramGb -lt 32) { "Borderline" } elseif ($ramGb -lt 64) { "Target-range" } else { "Good" }
$pageStatus = if ($pagefileGb -le 0) { "Risky" } elseif (($ramGb + $pagefileGb) -lt 64) { "Borderline" } else { "Good" }
$cpuStatus = if ($cpu.Name) { "Unknown" } else { "Unknown" }
$gpuStatus = if ($gpu.Count -gt 0) { "Unknown" } else { "Unknown" }
$storageStatus = "Unknown"

$report = [ordered]@{
    schema = "tarkov-config-analysis/v1"
    created_at = (Get-Date).ToString("o")
    active_goal = $activeGoal
    settings_dir = $SettingsDir
    files = $settings.files
    important_settings = $importantSettings
    system = $systemInfo
    readiness = [ordered]@{
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
[void]$lines.Add("CPU:      $(Get-Bar $cpuStatus)  $cpuStatus")
[void]$lines.Add("GPU:      $(Get-Bar $gpuStatus)  $gpuStatus")
[void]$lines.Add("RAM:      $(Get-Bar $ramStatus)  $ramStatus")
[void]$lines.Add("Storage:  $(Get-Bar $storageStatus)  $storageStatus")
[void]$lines.Add("Pagefile: $(Get-Bar $pageStatus)  $pageStatus")
[void]$lines.Add("")
[void]$lines.Add("Note: this is an expectation estimate for the active goal, not a benchmark score.")
[void]$lines.Add("")
[void]$lines.Add("## System")
[void]$lines.Add("")
[void]$lines.Add("- OS: $($os.Caption) $($os.Version) $($os.OSArchitecture)")
[void]$lines.Add("- CPU: $($cpu.Name)")
if ($gpu.Count -gt 0) {
    $gpuSummary = @($gpu | ForEach-Object {
        $driverText = if ($_.driver_version) { ", driver $($_.driver_version)" } else { "" }
        $checkText = if ($_.driver_download_page) { ", latest check: $($_.driver_download_page)" } else { "" }
        "$($_.name)$driverText$checkText"
    }) -join "; "
    [void]$lines.Add("- GPU: $gpuSummary")
}
else {
    [void]$lines.Add("- GPU: unknown")
}
[void]$lines.Add("- RAM: $ramGb GB")
[void]$lines.Add("- Pagefile allocated: $pagefileGb GB")
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
