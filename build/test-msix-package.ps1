[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('benchmark', 'toolkit')]
    [string] $Product,
    [Parameter(Mandatory)]
    [string] $PackagePath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'StoreRelease.psm1') -Force
$release = Assert-StoreRelease -Product $Product -RepositoryRoot $repoRoot
$resolvedPackage = [IO.Path]::GetFullPath($PackagePath)

if (-not (Test-Path -LiteralPath $resolvedPackage -PathType Leaf)) {
    throw "MSIX package was not found: $resolvedPackage"
}

$requirements = @{
    benchmark = @('TarkovPerformanceBenchmark.exe', 'TarkovSkills.Core.dll', 'TarkovBenchmark.Feature.dll', 'tools/PresentMon/PresentMon.exe')
    toolkit = @('TarkovPerformanceToolkit.exe', 'TarkovSkills.exe', 'TarkovSkills.Core.dll', 'TarkovBenchmark.Feature.dll', 'tools/PresentMon/PresentMon.exe')
}[$Product]
$assets = @('Assets/AppIcon.ico', 'Assets/AppIcon.png', 'Assets/Square44x44Logo.png', 'Assets/Square150x150Logo.png', 'Assets/Wide310x150Logo.png', 'Assets/StoreLogo.png')

$archive = [IO.Compression.ZipFile]::OpenRead($resolvedPackage)
try {
    $entries = @{}
    foreach ($entry in $archive.Entries) { $entries[$entry.FullName] = $entry }
    if ($entries.ContainsKey('AppxSignature.p7x')) {
        throw 'MSIX must be unsigned before Microsoft Store submission, but AppxSignature.p7x is present.'
    }
    foreach ($path in @('AppxManifest.xml') + $requirements + $assets) {
        if (-not $entries.ContainsKey($path)) { throw "MSIX is missing required content: $path" }
    }

    $reader = [IO.StreamReader]::new($entries['AppxManifest.xml'].Open())
    try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $identity = $manifest.Package.Identity
    if ($identity.Name -ne $release.PackageIdentity) { throw "MSIX identity '$($identity.Name)' does not match '$($release.PackageIdentity)'." }
    if ($identity.Version -ne $release.PackageVersion) { throw "MSIX version '$($identity.Version)' does not match '$($release.PackageVersion)'." }
    if ($identity.ProcessorArchitecture -ne 'x64') { throw "MSIX architecture '$($identity.ProcessorArchitecture)' is not x64." }

    $dependencyPath = 'apps/tarkov-performance-benchmark/src/TarkovPerformanceBenchmark/third_party/presentmon/dependency.json'
    $dependency = Get-Content -LiteralPath (Join-Path $repoRoot $dependencyPath) -Raw | ConvertFrom-Json
    $hash = [Security.Cryptography.SHA256]::Create()
    try {
        $stream = $entries['tools/PresentMon/PresentMon.exe'].Open()
        try { $actualHash = ([BitConverter]::ToString($hash.ComputeHash($stream))).Replace('-', '') } finally { $stream.Dispose() }
    }
    finally { $hash.Dispose() }
    if ($actualHash -ne $dependency.sha256) { throw "Bundled PresentMon SHA-256 mismatch. Expected $($dependency.sha256), got $actualHash." }
}
finally {
    $archive.Dispose()
}

Write-Output "Verified unsigned $Product MSIX: $resolvedPackage"
