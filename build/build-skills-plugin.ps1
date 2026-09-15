param(
    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot ".claude-plugin\plugin.json"
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json

if ([string]::IsNullOrWhiteSpace($manifest.name) -or
    [string]::IsNullOrWhiteSpace($manifest.description) -or
    [string]::IsNullOrWhiteSpace($manifest.version)) {
    throw "Plugin manifest must define name, description, and version."
}

$skillsRoot = Join-Path $repoRoot "skills"
$allowedExtensions = @(".jpeg", ".jpg", ".json", ".md", ".png", ".svg", ".txt", ".webp", ".yaml", ".yml")
$unsupportedFiles = @(Get-ChildItem -LiteralPath $skillsRoot -Recurse -File | Where-Object {
    $allowedExtensions -notcontains $_.Extension.ToLowerInvariant()
})
if ($unsupportedFiles.Count -gt 0) {
    $relativePaths = $unsupportedFiles | ForEach-Object { $_.FullName.Substring($repoRoot.Length + 1) }
    throw "Skill directories contain files outside the allowed extension list:`n$($relativePaths -join "`n")"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\skills-plugin"
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$stagingRoot = Join-Path $OutputDirectory ("staging-" + [guid]::NewGuid().ToString("N"))
$archivePath = Join-Path $OutputDirectory ("{0}-plugin-{1}.zip" -f $manifest.name, $manifest.version)

try {
    New-Item -ItemType Directory -Force -Path (Join-Path $stagingRoot ".claude-plugin") | Out-Null
    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $stagingRoot ".claude-plugin\plugin.json")

    $skillDirectories = Get-ChildItem -LiteralPath $skillsRoot -Directory
    if ($skillDirectories.Count -eq 0) {
        throw "No skills found."
    }

    foreach ($skillDirectory in $skillDirectories) {
        $skillFile = Join-Path $skillDirectory.FullName "SKILL.md"
        if (-not (Test-Path -LiteralPath $skillFile)) {
            throw "Missing SKILL.md: $($skillDirectory.FullName)"
        }

        $targetSkill = Join-Path $stagingRoot ("skills\" + $skillDirectory.Name)
        New-Item -ItemType Directory -Force -Path $targetSkill | Out-Null
        Copy-Item -LiteralPath $skillFile -Destination (Join-Path $targetSkill "SKILL.md")

        $references = Join-Path $skillDirectory.FullName "references"
        if (Test-Path -LiteralPath $references) {
            Copy-Item -LiteralPath $references -Destination $targetSkill -Recurse
        }
    }

    if (Test-Path -LiteralPath $archivePath) {
        Remove-Item -LiteralPath $archivePath -Force
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $stagingRoot,
        $archivePath,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false
    )

    Write-Output $archivePath
}
finally {
    $resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\') + '\'
    $resolvedStaging = [System.IO.Path]::GetFullPath($stagingRoot)
    if ($resolvedStaging.StartsWith($resolvedOutput, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolvedStaging)) {
        Remove-Item -LiteralPath $resolvedStaging -Recurse -Force
    }
}
