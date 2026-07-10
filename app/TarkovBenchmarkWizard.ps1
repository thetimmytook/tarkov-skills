$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$RootDir = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$ScriptsDir = Join-Path $RootDir "scripts"
. (Join-Path $ScriptsDir "TarkovCommon.ps1")
$RunsDir = Get-TarkovDataDir -SubDir "runs"

$State = [ordered]@{
    RunDir = $null
    SettingsJsonPath = $null
    SystemJsonPath = $null
    FpsCsvPath = $null
    FpsJsonPath = $null
    RunJsonPath = $null
    GameVersion = "unknown"
}

function New-RunDirectory {
    $stamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $path = Join-Path $RunsDir $stamp
    New-Item -ItemType Directory -Force $path | Out-Null
    $State.RunDir = $path
    $State.SettingsJsonPath = Join-Path $path "settings.json"
    $State.SystemJsonPath = Join-Path $path "system.json"
    $State.FpsJsonPath = Join-Path $path "fps.json"
    $State.RunJsonPath = Join-Path $path "run.json"
}

function Invoke-BenchmarkScript {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [string[]]$Arguments = @()
    )

    $scriptPath = Join-Path $ScriptsDir $Name
    if (-not (Test-Path -LiteralPath $scriptPath)) {
        throw "Script not found: $scriptPath"
    }

    & $scriptPath @Arguments
}

function Get-ComboValue {
    param([System.Windows.Forms.ComboBox]$Combo)

    if ([string]::IsNullOrWhiteSpace($Combo.Text)) {
        return "unknown"
    }

    return $Combo.Text.Trim()
}

function Add-Log {
    param([string]$Message)

    $timestamp = Get-Date -Format "HH:mm:ss"
    $txtLog.AppendText("[$timestamp] $Message`r`n")
    $txtLog.SelectionStart = $txtLog.TextLength
    $txtLog.ScrollToCaret()
}

function Set-Status {
    param([string]$Message)
    $lblStatus.Text = $Message
}

function Set-UiEnabled {
    param([bool]$Enabled)

    foreach ($control in @($btnCollect, $btnReadLogs, $btnBrowseCsv, $btnParseCsv, $btnBuildRun, $btnOpenRunFolder, $btnOpenRunJson, $btnUploadTodo)) {
        $control.Enabled = $Enabled
    }
}

$form = New-Object System.Windows.Forms.Form
$form.Text = "Tarkov Performance Benchmark"
$form.StartPosition = "CenterScreen"
$form.Size = New-Object System.Drawing.Size(820, 680)
$form.MinimumSize = New-Object System.Drawing.Size(760, 620)

$font = New-Object System.Drawing.Font("Segoe UI", 9)
$form.Font = $font

$lblTitle = New-Object System.Windows.Forms.Label
$lblTitle.Text = "Tarkov Performance Benchmark"
$lblTitle.Font = New-Object System.Drawing.Font("Segoe UI", 14, [System.Drawing.FontStyle]::Bold)
$lblTitle.Location = New-Object System.Drawing.Point(16, 14)
$lblTitle.Size = New-Object System.Drawing.Size(520, 28)
$form.Controls.Add($lblTitle)

$lblStatus = New-Object System.Windows.Forms.Label
$lblStatus.Text = "Ready. Start by collecting settings and system info."
$lblStatus.Location = New-Object System.Drawing.Point(18, 48)
$lblStatus.Size = New-Object System.Drawing.Size(760, 24)
$form.Controls.Add($lblStatus)

$grpCollect = New-Object System.Windows.Forms.GroupBox
$grpCollect.Text = "1. Local snapshot"
$grpCollect.Location = New-Object System.Drawing.Point(18, 82)
$grpCollect.Size = New-Object System.Drawing.Size(760, 82)
$form.Controls.Add($grpCollect)

$btnCollect = New-Object System.Windows.Forms.Button
$btnCollect.Text = "Collect EFT settings and system info"
$btnCollect.Location = New-Object System.Drawing.Point(16, 32)
$btnCollect.Size = New-Object System.Drawing.Size(250, 30)
$grpCollect.Controls.Add($btnCollect)

$lblCollectResult = New-Object System.Windows.Forms.Label
$lblCollectResult.Text = "Not collected yet."
$lblCollectResult.Location = New-Object System.Drawing.Point(282, 37)
$lblCollectResult.Size = New-Object System.Drawing.Size(455, 22)
$grpCollect.Controls.Add($lblCollectResult)

$grpContext = New-Object System.Windows.Forms.GroupBox
$grpContext.Text = "2. Raid context"
$grpContext.Location = New-Object System.Drawing.Point(18, 176)
$grpContext.Size = New-Object System.Drawing.Size(760, 172)
$form.Controls.Add($grpContext)

$labels = @(
    @{ Text = "Map"; X = 16; Y = 32 },
    @{ Text = "Mode"; X = 266; Y = 32 },
    @{ Text = "Server model"; X = 516; Y = 32 },
    @{ Text = "Weather"; X = 16; Y = 88 },
    @{ Text = "Time of day"; X = 266; Y = 88 },
    @{ Text = "Activity"; X = 516; Y = 88 }
)

foreach ($item in $labels) {
    $label = New-Object System.Windows.Forms.Label
    $label.Text = $item.Text
    $label.Location = New-Object System.Drawing.Point($item.X, $item.Y)
    $label.Size = New-Object System.Drawing.Size(210, 18)
    $grpContext.Controls.Add($label)
}

function New-Combo {
    param(
        [int]$X,
        [int]$Y,
        [string[]]$Items
    )

    $combo = New-Object System.Windows.Forms.ComboBox
    $combo.Location = New-Object System.Drawing.Point($X, $Y)
    $combo.Size = New-Object System.Drawing.Size(210, 24)
    $combo.DropDownStyle = "DropDown"
    [void]$combo.Items.AddRange($Items)
    if ($Items.Count -gt 0) {
        $combo.Text = $Items[0]
    }
    $grpContext.Controls.Add($combo)
    return $combo
}

$cmbMap = New-Combo -X 16 -Y 52 -Items @("unknown", "Streets of Tarkov", "Customs", "Woods", "Shoreline", "Interchange", "Reserve", "Lighthouse", "Factory", "Ground Zero", "The Lab", "Hideout")
$cmbMode = New-Combo -X 266 -Y 52 -Items @("unknown", "PvP online BSG", "PvE BSG server", "PvE local", "Offline practice", "Hideout")
$cmbServer = New-Combo -X 516 -Y 52 -Items @("unknown", "BSG server", "local", "offline", "hideout")
$cmbWeather = New-Combo -X 16 -Y 108 -Items @("unknown", "clear", "cloudy", "rain", "fog", "snow")
$cmbTime = New-Combo -X 266 -Y 108 -Items @("unknown", "day", "night", "dawn/dusk")
$cmbActivity = New-Combo -X 516 -Y 108 -Items @("unknown", "standing still", "walking route", "mixed", "combat")

$lblRoute = New-Object System.Windows.Forms.Label
$lblRoute.Text = "Route / notes"
$lblRoute.Location = New-Object System.Drawing.Point(16, 136)
$lblRoute.Size = New-Object System.Drawing.Size(110, 18)
$grpContext.Controls.Add($lblRoute)

$txtRoute = New-Object System.Windows.Forms.TextBox
$txtRoute.Location = New-Object System.Drawing.Point(128, 132)
$txtRoute.Size = New-Object System.Drawing.Size(470, 24)
$txtRoute.Text = "unknown"
$grpContext.Controls.Add($txtRoute)

$btnReadLogs = New-Object System.Windows.Forms.Button
$btnReadLogs.Text = "Read latest logs"
$btnReadLogs.Location = New-Object System.Drawing.Point(612, 132)
$btnReadLogs.Size = New-Object System.Drawing.Size(124, 28)
$grpContext.Controls.Add($btnReadLogs)

$grpCsv = New-Object System.Windows.Forms.GroupBox
$grpCsv.Text = "3. FPS CSV"
$grpCsv.Location = New-Object System.Drawing.Point(18, 360)
$grpCsv.Size = New-Object System.Drawing.Size(760, 102)
$form.Controls.Add($grpCsv)

$txtCsvPath = New-Object System.Windows.Forms.TextBox
$txtCsvPath.Location = New-Object System.Drawing.Point(16, 30)
$txtCsvPath.Size = New-Object System.Drawing.Size(585, 24)
$grpCsv.Controls.Add($txtCsvPath)

$btnBrowseCsv = New-Object System.Windows.Forms.Button
$btnBrowseCsv.Text = "Browse"
$btnBrowseCsv.Location = New-Object System.Drawing.Point(612, 28)
$btnBrowseCsv.Size = New-Object System.Drawing.Size(124, 28)
$grpCsv.Controls.Add($btnBrowseCsv)

$btnParseCsv = New-Object System.Windows.Forms.Button
$btnParseCsv.Text = "Parse FPS CSV"
$btnParseCsv.Location = New-Object System.Drawing.Point(16, 64)
$btnParseCsv.Size = New-Object System.Drawing.Size(140, 28)
$grpCsv.Controls.Add($btnParseCsv)

$lblFpsResult = New-Object System.Windows.Forms.Label
$lblFpsResult.Text = "No CSV parsed yet."
$lblFpsResult.Location = New-Object System.Drawing.Point(172, 69)
$lblFpsResult.Size = New-Object System.Drawing.Size(560, 20)
$grpCsv.Controls.Add($lblFpsResult)

$grpOutput = New-Object System.Windows.Forms.GroupBox
$grpOutput.Text = "4. Output"
$grpOutput.Location = New-Object System.Drawing.Point(18, 474)
$grpOutput.Size = New-Object System.Drawing.Size(760, 72)
$form.Controls.Add($grpOutput)

$btnBuildRun = New-Object System.Windows.Forms.Button
$btnBuildRun.Text = "Build run.json"
$btnBuildRun.Location = New-Object System.Drawing.Point(16, 28)
$btnBuildRun.Size = New-Object System.Drawing.Size(130, 30)
$grpOutput.Controls.Add($btnBuildRun)

$btnOpenRunFolder = New-Object System.Windows.Forms.Button
$btnOpenRunFolder.Text = "Open run folder"
$btnOpenRunFolder.Location = New-Object System.Drawing.Point(160, 28)
$btnOpenRunFolder.Size = New-Object System.Drawing.Size(130, 30)
$grpOutput.Controls.Add($btnOpenRunFolder)

$btnOpenRunJson = New-Object System.Windows.Forms.Button
$btnOpenRunJson.Text = "Open run.json"
$btnOpenRunJson.Location = New-Object System.Drawing.Point(304, 28)
$btnOpenRunJson.Size = New-Object System.Drawing.Size(130, 30)
$grpOutput.Controls.Add($btnOpenRunJson)

$btnUploadTodo = New-Object System.Windows.Forms.Button
$btnUploadTodo.Text = "Upload form TODO"
$btnUploadTodo.Location = New-Object System.Drawing.Point(448, 28)
$btnUploadTodo.Size = New-Object System.Drawing.Size(130, 30)
$grpOutput.Controls.Add($btnUploadTodo)

$lblOutput = New-Object System.Windows.Forms.Label
$lblOutput.Text = "No run.json yet."
$lblOutput.Location = New-Object System.Drawing.Point(592, 34)
$lblOutput.Size = New-Object System.Drawing.Size(144, 20)
$grpOutput.Controls.Add($lblOutput)

$txtLog = New-Object System.Windows.Forms.TextBox
$txtLog.Multiline = $true
$txtLog.ReadOnly = $true
$txtLog.ScrollBars = "Vertical"
$txtLog.Location = New-Object System.Drawing.Point(18, 558)
$txtLog.Size = New-Object System.Drawing.Size(760, 72)
$txtLog.Anchor = "Left,Right,Bottom"
$form.Controls.Add($txtLog)

$btnCollect.Add_Click({
    try {
        Set-UiEnabled $false
        Set-Status "Collecting EFT settings and system info..."
        if (-not $State.RunDir) {
            New-RunDirectory
            Add-Log "Created run folder: $($State.RunDir)"
        }

        Invoke-BenchmarkScript -Name "read-tarkov-settings.ps1" | Set-Content -LiteralPath $State.SettingsJsonPath -Encoding UTF8
        Invoke-BenchmarkScript -Name "collect-system-info.ps1" -Arguments @("-IncludePagefile") | Set-Content -LiteralPath $State.SystemJsonPath -Encoding UTF8

        $lblCollectResult.Text = "Saved settings.json and system.json"
        Add-Log "Collected settings and system info."
        Set-Status "Snapshot collected. Select an FPS CSV after your capture."
    }
    catch {
        Add-Log "ERROR: $($_.Exception.Message)"
        [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, "Collection failed", "OK", "Error") | Out-Null
        Set-Status "Collection failed."
    }
    finally {
        Set-UiEnabled $true
    }
})

$btnBrowseCsv.Add_Click({
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Title = "Select FPS CSV"
    $dialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
    $dialog.CheckFileExists = $true
    if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        $txtCsvPath.Text = $dialog.FileName
    }
})

$btnReadLogs.Add_Click({
    try {
        Set-UiEnabled $false
        Set-Status "Reading latest Tarkov logs..."
        $contextJson = Invoke-BenchmarkScript -Name "read-tarkov-raid-context.ps1"
        $context = $contextJson | ConvertFrom-Json

        if (-not $context.found) {
            throw $context.message
        }

        if ($context.map -and $context.map -ne "unknown") {
            $cmbMap.Text = $context.map
        }
        # Logs cannot distinguish PvP from PvE on BSG servers (both report RaidMode
        # Online), so the precise mode selection stays with the user.
        if ($context.server_model -and $context.server_model -ne "unknown") {
            $cmbServer.Text = $context.server_model
        }
        if ($context.game_version -and $context.game_version -ne "unknown") {
            $State.GameVersion = $context.game_version
        }

        $logNote = "logs: confidence=$($context.confidence)"
        if ($context.mode -and $context.mode -ne "unknown") {
            $logNote += ", link=$($context.mode)"
        }
        if ($State.GameVersion -ne "unknown") {
            $logNote += ", version=$($State.GameVersion)"
        }
        if ($context.raid_id -and $context.raid_id -ne "unknown") {
            $logNote += ", raid_id=$($context.raid_id)"
        }
        if ($context.queue_time_sec -ne $null) {
            $logNote += ", queue=$($context.queue_time_sec)s"
        }
        if ($context.started_at) {
            $logNote += ", started=$($context.started_at)"
        }
        $txtRoute.Text = $logNote

        Add-Log "Read raid context from logs: map=$($context.map), mode=$($context.mode), version=$($State.GameVersion), confidence=$($context.confidence)"
        Set-Status "Raid context loaded from logs. Select the exact mode (PvP/PvE) and weather/time/activity manually."
    }
    catch {
        Add-Log "ERROR: $($_.Exception.Message)"
        [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, "Log context failed", "OK", "Warning") | Out-Null
        Set-Status "Could not read raid context from logs."
    }
    finally {
        Set-UiEnabled $true
    }
})

$btnParseCsv.Add_Click({
    try {
        Set-UiEnabled $false
        if (-not $State.RunDir) {
            New-RunDirectory
            Add-Log "Created run folder: $($State.RunDir)"
        }

        $csvPath = $txtCsvPath.Text.Trim()
        if (-not $csvPath) {
            throw "Select an FPS CSV first."
        }

        $State.FpsCsvPath = $csvPath
        Set-Status "Parsing FPS CSV..."
        Invoke-BenchmarkScript -Name "parse-fps-csv.ps1" -Arguments @("-Path", $csvPath) | Set-Content -LiteralPath $State.FpsJsonPath -Encoding UTF8
        $fps = Get-Content -Raw -LiteralPath $State.FpsJsonPath | ConvertFrom-Json
        $lblFpsResult.Text = "Avg $($fps.avg_fps) FPS, 1% low $($fps.p1_low_fps), method $($fps.method)"
        Add-Log "Parsed FPS CSV: $csvPath"
        Set-Status "FPS CSV parsed. Fill raid context and build run.json."
    }
    catch {
        Add-Log "ERROR: $($_.Exception.Message)"
        [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, "CSV parsing failed", "OK", "Error") | Out-Null
        Set-Status "CSV parsing failed."
    }
    finally {
        Set-UiEnabled $true
    }
})

$btnBuildRun.Add_Click({
    try {
        Set-UiEnabled $false
        if (-not $State.SettingsJsonPath -or -not (Test-Path -LiteralPath $State.SettingsJsonPath)) {
            throw "Collect settings and system info first."
        }
        if (-not $State.FpsJsonPath -or -not (Test-Path -LiteralPath $State.FpsJsonPath)) {
            throw "Parse an FPS CSV first."
        }

        $route = if ([string]::IsNullOrWhiteSpace($txtRoute.Text)) { "unknown" } else { $txtRoute.Text.Trim() }
        $notes = $route

        $buildArgs = @(
            "-SettingsJsonPath", $State.SettingsJsonPath,
            "-SystemJsonPath", $State.SystemJsonPath,
            "-FpsJsonPath", $State.FpsJsonPath,
            "-Map", (Get-ComboValue $cmbMap),
            "-Mode", (Get-ComboValue $cmbMode),
            "-ServerModel", (Get-ComboValue $cmbServer),
            "-Weather", (Get-ComboValue $cmbWeather),
            "-TimeOfDay", (Get-ComboValue $cmbTime),
            "-Route", $route,
            "-RaidActivity", (Get-ComboValue $cmbActivity),
            "-GameVersion", $State.GameVersion,
            "-Notes", $notes,
            "-OutputPath", $State.RunJsonPath
        )

        Set-Status "Building run.json..."
        Invoke-BenchmarkScript -Name "build-run-json.ps1" -Arguments $buildArgs | Out-Null
        $lblOutput.Text = "Saved: $($State.RunJsonPath)"
        Add-Log "Built run.json: $($State.RunJsonPath)"
        Set-Status "Done. run.json is ready."
    }
    catch {
        Add-Log "ERROR: $($_.Exception.Message)"
        [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, "Build failed", "OK", "Error") | Out-Null
        Set-Status "Build failed."
    }
    finally {
        Set-UiEnabled $true
    }
})

$btnOpenRunFolder.Add_Click({
    if ($State.RunDir -and (Test-Path -LiteralPath $State.RunDir)) {
        Start-Process explorer.exe -ArgumentList $State.RunDir
    }
    else {
        Start-Process explorer.exe -ArgumentList $RunsDir
    }
})

$btnOpenRunJson.Add_Click({
    if ($State.RunJsonPath -and (Test-Path -LiteralPath $State.RunJsonPath)) {
        Start-Process notepad.exe -ArgumentList $State.RunJsonPath
    }
    else {
        [System.Windows.Forms.MessageBox]::Show("No run.json has been created yet.", "Nothing to open", "OK", "Information") | Out-Null
    }
})

$btnUploadTodo.Add_Click({
    if ($State.RunJsonPath -and (Test-Path -LiteralPath $State.RunJsonPath)) {
        Add-Log "Upload form is TODO. run.json is ready: $($State.RunJsonPath)"
        [System.Windows.Forms.MessageBox]::Show("Upload form URL is TODO. For now, keep run.json ready for manual sharing.", "Upload form TODO", "OK", "Information") | Out-Null
    }
    else {
        [System.Windows.Forms.MessageBox]::Show("Build run.json first. Upload form URL is TODO.", "Upload form TODO", "OK", "Information") | Out-Null
    }
})

Add-Log "Wizard started from $RootDir"
[void]$form.ShowDialog()
