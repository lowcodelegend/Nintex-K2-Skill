[CmdletBinding(PositionalBinding = $false)]
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
$ErrorActionPreference = 'Stop'
$skillRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $skillRoot 'tool\K2SmartBoxCli\bin\Release\k2smartbox.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw 'k2smartbox.exe is missing; reinstall the k2-smartbox-smartobjects release.'
}
& $exe @Arguments
$code = $LASTEXITCODE
$global:LASTEXITCODE = $code
if ($code -ne 0) {
    Write-Error "k2smartbox failed with exit code $code." -ErrorAction Continue
}
