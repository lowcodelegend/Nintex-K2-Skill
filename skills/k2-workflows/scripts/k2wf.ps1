[CmdletBinding(PositionalBinding = $false)]
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
$ErrorActionPreference = 'Stop'
$skillRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $skillRoot 'tool\K2WorkflowCli\bin\Release\k2wf.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { & (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release | Out-Null }
& $exe @Arguments
exit $LASTEXITCODE
