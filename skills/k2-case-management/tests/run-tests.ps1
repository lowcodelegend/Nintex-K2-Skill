$ErrorActionPreference = 'Stop'
$validator = Join-Path $PSScriptRoot '..\scripts\validate-case-model.ps1'
$valid = Join-Path $PSScriptRoot '..\assets\case-type-definition.yaml'
$supplier = Join-Path $PSScriptRoot 'supplier-nonconformance.yaml'
$invalid = Join-Path $PSScriptRoot 'invalid-case-type.yaml'

& $validator -Manifest $valid
if ($LASTEXITCODE -ne 0) { throw 'Expected valid fixture to pass.' }
& $validator -Manifest $supplier
if ($LASTEXITCODE -ne 0) { throw 'Expected supplier nonconformance fixture to pass.' }

$output = & $validator -Manifest $invalid 2>&1
if ($LASTEXITCODE -ne 1) { throw "Expected invalid fixture to fail with exit 1; got $LASTEXITCODE." }
$text = $output -join "`n"
foreach ($expected in @('transition destination does not exist', 'terminal stage CLOSE', 'unreachable stage')) {
    if ($text -notmatch [regex]::Escape($expected)) { throw "Invalid fixture did not report: $expected" }
}
Write-Output 'Case-model validator tests passed.'

$uxValidator = Join-Path $PSScriptRoot '..\scripts\validate-case-ux.ps1'
$validUx = Join-Path $PSScriptRoot '..\assets\case-ux.yaml'
$invalidUx = Join-Path $PSScriptRoot 'invalid-case-ux.yaml'
& $uxValidator -Manifest $validUx
if ($LASTEXITCODE -ne 0) { throw 'Expected canonical case UX fixture to pass.' }
$uxOutput = & $uxValidator -Manifest $invalidUx 2>&1
if ($LASTEXITCODE -ne 1) { throw "Expected invalid UX fixture to fail with exit 1; got $LASTEXITCODE." }
$uxText = $uxOutput -join "`n"
foreach ($expected in @('navigation target does not exist', 'requires table_alternative', 'requires mobile viewport', 'requires a summary step')) {
    if ($uxText -notmatch [regex]::Escape($expected)) { throw "Invalid UX fixture did not report: $expected" }
}
Write-Output 'Case-UX validator tests passed.'

$styleExample = Join-Path $PSScriptRoot '..\..\k2-style-profiles\assets\examples\northstar-native-homepage'
$northstarShell = Get-Content -Raw -LiteralPath (Join-Path $styleExample 'northstar-shell.js')
$northstarCss = Get-Content -Raw -LiteralPath (Join-Path $styleExample 'northstar-homepage.css')
if ($northstarShell -match 'card\.appendChild\((label|value)\)' -or $northstarShell -match 'source\.style\.display\s*=\s*[''"]none') {
    throw 'Northstar presentation must not move or replace live K2 metric controls.'
}
if ($northstarShell -match 'chartView\.appendChild\(' -or $northstarShell -match 'addDataAlternativeToggle') {
    throw 'Northstar presentation must not inject chart actions into native K2 Views.'
}
if ($northstarShell -notmatch 'k2sp-kpi-native-grid' -or $northstarCss -notmatch '\.k2sp-kpi-native-grid') {
    throw 'Northstar presentation must decorate the native K2 metric grid in place.'
}
if ($northstarShell -notmatch 'suppressedFrameworkPanelNames' -or $northstarShell -notmatch 'panel\.closest\(''\.row''\)') {
    throw 'Northstar presentation must preserve lifecycle-required common Views and suppress only configured semantic panels in place.'
}
if ($northstarShell -notmatch 'alignNarrowNativeForm' -or $northstarShell -notmatch 'minimumTop - formTop') {
    throw 'Northstar presentation must reserve narrow-layout space after suppressing a lifecycle framework panel.'
}
if ($northstarShell -notmatch 'layoutKpiCells' -or $northstarShell -notmatch 'grid-row' -or $northstarShell -notmatch 'grid-column') {
    throw 'Northstar presentation must coordinate the existing K2 KPI cells at responsive breakpoints without reparenting controls.'
}
& (Get-Command node -ErrorAction Stop).Source --check (Join-Path $styleExample 'northstar-shell.js')
if ($LASTEXITCODE -ne 0) { throw 'Northstar runtime shell JavaScript did not parse.' }
& (Get-Command node -ErrorAction Stop).Source --check (Join-Path $PSScriptRoot '..\scripts\capture-browser-page-cdp.mjs')
if ($LASTEXITCODE -ne 0) { throw 'Northstar Node browser driver JavaScript did not parse.' }
Write-Output 'Northstar K2-ownership and browser-driver tests passed.'

$composer = Join-Path $PSScriptRoot '..\scripts\compose-case-ux.ps1'
$overlay = Join-Path $PSScriptRoot '..\assets\case-ux-overlay.yaml'
$composed = [IO.Path]::GetTempFileName()
try {
    & $composer -Overlay $overlay -Output $composed
    if ($LASTEXITCODE -ne 0) { throw 'Expected canonical UX overlay composition to pass.' }
    & $uxValidator -Manifest $composed
    if ($LASTEXITCODE -ne 0) { throw 'Expected composed canonical UX to validate.' }
    $document = Get-Content -Raw -LiteralPath $composed | ConvertFrom-Json
    foreach ($expected in @('case-header','case-lifecycle','case-actions')) {
        if (@($document.components.id) -notcontains $expected) { throw "Composed UX omitted canonical component: $expected" }
    }
} finally {
    if (Test-Path -LiteralPath $composed) { Remove-Item -LiteralPath $composed -Force }
}
Write-Output 'Case-UX composition tests passed.'

$compiler = Join-Path $PSScriptRoot '..\scripts\compile-case-ux-smartforms.ps1'
$compilerMapping = Join-Path $PSScriptRoot 'case-ux-k2-mapping.json'
$compiled = [IO.Path]::GetTempFileName()
try {
    & $compiler -Ux $validUx -Mapping $compilerMapping -Output $compiled
    if ($LASTEXITCODE -ne 0) { throw 'Expected canonical UX compilation to pass.' }
    $manifest = Get-Content -Raw -LiteralPath $compiled | ConvertFrom-Json
    if (@($manifest.application.views).Count -ne 6) { throw 'Compiled UX did not emit navigation, command palette, summary, chart, queue, and accessible chart-data Views.' }
    if (@($manifest.application.views | Where-Object { $null -ne $_.webComponents -and @($_.webComponents | Where-Object controlType -eq 'northstar-case-homepage').Count -gt 0 }).Count -ne 0) { throw 'Compiled UX retained the retired full-page Northstar Web Component.' }
    $navigationView = @($manifest.application.views | Where-Object { $_.name -eq 'TST.Application Navigation' })[0]
    if (($navigationView.properties -join '|') -ne 'NavigationCode|SectionLabel|Label|IconToken|TargetFormName|SortOrder|IsActive|ConfigurationVersion') { throw 'Compiled UX did not preserve the canonical navigation projection.' }
    $paletteView = @($manifest.application.views | Where-Object { $_.name -eq 'TST.Command Palette' })[0]
    $palette = $paletteView.webComponents[0]
    if ($palette.controlType -ne 'northstar-command-palette' -or $palette.replaceBody -ne $true) { throw 'Compiled UX did not emit the bounded command-palette Web Component.' }
    if ($palette.dataBinding.property -ne 'Suggestions' -or $palette.dataBinding.method -ne 'List' -or $palette.dataBinding.serverUserScoped -ne $true) { throw 'Compiled command palette lacks the governed server-user-scoped list binding.' }
    if ($palette.events[0].name -ne 'Navigate' -or $palette.events[0].sourceProperty -ne 'Value') { throw 'Compiled command palette lacks the native Navigate event contract.' }
    $summaryView = @($manifest.application.views | Where-Object { $_.name -eq 'TST.Operations KPIs' })[0]
    if ($summaryView.type -ne 'capture') { throw 'Compiled UX metric cards must use a native capture layout.' }
    if (@($summaryView.metricCards).Count -ne 2) { throw 'Compiled UX did not emit both metric cards.' }
    if ($summaryView.metricCards[1].label -ne 'sla-at-risk') { throw 'Compiled UX did not apply the canonical component fallback label.' }
    $chartView = @($manifest.application.views | Where-Object { $_.name -eq 'TST.Cases by Stage' })[0]
    if ($chartView.charts[0].name -ne 'chtCasesByStage') { throw 'Compiled UX control naming is not deterministic PascalCase.' }
    if ($chartView.charts[0].type -ne 'bar') { throw 'Compiled UX did not translate horizontal-bar to native bar.' }
    if ($chartView.charts[0].showLabels -ne $true) { throw 'Compiled UX did not apply the chart label default.' }
    $dashboardForm = @($manifest.application.forms | Where-Object { $_.name -eq 'TST.Quality Operations' })[0]
    if ($dashboardForm.views.Count -ne 6 -or $dashboardForm.views[0] -ne 'TST.Application Navigation' -or $dashboardForm.views[1] -ne 'TST.Command Palette') { throw 'Compiled native homepage did not compose navigation first, palette second, and every native dashboard View.' }
    if ($dashboardForm.useLegacyTheme -ne $false -or $dashboardForm.preFill.enabled -ne $false) { throw 'Native homepage Form must use the modern theme and an explicit Pre-fill opt-out.' }
    if ($null -ne $manifest.application.PSObject.Properties['commonHeader']) { throw 'Northstar compilation must not remove an environment common framework that can participate in required native Form-load rules; suppress duplicate chrome through the guarded Style Profile.' }
} finally {
    if (Test-Path -LiteralPath $compiled) { Remove-Item -LiteralPath $compiled -Force }
}
Write-Output 'Case-UX SmartForms compiler tests passed.'

$baseManifest = Join-Path $PSScriptRoot 'smartforms-base.json'
$combined = [IO.Path]::GetTempFileName()
try {
    & $compiler -Ux $validUx -Mapping $compilerMapping -BaseManifest $baseManifest -Output $combined
    if ($LASTEXITCODE -ne 0) { throw 'Expected base-manifest UX embellishment to pass.' }
    $manifest = Get-Content -Raw -LiteralPath $combined | ConvertFrom-Json
    if ($manifest.name -ne 'TST.Case Workspace') { throw 'UX embellishment did not preserve the base application identity.' }
    if (@($manifest.application.views).Count -ne 7 -or @($manifest.application.forms).Count -ne 2) { throw 'UX embellishment did not preserve base artifacts and append the native homepage/dashboard artifacts.' }
    $workspace = @($manifest.application.views | Where-Object { $_.name -eq 'TST.Case Workspace' })[0]
    if ($workspace.lifecycleTrackers[0].property -ne 'CurrentStageCode' -or @($workspace.lifecycleTrackers[0].stages).Count -ne 3) { throw 'UX embellishment did not apply the reusable lifecycle tracker.' }
    $shell = @($manifest.application.forms | Where-Object { $_.name -eq 'TST.Case Management' })[0]
    if (@($shell.tabs).Count -ne 3 -or $shell.tabs[1].name -ne 'Analytics' -or @($shell.tabs[1].views).Count -ne 6) { throw 'UX embellishment did not insert the complete native homepage/dashboard before My Tasks.' }
    if (@($shell.views).Count -ne 7) { throw 'UX embellishment did not compose generated native homepage Views into the shell Form.' }
} finally {
    if (Test-Path -LiteralPath $combined) { Remove-Item -LiteralPath $combined -Force }
}
Write-Output 'Case-UX base-manifest embellishment tests passed.'

$exampleRoot = Join-Path $PSScriptRoot '..\..\..\examples\supplier-nonconformance'
$initiationCompiled = [IO.Path]::GetTempFileName()
try {
    & $compiler -Ux (Join-Path $exampleRoot 'case-ux.composed.json') -Mapping (Join-Path $exampleRoot 'case-ux-k2-mapping.yaml') -BaseManifest (Join-Path $exampleRoot 'smartforms-manifest.json') -Output $initiationCompiled
    if ($LASTEXITCODE -ne 0) { throw 'Expected canonical initiation compilation to pass.' }
    $manifest = Get-Content -Raw -LiteralPath $initiationCompiled | ConvertFrom-Json
    $form = @($manifest.application.forms | Where-Object { $_.name -eq 'SNC.New Nonconformance' })[0]
    if ($null -eq $form) { throw 'Initiation compiler did not emit the guided initiation Form.' }
    if (@($form.tabs.name) -join '|' -ne 'Case Details|Evidence|Review & Submit') { throw 'Initiation compiler emitted the wrong journey steps.' }
    if ($form.masterDetail.review.view -ne 'SNC.New Case Review' -or $form.masterDetail.review.tab -ne 'Review & Submit') { throw 'Initiation compiler did not emit saved-key review navigation.' }
    if ($form.masterDetail.masterView -ne 'SNC.New Case Details' -or $form.tabs[0].views[0] -ne 'SNC.New Case Details') { throw 'Initiation compiler did not use the dedicated reporter-facing entry View.' }
    $entry = @($manifest.application.views | Where-Object { $_.name -eq 'SNC.New Case Details' })[0]
    if ($null -eq $entry -or @($entry.hiddenProperties) -notcontains 'CaseId' -or @($entry.hiddenProperties) -contains 'Title') { throw 'Initiation compiler did not retain hidden method-bound fields while exposing mapped entry fields.' }
    if ($entry.propertyLabels.PriorityCode -ne 'Priority' -or $entry.propertyLabels.ConfidentialityCode -ne 'Confidentiality') { throw 'Initiation compiler did not emit reporter-facing property labels.' }
    if ($form.workflowStartButton.name -ne 'btnSubmitCase' -or $form.workflowStartButton.tab -ne 'Review & Submit') { throw 'Initiation compiler did not emit the dedicated final submit seam.' }
    if (@($form.masterDetail.details).Count -ne 2) { throw 'Initiation compiler did not preserve both mapped child collections.' }
    $shell = @($manifest.application.forms | Where-Object { $_.name -eq 'SNC.Supplier Nonconformance' })[0]
    if (@($shell.tabs.name) -join '|' -ne 'Cases|Overview|Investigation|Collaboration|Decisions & Actions|Activity & History|Analytics|Reports|My Tasks') { throw 'Workspace compiler did not compose the reusable section, analytics, reports, and task tabs.' }
    if ($shell.listClickTabNavigation[0].targetTab -ne 'Overview') { throw 'Workspace compiler did not retarget list drill-in to the first section.' }
    if (@($shell.tabs | Where-Object name -eq 'Overview')[0].views[1] -ne 'SNC.Commands') { throw 'Workspace compiler did not keep governed next actions in the primary case context.' }
    $reports = @($manifest.application.forms | Where-Object { $_.name -eq 'SNC.Reports' })[0]
    if (@($reports.tabs.name) -join '|' -ne 'Operations|Performance|Quality' -or @($reports.views).Count -ne 12) { throw 'Reports compiler did not emit the reusable governed report collection with accessible data alternatives.' }
    $dashboard = @($manifest.application.forms | Where-Object { $_.name -eq 'SNC.Quality Operations' })[0]
    if ($dashboard.useLegacyTheme -ne $false -or $dashboard.preFill.enabled -ne $false -or @($dashboard.views).Count -ne 11) { throw 'Northstar dashboard compiler did not emit the modern native shell and complete bounded-widget composition.' }
    $widgetViews = @($manifest.application.views | Where-Object { @($_.webComponents | Where-Object controlType -eq 'northstar-dashboard-widget').Count -eq 1 })
    if ($widgetViews.Count -ne 4 -or (@($widgetViews.webComponents.properties.Variant | Sort-Object) -join '|') -ne 'attention|stage|supplier|trend') { throw 'Northstar dashboard compiler did not emit the four bounded widget variants.' }
    foreach ($widgetView in $widgetViews) {
        $widget = $widgetView.webComponents[0]
        if ($widget.dataBinding.property -ne 'Data' -or $widget.dataBinding.method -ne 'List' -or $widget.dataBinding.serverUserScoped -ne $true) { throw "Dashboard widget '$($widgetView.name)' lacks the governed View-init list binding." }
        if (@($manifest.application.views | Where-Object name -eq ($widgetView.name + ' Data')).Count -ne 1) { throw "Dashboard widget '$($widgetView.name)' lacks its native accessible data alternative." }
    }
    $myWork = @($manifest.application.forms | Where-Object { $_.name -eq 'SNC.My Work' })[0]
    if (@($myWork.tabs.name) -join '|' -ne 'My Tasks|Urgent Team Work' -or $myWork.tabs[0].worklist.rows -ne 20 -or @($myWork.views) -notcontains 'SNC.Attention Now Data') { throw 'My Work compiler did not reuse the native Worklist and mapped operational queue.' }
} finally {
    if (Test-Path -LiteralPath $initiationCompiled) { Remove-Item -LiteralPath $initiationCompiled -Force }
}
Write-Output 'Case-UX guided-initiation compiler tests passed.'

$portableCompiled = [IO.Path]::GetTempFileName()
try {
    & $compiler -Ux $validUx -Mapping (Join-Path $PSScriptRoot 'portable-case-mapping.json') -BaseManifest (Join-Path $PSScriptRoot 'portable-case-base.json') -Output $portableCompiled
    if ($LASTEXITCODE -ne 0) { throw 'Expected second-case portability compilation to pass.' }
    $manifest = Get-Content -Raw -LiteralPath $portableCompiled | ConvertFrom-Json
    if ($manifest.name -ne 'RQT.Request Case') { throw 'Portable compilation did not preserve the second package identity.' }
    $shell = @($manifest.application.forms | Where-Object name -eq 'RQT.Case Management')[0]
    if (@($shell.tabs.name) -join '|' -ne 'Cases|Insights|My Tasks') { throw 'Portable compilation did not embellish the second shell at its mapped seam.' }
    $initiation = @($manifest.application.forms | Where-Object name -eq 'RQT.New Request')[0]
    if ($initiation.workflowStartButton.name -ne 'btnSubmitRequest' -or $initiation.masterDetail.details[0].view -ne 'RQT.Request Detail') { throw 'Portable compilation leaked supplier-nonconformance assumptions.' }
    if (@($manifest.application.views.name | Where-Object { $_ -like 'SNC.*' }).Count -ne 0) { throw 'Portable compilation emitted an SNC-specific artifact.' }
} finally {
    if (Test-Path -LiteralPath $portableCompiled) { Remove-Item -LiteralPath $portableCompiled -Force }
}
Write-Output 'Case-UX second-package portability tests passed.'

$referenceRenderer = Join-Path $PSScriptRoot '..\scripts\render-case-ux-reference-suite.ps1'
$referenceRoot = Join-Path ([IO.Path]::GetTempPath()) ('case-ux-reference-' + [guid]::NewGuid())
try {
    & $referenceRenderer -Manifest $validUx -OutputDirectory $referenceRoot
    foreach ($page in @('operations-dashboard','my-work','case-initiation','case-workspace','reports')) {
        $html = Join-Path $referenceRoot ($page + '.html')
        if (-not (Test-Path -LiteralPath $html)) { throw "Reference suite omitted page: $page" }
        $content = Get-Content -Raw -LiteralPath $html
        if ($content -notlike "*data-page=`"$page`"*" -or $content -notlike '*Skip to content*') { throw "Reference page lacks reusable shell/accessibility contract: $page" }
    }
} finally {
    $resolvedReference = [IO.Path]::GetFullPath($referenceRoot); $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
    if ($resolvedReference.StartsWith($resolvedTemp + '\',[StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $resolvedReference)) { Remove-Item -LiteralPath $resolvedReference -Recurse -Force }
}
Write-Output 'Case-UX multi-page reference renderer tests passed.'
