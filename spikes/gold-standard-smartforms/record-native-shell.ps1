[CmdletBinding()]
param(
    [string]$OutputDirectory = '.artifacts/gold-standard-smartforms/runtime-video',
    [int]$Width = 1440,
    [int]$Height = 900,
    [int]$Port = 9990
)

$ErrorActionPreference = 'Stop'
$toolRoot = Join-Path ([IO.Path]::GetTempPath()) 'gux-native-shell-recorder-v1'
$nodeModules = Join-Path $toolRoot 'node_modules'

if (
    -not (Test-Path -LiteralPath (Join-Path $nodeModules 'ws')) -or
    -not (Test-Path -LiteralPath (Join-Path $nodeModules '@ffmpeg-installer\ffmpeg'))
) {
    New-Item -ItemType Directory -Path $toolRoot -Force | Out-Null
    & npm.cmd install --prefix $toolRoot --no-save --no-audit --no-fund `
        'ws@8.18.0' '@ffmpeg-installer/ffmpeg@1.1.0'
    if ($LASTEXITCODE -ne 0) {
        throw "Recorder dependency installation failed with exit code $LASTEXITCODE."
    }
}

$previousNodePath = $env:NODE_PATH
try {
    $env:NODE_PATH = $nodeModules
    & node.exe (Join-Path $PSScriptRoot 'record-native-shell.js') `
        --output $OutputDirectory `
        --width $Width `
        --height $Height `
        --port $Port
    if ($LASTEXITCODE -ne 0) {
        throw "Native shell recording failed with exit code $LASTEXITCODE."
    }
} finally {
    $env:NODE_PATH = $previousNodePath
}
