[CmdletBinding(PositionalBinding = $false)]
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
$ErrorActionPreference = 'Stop'
$skillRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $skillRoot 'tool\K2SmartFormsCli\bin\Release\k2forms.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'k2forms.exe is missing; reinstall the k2-smartforms release.' }
& $exe @Arguments
$code = $LASTEXITCODE
$global:LASTEXITCODE = $code
if ($code -ne 0) { Write-Error "k2forms failed with exit code $code." -ErrorAction Continue }
