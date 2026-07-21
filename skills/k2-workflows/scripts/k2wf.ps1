[CmdletBinding(PositionalBinding = $false)]
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
$ErrorActionPreference = 'Stop'
$skillRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $skillRoot 'tool\K2WorkflowCli\bin\Release\k2wf.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'k2wf.exe is missing; reinstall the k2-workflows release.' }
& $exe @Arguments
$code = $LASTEXITCODE
$global:LASTEXITCODE = $code
if ($code -ne 0) { Write-Error "k2wf failed with exit code $code." -ErrorAction Continue }
