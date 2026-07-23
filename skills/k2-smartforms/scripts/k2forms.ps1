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
$hasFormsOnly = @($Arguments | Where-Object { [string]::Equals($_, '--forms-only', [StringComparison]::OrdinalIgnoreCase) }).Count -gt 0
if ($code -ne 0 -and $isDeploy -and -not $hasResume -and -not $hasFormsOnly) {
    $firstOutput | Where-Object { [string]$_ -notmatch '^ERROR:' } | Write-Output
    Write-Warning 'Initial K2 replacement session did not complete; starting the supported resume pass in a fresh authoring process.'
    & $exe @Arguments '--resume'
    $code = $LASTEXITCODE
} else {
    $firstOutput | Write-Output
}
$global:LASTEXITCODE = $code
if ($code -ne 0) { Write-Error "k2forms failed with exit code $code." -ErrorAction Continue }
