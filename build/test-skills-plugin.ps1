param(
    [string] $ExpectedTag,
    [string] $ArchivePath
)

$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$manifestPath = Join-Path $repoRoot ".claude-plugin\plugin.json"
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
$openAiManifestPath = Join-Path $repoRoot ".codex-plugin\plugin.template.json"
$openAiManifest = Get-Content -Raw -LiteralPath $openAiManifestPath | ConvertFrom-Json

if ($manifest.name -notmatch '^[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?$') {
    throw "Plugin name must use lowercase letters, digits, and hyphens: $($manifest.name)"
}
if ($manifest.version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
    throw "Plugin version must use semantic X.Y.Z format: $($manifest.version)"
}
if ([string]::IsNullOrWhiteSpace($manifest.description) -or
    [string]::IsNullOrWhiteSpace($manifest.displayName) -or
    [string]::IsNullOrWhiteSpace($manifest.author.name)) {
    throw "Plugin manifest must define displayName, description, and author.name."
}
if ($ExpectedTag -and $ExpectedTag -ne "skills-v$($manifest.version)") {
    throw "Tag '$ExpectedTag' does not match plugin version '$($manifest.version)'."
}
if ($openAiManifest.name -ne $manifest.name) {
    throw "OpenAI and release plugin manifests must define the same name."
}
if ($openAiManifest.PSObject.Properties.Name -contains 'version') {
    throw "OpenAI plugin template must not define version; it is sourced from .claude-plugin/plugin.json."
}

$interface = $openAiManifest.interface
if ([string]::IsNullOrWhiteSpace($interface.displayName) -or $interface.displayName.Length -gt 30) {
    throw "OpenAI displayName is required and must contain at most 30 characters."
}
if ([string]::IsNullOrWhiteSpace($interface.shortDescription) -or $interface.shortDescription.Length -gt 30) {
    throw "OpenAI shortDescription is required and must contain at most 30 characters."
}
if ([string]::IsNullOrWhiteSpace($interface.longDescription) -or $interface.longDescription.Length -gt 4000) {
    throw "OpenAI longDescription is required and must contain at most 4000 characters."
}
$requiredInterfaceFields = @(
    'developerName',
    'category',
    'websiteURL',
    'supportURL',
    'privacyPolicyURL',
    'termsOfServiceURL',
    'composerIcon',
    'logo'
)
foreach ($field in $requiredInterfaceFields) {
    if ([string]::IsNullOrWhiteSpace($interface.$field)) {
        throw "OpenAI interface field '$field' is required."
    }
}
if ($interface.category -ne 'Data & Analytics') {
    throw "OpenAI category must describe the plugin's performance-analysis purpose."
}
if (@($interface.capabilities).Count -eq 0) {
    throw "OpenAI capabilities must describe the plugin's main functions."
}
foreach ($urlField in @('websiteURL', 'supportURL', 'privacyPolicyURL', 'termsOfServiceURL')) {
    $uri = $null
    if (-not [System.Uri]::TryCreate([string]$interface.$urlField, [System.UriKind]::Absolute, [ref]$uri) -or
        $uri.Scheme -ne 'https') {
        throw "OpenAI interface field '$urlField' must be an absolute HTTPS URL."
    }
}
foreach ($prompt in @($interface.defaultPrompt)) {
    if ([string]::IsNullOrWhiteSpace($prompt) -or $prompt.Length -gt 128) {
        throw "OpenAI starter prompts must contain 1 to 128 characters."
    }
}
if (@($interface.defaultPrompt).Count -gt 3) {
    throw "OpenAI defaultPrompt supports at most three starter prompts."
}

$iconPath = Join-Path $repoRoot "assets\tarkov-performance-icon.png"
if (-not (Test-Path -LiteralPath $iconPath -PathType Leaf)) {
    throw "Missing OpenAI plugin icon: $iconPath"
}
$iconFile = Get-Item -LiteralPath $iconPath
if ($iconFile.Length -gt 5MB) {
    throw "OpenAI plugin icon must not exceed 5 MiB."
}
$iconBytes = [System.IO.File]::ReadAllBytes($iconPath)
$pngSignature = @(137, 80, 78, 71, 13, 10, 26, 10)
if ($iconBytes.Length -lt 24 -or
    (($iconBytes[0..7] | ForEach-Object { [int] $_ }) -join ',') -ne ($pngSignature -join ',')) {
    throw "OpenAI plugin icon must be a valid PNG file."
}
$iconWidth = [System.Net.IPAddress]::NetworkToHostOrder([System.BitConverter]::ToInt32($iconBytes, 16))
$iconHeight = [System.Net.IPAddress]::NetworkToHostOrder([System.BitConverter]::ToInt32($iconBytes, 20))
if ($iconWidth -ne $iconHeight -or $iconWidth -lt 48 -or $iconWidth -gt 4096) {
    throw "OpenAI plugin icon must be square and between 48 and 4096 pixels."
}

$marketplacePath = Join-Path $repoRoot ".claude-plugin\marketplace.json"
$marketplace = Get-Content -Raw -LiteralPath $marketplacePath | ConvertFrom-Json
$marketplaceEntry = @($marketplace.plugins | Where-Object { $_.name -eq $manifest.name })
if ($marketplaceEntry.Count -ne 1) {
    throw "Marketplace must contain exactly one entry for '$($manifest.name)'."
}
if ($marketplaceEntry[0].source -ne "./plugins/tarkov-performance") {
    throw "Marketplace entry must point to ./plugins/tarkov-performance."
}

$skillsRoot = Join-Path $repoRoot "skills"
$skillDirectories = @(Get-ChildItem -LiteralPath $skillsRoot -Directory | Sort-Object Name)
$expectedSkillNames = @(
    'tarkov-config',
    'tarkov-frametime',
    'tarkov-performance-benchmark',
    'tarkov-tuning'
)
$actualSkillNames = @($skillDirectories | ForEach-Object { $_.Name })
if (($actualSkillNames -join "`n") -cne ($expectedSkillNames -join "`n")) {
    throw "Skills plugin must contain exactly: $($expectedSkillNames -join ', '). Found: $($actualSkillNames -join ', ')."
}

foreach ($skillDirectory in $skillDirectories) {
    $skillPath = Join-Path $skillDirectory.FullName "SKILL.md"
    if (-not (Test-Path -LiteralPath $skillPath -PathType Leaf)) {
        throw "Missing SKILL.md: $($skillDirectory.Name)"
    }

    $content = Get-Content -Raw -LiteralPath $skillPath
    $frontmatter = [regex]::Match($content, '\A---\r?\n(?<body>.*?)\r?\n---(?:\r?\n|\z)', 'Singleline')
    if (-not $frontmatter.Success) {
        throw "Invalid YAML front matter boundary: $skillPath"
    }

    $nameMatch = [regex]::Match($frontmatter.Groups['body'].Value, '(?m)^name:\s*(?<value>[^\r\n]+)\r?$')
    $descriptionMatch = [regex]::Match($frontmatter.Groups['body'].Value, '(?m)^description:\s*(?<value>[^\r\n]+)\r?$')
    if (-not $nameMatch.Success -or $nameMatch.Groups['value'].Value.Trim() -ne $skillDirectory.Name) {
        throw "Skill name must match its directory: $skillPath"
    }
    if (-not $descriptionMatch.Success -or [string]::IsNullOrWhiteSpace($descriptionMatch.Groups['value'].Value)) {
        throw "Skill description is missing: $skillPath"
    }

    foreach ($referenceMatch in [regex]::Matches($content, 'references/[A-Za-z0-9_.\-/]+\.md')) {
        $referencePath = Join-Path $skillDirectory.FullName $referenceMatch.Value.Replace('/', '\')
        if (-not (Test-Path -LiteralPath $referencePath -PathType Leaf)) {
            throw "Missing skill reference '$($referenceMatch.Value)' in $skillPath"
        }
    }

    $openAiMetadata = Join-Path $skillDirectory.FullName "agents\openai.yaml"
    if (-not (Test-Path -LiteralPath $openAiMetadata -PathType Leaf)) {
        throw "Missing OpenAI interface metadata: $openAiMetadata"
    }
    $openAiContent = Get-Content -Raw -LiteralPath $openAiMetadata
    $openAiSchema = [regex]::Match(
        $openAiContent,
        '\Ainterface:\r?\n  display_name: "(?<display>[^"\r\n]+)"\r?\n  short_description: "(?<short>[^"\r\n]+)"\r?\n  default_prompt: "(?<prompt>[^"\r\n]+)"\r?\npolicy:\r?\n  allow_implicit_invocation: (?<implicit>true|false)\r?\n?\z'
    )
    if (-not $openAiSchema.Success) {
        throw "OpenAI interface metadata does not match the required schema: $openAiMetadata"
    }
}

$publicPluginRoot = Join-Path $repoRoot "plugins\tarkov-performance"
$publicFiles = @(Get-ChildItem -LiteralPath $publicPluginRoot -Recurse -File -Force)
if ($publicFiles.Count -gt 512) {
    throw "Public plugin contains more than 512 files."
}
$unsupportedExtensions = @('.cmd', '.dll', '.exe', '.ico', '.msix', '.ps1', '.zip')
foreach ($file in $publicFiles) {
    if ($unsupportedExtensions -contains $file.Extension.ToLowerInvariant()) {
        throw "Unsupported file in public plugin: $($file.FullName)"
    }
    if ($file.Length -gt 262144 -and $file.Extension.ToLowerInvariant() -notin @('.gif', '.jpeg', '.jpg', '.png', '.webp')) {
        throw "Public plugin text file exceeds 256 KiB: $($file.FullName)"
    }
}

$readmePath = Join-Path $publicPluginRoot "README.md"
$readmeWords = @([regex]::Matches((Get-Content -Raw -LiteralPath $readmePath), '\b[\p{L}\p{N}][\p{L}\p{N}''-]*\b')).Count
if ($readmeWords -lt 40) {
    throw "Public plugin README must contain at least 40 words; found $readmeWords."
}

if ($ArchivePath) {
    $resolvedArchive = [System.IO.Path]::GetFullPath($ArchivePath)
    if (-not (Test-Path -LiteralPath $resolvedArchive -PathType Leaf)) {
        throw "Plugin archive not found: $resolvedArchive"
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedArchive)
    try {
        $entryNames = @($archive.Entries | ForEach-Object { $_.FullName })
        if ('.codex-plugin/plugin.json' -notin $entryNames) {
            throw "Archive is missing .codex-plugin/plugin.json."
        }
        if ('assets/icon.png' -notin $entryNames) {
            throw "Archive is missing assets/icon.png."
        }

        $manifestEntry = $archive.GetEntry('.codex-plugin/plugin.json')
        $manifestStream = $manifestEntry.Open()
        $manifestReader = New-Object System.IO.StreamReader($manifestStream)
        try {
            $archivedManifest = $manifestReader.ReadToEnd() | ConvertFrom-Json
        }
        finally {
            $manifestReader.Dispose()
            $manifestStream.Dispose()
        }
        if ($archivedManifest.name -ne $manifest.name -or $archivedManifest.version -ne $manifest.version) {
            throw "Archived OpenAI manifest must use release name '$($manifest.name)' and version '$($manifest.version)'."
        }

        foreach ($skillDirectory in $skillDirectories) {
            $skillBase = "skills/$($skillDirectory.Name)"
            if ("$skillBase/SKILL.md" -notin $entryNames) {
                throw "Archive is missing $skillBase/SKILL.md."
            }
            if ("$skillBase/agents/openai.yaml" -notin $entryNames) {
                throw "Archive is missing $skillBase/agents/openai.yaml."
            }
        }

        $unexpected = @($entryNames | Where-Object {
            $_ -notmatch '^(\.codex-plugin/plugin\.json|assets/icon\.png|skills/[^/]+/(SKILL\.md|agents/openai\.yaml|references/[^/]+))$'
        })
        if ($unexpected.Count -gt 0) {
            throw "Archive contains unexpected entries: $($unexpected -join ', ')"
        }
    }
    finally {
        $archive.Dispose()
    }
}

"Skills plugin validation passed for $($skillDirectories.Count) skills at version $($manifest.version)."
