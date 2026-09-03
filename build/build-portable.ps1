[CmdletBinding()]
param(
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+$')]
    [string] $Version,
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '..\dist')
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory)

if (-not $Version) {
    $manifest = Get-Content -Raw -LiteralPath (Join-Path $repoRoot '.claude-plugin\plugin.json') | ConvertFrom-Json
    $Version = $manifest.version
}

$products = @(
    [pscustomobject]@{
        Name = 'TarkovPerformanceBenchmark'
        Project = Join-Path $repoRoot 'apps\tarkov-performance-benchmark\src\TarkovPerformanceBenchmark\TarkovPerformanceBenchmark.csproj'
        Archive = "TarkovPerformanceBenchmark-$Version-win-x64.zip"
    },
    [pscustomobject]@{
        Name = 'TarkovPerformanceToolkit'
        Project = Join-Path $repoRoot 'apps\tarkov-performance-toolkit\src\TarkovPerformanceToolkit\TarkovPerformanceToolkit.csproj'
        AdditionalProject = Join-Path $repoRoot 'apps\tarkov-performance-toolkit\src\TarkovSkills.Cli\TarkovSkills.Cli.csproj'
        Archive = "TarkovPerformanceToolkit-$Version-win-x64.zip"
    }
)

$staging = Join-Path ([IO.Path]::GetTempPath()) ('TarkovSkillsPortable-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $resolvedOutput, $staging -Force | Out-Null

try {
    foreach ($product in $products) {
        $layout = Join-Path $staging $product.Name
        New-Item -ItemType Directory -Path $layout | Out-Null

        dotnet publish $product.Project -c $Configuration -r win-x64 --self-contained true -p:Version=$Version -o $layout
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to publish $($product.Name)."
        }

        if ($product.AdditionalProject) {
            dotnet publish $product.AdditionalProject -c $Configuration -r win-x64 --self-contained true -p:Version=$Version -o $layout
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to publish the $($product.Name) command-line tool."
            }
        }

        Get-ChildItem -LiteralPath $layout -Filter '*.pdb' -File | Remove-Item -Force
        $archivePath = Join-Path $resolvedOutput $product.Archive
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
