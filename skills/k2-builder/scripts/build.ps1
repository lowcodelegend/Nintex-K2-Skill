[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

$skillRoot = Split-Path -Parent $PSScriptRoot
$entryPoint = Join-Path $PSScriptRoot 'k2build.ps1'
$parseTokens = $null
$parseErrors = $null
[Management.Automation.Language.Parser]::ParseFile($entryPoint, [ref]$parseTokens, [ref]$parseErrors) | Out-Null
if ($parseErrors.Count -gt 0) {
    throw "k2build.ps1 has PowerShell parse errors: $($parseErrors.Message -join '; ')"
}

foreach ($assetName in @('solution-manifest.template.json', 'deployment-ledger.template.json')) {
    $assetPath = Join-Path $skillRoot ('assets\' + $assetName)
    Get-Content -LiteralPath $assetPath -Raw | ConvertFrom-Json | Out-Null
}

$skillContent = Get-Content -LiteralPath (Join-Path $skillRoot 'SKILL.md') -Raw
if ($skillContent -notmatch '(?s)^---\r?\nname: k2-builder\r?\ndescription: .+?\r?\n---(?:\r?\n|$)') {
    throw 'SKILL.md frontmatter is invalid.'
}

$agentContent = Get-Content -LiteralPath (Join-Path $skillRoot 'agents\openai.yaml') -Raw
if ($agentContent -notmatch '(?m)^\s*default_prompt:\s*"Use \$k2-builder .+"\s*$') {
    throw 'agents/openai.yaml default_prompt must name $k2-builder.'
}

$actualVersion = (& $entryPoint version | Out-String).Trim()
if ($actualVersion -cne 'k2build 0.1.0') {
    throw "Unexpected k2build version output: $actualVersion"
}

Write-Output "k2-builder 0.1.0 validation passed ($Configuration)."
