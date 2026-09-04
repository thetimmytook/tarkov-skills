[CmdletBinding()]
param(
    [ValidateSet('benchmark', 'toolkit', 'all')]
    [string] $Product = 'all',
    [string] $ExpectedTag
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'StoreRelease.psm1') -Force

$products = if ($Product -eq 'all') { @('benchmark', 'toolkit') } else { @($Product) }
foreach ($item in $products) {
    $release = Assert-StoreRelease -Product $item -RepositoryRoot $repoRoot -ExpectedTag $ExpectedTag
    Write-Output "$($release.Product): $($release.Tag) -> $($release.PackageVersion)"
}
