[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ConfigurationPath,
    [ValidateSet('Development', 'Production')] [string] $Environment = 'Production'
)
$ErrorActionPreference = 'Stop'
try {
    $config = Get-Content -LiteralPath $ConfigurationPath -Raw | ConvertFrom-Json
    $properties = @($config.PSObject.Properties.Name)
    $uri = [Uri]$config.baseUri
    $https = $uri.Scheme -eq 'https' -and -not $uri.IsLoopback
    $local = $Environment -eq 'Development' -and $uri.Scheme -eq 'http' -and $uri.Host -eq '127.0.0.1'
    if ($properties.Count -ne 1 -or $properties[0] -cne 'baseUri' -or
        -not $uri.IsAbsoluteUri -or $uri.UserInfo -or $uri.Query -or $uri.Fragment -or
        $uri.AbsolutePath.TrimEnd('/') -ne '/api/bench/v1' -or -not ($https -or $local)) { throw 'Invalid' }
} catch {
    throw 'Invalid Academy API configuration: expected only a public HTTPS baseUri; loopback HTTP is Development-only.'
}
