[CmdletBinding(PositionalBinding=$false)]
param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Arguments)
$ErrorActionPreference='Stop'
$skillRoot=Split-Path -Parent $PSScriptRoot
$exe=Join-Path $skillRoot 'tool\K2WebComponentCli\bin\Release\k2controls.exe'
if(-not(Test-Path -LiteralPath $exe)){throw "k2controls is not built. Run '$PSScriptRoot\build.ps1'."}
& $exe @Arguments
exit $LASTEXITCODE
