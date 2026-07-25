[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Output,
    [string]$VariablesCss,
    [string]$Palette,
    [string]$SchemeName = 'K2 modern colour scheme',
    [string]$Accent = '#1769c2',
    [string]$AccentStrong = '#123f73',
    [string]$AccentSoft = '#dceeff',
    [string]$AccentSubtle = '#f3f9ff',
    [string]$OnAccent = '#ffffff',
    [string]$Page = '#f3f9ff',
    [string]$Surface = '#ffffff',
    [string]$SurfaceAlt = '#f7fafc',
    [string]$Text = '#10233c',
    [string]$Muted = '#52677f',
    [string]$Border = '#c9dff3',
    [string]$Focus = '#ffbf47',
    [string]$Danger = '#b42318',
    [string]$DangerSoft = '#fee4e2',
    [string]$Warning = '#b54708',
    [string]$WarningSoft = '#fef0c7',
    [string]$Success = '#067647',
    [string]$SuccessSoft = '#dcfae6',
    [string]$DisabledSurface = '#eef2f6',
    [string]$DisabledText = '#8091a5',
    [string]$Shadow = '0 10px 28px rgba(11, 36, 71, 0.10)',
    [string[]]$ChartSeries = @(
        '#1769c2', '#067647', '#b54708', '#7a5af8',
        '#c11574', '#0e7090', '#b42318', '#4e5ba6',
        '#027a48', '#93370d', '#5925dc', '#c01048'
    )
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

if (-not [string]::IsNullOrWhiteSpace($Palette)) {
    $palettePath = (Resolve-Path -LiteralPath $Palette).Path
    $paletteDocument = Get-Content -LiteralPath $palettePath -Raw | ConvertFrom-Json
    $supported = @(
        'schemeName', 'accent', 'accentStrong', 'accentSoft', 'accentSubtle', 'onAccent',
        'page', 'surface', 'surfaceAlt', 'text', 'muted', 'border', 'focus', 'danger',
        'dangerSoft', 'warning', 'warningSoft', 'success', 'successSoft',
        'disabledSurface', 'disabledText', 'shadow', 'chartSeries'
    )
    $unknown = @($paletteDocument.PSObject.Properties.Name | Where-Object { $_ -notin $supported })
    if ($unknown.Count) { throw "Unsupported palette field(s): $($unknown -join ', ')" }
    foreach ($property in $paletteDocument.PSObject.Properties) {
        switch ($property.Name) {
            'schemeName' { $SchemeName = [string]$property.Value }
            'accent' { $Accent = [string]$property.Value }
            'accentStrong' { $AccentStrong = [string]$property.Value }
            'accentSoft' { $AccentSoft = [string]$property.Value }
            'accentSubtle' { $AccentSubtle = [string]$property.Value }
            'onAccent' { $OnAccent = [string]$property.Value }
            'page' { $Page = [string]$property.Value }
            'surface' { $Surface = [string]$property.Value }
            'surfaceAlt' { $SurfaceAlt = [string]$property.Value }
            'text' { $Text = [string]$property.Value }
            'muted' { $Muted = [string]$property.Value }
            'border' { $Border = [string]$property.Value }
            'focus' { $Focus = [string]$property.Value }
            'danger' { $Danger = [string]$property.Value }
            'dangerSoft' { $DangerSoft = [string]$property.Value }
            'warning' { $Warning = [string]$property.Value }
            'warningSoft' { $WarningSoft = [string]$property.Value }
            'success' { $Success = [string]$property.Value }
            'successSoft' { $SuccessSoft = [string]$property.Value }
            'disabledSurface' { $DisabledSurface = [string]$property.Value }
            'disabledText' { $DisabledText = [string]$property.Value }
            'shadow' { $Shadow = [string]$property.Value }
            'chartSeries' { $ChartSeries = @($property.Value | ForEach-Object { [string]$_ }) }
        }
    }
}
if ($ChartSeries.Count -lt 1) { throw 'Palette chartSeries must contain at least one colour.' }

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

function Get-MappedValue([string]$Name, [bool]$SelectedContext) {
    $n = $Name.ToLowerInvariant()
    if ($n -match '^chart-series-(\d+)$') {
        return "var(--k2sp-chart-$([int]$Matches[1] % $ChartSeries.Count))"
    }
    if ($n -match '-effect$') { return 'none' }
    if ($n -match 'shadow') { return 'var(--k2sp-shadow)' }

    $background = $n -match 'background-color'
    $borderColour = $n -match 'border-color'
    $textColour = $n -match 'text-color'
    $iconColour = $n -match '(^|-)icon-|icon-color'
    $transparentBase = $n -match '^(quiet-button|help-button|icon-button|toolbar|toolbar-button|tab|segmented-tab|list-item|list-add-row|view-header-button|input-button|file-upload|menu-item-disabled|chart-header).*background-color$'

    if ($n -match '(error|destructive|attention|required-indicator)') {
        if ($textColour -and $n -match '(destructive-button|tooltip-error)') { return 'var(--k2sp-on-accent)' }
        if ($background -and $n -notmatch '(button|badge|progress-step|tooltip)') { return 'var(--k2sp-danger-soft)' }
        if ($textColour -or $iconColour) { return 'var(--k2sp-danger)' }
        return 'var(--k2sp-danger)'
    }
    if ($n -match 'warning') {
        if ($background -and $n -notmatch '^badge-') { return 'var(--k2sp-warning-soft)' }
        return 'var(--k2sp-warning)'
    }
    if ($n -match '(success|completed)') {
        if ($textColour -or $iconColour) { return 'var(--k2sp-success)' }
        return 'var(--k2sp-success)'
    }
    if ($n -match '(disabled|readonly)') {
        if ($background) { return 'var(--k2sp-disabled-surface)' }
        if ($borderColour) { return 'var(--k2sp-border)' }
        return 'var(--k2sp-disabled-text)'
    }

    if ($SelectedContext) {
        if ($n -match '^input-' -and $background) { return 'var(--k2sp-surface)' }
        if ($n -match '^input-' -and ($textColour -or $iconColour)) { return 'var(--k2sp-text)' }
        if ($textColour -or $iconColour -or $n -match '(^|-)color$') { return 'var(--k2sp-on-accent)' }
        if ($background) { return 'var(--k2sp-accent-strong)' }
        if ($borderColour) { return 'var(--k2sp-on-accent)' }
    }

    if ($n -match '(focus|highlight)') {
        if ($borderColour) { return 'var(--k2sp-focus)' }
        if ($background) { return 'var(--k2sp-accent-soft)' }
        if ($textColour) {
            if ($n -match 'highlight-text') { return 'var(--k2sp-on-accent)' }
            return 'var(--k2sp-text)'
        }
    }
    if ($n -match '(selected|toggleon|toggle-background|checked|in-progress|main-accent|primary-button|accent-color)') {
        if ($textColour -and $n -notmatch '(hyperlink|heading|title)') { return 'var(--k2sp-on-accent)' }
        if ($background -or $borderColour -or $n -match '(checked-color|accent-color)$') { return 'var(--k2sp-accent)' }
    }
    if ($n -match '(hyperlink|ratings-checked|radio-checked|checkbox-checked)') {
        return 'var(--k2sp-accent)'
    }
    if ($n -match 'hover') {
        if ($background) { return 'var(--k2sp-accent-soft)' }
        if ($borderColour) { return 'var(--k2sp-accent)' }
        if ($textColour -or $iconColour) { return 'var(--k2sp-accent-strong)' }
    }

    if ($iconColour) {
        if ($n -match '(secondary|muted)') { return 'var(--k2sp-muted)' }
        return 'var(--k2sp-text)'
    }
    if ($borderColour -and $n -match '^button-border') { return 'var(--k2sp-accent)' }
    if ($borderColour) { return 'var(--k2sp-border)' }
    if ($background) {
        if ($transparentBase) { return 'transparent' }
        if ($n -match 'view-header-background') { return 'var(--k2sp-accent-strong)' }
        if ($n -match '(column-header|multi-select-header|list-box-header|worklist-header|tab-body|segmented-tab-bar)') { return 'var(--k2sp-accent-soft)' }
        if ($n -match '(even-zebra|surface-alt)') { return 'var(--k2sp-surface-alt)' }
        if ($n -match '^page-background') { return 'var(--k2sp-page)' }
        if ($n -match '^button-background') { return 'var(--k2sp-accent)' }
        return 'var(--k2sp-surface)'
    }
    if ($textColour -or $n -match '(^|-)color(?:-\d+)?$') {
        if ($n -match '(view-header-text|^button-text)') { return 'var(--k2sp-on-accent)' }
        if ($n -match '(page-title|page-subtitle|heading[1-4])') { return 'var(--k2sp-accent-strong)' }
        if ($n -match '(watermark|description|day-header|secondary)') { return 'var(--k2sp-muted)' }
        return 'var(--k2sp-text)'
    }
    throw "No colour mapping exists for --$Name."
}

$sourcePath = Resolve-VariablesCss
$source = [IO.File]::ReadAllText($sourcePath)
$blocks = [regex]::Matches($source, '(?s)(?<selector>[^{}]+)\{(?<body>[^{}]*)\}')
$outputLines = [Collections.Generic.List[string]]::new()
$sourceVariables = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$contextPairs = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)

$outputLines.Add('/*')
$outputLines.Add(" * $SchemeName")
$outputLines.Add(' * Generated from the installed K2 Variables_Dynamic.css contract.')
$outputLines.Add(" * Source SHA-256: $((Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant())")
$outputLines.Add(' * Regenerate and revalidate after every K2 upgrade.')
$outputLines.Add(' */')
$outputLines.Add('html:not(.designer) .theme-entry {')
$tokens = [ordered]@{
    accent = $Accent; 'accent-strong' = $AccentStrong; 'accent-soft' = $AccentSoft
    'accent-subtle' = $AccentSubtle; 'on-accent' = $OnAccent; page = $Page
    surface = $Surface; 'surface-alt' = $SurfaceAlt; text = $Text; muted = $Muted
    border = $Border; focus = $Focus; danger = $Danger; 'danger-soft' = $DangerSoft
    warning = $Warning; 'warning-soft' = $WarningSoft; success = $Success
    'success-soft' = $SuccessSoft; 'disabled-surface' = $DisabledSurface
    'disabled-text' = $DisabledText; shadow = $Shadow
}
foreach ($entry in $tokens.GetEnumerator()) { $outputLines.Add("  --k2sp-$($entry.Key): $($entry.Value);") }
for ($index = 0; $index -lt $ChartSeries.Count; $index++) {
    $outputLines.Add("  --k2sp-chart-$index`: $($ChartSeries[$index]);")
}
$outputLines.Add('}')
$outputLines.Add('')

foreach ($block in $blocks) {
    $declarations = @([regex]::Matches($block.Groups['body'].Value, '(?m)--(?<name>[A-Za-z0-9_-]+)\s*:\s*(?<value>[^;]+?)(?:;|$)') |
        Where-Object { Test-K2ColourVariable $_.Groups['name'].Value })
    if ($declarations.Count -eq 0) { continue }

    $rawSelector = $block.Groups['selector'].Value.Trim()
    $selectors = @($rawSelector -split ',' | ForEach-Object {
        $selector = ([regex]::Replace($_.Trim(), '\s+', ' '))
        if (-not $selector.StartsWith('.theme-entry', [StringComparison]::Ordinal)) {
            throw "Unexpected K2 variable selector: $selector"
        }
        "html:not(.designer) $selector"
    })
    $outputLines.Add(($selectors -join ",`n"))
    $outputLines.Add('{')
    $selectedContext = $rawSelector -match 'tr\.selected'
    $blockVariables = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($declaration in $declarations) {
        $name = $declaration.Groups['name'].Value
        if (-not $blockVariables.Add($name)) { continue }
        $null = $sourceVariables.Add($name)
        foreach ($selector in $selectors) { $null = $contextPairs.Add("$selector|$name") }
        $outputLines.Add("  --$name`: $(Get-MappedValue $name $selectedContext);")
    }
    $outputLines.Add('}')
    $outputLines.Add('')
}

if ($sourceVariables.Count -lt 100) {
    throw "Only $($sourceVariables.Count) K2 colour variables were found; the supplied variable contract is not credible."
}

$outputPath = [IO.Path]::GetFullPath($Output)
$parent = Split-Path -Parent $outputPath
if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
$utf8 = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText($outputPath, (($outputLines -join [Environment]::NewLine).TrimEnd() + [Environment]::NewLine), $utf8)

[ordered]@{
    output = $outputPath
    variablesCss = $sourcePath
    sourceSha256 = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant()
    colourVariables = $sourceVariables.Count
    contextualDeclarations = $contextPairs.Count
    bytes = (Get-Item -LiteralPath $outputPath).Length
} | ConvertTo-Json -Depth 4
