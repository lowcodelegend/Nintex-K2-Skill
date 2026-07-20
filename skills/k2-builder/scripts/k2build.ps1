[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('validate', 'plan', 'version')]
    [string]$Command = 'validate',

    [string]$Manifest,

    [ValidateSet('text', 'json')]
    [string]$Output = 'text'
)

$ErrorActionPreference = 'Stop'

if ($Command -eq 'version') {
    Write-Output 'k2build 0.17.0'
    exit 0
}

if ([string]::IsNullOrWhiteSpace($Manifest)) {
    Write-Error 'Manifest is required for validate and plan.'
    exit 2
}

function Add-Issue {
    param([string]$Message)
    $script:issues.Add($Message)
}

function Read-JsonFile {
    param([string]$Path, [string]$Label)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Add-Issue "$Label manifest does not exist: $Path"
        return $null
    }

    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        Add-Issue "$Label manifest is not valid JSON: $Path ($($_.Exception.Message))"
        return $null
    }
}

function Get-PropertyValue {
    param($Object, [string]$Name)

    if ($null -eq $Object) { return $null }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Resolve-ComponentManifest {
    param($Component, [string]$Label)

    $relativePath = Get-PropertyValue $Component 'manifest'
    if ([string]::IsNullOrWhiteSpace([string]$relativePath)) {
        Add-Issue "$Label must declare manifest."
        return $null
    }

    $candidate = Join-Path -Path $script:manifestDirectory -ChildPath ([string]$relativePath)
    try { return [System.IO.Path]::GetFullPath($candidate) }
    catch {
        Add-Issue "$Label has an invalid manifest path: $relativePath"
        return $null
    }
}

function Test-VersionFreeName {
    param([string]$Value, [string]$Label)

    if ([string]::IsNullOrWhiteSpace($Value)) { return }
    if ($Value -match '(?i)(^|[\\/\s_.-])v?\d+\.\d+(?:\.\d+)?($|[\\/\s_.-])') {
        Add-Issue "$Label contains a release/version token: $Value"
    }
}

function Test-ShortCodePrefix {
    param([string]$Value, [string]$Label)

    if ([string]::IsNullOrWhiteSpace($Value) -or [string]::IsNullOrWhiteSpace($script:shortCode)) { return }
    $expected = $script:shortCode + '.'
    if (-not $Value.StartsWith($expected, [StringComparison]::Ordinal)) {
        Add-Issue "$Label must start with solution short-code prefix '$expected': $Value"
    }
}

function Test-ShortCodeCategoryPath {
    param([string]$Value, [string]$Label)

    if ([string]::IsNullOrWhiteSpace($Value)) { return }
    $segments = @($Value.Split([char[]]'\/', [StringSplitOptions]::RemoveEmptyEntries))
    if ($segments.Count -gt 0) { Test-ShortCodePrefix $segments[-1] $Label }
}

function Test-SmartObjectPrefix {
    param([string]$Value, [string]$Label)

    if ([string]::IsNullOrWhiteSpace($Value) -or [string]::IsNullOrWhiteSpace($script:shortCode)) { return }
    if (-not ($Value.StartsWith($script:shortCode + '.', [StringComparison]::Ordinal) -or $Value.StartsWith($script:shortCode + '_', [StringComparison]::Ordinal))) {
        Add-Issue "$Label must start with solution short-code prefix '$($script:shortCode).' or K2's generated '$($script:shortCode)_': $Value"
    }
}

function Get-DependencyNames {
    param($Component)
    $value = Get-PropertyValue $Component 'dependsOn'
    if ($null -eq $value) { return @() }
    return @($value | ForEach-Object { [string]$_ })
}

$issues = [System.Collections.Generic.List[string]]::new()
$resolvedPolicies = [System.Collections.Generic.List[object]]::new()
$planItems = [System.Collections.Generic.List[object]]::new()
$errata = [System.Collections.Generic.List[object]]::new()

try {
    $manifestPath = (Resolve-Path -LiteralPath $Manifest).Path
}
catch {
    Write-Error "Solution manifest does not exist: $Manifest"
    exit 2
}

$manifestDirectory = Split-Path -Parent $manifestPath
$solution = Read-JsonFile $manifestPath 'Solution'
if ($null -eq $solution) {
    Write-Error ($issues -join [Environment]::NewLine)
    exit 2
}

if ((Get-PropertyValue $solution 'schemaVersion') -ne 1) {
    Add-Issue 'schemaVersion must be 1.'
}

$solutionName = [string](Get-PropertyValue $solution 'name')
if ([string]::IsNullOrWhiteSpace($solutionName)) { Add-Issue 'name is required.' }

$shortCode = [string](Get-PropertyValue $solution 'shortCode')
if ($shortCode -cnotmatch '^[A-Z]{3,4}$') {
    Add-Issue 'shortCode is required and must contain exactly three or four uppercase letters.'
    $shortCode = $null
} else {
    Test-ShortCodePrefix $solutionName 'Solution name'
}

$application = Get-PropertyValue $solution 'application'
$rootCategoryPath = [string](Get-PropertyValue $application 'rootCategoryPath')
if ([string]::IsNullOrWhiteSpace($rootCategoryPath)) {
    Add-Issue 'application.rootCategoryPath is required.'
} else {
    Test-ShortCodeCategoryPath $rootCategoryPath 'Root category leaf'
}
$rootSegments = @($rootCategoryPath.Split([char[]]'\/', [StringSplitOptions]::RemoveEmptyEntries))
$rootLeaf = if ($rootSegments.Count -gt 0) { $rootSegments[-1] } else { $null }
$dataCategoryPath = if ([string]::IsNullOrWhiteSpace($rootCategoryPath)) { $null } else { $rootCategoryPath.TrimEnd([char[]]'\/') + '\Data' }
$adminCategoryPath = if ([string]::IsNullOrWhiteSpace($rootCategoryPath)) { $null } else { $rootCategoryPath.TrimEnd([char[]]'\/') + '\Admin' }

$policies = Get-PropertyValue $solution 'policies'
$dataModelComplexity = ([string](Get-PropertyValue $policies 'dataModelComplexity')).ToLowerInvariant()
if ($dataModelComplexity -notin @('small', 'complex')) {
    Add-Issue 'policies.dataModelComplexity is required and must be small or complex.'
}
if ((Get-PropertyValue $policies 'versionFreeNames') -ne $false) {
    Test-VersionFreeName $solutionName 'Solution name'
    Test-VersionFreeName $rootCategoryPath 'Root category path'
}

$components = Get-PropertyValue $solution 'components'
if ($null -eq $components) { Add-Issue 'components is required.' }

$smartObjects = Get-PropertyValue $components 'smartObjects'
$forms = Get-PropertyValue $components 'forms'
$workflows = @(Get-PropertyValue $components 'workflows')
if ($workflows.Count -eq 1 -and $null -eq $workflows[0]) { $workflows = @() }
if ($null -eq $smartObjects -and $null -eq $forms -and $workflows.Count -eq 0) {
    Add-Issue 'At least one component is required.'
}

$smartObjectsPath = $null
$formsPath = $null
$smartObjectsManifest = $null
$formsManifest = $null
$approvalMatrices = @()
$sqlMasterDetails = @()

if ($null -ne $smartObjects) {
    $smartObjectsPath = Resolve-ComponentManifest $smartObjects 'components.smartObjects'
    if ($null -ne $smartObjectsPath) {
        $smartObjectsManifest = Read-JsonFile $smartObjectsPath 'SmartObjects'
        $planItems.Add([pscustomobject]@{
            order = 1; component = 'smartObjects'; skill = 'k2-sql-smartobjects'
            manifest = $smartObjectsPath; dependsOn = @()
            planCommand = "& '<k2-sql-smartobjects-root>\scripts\k2sql.ps1' plan --manifest '$smartObjectsPath'"
        })
    }
}

if ($null -ne $smartObjectsManifest) {
    Test-ShortCodePrefix ([string](Get-PropertyValue $smartObjectsManifest 'name')) 'SmartObjects manifest name'
    $smartObjectsApplication = Get-PropertyValue $smartObjectsManifest 'application'
    $smartObjectsRoot = [string](Get-PropertyValue $smartObjectsApplication 'rootCategoryPath')
    if ([string]::IsNullOrWhiteSpace($smartObjectsRoot)) {
        Add-Issue 'SmartObjects application.rootCategoryPath is required for a complete solution.'
    } elseif ($smartObjectsRoot -ne $rootCategoryPath) {
        Add-Issue "SmartObjects rootCategoryPath '$smartObjectsRoot' does not match solution root '$rootCategoryPath'."
    }
    $database = Get-PropertyValue $smartObjectsManifest 'database'
    Test-ShortCodePrefix ([string](Get-PropertyValue $database 'name')) 'Database name'
    $smartObjectK2 = Get-PropertyValue $smartObjectsManifest 'k2'
    $serviceInstance = Get-PropertyValue $smartObjectK2 'serviceInstance'
    Test-ShortCodePrefix ([string](Get-PropertyValue $serviceInstance 'systemName')) 'Service Instance system name'
    Test-ShortCodePrefix ([string](Get-PropertyValue $serviceInstance 'displayName')) 'Service Instance display name'
    $sqlVerification = Get-PropertyValue $smartObjectsManifest 'verification'
    foreach ($sqlObject in @(Get-PropertyValue $sqlVerification 'sqlObjects')) {
        $qualifiedName = ([string](Get-PropertyValue $sqlObject 'schema')) + '.' + ([string](Get-PropertyValue $sqlObject 'name'))
        Test-ShortCodePrefix $qualifiedName 'Fully qualified SQL object name'
    }

    $approvalMatrices = @(Get-PropertyValue $smartObjectsManifest 'approvalMatrices')
    if ($approvalMatrices.Count -eq 1 -and $null -eq $approvalMatrices[0]) { $approvalMatrices = @() }
    foreach ($matrix in $approvalMatrices) {
        $matrixCode = [string](Get-PropertyValue $matrix 'matrixCode')
        $matrixName = [string](Get-PropertyValue $matrix 'name')
        $matrixSchema = [string](Get-PropertyValue $matrix 'schema')
        Test-ShortCodePrefix $matrixName 'Approval matrix name'
        Test-ShortCodePrefix $matrixCode 'Approval matrix code'
        Test-ShortCodePrefix ($matrixSchema + '.' + [string](Get-PropertyValue $matrix 'table')) 'Approval matrix table'
        Test-ShortCodePrefix ($matrixSchema + '.' + [string](Get-PropertyValue $matrix 'resolverProcedure')) 'Approval matrix resolver procedure'
        $designerRules = @((Get-PropertyValue $matrix 'rules') | Where-Object { [string](Get-PropertyValue $_ 'approver') -eq '$designer' })
        if ($designerRules.Count -gt 0) {
            $errata.Add([pscustomobject]@{
                id = 'approval-matrix-demo-identities'
                category = 'placeholder'
                severity = 'warning'
                artifact = $matrixName
                requestedAssignees = @($designerRules | ForEach-Object { [string](Get-PropertyValue $_ 'key') })
                effectiveAssignee = '$designer (resolved to the deploying AD user as a K2 identity)'
                description = 'Approval matrix seed rules use the designer identity for test/demo and must be reviewed before production.'
                status = 'open'
            })
        }
    }
    $sqlMasterDetails = @(Get-PropertyValue $smartObjectsManifest 'masterDetails')
    if ($sqlMasterDetails.Count -eq 1 -and $null -eq $sqlMasterDetails[0]) { $sqlMasterDetails = @() }
}

if ($null -ne $forms) {
    $formsPath = Resolve-ComponentManifest $forms 'components.forms'
    if ($null -ne $formsPath) {
        $formsManifest = Read-JsonFile $formsPath 'SmartForms'
        $formDependencies = Get-DependencyNames $forms
        foreach ($dependency in $formDependencies) {
            if ($dependency -ne 'smartObjects') { Add-Issue "components.forms has unknown dependency '$dependency'." }
        }
        if ($null -ne $smartObjects -and $formDependencies -notcontains 'smartObjects') {
            Add-Issue 'components.forms.dependsOn must contain smartObjects when both components are present.'
        }
        $planItems.Add([pscustomobject]@{
            order = 2; component = 'forms'; skill = 'k2-smartforms'
            manifest = $formsPath; dependsOn = $formDependencies
            planCommand = "& '<k2-smartforms-root>\scripts\k2forms.ps1' plan --manifest '$formsPath'"
        })
    }
}

$formNames = @()
if ($null -ne $formsManifest) {
    Test-ShortCodePrefix ([string](Get-PropertyValue $formsManifest 'name')) 'SmartForms manifest name'
    $formsApplication = Get-PropertyValue $formsManifest 'application'
    $formsRoot = [string](Get-PropertyValue $formsApplication 'rootCategoryPath')
    if ($formsRoot -ne $rootCategoryPath) {
        Add-Issue "SmartForms rootCategoryPath '$formsRoot' does not match solution root '$rootCategoryPath'."
    }

    $declaredForms = @(Get-PropertyValue $formsApplication 'forms')
    $declaredViews = @(Get-PropertyValue $formsApplication 'views')
    $lookupSources = @(Get-PropertyValue $formsApplication 'lookups')
    $lookupNames = @($lookupSources | ForEach-Object { [string](Get-PropertyValue $_ 'name') })

    foreach ($lookup in $lookupSources) {
        $lookupName = [string](Get-PropertyValue $lookup 'name')
        Test-SmartObjectPrefix ([string](Get-PropertyValue $lookup 'smartObject')) "Lookup '$lookupName' SmartObject"
        $adminForm = [string](Get-PropertyValue $lookup 'adminForm')
        if (-not [string]::IsNullOrWhiteSpace($adminForm)) {
            $adminDefinition = $declaredForms | Where-Object { [string](Get-PropertyValue $_ 'name') -eq $adminForm } | Select-Object -First 1
            if ($null -eq $adminDefinition) {
                Add-Issue "Lookup '$lookupName' adminForm is not declared: $adminForm"
            } elseif ([string](Get-PropertyValue $adminDefinition 'area') -ne 'admin') {
                Add-Issue "Lookup '$lookupName' adminForm must use area 'admin': $adminForm"
            }
        }
    }

    foreach ($form in $declaredForms) {
        $formName = [string](Get-PropertyValue $form 'name')
        if (-not [string]::IsNullOrWhiteSpace($formName)) { $formNames += $formName }
        Test-ShortCodePrefix $formName 'Form name'
        $formArea = [string](Get-PropertyValue $form 'area')
        if (-not [string]::IsNullOrWhiteSpace($formArea) -and $formArea -notin @('application', 'admin')) {
            Add-Issue "Form '$formName' has unsupported area '$formArea'."
        }
        if ((Get-PropertyValue $policies 'versionFreeNames') -ne $false) {
            Test-VersionFreeName $formName 'Form name'
        }
        if ((Get-PropertyValue $policies 'modernForms') -ne $false -and (Get-PropertyValue $form 'useLegacyTheme') -eq $true) {
            Add-Issue "Form '$formName' enables legacy theme while policies.modernForms is true."
        }
    }

    foreach ($view in $declaredViews) {
        Test-ShortCodePrefix ([string](Get-PropertyValue $view 'name')) 'View name'
        Test-SmartObjectPrefix ([string](Get-PropertyValue $view 'smartObject')) 'View SmartObject system name'
        $viewArea = [string](Get-PropertyValue $view 'area')
        if (-not [string]::IsNullOrWhiteSpace($viewArea) -and $viewArea -notin @('application', 'admin')) {
            Add-Issue "View '$([string](Get-PropertyValue $view 'name'))' has unsupported area '$viewArea'."
        }
        foreach ($control in @(Get-PropertyValue $view 'lookupControls')) {
            if ($null -eq $control) { continue }
            $lookupName = [string](Get-PropertyValue $control 'lookup')
            if ($lookupNames -notcontains $lookupName) {
                Add-Issue "View '$([string](Get-PropertyValue $view 'name'))' references undeclared lookup '$lookupName'."
            }
        }
        if ((Get-PropertyValue $policies 'versionFreeNames') -ne $false) {
            Test-VersionFreeName ([string](Get-PropertyValue $view 'name')) 'View name'
        }
    }


    foreach ($matrix in $approvalMatrices) {
        $matrixSchema = [string](Get-PropertyValue $matrix 'schema')
        $matrixTable = [string](Get-PropertyValue $matrix 'table')
        $matrixSuffix = '_' + $matrixSchema + '_' + $matrixTable
        $matrixAdminViews = @($declaredViews | Where-Object {
            ([string](Get-PropertyValue $_ 'smartObject')).EndsWith($matrixSuffix, [StringComparison]::OrdinalIgnoreCase) -and
            [string](Get-PropertyValue $_ 'area') -eq 'admin'
        })
        if ($matrixAdminViews.Count -lt 2) {
            Add-Issue "Approval matrix '$([string](Get-PropertyValue $matrix 'matrixCode'))' requires Admin capture/list views over its generated table SmartObject."
        }
        $matrixAdminViewNames = @($matrixAdminViews | ForEach-Object { [string](Get-PropertyValue $_ 'name') })
        $matrixAdminForms = @($declaredForms | Where-Object {
            [string](Get-PropertyValue $_ 'area') -eq 'admin' -and
            @((Get-PropertyValue $_ 'views') | Where-Object { $matrixAdminViewNames -contains [string]$_ }).Count -gt 0
        })
        if ($matrixAdminForms.Count -eq 0) {
            Add-Issue "Approval matrix '$([string](Get-PropertyValue $matrix 'matrixCode'))' requires an Admin maintenance form."
        }
    }
}

$masterDetailPolicies = @(Get-PropertyValue $policies 'masterDetails')
if ($masterDetailPolicies.Count -eq 1 -and $null -eq $masterDetailPolicies[0]) { $masterDetailPolicies = @() }
$formMasterDetails = @()
if ($null -ne $formsManifest) {
    $formsApplication = Get-PropertyValue $formsManifest 'application'
    $declaredForms = @(Get-PropertyValue $formsApplication 'forms')
    $declaredViews = @(Get-PropertyValue $formsApplication 'views')
    foreach ($form in $declaredForms) {
        $contract = Get-PropertyValue $form 'masterDetail'
        if ($null -ne $contract) {
            $formMasterDetails += [pscustomobject]@{ Form = $form; Contract = $contract }
        }
    }
}
foreach ($policy in $masterDetailPolicies) {
    $policyName = [string](Get-PropertyValue $policy 'name')
    $policyFormName = [string](Get-PropertyValue $policy 'form')
    $policyMasterView = [string](Get-PropertyValue $policy 'masterView')
    $policyDetailView = [string](Get-PropertyValue $policy 'detailView')
    Test-ShortCodePrefix $policyName 'Master-detail name'
    Test-ShortCodePrefix $policyFormName 'Master-detail form'
    Test-ShortCodePrefix $policyMasterView 'Master-detail master view'
    Test-ShortCodePrefix $policyDetailView 'Master-detail detail view'
    Test-SmartObjectPrefix ([string](Get-PropertyValue $policy 'masterSmartObject')) 'Master-detail master SmartObject'
    Test-SmartObjectPrefix ([string](Get-PropertyValue $policy 'detailSmartObject')) 'Master-detail detail SmartObject'

    $sqlMatch = @($sqlMasterDetails | Where-Object { [string](Get-PropertyValue $_ 'name') -eq $policyName })
    if ($sqlMatch.Count -ne 1) {
        Add-Issue "Master-detail policy '$policyName' must match exactly one SmartObjects masterDetails contract."
    } else {
        $sqlRelationship = $sqlMatch[0]
        if ([string](Get-PropertyValue $sqlRelationship 'masterKey') -ne [string](Get-PropertyValue $policy 'masterKey')) {
            Add-Issue "Master-detail policy '$policyName' masterKey does not match the SQL contract."
        }
        if ([string](Get-PropertyValue $sqlRelationship 'detailKey') -ne [string](Get-PropertyValue $policy 'detailKey')) {
            Add-Issue "Master-detail policy '$policyName' detailKey does not match the SQL contract."
        }
        if ([string](Get-PropertyValue $sqlRelationship 'detailForeignKey') -ne [string](Get-PropertyValue $policy 'foreignKey')) {
            Add-Issue "Master-detail policy '$policyName' foreignKey does not match the SQL contract."
        }
    }

    $formMatch = @($formMasterDetails | Where-Object { [string](Get-PropertyValue $_.Form 'name') -eq $policyFormName })
    if ($formMatch.Count -ne 1) {
        Add-Issue "Master-detail policy '$policyName' must match exactly one SmartForms form contract: $policyFormName"
        continue
    }
    $formContract = $formMatch[0].Contract
    if ([string](Get-PropertyValue $formContract 'masterView') -ne $policyMasterView) {
        Add-Issue "Master-detail policy '$policyName' masterView does not match the SmartForms contract."
    }
    if ([string](Get-PropertyValue $formContract 'masterKeyProperty') -ne [string](Get-PropertyValue $policy 'masterKey')) {
        Add-Issue "Master-detail policy '$policyName' masterKey does not match the SmartForms contract."
    }
    $childMatches = @((Get-PropertyValue $formContract 'details') | Where-Object {
        [string](Get-PropertyValue $_ 'view') -eq $policyDetailView -and
        [string](Get-PropertyValue $_ 'foreignKeyProperty') -eq [string](Get-PropertyValue $policy 'foreignKey')
    })
    if ($childMatches.Count -ne 1) {
        Add-Issue "Master-detail policy '$policyName' detail view/foreign key does not match the SmartForms contract."
    }
    $masterViewDefinition = $declaredViews | Where-Object { [string](Get-PropertyValue $_ 'name') -eq $policyMasterView } | Select-Object -First 1
    $detailViewDefinition = $declaredViews | Where-Object { [string](Get-PropertyValue $_ 'name') -eq $policyDetailView } | Select-Object -First 1
    if ($null -eq $masterViewDefinition -or [string](Get-PropertyValue $masterViewDefinition 'smartObject') -ne [string](Get-PropertyValue $policy 'masterSmartObject')) {
        Add-Issue "Master-detail policy '$policyName' master SmartObject does not match its View."
    }
    if ($null -eq $detailViewDefinition -or [string](Get-PropertyValue $detailViewDefinition 'smartObject') -ne [string](Get-PropertyValue $policy 'detailSmartObject')) {
        Add-Issue "Master-detail policy '$policyName' detail SmartObject does not match its View."
    }
}
foreach ($sqlRelationship in $sqlMasterDetails) {
    $name = [string](Get-PropertyValue $sqlRelationship 'name')
    if (@($masterDetailPolicies | Where-Object { [string](Get-PropertyValue $_ 'name') -eq $name }).Count -eq 0) {
        Add-Issue "SQL master-detail '$name' requires a policies.masterDetails integration contract so the detail UX cannot be omitted."
    }
}
foreach ($formRelationship in $formMasterDetails) {
    $formName = [string](Get-PropertyValue $formRelationship.Form 'name')
    if (@($masterDetailPolicies | Where-Object { [string](Get-PropertyValue $_ 'form') -eq $formName }).Count -eq 0) {
        Add-Issue "SmartForms master-detail form '$formName' requires a policies.masterDetails integration contract."
    }
}

$workflowManifests = @{}
$workflowNames = @()
$workflowIndex = 0
foreach ($workflowComponent in $workflows) {
    $workflowIndex++
    $declaredName = [string](Get-PropertyValue $workflowComponent 'name')
    if ([string]::IsNullOrWhiteSpace($declaredName)) {
        Add-Issue "components.workflows[$workflowIndex].name is required."
        $declaredName = "workflow-$workflowIndex"
    }
    Test-ShortCodePrefix $declaredName 'Workflow component name'
    if ($workflowNames -contains $declaredName) { Add-Issue "Workflow component name is duplicated: $declaredName" }
    $workflowNames += $declaredName

    $workflowPath = Resolve-ComponentManifest $workflowComponent "workflow '$declaredName'"
    if ($null -eq $workflowPath) { continue }
    $workflowManifest = Read-JsonFile $workflowPath "Workflow '$declaredName'"
    $workflowManifests[$declaredName] = [pscustomobject]@{ Path = $workflowPath; Value = $workflowManifest }

    $workflowDependencies = Get-DependencyNames $workflowComponent
    foreach ($dependency in $workflowDependencies) {
        if ($dependency -notin @('smartObjects', 'forms')) {
            Add-Issue "Workflow '$declaredName' has unknown dependency '$dependency'."
        }
    }
    if ($null -ne $smartObjects -and $workflowDependencies -notcontains 'smartObjects') {
        Add-Issue "Workflow '$declaredName' dependsOn must contain smartObjects."
    }
    if ($null -ne $forms -and $workflowDependencies -notcontains 'forms') {
        Add-Issue "Workflow '$declaredName' dependsOn must contain forms."
    }

    $planItems.Add([pscustomobject]@{
        order = 3; component = "workflow:$declaredName"; skill = 'k2-workflows'
        manifest = $workflowPath; dependsOn = $workflowDependencies
        planCommand = "& '<k2-workflows-root>\scripts\k2wf.ps1' plan '$workflowPath'"
    })

    if ($null -ne $workflowManifest) {
        Test-ShortCodePrefix ([string](Get-PropertyValue $workflowManifest 'name')) 'Workflow manifest name'
        $workflowRoot = [string](Get-PropertyValue (Get-PropertyValue $workflowManifest 'application') 'rootCategoryPath')
        if ($workflowRoot -ne $rootCategoryPath) {
            Add-Issue "Workflow '$declaredName' rootCategoryPath '$workflowRoot' does not match solution root '$rootCategoryPath'."
        }
        $workflowApplication = Get-PropertyValue $workflowManifest 'application'
        $workflowCategoryName = [string](Get-PropertyValue $workflowApplication 'workflowCategoryName')
        $expectedWorkflowCategoryName = $rootLeaf + ' WFs'
        if ([string]::IsNullOrWhiteSpace($workflowCategoryName)) {
            Add-Issue "Workflow '$declaredName' application.workflowCategoryName is required."
        } elseif ($workflowCategoryName -in @('Workflow', 'Workflows')) {
            Add-Issue "Workflow '$declaredName' category must not be named Workflow or Workflows."
        } elseif ($workflowCategoryName -cne $expectedWorkflowCategoryName) {
            Add-Issue "Workflow '$declaredName' category must be '$expectedWorkflowCategoryName': $workflowCategoryName"
        }
        $actualWorkflowName = [string](Get-PropertyValue (Get-PropertyValue $workflowManifest 'workflow') 'name')
        Test-ShortCodePrefix $actualWorkflowName 'Workflow name'
        if ($actualWorkflowName -ne $declaredName) {
            Add-Issue "Workflow component name '$declaredName' does not match workflow manifest name '$actualWorkflowName'."
        }
        if ((Get-PropertyValue $policies 'versionFreeNames') -ne $false) {
            Test-VersionFreeName $actualWorkflowName 'Workflow name'
        }
        $workflowDefinition = Get-PropertyValue $workflowManifest 'workflow'
        $statusUpdate = Get-PropertyValue $workflowDefinition 'requestStatusUpdate'
        if ($null -ne $statusUpdate) {
            Test-ShortCodePrefix ([string](Get-PropertyValue $statusUpdate 'name')) 'Workflow status-update step name'
            Test-SmartObjectPrefix ([string](Get-PropertyValue $statusUpdate 'smartObject')) 'Workflow SmartObject system name'
        }
        $emailStep = Get-PropertyValue $workflowDefinition 'email'
        if ($null -ne $emailStep) { Test-ShortCodePrefix ([string](Get-PropertyValue $emailStep 'name')) 'Workflow email step name' }
        $userTask = Get-PropertyValue $workflowDefinition 'userTask'
        if ($null -ne $userTask) {
            Test-ShortCodePrefix ([string](Get-PropertyValue $userTask 'name')) 'Workflow user-task name'
            $requestedAssignees = @(Get-PropertyValue $userTask 'assignees')
            if ($requestedAssignees.Count -eq 0 -or ($requestedAssignees.Count -eq 1 -and $null -eq $requestedAssignees[0])) {
                $requestedAssignees = @('$originator')
            }
            $approvalMatrix = Get-PropertyValue $workflowDefinition 'approvalMatrix'
            if ($null -eq $approvalMatrix) {
                $errata.Add([pscustomobject]@{
                    id = 'workflow-test-demo-originator-routing'
                    category = 'placeholder'
                    severity = 'warning'
                    artifact = $actualWorkflowName
                    requestedAssignees = @($requestedAssignees)
                    effectiveAssignee = '$originator'
                    description = 'Human task assignment is forced to the workflow Originator for testing/demo; requested production routing is not applied.'
                    status = 'open'
                })
            }
            else {
                $matrixCode = [string](Get-PropertyValue $approvalMatrix 'matrixCode')
                Test-SmartObjectPrefix ([string](Get-PropertyValue $approvalMatrix 'smartObject')) 'Workflow approval-matrix resolver SmartObject'
                $matchingMatrix = @($approvalMatrices | Where-Object { [string](Get-PropertyValue $_ 'matrixCode') -eq $matrixCode })
                if ($matchingMatrix.Count -ne 1) {
                    Add-Issue "Workflow '$actualWorkflowName' references approval matrix '$matrixCode', but the SmartObjects manifest does not declare it exactly once."
                }
                elseif ($null -ne $smartObjectsManifest) {
                    $sqlDimensions = @((Get-PropertyValue $matchingMatrix[0] 'dimensions') | ForEach-Object { [string](Get-PropertyValue $_ 'name') + 'Input' })
                    foreach ($mapping in @(Get-PropertyValue $approvalMatrix 'dimensions')) {
                        $inputProperty = [string](Get-PropertyValue $mapping 'inputProperty')
                        if ($sqlDimensions -notcontains $inputProperty) {
                            Add-Issue "Workflow '$actualWorkflowName' maps unknown approval-matrix resolver input '$inputProperty'."
                        }
                    }
                }
            }
        }
        $workflowSmartForms = Get-PropertyValue $workflowDefinition 'smartForms'
        if ($null -ne $workflowSmartForms) {
            Test-ShortCodePrefix ([string](Get-PropertyValue $workflowSmartForms 'form')) 'Workflow SmartForm name'
            Test-ShortCodePrefix ([string](Get-PropertyValue $workflowSmartForms 'startState')) 'Workflow Start state name'
            Test-ShortCodePrefix ([string](Get-PropertyValue $workflowSmartForms 'taskState')) 'Workflow Task state name'
        }
    }
}

$entries = @(Get-PropertyValue $policies 'workflowEntries')
if ($entries.Count -eq 1 -and $null -eq $entries[0]) { $entries = @() }
$entryWorkflowNames = @()
foreach ($entry in $entries) {
    $workflowName = [string](Get-PropertyValue $entry 'workflow')
    $formName = [string](Get-PropertyValue $entry 'form')
    $ownership = ([string](Get-PropertyValue $entry 'formOwnership')).ToLowerInvariant()
    $requestedDefault = ([string](Get-PropertyValue $entry 'startStateDefault')).ToLowerInvariant()

    if ($workflowNames -notcontains $workflowName) { Add-Issue "workflowEntries references unknown workflow '$workflowName'." }
    if ($entryWorkflowNames -contains $workflowName) { Add-Issue "workflowEntries contains duplicate policy for workflow '$workflowName'." }
    $entryWorkflowNames += $workflowName
    if ($formNames -notcontains $formName) { Add-Issue "workflowEntries references unknown form '$formName'." }
    if ($ownership -notin @('dedicated', 'shared')) { Add-Issue "Workflow '$workflowName' formOwnership must be dedicated or shared." }
    if ($requestedDefault -notin @('auto', 'true', 'false')) { Add-Issue "Workflow '$workflowName' startStateDefault must be auto, true, or false." }

    $resolvedDefault = $null
    if ($requestedDefault -eq 'true') { $resolvedDefault = $true }
    elseif ($requestedDefault -eq 'false') { $resolvedDefault = $false }
    elseif ($ownership -eq 'dedicated') { $resolvedDefault = $true }
    elseif ($ownership -eq 'shared') { Add-Issue "Workflow '$workflowName' uses startStateDefault=auto on a shared form; choose true or false explicitly." }

    $resolvedPolicies.Add([pscustomobject]@{
        workflow = $workflowName; form = $formName; formOwnership = $ownership
        requestedStartStateDefault = $requestedDefault; resolvedStartStateDefault = $resolvedDefault
        taskStateDefault = $false
    })

    if ($workflowManifests.ContainsKey($workflowName) -and $null -ne $workflowManifests[$workflowName].Value) {
        $workflowDefinition = Get-PropertyValue $workflowManifests[$workflowName].Value 'workflow'
        $smartForms = Get-PropertyValue $workflowDefinition 'smartForms'
        $manifestForm = [string](Get-PropertyValue $smartForms 'form')
        if ($manifestForm -ne $formName) {
            Add-Issue "Workflow '$workflowName' integrates form '$manifestForm', not policy form '$formName'."
        }
        if ($null -ne $resolvedDefault) {
            $manifestDefault = Get-PropertyValue $smartForms 'makeStartStateDefault'
            if ($manifestDefault -isnot [bool] -or $manifestDefault -ne $resolvedDefault) {
                Add-Issue "Workflow '$workflowName' must set workflow.smartForms.makeStartStateDefault=$($resolvedDefault.ToString().ToLowerInvariant()) to satisfy solution policy."
            }
        }
    }
}

foreach ($workflowName in $workflowNames) {
    if (-not $workflowManifests.ContainsKey($workflowName) -or $null -eq $workflowManifests[$workflowName].Value) { continue }
    $workflowDefinition = Get-PropertyValue $workflowManifests[$workflowName].Value 'workflow'
    if ($null -ne (Get-PropertyValue $workflowDefinition 'smartForms') -and $entryWorkflowNames -notcontains $workflowName) {
        Add-Issue "SmartForms-integrated workflow '$workflowName' requires a policies.workflowEntries item."
    }
}

$result = [pscustomobject]@{
    valid = ($issues.Count -eq 0)
    solution = $solutionName
    shortCode = $shortCode
    manifest = $manifestPath
    rootCategoryPath = $rootCategoryPath
    dataCategoryPath = $dataCategoryPath
    adminCategoryPath = $adminCategoryPath
    dataModelComplexity = $dataModelComplexity
    approvalMatrices = @($approvalMatrices | ForEach-Object {
        [pscustomobject]@{
            name = [string](Get-PropertyValue $_ 'name')
            matrixCode = [string](Get-PropertyValue $_ 'matrixCode')
            dimensions = @((Get-PropertyValue $_ 'dimensions') | ForEach-Object { [string](Get-PropertyValue $_ 'name') })
            stages = @((Get-PropertyValue $_ 'rules') | ForEach-Object { [int](Get-PropertyValue $_ 'stage') } | Sort-Object -Unique)
            seededRules = @((Get-PropertyValue $_ 'rules')).Count
        }
    })
    masterDetails = @($masterDetailPolicies)
    issues = @($issues)
    resolvedPolicies = @($resolvedPolicies)
    errata = @($errata)
    plan = @($planItems | Sort-Object order, component)
}

if ($Output -eq 'json') {
    $result | ConvertTo-Json -Depth 8
}
else {
    Write-Output "K2 solution: $solutionName"
    Write-Output "Short code: $shortCode"
    Write-Output "Root category: $rootCategoryPath"
    Write-Output "SmartObject category: $dataCategoryPath"
    Write-Output "Administration category: $adminCategoryPath"
    Write-Output "Data model complexity: $dataModelComplexity"
    foreach ($matrix in $result.approvalMatrices) {
        Write-Output ("Approval matrix: {0} ({1} rule(s), stages={2}, dimensions={3})" -f $matrix.matrixCode, $matrix.seededRules, ($matrix.stages -join ','), (($matrix.dimensions + @('Amount')) -join ','))
    }
    foreach ($relationship in $result.masterDetails) {
        Write-Output ("Master-detail: {0} ({1}.{2} -> {3}.{4}; form={5})" -f $relationship.name, $relationship.masterSmartObject, $relationship.masterKey, $relationship.detailSmartObject, $relationship.foreignKey, $relationship.form)
    }
    if ($Command -eq 'plan') {
        Write-Output 'Dependency-ordered specialist plan:'
        foreach ($item in $result.plan) {
            Write-Output ('  {0}. {1} -> ${2} ({3})' -f $item.order, $item.component, $item.skill, $item.planCommand)
        }
        if ($result.resolvedPolicies.Count -gt 0) {
            Write-Output 'Resolved workflow entry policies:'
            foreach ($policy in $result.resolvedPolicies) {
                Write-Output ("  {0}: form={1}, ownership={2}, startDefault={3}, taskDefault=false" -f $policy.workflow, $policy.form, $policy.formOwnership, $policy.resolvedStartStateDefault)
            }
        }
        if ($result.errata.Count -gt 0) {
            Write-Output 'Errata preview:'
            foreach ($item in $result.errata) {
                Write-Output ("  [{0}] {1}: requested={2}; effective={3}" -f $item.category, $item.artifact, ($item.requestedAssignees -join ', '), $item.effectiveAssignee)
            }
        }
    }
    if ($issues.Count -gt 0) {
        Write-Output 'Validation issues:'
        foreach ($issue in $issues) { Write-Output "  - $issue" }
    }
    else {
        Write-Output 'Validation passed.'
    }
}

if ($issues.Count -gt 0) { exit 1 }
