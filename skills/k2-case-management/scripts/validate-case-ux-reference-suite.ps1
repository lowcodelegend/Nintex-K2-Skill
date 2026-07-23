[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Manifest,[Parameter(Mandatory=$true)][string]$ReferenceDirectory,[string[]]$Pages=@('operations-dashboard','my-work','case-initiation','case-workspace','reports'))
$ErrorActionPreference='Stop';$ux=Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $Manifest)|ConvertFrom-Json;$root=[IO.Path]::GetFullPath($ReferenceDirectory);$issues=@();$metricsPath=Join-Path $root 'layout-metrics.json';$metrics=if(Test-Path -LiteralPath $metricsPath){@(Get-Content -Raw -LiteralPath $metricsPath|ConvertFrom-Json)}else{@()}
foreach($page in $Pages){
  $path=Join-Path $root ($page+'.html');if(-not(Test-Path -LiteralPath $path)){$issues+="missing HTML: $page";continue};$html=Get-Content -Raw -LiteralPath $path
  foreach($token in @('<main','<h1','aria-label=','data-page=')){if($html -notlike "*$token*"){$issues+="$page missing $token"}}
  if($html -notmatch 'class=["'']skip["'']'){$issues+="$page missing skip link"}
  $primaryCount=([regex]::Matches($html,"class='button primary(?! global-create)")).Count;if($primaryCount -gt 1){$issues+="$page exposes $primaryCount simultaneous contextual primary actions"}
  if($page -eq 'operations-dashboard' -and ($html -notlike '*role=''img''*' -or $html -notlike '*View data table*')){$issues+='dashboard lacks chart text/table alternatives'}
  if($page -eq 'case-initiation' -and ($html -notlike '*aria-current=''step''*' -or $html -notlike '*Submit case*')){$issues+='initiation lacks progress or final submit action'}
  if($page -eq 'case-workspace' -and ($html -notlike '*aria-label=''Case lifecycle''*' -or $html -notlike '*Available actions*')){$issues+='workspace lacks persistent lifecycle/action semantics'}
  foreach($viewport in @($ux.visual_acceptance.viewports)){$png=Join-Path $root ($page+'-'+$viewport.name+'.png');if(-not(Test-Path -LiteralPath $png)){$issues+="missing capture: $page/$($viewport.name)"}elseif((Get-Item -LiteralPath $png).Length -lt 5000){$issues+="suspiciously small capture: $page/$($viewport.name)"}}
  foreach($viewport in @($ux.visual_acceptance.viewports)){$metric=@($metrics|Where-Object {$_.page -eq $page -and $_.viewport -eq $viewport.name})|Select-Object -First 1;if($null -eq $metric){$issues+="missing browser layout metrics: $page/$($viewport.name)"}elseif($metric.horizontalOverflow -or $metric.scrollWidth -gt $metric.clientWidth){$issues+="horizontal overflow: $page/$($viewport.name) ($($metric.scrollWidth)>$($metric.clientWidth))"}}
}
if($issues.Count){$issues|ForEach-Object{Write-Error $_};exit 2};Write-Output "Case UX reference suite valid: $($Pages.Count) pages x $(@($ux.visual_acceptance.viewports).Count) viewports."
