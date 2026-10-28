[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$skillRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$validator = Join-Path $skillRoot 'scripts\validate-control.ps1'
$scaffolder = Join-Path $skillRoot 'scripts\scaffold-control.ps1'

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('k2-modern-control-test-' + [guid]::NewGuid())
try {
  & $scaffolder -TagName 'validator-test-control' -DisplayName 'Validator Test Control' -OutputDirectory $testRoot
  & $validator -Source $testRoot
  Set-Content -LiteralPath (Join-Path $testRoot 'forbidden.cs') -Encoding utf8 -Value 'public class Forbidden {}'
  $failed = $false
  try {
    & $validator -Source $testRoot
  } catch {
    $failed = $true
  }
  if (-not $failed) {
    throw 'The validator accepted a legacy .NET custom-control source file.'
  }
} finally {
  $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
  $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
  if ($resolvedTestRoot.StartsWith($resolvedTemp + '\', [StringComparison]::OrdinalIgnoreCase) -and
      (Test-Path -LiteralPath $resolvedTestRoot)) {
    Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
  }
}

Write-Output 'Modern control scaffold validation and legacy-source rejection: PASS'
