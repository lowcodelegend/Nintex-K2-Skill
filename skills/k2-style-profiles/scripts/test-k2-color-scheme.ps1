[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Css,
    [string]$VariablesCss
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

function Resolve-VariablesCss {
    if (-not [string]::IsNullOrWhiteSpace($VariablesCss)) {
        return (Resolve-Path -LiteralPath $VariablesCss).Path
    }
    $candidates = @(
        'C:\Program Files\K2\K2 smartforms Runtime\Styles\Themes\_Dynamic\Variables_Dynamic.css',
        'C:\Program Files\K2\K2 smartforms Designer\Styles\Themes\_Dynamic\Variables_Dynamic.css'
    )
    $match = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $match) {
        throw 'K2 Variables_Dynamic.css was not found. Supply -VariablesCss from the target K2 Runtime installation.'
    }
    return (Resolve-Path -LiteralPath $match).Path
}

function Test-K2ColourVariable([string]$Name) {
    if ($Name.StartsWith('k2sp-', [StringComparison]::OrdinalIgnoreCase)) { return $false }
    return $Name -match '(?i)(^k2-(main-accent|content-background|panel-background|error)-color$|-color(?:-|$)|-shadow(?:-color)?$|-effect$|^chart-series-\d+$)'
}

function Get-NormalizedSelector([string]$Selector, [switch]$StripRuntimeGuard) {
    $value = [regex]::Replace($Selector.Trim(), '\s+', ' ')
    $value = [regex]::Replace($value, '\s*([>+~])\s*', '$1')
    if ($StripRuntimeGuard) {
        $value = $value -replace '^html:not\(\.designer\)\s+', ''
    }
    return $value
}

function Get-Contract([string]$Text, [bool]$IsGenerated) {
    $variables = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $pairs = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $unguarded = [Collections.Generic.List[string]]::new()
    foreach ($block in [regex]::Matches($Text, '(?s)(?<selector>[^{}]+)\{(?<body>[^{}]*)\}')) {
        $declarations = @([regex]::Matches($block.Groups['body'].Value, '(?m)--(?<name>[A-Za-z0-9_-]+)\s*:\s*(?<value>[^;]+?)(?:;|$)') |
            Where-Object { Test-K2ColourVariable $_.Groups['name'].Value })
        if ($declarations.Count -eq 0) { continue }
        $selectors = @($block.Groups['selector'].Value -split ',' | ForEach-Object { Get-NormalizedSelector $_ -StripRuntimeGuard:$IsGenerated })
        if ($IsGenerated) {
            foreach ($raw in @($block.Groups['selector'].Value -split ',')) {
                $normalized = Get-NormalizedSelector $raw
                if (-not $normalized.StartsWith('html:not(.designer) ', [StringComparison]::Ordinal)) {
                    $unguarded.Add($normalized)
                }
            }
        }
        foreach ($declaration in $declarations) {
            $name = $declaration.Groups['name'].Value
            $null = $variables.Add($name)
            foreach ($selector in $selectors) { $null = $pairs.Add("$selector|$name") }
        }
    }
    return [pscustomobject]@{ Variables = $variables; Pairs = $pairs; Unguarded = $unguarded }
}

$sourcePath = Resolve-VariablesCss
$cssPath = (Resolve-Path -LiteralPath $Css).Path
$expected = Get-Contract ([IO.File]::ReadAllText($sourcePath)) $false
$actual = Get-Contract ([IO.File]::ReadAllText($cssPath)) $true

if ($expected.Variables.Count -lt 100) {
    throw "Only $($expected.Variables.Count) K2 colour variables were found; the supplied variable contract is not credible."
}
$missingVariables = @($expected.Variables | Where-Object { -not $actual.Variables.Contains($_) } | Sort-Object)
$missingPairs = @($expected.Pairs | Where-Object { -not $actual.Pairs.Contains($_) } | Sort-Object)
$unguarded = @($actual.Unguarded | Sort-Object -Unique)
$result = [ordered]@{
    css = $cssPath
    variablesCss = $sourcePath
    expectedColourVariables = $expected.Variables.Count
    coveredColourVariables = $expected.Variables.Count - $missingVariables.Count
    expectedContextualDeclarations = $expected.Pairs.Count
    coveredContextualDeclarations = $expected.Pairs.Count - $missingPairs.Count
    runtimeGuarded = $unguarded.Count -eq 0
    missingVariables = @($missingVariables | Select-Object -First 50)
    missingContextualDeclarations = @($missingPairs | Select-Object -First 50)
    unguardedSelectors = @($unguarded | Select-Object -First 50)
    passed = $missingVariables.Count -eq 0 -and $missingPairs.Count -eq 0 -and $unguarded.Count -eq 0
}
$result | ConvertTo-Json -Depth 6
if (-not $result.passed) { exit 1 }
