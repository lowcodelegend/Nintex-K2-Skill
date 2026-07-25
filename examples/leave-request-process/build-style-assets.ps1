[CmdletBinding()]
param([string]$VariablesCss)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$generator = Join-Path $root '..\..\skills\k2-style-profiles\scripts\new-k2-color-scheme.ps1'
$validator = Join-Path $root '..\..\skills\k2-style-profiles\scripts\test-k2-color-scheme.ps1'
$palette = Join-Path $root 'assets\lpr-blue-white.palette.json'
$temporaryDirectory = Join-Path $root '.tmp'
$readable = Join-Path $temporaryDirectory 'lpr-blue-white-k2-vars.v2.css'
$production = Join-Path $root 'assets\lpr-blue-white-k2-vars.v2.min.css'

New-Item -ItemType Directory -Path $temporaryDirectory -Force | Out-Null
$generatorArguments = @{ Palette = $palette; Output = $readable }
$validatorArguments = @{ Css = $readable }
if (-not [string]::IsNullOrWhiteSpace($VariablesCss)) {
    $generatorArguments.VariablesCss = $VariablesCss
    $validatorArguments.VariablesCss = $VariablesCss
}
& $generator @generatorArguments
if (-not $?) { throw 'K2 colour adapter generation failed.' }

& $validator @validatorArguments
if (-not $?) { throw 'Readable K2 colour adapter validation failed.' }

& npx.cmd --yes esbuild@0.25.6 $readable --minify --target=chrome120 "--outfile=$production"
if ($LASTEXITCODE -ne 0) { throw "CSS minification failed with exit code $LASTEXITCODE." }

$validatorArguments.Css = $production
& $validator @validatorArguments
if (-not $?) { throw 'Minified K2 colour adapter validation failed.' }

[ordered]@{
    file = $production
    bytes = (Get-Item -LiteralPath $production).Length
    sha256 = (Get-FileHash -LiteralPath $production -Algorithm SHA256).Hash.ToLowerInvariant()
} | ConvertTo-Json
