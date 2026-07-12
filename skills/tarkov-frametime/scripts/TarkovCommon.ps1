$ErrorActionPreference = "Stop"

function Get-TarkovDataDir {
    # Local state must survive plugin/repo updates, so it lives outside the install tree.
    param([string]$SubDir = "")

    $base = Join-Path $env:LOCALAPPDATA "TarkovSkills"
    $path = if ($SubDir) { Join-Path $base $SubDir } else { $base }
    New-Item -ItemType Directory -Force $path | Out-Null
    return $path
}

function Hide-TarkovUserPath {
    # Artifacts intended for upload must not contain user names or host names.
    param([string]$Text)

    if (-not $Text) {
        return $Text
    }

    $masked = [regex]::Replace($Text, '(?i)(Users)(\\\\|\\|/)([^\\/"]+)', '$1$2user')
    foreach ($name in @($env:USERNAME, $env:COMPUTERNAME)) {
        if ($name) {
            $masked = [regex]::Replace($masked, "(?i)(?<![A-Za-z0-9])$([regex]::Escape($name))(?![A-Za-z0-9])", 'user')
        }
    }
    return $masked
}

function Convert-TarkovIniFile {
    param([string]$Path)

    $result = [ordered]@{}
    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    $section = "root"
    $result[$section] = [ordered]@{}

    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if ($trimmed.Length -eq 0 -or $trimmed.StartsWith(";") -or $trimmed.StartsWith("#")) {
            continue
        }

        if ($trimmed -match '^\[(.+)\]$') {
            $section = $Matches[1]
            if (-not $result.Contains($section)) {
                $result[$section] = [ordered]@{}
            }
            continue
        }

        $parts = $trimmed -split '=', 2
        if ($parts.Count -eq 2) {
            $result[$section][$parts[0].Trim()] = $parts[1].Trim()
        }
    }

    return $result
}

function Read-TarkovSettingsFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    $raw = Get-Content -Raw -LiteralPath $Path
    $trimmed = $raw.TrimStart()
    if ($trimmed.StartsWith("{") -or $trimmed.StartsWith("[")) {
        return $raw | ConvertFrom-Json
    }

    return Convert-TarkovIniFile -Path $Path
}

function Read-TarkovSettings {
    param(
        [string]$SettingsDir = "$env:APPDATA\Battlestate Games\Escape from Tarkov\Settings",
        [string[]]$Files = @("Graphics.ini", "PostFx.ini", "Game.ini")
    )

    $settings = [ordered]@{
        source = "eft_settings"
        captured_at = (Get-Date).ToString("o")
        files = [ordered]@{}
    }

    foreach ($file in $Files) {
        $path = Join-Path $SettingsDir $file
        $settings.files[$file] = [ordered]@{
            exists = (Test-Path -LiteralPath $path)
            data = (Read-TarkovSettingsFile -Path $path)
        }
    }

    return $settings
}

function Add-TarkovFlatValue {
    param(
        [hashtable]$Target,
        [string]$FileName,
        [string]$Path,
        [string]$Key,
        $Value
    )

    if ($null -eq $Value) {
        $Target[$Path] = [pscustomobject]@{
            file = $FileName
            section = ""
            key = $Key
            value = $null
            path = $Path
        }
        return
    }

    if ($Value -is [System.Collections.IDictionary]) {
        foreach ($childKey in $Value.Keys) {
            Add-TarkovFlatValue -Target $Target -FileName $FileName -Path "$Path/$childKey" -Key $childKey -Value $Value[$childKey]
        }
        return
    }

    if ($Value -is [System.Array] -and -not ($Value -is [string])) {
        for ($i = 0; $i -lt $Value.Count; $i++) {
            Add-TarkovFlatValue -Target $Target -FileName $FileName -Path "${Path}[$i]" -Key "${Key}[$i]" -Value $Value[$i]
        }
        return
    }

    if ($Value -is [pscustomobject]) {
        foreach ($prop in $Value.PSObject.Properties) {
            Add-TarkovFlatValue -Target $Target -FileName $FileName -Path "$Path/$($prop.Name)" -Key $prop.Name -Value $prop.Value
        }
        return
    }

    $Target[$Path] = [pscustomobject]@{
        file = $FileName
        section = ""
        key = $Key
        value = [string]$Value
        path = $Path
    }
}

function Add-TarkovFlatSettings {
    param(
        [hashtable]$Target,
        [string]$FileName,
        $Data
    )

    if (-not $Data) {
        return
    }

    Add-TarkovFlatValue -Target $Target -FileName $FileName -Path $FileName -Key $FileName -Value $Data
}

function Convert-TarkovSettingsToFlatMap {
    param($Settings)

    $flat = @{}
    $files = $Settings.files

    # Dictionaries do not expose their keys via PSObject.Properties, so enumerate
    # entries directly; PSCustomObject (settings loaded back from JSON) needs Properties.
    if ($files -is [System.Collections.IDictionary]) {
        foreach ($fileName in @($files.Keys)) {
            Add-TarkovFlatSettings -Target $flat -FileName $fileName -Data $files[$fileName].data
        }
    }
    else {
        foreach ($fileProp in $files.PSObject.Properties) {
            Add-TarkovFlatSettings -Target $flat -FileName $fileProp.Name -Data $fileProp.Value.data
        }
    }
    return $flat
}

function Get-TarkovInstallLocation {
    $registryPaths = @(
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\EscapeFromTarkov",
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 3932890"
    )

    foreach ($registryPath in $registryPaths) {
        $item = Get-ItemProperty -Path $registryPath -ErrorAction SilentlyContinue
        if ($item -and $item.InstallLocation -and (Test-Path -LiteralPath $item.InstallLocation)) {
            return $item.InstallLocation
        }
    }
    return ""
}

function Get-TarkovDriveMediaType {
    # Returns SSD / HDD / SCM / unknown for a drive letter, without elevation.
    param([string]$DriveLetter)

    if (-not $DriveLetter) {
        return "unknown"
    }

    $letter = $DriveLetter.TrimEnd(':', '\').ToUpperInvariant()
    $partition = Get-CimInstance -Namespace root\Microsoft\Windows\Storage -ClassName MSFT_Partition -ErrorAction SilentlyContinue |
        Where-Object { "$($_.DriveLetter)" -eq $letter } | Select-Object -First 1
    if (-not $partition) {
        return "unknown"
    }

    $disk = Get-CimInstance -Namespace root\Microsoft\Windows\Storage -ClassName MSFT_PhysicalDisk -ErrorAction SilentlyContinue |
        Where-Object { "$($_.DeviceId)" -eq "$($partition.DiskNumber)" } | Select-Object -First 1
    if (-not $disk) {
        return "unknown"
    }

    switch ([int]$disk.MediaType) {
        3 { return "HDD" }
        4 { return "SSD" }
        5 { return "SCM" }
        default { return "unknown" }
    }
}

function Get-TarkovGpuVramMap {
    # Win32_VideoController.AdapterRAM is uint32 and caps at 4 GB; the display class
    # registry key exposes the real value as a qword.
    $map = @{}
    $classKey = "HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"

    foreach ($key in @(Get-ChildItem -Path $classKey -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -match '^\d{4}$' })) {
        $props = Get-ItemProperty -Path $key.PSPath -ErrorAction SilentlyContinue
        if (-not $props -or -not $props.DriverDesc) {
            continue
        }

        $bytes = $null
        if ($props.PSObject.Properties['HardwareInformation.qwMemorySize']) {
            $bytes = [uint64]$props.'HardwareInformation.qwMemorySize'
        }
        elseif ($props.PSObject.Properties['HardwareInformation.MemorySize'] -and $props.'HardwareInformation.MemorySize' -is [ValueType]) {
            $bytes = [uint64]$props.'HardwareInformation.MemorySize'
        }

        if ($bytes -and $bytes -gt 0 -and -not $map.ContainsKey($props.DriverDesc)) {
            $map[$props.DriverDesc] = [math]::Round($bytes / 1GB, 2)
        }
    }
    return $map
}

function Get-TarkovSystemInfo {
    param([switch]$IncludePagefile)

    $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1 Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed
    $gpu = @(Get-CimInstance Win32_VideoController | Select-Object Name, AdapterRAM, DriverVersion, CurrentHorizontalResolution, CurrentVerticalResolution)
    $memoryModules = @(Get-CimInstance Win32_PhysicalMemory)
    $os = Get-CimInstance Win32_OperatingSystem | Select-Object Caption, Version, BuildNumber, OSArchitecture
    $totalRamBytes = ($memoryModules | Measure-Object -Property Capacity -Sum).Sum
    $vramMap = Get-TarkovGpuVramMap

    $system = [ordered]@{
        source = "windows_cim"
        captured_at = (Get-Date).ToString("o")
        os = $os
        cpu = $cpu
        gpu = @($gpu | ForEach-Object {
            $gpuName = $_.Name
            $vendor = "unknown"
            $driverPage = $null
            if ($gpuName -match "NVIDIA|GeForce|RTX|GTX") {
                $vendor = "nvidia"
                $driverPage = "https://www.nvidia.com/Download/index.aspx"
            }
            elseif ($gpuName -match "AMD|Radeon") {
                $vendor = "amd"
                $driverPage = "https://www.amd.com/en/support/download/drivers.html"
            }
            elseif ($gpuName -match "Intel|Arc") {
                $vendor = "intel"
                $driverPage = "https://www.intel.com/content/www/us/en/download-center/home.html"
            }

            $vramGb = $null
            $vramSource = "unknown"
            if ($vramMap.ContainsKey($gpuName)) {
                $vramGb = $vramMap[$gpuName]
                $vramSource = "registry"
            }
            elseif ($_.AdapterRAM) {
                $vramGb = [math]::Round($_.AdapterRAM / 1GB, 2)
                $vramSource = "wmi_capped_4gb"
            }

            [ordered]@{
                name = $gpuName
                vendor = $vendor
                vram_gb = $vramGb
                vram_source = $vramSource
                driver_version = $_.DriverVersion
                driver_latest_check = "manual"
                driver_download_page = $driverPage
                current_resolution = if ($_.CurrentHorizontalResolution -and $_.CurrentVerticalResolution) { "$($_.CurrentHorizontalResolution)x$($_.CurrentVerticalResolution)" } else { "unknown" }
            }
        })
        ram = [ordered]@{
            total_gb = if ($totalRamBytes) { [math]::Round($totalRamBytes / 1GB, 2) } else { 0 }
            modules = @($memoryModules | ForEach-Object {
                [ordered]@{
                    manufacturer = $_.Manufacturer
                    capacity_gb = [math]::Round($_.Capacity / 1GB, 2)
                    speed_mhz = $_.Speed
                    configured_clock_speed_mhz = $_.ConfiguredClockSpeed
                }
            })
        }
    }

    if ($IncludePagefile) {
        $pagefiles = @(Get-CimInstance Win32_PageFileUsage -ErrorAction SilentlyContinue)
        $pagefileGb = [math]::Round((($pagefiles | Measure-Object -Property AllocatedBaseSize -Sum).Sum) / 1024, 2)
        $system.pagefile = [ordered]@{
            total_allocated_gb = $pagefileGb
            files = @($pagefiles | ForEach-Object {
                $driveLetter = if ($_.Name -match '^([A-Za-z]):') { $Matches[1] } else { "" }
                [ordered]@{
                    name = $_.Name
                    drive_media_type = (Get-TarkovDriveMediaType -DriveLetter $driveLetter)
                    allocated_gb = [math]::Round($_.AllocatedBaseSize / 1024, 2)
                    current_usage_gb = [math]::Round($_.CurrentUsage / 1024, 2)
                    peak_usage_gb = [math]::Round($_.PeakUsage / 1024, 2)
                }
            })
        }
    }

    return $system
}
