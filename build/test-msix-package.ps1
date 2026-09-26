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
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'StoreRelease.psm1') -Force
$release = Assert-StoreRelease -Product $Product -RepositoryRoot $repoRoot
$resolvedPackage = [IO.Path]::GetFullPath($PackagePath)
$productionAuth = Join-Path $repoRoot 'config/desktop-auth.production.json'
& (Join-Path $PSScriptRoot 'test-desktop-auth-config.ps1') -ConfigurationPath $productionAuth
$productionApi = Join-Path $repoRoot 'config/academy-api.production.json'
& (Join-Path $PSScriptRoot 'test-academy-api-config.ps1') -ConfigurationPath $productionApi -Environment Production

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
    foreach ($path in @('AppxManifest.xml', 'desktop-auth.json', 'academy-api.json') + $requirements + $assets) {
        if (-not $entries.ContainsKey($path)) { throw "MSIX is missing required content: $path" }
    }

    if (@($archive.Entries | Where-Object { $_.FullName -eq 'desktop-auth.json' }).Count -ne 1) {
        throw 'MSIX must contain exactly one desktop-auth.json.'
    }
    if (@($archive.Entries | Where-Object { $_.FullName -match '(development.*\.json|\.local\.json|\.credential(?:\.lock)?)$' }).Count -ne 0) {
        throw 'MSIX must not contain development configuration or saved credentials.'
    }
    $authHash = [Security.Cryptography.SHA256]::Create()
    try {
        $authStream = $entries['desktop-auth.json'].Open()
        try { $packagedAuthHash = ([BitConverter]::ToString($authHash.ComputeHash($authStream))).Replace('-', '') }
        finally { $authStream.Dispose() }
    }
    finally { $authHash.Dispose() }
    if ($packagedAuthHash -ne (Get-FileHash -LiteralPath $productionAuth -Algorithm SHA256).Hash) {
        throw 'Packaged desktop-auth.json differs from the versioned production configuration.'
    }

    if (@($archive.Entries | Where-Object { $_.FullName -eq 'academy-api.json' }).Count -ne 1) {
        throw 'MSIX must contain exactly one academy-api.json.'
    }
    $apiHash = [Security.Cryptography.SHA256]::Create()
    try {
        $apiStream = $entries['academy-api.json'].Open()
        try { $packagedApiHash = ([BitConverter]::ToString($apiHash.ComputeHash($apiStream))).Replace('-', '') }
        finally { $apiStream.Dispose() }
    } finally { $apiHash.Dispose() }
    if ($packagedApiHash -ne (Get-FileHash -LiteralPath $productionApi -Algorithm SHA256).Hash) {
        throw 'Packaged academy-api.json differs from the versioned production configuration.'
    }

    $reader = [IO.StreamReader]::new($entries['AppxManifest.xml'].Open())
    try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $identity = $manifest.Package.Identity
    if ($identity.Name -ne $release.PackageIdentity) { throw "MSIX identity '$($identity.Name)' does not match '$($release.PackageIdentity)'." }
    if ($identity.Version -ne $release.PackageVersion) { throw "MSIX version '$($identity.Version)' does not match '$($release.PackageVersion)'." }
    if ($identity.ProcessorArchitecture -ne 'x64') { throw "MSIX architecture '$($identity.ProcessorArchitecture)' is not x64." }
    $sharedAuth = @($manifest.Package.Extensions.Extension | Where-Object { $_.Category -eq 'windows.publisherCacheFolders' })
    if ($sharedAuth.Count -ne 1 -or @($sharedAuth[0].PublisherCacheFolders.Folder).Count -ne 1 -or
        $sharedAuth[0].PublisherCacheFolders.Folder.Name -ne 'TarkovDesktopAuth') {
        throw 'MSIX must declare the common TarkovDesktopAuth publisher cache folder.'
    }

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
