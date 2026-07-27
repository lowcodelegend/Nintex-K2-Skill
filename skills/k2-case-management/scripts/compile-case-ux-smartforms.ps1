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

function Test-IncludeDataAlternative {
    param($Binding)
    $property=$Binding.PSObject.Properties['includeDataAlternative']
    if($null -eq $property){return $false}
    if($Binding.includeDataAlternative -isnot [bool]){throw "includeDataAlternative must be a JSON boolean on mapping '$($Binding.viewName)'."}
    return $Binding.includeDataAlternative
}

function Test-UseGuidedJourney {
    param($Initiation, $Journey)
    $mode=[string](Get-ValueOrDefault $Initiation.guidedMode 'auto')
    if($mode -notin @('auto','always','never')){throw "initiation.guidedMode must be auto, always, or never; found '$mode'."}
    if($mode -eq 'always'){return $true}
    if($mode -eq 'never'){return $false}
    $steps=@($Journey.steps)
    $fieldCount=0
    $hasCollection=$false
    $hasReview=$false
    foreach($step in $steps){
        $fieldCount+=@($step.fields).Count
        if(-not [string]::IsNullOrWhiteSpace([string]$step.collection)){$hasCollection=$true}
        if([bool]$step.summary){$hasReview=$true}
    }
    $resumable=[bool]$Journey.autosave -or [bool]$Journey.resume_draft
    $reviewed=[bool]$Journey.review_before_submit -or $hasReview
    return $steps.Count -ge 3 -and ($fieldCount -gt 8 -or $hasCollection -or $resumable -or $reviewed)
}

function Resolve-InitiationStepViews {
    param($Step, [string]$MasterView, [string]$ReviewView)
    $resolved=@($Step.views|ForEach-Object {
        if([string]$_ -eq '$master'){$MasterView}
        elseif([string]$_ -eq '$review'){$ReviewView}
        else{[string]$_}
    })
    if($resolved.Count -eq 0){throw "initiation.stepTabs '$($Step.id)' must place at least one View."}
    return $resolved
}

function Set-ObjectProperty {
    param([Parameter(Mandatory=$true)]$Object,[Parameter(Mandatory=$true)][string]$Name,$Value)
    $Object|Add-Member -NotePropertyName $Name -NotePropertyValue $Value -Force
}

function Add-AgenticChat {
    param([Parameter(Mandatory=$true)]$Manifest,[Parameter(Mandatory=$true)]$MappingDocument)
    $chat=$MappingDocument.agenticChat
    if($null -eq $chat){return}
    $enabledProperty=$chat.PSObject.Properties['enabled']
    if($null -eq $enabledProperty -or $chat.enabled -isnot [bool]){throw 'agenticChat.enabled must be a JSON boolean.'}
    if(-not $chat.enabled){return}

    $allowedKeys=@('enabled','integration','viewName','sourcePaletteViewName','controlName','controlType','controlPackage','hostUrl','flowId','scriptUrl','windowTitle','label','description','chatPosition','width','height','authentication','placement')
    $unknownKeys=@($chat.PSObject.Properties.Name|Where-Object {$allowedKeys -notcontains [string]$_})
    if($unknownKeys.Count -gt 0){throw "agenticChat contains unsupported or potentially secret-bearing properties: $($unknownKeys -join ', '). Browser API keys, headers, tokens, tweaks, and secrets are forbidden."}
    if([string]$chat.integration -ne 'command-palette'){throw "agenticChat.integration must be 'command-palette'."}
    if([string]$chat.controlPackage -ne 'assets/northstar-command-palette'){throw "agenticChat.controlPackage must be 'assets/northstar-command-palette'."}
    $assistantControlType=[string](Get-ValueOrDefault $chat.controlType 'northstar-command-palette')
    if($assistantControlType -notmatch '^northstar-[a-z0-9]+(?:-[a-z0-9]+)+$'){
        throw 'agenticChat.controlType must be a stable northstar-* custom-element tag.'
    }
    foreach($required in @('viewName','hostUrl','flowId','scriptUrl','windowTitle','label','description')){
        if([string]::IsNullOrWhiteSpace([string]$chat.$required)){throw "agenticChat.$required is required when agentic chat is enabled."}
    }
    $approvedScript='https://cdn.jsdelivr.net/gh/langflow-ai/langflow-embedded-chat@v1.0.8/dist/build/static/js/bundle.min.js'
    if([string]$chat.scriptUrl -cne $approvedScript){throw "agenticChat.scriptUrl must use the approved pinned Langflow v1.0.8 bundle: $approvedScript"}
    $hostUri=$null
    if(-not [Uri]::TryCreate([string]$chat.hostUrl,[UriKind]::Absolute,[ref]$hostUri) -or $hostUri.Scheme -ne 'https' -or -not [string]::IsNullOrEmpty($hostUri.Query) -or -not [string]::IsNullOrEmpty($hostUri.Fragment) -or ([string]$chat.hostUrl).EndsWith('/')){
        throw 'agenticChat.hostUrl must be an absolute HTTPS origin with no query, fragment, or trailing slash.'
    }
    $flowGuid=[guid]::Empty
    if(-not [guid]::TryParse([string]$chat.flowId,[ref]$flowGuid)){throw 'agenticChat.flowId must be a GUID.'}
    if($null -eq $chat.authentication){throw 'agenticChat.authentication.mode is required when agentic chat is enabled.'}
    $authenticationKeys=@($chat.authentication.PSObject.Properties.Name)
    if($authenticationKeys.Count -ne 1 -or $authenticationKeys[0] -ne 'mode'){
        throw 'agenticChat.authentication supports only mode. Browser API keys, headers, tokens, and secret references are forbidden.'
    }
    $authenticationMode=[string]$chat.authentication.mode
    if($authenticationMode -notin @('server-open-alpha','server-proxy')){
        throw "agenticChat.authentication.mode must be 'server-open-alpha' or 'server-proxy'."
    }
    $positions=@('top-left','top-center','top-right','center-left','center-right','bottom-right','bottom-center','bottom-left')
    $position=[string](Get-ValueOrDefault $chat.chatPosition 'bottom-right')
    if($positions -notcontains $position){throw "agenticChat.chatPosition must be one of: $($positions -join ', ')."}
    $chatWidth=[int](Get-ValueOrDefault $chat.width 420)
    $chatHeight=[int](Get-ValueOrDefault $chat.height 640)
    if($chatWidth -lt 320 -or $chatWidth -gt 1200){throw 'agenticChat.width must be between 320 and 1200 pixels.'}
    if($chatHeight -lt 420 -or $chatHeight -gt 1200){throw 'agenticChat.height must be between 420 and 1200 pixels.'}
    if($null -eq $chat.placement -or [string]::IsNullOrWhiteSpace([string]$chat.placement.formName) -or [string]::IsNullOrWhiteSpace([string]$chat.placement.tab)){
        throw 'agenticChat.placement.formName and agenticChat.placement.tab are required.'
    }

    $sourceViewName=[string](Get-ValueOrDefault $chat.sourcePaletteViewName $MappingDocument.homepage.commandPalette.viewName)
    $sourceView=@($Manifest.application.views|Where-Object {[string]$_.name -eq $sourceViewName})|Select-Object -First 1
    if($null -eq $sourceView){throw "agenticChat source command-palette View '$sourceViewName' was not found."}
    $sourceControl=@($sourceView.webComponents|Where-Object {[string]$_.controlType -eq 'northstar-command-palette'})|Select-Object -First 1
    if($null -eq $sourceControl){throw "agenticChat source View '$sourceViewName' does not host northstar-command-palette."}
    $existingAssistantViews=@($Manifest.application.views|Where-Object {[string]$_.name -eq [string]$chat.viewName})
    if($existingAssistantViews.Count -gt 1){throw "agenticChat.viewName is duplicated in the base manifest: $($chat.viewName)"}
    if($existingAssistantViews.Count -eq 1){
        $Manifest.application.views=@($Manifest.application.views|Where-Object {[string]$_.name -ne [string]$chat.viewName})
    }
    $paletteViewTitle=[string]$MappingDocument.homepage.commandPalette.viewTitle
    if([string]::IsNullOrWhiteSpace($paletteViewTitle)){
        throw 'homepage.commandPalette.viewTitle is required as the shared Northstar shell/compiler contract.'
    }

    $assistantView=$sourceView|ConvertTo-Json -Depth 100|ConvertFrom-Json
    $assistantView.name=[string]$chat.viewName
    $assistantControl=@($assistantView.webComponents|Where-Object {[string]$_.controlType -eq 'northstar-command-palette'})|Select-Object -First 1
    $assistantControl.name=[string](Get-ValueOrDefault $chat.controlName 'Northstar Case Assistant')
    $assistantControl.controlType=$assistantControlType
    Set-ObjectProperty $assistantControl.properties 'TagName' $assistantControlType
    foreach($property in ([ordered]@{
        AssistantEnabled=$true
        AssistantLabel=[string]$chat.label
        AssistantDescription=[string]$chat.description
        LangflowHostUrl=[string]$chat.hostUrl
        LangflowFlowId=[string]$flowGuid
        LangflowScriptUrl=$approvedScript
        LangflowAuthenticationMode=$authenticationMode
        LangflowWindowTitle=[string]$chat.windowTitle
        LangflowChatPosition=$position
        LangflowWidth=$chatWidth
        LangflowHeight=$chatHeight
    }).GetEnumerator()){Set-ObjectProperty $assistantControl.properties ([string]$property.Key) $property.Value}
    $Manifest.application.views=@($Manifest.application.views)+@($assistantView)

    $targetForm=@($Manifest.application.forms|Where-Object {[string]$_.name -eq [string]$chat.placement.formName})|Select-Object -First 1
    if($null -eq $targetForm){throw "agenticChat target Form '$($chat.placement.formName)' was not found."}
    if($null -eq $targetForm.tabs -or @($targetForm.tabs).Count -eq 0){throw "agenticChat target Form '$($chat.placement.formName)' must use tabs."}
    $targetTab=@($targetForm.tabs|Where-Object {[string]$_.name -eq [string]$chat.placement.tab})|Select-Object -First 1
    if($null -eq $targetTab){throw "agenticChat target Form '$($chat.placement.formName)' has no tab '$($chat.placement.tab)'."}

    # A K2 View has one placement per Form. Replace any plain palette placement in this Form
    # with the assistant-enabled clone and place it first on the chosen case-context tab.
    $targetForm.views=@($targetForm.views|Where-Object {[string]$_ -ne $sourceViewName -and [string]$_ -ne [string]$chat.viewName})+@([string]$chat.viewName)
    foreach($tab in @($targetForm.tabs)){
        if($null -ne $tab.views){$tab.views=@($tab.views|Where-Object {[string]$_ -ne $sourceViewName -and [string]$_ -ne [string]$chat.viewName})}
    }
    $targetTab.views=@([string]$chat.viewName)+@($targetTab.views)
    if($null -eq $targetForm.viewTitles){Set-ObjectProperty $targetForm 'viewTitles' ([pscustomobject]@{})}
    Set-ObjectProperty $targetForm.viewTitles ([string]$chat.viewName) $paletteViewTitle
    if($authenticationMode -eq 'server-open-alpha'){
        Write-Warning "ERRATA: '$($chat.viewName)' uses server-open-alpha Langflow authentication. This is temporary development configuration only: set LANGFLOW_AUTO_LOGIN=true and LANGFLOW_SKIP_AUTH_AUTO_LOGIN=true on the Langflow server, restart it, and replace this mode with a governed server-side proxy/token exchange before production."
    } else {
        Write-Warning "ERRATA: '$($chat.viewName)' loads the pinned Langflow widget through a governed server proxy. Verify the proxy's authenticated user/token-exchange contract, K2 CSP/CORS, case-context claims, and 401/403 behavior before production; no reusable API key is embedded."
    }
}

$uxDocument=Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $Ux)|ConvertFrom-Json
$mappingDocument=Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $Mapping)|ConvertFrom-Json
$usesStyleProfile=-not [string]::IsNullOrWhiteSpace([string]$mappingDocument.application.styleProfile)
$components=@{};foreach($c in @($uxDocument.components)){$components[[string]$c.id]=$c}
$dashboardPageId=[string]$mappingDocument.dashboard.page;$page=$uxDocument.pages|Where-Object {[string]$_.id -eq $dashboardPageId}|Select-Object -First 1;if($null -eq $page){throw "Dashboard page '$dashboardPageId' was not found."}
$views=[Collections.Generic.List[object]]::new();$formViews=[Collections.Generic.List[string]]::new();$titles=[ordered]@{}
$generatedForms=[Collections.Generic.List[object]]::new()
if($null -eq $mappingDocument.homepage){throw 'The K2 mapping must declare the required native Northstar homepage.'}
$homepage=$mappingDocument.homepage
$homepagePage=@($uxDocument.pages|Where-Object {[string]$_.id -eq [string]$homepage.page})|Select-Object -First 1
if($null -eq $homepagePage){throw "Homepage page '$($homepage.page)' was not found."}
if([string]$homepagePage.renderer -ne 'native-smartforms'){throw "Homepage page '$($homepage.page)' must use renderer native-smartforms."}
if([string]$homepage.implementation -ne 'native-smartforms'){throw 'homepage.implementation must be native-smartforms.'}
if([string]$homepage.page -ne [string]$mappingDocument.dashboard.page){throw 'The canonical native homepage and dashboard must identify the same page.'}
if($null -eq $homepage.navigation){throw 'homepage.navigation is required for the native SmartObject-backed shell.'}
if($null -eq $homepage.commandPalette){throw 'homepage.commandPalette is required as the bounded modern-control enhancement.'}
$navigation=$homepage.navigation
$requiredNavigationProperties=@('NavigationCode','SectionLabel','Label','IconToken','TargetFormName','SortOrder','IsActive','ConfigurationVersion')
if(@($navigation.properties).Count -ne $requiredNavigationProperties.Count -or (@($navigation.properties) -join '|') -cne ($requiredNavigationProperties -join '|')){throw 'homepage.navigation.properties must use the canonical eight-column navigation contract in order.'}
$navigationView=[ordered]@{
    name=[string]$navigation.viewName
    smartObject=[string]$navigation.smartObject
    type='list'
    properties=@($navigation.properties)
    methods=@()
    defaultListMethod='List'
    options=@()
}
$views.Add($navigationView);$formViews.Add([string]$navigation.viewName);$titles[[string]$navigation.viewName]='Application navigation'
$palette=$homepage.commandPalette
if([string]$palette.controlType -ne 'northstar-command-palette'){throw 'homepage.commandPalette.controlType must be northstar-command-palette.'}
if($null -eq $palette.propertiesMap){throw 'homepage.commandPalette.propertiesMap is required.'}
$paletteViewTitle=[string]$palette.viewTitle
if([string]::IsNullOrWhiteSpace($paletteViewTitle)){throw 'homepage.commandPalette.viewTitle is required as the shared Northstar shell/compiler contract.'}
$requiredSuggestionProperties=@('SuggestionCode','Kind','Title','Subtitle','IconToken','TargetUrl','SortOrder','IsActive','ConfigurationVersion')
if(@($palette.properties).Count -ne $requiredSuggestionProperties.Count -or (@($palette.properties) -join '|') -cne ($requiredSuggestionProperties -join '|')){throw 'homepage.commandPalette.properties must use the canonical governed suggestion contract in order.'}
$listMethod=Get-ValueOrDefault $palette.listMethod 'List'
$listDataProperty=Get-ValueOrDefault $palette.listDataProperty 'Suggestions'
$paletteView=[ordered]@{
    name=[string]$palette.viewName
    smartObject=[string]$palette.smartObject
    type='capture'
    properties=@($palette.properties)
    readOnlyProperties=@($palette.properties)
    methods=@()
    defaultListMethod=$listMethod
    options=@()
    webComponents=@([ordered]@{
        name=(Get-ValueOrDefault $palette.controlName 'Northstar Command Palette')
        controlType=[string]$palette.controlType
        replaceBody=$true
        properties=$palette.propertiesMap
        dataBinding=[ordered]@{property=$listDataProperty;method=$listMethod;serverUserScoped=$true}
        events=@([ordered]@{name='Navigate';action='navigate';sourceProperty='Value';target='_self'})
    })
}
$views.Add($paletteView);$formViews.Add([string]$palette.viewName);$titles[[string]$palette.viewName]=$paletteViewTitle
$summary=$mappingDocument.dashboard.summary;$cards=[Collections.Generic.List[object]]::new();$props=[Collections.Generic.List[string]]::new()
foreach($binding in @($summary.components)){if(-not $components.ContainsKey([string]$binding.id)){throw "Unknown UX component mapping: $($binding.id)"};$c=$components[[string]$binding.id];$props.Add([string]$binding.property);$cards.Add([ordered]@{property=$binding.property;label=(Get-ValueOrDefault $binding.label $c.id);tone=(Get-ValueOrDefault $binding.tone 'neutral');explanation=(Get-ValueOrDefault $c.explanation '')})}
$views.Add([ordered]@{name=$summary.viewName;smartObject=$summary.smartObject;type='capture';properties=@($props);methods=@();defaultListMethod='List';options=@();metricCards=@($cards)});$formViews.Add($summary.viewName);$titles[$summary.viewName]='Operational position'
if($null -ne $mappingDocument.dashboard.widgets -and @($mappingDocument.dashboard.widgets).Count -gt 0){
    $validVariants=@('trend','attention','stage','supplier')
    foreach($binding in @($mappingDocument.dashboard.widgets)){
        if(-not $components.ContainsKey([string]$binding.component)){throw "Unknown UX dashboard-widget mapping: $($binding.component)"}
        $variant=[string]$binding.variant
        if($validVariants -notcontains $variant){throw "Dashboard widget '$($binding.viewName)' has unsupported variant '$variant'."}
        if($null -eq $binding.properties -or @($binding.properties).Count -eq 0){throw "Dashboard widget '$($binding.viewName)' must select governed projection properties."}
        $listMethod=Get-ValueOrDefault $binding.listMethod 'List'
        $controlProperties=[ordered]@{
            Value=''
            Data='[]'
            Variant=$variant
            Heading=(Get-ValueOrDefault $binding.heading $binding.title)
            Subtitle=(Get-ValueOrDefault $binding.subtitle '')
            ActionLabel=(Get-ValueOrDefault $binding.actionLabel '')
            ActionTarget=(Get-ValueOrDefault $binding.actionTarget '')
            EmptyMessage=(Get-ValueOrDefault $binding.emptyMessage 'No data to display.')
            Width='100%'
            Height=(Get-ValueOrDefault $binding.height $(if($variant -in @('trend','attention')){'356px'}else{'270px'}))
            TagName='northstar-dashboard-widget'
            RuntimeScriptFileNames='control-runtime.js'
            DesigntimeScriptFileNames='control-designtime.js'
            RuntimeStyleFileNames='control-runtime.css'
            DesigntimeStyleFileNames='control-designtime.css'
            Icon='control-icon.svg'
        }
        $widgetView=[ordered]@{
            name=[string]$binding.viewName
            smartObject=[string]$binding.smartObject
            type='capture'
            properties=@($binding.properties)
            readOnlyProperties=@($binding.properties)
            methods=@()
            defaultListMethod=$listMethod
            options=@()
            webComponents=@([ordered]@{
                name=(Get-ValueOrDefault $binding.controlName ('Northstar '+$binding.heading))
                controlType='northstar-dashboard-widget'
                replaceBody=$true
                properties=$controlProperties
                dataBinding=[ordered]@{property='Data';method=$listMethod;serverUserScoped=$true}
                events=@([ordered]@{name='Navigate';action='navigate';sourceProperty='Value';target='_self'})
            })
        }
        $views.Add($widgetView);$formViews.Add([string]$binding.viewName)
        $titles[[string]$binding.viewName]=(Get-ValueOrDefault $binding.title $binding.heading)
        if(Test-IncludeDataAlternative $binding){
            $dataViewName=Get-ValueOrDefault $binding.tableViewName ([string]$binding.viewName+' Data')
            $dataView=[ordered]@{name=$dataViewName;smartObject=[string]$binding.smartObject;type='list';properties=@($binding.properties);readOnlyProperties=@($binding.properties);methods=@();defaultListMethod=$listMethod;options=@('toolbar')}
            $views.Add($dataView);$formViews.Add([string]$dataViewName);$titles[[string]$dataViewName]=((Get-ValueOrDefault $binding.title $binding.heading)+' data')
        }
    }
} else {
    foreach($binding in @($mappingDocument.dashboard.charts)){
        if(-not $components.ContainsKey([string]$binding.component)){throw "Unknown UX chart mapping: $($binding.component)"}
        $c=$components[[string]$binding.component]
        $chartType=if($binding.type){$binding.type}elseif($c.chart_type -eq 'horizontal-bar'){'bar'}else{$c.chart_type}
        $chart=[ordered]@{name=(ConvertTo-ControlName 'cht' ([string]$binding.component));title=(Get-ValueOrDefault $binding.title $binding.component);type=$chartType;categoryProperty=$binding.categoryProperty;valueProperty=$binding.valueProperty;height=(Get-ValueOrDefault $binding.height 260);showLegend=[bool](Get-ValueOrDefault $binding.showLegend $false);showLabels=[bool](Get-ValueOrDefault $binding.showLabels $true);emptyState=(Get-ValueOrDefault $c.empty_state 'No data to display.')}
        $views.Add([ordered]@{name=$binding.viewName;smartObject=$binding.smartObject;type='capture';properties=@($binding.categoryProperty,$binding.valueProperty);methods=@();defaultListMethod='List';options=@();charts=@($chart)})
        $formViews.Add($binding.viewName);$titles[$binding.viewName]=$chart.title
        if(Test-IncludeDataAlternative $binding){
            $dataViewName=Get-ValueOrDefault $binding.tableViewName ([string]$binding.viewName+' Data')
            $views.Add([ordered]@{name=$dataViewName;smartObject=$binding.smartObject;type='list';properties=@($binding.categoryProperty,$binding.valueProperty);methods=@();defaultListMethod='List';options=@('toolbar')})
            $formViews.Add($dataViewName);$titles[$dataViewName]=($chart.title+' data')
        }
    }
    foreach($binding in @($mappingDocument.dashboard.queues)){if(-not $components.ContainsKey([string]$binding.component)){throw "Unknown UX queue mapping: $($binding.component)"};$views.Add([ordered]@{name=$binding.viewName;smartObject=$binding.smartObject;type='list';properties=@($binding.properties);methods=@();defaultListMethod='List';options=@('toolbar')});$formViews.Add($binding.viewName);$titles[$binding.viewName]=(Get-ValueOrDefault $binding.title $binding.component)}
}
$dashboardForm=[ordered]@{name=$mappingDocument.dashboard.formName;useLegacyTheme=$false;useStyleProfile=$usesStyleProfile;useCommonHeader=$false;useCommonFooter=$false;views=@($formViews);options=@('no-tabs');viewTitles=$titles;preFill=[ordered]@{enabled=$false;disabledReason='The native Northstar homepage is read-only; test data is supplied by governed SmartObject projections.'}}
$generatedForms.Add($dashboardForm);$reportViewNames=@()
if($null -ne $mappingDocument.reports){
    $reports=$mappingDocument.reports;$reportTitles=[ordered]@{};$reportViewsByGroup=[ordered]@{}
    foreach($binding in @($reports.views)){
        $chart=[ordered]@{name=(ConvertTo-ControlName 'cht' ([string]$binding.viewName));title=[string]$binding.title;type=(Get-ValueOrDefault $binding.type 'bar');categoryProperty=$binding.categoryProperty;valueProperty=$binding.valueProperty;height=(Get-ValueOrDefault $binding.height 250);showLegend=[bool](Get-ValueOrDefault $binding.showLegend $false);showLabels=[bool](Get-ValueOrDefault $binding.showLabels $true);emptyState=(Get-ValueOrDefault $binding.emptyState 'No report data to display.')}
        $reportView=[ordered]@{name=$binding.viewName;smartObject=$binding.smartObject;type='capture';properties=@($binding.categoryProperty,$binding.valueProperty);methods=@();defaultListMethod='List';options=@();charts=@($chart)}
        $views.Add($reportView);$reportViewNames+=([string]$binding.viewName);$reportTitles[[string]$binding.viewName]=[string]$binding.title;$group=Get-ValueOrDefault $binding.group 'Reports'
        if(-not $reportViewsByGroup.Contains($group)){$reportViewsByGroup[$group]=[Collections.Generic.List[string]]::new()}
        $reportViewsByGroup[$group].Add([string]$binding.viewName)
        if(Test-IncludeDataAlternative $binding){
            $dataViewName=Get-ValueOrDefault $binding.tableViewName ([string]$binding.viewName+' Data')
            $reportDataView=[ordered]@{name=$dataViewName;smartObject=$binding.smartObject;type='list';properties=@($binding.categoryProperty,$binding.valueProperty);methods=@();defaultListMethod='List';options=@('toolbar')}
            $views.Add($reportDataView);$reportViewNames+=([string]$dataViewName);$reportTitles[[string]$dataViewName]=([string]$binding.title+' data');$reportViewsByGroup[$group].Add([string]$dataViewName)
        }
    }
    $reportTabs=@($reportViewsByGroup.GetEnumerator()|ForEach-Object {[ordered]@{name=[string]$_.Key;views=@($_.Value)}})
    $generatedForms.Add([ordered]@{name=$reports.formName;useLegacyTheme=$false;useStyleProfile=$usesStyleProfile;useCommonHeader=$false;useCommonFooter=$false;views=@($reportViewNames);viewTitles=$reportTitles;tabs=$reportTabs})
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
    $generatedForms.Add([ordered]@{name=$myWork.formName;useLegacyTheme=$false;useStyleProfile=$usesStyleProfile;useCommonHeader=$false;useCommonFooter=$false;views=@($myWorkViews);viewTitles=$myWorkTitles;tabs=@($myWorkTabs)})
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
                foreach($tabNavigation in @($shellForm.listClickTabNavigation)){if([string]$tabNavigation.targetTab -eq [string]$workspaceTabName){$tabNavigation.targetTab=$firstSection}}
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
            $entryView.layoutColumns=2
            $entryView.options=@($entryView.options|Where-Object {[string]$_ -notin @('toolbar','labels-left')})
            $manifest.application.views=@($manifest.application.views)+@($entryView);$effectiveMasterView=[string]$init.captureViewName
        }
        $reviewReadMethod=Get-ValueOrDefault $init.readMethod 'Read'
        $reviewView=[ordered]@{name=$init.reviewViewName;smartObject=$init.smartObject;type='capture';properties=@($init.reviewProperties);readOnlyProperties=@($init.reviewProperties);methods=@($reviewReadMethod);layoutColumns=2;options=@('colon-labels')}
        if(@($manifest.application.views|Where-Object {[string]$_.name -eq [string]$reviewView.name}).Count -gt 0){throw "Initiation review View collides with an existing View: $($reviewView.name)"}
        $manifest.application.views=@($manifest.application.views)+@($reviewView)
        $initBusinessViews=@($effectiveMasterView)+@($init.details|ForEach-Object {$_.view})+@($init.reviewViewName)
        $initViews=if($usesStyleProfile){@(([string]$navigation.viewName))+@($initBusinessViews)}else{@($initBusinessViews)}
        foreach($requiredView in $initViews){if(@($manifest.application.views|Where-Object {[string]$_.name -eq [string]$requiredView}).Count -eq 0){throw "Initiation references unknown View: $requiredView"}}
        $detailContracts=@($init.details|ForEach-Object {[ordered]@{view=$_.view;foreignKeyProperty=$_.foreignKeyProperty;createMethod=(Get-ValueOrDefault $_.createMethod 'Create');updateMethod=(Get-ValueOrDefault $_.updateMethod 'Update');deleteMethod=(Get-ValueOrDefault $_.deleteMethod 'Delete');listMethod=(Get-ValueOrDefault $_.listMethod 'List')}})
        $compiledTabs=[Collections.Generic.List[object]]::new()
        $guidedSteps=[Collections.Generic.List[object]]::new()
        $useGuided=Test-UseGuidedJourney $init $journey
        $finalActionMode=([string](Get-ValueOrDefault $init.finalActionMode 'workflow')).Trim().ToLowerInvariant()
        if(@('workflow','complete') -notcontains $finalActionMode){throw "initiation.finalActionMode must be 'workflow' or 'complete'."}
        if($finalActionMode -eq 'complete' -and -not $useGuided){throw "initiation.finalActionMode='complete' requires a guided journey."}
        if($null -ne $init.stepTabs -and @($init.stepTabs).Count -gt 0){
            if(@($init.stepTabs).Count -lt 3 -or @($init.stepTabs).Count -gt 7){throw 'initiation.stepTabs must contain between 3 and 7 physical screens.'}
            foreach($stepTab in @($init.stepTabs)){
                $journeyStep=@($journey.steps|Where-Object {[string]$_.id -eq [string]$stepTab.id})|Select-Object -First 1
                if($null -eq $journeyStep){throw "initiation.stepTabs references unknown journey step '$($stepTab.id)'."}
                $tabName=Get-ValueOrDefault $stepTab.name (Get-ValueOrDefault $stepTab.tab $journeyStep.title)
                $stepViews=@(Resolve-InitiationStepViews $stepTab $effectiveMasterView ([string]$init.reviewViewName))
                $compiledTabs.Add([ordered]@{name=$tabName;views=$stepViews})
                $guidedSteps.Add([ordered]@{
                    code=([string]$stepTab.id).ToUpperInvariant().Replace('-','_')
                    label=(Get-ValueOrDefault $stepTab.label $journeyStep.title)
                    title=(Get-ValueOrDefault $stepTab.title $journeyStep.title)
                    description=(Get-ValueOrDefault $stepTab.description ("Complete "+([string]$journeyStep.title).ToLowerInvariant()+"."))
                    tab=$tabName
                    advance='continue'
                })
            }
            if($usesStyleProfile){$compiledTabs[0].views=@(([string]$navigation.viewName))+@($compiledTabs[0].views)}
            $placed=@($compiledTabs|ForEach-Object {@($_.views)})
            if($placed.Count -ne $initViews.Count -or @($placed|Select-Object -Unique).Count -ne $placed.Count -or @($initViews|Where-Object {$placed -notcontains $_}).Count -gt 0){
                throw 'initiation.stepTabs must place every initiation View exactly once. Use $master and $review for the generated capture and review Views.'
            }
        } else {
            $detailsTab=Get-ValueOrDefault $init.detailsTab 'Case Details'
            $evidenceTab=Get-ValueOrDefault $init.evidenceTab 'Evidence'
            $reviewTab=Get-ValueOrDefault $init.reviewTab $(if($finalActionMode -eq 'complete'){'Review & Finish'}else{'Review & Submit'})
            $compiledTabs.Add([ordered]@{name=$detailsTab;views=@($effectiveMasterView)+@($init.details|Where-Object {$_.step -eq 'details'}|ForEach-Object {$_.view})})
            $compiledTabs.Add([ordered]@{name=$evidenceTab;views=@($init.details|Where-Object {$_.step -eq 'evidence'}|ForEach-Object {$_.view})})
            $compiledTabs.Add([ordered]@{name=$reviewTab;views=@($init.reviewViewName)})
            if($usesStyleProfile){$compiledTabs[0].views=@(([string]$navigation.viewName))+@($compiledTabs[0].views)}
            $guidedSteps.Add([ordered]@{code='DETAILS';label=(Get-ValueOrDefault $init.detailsStepLabel 'Describe');title=(Get-ValueOrDefault $init.detailsStepTitle 'What happened?');description=(Get-ValueOrDefault $init.detailsStepDescription 'Describe what happened and provide the case context and impact.');tab=$detailsTab;advance='continue'})
            $guidedSteps.Add([ordered]@{code='EVIDENCE';label=(Get-ValueOrDefault $init.evidenceStepLabel 'Evidence');title=(Get-ValueOrDefault $init.evidenceStepTitle 'Add supporting evidence');description=(Get-ValueOrDefault $init.evidenceStepDescription 'Add the supporting records needed to understand the case.');tab=$evidenceTab;advance='continue'})
            $defaultReviewDescription=if($finalActionMode -eq 'complete'){'Check the saved draft and finish this design iteration.'}else{'Check the complete case before submitting it.'}
            $guidedSteps.Add([ordered]@{code='REVIEW';label=(Get-ValueOrDefault $init.reviewStepLabel 'Review');title=(Get-ValueOrDefault $init.reviewStepTitle $(if($finalActionMode -eq 'complete'){'Review and finish'}else{'Review and submit'}));description=(Get-ValueOrDefault $init.reviewStepDescription $defaultReviewDescription);tab=$reviewTab;advance='continue'})
        }
        if($finalActionMode -eq 'complete'){
            $completeReviewTab=Get-ValueOrDefault $init.completeReviewTab 'Review & Finish'
            $compiledTabs[$compiledTabs.Count-1].name=$completeReviewTab
            $guidedSteps[$guidedSteps.Count-1].tab=$completeReviewTab
            $guidedSteps[$guidedSteps.Count-1].label=Get-ValueOrDefault $init.completeReviewStepLabel 'Review'
            $guidedSteps[$guidedSteps.Count-1].title=Get-ValueOrDefault $init.completeReviewStepTitle 'Review and finish'
            $guidedSteps[$guidedSteps.Count-1].description=Get-ValueOrDefault $init.completeReviewStepDescription 'Check the saved draft and finish this design iteration.'
        }
        $guidedSteps[$guidedSteps.Count-2].advance='save'
        $guidedSteps[$guidedSteps.Count-1].advance=if($finalActionMode -eq 'complete'){'complete'}else{'submit'}
        $reviewTabName=[string]$compiledTabs[$compiledTabs.Count-1].name
        $initForm=[ordered]@{name=$init.formName;useLegacyTheme=$false;useStyleProfile=$usesStyleProfile;useCommonHeader=$false;useCommonFooter=$false;views=$initViews;behaviors=@('refresh-list-form-submit','refresh-list-form-load');viewTitles=[ordered]@{};tabs=@($compiledTabs);listClickTabNavigation=@();masterDetail=[ordered]@{masterView=$effectiveMasterView;masterKeyProperty=$init.masterKeyProperty;masterCreateMethod=(Get-ValueOrDefault $init.createMethod 'Create');masterUpdateMethod=(Get-ValueOrDefault $init.updateMethod 'Update');masterReadMethod=(Get-ValueOrDefault $init.readMethod 'Read');saveButtonText=(Get-ValueOrDefault $init.saveButtonText 'Save draft and review');successMessageTitle=(Get-ValueOrDefault $init.successTitle 'Draft saved');successMessageBody=(Get-ValueOrDefault $init.successBody 'Your draft was saved and is ready to review.');details=$detailContracts;review=[ordered]@{view=$init.reviewViewName;keyProperty=$init.masterKeyProperty;readMethod=(Get-ValueOrDefault $init.readMethod 'Read');tab=$reviewTabName}}}
        if($finalActionMode -eq 'complete'){
            $initForm['completionButton']=[ordered]@{
                name=(Get-ValueOrDefault $init.completeButtonName 'btnFinishDraft')
                text=(Get-ValueOrDefault $init.completeButtonText 'Finish')
                tab=$reviewTabName
                messageTitle=(Get-ValueOrDefault $init.completeTitle 'Draft complete')
                messageBody=(Get-ValueOrDefault $init.completeBody 'Your draft is saved. It has not been submitted.')
            }
        } else {
            $initForm['workflowStartButton']=[ordered]@{name=(Get-ValueOrDefault $init.submitButtonName 'btnSubmitCase');text=(Get-ValueOrDefault $init.submitButtonText 'Submit case');tab=$reviewTabName}
        }
        if($useGuided){
            $journeyPage=@($uxDocument.pages|Where-Object {[string]$_.journey -eq [string]$init.journey})|Select-Object -First 1
            $initForm.guidedJourney=[ordered]@{
                title=(Get-ValueOrDefault $init.journeyTitle (Get-ValueOrDefault $journeyPage.title 'New case'))
                description=$(if($finalActionMode -eq 'complete'){
                    Get-ValueOrDefault $init.completeJourneyDescription 'Complete each screen, save the draft, then review and finish.'
                }else{
                    Get-ValueOrDefault $init.journeyDescription 'Complete each screen, save the draft, then review and submit the case.'
                })
                validateOnContinue=$true
                backButtonText=(Get-ValueOrDefault $init.backButtonText 'Back')
                continueButtonText=(Get-ValueOrDefault $init.continueButtonText 'Continue')
                steps=@($guidedSteps)
            }
        }
        foreach($viewName in $initViews){$initForm.viewTitles[$viewName]=if($viewName -eq ([string]$navigation.viewName)){'Application navigation'}elseif($viewName -eq $init.reviewViewName){if($finalActionMode -eq 'complete'){'Review the saved draft'}else{'Review the case before submission'}}elseif($viewName -eq $effectiveMasterView){'Case details'}else{($viewName -replace '^[^.]+\.','')}}
        $manifest.application.forms=@($manifest.application.forms)+@($initForm)
    }
    Add-AgenticChat $manifest $mappingDocument
    if($null -eq $manifest.verification){$manifest|Add-Member -NotePropertyName verification -NotePropertyValue ([pscustomobject]@{})}
    $manifest.verification|Add-Member -NotePropertyName expectedViews -NotePropertyValue @($manifest.application.views|ForEach-Object {$_.name}) -Force
    $manifest.verification|Add-Member -NotePropertyName expectedForms -NotePropertyValue @($manifest.application.forms|ForEach-Object {$_.name}) -Force
    $manifest.verification|Add-Member -NotePropertyName smokeTestRuntime -NotePropertyValue $true -Force
    $manifest.verification|Add-Member -NotePropertyName runtimeBaseUrl -NotePropertyValue ([string]$mappingDocument.runtimeBaseUrl) -Force
} else {
    $manifest=[ordered]@{name=$mappingDocument.application.name;k2=[ordered]@{host='localhost';port=5555;integrated=$true;securityLabel='K2'};application=[ordered]@{rootCategoryPath=$mappingDocument.application.rootCategoryPath;theme=$mappingDocument.application.theme;styleProfile=$mappingDocument.application.styleProfile;solutionCode=$mappingDocument.application.solutionCode;replaceExisting=$true;checkIn=$true;views=@($views);forms=@($generatedForms)};verification=[ordered]@{expectedViews=@($views|ForEach-Object {$_.name});expectedForms=@($generatedForms|ForEach-Object {$_.name});smokeTestRuntime=$true;runtimeBaseUrl=$mappingDocument.runtimeBaseUrl}}
    Add-AgenticChat $manifest $mappingDocument
    $manifest.verification.expectedViews=@($manifest.application.views|ForEach-Object {$_.name})
    $manifest.verification.expectedForms=@($manifest.application.forms|ForEach-Object {$_.name})
}
$destination=[IO.Path]::GetFullPath($Output);$parent=Split-Path -Parent $destination;if($parent -and -not(Test-Path $parent)){New-Item -ItemType Directory -Path $parent|Out-Null};$manifest|ConvertTo-Json -Depth 100|Set-Content -Encoding utf8 -LiteralPath $destination;"Compiled SmartForms dashboard manifest: $destination";exit 0
