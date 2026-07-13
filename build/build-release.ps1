param(
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDir) {
    $OutputDir = Join-Path $repoRoot "dist"
}
New-Item -ItemType Directory -Force $OutputDir | Out-Null

$staging = Join-Path ([System.IO.Path]::GetTempPath()) "TarkovBenchmarkApp"
if (Test-Path -LiteralPath $staging) {
    Remove-Item -LiteralPath $staging -Recurse -Force
}
New-Item -ItemType Directory -Force (Join-Path $staging "app") | Out-Null
New-Item -ItemType Directory -Force (Join-Path $staging "scripts") | Out-Null

Copy-Item (Join-Path $repoRoot "app\TarkovBenchmarkWizard.ps1") (Join-Path $staging "app")
Copy-Item (Join-Path $repoRoot "LICENSE") $staging

$sharedScripts = @(
    "TarkovCommon.ps1",
    "read-tarkov-settings.ps1",
    "collect-system-info.ps1",
    "read-tarkov-raid-context.ps1",
    "check-presentmon.ps1",
    "capture-presentmon.ps1",
    "parse-fps-csv.ps1",
    "add-benchmark-run.ps1",
    "get-benchmark-upload.ps1"
)
foreach ($script in $sharedScripts) {
    Copy-Item (Join-Path $repoRoot "scripts\$script") (Join-Path $staging "scripts")
}

@(
    '@echo off',
    'setlocal',
    'set "ROOT=%~dp0"',
    'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%app\TarkovBenchmarkWizard.ps1"',
    'endlocal'
) | Set-Content -LiteralPath (Join-Path $staging "Start-TarkovBenchmark.cmd") -Encoding ASCII

@(
    'Tarkov Performance Benchmark App',
    '',
    'Run Start-TarkovBenchmark.cmd after Tarkov is running and you are in a raid.',
    'The app is read-only toward Escape from Tarkov: it never edits game files.',
    'Install PresentMon separately under %LOCALAPPDATA%\TarkovSkills\tools\PresentMon\PresentMon.exe.',
    'Results are saved as benchmark.json under %LOCALAPPDATA%\TarkovSkills\.',
    'User names are stripped from all paths inside benchmark.json before saving.'
) | Set-Content -LiteralPath (Join-Path $staging "README.txt") -Encoding UTF8

$zipPath = Join-Path $OutputDir "TarkovBenchmarkApp.zip"
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zipPath

Remove-Item -LiteralPath $staging -Recurse -Force
"Release archive created: $zipPath"
