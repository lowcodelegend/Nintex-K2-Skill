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

$paletteTestRoot = Join-Path ([IO.Path]::GetTempPath()) ('K2StylePaletteTest-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $paletteTestRoot -Force | Out-Null
    $paletteOutput = Join-Path $paletteTestRoot 'k2-color-scheme.css'
    & (Join-Path $PSScriptRoot 'new-k2-color-scheme.ps1') `
        -Palette (Join-Path $skillRoot 'assets\template\palette.json') `
        -Output $paletteOutput | Out-Null
    if (-not $?) { throw 'K2 colour-scheme generation test failed.' }
    & (Join-Path $PSScriptRoot 'test-k2-color-scheme.ps1') -Css $paletteOutput | Out-Null
    if (-not $?) { throw 'K2 colour-scheme coverage test failed.' }
    Write-Output 'PALETTE TEST SUCCEEDED: installed K2 colour variables and contextual declarations are fully covered.'
} finally {
    if (Test-Path -LiteralPath $paletteTestRoot) {
        [IO.Directory]::Delete([IO.Path]::GetFullPath($paletteTestRoot), $true)
    }
}
Write-Output $output
