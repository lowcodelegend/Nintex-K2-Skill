[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$RuntimeBaseUrl,
    [Parameter(Mandatory=$true)][string[]]$FormNames,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$TrustedAuthHost,
    [hashtable]$ValidationClickNames=@{},
    [hashtable]$ExpectedInvalidCounts=@{},
    [ValidateRange(0,30000)][int]$SettleMilliseconds=5000,
    [ValidateRange(1024,65500)][int]$StartingPort=9500
)
$ErrorActionPreference='Stop'
$capture=Join-Path $PSScriptRoot 'capture-browser-page-cdp.mjs'
$node=(Get-Command node -ErrorAction Stop).Source
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
        $arguments=@(
            '--max-old-space-size=64',
            '--experimental-websocket',
            $capture,
            '--url',$url,
            '--width',[string]$viewport.width,
            '--height',[string]$viewport.height,
            '--output',$image,
            '--profile',$profile,
            '--port',[string]$port,
            '--settle',[string]$SettleMilliseconds
        )
        if(-not[string]::IsNullOrWhiteSpace($TrustedAuthHost)){
            $arguments+=@('--trusted-auth-host',$TrustedAuthHost)
        }
        $validationClick=if($ValidationClickNames.ContainsKey($formName)){
            [string]$ValidationClickNames[$formName]
        }else{''}
        if(-not[string]::IsNullOrWhiteSpace($validationClick)){
            $arguments+=@('--click-name',$validationClick)
        }
        $raw=@(& $node @arguments)
        if($LASTEXITCODE-ne 0){throw "Browser capture failed for '$formName' at $($viewport.name) with exit code $LASTEXITCODE."}
        $port++
        $result=($raw|Select-Object -Last 1)|ConvertFrom-Json
        if($result.layout.horizontalOverflow){throw "Horizontal overflow at $formName/$($viewport.name): $($result.layout.scrollWidth) > $($result.layout.clientWidth)."}
        if(-not[string]::Equals([string]$result.layout.title,$formName,[StringComparison]::Ordinal)){throw "Runtime capture did not render '$formName' at $($viewport.name); title was '$($result.layout.title)' ($($result.layout.url))."}
        if([int]$result.layout.textLength-lt 40){throw "Runtime capture for '$formName' at $($viewport.name) contained too little rendered content."}
        if([int]$result.layout.shellCount-ne 1-or-not[bool]$result.layout.northstarReady-or-not[bool]$result.layout.contentReady){
            throw "Northstar Runtime composition was not ready at $formName/$($viewport.name) (shellCount=$($result.layout.shellCount), stylesReady=$($result.layout.northstarReady), contentReady=$($result.layout.contentReady), documentReadyState=$($result.layout.readyState))."
        }
        if([int]$viewport.width-le 800){
            $isGuided=@($result.layout.guidedJourney.controls).Count -gt 0
            $introEnd=if($isGuided){
                @($result.layout.regions|Where-Object selector -eq '.k2sp-page-intro')[0]
            }else{
                @($result.layout.regions|Where-Object selector -eq '.k2sp-insight')[0]
            }
            $nativeStart=@($result.layout.regions|Where-Object selector -eq '.k2sp-application-content')[0]
            if($null-eq$introEnd-or$null-eq$nativeStart-or[double]$nativeStart.top-lt[double]$introEnd.bottom){
                throw "Northstar native content overlaps the responsive shell at $formName/$($viewport.name)."
            }
        }
        $guidedControls=@($result.layout.guidedJourney.controls)
        if($guidedControls.Count-gt 0){
            $tabs=@($result.layout.guidedJourney.tabAnchors)
            if($result.layout.bodyClass-notmatch 'k2sp-page-initiation' -or
                $null-eq$result.layout.guidedJourney.directAdvanceProbe -or
                -not[bool]$result.layout.guidedJourney.directAdvanceProbe.blocked -or
                $tabs.Count-lt 3 -or
                [string]$tabs[0].stepState-ne'current' -or
                @($tabs|Select-Object -Skip 1|Where-Object stepLocked -ne 'true').Count-gt 0){
                throw "Northstar guided-journey ownership/forward-navigation gate failed at $formName/$($viewport.name)."
            }
            $preFill=$result.layout.guidedJourney.preFill
            if($null-ne$preFill-and[int]$preFill.panelIndex-ne 0){
                throw "The test-only Pre-fill action is not on the first guided screen at $formName/$($viewport.name)."
            }
            $visibleJourneyButtons=@($result.layout.guidedJourney.actionRows|
                Where-Object visible|
                ForEach-Object{$_.buttons}|
                Where-Object visible)
            $misaligned=@($visibleJourneyButtons|Where-Object{
                $secondary=([string]$_.name-match '^btnJourneyBack' -or [string]$_.name-eq'btnPreFill')
                if($secondary){[string]$_.cellTextAlign-ne'left'}else{[string]$_.cellTextAlign-ne'right'}
            })
            if($misaligned.Count-gt 0){
                throw "Guided action '$($misaligned[0].name)' is not aligned to its Northstar action edge at $formName/$($viewport.name)."
            }
        }
        if(-not[string]::IsNullOrWhiteSpace($validationClick)){
            $probe=$result.layout.clickProbe
            $feedback=$result.layout.validationFeedback
            $expected=if($ExpectedInvalidCounts.ContainsKey($formName)){
                [int]$ExpectedInvalidCounts[$formName]
            }else{0}
            if($null-eq$probe-or-not[bool]$probe.found){
                throw "Validation action '$validationClick' was not found at $formName/$($viewport.name)."
            }
            if([int]$feedback.invalidCount-lt 1-or
                ($expected-gt 0-and[int]$feedback.invalidCount-ne$expected)){
                throw "Validation action '$validationClick' produced $($feedback.invalidCount) invalid controls at $formName/$($viewport.name); expected $(if($expected-gt 0){$expected}else{'at least one'})."
            }
            if([int]$feedback.visibleTreatmentCount-ne[int]$feedback.invalidCount){
                throw "Only $($feedback.visibleTreatmentCount) of $($feedback.invalidCount) invalid controls have visible error treatment at $formName/$($viewport.name)."
            }
            if([int]$feedback.ariaInvalidCount-ne[int]$feedback.invalidCount-or
                -not[bool]$feedback.summaryVisible-or
                [int]$feedback.summaryInvalidCount-ne[int]$feedback.invalidCount){
                throw "Accessible validation feedback is incomplete at $formName/$($viewport.name)."
            }
            if(-not[bool]$feedback.firstInvalidFocused){
                throw "Validation did not return focus to the first invalid control at $formName/$($viewport.name)."
            }
            if($null-ne$probe.selectedTabBefore-and
                [int]$probe.selectedTabBefore-ne[int]$feedback.selectedTabAfter){
                throw "Validation did not block navigation at $formName/$($viewport.name)."
            }
            if($null-eq$feedback.compatibility){
                throw "The K2 Runtime validation compatibility probe did not report state at $formName/$($viewport.name)."
            }
        }
        if([int]$viewport.width -le 480 -and @($result.layout.kpiCells).Count -gt 0){
            $rows=@($result.layout.kpiCells|ForEach-Object{[int]$_.gridRow}|Sort-Object)
            if($rows.Count-ne 8-or($rows-join ',')-ne '1,2,3,4,5,6,7,8'){
                throw "Northstar KPI label/value cells are not paired into deterministic mobile rows at $formName/$($viewport.name)."
            }
        }
        $unexpected=@($result.diagnostics|Where-Object{
            $_.text -notmatch "^TypeError: Cannot read properties of null \(reading '0'\)" -and
            $_.text -notmatch '^net::ERR_ABORTED$'
        })
        if($unexpected.Count-gt 0){throw "Unexpected browser diagnostic at $formName/$($viewport.name): $($unexpected[0].text)"}
        $results.Add([ordered]@{form=$formName;viewport=$viewport.name;width=$viewport.width;height=$viewport.height;image=[IO.Path]::GetFileName($image);url=$result.layout.url;title=$result.layout.title;textLength=$result.layout.textLength;shellCount=$result.layout.shellCount;northstarReady=$result.layout.northstarReady;contentReady=$result.layout.contentReady;guidedJourney=($guidedControls.Count-gt 0);forwardTabBypassBlocked=$(if($guidedControls.Count-gt 0){[bool]$result.layout.guidedJourney.directAdvanceProbe.blocked}else{$null});validationClick=$(if([string]::IsNullOrWhiteSpace($validationClick)){$null}else{$validationClick});invalidCount=$(if([string]::IsNullOrWhiteSpace($validationClick)){$null}else{[int]$result.layout.validationFeedback.invalidCount});allInvalidVisiblyTreated=$(if([string]::IsNullOrWhiteSpace($validationClick)){$null}else{[int]$result.layout.validationFeedback.visibleTreatmentCount-eq[int]$result.layout.validationFeedback.invalidCount});summaryVisible=$(if([string]::IsNullOrWhiteSpace($validationClick)){$null}else{[bool]$result.layout.validationFeedback.summaryVisible});firstInvalidFocused=$(if([string]::IsNullOrWhiteSpace($validationClick)){$null}else{[bool]$result.layout.validationFeedback.firstInvalidFocused});horizontalOverflow=$false;knownK2Diagnostics=@($result.diagnostics|ForEach-Object{$_.text})})
        Write-Output "Captured native Runtime UX: $formName / $($viewport.name)"
    }
}
$report=[ordered]@{capturedUtc=[DateTime]::UtcNow.ToString('o');runtimeBaseUrl=$RuntimeBaseUrl;forms=@($FormNames);viewports=$viewports;captures=@($results);knownDiagnosticPolicy="K2 5.10 can emit a non-blocking DataLabel setValue null-index diagnostic and Edge can report canceled resource requests as net::ERR_ABORTED; actionable JavaScript exceptions, log errors, and other network failures fail evidence capture."}
$reportPath=Join-Path $output 'runtime-ux-evidence.json';$report|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $reportPath -Encoding utf8
Write-Output "Native Runtime UX evidence: $reportPath"
