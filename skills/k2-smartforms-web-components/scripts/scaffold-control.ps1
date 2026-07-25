[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-z][a-z0-9]*(?:-[a-z0-9]+)+$')][string]$TagName,
    [Parameter(Mandatory = $true)][string]$DisplayName,
    [Parameter(Mandatory = $true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$target = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
    [IO.Path]::GetFullPath($OutputDirectory)
} else {
    [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $OutputDirectory))
}
if (Test-Path -LiteralPath $target) { throw "OutputDirectory already exists: $target" }
$template = Join-Path (Split-Path -Parent $PSScriptRoot) 'assets\starter-control'
Copy-Item -LiteralPath $template -Destination $target -Recurse
foreach ($file in Get-ChildItem -LiteralPath $target -Recurse -File) {
    $text = [IO.File]::ReadAllText($file.FullName)
    $text = $text.Replace('{{TAG_NAME}}', $TagName).Replace('{{DISPLAY_NAME}}', $DisplayName)
    [IO.File]::WriteAllText($file.FullName, $text, [Text.UTF8Encoding]::new($false))
}
& (Join-Path $PSScriptRoot 'validate-control.ps1') -Source $target
Write-Output "Scaffolded modern K2 Web Component: $target"
