param(
    [switch]$SkipUpstreamCheck
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$dependencyDir = Join-Path $repoRoot "apps\tarkov-performance-benchmark\src\TarkovPerformanceBenchmark\third_party\presentmon"
$manifestPath = Join-Path $dependencyDir "dependency.json"
$binaryPath = Join-Path $dependencyDir "PresentMon.exe"

if (-not (Test-Path -LiteralPath $manifestPath) -or -not (Test-Path -LiteralPath $binaryPath)) {
    throw "Pinned PresentMon manifest or binary is missing."
}

$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
$actualHash = (Get-FileHash -LiteralPath $binaryPath -Algorithm SHA256).Hash
if ($actualHash -ne $manifest.sha256) {
    throw "PresentMon SHA-256 mismatch. Expected $($manifest.sha256), got $actualHash."
}

Write-Host "PresentMon $($manifest.version) SHA-256 verified."

if ($SkipUpstreamCheck) {
    exit 0
}

$gitDir = (& git -C $repoRoot rev-parse --git-dir 2>$null)
if (-not $gitDir) {
    exit 0
}
if (-not [IO.Path]::IsPathRooted($gitDir)) {
    $gitDir = Join-Path $repoRoot $gitDir
}
$cachePath = Join-Path $gitDir "presentmon-version-check.json"
$now = Get-Date
$latestVersion = $null

if (Test-Path -LiteralPath $cachePath) {
    try {
        $cache = Get-Content -Raw -LiteralPath $cachePath | ConvertFrom-Json
        $checkedAt = [datetime]$cache.checked_at
        if (($now - $checkedAt).TotalHours -lt 24) {
            $latestVersion = [string]$cache.latest_version
        }
    }
    catch {
        $latestVersion = $null
    }
}

if (-not $latestVersion) {
    try {
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/GameTechDev/PresentMon/releases/latest" -Headers @{ "User-Agent" = "tarkov-skills-dependency-check" } -TimeoutSec 3
        $latestVersion = ([string]$release.tag_name).TrimStart("v")
        [ordered]@{ checked_at = $now.ToString("o"); latest_version = $latestVersion } |
            ConvertTo-Json | Set-Content -LiteralPath $cachePath -Encoding UTF8
    }
    catch {
        exit 0
    }
}

if ([version]$latestVersion -gt [version]$manifest.version) {
    Write-Warning "PresentMon $latestVersion is available; pinned version is $($manifest.version). Update manually and run capture tests before release."
}
