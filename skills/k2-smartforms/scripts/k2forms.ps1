[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$CliArguments,
    [switch]$Rebuild
)

$ErrorActionPreference = 'Stop'
$skillRoot = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $skillRoot 'tool\K2SmartFormsCli\bin\Release\k2forms.exe'
if ($Rebuild -or -not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    & (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release -Clean:$Rebuild | Out-Host
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

& $executable @CliArguments
exit $LASTEXITCODE
