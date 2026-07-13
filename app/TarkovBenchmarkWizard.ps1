$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class TarkovWindowChrome {
    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);
}
'@

$RootDir = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$ScriptsDir = Join-Path $RootDir "scripts"
. (Join-Path $ScriptsDir "TarkovCommon.ps1")
$PerformanceFormUrl = "https://forms.gle/D692T2Umd5ktD5wj8"
$CrashFormUrl = "https://forms.gle/yvKPPWkzGVFrtGjG7"
$Theme = @{
    Background = [System.Drawing.Color]::FromArgb(20, 21, 20)
    Surface = [System.Drawing.Color]::FromArgb(38, 38, 36)
    SurfaceRaised = [System.Drawing.Color]::FromArgb(48, 48, 45)
    Border = [System.Drawing.Color]::FromArgb(12, 12, 12)
    Text = [System.Drawing.Color]::FromArgb(211, 205, 184)
    Muted = [System.Drawing.Color]::FromArgb(150, 143, 121)
    Accent = [System.Drawing.Color]::FromArgb(171, 158, 111)
    AccentDark = [System.Drawing.Color]::FromArgb(103, 94, 68)
    Ready = [System.Drawing.Color]::FromArgb(92, 154, 90)
    Warning = [System.Drawing.Color]::FromArgb(191, 151, 68)
    Error = [System.Drawing.Color]::FromArgb(163, 77, 71)
}

$State = [ordered]@{
    BenchmarkPath = Join-Path (Get-TarkovDataDir) "benchmark.json"
    CaptureStartedAt = $null
    CaptureDurationSec = 120
    CaptureJob = $null
    CaptureCanceled = $false
    Collecting = $false
    PresentMon = $null
}

function Invoke-BenchmarkScript {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [hashtable]$Parameters = @{}
    )

    $scriptPath = Join-Path $ScriptsDir $Name
    if (-not (Test-Path -LiteralPath $scriptPath)) {
        throw "Script not found: $scriptPath"
    }

    & $scriptPath @Parameters
}

function Get-TarkovProcess {
    return Get-Process -Name "EscapeFromTarkov" -ErrorAction SilentlyContinue | Select-Object -First 1
}

function Test-TarkovRunning {
    return $null -ne (Get-TarkovProcess)
}

function Set-Status {
    param(
        [string]$Text,
        [System.Drawing.Color]$Color
    )

    $lblStatus.Text = $Text
    $lblStatus.ForeColor = if ($Color.IsEmpty) { $Theme.Text } else { $Color }
}

function Set-TarkovForm {
    param([System.Windows.Forms.Form]$Dialog)

    $Dialog.BackColor = $Theme.Background
    $Dialog.ForeColor = $Theme.Text
}

function Set-TarkovLabel {
    param(
        [System.Windows.Forms.Control]$Control,
        [switch]$Muted
    )

    $Control.ForeColor = if ($Muted) { $Theme.Muted } else { $Theme.Text }
    $Control.BackColor = [System.Drawing.Color]::Transparent
}

function Set-TarkovGroup {
    param([System.Windows.Forms.GroupBox]$Group)

    $Group.BackColor = $Theme.Surface
    $Group.ForeColor = $Theme.Accent
}

function Update-TarkovButtonAppearance {
    param([System.Windows.Forms.Button]$Button)

    if (-not $Button.Enabled) {
        $Button.BackColor = $Theme.Surface
        $Button.ForeColor = $Theme.Muted
        $Button.FlatAppearance.BorderColor = $Theme.Border
        $Button.FlatAppearance.MouseOverBackColor = $Theme.Surface
        $Button.FlatAppearance.MouseDownBackColor = $Theme.Surface
        return
    }

    if ($Button.Tag -eq "primary") {
        $Button.BackColor = $Theme.Accent
        $Button.ForeColor = $Theme.Background
        $Button.FlatAppearance.BorderColor = $Theme.Accent
        $Button.FlatAppearance.MouseOverBackColor = [System.Drawing.Color]::FromArgb(194, 180, 128)
        $Button.FlatAppearance.MouseDownBackColor = $Theme.AccentDark
    }
    else {
        $Button.BackColor = $Theme.SurfaceRaised
        $Button.ForeColor = $Theme.Text
        $Button.FlatAppearance.BorderColor = $Theme.AccentDark
        $Button.FlatAppearance.MouseOverBackColor = [System.Drawing.Color]::FromArgb(63, 62, 57)
        $Button.FlatAppearance.MouseDownBackColor = $Theme.Border
    }
}

function Set-TarkovButton {
    param(
        [System.Windows.Forms.Button]$Button,
        [switch]$Primary
    )

    $Button.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $Button.FlatAppearance.BorderSize = 1
    $Button.UseVisualStyleBackColor = $false
    $Button.Tag = if ($Primary) { "primary" } else { "secondary" }
    $Button.Add_EnabledChanged({ Update-TarkovButtonAppearance -Button $this })
    Update-TarkovButtonAppearance -Button $Button
}

function Set-TarkovTextInput {
    param([System.Windows.Forms.Control]$Control)

    $Control.BackColor = $Theme.Background
    $Control.ForeColor = $Theme.Text
}

function Show-TarkovMessage {
    param(
        [System.Windows.Forms.IWin32Window]$Owner,
        [string]$Text,
        [string]$Caption,
        [System.Windows.Forms.MessageBoxButtons]$Buttons,
        [System.Windows.Forms.MessageBoxIcon]$Icon
    )

    return [System.Windows.Forms.MessageBox]::Show($Owner, $Text, $Caption, $Buttons, $Icon)
}

function Test-ActiveRaidContext {
    param(
        [pscustomobject]$Context,
        [object]$ProcessStartedAt
    )

    if (-not $Context -or -not $Context.found -or [string]::IsNullOrWhiteSpace($Context.started_at)) {
        return $false
    }

    $startedAt = [datetime]::MinValue
    if (-not [datetime]::TryParse($Context.started_at, [ref]$startedAt)) {
        return $false
    }
    if ($ProcessStartedAt -and $startedAt -lt $ProcessStartedAt) {
        return $false
    }

    if (-not [string]::IsNullOrWhiteSpace($Context.ended_at)) {
        $endedAt = [datetime]::MinValue
        if ([datetime]::TryParse($Context.ended_at, [ref]$endedAt) -and $endedAt -ge $startedAt) {
            return $false
        }
    }

    return $true
}

function Show-RaidRequired {
    param([string]$Message)

    Show-TarkovMessage -Owner $form -Text $Message -Caption "Ready when you are" -Buttons ([System.Windows.Forms.MessageBoxButtons]::OK) -Icon ([System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
}

function Update-Readiness {
    if ($State.Collecting) {
        return
    }

    $State.PresentMon = Invoke-BenchmarkScript -Name "check-presentmon.ps1" | ConvertFrom-Json
    $chkPresentMon.Checked = [bool]$State.PresentMon.found
    $chkPresentMon.Text = if ($State.PresentMon.found) { "PresentMon ready" } else { "PresentMon required" }
    $chkPresentMon.ForeColor = if ($State.PresentMon.found) { [System.Drawing.Color]::ForestGreen } else { [System.Drawing.Color]::Firebrick }
    if (-not $State.PresentMon.found) {
        $btnStart.Enabled = $false
        $lblAvailability.Text = "Install PresentMon first"
        Set-Status "PresentMon is needed for FPS capture." -Color $Theme.Warning
        return
    }

    $tarkovProcess = Get-TarkovProcess
    if (-not $tarkovProcess) {
        $btnStart.Enabled = $true
        $lblAvailability.Text = "Next: run Tarkov"
        Set-Status "Run Tarkov, then enter a raid to collect performance data." -Color $Theme.Warning
        return
    }

    try {
        $raidContext = Invoke-BenchmarkScript -Name "read-tarkov-raid-context.ps1" | ConvertFrom-Json
    }
    catch {
        $raidContext = $null
    }
    if (-not (Test-ActiveRaidContext -Context $raidContext -ProcessStartedAt $tarkovProcess.StartTime)) {
        $btnStart.Enabled = $true
        $lblAvailability.Text = "Next: start a raid"
        Set-Status "Enter a raid to collect performance data." -Color $Theme.Warning
        return
    }

    $btnStart.Enabled = $true
    $lblAvailability.Text = "Ready"
    Set-Status "Raid detected. Ready to collect performance data." -Color $Theme.Ready
}

function Show-PresentMonSetup {
    $installDir = Get-TarkovDataDir -SubDir "tools\PresentMon"
    $presentMon = Invoke-BenchmarkScript -Name "check-presentmon.ps1" | ConvertFrom-Json
    $dialog = New-Object System.Windows.Forms.Form
    $dialog.Text = if ($presentMon.found) { "PresentMon ready" } else { "Install PresentMon" }
    $dialog.StartPosition = "CenterParent"
    $dialog.FormBorderStyle = "FixedDialog"
    $dialog.MaximizeBox = $false
    $dialog.MinimizeBox = $false
    $dialog.ClientSize = New-Object System.Drawing.Size(520, 250)
    $dialog.Font = New-Object System.Drawing.Font("Segoe UI", 9)
    Set-TarkovForm -Dialog $dialog

    $linkDownload = New-Object System.Windows.Forms.LinkLabel
    $linkDownload.Text = if ($presentMon.found) { "Open the official PresentMon GitHub Releases page." } else { "1. Download PresentMon from its official GitHub Releases page." }
    $linkDownload.Location = New-Object System.Drawing.Point(18, 18)
    $linkDownload.Size = New-Object System.Drawing.Size(480, 22)
    $linkDownload.LinkColor = $Theme.Accent
    $linkDownload.ActiveLinkColor = $Theme.Warning
    $linkDownload.VisitedLinkColor = $Theme.Accent
    $dialog.Controls.Add($linkDownload)

    $lblDestination = New-Object System.Windows.Forms.Label
    $lblDestination.Text = if ($presentMon.found) { "PresentMon.exe was found in this folder:" } else { "2. Extract PresentMon.exe into this folder:" }
    $lblDestination.Location = New-Object System.Drawing.Point(18, 52)
    $lblDestination.Size = New-Object System.Drawing.Size(480, 22)
    Set-TarkovLabel -Control $lblDestination -Muted
    $dialog.Controls.Add($lblDestination)

    $txtInstallDir = New-Object System.Windows.Forms.TextBox
    $txtInstallDir.Text = $installDir
    $txtInstallDir.ReadOnly = $true
    $txtInstallDir.Location = New-Object System.Drawing.Point(18, 78)
    $txtInstallDir.Size = New-Object System.Drawing.Size(480, 24)
    Set-TarkovTextInput -Control $txtInstallDir
    $dialog.Controls.Add($txtInstallDir)

    $lblPermission = New-Object System.Windows.Forms.Label
    $lblPermission.Text = "PresentMon starts without elevation. Windows asks for permission only if it is required."
    $lblPermission.Location = New-Object System.Drawing.Point(18, 116)
    $lblPermission.Size = New-Object System.Drawing.Size(480, 38)
    Set-TarkovLabel -Control $lblPermission -Muted
    $dialog.Controls.Add($lblPermission)

    $btnOpenFolder = New-Object System.Windows.Forms.Button
    $btnOpenFolder.Text = "Open folder"
    $btnOpenFolder.Location = New-Object System.Drawing.Point(18, 184)
    $btnOpenFolder.Size = New-Object System.Drawing.Size(105, 32)
    Set-TarkovButton -Button $btnOpenFolder
    $dialog.Controls.Add($btnOpenFolder)

    $btnCopyPath = New-Object System.Windows.Forms.Button
    $btnCopyPath.Text = "Copy folder path"
    $btnCopyPath.Location = New-Object System.Drawing.Point(135, 184)
    $btnCopyPath.Size = New-Object System.Drawing.Size(120, 32)
    Set-TarkovButton -Button $btnCopyPath
    $dialog.Controls.Add($btnCopyPath)

    $btnClose = New-Object System.Windows.Forms.Button
    $btnClose.Text = "Close"
    $btnClose.DialogResult = [System.Windows.Forms.DialogResult]::Cancel
    $btnClose.Location = New-Object System.Drawing.Point(424, 184)
    $btnClose.Size = New-Object System.Drawing.Size(74, 32)
    Set-TarkovButton -Button $btnClose
    $dialog.Controls.Add($btnClose)

    $linkDownload.Add_LinkClicked({ Start-Process "https://github.com/GameTechDev/PresentMon/releases" })
    $btnOpenFolder.Add_Click({
        New-Item -ItemType Directory -Force $installDir | Out-Null
        Start-Process explorer.exe -ArgumentList $installDir
    })
    $btnCopyPath.Add_Click({
        [System.Windows.Forms.Clipboard]::SetText($installDir)
        $txtInstallDir.SelectAll()
        $txtInstallDir.Focus()
    })

    [void]$dialog.ShowDialog($form)
}

function Get-SafeCrashText {
    param([string]$Text)

    $safeText = Hide-TarkovUserPath -Text $Text
    return [regex]::Replace($safeText, '(?i)\b[a-z]:[\\/][^\r\n"'']*', '<path>')
}

function Get-CrashExceptionDetails {
    param([object]$ErrorRecord)

    if (-not $ErrorRecord) {
        return ""
    }

    $lines = New-Object System.Collections.Generic.List[string]
    if ($ErrorRecord.FullyQualifiedErrorId) {
        [void]$lines.Add("Error id: $($ErrorRecord.FullyQualifiedErrorId)")
    }
    if ($ErrorRecord.CategoryInfo) {
        [void]$lines.Add("Category: $($ErrorRecord.CategoryInfo)")
    }
    if ($ErrorRecord.InvocationInfo) {
        if ($ErrorRecord.InvocationInfo.MyCommand) {
            [void]$lines.Add("Command: $($ErrorRecord.InvocationInfo.MyCommand)")
        }
        if ($ErrorRecord.InvocationInfo.PositionMessage) {
            [void]$lines.Add("Position: $($ErrorRecord.InvocationInfo.PositionMessage)")
        }
    }
    if ($ErrorRecord.ScriptStackTrace) {
        [void]$lines.Add("PowerShell stack:`n$($ErrorRecord.ScriptStackTrace)")
    }

    $exception = if ($ErrorRecord.Exception) { $ErrorRecord.Exception } elseif ($ErrorRecord -is [System.Exception]) { $ErrorRecord } else { $null }
    $depth = 0
    while ($exception) {
        $label = if ($depth -eq 0) { "Exception" } else { "Inner exception $depth" }
        [void]$lines.Add("$label type: $($exception.GetType().FullName)")
        if ($exception.StackTrace) {
            [void]$lines.Add("$label stack:`n$($exception.StackTrace)")
        }
        $exception = $exception.InnerException
        $depth++
    }

    return $lines -join [Environment]::NewLine
}

function New-CrashReport {
    param(
        [string]$Stage,
        [string]$Message,
        [string]$Details = ""
    )

    $raidContext = $null
    try {
        $raidContext = Invoke-BenchmarkScript -Name "read-tarkov-raid-context.ps1" | ConvertFrom-Json
    }
    catch {
        $raidContext = $null
    }

    $presentMonStatus = if ($State.PresentMon -and $State.PresentMon.found) { "available" } else { "missing or unchecked" }
    $report = @(
        "Tarkov Performance Benchmark crash report"
        "Timestamp: $((Get-Date).ToString('o'))"
        "Stage: $Stage"
        "Message: $(Get-SafeCrashText -Text $Message)"
        "PowerShell: $($PSVersionTable.PSVersion)"
        "PresentMon: $presentMonStatus"
        "Tarkov running: $([bool](Get-TarkovProcess))"
        "Map: $(if ($raidContext -and $raidContext.map) { $raidContext.map } else { 'unknown' })"
        "Raid state: $(if ($raidContext -and $raidContext.ended_at) { 'ended' } elseif ($raidContext -and $raidContext.started_at) { 'active' } else { 'unknown' })"
        $(if ($Details) { "Error details:`n$(Get-SafeCrashText -Text $Details)" })
    ) -join [Environment]::NewLine

    $report = Get-SafeCrashText -Text $report
    $reportPath = ""
    try {
        $reportDirectory = Get-TarkovDataDir -SubDir "reports"
        $reportPath = Join-Path $reportDirectory "crash_$(Get-Date -Format 'yyyyMMdd_HHmmss').txt"
        Set-Content -LiteralPath $reportPath -Value $report -Encoding UTF8
    }
    catch {
        $reportPath = ""
    }

    $copied = $false
    try {
        [System.Windows.Forms.Clipboard]::SetText($report)
        $copied = $true
    }
    catch {
        $copied = $false
    }

    return [pscustomobject]@{
        text = $report
        copied = $copied
        saved = [bool]$reportPath
    }
}

function Offer-CrashReport {
    param(
        [string]$Stage,
        [string]$Message,
        [string]$Details = "",
        [string]$Caption = "Collection failed"
    )

    $report = New-CrashReport -Stage $Stage -Message $Message -Details $Details
    $copyStatus = if ($report.copied) { "A diagnostic report was copied to the clipboard." } else { "A diagnostic report was saved locally." }
    $choice = Show-TarkovMessage -Owner $form -Text "$(Get-SafeCrashText -Text $Message)`n`n$copyStatus`nOpen the crash report form?" -Caption $Caption -Buttons ([System.Windows.Forms.MessageBoxButtons]::YesNo) -Icon ([System.Windows.Forms.MessageBoxIcon]::Error)
    if ($choice -eq [System.Windows.Forms.DialogResult]::Yes) {
        Start-Process $CrashFormUrl
    }
}

function Get-ContextAnswers {
    param([pscustomobject]$RaidContext)

    $dialog = New-Object System.Windows.Forms.Form
    $dialog.Text = "Benchmark details"
    $dialog.StartPosition = "CenterParent"
    $dialog.FormBorderStyle = "FixedDialog"
    $dialog.MaximizeBox = $false
    $dialog.MinimizeBox = $false
    $dialog.ClientSize = New-Object System.Drawing.Size(430, 290)
    $dialog.Font = New-Object System.Drawing.Font("Segoe UI", 9)
    Set-TarkovForm -Dialog $dialog

    $map = if ($RaidContext.map -and $RaidContext.map -ne "unknown") { $RaidContext.map } else { "Could not identify map from Tarkov logs" }
    $lblMap = New-Object System.Windows.Forms.Label
    $lblMap.Text = "Map: $map"
    $lblMap.Location = New-Object System.Drawing.Point(18, 18)
    $lblMap.Size = New-Object System.Drawing.Size(395, 22)
    Set-TarkovLabel -Control $lblMap -Muted
    $dialog.Controls.Add($lblMap)

    $grpExecution = New-Object System.Windows.Forms.GroupBox
    $grpExecution.Text = "Where did you play?"
    $grpExecution.Location = New-Object System.Drawing.Point(18, 52)
    $grpExecution.Size = New-Object System.Drawing.Size(395, 58)
    Set-TarkovGroup -Group $grpExecution
    $dialog.Controls.Add($grpExecution)

    $radioBsg = New-Object System.Windows.Forms.RadioButton
    $radioBsg.Text = "BSG servers"
    $radioBsg.Location = New-Object System.Drawing.Point(16, 24)
    $radioBsg.Size = New-Object System.Drawing.Size(145, 22)
    $radioBsg.BackColor = $Theme.Surface
    $radioBsg.ForeColor = $Theme.Text
    $grpExecution.Controls.Add($radioBsg)

    $radioLocal = New-Object System.Windows.Forms.RadioButton
    $radioLocal.Text = "Local"
    $radioLocal.Location = New-Object System.Drawing.Point(195, 24)
    $radioLocal.Size = New-Object System.Drawing.Size(145, 22)
    $radioLocal.BackColor = $Theme.Surface
    $radioLocal.ForeColor = $Theme.Text
    $grpExecution.Controls.Add($radioLocal)

    $lblWeather = New-Object System.Windows.Forms.Label
    $lblWeather.Text = "Weather"
    $lblWeather.Location = New-Object System.Drawing.Point(18, 126)
    $lblWeather.Size = New-Object System.Drawing.Size(180, 18)
    Set-TarkovLabel -Control $lblWeather -Muted
    $dialog.Controls.Add($lblWeather)

    $cmbWeather = New-Object System.Windows.Forms.ComboBox
    $cmbWeather.DropDownStyle = "DropDownList"
    [void]$cmbWeather.Items.AddRange(@("Choose weather", "Clear", "Cloudy", "Rain", "Fog", "Snow", "Not sure"))
    $cmbWeather.SelectedIndex = 0
    $cmbWeather.Location = New-Object System.Drawing.Point(18, 146)
    $cmbWeather.Size = New-Object System.Drawing.Size(185, 24)
    Set-TarkovTextInput -Control $cmbWeather
    $dialog.Controls.Add($cmbWeather)

    $lblTime = New-Object System.Windows.Forms.Label
    $lblTime.Text = "Time of day"
    $lblTime.Location = New-Object System.Drawing.Point(228, 126)
    $lblTime.Size = New-Object System.Drawing.Size(180, 18)
    Set-TarkovLabel -Control $lblTime -Muted
    $dialog.Controls.Add($lblTime)

    $cmbTime = New-Object System.Windows.Forms.ComboBox
    $cmbTime.DropDownStyle = "DropDownList"
    [void]$cmbTime.Items.AddRange(@("Choose time", "Day", "Night", "Dawn / dusk", "Not sure"))
    $cmbTime.SelectedIndex = 0
    $cmbTime.Location = New-Object System.Drawing.Point(228, 146)
    $cmbTime.Size = New-Object System.Drawing.Size(185, 24)
    Set-TarkovTextInput -Control $cmbTime
    $dialog.Controls.Add($cmbTime)

    $btnSave = New-Object System.Windows.Forms.Button
    $btnSave.Text = "Save benchmark"
    $btnSave.Location = New-Object System.Drawing.Point(282, 234)
    $btnSave.Size = New-Object System.Drawing.Size(131, 32)
    $btnSave.Enabled = $false
    Set-TarkovButton -Button $btnSave -Primary
    $dialog.Controls.Add($btnSave)

    $updateSaveButton = {
        $btnSave.Enabled = ($radioBsg.Checked -or $radioLocal.Checked) -and $cmbWeather.SelectedIndex -gt 0 -and $cmbTime.SelectedIndex -gt 0
    }
    $radioBsg.Add_CheckedChanged($updateSaveButton)
    $radioLocal.Add_CheckedChanged($updateSaveButton)
    $cmbWeather.Add_SelectedIndexChanged($updateSaveButton)
    $cmbTime.Add_SelectedIndexChanged($updateSaveButton)

    $btnSave.Add_Click({
        if (-not $radioBsg.Checked -and -not $radioLocal.Checked) {
            Show-TarkovMessage -Owner $dialog -Text "Choose BSG servers or Local." -Caption "Benchmark details" -Buttons ([System.Windows.Forms.MessageBoxButtons]::OK) -Icon ([System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
            return
        }
        if ($cmbWeather.SelectedIndex -eq 0 -or $cmbTime.SelectedIndex -eq 0) {
            Show-TarkovMessage -Owner $dialog -Text "Choose weather and time of day, or choose Not sure." -Caption "Benchmark details" -Buttons ([System.Windows.Forms.MessageBoxButtons]::OK) -Icon ([System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
            return
        }
        $dialog.DialogResult = [System.Windows.Forms.DialogResult]::OK
        $dialog.Close()
    })

    if ($dialog.ShowDialog($form) -ne [System.Windows.Forms.DialogResult]::OK) {
        return $null
    }

    $weatherMap = @{ "Clear" = "clear"; "Cloudy" = "cloudy"; "Rain" = "rain"; "Fog" = "fog"; "Snow" = "snow"; "Not sure" = "unknown" }
    $timeMap = @{ "Day" = "day"; "Night" = "night"; "Dawn / dusk" = "dawn_dusk"; "Not sure" = "unknown" }
    return [pscustomobject]@{
        execution = if ($radioBsg.Checked) { "bsg_servers" } else { "local" }
        weather = $weatherMap[$cmbWeather.SelectedItem]
        time_of_day = $timeMap[$cmbTime.SelectedItem]
    }
}

function Save-BenchmarkRun {
    param(
        [pscustomobject]$Capture,
        [pscustomobject]$Context,
        [pscustomobject]$Answers
    )

    $sessionDir = Join-Path ([System.IO.Path]::GetTempPath()) "TarkovSkills-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Force $sessionDir | Out-Null
    try {
        $settingsPath = Join-Path $sessionDir "settings.json"
        $systemPath = Join-Path $sessionDir "system.json"
        $fpsPath = Join-Path $sessionDir "fps.json"
        $Capture.settings_json | Set-Content -LiteralPath $settingsPath -Encoding UTF8
        $Capture.system_json | Set-Content -LiteralPath $systemPath -Encoding UTF8
        $Capture.fps_json | Set-Content -LiteralPath $fpsPath -Encoding UTF8

        $resultJson = Invoke-BenchmarkScript -Name "add-benchmark-run.ps1" -Parameters @{
            SettingsJsonPath = $settingsPath
            SystemJsonPath = $systemPath
            FpsJsonPath = $fpsPath
            Execution = $Answers.execution
            Weather = $Answers.weather
            TimeOfDay = $Answers.time_of_day
            Map = $Context.map
            GameVersion = $Context.game_version
            BenchmarkPath = $State.BenchmarkPath
        }
        return $resultJson | ConvertFrom-Json
    }
    finally {
        Remove-Item -LiteralPath $sessionDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$form = New-Object System.Windows.Forms.Form
$form.Text = "Tarkov Performance Benchmark"
$form.StartPosition = "CenterScreen"
$form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::None
$form.ShowIcon = $false
$form.ClientSize = New-Object System.Drawing.Size(680, 330)
$form.MinimumSize = New-Object System.Drawing.Size(680, 330)
$form.MaximumSize = New-Object System.Drawing.Size(680, 330)
$form.Font = New-Object System.Drawing.Font("Segoe UI", 9)
Set-TarkovForm -Dialog $form

$windowHeader = New-Object System.Windows.Forms.Panel
$windowHeader.BackColor = $Theme.Surface
$windowHeader.Location = New-Object System.Drawing.Point(0, 0)
$windowHeader.Size = New-Object System.Drawing.Size(680, 40)
$form.Controls.Add($windowHeader)

$windowHeaderTitle = New-Object System.Windows.Forms.Label
$windowHeaderTitle.Text = "TARKOV PERFORMANCE BENCHMARK"
$windowHeaderTitle.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)
$windowHeaderTitle.ForeColor = $Theme.Accent
$windowHeaderTitle.BackColor = $Theme.Surface
$windowHeaderTitle.Location = New-Object System.Drawing.Point(18, 11)
$windowHeaderTitle.Size = New-Object System.Drawing.Size(360, 20)
$windowHeader.Controls.Add($windowHeaderTitle)

$btnWindowClose = New-Object System.Windows.Forms.Button
$btnWindowClose.Text = "X"
$btnWindowClose.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
$btnWindowClose.FlatAppearance.BorderSize = 0
$btnWindowClose.FlatAppearance.MouseOverBackColor = $Theme.Error
$btnWindowClose.FlatAppearance.MouseDownBackColor = $Theme.Border
$btnWindowClose.BackColor = $Theme.Surface
$btnWindowClose.ForeColor = $Theme.Text
$btnWindowClose.Location = New-Object System.Drawing.Point(636, 5)
$btnWindowClose.Size = New-Object System.Drawing.Size(34, 30)
$windowHeader.Controls.Add($btnWindowClose)

$startWindowDrag = {
    param($sender, $eventArgs)
    if ($eventArgs.Button -eq [System.Windows.Forms.MouseButtons]::Left) {
        [TarkovWindowChrome]::ReleaseCapture() | Out-Null
        [void][TarkovWindowChrome]::SendMessage($form.Handle, 0xA1, [IntPtr]2, [IntPtr]::Zero)
    }
}
$windowHeader.Add_MouseDown($startWindowDrag)
$windowHeaderTitle.Add_MouseDown($startWindowDrag)
$btnWindowClose.Add_Click({ $form.Close() })

$lblTitle = New-Object System.Windows.Forms.Label
$lblTitle.Text = "Tarkov Performance Benchmark"
$lblTitle.Font = New-Object System.Drawing.Font("Segoe UI", 14, [System.Drawing.FontStyle]::Bold)
$lblTitle.Location = New-Object System.Drawing.Point(18, 16)
$lblTitle.Size = New-Object System.Drawing.Size(500, 30)
Set-TarkovLabel -Control $lblTitle
$lblTitle.ForeColor = $Theme.Accent
$lblTitle.Visible = $false
$form.Controls.Add($lblTitle)

$lblStatus = New-Object System.Windows.Forms.Label
$lblStatus.Location = New-Object System.Drawing.Point(20, 52)
$lblStatus.Size = New-Object System.Drawing.Size(635, 22)
Set-TarkovLabel -Control $lblStatus -Muted
$form.Controls.Add($lblStatus)

$statusLine = New-Object System.Windows.Forms.Panel
$statusLine.BackColor = $Theme.AccentDark
$statusLine.Location = New-Object System.Drawing.Point(18, 78)
$statusLine.Size = New-Object System.Drawing.Size(644, 2)
$form.Controls.Add($statusLine)

$grpCollection = New-Object System.Windows.Forms.GroupBox
$grpCollection.Text = "Collection"
$grpCollection.Location = New-Object System.Drawing.Point(18, 88)
$grpCollection.Size = New-Object System.Drawing.Size(644, 112)
Set-TarkovGroup -Group $grpCollection
$form.Controls.Add($grpCollection)

$lblDuration = New-Object System.Windows.Forms.Label
$lblDuration.Text = "Capture duration"
$lblDuration.Location = New-Object System.Drawing.Point(16, 30)
$lblDuration.Size = New-Object System.Drawing.Size(120, 20)
Set-TarkovLabel -Control $lblDuration -Muted
$grpCollection.Controls.Add($lblDuration)

$radioTwoMinutes = New-Object System.Windows.Forms.RadioButton
$radioTwoMinutes.Text = "2 minutes"
$radioTwoMinutes.Checked = $true
$radioTwoMinutes.Location = New-Object System.Drawing.Point(16, 56)
$radioTwoMinutes.Size = New-Object System.Drawing.Size(100, 24)
$radioTwoMinutes.BackColor = $Theme.Surface
$radioTwoMinutes.ForeColor = $Theme.Text
$grpCollection.Controls.Add($radioTwoMinutes)

$radioFourMinutes = New-Object System.Windows.Forms.RadioButton
$radioFourMinutes.Text = "4 minutes"
$radioFourMinutes.Location = New-Object System.Drawing.Point(130, 56)
$radioFourMinutes.Size = New-Object System.Drawing.Size(100, 24)
$radioFourMinutes.BackColor = $Theme.Surface
$radioFourMinutes.ForeColor = $Theme.Text
$grpCollection.Controls.Add($radioFourMinutes)

$btnStart = New-Object System.Windows.Forms.Button
$btnStart.Text = "Start collection"
$btnStart.Location = New-Object System.Drawing.Point(420, 29)
$btnStart.Size = New-Object System.Drawing.Size(128, 34)
$btnStart.Enabled = $false
Set-TarkovButton -Button $btnStart -Primary
$grpCollection.Controls.Add($btnStart)

$btnCancel = New-Object System.Windows.Forms.Button
$btnCancel.Text = "Cancel"
$btnCancel.Location = New-Object System.Drawing.Point(558, 29)
$btnCancel.Size = New-Object System.Drawing.Size(62, 34)
$btnCancel.Enabled = $false
Set-TarkovButton -Button $btnCancel
$grpCollection.Controls.Add($btnCancel)

$chkPresentMon = New-Object System.Windows.Forms.CheckBox
$chkPresentMon.AutoCheck = $false
$chkPresentMon.TabStop = $false
$chkPresentMon.Text = "PresentMon required"
$chkPresentMon.ForeColor = [System.Drawing.Color]::Firebrick
$chkPresentMon.Location = New-Object System.Drawing.Point(280, 29)
$chkPresentMon.Size = New-Object System.Drawing.Size(130, 22)
$chkPresentMon.BackColor = $Theme.Surface
$grpCollection.Controls.Add($chkPresentMon)

$lblAvailability = New-Object System.Windows.Forms.Label
$lblAvailability.Location = New-Object System.Drawing.Point(420, 69)
$lblAvailability.Size = New-Object System.Drawing.Size(200, 22)
Set-TarkovLabel -Control $lblAvailability -Muted
$grpCollection.Controls.Add($lblAvailability)

$linkPresentMon = New-Object System.Windows.Forms.LinkLabel
$linkPresentMon.Text = "PresentMon setup"
$linkPresentMon.Location = New-Object System.Drawing.Point(280, 58)
$linkPresentMon.Size = New-Object System.Drawing.Size(110, 24)
$linkPresentMon.LinkColor = $Theme.Accent
$linkPresentMon.ActiveLinkColor = $Theme.Warning
$linkPresentMon.VisitedLinkColor = $Theme.Accent
$grpCollection.Controls.Add($linkPresentMon)

$grpResult = New-Object System.Windows.Forms.GroupBox
$grpResult.Text = "Benchmark data"
$grpResult.Location = New-Object System.Drawing.Point(18, 214)
$grpResult.Size = New-Object System.Drawing.Size(644, 86)
Set-TarkovGroup -Group $grpResult
$form.Controls.Add($grpResult)

$bottomLine = New-Object System.Windows.Forms.Panel
$bottomLine.BackColor = $Theme.Border
$bottomLine.Location = New-Object System.Drawing.Point(18, 315)
$bottomLine.Size = New-Object System.Drawing.Size(644, 1)
$form.Controls.Add($bottomLine)

$lblResult = New-Object System.Windows.Forms.Label
$lblResult.Text = "No benchmark data saved yet."
$lblResult.Location = New-Object System.Drawing.Point(16, 30)
$lblResult.Size = New-Object System.Drawing.Size(360, 22)
Set-TarkovLabel -Control $lblResult -Muted
$grpResult.Controls.Add($lblResult)

$btnOpenFolder = New-Object System.Windows.Forms.Button
$btnOpenFolder.Text = "Open folder"
$btnOpenFolder.Location = New-Object System.Drawing.Point(390, 25)
$btnOpenFolder.Size = New-Object System.Drawing.Size(105, 30)
$btnOpenFolder.Enabled = $false
Set-TarkovButton -Button $btnOpenFolder
$grpResult.Controls.Add($btnOpenFolder)

$btnUpload = New-Object System.Windows.Forms.Button
$btnUpload.Text = "Upload"
$btnUpload.Location = New-Object System.Drawing.Point(510, 25)
$btnUpload.Size = New-Object System.Drawing.Size(110, 30)
$btnUpload.Enabled = $false
Set-TarkovButton -Button $btnUpload -Primary
$grpResult.Controls.Add($btnUpload)

function Update-BenchmarkDataAvailability {
    $btnOpenFolder.Enabled = Test-Path -LiteralPath $State.BenchmarkPath
    $btnUpload.Enabled = $false
    if (-not $btnOpenFolder.Enabled) {
        return
    }

    try {
        $benchmark = Get-Content -Raw -LiteralPath $State.BenchmarkPath | ConvertFrom-Json
        $runs = @($benchmark.runs)
        if ($runs.Count -eq 0) {
            return
        }

        $btnUpload.Enabled = $true
        $latestRun = $runs[-1]
        if ($latestRun.fps.avg_fps -and $latestRun.fps.p1_low_fps) {
            $lblResult.Text = "Saved $($runs.Count) runs. Latest avg $($latestRun.fps.avg_fps) FPS, 1% low $($latestRun.fps.p1_low_fps)."
        }
        else {
            $lblResult.Text = "Saved $($runs.Count) benchmark runs."
        }
    }
    catch {
        $lblResult.Text = "Benchmark data file found."
    }
}

$captureClock = New-Object System.Windows.Forms.Timer
$captureClock.Interval = 1000
$captureClock.Add_Tick({
    if (-not $State.Collecting -or -not $State.CaptureStartedAt) {
        return
    }
    $elapsed = [math]::Min([int]((Get-Date) - $State.CaptureStartedAt).TotalSeconds, $State.CaptureDurationSec)
    $elapsedText = [TimeSpan]::FromSeconds($elapsed).ToString("mm\:ss")
    $totalText = [TimeSpan]::FromSeconds($State.CaptureDurationSec).ToString("mm\:ss")
    Set-Status "Collecting FPS data in the background: $elapsedText / $totalText" -Color $Theme.Accent
})

$readinessTimer = New-Object System.Windows.Forms.Timer
$readinessTimer.Interval = 2000
$readinessTimer.Add_Tick({
    try {
        Update-Readiness
    }
    catch {
        $btnStart.Enabled = $false
        $lblAvailability.Text = "Check failed"
        Set-Status "Could not check collection readiness." -Color $Theme.Error
    }
})

function Complete-CaptureCollection {
    param(
        [pscustomobject]$Capture,
        [string]$ErrorText = "",
        [string]$ErrorDetails = "",
        [switch]$Discarded
    )

    $State.Collecting = $false
    $State.CaptureCanceled = $false
    $captureClock.Stop()
    $readinessTimer.Start()
    $radioTwoMinutes.Enabled = $true
    $radioFourMinutes.Enabled = $true
    $btnCancel.Enabled = $false

    if ($Discarded) {
        Update-Readiness
        Set-Status "Collection discarded." -Color $Theme.Warning
        return
    }

    if (-not [string]::IsNullOrWhiteSpace($ErrorText)) {
        Offer-CrashReport -Stage "capture" -Message $ErrorText -Details $ErrorDetails
        Update-Readiness
        Set-Status "Collection failed: $ErrorText" -Color $Theme.Error
        return
    }

    $answers = Get-ContextAnswers -RaidContext $capture.context
    if (-not $answers) {
        Set-Status "Collection completed but was not saved."
        Update-Readiness
        return
    }

    try {
        Save-BenchmarkRun -Capture $capture -Context $capture.context -Answers $answers | Out-Null
        Update-BenchmarkDataAvailability
        $finalStatus = "Benchmark data saved."
    }
    catch {
        $errorText = if ($_.Exception -and -not [string]::IsNullOrWhiteSpace($_.Exception.Message)) {
            $_.Exception.Message
        }
        else {
            "Benchmark data could not be saved."
        }
        Offer-CrashReport -Stage "save" -Message $errorText -Details (Get-CrashExceptionDetails -ErrorRecord $_) -Caption "Save failed"
        $finalStatus = "Collection completed but could not be saved."
    }

    Update-Readiness
    Set-Status $finalStatus -Color $(if ($finalStatus -eq "Benchmark data saved.") { $Theme.Ready } else { $Theme.Error })
}

$captureJobTimer = New-Object System.Windows.Forms.Timer
$captureJobTimer.Interval = 500
$captureJobTimer.Add_Tick({
    $job = $State.CaptureJob
    if (-not $job) {
        $captureJobTimer.Stop()
        return
    }
    if ($job.State -notin @("Completed", "Failed", "Stopped")) {
        return
    }

    $captureJobTimer.Stop()
    $State.CaptureJob = $null
    if ($State.CaptureCanceled) {
        Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
        Complete-CaptureCollection -Discarded
        return
    }
    try {
        if ($job.State -ne "Completed") {
            $jobError = @($job.ChildJobs[0].Error | Select-Object -Last 1)[0]
            if ($jobError) {
                $errorText = if ($jobError.Exception -and $jobError.Exception.Message) { $jobError.Exception.Message } else { "The collection helper stopped before it could finish." }
                Complete-CaptureCollection -ErrorText $errorText -ErrorDetails (Get-CrashExceptionDetails -ErrorRecord $jobError)
                return
            }
            $reason = $job.ChildJobs[0].JobStateInfo.Reason
            if ($reason -and -not [string]::IsNullOrWhiteSpace($reason.Message)) {
                throw $reason.Message
            }
            throw "The collection helper stopped before it could finish."
        }

        $result = @(Receive-Job -Job $job -ErrorAction Stop)
        if ($result.Count -ne 1 -or [string]::IsNullOrWhiteSpace([string]$result[0])) {
            throw "The collection helper returned no benchmark data."
        }
        $capture = $result[0] | ConvertFrom-Json -ErrorAction Stop
        Complete-CaptureCollection -Capture $capture
    }
    catch {
        $errorText = if ($_.Exception -and -not [string]::IsNullOrWhiteSpace($_.Exception.Message)) {
            $_.Exception.Message
        }
        else {
            "The collection helper could not start."
        }
        Complete-CaptureCollection -ErrorText $errorText -ErrorDetails (Get-CrashExceptionDetails -ErrorRecord $_)
    }
    finally {
        Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
    }
})

$btnStart.Add_Click({
    if (-not $State.PresentMon -or -not $State.PresentMon.found) {
        Show-PresentMonSetup
        Update-Readiness
        return
    }
    $tarkovProcess = Get-TarkovProcess
    if (-not $tarkovProcess) {
        $lblAvailability.Text = "Next: run Tarkov"
        Set-Status "Run Tarkov, enter a raid, then press Start collection." -Color $Theme.Warning
        Show-RaidRequired -Message "Run Tarkov, enter a raid, then press Start collection."
        return
    }

    try {
        $raidContext = Invoke-BenchmarkScript -Name "read-tarkov-raid-context.ps1" | ConvertFrom-Json
    }
    catch {
        $raidContext = $null
    }
    if (-not (Test-ActiveRaidContext -Context $raidContext -ProcessStartedAt $tarkovProcess.StartTime)) {
        $lblAvailability.Text = "Now start the raid"
        Set-Status "Now start the raid, then press Start collection." -Color $Theme.Warning
        Show-RaidRequired -Message "Now start the raid, then press Start collection."
        return
    }

    $State.CaptureDurationSec = if ($radioFourMinutes.Checked) { 240 } else { 120 }
    $State.CaptureStartedAt = Get-Date
    $State.CaptureCanceled = $false
    $State.Collecting = $true
    $btnStart.Enabled = $false
    $btnCancel.Enabled = $true
    $radioTwoMinutes.Enabled = $false
    $radioFourMinutes.Enabled = $false
    $readinessTimer.Stop()
    $captureClock.Start()
    $lblAvailability.Text = "Collecting"
    Set-Status "Starting PresentMon. Windows asks for permission only when it is required." -Color $Theme.Accent
    try {
        $State.CaptureJob = Start-Job -ArgumentList $ScriptsDir, $State.CaptureDurationSec -ScriptBlock {
            param(
                [string]$JobScriptsDir,
                [int]$DurationSec
            )

            $ErrorActionPreference = "Stop"
            $sessionDir = Join-Path ([System.IO.Path]::GetTempPath()) "TarkovSkills-capture-$([guid]::NewGuid().ToString('N'))"
            New-Item -ItemType Directory -Force $sessionDir | Out-Null
            try {
                $settingsJson = & (Join-Path $JobScriptsDir "read-tarkov-settings.ps1")
                $systemJson = & (Join-Path $JobScriptsDir "collect-system-info.ps1") -IncludePagefile
                $captureJson = & (Join-Path $JobScriptsDir "capture-presentmon.ps1") -DurationSec $DurationSec -OutputDir $sessionDir -RequestElevation
                $capture = $captureJson | ConvertFrom-Json
                $contextJson = & (Join-Path $JobScriptsDir "read-tarkov-raid-context.ps1")
                $context = $contextJson | ConvertFrom-Json
                [ordered]@{
                    settings_json = $settingsJson
                    system_json = $systemJson
                    fps_json = ($capture.parsed | ConvertTo-Json -Depth 20)
                    context = $context
                } | ConvertTo-Json -Depth 30 -Compress
            }
            finally {
                Remove-Item -LiteralPath $sessionDir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        $captureJobTimer.Start()
    }
    catch {
        $errorText = if ($_.Exception -and -not [string]::IsNullOrWhiteSpace($_.Exception.Message)) {
            $_.Exception.Message
        }
        else {
            "The collection helper could not start."
        }
        Complete-CaptureCollection -ErrorText $errorText
    }
})

$btnCancel.Add_Click({
    if (-not $State.Collecting) {
        return
    }

    $State.CaptureCanceled = $true
    $btnCancel.Enabled = $false
    $lblAvailability.Text = "Discarding"
    Set-Status "Stopping collection. This run will be discarded." -Color $Theme.Warning
    if ($State.CaptureJob) {
        Stop-Job -Job $State.CaptureJob -ErrorAction SilentlyContinue
    }
})

$linkPresentMon.Add_LinkClicked({ Show-PresentMonSetup })

$btnOpenFolder.Add_Click({
    $directory = Split-Path -Parent $State.BenchmarkPath
    New-Item -ItemType Directory -Force $directory | Out-Null
    Start-Process explorer.exe -ArgumentList $directory
})

$btnUpload.Add_Click({
    try {
        $upload = Invoke-BenchmarkScript -Name "get-benchmark-upload.ps1" -Parameters @{ BenchmarkPath = $State.BenchmarkPath } | ConvertFrom-Json
        if ($upload.new_run_count -eq 0) {
            $choice = Show-TarkovMessage -Owner $form -Text "All $($upload.total_run_count) saved runs were already submitted. Copy all of them again?" -Caption "Nothing new to upload" -Buttons ([System.Windows.Forms.MessageBoxButtons]::YesNo) -Icon ([System.Windows.Forms.MessageBoxIcon]::Question)
            if ($choice -ne [System.Windows.Forms.DialogResult]::Yes) {
                return
            }
            $upload = Invoke-BenchmarkScript -Name "get-benchmark-upload.ps1" -Parameters @{ BenchmarkPath = $State.BenchmarkPath; IncludeAll = $true } | ConvertFrom-Json
        }

        [System.Windows.Forms.Clipboard]::SetText($upload.payload_json)
        Invoke-BenchmarkScript -Name "get-benchmark-upload.ps1" -Parameters @{ BenchmarkPath = $State.BenchmarkPath; MarkUploaded = $true } | Out-Null
        Set-Status "Copied $($upload.new_run_count) run(s) to the clipboard. Paste them into the form." -Color $Theme.Ready
        Start-Process $PerformanceFormUrl
    }
    catch {
        $errorText = if ($_.Exception -and -not [string]::IsNullOrWhiteSpace($_.Exception.Message)) {
            $_.Exception.Message
        }
        else {
            "Upload preparation failed."
        }
        Offer-CrashReport -Stage "upload" -Message $errorText -Details (Get-CrashExceptionDetails -ErrorRecord $_) -Caption "Upload failed"
    }
})

$form.Add_Shown({
    Update-BenchmarkDataAvailability
    Update-Readiness
    $readinessTimer.Start()
})
$form.Add_FormClosed({
    $captureClock.Stop()
    $readinessTimer.Stop()
    if ($State.CaptureJob) {
        Stop-Job -Job $State.CaptureJob -ErrorAction SilentlyContinue
        Remove-Job -Job $State.CaptureJob -Force -ErrorAction SilentlyContinue
    }
})

[void]$form.ShowDialog()
