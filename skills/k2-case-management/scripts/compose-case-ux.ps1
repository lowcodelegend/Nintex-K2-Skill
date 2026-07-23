[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Overlay,
    [Parameter(Mandatory=$true)][string]$Output,
    [string]$Base = (Join-Path $PSScriptRoot '..\assets\case-ux.yaml')
)
$ErrorActionPreference='Stop'

function Merge-Value($BaseValue, $OverlayValue) {
    if ($null -eq $OverlayValue) { return $BaseValue }
    if ($null -eq $BaseValue) { return $OverlayValue }
    if ($BaseValue -is [Management.Automation.PSCustomObject] -and $OverlayValue -is [Management.Automation.PSCustomObject]) {
        $result=[ordered]@{}
        foreach($p in $BaseValue.PSObject.Properties){$result[$p.Name]=$p.Value}
        foreach($p in $OverlayValue.PSObject.Properties){
            $result[$p.Name]=if($result.Contains($p.Name)){Merge-Value $result[$p.Name] $p.Value}else{$p.Value}
        }
        return [pscustomobject]$result
    }
    if ($BaseValue -is [Array] -and $OverlayValue -is [Array]) {
        $allIdentified=@($BaseValue + $OverlayValue | Where-Object {$null -ne $_}) | Where-Object {-not ($_ -is [Management.Automation.PSCustomObject]) -or -not ($_.PSObject.Properties.Name -contains 'id')}
        if ($allIdentified.Count -eq 0) {
            $byId=[ordered]@{}
            foreach($item in $BaseValue){$byId[[string]$item.id]=$item}
            foreach($item in $OverlayValue){$id=[string]$item.id;$byId[$id]=if($byId.Contains($id)){Merge-Value $byId[$id] $item}else{$item}}
            return @($byId.Values)
        }
        return $OverlayValue
    }
    return $OverlayValue
}

$baseDocument=Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $Base) | ConvertFrom-Json
$overlayDocument=Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $Overlay) | ConvertFrom-Json
if($overlayDocument.template.extends -ne 'canonical-case-ux'){throw "Overlay template.extends must be 'canonical-case-ux'."}
$composed=Merge-Value $baseDocument $overlayDocument
$destination=[IO.Path]::GetFullPath($Output)
$parent=Split-Path -Parent $destination
if($parent -and -not (Test-Path -LiteralPath $parent)){New-Item -ItemType Directory -Path $parent | Out-Null}
$composed | ConvertTo-Json -Depth 100 | Set-Content -Encoding utf8 -LiteralPath $destination
Write-Output "Composed case UX: $destination"
exit 0
