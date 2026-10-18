[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet('benchmark', 'toolkit')] [string] $Product,
    [string] $OutputDirectory = 'artifacts/release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'StoreRelease.psm1') -Force
$release = Assert-StoreRelease -Product $Product -RepositoryRoot $repoRoot
& (Join-Path $PSScriptRoot 'check-presentmon-dependency.ps1') -SkipUpstreamCheck
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $output) -and @(Get-ChildItem -LiteralPath $output -Force).Count) {
    throw 'Release output must be empty. Never reuse files from another build or attempt.'
}
$staging = Join-Path ([IO.Path]::GetTempPath()) ('TarkovRelease-' + [Guid]::NewGuid().ToString('N'))
$published = Join-Path $staging 'published'
New-Item -ItemType Directory -Path $published, $output -Force | Out-Null
try {
    $projects = if ($Product -eq 'benchmark') {
        @('apps/tarkov-performance-benchmark/src/TarkovPerformanceBenchmark/TarkovPerformanceBenchmark.csproj')
    } else {
        @('apps/tarkov-performance-toolkit/src/TarkovPerformanceToolkit/TarkovPerformanceToolkit.csproj',
          'apps/tarkov-performance-toolkit/src/TarkovSkills.Cli/TarkovSkills.Cli.csproj')
    }
    # Each executable is published once. Both package formats copy this same output.
    foreach ($project in $projects) {
        dotnet publish (Join-Path $repoRoot $project) -c Release -r win-x64 --self-contained true `
            -p:DesktopAuthEnvironment=Production -p:AcademyApiEnvironment=Production -o $published
        if ($LASTEXITCODE -ne 0) { throw "Publishing $Product failed." }
    }
    & (Join-Path $PSScriptRoot "build-$Product-msix.ps1") -PublishedDirectory $published -OutputDirectory $output
    & (Join-Path $PSScriptRoot 'build-portable.ps1') -Product $Product -PublishedDirectory $published -OutputDirectory $output
    & (Join-Path $PSScriptRoot 'test-store-artifact-formats.ps1') -Product $Product -Directory $output
    node (Join-Path $PSScriptRoot 'release-artifacts.mjs') manifest $Product $output
    if ($LASTEXITCODE -ne 0) { throw 'Release provenance generation failed.' }
}
finally {
    $resolved = [IO.Path]::GetFullPath($staging)
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ((Test-Path -LiteralPath $resolved) -and $resolved.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
