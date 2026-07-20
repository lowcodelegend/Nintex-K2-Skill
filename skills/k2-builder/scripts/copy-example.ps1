[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('corporate-workflow', 'expense-claim', 'request-management')]
    [string]$Name,

    [Parameter(Mandatory = $true)]
    [string]$Destination,

    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

$sourceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\assets\examples'))
$source = [IO.Path]::GetFullPath((Join-Path $sourceRoot $Name))
if (-not $source.StartsWith($sourceRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Example source escaped its asset root: $source"
}
if (-not (Test-Path -LiteralPath $source -PathType Container)) {
    throw "Bundled example was not found: $Name"
}

$destinationPath = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $destinationPath) {
    if (-not (Test-Path -LiteralPath $destinationPath -PathType Container)) {
        throw "Destination exists and is not a directory: $destinationPath"
    }
    $existing = @(Get-ChildItem -LiteralPath $destinationPath -Force)
    if ($existing.Count -gt 0 -and -not $Force) {
        throw "Destination is not empty. Use -Force to overwrite matching files: $destinationPath"
    }
} else {
    New-Item -ItemType Directory -Path $destinationPath | Out-Null
}

$sourcePrefix = $source.TrimEnd('\') + '\'
foreach ($file in Get-ChildItem -LiteralPath $source -Recurse -File -Force) {
    $relative = $file.FullName.Substring($sourcePrefix.Length)
    $target = [IO.Path]::GetFullPath((Join-Path $destinationPath $relative))
    if (-not $target.StartsWith($destinationPath.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Example file escaped the destination: $relative"
    }
    $parent = Split-Path -Parent $target
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    Copy-Item -LiteralPath $file.FullName -Destination $target -Force:$Force
}

Write-Output "Copied $Name example to $destinationPath"
