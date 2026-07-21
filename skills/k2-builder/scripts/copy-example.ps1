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

function Assert-ExampleReference {
    param(
        [Parameter(Mandatory = $true)][string]$ExampleRoot,
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][string]$DeclaredBy
    )
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath)) {
        throw "Example reference must be a non-empty relative path ($DeclaredBy): $RelativePath"
    }
    $root = [IO.Path]::GetFullPath($ExampleRoot).TrimEnd('\')
    $target = [IO.Path]::GetFullPath((Join-Path $root $RelativePath))
    if (-not $target.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Example reference escaped its root ($DeclaredBy): $RelativePath"
    }
    if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
        throw "Bundled example reference is missing ($DeclaredBy): $RelativePath"
    }
}

function Assert-ExampleIntegrity {
    param([Parameter(Mandatory = $true)][string]$ExampleRoot)
    $smartObjectsPath = Join-Path $ExampleRoot 'manifest.json'
    if (-not (Test-Path -LiteralPath $smartObjectsPath -PathType Leaf)) {
        throw "Bundled example has no manifest.json: $ExampleRoot"
    }
    $smartObjects = Get-Content -LiteralPath $smartObjectsPath -Raw | ConvertFrom-Json
    foreach ($script in @($smartObjects.database.scripts)) {
        Assert-ExampleReference $ExampleRoot ([string]$script) 'manifest.json database.scripts'
    }

    $solutionPath = Join-Path $ExampleRoot 'solution-manifest.json'
    if (Test-Path -LiteralPath $solutionPath -PathType Leaf) {
        $solution = Get-Content -LiteralPath $solutionPath -Raw | ConvertFrom-Json
        if ($null -ne $solution.components.smartObjects) {
            Assert-ExampleReference $ExampleRoot ([string]$solution.components.smartObjects.manifest) 'solution-manifest.json components.smartObjects'
        }
        if ($null -ne $solution.components.forms) {
            Assert-ExampleReference $ExampleRoot ([string]$solution.components.forms.manifest) 'solution-manifest.json components.forms'
        }
        foreach ($workflow in @($solution.components.workflows)) {
            if ($null -ne $workflow) {
                Assert-ExampleReference $ExampleRoot ([string]$workflow.manifest) 'solution-manifest.json components.workflows'
            }
        }
    }
}

$sourceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\assets\examples'))
$source = [IO.Path]::GetFullPath((Join-Path $sourceRoot $Name))
if (-not $source.StartsWith($sourceRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Example source escaped its asset root: $source"
}
if (-not (Test-Path -LiteralPath $source -PathType Container)) {
    throw "Bundled example was not found: $Name"
}
Assert-ExampleIntegrity $source

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

Assert-ExampleIntegrity $destinationPath

Write-Output "Copied $Name example to $destinationPath"
