[CmdletBinding()]
param(
    [ValidatePattern('^[1-9][0-9]{0,4}\.[0-9]{1,5}\.[0-9]{1,5}\.0$')] [string] $PackageVersion,
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Release',
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\toolkit-msix')
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'StoreRelease.psm1') -Force
$storeRelease = Assert-StoreRelease -Product toolkit -RepositoryRoot $repoRoot -PackageVersion $PackageVersion
$PackageVersion = $storeRelease.PackageVersion
$appRoot = Join-Path $repoRoot 'apps\tarkov-performance-toolkit'
$guiProject = Join-Path $appRoot 'src\TarkovPerformanceToolkit\TarkovPerformanceToolkit.csproj'
$cliProject = Join-Path $appRoot 'src\TarkovSkills.Cli\TarkovSkills.Cli.csproj'
$manifestTemplate = Join-Path $appRoot 'packaging\AppxManifest.template.xml'
$assets = Join-Path $appRoot 'packaging\Assets'
$toolsProject = Join-Path $repoRoot 'apps\tarkov-performance-benchmark\packaging\PackagingTools.csproj'
$resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory)
$staging = Join-Path ([IO.Path]::GetTempPath()) ('TarkovToolkitMsix-' + [Guid]::NewGuid().ToString('N'))
$layout = Join-Path $staging 'layout'
$packagePath = Join-Path $resolvedOutput "TarkovPerformanceToolkit_$PackageVersion`_x64.msix"

function Invoke-Checked([string] $FilePath, [string[]] $ArgumentList) {
    $output = & $FilePath @ArgumentList 2>&1
    if ($LASTEXITCODE -ne 0) { $output | Out-Host; throw "$FilePath exited with code $LASTEXITCODE." }
    if ([IO.Path]::GetFileName($FilePath) -ne 'makeappx.exe') { $output | Out-Host }
}

function Find-MakeAppx {
    $installed = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" -File -ErrorAction SilentlyContinue | Sort-Object { [Version]$_.Directory.Parent.Name } -Descending | Select-Object -First 1
    if ($installed) { return $installed.FullName }
    Invoke-Checked 'dotnet' @('restore', $toolsProject)
    $restored = Get-ChildItem "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools\10.0.26100.7705\bin\*\x64\makeappx.exe" -File -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $restored) { throw 'MakeAppx.exe was not found.' }
    return $restored.FullName
}

New-Item -ItemType Directory -Path $resolvedOutput,$layout -Force | Out-Null
try {
    & (Join-Path $PSScriptRoot 'check-presentmon-dependency.ps1') -SkipUpstreamCheck
    $makeAppx = Find-MakeAppx
    foreach ($project in @($guiProject, $cliProject)) {
        Invoke-Checked 'dotnet' @('publish', $project, '-c', $Configuration, '-r', 'win-x64', '--self-contained', 'true', ('-p:Version=' + ($PackageVersion -replace '\.0$', '')), '-o', $layout)
    }
    Get-ChildItem $layout -Filter '*.pdb' -File | Remove-Item -Force
    Copy-Item $assets (Join-Path $layout 'Assets') -Recurse
    (Get-Content $manifestTemplate -Raw).Replace('__PACKAGE_VERSION__', $PackageVersion) | Set-Content (Join-Path $layout 'AppxManifest.xml') -Encoding utf8
    if (Test-Path $packagePath) { Remove-Item $packagePath -Force }
    Invoke-Checked $makeAppx @('pack', '/d', $layout, '/p', $packagePath, '/o')
    & (Join-Path $PSScriptRoot 'test-msix-package.ps1') -Product toolkit -PackagePath $packagePath
    Write-Output $packagePath
}
finally {
    $resolved = [IO.Path]::GetFullPath($staging)
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ((Test-Path $resolved) -and $resolved.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase)) { Remove-Item $resolved -Recurse -Force }
}
