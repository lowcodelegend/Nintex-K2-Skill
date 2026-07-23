[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$output = Join-Path $root 'dist'
$esbuild = 'esbuild@0.25.6'
New-Item -ItemType Directory -Path $output -Force | Out-Null

& npx.cmd --yes $esbuild (Join-Path $root 'sidebar-critical.css') --minify --target=chrome120 "--outfile=$(Join-Path $output 'sidebar-critical.min.css')"
if ($LASTEXITCODE -ne 0) { throw 'Critical CSS minification failed.' }
& npx.cmd --yes $esbuild (Join-Path $root 'sidebar-application.css') --minify --target=chrome120 "--outfile=$(Join-Path $output 'sidebar-application.min.css')"
if ($LASTEXITCODE -ne 0) { throw 'Application CSS minification failed.' }
& npx.cmd --yes $esbuild (Join-Path $root 'sidebar-shell.js') --minify --target=chrome120 "--outfile=$(Join-Path $output 'sidebar-shell.min.js')"
if ($LASTEXITCODE -ne 0) { throw 'Sidebar JavaScript minification failed.' }

Get-ChildItem -LiteralPath $output -File | ForEach-Object {
    [pscustomobject]@{
        file = $_.Name
        bytes = $_.Length
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }
}
