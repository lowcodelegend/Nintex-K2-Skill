[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$skillRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $skillRoot 'tool\K2StyleProfilesCli\K2StyleProfilesCli.csproj'
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

$output = Join-Path $skillRoot "tool\K2StyleProfilesCli\bin\$Configuration\k2style.exe"
& $output selftest | Out-Host
if ($LASTEXITCODE -ne 0) { throw "k2style self-test failed with exit code $LASTEXITCODE." }
Write-Output $output
