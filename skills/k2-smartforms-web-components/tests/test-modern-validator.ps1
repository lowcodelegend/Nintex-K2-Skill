[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$skillRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$validator = Join-Path $skillRoot 'scripts\validate-control.ps1'
$scaffolder = Join-Path $skillRoot 'scripts\scaffold-control.ps1'
$northstar = [IO.Path]::GetFullPath((Join-Path $skillRoot '..\k2-case-management\assets\northstar-case-homepage'))
$palette = [IO.Path]::GetFullPath((Join-Path $skillRoot '..\k2-case-management\assets\northstar-command-palette'))
$dashboardWidget = [IO.Path]::GetFullPath((Join-Path $skillRoot '..\k2-case-management\assets\northstar-dashboard-widget'))

& $validator -Source $northstar
& $validator -Source $palette
& $validator -Source $dashboardWidget
$prototypeCss = [IO.Path]::GetFullPath((Join-Path $skillRoot '..\..\examples\supplier-nonconformance\gold-standard-prototype\styles.css'))
$componentCss = Join-Path $northstar 'northstar-prototype.css'
$prototypeHash = (Get-FileHash -LiteralPath $prototypeCss -Algorithm SHA256).Hash
$componentHash = (Get-FileHash -LiteralPath $componentCss -Algorithm SHA256).Hash
if ($prototypeHash -cne $componentHash) {
  throw "Northstar prototype CSS fidelity failed: $componentHash != $prototypeHash"
}
$runtimeSource = Get-Content -Raw -LiteralPath (Join-Path $northstar 'northstar-runtime.js')
if ($runtimeSource -match 'K2\.RaiseEvent' -or $runtimeSource -notmatch 'SourceCode\.Forms\.ControlStyles') {
  throw 'Northstar runtime does not use the verified modern K2 event/style APIs.'
}

$paletteSource = Get-Content -Raw -LiteralPath (Join-Path $palette 'northstar-command-runtime.js')
foreach ($required in @(
  'slice(0, 50)',
  'dispatchEvent(new Event("Navigate"))',
  'String(event.key).toLowerCase() === "k"',
  'parsed.origin !== window.location.origin',
  'textContent =',
  'role", "dialog"',
  'aria-live'
)) {
  if ($paletteSource -notmatch [regex]::Escape($required)) {
    throw "Northstar command palette is missing required runtime contract: $required"
  }
}
if ($paletteSource -match '\.innerHTML\s*=') {
  throw 'Northstar command palette writes live content through innerHTML.'
}

$dashboardSource = Get-Content -Raw -LiteralPath (Join-Path $dashboardWidget 'control-runtime.js')
foreach ($required in @(
  'listItemsChangedCallback(itemsChangedEventArgs)',
  'itemsChangedEventArgs.NewItems',
  'dispatchEvent(new Event("Navigate"))',
  'parsed.origin !== window.location.origin',
  'slice(0, 100)',
  'role", "img"',
  'textContent ='
)) {
  if ($dashboardSource -notmatch [regex]::Escape($required)) {
    throw "Northstar dashboard widget is missing required runtime contract: $required"
  }
}
if ($dashboardSource -match '\.innerHTML\s*=') {
  throw 'Northstar dashboard widget writes live content through innerHTML.'
}

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('k2-modern-control-test-' + [guid]::NewGuid())
try {
  & $scaffolder -TagName 'validator-test-control' -DisplayName 'Validator Test Control' -OutputDirectory $testRoot
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

Write-Output "Modern-only validator, bounded command-palette, and byte-exact Northstar oracle CSS tests: PASS ($prototypeHash)"
& (Join-Path $PSScriptRoot 'test-command-palette-browser.ps1')
