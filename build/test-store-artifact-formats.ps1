[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet('benchmark', 'toolkit')] [string] $Product,
    [Parameter(Mandatory)] [string] $Directory
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'StoreRelease.psm1') -Force
$release = Assert-StoreRelease -Product $Product -RepositoryRoot (Join-Path $PSScriptRoot '..')
$name = if ($Product -eq 'benchmark') { 'TarkovPerformanceBenchmark' } else { 'TarkovPerformanceToolkit' }
$msixPath = Join-Path $Directory "$name`_$($release.PackageVersion)_x64.msix"
$zipPath = Join-Path $Directory "$name-$($release.PackageVersion)-win-x64.zip"
$originalMsixHash = (Get-FileHash -LiteralPath $msixPath -Algorithm SHA256).Hash
$msix = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($msixPath))
$portable = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($zipPath))
function Get-EntryHash($Entry) {
    $stream = $Entry.Open()
    try { return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) }
    finally { $stream.Dispose() }
}
try {
    $seen = @{}
    $files = 0
    foreach ($entry in $portable.Entries) {
        $path = $entry.FullName.Replace('\', '/')
        if ($path.EndsWith('/')) { continue }
        if (-not $path.StartsWith("$name/", [StringComparison]::Ordinal)) { throw "Unexpected portable root: $path" }
        $relative = $path.Substring($name.Length + 1)
        if ($seen.ContainsKey($relative)) { throw "Duplicate portable payload: $relative" }
        $seen[$relative] = $true
        if ($relative -eq 'AppxManifest.xml' -or $relative -match '\.(pdb|ps1|cmd)$') { throw "Unexpected portable content: $relative" }
        $packaged = $msix.GetEntry($relative)
        if (-not $packaged -or (Get-EntryHash $entry) -ne (Get-EntryHash $packaged)) {
            throw "MSIX and portable payload differ: $relative"
        }
        $files++
    }
    foreach ($required in @("$name.exe", 'TarkovSkills.Core.dll', 'desktop-auth.json', 'academy-api.json', 'tools/PresentMon/PresentMon.exe')) {
        if (-not $seen.ContainsKey($required)) { throw "Portable payload is missing $required" }
    }
    if ($Product -eq 'toolkit' -and -not $seen.ContainsKey('TarkovSkills.exe')) { throw 'Toolkit portable CLI is missing.' }
    if ($files -lt 10) { throw 'Self-contained runtime payload is missing.' }
}
finally {
    $msix.Dispose()
    $portable.Dispose()
}
if ((Get-FileHash -LiteralPath $msixPath -Algorithm SHA256).Hash -ne $originalMsixHash) { throw 'Format inspection changed the MSIX.' }
Write-Host "Verified $files identical published files in $Product MSIX and portable ZIP; MSIX bytes unchanged."
