[CmdletBinding()]
param(
    [ValidateSet('iso-3166-1-country')]
    [string]$Catalog = 'iso-3166-1-country',

    [Parameter(Mandatory = $true)]
    [string]$Destination,

    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

$skillRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $skillRoot ('assets\reference-data\' + $Catalog + '.sql')
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
    throw "Bundled reference-data catalog not found: $Catalog"
}

$destinationPath = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $destinationPath) {
    if (-not $Force) {
        throw "Destination already exists. Review it and use -Force to replace it: $destinationPath"
    }
    if (-not (Test-Path -LiteralPath $destinationPath -PathType Leaf)) {
        throw "Destination exists but is not a file: $destinationPath"
    }
}

$parent = Split-Path -Parent $destinationPath
if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}
Copy-Item -LiteralPath $source -Destination $destinationPath -Force:$Force

$hash = (Get-FileHash -LiteralPath $destinationPath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Output "Copied $Catalog to $destinationPath"
Write-Output "SHA256 $hash"
