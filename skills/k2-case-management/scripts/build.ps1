[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
# The suite packager uses StrictMode for its own implementation. The existing
# case-management test scripts predate that caller policy and must run in their
# declared default PowerShell semantics.
Set-StrictMode -Off
$skillRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $skillRoot 'tool\K2CaseOperationsCli\K2CaseOperationsCli.csproj'
$msbuild = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'

$k2InstallDir = $env:K2_INSTALL_DIR
if ([string]::IsNullOrWhiteSpace($k2InstallDir)) {
    $k2InstallDir = (Get-ItemProperty 'HKLM:\SOFTWARE\SourceCode\blackpearl\blackpearl Core' -ErrorAction SilentlyContinue).InstallDir
}
if ([string]::IsNullOrWhiteSpace($k2InstallDir) -or -not (Test-Path -LiteralPath $k2InstallDir -PathType Container)) {
    throw 'K2 installation not found. Set K2_INSTALL_DIR.'
}
if (-not (Test-Path -LiteralPath $msbuild -PathType Leaf)) {
    throw "MSBuild not found at $msbuild"
}

$target = if ($Clean) { 'Rebuild' } else { 'Build' }
$buildOutput = & $msbuild $project "/t:$target" "/p:Configuration=$Configuration" "/p:K2InstallDir=$($k2InstallDir.TrimEnd('\'))" /nologo /verbosity:quiet 2>&1
if ($LASTEXITCODE -ne 0) {
    $buildOutput | Write-Error
    exit $LASTEXITCODE
}

$output = Join-Path $skillRoot "tool\K2CaseOperationsCli\bin\$Configuration\k2caseops.exe"
& $output version | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "k2caseops version check failed with exit code $LASTEXITCODE."
}

$previousBytecodeSetting = $env:PYTHONDONTWRITEBYTECODE
$env:PYTHONDONTWRITEBYTECODE = '1'
try {
    & (Join-Path $PSScriptRoot '..\tests\run-tests.ps1')
    if ($LASTEXITCODE -ne 0) {
        throw "k2-case-management tests failed with exit code $LASTEXITCODE."
    }
} finally {
    $env:PYTHONDONTWRITEBYTECODE = $previousBytecodeSetting
}

Write-Output 'k2-case-management build validation passed.'
Write-Output $output
