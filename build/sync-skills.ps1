param(
    [switch]$Check
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$map = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "sync-map.json") | ConvertFrom-Json

$drift = @()
$synced = 0

foreach ($entry in $map.PSObject.Properties) {
    $masterPath = Join-Path $repoRoot $entry.Name
    if (-not (Test-Path -LiteralPath $masterPath)) {
        throw "Master file not found: $($entry.Name)"
    }

    $masterContent = Get-Content -Raw -LiteralPath $masterPath
    foreach ($target in @($entry.Value)) {
        $targetPath = Join-Path $repoRoot $target
        $upToDate = (Test-Path -LiteralPath $targetPath) -and ((Get-Content -Raw -LiteralPath $targetPath) -ceq $masterContent)
        if ($upToDate) {
            continue
        }

        if ($Check) {
            $drift += "$target (master: $($entry.Name))"
        }
        else {
            New-Item -ItemType Directory -Force (Split-Path -Parent $targetPath) | Out-Null
            Copy-Item -LiteralPath $masterPath -Destination $targetPath -Force
            "synced: $target"
            $synced++
        }
    }
}

if ($Check) {
    if ($drift.Count -gt 0) {
        "OUT OF SYNC (edit the master, then run build/sync-skills.ps1):"
        $drift | ForEach-Object { "  $_" }
        exit 1
    }
    "All vendored copies match their masters."
}
elseif ($synced -eq 0) {
    "All vendored copies already match their masters."
}
