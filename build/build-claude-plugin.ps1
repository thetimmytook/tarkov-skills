param(
    [string] $OutputDirectory
)

$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$pluginRoot = Join-Path $repoRoot "plugins\tarkov-performance"
$manifestPath = Join-Path $pluginRoot ".claude-plugin\plugin.json"
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json

if ([string]::IsNullOrWhiteSpace($manifest.name) -or
    [string]::IsNullOrWhiteSpace($manifest.version)) {
    throw "Claude plugin manifest must define name and version."
}

$null = & (Join-Path $PSScriptRoot "sync-public-plugin.ps1") -Check

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\skills-plugin"
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$archivePath = Join-Path $OutputDirectory ("{0}-claude-plugin-{1}.zip" -f $manifest.name, $manifest.version)
if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $pluginRoot,
    $archivePath,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false
)

$archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $archiveEntries = @($archive.Entries | Where-Object {
        -not [string]::IsNullOrEmpty($_.Name)
    } | ForEach-Object { $_.FullName } | Sort-Object)
    $sourceEntries = @(Get-ChildItem -LiteralPath $pluginRoot -Recurse -File -Force | ForEach-Object {
        $_.FullName.Substring($pluginRoot.Length + 1).Replace('\', '/')
    } | Sort-Object)
    if (($archiveEntries -join "`n") -cne ($sourceEntries -join "`n")) {
        $missing = @($sourceEntries | Where-Object { $_ -cnotin $archiveEntries })
        $unexpected = @($archiveEntries | Where-Object { $_ -cnotin $sourceEntries })
        throw "Claude archive inventory does not match the public plugin folder. Missing: $($missing -join ', '). Unexpected: $($unexpected -join ', ')."
    }
}
finally {
    $archive.Dispose()
}

Write-Output $archivePath
