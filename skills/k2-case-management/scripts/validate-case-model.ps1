[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$Manifest)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

function Add-ValidationError([Collections.Generic.List[string]]$Errors, [string]$Message) {
    $Errors.Add($Message)
}

try {
    $path = (Resolve-Path -LiteralPath $Manifest).Path
    $document = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
} catch {
    Write-Error "Unable to load JSON-compatible YAML manifest '$Manifest': $($_.Exception.Message)"
    exit 2
}

$errors = [Collections.Generic.List[string]]::new()
foreach ($property in @('case_type', 'stages', 'transitions')) {
    if (-not ($document.PSObject.Properties.Name -contains $property)) {
        Add-ValidationError $errors "$property is required"
    }
}
if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

$caseType = $document.case_type
foreach ($field in @('code', 'name', 'initial_stage', 'retention_code', 'configuration_version')) {
    if (-not ($caseType.PSObject.Properties.Name -contains $field) -or [string]::IsNullOrWhiteSpace([string]$caseType.$field)) {
        Add-ValidationError $errors "case_type.$field is required"
    }
}

$stages = @($document.stages)
$transitions = @($document.transitions)
if ($stages.Count -eq 0) { Add-ValidationError $errors 'stages must be a non-empty list' }
$stageByCode = @{}
foreach ($stage in $stages) {
    $code = [string]$stage.code
    if ([string]::IsNullOrWhiteSpace($code)) { Add-ValidationError $errors 'stage code is required'; continue }
    if ($stageByCode.ContainsKey($code)) { Add-ValidationError $errors "duplicate stage code: $code" }
    $stageByCode[$code] = $stage
    if ([string]::IsNullOrWhiteSpace([string]$stage.workflow)) { Add-ValidationError $errors "stage $code requires workflow" }
}
if (-not $stageByCode.ContainsKey([string]$caseType.initial_stage)) {
    Add-ValidationError $errors "initial stage does not exist: $($caseType.initial_stage)"
}
if (@($stages | Where-Object { $_.terminal -eq $true }).Count -eq 0) {
    Add-ValidationError $errors 'at least one stage must set terminal: true'
}

$outgoing = @{}; $graph = @{}; $seen = @{}
foreach ($code in $stageByCode.Keys) { $outgoing[$code] = 0; $graph[$code] = [Collections.Generic.List[string]]::new() }
foreach ($transition in $transitions) {
    $source = [string]$transition.from; $outcome = [string]$transition.outcome; $target = [string]$transition.to
    if ([string]::IsNullOrWhiteSpace($source)) { Add-ValidationError $errors 'transition from is required' }
    if ([string]::IsNullOrWhiteSpace($outcome)) { Add-ValidationError $errors 'transition outcome is required' }
    if ([string]::IsNullOrWhiteSpace($target)) { Add-ValidationError $errors 'transition to is required' }
    $identity = "$source|$outcome|$target"
    if ($seen.ContainsKey($identity)) { Add-ValidationError $errors "duplicate transition: $source/$outcome/$target" } else { $seen[$identity] = $true }
    if (-not $stageByCode.ContainsKey($source)) { Add-ValidationError $errors "transition source does not exist: $source" }
    if (-not $stageByCode.ContainsKey($target)) { Add-ValidationError $errors "transition destination does not exist: $target" }
    if ($stageByCode.ContainsKey($source) -and $stageByCode.ContainsKey($target)) {
        $outgoing[$source]++; $graph[$source].Add($target)
        $reentry = $transition.PSObject.Properties.Name -contains 'reentry' -and $transition.reentry -eq $true
        $reopen = $transition.PSObject.Properties.Name -contains 'reopen' -and $transition.reopen -eq $true
        if ($stageByCode[$source].terminal -eq $true -and -not $reopen) { Add-ValidationError $errors "terminal stage $source has a transition not marked reopen" }
        if ([int]$stageByCode[$target].sequence -le [int]$stageByCode[$source].sequence -and -not $reentry -and -not $reopen) {
            Add-ValidationError $errors "cyclic/backward transition $source->$target must set reentry or reopen"
        }
    }
}
foreach ($code in $stageByCode.Keys) {
    if ($stageByCode[$code].terminal -ne $true -and $outgoing[$code] -eq 0) { Add-ValidationError $errors "nonterminal stage has no possible exit: $code" }
}

if ($stageByCode.ContainsKey([string]$caseType.initial_stage)) {
    $reachable = @{}; $pending = [Collections.Generic.Stack[string]]::new(); $pending.Push([string]$caseType.initial_stage)
    while ($pending.Count -gt 0) {
        $code = $pending.Pop(); if ($reachable.ContainsKey($code)) { continue }; $reachable[$code] = $true
        foreach ($target in $graph[$code]) { if (-not $reachable.ContainsKey($target)) { $pending.Push($target) } }
    }
    foreach ($code in $stageByCode.Keys) { if (-not $reachable.ContainsKey($code)) { Add-ValidationError $errors "unreachable stage: $code" } }
}

if ($errors.Count -gt 0) {
    foreach ($validationError in $errors) { Write-Output "ERROR: $validationError" }
    Write-Output "Validation failed with $($errors.Count) error(s)."
    exit 1
}
Write-Output "Valid case-type design manifest: $path"
exit 0
