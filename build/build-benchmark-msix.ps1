[CmdletBinding()]
param(
    [ValidatePattern('^[1-9][0-9]{0,4}\.[0-9]{1,5}\.[0-9]{1,5}\.0$')]
    [string] $PackageVersion = '1.0.0.0',
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\msix')
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$appRoot = Join-Path $repoRoot 'apps\tarkov-performance-benchmark'
$project = Join-Path $appRoot 'src\TarkovPerformanceBenchmark\TarkovPerformanceBenchmark.csproj'
$packagingRoot = Join-Path $appRoot 'packaging'
$toolsProject = Join-Path $packagingRoot 'PackagingTools.csproj'
$manifestTemplate = Join-Path $packagingRoot 'AppxManifest.template.xml'
$assets = Join-Path $packagingRoot 'Assets'
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
$staging = Join-Path ([System.IO.Path]::GetTempPath()) ("TarkovBenchmarkMsix-" + [Guid]::NewGuid().ToString('N'))
$publish = Join-Path $staging 'layout'
$packageName = "TarkovPerformanceBenchmark_$PackageVersion`_x64.msix"
$packagePath = Join-Path $resolvedOutput $packageName

function Invoke-Checked {
    param([Parameter(Mandatory)] [string] $FilePath, [Parameter(Mandatory)] [string[]] $ArgumentList)
    $output = & $FilePath @ArgumentList 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        $output | Out-Host
        throw "$FilePath exited with code $exitCode."
    }
    if ([System.IO.Path]::GetFileName($FilePath) -ne 'makeappx.exe') { $output | Out-Host }
}

function Find-MakeAppx {
    $installed = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" -File -ErrorAction SilentlyContinue |
        Sort-Object { [Version]$_.Directory.Parent.Name } -Descending |
        Select-Object -First 1
    if ($installed) { return $installed.FullName }

    Invoke-Checked -FilePath 'dotnet' -ArgumentList @('restore', $toolsProject)
    $packageRoot = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.buildtools\10.0.26100.7705'
    $restored = Get-ChildItem -Path (Join-Path $packageRoot 'bin\*\x64\makeappx.exe') -File -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if (-not $restored) { throw 'MakeAppx.exe was not found after restoring Microsoft.Windows.SDK.BuildTools.' }
    return $restored.FullName
}

New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null
New-Item -ItemType Directory -Path $publish -Force | Out-Null

try {
    $makeAppx = Find-MakeAppx
    Invoke-Checked -FilePath 'dotnet' -ArgumentList @(
        'publish', $project,
        '-c', $Configuration,
        '-r', 'win-x64',
        '--self-contained', 'true',
        ('-p:Version=' + ($PackageVersion -replace '\.0$', '')),
        '-o', $publish
    )

    Get-ChildItem -LiteralPath $publish -File -Filter '*.pdb' | Remove-Item -Force

    Copy-Item -LiteralPath $assets -Destination (Join-Path $publish 'Assets') -Recurse
    $manifest = (Get-Content -LiteralPath $manifestTemplate -Raw).Replace('__PACKAGE_VERSION__', $PackageVersion)
    Set-Content -LiteralPath (Join-Path $publish 'AppxManifest.xml') -Value $manifest -Encoding utf8

    if (Test-Path -LiteralPath $packagePath) { Remove-Item -LiteralPath $packagePath -Force }
    Invoke-Checked -FilePath $makeAppx -ArgumentList @('pack', '/d', $publish, '/p', $packagePath, '/o')
    Write-Output $packagePath
}
finally {
    $resolvedStaging = [System.IO.Path]::GetFullPath($staging)
    $resolvedTemp = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if ((Test-Path -LiteralPath $resolvedStaging) -and $resolvedStaging.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolvedStaging -Recurse -Force
    }
}
