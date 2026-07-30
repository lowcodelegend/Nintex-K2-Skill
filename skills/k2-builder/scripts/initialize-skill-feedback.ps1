[CmdletBinding(DefaultParameterSetName = 'Initialize', SupportsShouldProcess = $true, PositionalBinding = $false)]
param(
    [Parameter(ParameterSetName = 'Initialize')]
    [string]$ProjectRoot = (Get-Location).Path,

    [Parameter(ParameterSetName = 'Initialize')]
    [string[]]$SkillOwner = @('k2-builder'),

    [Parameter(Mandatory = $true, ParameterSetName = 'SelfTest')]
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

$skillRoot = Split-Path -Parent $PSScriptRoot
$templateRoot = Join-Path $skillRoot 'assets\skill-feedback'
$agentsTemplatePath = Join-Path $templateRoot 'AGENTS.fragment.md'
$learningsTemplatePath = Join-Path $templateRoot 'skill-learnings.template.md'
$startMarker = '<!-- k2-skills:feedback-loop:v1:start -->'
$endMarker = '<!-- k2-skills:feedback-loop:v1:end -->'

function Get-Utf8Text {
    param([Parameter(Mandatory = $true)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Feedback-loop template is missing: $Path"
    }
    return [IO.File]::ReadAllText($Path)
}

function Write-Utf8Text {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )
    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    [IO.File]::WriteAllText(
        $Path,
        $Content.TrimEnd() + [Environment]::NewLine,
        [Text.UTF8Encoding]::new($false))
}

function Get-OwnerText {
    param([string[]]$Owners)
    $normalized = @($Owners |
        ForEach-Object {
            $value = ([string]$_).Trim()
            if ([string]::IsNullOrWhiteSpace($value)) { return }
            if ($value[0] -ne '$') { $value = '$' + $value }
            $value
        } |
        Sort-Object -Unique)
    if ($normalized.Count -eq 0) { throw 'SkillOwner must contain at least one skill name.' }
    return $normalized -join ', '
}

function Initialize-FeedbackLoop {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string[]]$Owners
    )
    $resolvedRoot = [IO.Path]::GetFullPath($Root)
    if (-not (Test-Path -LiteralPath $resolvedRoot -PathType Container)) {
        throw "ProjectRoot must be an existing directory: $resolvedRoot"
    }

    $ownerText = Get-OwnerText $Owners
    $agentsFragment = (Get-Utf8Text $agentsTemplatePath).Trim()
    if (-not $agentsFragment.Contains($startMarker) -or
        -not $agentsFragment.Contains($endMarker)) {
        throw 'AGENTS feedback-loop template has invalid managed markers.'
    }

    $agentsPath = Join-Path $resolvedRoot 'AGENTS.md'
    $agentsStatus = 'unchanged'
    $existingAgents = if (Test-Path -LiteralPath $agentsPath -PathType Leaf) {
        [IO.File]::ReadAllText($agentsPath)
    } else {
        $null
    }
    if ($null -eq $existingAgents) {
        $newAgents = '# Project agent instructions' + [Environment]::NewLine * 2 + $agentsFragment
        if ($PSCmdlet.ShouldProcess($agentsPath, 'Create project AGENTS.md feedback rule')) {
            Write-Utf8Text $agentsPath $newAgents
            $agentsStatus = 'created'
        }
    } else {
        $managedPattern = '(?ms)' +
            [Regex]::Escape($startMarker) +
            '.*?' +
            [Regex]::Escape($endMarker)
        $hasStartMarker = $existingAgents.Contains($startMarker)
        $hasEndMarker = $existingAgents.Contains($endMarker)
        if ($hasStartMarker -xor $hasEndMarker) {
            throw 'AGENTS.md contains an incomplete K2 skill-feedback managed block.'
        }
        if ([Regex]::IsMatch($existingAgents, $managedPattern)) {
            $newAgents = [Regex]::Replace(
                $existingAgents,
                $managedPattern,
                [Text.RegularExpressions.MatchEvaluator]{ param($match) $agentsFragment },
                1)
        } else {
            $newAgents = $existingAgents.TrimEnd() +
                [Environment]::NewLine * 2 +
                $agentsFragment
        }
        if ($newAgents.TrimEnd() -cne $existingAgents.TrimEnd() -and
            $PSCmdlet.ShouldProcess($agentsPath, 'Update managed AGENTS.md feedback rule')) {
            Write-Utf8Text $agentsPath $newAgents
            $agentsStatus = 'updated'
        }
    }

    $learningsPath = Join-Path $resolvedRoot 'docs\skill-learnings.md'
    $learningsStatus = 'unchanged'
    if (-not (Test-Path -LiteralPath $learningsPath -PathType Leaf)) {
        $today = [DateTime]::UtcNow.ToString('yyyy-MM-dd')
        $learnings = (Get-Utf8Text $learningsTemplatePath).
            Replace('{{INITIALIZED_DATE}}', $today).
            Replace('{{SKILL_OWNERS}}', $ownerText)
        if ($PSCmdlet.ShouldProcess($learningsPath, 'Create project skill-learning log')) {
            Write-Utf8Text $learningsPath $learnings
            $learningsStatus = 'created'
        }
    } else {
        $existingLearnings = [IO.File]::ReadAllText($learningsPath)
        $ownerPattern = '(?m)^- Active skill owners:[ \t]*(?<owners>[^\r\n]+?)[ \t]*$'
        $ownerMatch = [Regex]::Match($existingLearnings, $ownerPattern)
        if ($ownerMatch.Success) {
            $existingOwners = @($ownerMatch.Groups['owners'].Value.Split(',') |
                ForEach-Object { ([string]$_).Trim().TrimStart('$') })
            $mergedOwnerText = Get-OwnerText (@($Owners) + $existingOwners)
            $newLearnings = [Regex]::Replace(
                $existingLearnings,
                $ownerPattern,
                [Text.RegularExpressions.MatchEvaluator]{
                    param($match)
                    '- Active skill owners: ' + $mergedOwnerText
                },
                1)
            if ($newLearnings.TrimEnd() -cne $existingLearnings.TrimEnd() -and
                $PSCmdlet.ShouldProcess($learningsPath, 'Add active skill owner to learning log')) {
                Write-Utf8Text $learningsPath $newLearnings
                $learningsStatus = 'updated'
            }
        }
    }

    return [ordered]@{
        schemaVersion = 1
        projectRoot = $resolvedRoot
        agents = [ordered]@{ path = $agentsPath; status = $agentsStatus }
        learnings = [ordered]@{ path = $learningsPath; status = $learningsStatus }
        skillOwners = $ownerText
    }
}

function Invoke-SelfTest {
    $testRoot = Join-Path ([IO.Path]::GetTempPath()) (
        'K2SkillFeedback-' + [Guid]::NewGuid().ToString('N'))
    try {
        New-Item -ItemType Directory -Path $testRoot | Out-Null
        $agentsPath = Join-Path $testRoot 'AGENTS.md'
        Write-Utf8Text $agentsPath "# Existing project rule`r`n`r`n- Preserve me."

        $first = Initialize-FeedbackLoop $testRoot @('k2-smartforms', 'k2-builder')
        $second = Initialize-FeedbackLoop $testRoot @('k2-builder', 'k2-smartforms')
        $third = Initialize-FeedbackLoop $testRoot @('k2-workflows')
        $agents = [IO.File]::ReadAllText($agentsPath)
        $learningsPath = Join-Path $testRoot 'docs\skill-learnings.md'
        $learnings = [IO.File]::ReadAllText($learningsPath)

        if ($first.agents.status -ne 'updated' -or
            $first.learnings.status -ne 'created' -or
            $second.agents.status -ne 'unchanged' -or
            $second.learnings.status -ne 'unchanged' -or
            $third.learnings.status -ne 'updated') {
            throw 'Feedback-loop idempotency self-test failed.'
        }
        if (-not $agents.Contains('- Preserve me.') -or
            [Regex]::Matches($agents, [Regex]::Escape($startMarker)).Count -ne 1) {
            throw 'AGENTS preservation self-test failed.'
        }
        if (-not $learnings.Contains('$k2-workflows')) {
            throw 'Learning-log active-owner merge self-test failed.'
        }
        foreach ($required in @(
            'Next learning ID',
            'Affected skill owners',
            'Observed behavior',
            'Evidence',
            'Recommended skill change',
            'Acceptance criteria',
            'Feedback history')) {
            if (-not $learnings.Contains($required)) {
                throw "Learning-log contract self-test failed: $required"
            }
        }
        [pscustomobject]@{
            passed = $true
            checks = @(
                'existing-AGENTS-preserved',
                'managed-rule-idempotent',
                'learning-log-never-overwritten',
                'active-skill-owner-merged',
                'stable-learning-contract')
        } | ConvertTo-Json -Depth 5
    } finally {
        if (Test-Path -LiteralPath $testRoot) {
            $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
            $resolved = [IO.Path]::GetFullPath($testRoot).TrimEnd('\')
            if (-not $resolved.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
                throw "Feedback-loop self-test cleanup escaped the temporary root: $resolved"
            }
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
    }
}

if ($SelfTest) {
    Invoke-SelfTest
    exit 0
}

Initialize-FeedbackLoop $ProjectRoot $SkillOwner | ConvertTo-Json -Depth 6
