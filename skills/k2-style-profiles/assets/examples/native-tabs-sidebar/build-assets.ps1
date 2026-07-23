[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$output = Join-Path $root 'dist'
$esbuild = 'esbuild@0.25.6'
New-Item -ItemType Directory -Path $output -Force | Out-Null

& npx.cmd --yes $esbuild (Join-Path $root 'tabs-sidebar-critical.css') --minify --target=chrome120 "--outfile=$(Join-Path $output 'tabs-sidebar-critical.min.css')"
if ($LASTEXITCODE -ne 0) { throw 'Critical CSS minification failed.' }
& npx.cmd --yes $esbuild (Join-Path $root 'tabs-sidebar-application.css') --minify --target=chrome120 "--outfile=$(Join-Path $output 'tabs-sidebar-application.min.css')"
if ($LASTEXITCODE -ne 0) { throw 'Application CSS minification failed.' }
& npx.cmd --yes $esbuild (Join-Path $root 'tabs-sidebar-config.js') --minify --target=chrome120 "--outfile=$(Join-Path $output 'tabs-sidebar-config.min.js')"
if ($LASTEXITCODE -ne 0) { throw 'Native-tabs configuration minification failed.' }
& npx.cmd --yes $esbuild (Join-Path $root 'tabs-sidebar.js') --minify --target=chrome120 "--outfile=$(Join-Path $output 'tabs-sidebar.min.js')"
if ($LASTEXITCODE -ne 0) { throw 'Native-tabs JavaScript minification failed.' }

Get-ChildItem -LiteralPath $output -File | ForEach-Object {
    [pscustomobject]@{
        file = $_.Name
        bytes = $_.Length
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }
}
