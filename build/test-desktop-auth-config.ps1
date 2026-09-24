[CmdletBinding()]
param([Parameter(Mandatory)] [string] $ConfigurationPath)

$ErrorActionPreference = 'Stop'
# Build tooling only. Never include this script or raw parser errors in a product.
try {
    if (-not (Test-Path -LiteralPath $ConfigurationPath -PathType Leaf)) { throw 'Missing configuration.' }
    $config = Get-Content -LiteralPath $ConfigurationPath -Raw | ConvertFrom-Json
    $names = @($config.PSObject.Properties.Name)
    if ($names.Count -ne 2 -or $names -cnotcontains 'issuer' -or $names -cnotcontains 'clientId') { throw 'Unexpected fields.' }
    if ($config.issuer -isnot [string] -or $config.clientId -isnot [string]) { throw 'Invalid fields.' }
    $issuer = $null
    if (-not [Uri]::TryCreate($config.issuer, [UriKind]::Absolute, [ref]$issuer) -or
        $issuer.Scheme -ne 'https' -or $issuer.HostNameType -ne [UriHostNameType]::Dns -or
        $issuer.IsLoopback -or -not $issuer.IsDefaultPort -or $issuer.UserInfo.Length -ne 0 -or
        $issuer.GetLeftPart([UriPartial]::Authority) -cne $config.issuer -or
        [string]::IsNullOrWhiteSpace($config.clientId) -or $config.clientId -eq 'REPLACE' -or
        $config.clientId.Length -gt 256 -or $config.clientId -match '\s') { throw 'Unconfigured or invalid fields.' }
}
catch {
    throw 'Desktop auth configuration must contain only a valid HTTPS issuer origin and a nonempty public clientId. Fill in the configuration; no client secret or tokens are allowed.'
}
Write-Output 'Desktop auth public configuration validated.'
