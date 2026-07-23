[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Ux,[Parameter(Mandatory=$true)][string]$Mapping,[Parameter(Mandatory=$true)][string]$Output,[string]$BaseManifest)
$ErrorActionPreference='Stop'

function Get-ValueOrDefault {
    param($Value, $Default)
    if ($null -eq $Value -or ([string]$Value).Length -eq 0) { return $Default }
    return $Value
}

function ConvertTo-ControlName {
    param([Parameter(Mandatory=$true)][string]$Prefix,[Parameter(Mandatory=$true)][string]$Value)
    $words = @($Value -split '[^A-Za-z0-9]+' | Where-Object { $_ })
    return $Prefix + (($words | ForEach-Object {
        if ($_.Length -eq 1) { $_.ToUpperInvariant() }
        else { $_.Substring(0,1).ToUpperInvariant() + $_.Substring(1) }
    }) -join '')
}

$uxDocument=Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $Ux)|ConvertFrom-Json
$mappingDocument=Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $Mapping)|ConvertFrom-Json
$components=@{};foreach($c in @($uxDocument.components)){$components[[string]$c.id]=$c}
$dashboardPageId=[string]$mappingDocument.dashboard.page;$page=$uxDocument.pages|Where-Object {[string]$_.id -eq $dashboardPageId}|Select-Object -First 1;if($null -eq $page){throw "Dashboard page '$dashboardPageId' was not found."}
$views=[Collections.Generic.List[object]]::new();$formViews=[Collections.Generic.List[string]]::new();$titles=[ordered]@{}
$summary=$mappingDocument.dashboard.summary;$cards=[Collections.Generic.List[object]]::new();$props=[Collections.Generic.List[string]]::new()
foreach($binding in @($summary.components)){if(-not $components.ContainsKey([string]$binding.id)){throw "Unknown UX component mapping: $($binding.id)"};$c=$components[[string]$binding.id];$props.Add([string]$binding.property);$cards.Add([ordered]@{property=$binding.property;label=(Get-ValueOrDefault $binding.label $c.id);tone=(Get-ValueOrDefault $binding.tone 'neutral');explanation=(Get-ValueOrDefault $c.explanation '')})}
$views.Add([ordered]@{name=$summary.viewName;smartObject=$summary.smartObject;type='capture';properties=@($props);methods=@();defaultListMethod='List';options=@();metricCards=@($cards)});$formViews.Add($summary.viewName);$titles[$summary.viewName]='Operational position'
foreach($binding in @($mappingDocument.dashboard.charts)){if(-not $components.ContainsKey([string]$binding.component)){throw "Unknown UX chart mapping: $($binding.component)"};$c=$components[[string]$binding.component];$chartType=if($binding.type){$binding.type}elseif($c.chart_type -eq 'horizontal-bar'){'bar'}else{$c.chart_type};$chart=[ordered]@{name=(ConvertTo-ControlName 'cht' ([string]$binding.component));title=(Get-ValueOrDefault $binding.title $binding.component);type=$chartType;categoryProperty=$binding.categoryProperty;valueProperty=$binding.valueProperty;height=(Get-ValueOrDefault $binding.height 260);showLegend=[bool](Get-ValueOrDefault $binding.showLegend $false);showLabels=[bool](Get-ValueOrDefault $binding.showLabels $true);emptyState=(Get-ValueOrDefault $c.empty_state 'No data to display.')};$dataViewName=Get-ValueOrDefault $binding.tableViewName ([string]$binding.viewName+' Data');$views.Add([ordered]@{name=$binding.viewName;smartObject=$binding.smartObject;type='capture';properties=@($binding.categoryProperty,$binding.valueProperty);methods=@();defaultListMethod='List';options=@();charts=@($chart)});$views.Add([ordered]@{name=$dataViewName;smartObject=$binding.smartObject;type='list';properties=@($binding.categoryProperty,$binding.valueProperty);methods=@();defaultListMethod='List';options=@('toolbar')});$formViews.Add($binding.viewName);$formViews.Add($dataViewName);$titles[$binding.viewName]=$chart.title;$titles[$dataViewName]=($chart.title+' data')}
foreach($binding in @($mappingDocument.dashboard.queues)){if(-not $components.ContainsKey([string]$binding.component)){throw "Unknown UX queue mapping: $($binding.component)"};$views.Add([ordered]@{name=$binding.viewName;smartObject=$binding.smartObject;type='list';properties=@($binding.properties);methods=@();defaultListMethod='List';options=@('toolbar')});$formViews.Add($binding.viewName);$titles[$binding.viewName]=(Get-ValueOrDefault $binding.title $binding.component)}
$dashboardForm=[ordered]@{name=$mappingDocument.dashboard.formName;useLegacyTheme=$false;views=@($formViews);options=@('no-tabs');viewTitles=$titles}
$generatedForms=[Collections.Generic.List[object]]::new();$generatedForms.Add($dashboardForm);$reportViewNames=@()
if($null -ne $mappingDocument.reports){
    $reports=$mappingDocument.reports;$reportTitles=[ordered]@{};$reportViewsByGroup=[ordered]@{}
    foreach($binding in @($reports.views)){
        $chart=[ordered]@{name=(ConvertTo-ControlName 'cht' ([string]$binding.viewName));title=[string]$binding.title;type=(Get-ValueOrDefault $binding.type 'bar');categoryProperty=$binding.categoryProperty;valueProperty=$binding.valueProperty;height=(Get-ValueOrDefault $binding.height 250);showLegend=[bool](Get-ValueOrDefault $binding.showLegend $false);showLabels=[bool](Get-ValueOrDefault $binding.showLabels $true);emptyState=(Get-ValueOrDefault $binding.emptyState 'No report data to display.')}
        $dataViewName=Get-ValueOrDefault $binding.tableViewName ([string]$binding.viewName+' Data');$reportView=[ordered]@{name=$binding.viewName;smartObject=$binding.smartObject;type='capture';properties=@($binding.categoryProperty,$binding.valueProperty);methods=@();defaultListMethod='List';options=@();charts=@($chart)};$reportDataView=[ordered]@{name=$dataViewName;smartObject=$binding.smartObject;type='list';properties=@($binding.categoryProperty,$binding.valueProperty);methods=@();defaultListMethod='List';options=@('toolbar')}
        $views.Add($reportView);$views.Add($reportDataView);$reportViewNames+=([string]$binding.viewName);$reportViewNames+=([string]$dataViewName);$reportTitles[[string]$binding.viewName]=[string]$binding.title;$reportTitles[[string]$dataViewName]=([string]$binding.title+' data');$group=Get-ValueOrDefault $binding.group 'Reports'
        if(-not $reportViewsByGroup.Contains($group)){$reportViewsByGroup[$group]=[Collections.Generic.List[string]]::new()};$reportViewsByGroup[$group].Add([string]$binding.viewName);$reportViewsByGroup[$group].Add([string]$dataViewName)
    }
    $reportTabs=@($reportViewsByGroup.GetEnumerator()|ForEach-Object {[ordered]@{name=[string]$_.Key;views=@($_.Value)}})
    $generatedForms.Add([ordered]@{name=$reports.formName;useLegacyTheme=$false;views=@($reportViewNames);viewTitles=$reportTitles;tabs=$reportTabs})
}
if($null -ne $mappingDocument.myWork){
    $myWork=$mappingDocument.myWork;$myWorkViews=[Collections.Generic.List[string]]::new();$myWorkTitles=[ordered]@{};$myWorkTabs=[Collections.Generic.List[object]]::new()
    $worklist=[ordered]@{rows=[int](Get-ValueOrDefault $myWork.worklist.rows 20);refreshIntervalSeconds=[int](Get-ValueOrDefault $myWork.worklist.refreshIntervalSeconds 120);showToolbar=[bool](Get-ValueOrDefault $myWork.worklist.showToolbar $true);showFilter=[bool](Get-ValueOrDefault $myWork.worklist.showFilter $true);showSearch=[bool](Get-ValueOrDefault $myWork.worklist.showSearch $false);enableSearch=[bool](Get-ValueOrDefault $myWork.worklist.enableSearch $true);height=(Get-ValueOrDefault $myWork.worklist.height '445px');openTaskInNewWindow=[bool](Get-ValueOrDefault $myWork.worklist.openTaskInNewWindow $true);actions=@(Get-ValueOrDefault $myWork.worklist.actions @('viewWorkflow','sleep','redirect','release','share'))}
    $myWorkTabs.Add([ordered]@{name=(Get-ValueOrDefault $myWork.worklistTab 'My Tasks');worklist=$worklist})
    foreach($queue in @($myWork.queues)){
        $viewName=[string]$queue.viewName
        if(@($views|Where-Object {[string]$_.name -eq $viewName}).Count -eq 0){throw "My Work queue references unknown generated View: $viewName"}
        if(-not $myWorkViews.Contains($viewName)){$myWorkViews.Add($viewName)}
        $myWorkTitles[$viewName]=Get-ValueOrDefault $queue.title ($viewName -replace '^[^.]+\.','')
        $myWorkTabs.Add([ordered]@{name=(Get-ValueOrDefault $queue.tab $myWorkTitles[$viewName]);views=@($viewName)})
    }
    $generatedForms.Add([ordered]@{name=$myWork.formName;useLegacyTheme=$false;views=@($myWorkViews);viewTitles=$myWorkTitles;tabs=@($myWorkTabs)})
}
if ($BaseManifest) {
    $manifest=Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $BaseManifest)|ConvertFrom-Json
    $existingViewNames=@($manifest.application.views|ForEach-Object {[string]$_.name})
    $existingFormNames=@($manifest.application.forms|ForEach-Object {[string]$_.name})
    foreach($generatedView in @($views)){if($existingViewNames -contains [string]$generatedView.name){throw "Generated dashboard View collides with base manifest View: $($generatedView.name)"}}
    foreach($generatedForm in @($generatedForms)){if($existingFormNames -contains [string]$generatedForm.name){throw "Generated reusable UX Form collides with base manifest Form: $($generatedForm.name)"}}
    # Preserve the base solution identity because the selected common framework maps it into every Form subtitle.
    $manifest.application.views=@($manifest.application.views)+@($views)
    $manifest.application.forms=@($manifest.application.forms)+@($generatedForms)
    if($null -ne $mappingDocument.workspace){
        $workspace=$mappingDocument.workspace
        if(-not $components.ContainsKey([string]$workspace.lifecycleComponent)){throw "Unknown UX lifecycle mapping: $($workspace.lifecycleComponent)"}
        $workspaceView=@($manifest.application.views|Where-Object {[string]$_.name -eq [string]$workspace.viewName})|Select-Object -First 1
        if($null -eq $workspaceView){throw "Workspace View '$($workspace.viewName)' was not found in the base manifest."}
        $tracker=[ordered]@{name=(Get-ValueOrDefault $workspace.title 'Case Lifecycle');property=$workspace.currentStageProperty;stages=@($workspace.stages)}
        $workspaceView|Add-Member -NotePropertyName lifecycleTrackers -NotePropertyValue @($tracker) -Force
        if(-not [string]::IsNullOrWhiteSpace([string]$workspace.shellFormName)){
            $shellForm=@($manifest.application.forms|Where-Object {[string]$_.name -eq [string]$workspace.shellFormName})|Select-Object -First 1
            if($null -eq $shellForm){throw "Workspace shell Form '$($workspace.shellFormName)' was not found in the base manifest."}
            if($null -eq $shellForm.tabs -or @($shellForm.tabs).Count -eq 0){throw "Workspace shell Form '$($workspace.shellFormName)' must use tabs before reusable navigation can be composed."}
            if($null -ne $workspace.sectionTabs -and @($workspace.sectionTabs).Count -gt 0){
                $workspaceTabName=Get-ValueOrDefault $workspace.workspaceTab 'Case Workspace'
                $existingWorkspaceTab=@($shellForm.tabs|Where-Object {[string]$_.name -eq [string]$workspaceTabName})|Select-Object -First 1
                if($null -eq $existingWorkspaceTab){throw "Workspace shell Form '$($workspace.shellFormName)' has no mapped workspace tab '$workspaceTabName'."}
                $mappedViews=@($workspace.sectionTabs|ForEach-Object {@($_.views)});$existingViews=@($existingWorkspaceTab.views)
                if($mappedViews.Count -ne $existingViews.Count -or @($mappedViews|Where-Object {$existingViews -notcontains $_}).Count -gt 0 -or @($mappedViews|Select-Object -Unique).Count -ne $mappedViews.Count){throw "workspace.sectionTabs must place every View from '$workspaceTabName' exactly once."}
                $sectionNames=@($workspace.sectionTabs|ForEach-Object {[string]$_.name});if(@($sectionNames|Select-Object -Unique).Count -ne $sectionNames.Count){throw 'workspace.sectionTabs names must be unique.'}
                $regrouped=[Collections.Generic.List[object]]::new()
                foreach($tab in @($shellForm.tabs)){if([string]$tab.name -eq [string]$workspaceTabName){foreach($section in @($workspace.sectionTabs)){$regrouped.Add([ordered]@{name=[string]$section.name;views=@($section.views)})}}else{$regrouped.Add($tab)}}
                $shellForm.tabs=@($regrouped);$firstSection=[string]$workspace.sectionTabs[0].name
                foreach($navigation in @($shellForm.listClickTabNavigation)){if([string]$navigation.targetTab -eq [string]$workspaceTabName){$navigation.targetTab=$firstSection}}
            }
            $analyticsTabName=Get-ValueOrDefault $workspace.analyticsTab 'Analytics'
            if(@($shellForm.tabs|Where-Object {[string]$_.name -eq [string]$analyticsTabName}).Count -gt 0){throw "Workspace shell Form already contains tab '$analyticsTabName'."}
            $shellForm.views=@($shellForm.views)+@($formViews)
            $analyticsTab=[ordered]@{name=$analyticsTabName;views=@($formViews)}
            $tabs=[Collections.Generic.List[object]]::new();$inserted=$false
            foreach($tab in @($shellForm.tabs)){
                if(-not $inserted -and $null -ne $tab.worklist){$tabs.Add($analyticsTab);$inserted=$true}
                $tabs.Add($tab)
            }
            if(-not $inserted){$tabs.Add($analyticsTab)}
            $shellForm.tabs=@($tabs)
            if($null -eq $shellForm.viewTitles){$shellForm|Add-Member -NotePropertyName viewTitles -NotePropertyValue ([pscustomobject]@{}) -Force}
            foreach($entry in $titles.GetEnumerator()){$shellForm.viewTitles|Add-Member -NotePropertyName ([string]$entry.Key) -NotePropertyValue ([string]$entry.Value) -Force}
            if($null -ne $mappingDocument.reports -and -not [string]::IsNullOrWhiteSpace([string]$mappingDocument.reports.shellTab)){
                $reportTabName=[string]$mappingDocument.reports.shellTab;if(@($shellForm.tabs|Where-Object {[string]$_.name -eq $reportTabName}).Count){throw "Workspace shell Form already contains report tab '$reportTabName'."}
                $shellForm.views=@($shellForm.views)+@($reportViewNames);$reportTab=[ordered]@{name=$reportTabName;views=@($reportViewNames)};$reportTabs=[Collections.Generic.List[object]]::new();$reportInserted=$false
                foreach($tab in @($shellForm.tabs)){if(-not $reportInserted -and $null -ne $tab.worklist){$reportTabs.Add($reportTab);$reportInserted=$true};$reportTabs.Add($tab)};if(-not $reportInserted){$reportTabs.Add($reportTab)};$shellForm.tabs=@($reportTabs)
                foreach($entry in $reportTitles.GetEnumerator()){$shellForm.viewTitles|Add-Member -NotePropertyName ([string]$entry.Key) -NotePropertyValue ([string]$entry.Value) -Force}
            }
        }
    }
    if($null -ne $mappingDocument.initiation){
        $init=$mappingDocument.initiation
        $journey=@($uxDocument.journeys|Where-Object {[string]$_.id -eq [string]$init.journey})|Select-Object -First 1
        if($null -eq $journey){throw "Initiation journey '$($init.journey)' was not found in the composed UX."}
        $effectiveMasterView=[string]$init.masterView
        if(-not [string]::IsNullOrWhiteSpace([string]$init.captureViewName)){
            if($null -eq $init.entryProperties -or @($init.entryProperties).Count -eq 0){throw 'initiation.entryProperties is required when initiation.captureViewName is set.'}
            $sourceMaster=@($manifest.application.views|Where-Object {[string]$_.name -eq [string]$init.masterView})|Select-Object -First 1
            if($null -eq $sourceMaster){throw "Initiation source master View '$($init.masterView)' was not found in the base manifest."}
            if(@($manifest.application.views|Where-Object {[string]$_.name -eq [string]$init.captureViewName}).Count -gt 0){throw "Initiation capture View collides with an existing View: $($init.captureViewName)"}
            $unknownEntry=@($init.entryProperties|Where-Object {@($sourceMaster.properties) -notcontains [string]$_});if($unknownEntry.Count -gt 0){throw "Initiation entryProperties references properties not selected on '$($init.masterView)': $($unknownEntry -join ', ')"}
            $entryView=($sourceMaster|ConvertTo-Json -Depth 100|ConvertFrom-Json);$entryView.name=[string]$init.captureViewName
            $entryView|Add-Member -NotePropertyName hiddenProperties -NotePropertyValue @($sourceMaster.properties|Where-Object {@($init.entryProperties) -notcontains [string]$_}) -Force
            if($null -ne $init.propertyLabels){$entryView|Add-Member -NotePropertyName propertyLabels -NotePropertyValue $init.propertyLabels -Force}
            $entryView|Add-Member -NotePropertyName lifecycleTrackers -NotePropertyValue @() -Force
            $entryView.options=@($entryView.options|Where-Object {[string]$_ -ne 'toolbar'})
            $manifest.application.views=@($manifest.application.views)+@($entryView);$effectiveMasterView=[string]$init.captureViewName
        }
        $reviewReadMethod=Get-ValueOrDefault $init.readMethod 'Read'
        $reviewView=[ordered]@{name=$init.reviewViewName;smartObject=$init.smartObject;type='capture';properties=@($init.reviewProperties);readOnlyProperties=@($init.reviewProperties);methods=@($reviewReadMethod);layoutColumns=2;options=@('labels-left','colon-labels')}
        if(@($manifest.application.views|Where-Object {[string]$_.name -eq [string]$reviewView.name}).Count -gt 0){throw "Initiation review View collides with an existing View: $($reviewView.name)"}
        $manifest.application.views=@($manifest.application.views)+@($reviewView)
        $initViews=@($effectiveMasterView)+@($init.details|ForEach-Object {$_.view})+@($init.reviewViewName)
        foreach($requiredView in $initViews){if(@($manifest.application.views|Where-Object {[string]$_.name -eq [string]$requiredView}).Count -eq 0){throw "Initiation references unknown View: $requiredView"}}
        $detailContracts=@($init.details|ForEach-Object {[ordered]@{view=$_.view;foreignKeyProperty=$_.foreignKeyProperty;createMethod=(Get-ValueOrDefault $_.createMethod 'Create');updateMethod=(Get-ValueOrDefault $_.updateMethod 'Update');deleteMethod=(Get-ValueOrDefault $_.deleteMethod 'Delete');listMethod=(Get-ValueOrDefault $_.listMethod 'List')}})
        $initForm=[ordered]@{name=$init.formName;useLegacyTheme=$false;views=$initViews;behaviors=@('refresh-list-form-submit','refresh-list-form-load');viewTitles=[ordered]@{};tabs=@([ordered]@{name=(Get-ValueOrDefault $init.detailsTab 'Case Details');views=@($effectiveMasterView)+@($init.details|Where-Object {$_.step -eq 'details'}|ForEach-Object {$_.view})},[ordered]@{name=(Get-ValueOrDefault $init.evidenceTab 'Evidence');views=@($init.details|Where-Object {$_.step -eq 'evidence'}|ForEach-Object {$_.view})},[ordered]@{name=(Get-ValueOrDefault $init.reviewTab 'Review & Submit');views=@($init.reviewViewName)});listClickTabNavigation=@();masterDetail=[ordered]@{masterView=$effectiveMasterView;masterKeyProperty=$init.masterKeyProperty;masterCreateMethod=(Get-ValueOrDefault $init.createMethod 'Create');masterUpdateMethod=(Get-ValueOrDefault $init.updateMethod 'Update');masterReadMethod=(Get-ValueOrDefault $init.readMethod 'Read');saveButtonText=(Get-ValueOrDefault $init.saveButtonText 'Save draft and review');successMessageTitle=(Get-ValueOrDefault $init.successTitle 'Draft saved');successMessageBody=(Get-ValueOrDefault $init.successBody 'Your draft was saved and is ready to review.');details=$detailContracts;review=[ordered]@{view=$init.reviewViewName;keyProperty=$init.masterKeyProperty;readMethod=(Get-ValueOrDefault $init.readMethod 'Read');tab=(Get-ValueOrDefault $init.reviewTab 'Review & Submit')}};workflowStartButton=[ordered]@{name=(Get-ValueOrDefault $init.submitButtonName 'btnSubmitCase');text=(Get-ValueOrDefault $init.submitButtonText 'Submit case');tab=(Get-ValueOrDefault $init.reviewTab 'Review & Submit')}}
        foreach($viewName in $initViews){$initForm.viewTitles[$viewName]=if($viewName -eq $init.reviewViewName){'Review the case before submission'}elseif($viewName -eq $effectiveMasterView){'Case details'}else{($viewName -replace '^[^.]+\.','')}}
        $manifest.application.forms=@($manifest.application.forms)+@($initForm)
    }
    if($null -eq $manifest.verification){$manifest|Add-Member -NotePropertyName verification -NotePropertyValue ([pscustomobject]@{})}
    $manifest.verification|Add-Member -NotePropertyName expectedViews -NotePropertyValue @($manifest.application.views|ForEach-Object {$_.name}) -Force
    $manifest.verification|Add-Member -NotePropertyName expectedForms -NotePropertyValue @($manifest.application.forms|ForEach-Object {$_.name}) -Force
    $manifest.verification|Add-Member -NotePropertyName smokeTestRuntime -NotePropertyValue $true -Force
    $manifest.verification|Add-Member -NotePropertyName runtimeBaseUrl -NotePropertyValue ([string]$mappingDocument.runtimeBaseUrl) -Force
} else {
    $manifest=[ordered]@{name=$mappingDocument.application.name;k2=[ordered]@{host='localhost';port=5555;integrated=$true;securityLabel='K2'};application=[ordered]@{rootCategoryPath=$mappingDocument.application.rootCategoryPath;theme=$mappingDocument.application.theme;styleProfile=$mappingDocument.application.styleProfile;solutionCode=$mappingDocument.application.solutionCode;replaceExisting=$true;checkIn=$true;views=@($views);forms=@($generatedForms)};verification=[ordered]@{expectedViews=@($views|ForEach-Object {$_.name});expectedForms=@($generatedForms|ForEach-Object {$_.name});smokeTestRuntime=$true;runtimeBaseUrl=$mappingDocument.runtimeBaseUrl}}
}
$destination=[IO.Path]::GetFullPath($Output);$parent=Split-Path -Parent $destination;if($parent -and -not(Test-Path $parent)){New-Item -ItemType Directory -Path $parent|Out-Null};$manifest|ConvertTo-Json -Depth 100|Set-Content -Encoding utf8 -LiteralPath $destination;"Compiled SmartForms dashboard manifest: $destination";exit 0
