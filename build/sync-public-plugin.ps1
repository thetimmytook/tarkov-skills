param(
    [switch]$Check
)

$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$pluginRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "plugins\tarkov-performance"))
$allowedRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "plugins")) + [System.IO.Path]::DirectorySeparatorChar

if (-not $pluginRoot.StartsWith($allowedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Public plugin path must stay inside $allowedRoot"
}

$mappings = [System.Collections.Generic.List[object]]::new()

function Add-PublicPluginFile {
    param(
        [Parameter(Mandatory)] [string] $Source,
        [Parameter(Mandatory)] [string] $Destination
    )

    $mappings.Add([pscustomobject]@{
        Source = Join-Path $repoRoot $Source
        Destination = Join-Path $pluginRoot $Destination
        RelativeDestination = $Destination.Replace('\', '/')
    })
}

Add-PublicPluginFile ".claude-plugin\plugin.json" ".claude-plugin\plugin.json"
Add-PublicPluginFile "LICENSE" "LICENSE"
Add-PublicPluginFile "PRIVACY.md" "PRIVACY.md"
Add-PublicPluginFile "TERMS.md" "TERMS.md"

$skillsRoot = Join-Path $repoRoot "skills"
foreach ($skillDirectory in @(Get-ChildItem -LiteralPath $skillsRoot -Directory | Sort-Object Name)) {
    Add-PublicPluginFile `
        ("skills\{0}\SKILL.md" -f $skillDirectory.Name) `
        ("skills\{0}\SKILL.md" -f $skillDirectory.Name)

    $referencesRoot = Join-Path $skillDirectory.FullName "references"
    if (Test-Path -LiteralPath $referencesRoot) {
        foreach ($reference in @(Get-ChildItem -LiteralPath $referencesRoot -Recurse -File | Sort-Object FullName)) {
            $relativeReference = $reference.FullName.Substring($referencesRoot.Length + 1)
            Add-PublicPluginFile `
                ("skills\{0}\references\{1}" -f $skillDirectory.Name, $relativeReference) `
                ("skills\{0}\references\{1}" -f $skillDirectory.Name, $relativeReference)
        }
    }
}

$drift = [System.Collections.Generic.List[string]]::new()
foreach ($mapping in $mappings) {
    if (-not (Test-Path -LiteralPath $mapping.Source -PathType Leaf)) {
        throw "Public plugin source file not found: $($mapping.Source)"
    }

    $matches = (Test-Path -LiteralPath $mapping.Destination -PathType Leaf) -and
        ((Get-FileHash -Algorithm SHA256 -LiteralPath $mapping.Source).Hash -eq
         (Get-FileHash -Algorithm SHA256 -LiteralPath $mapping.Destination).Hash)

    if ($matches) {
        continue
    }

    if ($Check) {
        $drift.Add($mapping.RelativeDestination)
        continue
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $mapping.Destination) | Out-Null
    Copy-Item -LiteralPath $mapping.Source -Destination $mapping.Destination -Force
    "synced: plugins/tarkov-performance/$($mapping.RelativeDestination)"
}

$allowedFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
[void]$allowedFiles.Add("README.md")
foreach ($mapping in $mappings) {
    [void]$allowedFiles.Add($mapping.RelativeDestination)
}

if (Test-Path -LiteralPath $pluginRoot) {
    foreach ($file in @(Get-ChildItem -LiteralPath $pluginRoot -Recurse -File)) {
        $relative = $file.FullName.Substring($pluginRoot.Length + 1).Replace('\', '/')
        if ($allowedFiles.Contains($relative)) {
            continue
        }

        if ($Check) {
            $drift.Add("unexpected file: $relative")
        }
        else {
            Remove-Item -LiteralPath $file.FullName -Force
            "removed: plugins/tarkov-performance/$relative"
        }
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $pluginRoot "README.md") -PathType Leaf)) {
    $drift.Add("README.md")
}

if ($Check) {
    if ($drift.Count -gt 0) {
        "PUBLIC PLUGIN OUT OF SYNC:"
        $drift | Sort-Object -Unique | ForEach-Object { "  $_" }
        exit 1
    }
    "Public Claude plugin matches its source files."
}
elseif ($drift.Count -gt 0) {
    throw "The public plugin README is missing: plugins/tarkov-performance/README.md"
}

