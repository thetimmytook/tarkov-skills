$ErrorActionPreference = "Stop"

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
        [string[]]$Files = @("Graphics.ini", "PostFx.ini", "Game.ini", "Control.ini", "Sound.ini")
    )

    $settings = [ordered]@{
        source = "eft_settings"
        settings_dir = $SettingsDir
        captured_at = (Get-Date).ToString("o")
        files = [ordered]@{}
    }

    foreach ($file in $Files) {
        $path = Join-Path $SettingsDir $file
        $settings.files[$file] = [ordered]@{
            path = $path
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
    foreach ($fileProp in $Settings.files.PSObject.Properties) {
        Add-TarkovFlatSettings -Target $flat -FileName $fileProp.Name -Data $fileProp.Value.data
    }
    return $flat
}

function Get-TarkovSystemInfo {
    param([switch]$IncludePagefile)

    $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1 Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed
    $gpu = @(Get-CimInstance Win32_VideoController | Select-Object Name, AdapterRAM, DriverVersion, CurrentHorizontalResolution, CurrentVerticalResolution)
    $memoryModules = @(Get-CimInstance Win32_PhysicalMemory)
    $os = Get-CimInstance Win32_OperatingSystem | Select-Object Caption, Version, BuildNumber, OSArchitecture
    $totalRamBytes = ($memoryModules | Measure-Object -Property Capacity -Sum).Sum

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
            [ordered]@{
                name = $gpuName
                vendor = $vendor
                adapter_ram_gb = if ($_.AdapterRAM) { [math]::Round($_.AdapterRAM / 1GB, 2) } else { $null }
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
                [ordered]@{
                    name = $_.Name
                    allocated_gb = [math]::Round($_.AllocatedBaseSize / 1024, 2)
                    current_usage_gb = [math]::Round($_.CurrentUsage / 1024, 2)
                    peak_usage_gb = [math]::Round($_.PeakUsage / 1024, 2)
                }
            })
        }
    }

    return $system
}
