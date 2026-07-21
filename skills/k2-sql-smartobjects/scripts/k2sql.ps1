[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$CliArguments,
    [switch]$Rebuild
)

$ErrorActionPreference = 'Stop'
$skillRoot = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $skillRoot 'tool\K2SqlCli\bin\Release\k2sql.exe'
if ($Rebuild -or -not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    & (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release -Clean:$Rebuild | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "k2sql build failed with exit code $LASTEXITCODE." }
}

& $executable @CliArguments
$toolExitCode = $LASTEXITCODE
$global:LASTEXITCODE = $toolExitCode
if ($toolExitCode -ne 0) { Write-Error "k2sql failed with exit code $toolExitCode." -ErrorAction Continue }
return
