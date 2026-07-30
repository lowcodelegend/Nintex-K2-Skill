[CmdletBinding()]
param(
    [string]$ExampleRoot = (Join-Path $PSScriptRoot '..\assets\examples\rest-shaping'),
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($ExampleRoot)
if (-not (Test-Path -LiteralPath (Join-Path $root 'package.json') -PathType Leaf)) {
    throw "JSSP example root is invalid: $root"
}
if ($Clean) {
    $dist = Join-Path $root 'dist'
    if (Test-Path -LiteralPath $dist) { [IO.Directory]::Delete($dist, $true) }
}
Push-Location $root
try {
    & npm test
    if ($LASTEXITCODE -ne 0) { throw 'JSSP tests failed.' }
    & npm run build
    if ($LASTEXITCODE -ne 0) { throw 'JSSP build failed.' }
}
finally { Pop-Location }
