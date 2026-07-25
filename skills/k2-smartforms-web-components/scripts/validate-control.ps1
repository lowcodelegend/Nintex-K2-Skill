[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$Source)
$ErrorActionPreference = 'Stop'
$sourcePath = if ([IO.Path]::IsPathRooted($Source)) {
    [IO.Path]::GetFullPath($Source)
} else {
    [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $Source))
}
$validator = Join-Path $PSScriptRoot 'validate_web_component.py'
& python $validator --source $sourcePath
$global:LASTEXITCODE = $LASTEXITCODE
if ($LASTEXITCODE -ne 0) { throw "Modern K2 Web Component validation failed with exit code $LASTEXITCODE." }
