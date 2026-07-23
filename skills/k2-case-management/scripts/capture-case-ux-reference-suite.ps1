[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Manifest,[Parameter(Mandatory=$true)][string]$ReferenceDirectory,[string[]]$Pages=@('operations-dashboard','my-work','case-initiation','case-workspace','reports'))
$ErrorActionPreference='Stop'
$ux=Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $Manifest)|ConvertFrom-Json
$root=[IO.Path]::GetFullPath($ReferenceDirectory);$edge='C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe';$cdp=Join-Path $PSScriptRoot 'capture-case-ux-page-cdp.ps1'
if(-not(Test-Path -LiteralPath $edge)){throw 'Microsoft Edge was not found.'};$metrics=@();$profile=Join-Path ([IO.Path]::GetTempPath()) ('k2-case-ux-edge-'+[guid]::NewGuid());New-Item -ItemType Directory -Path $profile|Out-Null
try{foreach($page in $Pages){
  $html=Join-Path $root ($page+'.html');if(-not(Test-Path -LiteralPath $html)){throw "Reference HTML not found: $html"}
  $uri=[Uri]$html
  foreach($viewport in @($ux.visual_acceptance.viewports)){
    $png=Join-Path $root ($page+'-'+$viewport.name+'.png')
    $captureProfile=Join-Path $profile ($page+'-'+$viewport.name);New-Item -ItemType Directory -Path $captureProfile|Out-Null
    $layoutJson=& $cdp -Url $uri.AbsoluteUri -Width ([int]$viewport.width) -Height ([int]$viewport.height) -Output $png -Profile $captureProfile -Port (9333+$metrics.Count)
    if(-not(Test-Path -LiteralPath $png)){throw "Reference capture failed: $page/$($viewport.name)"}
    Write-Output "Captured case UX reference: $png"
    $layout=$layoutJson|ConvertFrom-Json;$metrics+=[pscustomobject]@{page=$page;viewport=$viewport.name;width=[int]$viewport.width;height=[int]$viewport.height;clientWidth=[int]$layout.clientWidth;scrollWidth=[int]$layout.scrollWidth;horizontalOverflow=[bool]$layout.horizontalOverflow}
  }
}
$metrics|ConvertTo-Json -Depth 5|Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'layout-metrics.json')
}finally{$resolvedProfile=[IO.Path]::GetFullPath($profile);$resolvedTemp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\');if($resolvedProfile.StartsWith($resolvedTemp+'\',[StringComparison]::OrdinalIgnoreCase)-and(Test-Path -LiteralPath $resolvedProfile)){for($attempt=0;$attempt-lt 10-and(Test-Path -LiteralPath $resolvedProfile);$attempt++){Start-Sleep -Milliseconds 100;Remove-Item -LiteralPath $resolvedProfile -Recurse -Force -ErrorAction SilentlyContinue}}}
