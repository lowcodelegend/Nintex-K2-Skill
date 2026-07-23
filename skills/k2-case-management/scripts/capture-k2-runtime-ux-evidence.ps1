[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$RuntimeBaseUrl,
    [Parameter(Mandatory=$true)][string[]]$FormNames,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$TrustedAuthHost,
    [ValidateRange(0,30000)][int]$SettleMilliseconds=5000,
    [ValidateRange(1024,65500)][int]$StartingPort=9500
)
$ErrorActionPreference='Stop'
$capture=Join-Path $PSScriptRoot 'capture-case-ux-page-cdp.ps1'
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(-not(Test-Path -LiteralPath $output)){New-Item -ItemType Directory -Path $output|Out-Null}
$viewports=@(
    [ordered]@{name='desktop';width=1440;height=1000},
    [ordered]@{name='laptop';width=1280;height=800},
    [ordered]@{name='tablet';width=768;height=1024},
    [ordered]@{name='mobile';width=390;height=844}
)
$results=[Collections.Generic.List[object]]::new();$port=$StartingPort
foreach($formName in $FormNames){
    $slug=($formName.ToLowerInvariant()-replace '[^a-z0-9]+','-').Trim('-')
    $url=$RuntimeBaseUrl.TrimEnd('/')+'/Runtime/Form/'+[Uri]::EscapeDataString($formName)+'/'
    foreach($viewport in $viewports){
        $image=Join-Path $output ($slug+'-'+$viewport.name+'.png')
        $profile=Join-Path $output ('.cdp-'+$slug+'-'+$viewport.name)
        $raw=@(& $capture -Url $url -Width $viewport.width -Height $viewport.height -Output $image -Profile $profile -Port $port -TrustedAuthHost $TrustedAuthHost -SettleMilliseconds $SettleMilliseconds)
        $port++
        $result=($raw|Select-Object -Last 1)|ConvertFrom-Json
        if($result.layout.horizontalOverflow){throw "Horizontal overflow at $formName/$($viewport.name): $($result.layout.scrollWidth) > $($result.layout.clientWidth)."}
        if(-not[string]::Equals([string]$result.layout.title,$formName,[StringComparison]::Ordinal)){throw "Runtime capture did not render '$formName' at $($viewport.name); title was '$($result.layout.title)' ($($result.layout.url))."}
        if([int]$result.layout.textLength-lt 40){throw "Runtime capture for '$formName' at $($viewport.name) contained too little rendered content."}
        $unexpected=@($result.diagnostics|Where-Object{$_.text -notmatch "^TypeError: Cannot read properties of null \(reading '0'\)"})
        if($unexpected.Count-gt 0){throw "Unexpected browser diagnostic at $formName/$($viewport.name): $($unexpected[0].text)"}
        $results.Add([ordered]@{form=$formName;viewport=$viewport.name;width=$viewport.width;height=$viewport.height;image=[IO.Path]::GetFileName($image);url=$result.layout.url;title=$result.layout.title;textLength=$result.layout.textLength;horizontalOverflow=$false;knownK2Diagnostics=@($result.diagnostics|ForEach-Object{$_.text})})
        Write-Output "Captured native Runtime UX: $formName / $($viewport.name)"
    }
}
$report=[ordered]@{capturedUtc=[DateTime]::UtcNow.ToString('o');runtimeBaseUrl=$RuntimeBaseUrl;forms=@($FormNames);viewports=$viewports;captures=@($results);knownDiagnosticPolicy="K2 5.10 emits a non-blocking DataLabel setValue null-index diagnostic on generated capture Views; any other browser diagnostic fails evidence capture."}
$reportPath=Join-Path $output 'runtime-ux-evidence.json';$report|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $reportPath -Encoding utf8
Write-Output "Native Runtime UX evidence: $reportPath"
