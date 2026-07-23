[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Manifest,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop';$root=[IO.Path]::GetFullPath($OutputDirectory)
$sets=@(
  @{state='populated';pages=@('operations-dashboard','my-work','case-initiation','case-workspace','reports')},
  @{state='empty';pages=@('operations-dashboard','my-work')},
  @{state='validation-error';pages=@('case-initiation')},
  @{state='sla-breached';pages=@('case-workspace')},
  @{state='read-only';pages=@('case-workspace')},
  @{state='long-content';pages=@('case-workspace')}
)
foreach($set in $sets){$directory=Join-Path $root $set.state;& (Join-Path $PSScriptRoot 'render-case-ux-reference-suite.ps1') -Manifest $Manifest -OutputDirectory $directory -Pages $set.pages -State $set.state;& (Join-Path $PSScriptRoot 'capture-case-ux-reference-suite.ps1') -Manifest $Manifest -ReferenceDirectory $directory -Pages $set.pages;& (Join-Path $PSScriptRoot 'validate-case-ux-reference-suite.ps1') -Manifest $Manifest -ReferenceDirectory $directory -Pages $set.pages}
$summary=[ordered]@{schemaVersion=1;manifest=[IO.Path]::GetFullPath($Manifest);generatedAtUtc=[DateTime]::UtcNow.ToString('o');states=@($sets|ForEach-Object{[ordered]@{state=$_.state;pages=@($_.pages);viewports=@('desktop','laptop','tablet','mobile')}})}
$summary|ConvertTo-Json -Depth 10|Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'evidence.json');Write-Output "Case UX visual evidence built: $root"
