[CmdletBinding(PositionalBinding = $false)]
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
$ErrorActionPreference = 'Stop'
$skillRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $skillRoot 'tool\K2SqlCli\bin\Release\k2sql.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'k2sql.exe is missing; reinstall the k2-sql-smartobjects release.' }
& $exe @Arguments
$code = $LASTEXITCODE
$global:LASTEXITCODE = $code
if ($code -ne 0) { Write-Error "k2sql failed with exit code $code." -ErrorAction Continue }
