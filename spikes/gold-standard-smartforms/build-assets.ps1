[CmdletBinding()]
param(
    [string]$OutputDirectory = 'build/assets'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$output = [IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
if (-not (Test-Path -LiteralPath $output)) {
    New-Item -ItemType Directory -Path $output | Out-Null
}

$esbuild = 'esbuild@0.25.6'
$criticalCssSource = Join-Path $root 'assets/gux-northstar-critical.css'
$applicationCssSource = Join-Path $root 'assets/gux-northstar.css'
$jsSource = Join-Path $root 'assets/gux-northstar.js'
$criticalCssOutput = Join-Path $output 'gux-northstar.css'
$applicationCssOutput = Join-Path $output 'gux-northstar-app.css'
$jsOutput = Join-Path $output 'gux-northstar.js'

& npx.cmd --yes $esbuild $criticalCssSource --minify --target=chrome120 "--outfile=$criticalCssOutput"
if ($LASTEXITCODE -ne 0) {
    throw "Critical CSS minification failed with exit code $LASTEXITCODE."
}

& npx.cmd --yes $esbuild $applicationCssSource --minify --target=chrome120 "--outfile=$applicationCssOutput"
if ($LASTEXITCODE -ne 0) {
    throw "Application CSS minification failed with exit code $LASTEXITCODE."
}

& npx.cmd --yes $esbuild $jsSource --minify --target=chrome120 "--outfile=$jsOutput"
if ($LASTEXITCODE -ne 0) {
    throw "JavaScript minification failed with exit code $LASTEXITCODE."
}

$results = @(
    [ordered]@{
        asset = 'Critical CSS'
        sourceBytes = (Get-Item -LiteralPath $criticalCssSource).Length
        outputBytes = (Get-Item -LiteralPath $criticalCssOutput).Length
        output = $criticalCssOutput
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $criticalCssOutput).Hash
    },
    [ordered]@{
        asset = 'Application CSS'
        sourceBytes = (Get-Item -LiteralPath $applicationCssSource).Length
        outputBytes = (Get-Item -LiteralPath $applicationCssOutput).Length
        output = $applicationCssOutput
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $applicationCssOutput).Hash
    },
    [ordered]@{
        asset = 'JavaScript'
        sourceBytes = (Get-Item -LiteralPath $jsSource).Length
        outputBytes = (Get-Item -LiteralPath $jsOutput).Length
        output = $jsOutput
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $jsOutput).Hash
    }
)

$results | ConvertTo-Json -Depth 4
