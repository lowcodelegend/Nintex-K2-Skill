[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

$ErrorActionPreference = 'Stop'
$executable = Join-Path $PSScriptRoot '..\tool\K2CaseOperationsCli\bin\Release\k2caseops.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "k2caseops executable was not found: $executable"
}

& $executable @Arguments
exit $LASTEXITCODE
