[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [string]$Name,
    [string]$Output
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($Source)
$manifestPath = Join-Path $root 'manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "manifest.json not found: $manifestPath" }
& (Join-Path $PSScriptRoot 'validate-control.ps1') -Source $root
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
$properties = [ordered]@{
    TagName = [string]$manifest.tagName
    RuntimeScriptFileNames = (@($manifest.runtimeScriptFileNames) -join ',')
    DesigntimeScriptFileNames = (@($manifest.designtimeScriptFileNames) -join ',')
    RuntimeStyleFileNames = (@($manifest.runtimeStyleFileNames) -join ',')
    DesigntimeStyleFileNames = (@($manifest.designtimeStyleFileNames) -join ',')
    Icon = [string]$manifest.icon
    Width = '100%'
}
$placement = [ordered]@{
    name = if ([string]::IsNullOrWhiteSpace($Name)) { [string]$manifest.displayName } else { $Name }
    controlType = [string]$manifest.tagName
    replaceBody = $true
    properties = $properties
}
$json = $placement | ConvertTo-Json -Depth 20
if ([string]::IsNullOrWhiteSpace($Output)) {
    $json
} else {
    $destination = [IO.Path]::GetFullPath($Output)
    $parent = Split-Path -Parent $destination
    if ($parent -and -not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent | Out-Null }
    [IO.File]::WriteAllText($destination, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
    "Wrote SmartForms webComponents placement: $destination"
}
