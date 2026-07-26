[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
# The suite packager uses StrictMode for its own implementation. The existing
# case-management test scripts predate that caller policy and must run in their
# declared default PowerShell semantics.
Set-StrictMode -Off
$previousBytecodeSetting = $env:PYTHONDONTWRITEBYTECODE
$env:PYTHONDONTWRITEBYTECODE = '1'
try {
    & (Join-Path $PSScriptRoot '..\tests\run-tests.ps1')
    if ($LASTEXITCODE -ne 0) {
        throw "k2-case-management tests failed with exit code $LASTEXITCODE."
    }
} finally {
    $env:PYTHONDONTWRITEBYTECODE = $previousBytecodeSetting
}

Write-Output 'k2-case-management build validation passed.'
