[CmdletBinding()]
param(
    [ValidateSet('benchmark', 'toolkit', 'all')]
    [string] $Product = 'all',
    [ValidatePattern('^[1-9][0-9]{0,4}\.[0-9]{1,5}\.[0-9]{1,5}\.0$')]
    [string] $BenchmarkVersion,
    [ValidatePattern('^[1-9][0-9]{0,4}\.[0-9]{1,5}\.[0-9]{1,5}\.0$')]
    [string] $ToolkitVersion,
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '..\dist')
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory)
Import-Module (Join-Path $PSScriptRoot 'StoreRelease.psm1') -Force
$benchmarkRelease = Assert-StoreRelease -Product benchmark -RepositoryRoot $repoRoot -PackageVersion $BenchmarkVersion
$toolkitRelease = Assert-StoreRelease -Product toolkit -RepositoryRoot $repoRoot -PackageVersion $ToolkitVersion

$products = @(
    [pscustomobject]@{
        Name = 'TarkovPerformanceBenchmark'
        PackageVersion = $benchmarkRelease.PackageVersion
        Project = Join-Path $repoRoot 'apps\tarkov-performance-benchmark\src\TarkovPerformanceBenchmark\TarkovPerformanceBenchmark.csproj'
        Archive = "TarkovPerformanceBenchmark-$($benchmarkRelease.PackageVersion)-win-x64.zip"
    },
    [pscustomobject]@{
        Name = 'TarkovPerformanceToolkit'
        PackageVersion = $toolkitRelease.PackageVersion
        Project = Join-Path $repoRoot 'apps\tarkov-performance-toolkit\src\TarkovPerformanceToolkit\TarkovPerformanceToolkit.csproj'
        AdditionalProject = Join-Path $repoRoot 'apps\tarkov-performance-toolkit\src\TarkovSkills.Cli\TarkovSkills.Cli.csproj'
        Archive = "TarkovPerformanceToolkit-$($toolkitRelease.PackageVersion)-win-x64.zip"
    }
)

if ($Product -ne 'all') {
    $products = @($products | Where-Object { $_.Name -eq "TarkovPerformance$($Product.Substring(0, 1).ToUpperInvariant() + $Product.Substring(1))" })
}

$staging = Join-Path ([IO.Path]::GetTempPath()) ('TarkovSkillsPortable-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $resolvedOutput, $staging -Force | Out-Null

try {
    foreach ($portableProduct in $products) {
        $layout = Join-Path $staging $portableProduct.Name
        New-Item -ItemType Directory -Path $layout | Out-Null

        $assemblyVersion = $portableProduct.PackageVersion -replace '\.0$', ''
        dotnet publish $portableProduct.Project -c $Configuration -r win-x64 --self-contained true -p:Version=$assemblyVersion -o $layout
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to publish $($portableProduct.Name)."
        }

        if ($portableProduct.AdditionalProject) {
            dotnet publish $portableProduct.AdditionalProject -c $Configuration -r win-x64 --self-contained true -p:Version=$assemblyVersion -o $layout
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to publish the $($portableProduct.Name) command-line tool."
            }
        }

        Get-ChildItem -LiteralPath $layout -Filter '*.pdb' -File | Remove-Item -Force
        $archivePath = Join-Path $resolvedOutput $portableProduct.Archive
        if (Test-Path -LiteralPath $archivePath) {
            Remove-Item -LiteralPath $archivePath -Force
        }
        Compress-Archive -LiteralPath $layout -DestinationPath $archivePath -CompressionLevel Optimal
        Write-Output $archivePath
    }
}
finally {
    $resolvedStaging = [IO.Path]::GetFullPath($staging)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ((Test-Path -LiteralPath $resolvedStaging) -and $resolvedStaging.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolvedStaging -Recurse -Force
    }
}
