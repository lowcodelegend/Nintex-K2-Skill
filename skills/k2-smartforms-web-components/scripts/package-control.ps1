[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Output,
    [switch]$Force
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

$sourcePath = if ([IO.Path]::IsPathRooted($Source)) {
    [IO.Path]::GetFullPath($Source).TrimEnd('\')
} else {
    [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $Source)).TrimEnd('\')
}
$outputPath = if ([IO.Path]::IsPathRooted($Output)) {
    [IO.Path]::GetFullPath($Output)
} else {
    [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $Output))
}
if (-not (Test-Path -LiteralPath $sourcePath -PathType Container)) { throw "Source directory not found: $sourcePath" }
if ([IO.Path]::GetExtension($outputPath) -ine '.zip') { throw 'Output must be a .zip file.' }
if (Test-Path -LiteralPath $outputPath) {
    if (-not $Force) { throw "Output exists; use -Force to replace it: $outputPath" }
    Remove-Item -LiteralPath $outputPath -Force
}
& (Join-Path $PSScriptRoot 'validate-control.ps1') -Source $sourcePath

Add-Type -AssemblyName System.IO.Compression
New-Item -ItemType Directory -Path (Split-Path -Parent $outputPath) -Force | Out-Null
$stream = [IO.File]::Open($outputPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
    $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        $prefix = $sourcePath + '\'
        foreach ($file in Get-ChildItem -LiteralPath $sourcePath -Recurse -File | Sort-Object FullName) {
            $relative = $file.FullName.Substring($prefix.Length).Replace('\', '/')
            $entry = $archive.CreateEntry($relative, [IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
            $input = [IO.File]::OpenRead($file.FullName)
            try {
                $target = $entry.Open()
                try { $input.CopyTo($target) } finally { $target.Dispose() }
            } finally { $input.Dispose() }
        }
    } finally { $archive.Dispose() }
} finally { $stream.Dispose() }
$hash = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($outputPath + '.sha256', "$hash  $([IO.Path]::GetFileName($outputPath))`r`n", [Text.UTF8Encoding]::new($false))
Write-Output "Packaged modern K2 Web Component: $outputPath"
Write-Output "SHA-256: $hash"
