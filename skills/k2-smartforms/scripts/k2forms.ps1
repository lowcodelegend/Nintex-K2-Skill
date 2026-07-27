[CmdletBinding(PositionalBinding = $false)]
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
$ErrorActionPreference = 'Stop'
$skillRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $skillRoot 'tool\K2SmartFormsCli\bin\Release\k2forms.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'k2forms.exe is missing; reinstall the k2-smartforms release.' }
$firstOutput = @(& $exe @Arguments 2>&1)
$code = $LASTEXITCODE
$isDeploy = $Arguments.Count -gt 0 -and [string]::Equals($Arguments[0], 'deploy', [StringComparison]::OrdinalIgnoreCase)
$hasResume = @($Arguments | Where-Object { [string]::Equals($_, '--resume', [StringComparison]::OrdinalIgnoreCase) }).Count -gt 0
$knownReplacementRecovery = @($firstOutput | Where-Object {
    [string]$_ -match '^ERROR:\s+REPLACEMENT RECOVERY REQUIRED:'
}).Count -eq 1
if ($code -ne 0 -and $isDeploy -and -not $hasResume -and $knownReplacementRecovery) {
    $firstOutput | Where-Object { [string]$_ -notmatch '^ERROR:' } | Write-Output
    Write-Warning 'Replacement deletion completed; the same deploy command is creating missing artifacts in one bounded fresh-process recovery pass.'
    & $exe @Arguments '--resume'
    $code = $LASTEXITCODE
} else {
    $firstOutput | Write-Output
}
$global:LASTEXITCODE = $code
if ($code -ne 0) { Write-Error "k2forms failed with exit code $code." -ErrorAction Continue }
