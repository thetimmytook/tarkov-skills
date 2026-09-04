Set-StrictMode -Version Latest

function Get-StoreRelease {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('benchmark', 'toolkit')]
        [string] $Product,
        [Parameter(Mandatory)]
        [string] $RepositoryRoot
    )

    $definitions = @{
        benchmark = @{
            RelativePath = 'apps/tarkov-performance-benchmark/packaging/store-release.json'
            Identity = 'TimmyTook.TarkovPerformanceBenchmark'
            StoreProductId = '9PJMPQ06JL21'
        }
        toolkit = @{
            RelativePath = 'apps/tarkov-performance-toolkit/packaging/store-release.json'
            Identity = 'TimmyTook.TarkovPerformanceToolkit'
            StoreProductId = '9N3L7DZH0K64'
        }
    }

    $definition = $definitions[$Product]
    $path = Join-Path $RepositoryRoot $definition.RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Store release file is missing: $path"
    }

    try {
        $release = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    }
    catch {
        throw "Store release file is not valid JSON: $path. $($_.Exception.Message)"
    }

    foreach ($property in 'packageVersion', 'tag', 'storeProductId', 'packageIdentity') {
        if ([string]::IsNullOrWhiteSpace([string]$release.$property)) {
            throw "Store release file $path is missing '$property'."
        }
    }

    if ($release.packageIdentity -ne $definition.Identity) {
        throw "Store release identity '$($release.packageIdentity)' does not match expected '$($definition.Identity)'."
    }
    if ($release.storeProductId -ne $definition.StoreProductId) {
        throw "Store release Store product ID '$($release.storeProductId)' does not match expected '$($definition.StoreProductId)'."
    }
    if ($release.packageVersion -notmatch '^[1-9][0-9]{0,4}\.[0-9]{1,5}\.[0-9]{1,5}\.0$') {
        throw "Store package version '$($release.packageVersion)' must have a nonzero first component and a zero fourth component."
    }
    if ($release.tag -notmatch "^$Product-v([0-9]+\.[0-9]+\.[0-9]+)$") {
        throw "Store release tag '$($release.tag)' must use the form '$Product-vX.Y.Z'."
    }

    $tagVersion = $Matches[1]
    if ($release.packageVersion -ne "$tagVersion.0") {
        throw "Store package version '$($release.packageVersion)' must match tag '$($release.tag)' as '$tagVersion.0'."
    }

    [pscustomobject]@{
        Product = $Product
        PackageVersion = [string]$release.packageVersion
        Tag = [string]$release.tag
        StoreProductId = [string]$release.storeProductId
        PackageIdentity = [string]$release.packageIdentity
        Path = $path
    }
}

function Assert-StoreRelease {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [ValidateSet('benchmark', 'toolkit')] [string] $Product,
        [Parameter(Mandatory)] [string] $RepositoryRoot,
        [string] $ExpectedTag,
        [string] $PackageVersion
    )

    $release = Get-StoreRelease -Product $Product -RepositoryRoot $RepositoryRoot
    if ($ExpectedTag -and $ExpectedTag -ne $release.Tag) {
        throw "Tag '$ExpectedTag' does not match the approved $Product release tag '$($release.Tag)' in $($release.Path)."
    }
    if ($PackageVersion -and $PackageVersion -ne $release.PackageVersion) {
        throw "PackageVersion '$PackageVersion' does not match the approved $Product version '$($release.PackageVersion)' in $($release.Path)."
    }
    return $release
}

Export-ModuleMember -Function Get-StoreRelease, Assert-StoreRelease
