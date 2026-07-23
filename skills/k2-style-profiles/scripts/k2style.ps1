[CmdletBinding(PositionalBinding = $false)]
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)

$ErrorActionPreference = 'Stop'
$skillRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $skillRoot 'tool\K2StyleProfilesCli\bin\Release\k2style.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw 'k2style.exe is missing; reinstall the k2-style-profiles release.'
}
& $exe @Arguments
$code = $LASTEXITCODE
$global:LASTEXITCODE = $code
if ($code -ne 0) { Write-Error "k2style failed with exit code $code." -ErrorAction Continue }
